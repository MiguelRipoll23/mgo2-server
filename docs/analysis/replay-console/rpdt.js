/* rpdt.js — MGO2 replay (.dat) parser for the browser.

   A port of replay_parse.py, revision 2026-08-21.  The two implementations
   MUST agree byte for byte on every emitted field; xcheck.py runs both over all
   18 corpus files and asserts it.  Anything the format spec marks undecoded is
   emitted as null here, never as a default.

   Output:
     {file, degenerate, stage, stageGuess, mapId, tickRate, duration,
      players[], frames[], events[], unknown, meta}
     frame.p[i] = 27-element cell | null   (meta.positionCell names the slots)

   Deliberately NOT ported: the collision-mesh ground-contact stage fit (the
   meshes are not available in a browser), --max-frames decimation and the
   long-form provenance notes.  Coverage accounting IS ported, because it is the
   strongest single cross-check between the two decoders. */
(function (global) {
'use strict';

var MAGIC = 0x52504454;      // 'RPDT'
var HDR_LEN = 12;
var SCALE = 10;              // world = raw * 10   [CONFIRMED]
var WORLD_UNITS_PER_METRE = 1000;
var BODY_OFFSET = 800;       // position origin -> feet
var VITAL_MAX = 250;

// [C] POSITION BLOCK OFFSET, revision 2026-08-21 (RPDT_FORMAT.md 4.1, 0d #1).
// "always the last 10 bytes" is CORRECT for the base and long forms and WRONG
// for LEN 15, 16 and 18, which append a TRAILER and keep the head offset.
var POS_TAIL = 10;                 // the LONG forms' block, payload[L-10..L-3]
var POS_BASE_LEN = [14, 17];       // the per-tick trajectory record
var POS_SHORT_X = { 0: 4, 1: 4, 2: 7 };
// LEN 19 (v2) added 2026-08-21b: 3716 records in the HELD-OUT file
// replay_351754_9, ZERO in the 19-file corpus, and the previous closed
// whitelist dropped every one of them in silence.  Offset sweep xo = 0..13
// puts the argmin at xo = 7 (the standard v2 head offset), median L1 263 raw
// to the object's own base track against 4893 for the runner-up.  Anything
// this table cannot place is now COUNTED into meta.unknownPositionForms.
var POS_SHORT_LEN = { 0: [14, 15, 16], 1: [14, 15, 16], 2: [17, 18, 19] };
var POS_LONG_LEN = [33, 36, 39, 45, 67];
var POS_NOT_A_POSITION = [41];
// verified-constant payload bytes, CHECKED AT RUNTIME -- {ver: {LEN: {idx: v}}}
var POS_CONST = {
  0: { 14: { 13: 0x00 }, 15: { 12: 0x00, 13: 0x00 }, 16: { 12: 0x00, 13: 0x00 } },
  1: { 14: { 12: 0x00, 13: 0x00 }, 15: { 12: 0x00, 13: 0x00 },
       16: { 12: 0x00, 13: 0x00 },
       36: { 20: 0, 21: 0, 22: 0x20, 23: 0, 24: 8, 25: 0, 34: 0, 35: 0 } },
  2: { 17: { 4: 0, 6: 0, 15: 0, 16: 0 }, 18: { 4: 0, 6: 0, 15: 0, 16: 0 },
       19: { 4: 0, 6: 0, 15: 0, 16: 0 },
       33: { 4: 0x0b, 6: 0, 31: 0, 32: 0 },
       39: { 21: 0, 24: 8, 25: 0, 26: 0, 28: 0, 37: 0, 38: 0 },
       45: { 4: 6, 6: 0, 7: 0, 8: 0, 43: 0, 44: 0 } }
  // v2 LEN 67 is 109 records in ONE file: nothing is declared or credited.
};
// a u16 that DUPLICATES the block yaw exactly (26926/26926 over both v2 files)
var POS_YAWDUP = { 2: { 33: 21 } };
var POS_KD_LEN = [17, 18, 19, 33, 45];      // v2 payload[5] down-episode counter
var POS_ANCHOR_LEN = [36, 39, 67];
var POS_RAGDOLL_OFF = { 33: 9, 45: 21 };
function posConst(ver, L) {
  var t = POS_CONST[ver];
  return (t && t[L]) || null;
}
function posYawDup(ver, L) {
  var t = POS_YAWDUP[ver];
  return (t && t[L] !== undefined) ? t[L] : null;
}
function posXOffset(ver, L) {
  var sh = POS_SHORT_LEN[ver];
  if (sh && sh.indexOf(L) >= 0) return POS_SHORT_X[ver];
  if (POS_LONG_LEN.indexOf(L) >= 0) return L - POS_TAIL;
  return null;
}
/* The EXACT payload byte indices the type-0x02 decoder dereferences, derived
   from the same tables the decoder uses so the coverage credit cannot drift
   away from the code.  Returns a count, or -1 for a form it cannot place. */
function posBytesRead(ver, L) {
  var xo = posXOffset(ver, L);
  if (xo === null) return -1;
  var seen = {}, i;
  seen[0] = 1; seen[1] = 1; seen[2] = 1;
  for (i = xo; i < xo + 8; i++) seen[i] = 1;
  var c = posConst(ver, L);
  if (c) Object.keys(c).forEach(function (k) { seen[+k] = 1; });
  if (ver >= 2 && POS_KD_LEN.indexOf(L) >= 0) seen[5] = 1;
  var d = posYawDup(ver, L);
  if (d !== null) { seen[d] = 1; seen[d + 1] = 1; }
  if (POS_ANCHOR_LEN.indexOf(L) >= 0) for (i = 4; i < 16; i++) seen[i] = 1;
  var r = POS_RAGDOLL_OFF[L];
  if (r !== undefined) for (i = r; i < r + 12; i++) seen[i] = 1;
  return Object.keys(seen).length;
}
function posConstCount(ver, L) {
  var c = posConst(ver, L), n = c ? Object.keys(c).length : 0;
  if (posYawDup(ver, L) !== null) n += 2;
  return n;
}
var TYPE_DAMAGE = 0x00, TYPE_STATE = 0x01, TYPE_POS = 0x02, TYPE_HEALTH = 0x03,
    TYPE_WEAPON = 0x04, TYPE_AIM = 0x05, TYPE_INCAP = 0x06, TYPE_ROSTER = 0x07,
    TYPE_THROW = 0x08, TYPE_SETTINGS = 0x0b;
var TEAM_ARRAY_TYPES = [0x85, 0x86], TEAM_ARRAY_LEN = 26, TEAM_ARRAY_SLOTS = 24,
    TEAM_ARRAY_OCCUPIED = 0x07;
var TSNE_RULE = 7, WEAPON_SNEAK = 16;
var LOADOUT_LEN = [50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62];   // 52 and 59 seen 2026-10-11 (match 69641 Jotei; UU TSNE built-in, Lust For Life): the list used to stop short and those players had no look and no skills. 60-62 allowed ahead of need.
var SKILL_APPEARANCE = 23, SKILL_LIST_END = 0xff, SKILL_REGION_SUFFIX = 0x10;
var SKILL_NAMES = ['HAND_MAS', 'SBMC_MAS', 'ASSLT_MAS', 'SHOT_MAS',
  'SNIPPER_MAS', 'ZOOM', 'LOCKON', 'THROW', 'TRAP', 'CQC', 'KNIFE', 'FASTMOV',
  'ENEMY', 'AWARE', 'ALERT', 'SCANNING', 'OFFICER', 'RECOVER', 'CBOX', 'CQC_EX',
  'SHIELD_MAS', 'SCANNING_EX', 'SOP_JAMMING', 'TOUGHNESS', 'CHARM'];
// RPDT_FORMAT.md 4.1.3: 27 of the 44 observed motionState values carry a
// reading and cover 99.8884% of samples.  The other 17 stay null.
var MOTION_NAME = {
  0x00: 'stand idle', 0x28: 'stand, bit7 family',
  0x01: 'walk', 0x02: 'slow walk, weapon out',
  0x29: 'walk backward',
  0x81: 'run forward', 0xa8: 'run, bit7 family', 0xa9: 'run, fastest variant',
  0x03: 'airborne / falling',
  0x11: 'climb up', 0x12: 'descend',
  0x13: 'climb / step down over a ledge',
  0x0a: 'wall / ledge hold, entry transient', 0x0b: 'wall / ledge hold',
  0x2c: 'at the water surface', 0x2d: 'water entry / exit', 0x2e: 'swimming',
  0x05: 'stationary aim / scoped',
  0x17: 'bullet flinch', 0x18: 'explosion knockback',
  0x06: 'downed, lying on the ground', 0x04: 'get up / land',
  0x1b: 'incapacitated', 0x1c: 'gas choke',
  0x1a: 'dead', 0x19: 'dead, second state',
  0x0f: 'reload pose (one file)', 0x09: 'time bomb equipped (one file)' };
var MOTION_GROUP = {};
[['standing', [0x00, 0x28]], ['walking', [0x01, 0x02]],
 ['walking backwards', [0x29]], ['running', [0x81, 0xa8, 0xa9]],
 ['airborne', [0x03]], ['climbing up', [0x11]], ['descending', [0x12, 0x13]],
 ['holding a wall or ledge', [0x0a, 0x0b]], ['in water', [0x2c, 0x2d, 0x2e]],
 ['aiming, still', [0x05]], ['hit reaction', [0x17]], ['blown back', [0x18]],
 ['down on the ground', [0x06]], ['getting up', [0x04]],
 ['incapacitated', [0x1b]], ['choking', [0x1c]], ['dead', [0x1a, 0x19]]
].forEach(function (g) { g[1].forEach(function (v) { MOTION_GROUP[v] = g[0]; }); });
// Readings that do NOT reproduce off format version 0.  0x03 is a real
// ballistic signature on v0 (vertical p05 -787.9 world u/s, n=4012) and is
// STATIONARY on v1 (33/33 zero) and v2 (horizontal p50 = p90 = 0.0), so the
// name and the group are null there rather than mislabelling 308 samples.
var MOTION_NAME_V0_ONLY = [0x03];
function motionName(v, ver) {
  if (ver && MOTION_NAME_V0_ONLY.indexOf(v) >= 0) return null;
  var nm = MOTION_NAME[v];
  return nm === undefined ? null : nm;
}
function motionGroup(v, ver) {
  if (ver && MOTION_NAME_V0_ONLY.indexOf(v) >= 0) return null;
  var gp = MOTION_GROUP[v];
  return gp === undefined ? null : gp;
}
/* `facingLocked` WITHDRAWN 2026-08-21b.  Its criterion -- yaw rate 0.0 deg/s
   at p50 AND p90 -- cannot fail for any state that turns in under 10% of
   frames, and the direct statistic refutes it and does not even rank right:
   0x28 changes yaw in 8.839% of consecutive same-state pairs and 0x0B in
   7.194%, both shipped as FROZEN, while 0x16 (0.505%) and 0x0E (0.000%) were
   shipped as null.  The MEASUREMENT replaces the boolean.
   MOTION_YAW_CHANGE = {value: [pairs, pairsWithAnyYawChange, files]} over the
   19-file corpus plus the held-out file. */
var MOTION_YAW_CHANGE = {
  0x81: [1168293, 1161251, 18], 0x00: [574629, 182521, 19],
  0x1a: [428143, 103528, 16], 0x02: [90191, 68924, 18],
  0x01: [71057, 70547, 18], 0x18: [26404, 25362, 17],
  0xa8: [17863, 17787, 18], 0x28: [11800, 1043, 17],
  0x12: [11725, 1499, 18], 0x0b: [8757, 630, 14],
  0x1c: [8371, 713, 10], 0x2e: [5255, 3547, 2],
  0x03: [4320, 2616, 18], 0x05: [3726, 1998, 16],
  0x06: [3232, 2123, 17], 0x04: [3152, 902, 17],
  0x11: [2980, 2, 15], 0x17: [2415, 1909, 17],
  0x2c: [2266, 1574, 2], 0x1b: [1947, 244, 6],
  0x29: [1682, 133, 15], 0x0a: [1358, 473, 16],
  0x13: [1352, 39, 10], 0x2d: [1125, 10, 2],
  0xa9: [1107, 1105, 3], 0x15: [499, 99, 10],
  0x32: [460, 0, 2], 0x30: [397, 247, 1],
  0x33: [373, 0, 4], 0x3a: [236, 1, 2],
  0x1d: [222, 0, 5], 0x19: [214, 8, 2],
  0x39: [206, 0, 2], 0x16: [198, 1, 3],
  0x1f: [182, 121, 9], 0x22: [75, 47, 3],
  0x1e: [55, 38, 4], 0x0f: [51, 0, 1],
  0x42: [50, 5, 3], 0x09: [34, 33, 1],
  0x38: [21, 21, 1], 0x82: [18, 6, 2],
  0x0e: [12, 0, 1]
};
// the motionState lookup, so a size-sensitive consumer can DROP cells 20-21.
// {value: [name, group, yawChangePairs, yawChangedPairs, files]}.  Built per
// file because the name is version-scoped.
function motionStates(ver) {
  var out = {}, vals = [];
  Object.keys(MOTION_NAME).forEach(function (k) { vals.push(+k); });
  Object.keys(MOTION_YAW_CHANGE).forEach(function (k) {
    if (vals.indexOf(+k) < 0) vals.push(+k);
  });
  vals.sort(function (x, y) { return x - y; });
  vals.forEach(function (v) {
    var yc = MOTION_YAW_CHANGE[v] || [null, null, null];
    out[v] = [motionName(v, ver), motionGroup(v, ver), yc[0], yc[1], yc[2]];
  });
  return out;
}
// The frame cell, in EMISSION ORDER.  APPEND ONLY.
var POSITION_CELL = ['x', 'y', 'z', 'headingDeg', 'alive', 'health01',
  'stamina01', 'stateFlags', 'motionState', 'aimX', 'aimY', 'aimZ',
  'itemId', 'ammo', 'action', 'targetSlot', 'incapLevel',
  'targetPointX', 'targetPointY', 'targetPointZ',
  'motionName', 'motionGroup',
  'aimOriginSide', 'itemAttr', 'knockdownCount', 'itemFlag'];
// [C] non-lethal damage is a TRANQUILLISER hit; 0x42 is a headshot, 0x41 a body
// hit.  Emitted ALWAYS, like the in-game replay killfeed.
var STUN_SUBTYPE = { 0x41: 'body', 0x42: 'head' };
// the damage subtype's hit zone, for the kill feed.  0x91 ADDED 2026-08-21b:
// it was 107 of the 2065 corpus kills with no zone, and its profile is BODY
// (median 56.3 / p90 68.8 against body 0x11 50.6/56.4 and head 0x12
// 250.1/250.2 = an outright kill), n=328 over 16 files.  0x13 (GRENADE) and
// 0x0E (SMOKE G) are damage SOURCES, not zones, and stay absent.
var HIT_ZONE = { 0x11: 'body', 0x12: 'head', 0x91: 'body',
                 0x41: 'body', 0x42: 'head' };
// `wake` WITHDRAWN 2026-08-21b: ALL 31 of these events in the 19-file corpus
// follow the victim's OWN death -- 27 of them in a band of 8.066-8.267 s -- i.e.
// they are the respawn, not a revive.  The event
// is renamed to what is measured and carries sincePlayerDeathSec.  Threshold:
// 1282 of 1287 positive stamina steps are +1..+3 and the next observed step is
// +31, so the cut sits at +4 raw, at the bottom of the measured gap.
var STAMINA_JUMP = 4 / 250, STAMINA_MASS = 3, RESPAWN_WINDOW = 20.0;
var ROSTER_NAME_OFF = 68;
/* Replays recorded from 2026-10-07 on carry a roster record 12 bytes shorter
   (measured on replay_372920_2 / 372921_3 / 372921_4): the name starts at +56
   and the clan id sits at +19 instead of +31.  The header still says format
   version 2, so the layout is told apart per record: the record always ENDS
   with  nick NUL clan  and the byte before the nick is 0x00 or 0x03, so the
   nick is the run of printable bytes that ends at the last-but-one NUL. */
var ROSTER_NAME_OFF_SHORT = 56, ROSTER_MIN_LEN = ROSTER_NAME_OFF_SHORT + 2;
function rosterNameOff(b, a, e) {
  var z = e - 1;
  while (z > a && b[z] !== 0) z--;                 // the NUL that ends the nick
  var s = z;
  while (s > a && b[s - 1] >= 0x20) s--;           // back over the nick itself
  return (s - a === ROSTER_NAME_OFF_SHORT && z > s) ? ROSTER_NAME_OFF_SHORT : ROSTER_NAME_OFF;
}
var VID_DAMAGE = -5, VID_STATE = -2, VID_HEALTH = -1, VID_WEAPON = 2, VID_AIM = 4;
var KIND_ROSTER = 0x02, KIND_SETTINGS = 0x0a;

var RULES_VIDS = [0x261, 0xa61];
var OPS_VID_LOW = 0x254;
var GAUGE_LOW_MIN = 0x267, GAUGE_LOW_MAX = 0x26b;
var OP_STATE = 0x00, OP_CLOCK = 0x01, OP_WRITE = 0x02, OP_KILL = 0x09,
    OP_RATING = 0x13;
// state[42..43] are NOT holder fields: in the CAP recording state[42] runs
// 29,28,27...0,30,29..., a countdown.  Only state[40] and state[41] are read.
var STATE_HOLDER0 = 40, STATE_HOLDER1 = 41, HOLDER_NOBODY = 0x3f;
var HOLDER_NONE = 0xff, HOLDER_FLAG = 0x40;
var CLOCK_MS_PER_UNIT = 125.0;
var LIST_END = 0xfe;

var TICK_RESET_SLACK = 1000, TICK_RESET_GAP = 1000, TICK_FORWARD_MAX = 600000;
var POS_MATCH_WINDOW = 300, KILL_LOOKBACK_MS = 2000;
var OP9_MATCH_MIN = -500, OP9_MATCH_MAX = 3000;
var BEARING_TOL = 0.15, AMBIG_RATIO = 2.0, AMBIG_ABS = 0.02;
var MIN_FIT = 0.5, MIN_SAMPLES = 3, MIN_C_SAMPLES = 20;
var PARK_MIN_SAMPLES = 30, LOW_KILL_RATE = 0.10, ONE_LIFE_RESPAWN_RATIO = 0.10;

var MODE_NAMES = { 0: 'DM', 1: 'TDM', 2: 'RES', 3: 'CAP', 4: 'SNE', 5: 'BASE',
                   6: 'BOMB', 7: 'TSNE', 12: 'SDM', 13: 'INTERVAL', 15: 'SCAP',
                   16: 'RACE' };

var MAPS = {
  1: ["n001a", "Forest Firefight"],
  2: ["n002a", "Ambush Alley"],
  3: ["n003a", "Urban Ultimatum"],
  4: ["n004a", "Gronznyj Grad"],
  5: ["n005a", "Coppertown"],
  6: ["n006a", "Hazard House"],
  7: ["n007a", "Blood Bath"],
  8: ["n008a", "Tomb of Tubes"],
  9: ["n009a", "Ravaged Riverfront"],
  10: ["n010a", "Silo Sunset"],
  11: ["n018a", "Icebound Inferno"],
  12: ["n012a", "Midtown"],
  13: ["n013a", "Winter Warehouse"],
  14: ["n014a", "Virtuous Vista"],
  15: ["n015a", "Outer Outlet"],
  16: [null, "(unknown)"],
  17: ["sm_dd", "Desert Duel"],
  18: ["sm_ll", "Lethal Leviathan"],
  // 19-22 were one stage off until 2026-10-07. MEASURED by standing the tracks of real
  // matches on each stage's floor: map 21 (match 68994) = n023a 98%, map 22 (match 69003)
  // = n020a 100%, every other stage ~0%. 19 and 20 follow the game's own stage table
  // (lobby scenerio.gcx: 19 = n024a, 20 = n022a); no match on either was found to test.
  // n024a has no scenery in the viewer yet, so Perilous Palace draws players only.
  19: [null, "Perilous Palace"],
  20: ["n022a", "Quaint Quarrel"],
  21: ["n023a", "Jade Junction"],
  22: ["n020a", "Eastern Europe"]
};

var ITEM_NAMES = ["EMPTY","NONE","ST KNIFE","RUGER","OPERATOR","SOCOM","D EAGLE","FIVESEVEN","SIG GSR","D EAGLE L","THOR","M1911A1","COOLANT","GUN DEL SOL","PSS","GLOCK","R KNIFE","MP7","MP5","INGRAM","P90","BIZON","PATRIOT","SKORPION","M4","AK102","G3A3","XM8SHORT","FAL","TANTO","MK17","XM8","LMG","HK21E","PK","M60E4","V KNIFE","M870","SAIGA","VSS","M82A2","DSR1","M14EBR","MOSIN N","DRAGNOV","RAILGUN","TIME BOMB","MISSOURI","HF BLADE","JAVELIN","RPG7","M72A3","GRENADE","WP G","STUN G","CHAFF G","SMOKE G","SMOKE G.(R)","SMOKE G.(G)","SMOKE G.(Y)","SMOKE G.(P)","PETRO BOMB","MAGAZINE","EMP GRENADE","CLAYMORE","SLEEP CLAYMORE","C4","SLEEP C4","BASE BUSTER","BOOK","PSEUDO BOOK","G.O.P.","SONAR","SHIELD","SBS","XM320","GP30","SURPRESSOR","SCOPE","DOTSIGHT","MP7 DOTSIGHT","LIGHT","LASERSIGHT","GRIP A","GRIP B","REX VULCAN","REX MISSILE","REX LASER","Nighthawk","AK KNIFE","Throw Knife","Throw Machete","WP_ParaCord","XM8 Short","XM8 Long","XM8 Jona","MORTARBOMB","M2","CORE","KICK","PUNCH","STOMP","ROLLING","CQC","ROLLING DRUM","MK2 SPARK","DOOR ATTACK","DRUMCAN","GROUND FIRE","CBOX TACKLE","C4 TRIGGER","SLEEP C4 TRIGGER","INJECTION","CRITICAL KNIFE","EXPLOSION SMALL","EXPLOSION MIDDLE","EXPLOSION LARGE","TORNADO","GEKKOVULCAN","GEKKOMISSILE","GK KICK","GK STOMP","REX KICK","REX BODY","RAY VULCAN","RAY MISSILE","RAY CUTTER","RAY KICK","RAY BODY","BINOCULAR","ENVG","VEHICLE DAMAGE","VEHICLE SHAKE","VEHICLE NO DAMAGE","GRASS","DAM DEFENSE","PIPE GAS","DEATH","TOW","MINIGUN","KARAMBIT","STRYKERMOVE","STRYKERVULCAN","STRYKE105MM","BMP3MOVE","BMP3VULCAN","BMP3CANNON","CANARDVULCAN","CANARDMISSILE","HUMVEEMOVE","HUMVEEVULCAN","OCTOPUSBODY","OCTOPUSTENTACLE","OCTOPUSCAPTURE","OCTOPUSBALL","OCTOPUSELECTRIC","VAMPTANTO","VAMPTANTOFINISH","VAMPKNIFE","VAMPKICK","VAMPBODY","PLAYERSIGHT","RAVENCHARGE","RAVENSLIDERCHARGE","RAVENSLIDERMISSILE","RAVENSLIDERCLUSTER","LIQUIDLEFTPUNCH","LIQUIDHEADBAT","RATION","E. BAR","E. DRINK","PENTAZEMIN","STUPE","S Eye","MG MkII","CAMERA","C BOX A","iPod","RADIO","CIGAR","MUNA","BANDANA","STEALTH","SYRINGE","S. PLUG","BINO.","S.PLUG_SPR","FACECAM","KEROTAN","GAKO","OCTOCAM","CLOTH ME","CLOTH SA","CLOTH EE","ALTAIR","CLOTH_SUIT","YOUNG","S BEAUTY","L BEAUTY","R BEAUTY","C BEAUTY","YOUNG B","OTACON","CAMBELL","BOGBOSS","DREBIN","MGS1","RAIDEN","RAIDEN_OPEN","MID EAST","SOUTH AMERICA","EAST EUROPE","MOSES","NAVY","GREEN","BLUE","ORANGE","HAVEN","OCTOPUS","RAVEN","WOLF","MANTIS","BATTERY","MASK","EXTEND 1","EXTEND 2","EXTEND 3","EXTEND 4","EXTEND 5","IPOD MUSIC","WP_BulNone","WP_BulTranq","WP_BulScreaming","WP_BulLaughing","WP_BulRaging","WP_BulCrying","WP_BulRifleTranq","WP_BulRifleScreaming","WP_BulRifleLaughing","WP_BulRifleRaging","WP_BulRifleCrying","WP_BulSuper","WP_BulAcp","WP_BulGov","WP_BulAe","WP_BulBmr","WP_BulCal","WP_BulAn94","WP_BulNato45","WP_BulSs190","WP_BulPss","WP_BulNato51","WP_Bul54r","WP_Bul18","WP_Bul19","WP_BulVss","WP_BulMagnum","WP_BulTane","WP_BulShotSpread","WP_BulShotSlug","WP_BulShotAir","WP_BulLauncherGp30","WP_BulLauncher","WP_BulLauncherFire","WP_BulLauncherStun","WP_BulLauncherSmoke","WP_BulAirBurst","WP_BulMissile","WP_BulWarhead","WP_BulRocket","WP_BulRocket2","WP_BulGrenade","WP_BulFireGrenade","WP_BulStunGrenade","WP_BulChaffGrenade","WP_BulSmokeGrenade","WP_BulSmokeGrenadeRed","WP_BulSmokeGrenadeGreen","WP_BulSmokeGrenadeYellow","WP_BulSmokeGrenadePurple","WP_BulPetroBomb","WP_BulMagazine","WP_BulClaymore","WP_BulSleepClaymore","WP_BulC4","WP_BulSleepC4","WP_BulBaseBomb","WP_BulBook","WP_BulAffectBook","WP_BulRailGun","WP_BulSurpOperator","WP_BulSurpSocom","WP_BulSurpGove","WP_BulSurpIngram","WP_BulSurpP90","WP_BulSurpM4","WP_BulSurpM14ebr"];
/* ---- helpers ----------------------------------------------------------- */

/* Half-UP rounding, matching replay_parse._round.  Python's round() is
   half-to-EVEN, so the two languages disagree on exact ties (yaw hits one for
   every raw value that is an odd multiple of 1024).  Both use this. */
function round(v, nd) {
  var m = Math.pow(10, nd);
  return Math.floor(v * m + 0.5) / m;
}

/* Python's cp1252 codec, including its five UNDEFINED bytes -> U+FFFD.  The
   browser's TextDecoder maps those to C1 controls instead, which would make the
   two decoders disagree on a player name. */
var CP1252_HI = [
  0x20AC, 0xFFFD, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
  0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0xFFFD, 0x017D, 0xFFFD,
  0xFFFD, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
  0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0xFFFD, 0x017E, 0x0178];
function decodeName(u8) {
  var s = '';
  for (var i = 0; i < u8.length; i++) {
    var c = u8[i];
    s += String.fromCharCode(c >= 0x80 && c <= 0x9f ? CP1252_HI[c - 0x80] : c);
  }
  return s;
}

/* Index of the sample nearest t, or -1 when none is within POS_MATCH_WINDOW.
   Ties resolve to the EARLIER sample.  Mirrors replay_parse._nearest. */
function nearest(times, t) {
  if (!times.length) return -1;
  var i = bisectLeft(times, t), best = -1, bd = null;
  for (var k = 0; k < 2; k++) {
    var j = i - 1 + k;
    if (j >= 0 && j < times.length) {
      var d = Math.abs(times[j] - t);
      if (bd === null || d < bd) { bd = d; best = j; }
    }
  }
  return (best < 0 || bd > POS_MATCH_WINDOW) ? -1 : best;
}
function bisectLeft(a, t) {
  var lo = 0, hi = a.length;
  while (lo < hi) { var m = (lo + hi) >> 1; if (a[m] < t) lo = m + 1; else hi = m; }
  return lo;
}
function bisectRight(a, t) {
  var lo = 0, hi = a.length;
  while (lo < hi) { var m = (lo + hi) >> 1; if (a[m] <= t) lo = m + 1; else hi = m; }
  return lo;
}
function medianInt(v) {
  var s = v.slice().sort(function (a, b) { return a - b; });
  return s[Math.floor(s.length / 2)];
}
function itemName(i) {
  if (i === null || i === undefined) return null;
  return (i >= 0 && i < ITEM_NAMES.length) ? ITEM_NAMES[i] : null;
}
function nestedGet(m, vid, t) {
  var inner = m.get(vid);
  if (!inner) return undefined;
  return inner.get(t);
}
// THE LAST VALUE AT OR BEFORE t. SaveMGO recordings from about match 69000 on write a player's weapon / ammo record and his
// action record only when they CHANGE (17,358 weapon records against 149,927 position samples in match 69195; older files
// have one per sample). Looked up by exact time, 90 % of his samples came back with no weapon, no ammo and no action:
// empty hands, no shots counted, accuracy blank (user, 2026-10-08, on his own match 69195). Old files hit the exact
// entry every time, so nothing changes for them.
function carryGet(m, vid, t) {
  var inner = m.get(vid);
  if (!inner) return undefined;
  var v = inner.get(t);
  if (v !== undefined) return v;
  var k = inner.__keys;
  if (!k || k.__n !== inner.size) { k = Array.from(inner.keys()).sort(function (a, b) { return a - b; }); k.__n = inner.size; inner.__keys = k; }
  var lo = 0, hi = k.length - 1;
  if (!k.length || k[0] > t) return undefined;
  while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (k[mid] <= t) lo = mid; else hi = mid - 1; }
  return inner.get(k[lo]);
}
function nestedSet(m, vid, t, v) {
  var inner = m.get(vid);
  if (!inner) { inner = new Map(); m.set(vid, inner); }
  inner.set(t, v);
}

/* RPDT_FORMAT.md 7.8.1 -- the rules object's live-roster grammar,
     payload[0] = 0x00, then <GROUP> <slot>* 0xFE ... , terminated by 0xFE.
   Returns null when it does not consume EXACTLY LEN (then it is credited zero
   coverage bytes), otherwise the list of [group, slots]. */
function rulesRoster(b, a, L) {
  var i = 1, groups = [];
  while (i < L) {
    var g = b[a + i];
    i += 1;
    if (g === LIST_END) return (i === L) ? groups : null;
    var members = [];
    while (i < L && b[a + i] !== LIST_END) { members.push(b[a + i]); i += 1; }
    if (i >= L) return null;
    i += 1;
    groups.push([g, members]);
  }
  return null;
}

/* RPDT_FORMAT.md 7.2.2 -- the subtype's high nibble is the damage class. */
function damageClass(subtype) {
  var nib = (subtype & 0x7f) >> 4;
  if (nib === 1) return 'lethal';
  if (nib === 2 || nib === 4) return 'nonLethal';
  if (nib === 0) return 'gas';
  return null;
}

/* RPDT_FORMAT.md 5 -- the type-0x0b LEN 50-58 loadout record.  Returns
   [slot, [[skillId, level], ...]], [slot, null] when the pair region cannot be
   aligned, or null when the record is not a loadout record.  It NEVER returns
   an empty list for a region it failed to parse: the previous revision did,
   and published "this player equipped no skills" for 15 of 342 corpus records
   that in fact carry 2-3 skills.  The 0x10 region SUFFIX is measured: 19 of
   354 records carry it, every one of them failed the old alignment, and every
   one yields ids 1..25 / levels 1..3 once it is dropped. */
var APPEARANCE_FIELDS = [
  'head', 'chest', 'hand', 'waist', 'foot', 'acc1', 'acc2',
  'headColor', 'chestColor', 'handColor', 'waistColor', 'footColor',
  'gender', 'face', 'upper', 'lower', 'facePaint', 'upperColor',
  'lowerColor', 'voice', 'pitch', 'acc1Color', 'acc2Color'];

/* The trailing 23 bytes of the loadout record, named -- see _appearance_record
   in replay_parse.py for what is measured (item keys, gender 0 = male) and
   what is inferred (the colour bytes' order).  Returns [slot, object] or null. */
function appearanceRecord(b, a, L) {
  if (LOADOUT_LEN.indexOf(L) < 0 || b[a] !== TYPE_SETTINGS || b[a + 1] < 1) {
    return null;
  }
  var o = {}, t = a + L - SKILL_APPEARANCE;
  for (var i = 0; i < APPEARANCE_FIELDS.length; i++) o[APPEARANCE_FIELDS[i]] = b[t + i];
  return [b[a + 1] - 1, o];
}

function skillRecord(b, a, L) {
  if (LOADOUT_LEN.indexOf(L) < 0 || b[a] !== TYPE_SETTINGS || b[a + 1] < 1) {
    return null;
  }
  var nn = b[a + 22];
  var end = L - SKILL_APPEARANCE - nn - 1;
  if (!(end >= 23 && end < L) || b[a + end] !== SKILL_LIST_END) return null;
  var rlen = end - 23;
  if (rlen > 0 && b[a + 23 + rlen - 1] === SKILL_REGION_SUFFIX) rlen -= 1;
  if (rlen < 2) return [b[a + 1] - 1, []];
  var cands = [3, 2, 1];
  for (var ci = 0; ci < cands.length; ci++) {
    var np = cands[ci];
    if (2 * np > rlen) continue;
    var o = a + 23 + rlen - 2 * np, cand = [], ok = true;
    for (var i = 0; i < np; i++) {
      var sid = b[o + 2 * i], lv = b[o + 2 * i + 1];
      if (!(sid >= 1 && sid <= SKILL_NAMES.length && lv >= 1 && lv <= 3)) {
        ok = false; break;
      }
      cand.push([sid, lv]);
    }
    if (ok) return [b[a + 1] - 1, cand];
  }
  return [b[a + 1] - 1, null];
}

/* RPDT_FORMAT.md 7.8.4 -- the indexed 0x85 / 0x86 LEN-26 lobby array.  Returns
   [Map(slot -> 0|1), nPairs, nOccupiedMarks].  The polarity of {0,1} is PER
   FILE and per SESSION, so each session is aligned against what is already
   assigned before merging.  Revision 2026-08-21b DROPPED the extra
   "skip a session whose array never discriminates" filter: ablated against the
   independent type-0x0b team byte over all 19 files it buys 0 agreement
   (190/1 with it, 190/1 without) and costs 19 emitted player slots. */
function teamArray(sessions) {
  var group = new Map(), pairs = 0, occupied = 0;
  sessions.forEach(function (S) {
    var rows = S.teamArr.slice().sort(function (x, y) {
      return (x[0] - y[0]) || (x[1] - y[1]) || (x[2] - y[2]);
    });
    var votes = new Map();
    for (var i = 0; i < rows.length - 1; i++) {
      var r1 = rows[i], r2 = rows[i + 1];
      if (r1[2] !== TEAM_ARRAY_TYPES[0] || r2[2] !== TEAM_ARRAY_TYPES[1]
          || r2[0] !== r1[0] || r2[1] !== r1[1] + 1 || r2[3][1] !== r1[3][1]) {
        continue;
      }
      pairs++;
      for (var sl = 1; sl < TEAM_ARRAY_SLOTS; sl++) {
        if (r2[3][2 + sl] !== TEAM_ARRAY_OCCUPIED) continue;
        occupied++;
        var v = r1[3][2 + sl];
        if (v !== 0 && v !== 1) continue;
        var c = votes.get(sl);
        if (!c) { c = [0, 0]; votes.set(sl, c); }
        c[v]++;
      }
    }
    var local = new Map();
    votes.forEach(function (c, sl) { local.set(sl, c[1] > c[0] ? 1 : 0); });
    var same = 0, diff = 0;
    local.forEach(function (v, sl) {
      if (!group.has(sl)) return;
      if (group.get(sl) === v) same++; else diff++;
    });
    var flip = diff > same;
    local.forEach(function (v, sl) { group.set(sl, flip ? 1 - v : v); });
  });
  return [group, pairs, occupied];
}

function Session(index) {
  this.index = index;
  this.players = [];
  this.bySlot = new Map();
  this.teams = new Map();          // slot -> team byte
  this.rotation = [];
  this.pos = new Map();            // vid -> [[t,x,y,z,yaw,b1,b2,kd], ...]
  this.hp = new Map();             // posvid -> [[t,hp], ...]
  this.st = new Map();             // posvid -> [[t,byte2], ...]
  this.weapon = new Map();
  this.dmg = [];                   // {vid, t, a, L}
  this.aim = new Map();            // posvid -> Map(t -> [x,y,z])
  this.item = new Map();           // posvid -> Map(t -> [id, ammo])
  this.act = new Map();            // posvid -> Map(t -> [action, targetSlot])
  this.tail = new Map();           // posvid -> Map(t -> [tx,ty,tz]) stage-local
  this.incap = new Map();          // posvid -> Map(t -> level)
  this.ops = new Map();            // opcode -> [[t, a, L], ...]
  this.world = [];                 // objects 0x1250 / 0x125a: [t, low vid, type, a, L]
  this.notice = [];                // object 0x1258 type 0x29: [t, slot, kind, count]
  this.destab = [];                // object 0x1258 type 0x13 / 0x16: [t, slot, type]
  this.target = [];                // objects 0x1256 / 0x1257: [t, low vid, type, a]
  this.stat = [];                  // the scoreboard stream: [t, a, L]
  this.gauge = new Map();          // gauge low vid -> [[t, value], ...]
  this.rosterGroups = new Map();   // group marker -> Set(slots)
  this.rulesRecords = 0;
  this.rulesGrammarOk = 0;
  this.pvar = [];                  // non-base type-0x02 sub-forms
  this.frozen = [];                // destabilized samples: [vid, t, trailer byte]
  this.partner = [];               // [vid, t, slot acted on]
  this.skills = new Map();         // slot -> [[skillId, level], ...]
  this.appear = new Map();         // slot -> appearance object
  this.teamArr = [];               // [t, idx, type, payload]
  this.throw_ = [];                // [posvid, t, itemId, [x,y,z]]
  this.slotOf = new Map();
  this.genOf = new Map();
  this.stream = new Set();
}

function pushList(m, k, v) {
  var a = m.get(k);
  if (!a) { a = []; m.set(k, a); }
  a.push(v);
}

/* ---- container walk ---------------------------------------------------- */
/* One player's roster record (type 0x07, RPDT_FORMAT.md 3) -> {slot, joinCounter, charId,
   clanId, nick, clan}, or null for the chunk-framing records that carry no name. */
function rosterPlayer(b, dv, a, e, L) {
  if (L < ROSTER_MIN_LEN || b[a] !== TYPE_ROSTER) return null;
  var nameOff = rosterNameOff(b, a, e);
  if (L < nameOff + 1) return null;
  var tail = b.subarray(a + nameOff, e);
  var z = tail.indexOf(0);
  var nick = decodeName(z < 0 ? tail : tail.subarray(0, z));
  var clan = '';
  if (z >= 0) {
    var rest = tail.subarray(z + 1);
    var z2 = rest.indexOf(0);
    clan = decodeName(z2 < 0 ? rest : rest.subarray(0, z2));
  }
  return { slot: b[a + 5], joinCounter: b[a + 4],
           charId: dv.getUint32(a + 8, true),
           clanId: dv.getUint16(a + (nameOff === ROSTER_NAME_OFF_SHORT ? 19 : 31), true), nick: nick, clan: clan };
}
function walk(b, dv, n, cb) {
  var off = HDR_LEN, chunks = 0;
  while (off + 6 <= n) {
    var tick = dv.getUint32(off, true);
    var raw = dv.getUint16(off + 4, true);
    var size = raw & 0x7fff, ext = raw >> 15;
    var kind = null, hdrlen = 6;
    if (ext) {
      if (off + 9 > n) throw new Error('truncated ext chunk header at ' + off);
      kind = b[off + 8]; hdrlen = 9;
    }
    var p0 = off + hdrlen, p1 = p0 + size;
    if (p1 > n) {
      // SaveMGO match 45343: 2,821 good chunks, then the file ends 218 bytes into a 692-byte
      // one. A cut-off tail loses a fraction of a second; refusing the file loses the match.
      if (!chunks) throw new Error('chunk at ' + off + ' runs past EOF');
      return { chunks: chunks, walkEnd: n, truncatedAt: off };
    }
    chunks++;
    cb(off, tick, size, kind, hdrlen, p0, p1);
    off = p1;
  }
  return { chunks: chunks, walkEnd: off, truncatedAt: null };
}
function records(b, p0, p1, cb) {
  var o = p0;
  while (o < p1) {
    if (o + 3 > p1) throw new Error('record header past chunk end at ' + o);
    var vid = b[o] | (b[o + 1] << 8);
    var L = b[o + 2];
    var indexed = (vid & 0x1000) !== 0;
    var f = indexed ? 4 : 3;
    var a = o + f, e = a + L;
    if (e > p1) throw new Error('record at ' + o + ' overruns chunk end');
    cb(vid, indexed, f, L, a, e);
    o = e;
  }
}

/* ---- identity: vid = base + 0x800*gen + 10*slot ------------------------ */
function norm(vid) { return vid - 0x800 * (vid >> 11); }

function resolveIdentity(sessions, union) {
  var gens = new Map();                       // "si:g" -> [vids]
  sessions.forEach(function (S) {
    S.pos.forEach(function (series, vid) {
      if (series.length >= MIN_SAMPLES) {
        S.stream.add(vid);
        var g = vid >> 11;
        S.genOf.set(vid, g);
        var key = S.index + ':' + g;
        if (!gens.has(key)) gens.set(key, []);
        gens.get(key).push(vid);
      }
    });
  });
  var slots = Array.from(union.keys()).sort(function (a, b) { return a - b; });
  var cands = 0;
  gens.forEach(function (v) { cands += v.length; });
  if (!slots.length || !cands) {
    return { base: null, fitted: 0, candidates: cands, matchedPct: null,
             reason: (!slots.length ? 'no roster player records in this file'
                                    : 'no streaming position objects'),
             gens: [], rosterSlots: slots };
  }
  var votes = new Map();
  gens.forEach(function (vids) {
    vids.forEach(function (v) {
      for (var i = 0; i < slots.length; i++) {
        var k = norm(v) - 10 * slots[i];
        votes.set(k, (votes.get(k) || 0) + 1);
      }
    });
  });
  var base = null, bestN = -1;
  votes.forEach(function (c, k) { if (c > bestN) { bestN = c; base = k; } });

  var keys = Array.from(gens.keys()).sort(function (x, y) {
    var xs = x.split(':').map(Number), ys = y.split(':').map(Number);
    return xs[0] - ys[0] || xs[1] - ys[1];
  });
  var info = [], fitted = 0;
  keys.forEach(function (key) {
    var parts = key.split(':').map(Number), si = parts[0], g = parts[1];
    var S = sessions[si], vids = gens.get(key), hit = [], lattice = [];
    vids.forEach(function (v) {
      var d = norm(v) - base;
      if (d >= 0 && d % 10 === 0) {
        lattice.push([v, d / 10]);
        if (union.has(d / 10)) hit.push([v, d / 10]);
      }
    });
    var pct = hit.length / vids.length;
    var ok = (hit.length >= 2 && pct >= MIN_FIT) || (vids.length === 1 && hit.length === 1);
    // the SLOT is arithmetic and is assigned to every stream of a generation
    // that lands wholly on the lattice, roster or no roster; the roster union
    // is used only to NAME a slot.
    if (lattice.length === vids.length) {
      lattice.forEach(function (h) { S.slotOf.set(h[0], h[1]); });
    }
    if (ok) fitted += hit.length;
    info.push({ session: si, gen: g, vids: vids.length, matched: hit.length,
                onLattice: lattice.length,
                slotsAssigned: lattice.length === vids.length,
                matchedPct: round(100 * pct, 1), accepted: !!ok,
                reason: ok ? null : ('below ' + (100 * MIN_FIT).toFixed(0)
                  + '% or fewer than 2 streams fitted; names withheld for this generation '
                  + '(slots are still derived arithmetically from the vid)') });
  });
  return { base: base, fitted: fitted, candidates: cands,
           matchedPct: round(100 * fitted / cands, 1), gens: info,
           rosterSlots: slots, reason: null };
}

/* ---- main -------------------------------------------------------------- */
function parse(buf, filename, opts) {
  opts = opts || {};
  var b = new Uint8Array(buf), dv = new DataView(buf), n = b.length;
  if (n < 16 || dv.getUint32(0, false) !== MAGIC) {
    throw new Error('Not an MGO2 replay: expected magic "RPDT".');
  }
  var allEvents = !!opts.allEvents;
  var ydrop = opts.rawY ? 0 : BODY_OFFSET;

  // RPDT_FORMAT.md 1: the map id is the LOW BYTE of the BE u32 at +0x04.
  var mapIdRaw = dv.getUint32(4, false);
  var mapId = mapIdRaw & 0xff;
  // SaveMGO's first day of recordings (ids up to ~45315, 2025-12-10) carry map id 0. The
  // caller may pass the map SaveMGO's match record gives; used ONLY when the header's is unknown.
  var mapFromHint = false;
  if (!MAPS[mapId] && opts && opts.mapHint != null && MAPS[opts.mapHint]) { mapId = opts.mapHint; mapFromHint = true; }
  var hdr06 = (mapIdRaw >> 8) & 0xff;
  var hdr8 = dv.getUint32(8, false);
  var ver = b[6];                    // header +0x06 = FORMAT VERSION

  var warnings = [];
  var sessions = [], S = null;
  var tOff = 0, tMax = 0, prevTick = null, resets = 0, prevBodyTick = null;
  var firstT = null, lastT = null, nRecords = 0, nExt = 0;
  var kinds = new Map(), tickDeltas = new Map(), rosterAt = 0;
  var nPos13 = 0, nDmg2 = 0, nIncap2 = 0, nTickAnom = 0;
  var nPosConst = 0, nPosUnknown = {};

  var w = walk(b, dv, n, function (off, tick, size, kind, hdrlen, p0, p1) {
    if (kind !== null) { nExt++; kinds.set(kind, (kinds.get(kind) || 0) + 1); }
    if (prevTick !== null && tick > prevTick + TICK_FORWARD_MAX) {
      // corrupt timestamp (six corpus files carry one empty chunk 16 days out):
      // do not let it move the timeline.
      nTickAnom++;
      if (size) records(b, p0, p1, function () { nRecords++; });
      return;
    }
    if (prevTick !== null && tick + TICK_RESET_SLACK < prevTick) {
      resets++; tOff = tMax + TICK_RESET_GAP; prevBodyTick = null;
    }
    prevTick = tick;
    var t = tOff + tick;
    if (t > tMax) tMax = t;
    if (size === 0) return;
    if (firstT === null) firstT = t;
    lastT = t;

    if (kind === KIND_ROSTER) {
      S = new Session(sessions.length);
      sessions.push(S);
      rosterAt++;
      records(b, p0, p1, function (vid, indexed, f, L, a, e) {
        nRecords++;
        var p = rosterPlayer(b, dv, a, e, L);
        if (!p) return;
        S.players.push(p);
        S.bySlot.set(p.slot, p);
      });
      S.rosterEmpty = S.players.length === 0;
      return;
    }
    if (kind === KIND_SETTINGS && S !== null) {
      records(b, p0, p1, function (vid, indexed, f, L, a, e) {
        nRecords++;
        if (L < 3 || b[a] !== TYPE_SETTINGS) return;
        if (b[a + 1] === 0x00 && L >= 71) {
          S.rotation = [];
          for (var i = 0; i < 15; i++) {
            S.rotation.push([b[a + 26 + 3 * i], b[a + 27 + 3 * i], b[a + 28 + 3 * i]]);
          }
        } else if (b[a + 1] >= 1) {
          S.teams.set(b[a + 1] - 1, b[a + 2]);
          var ap = appearanceRecord(b, a, L);
          if (ap) S.appear.set(ap[0], ap[1]);
          var sk = skillRecord(b, a, L);
          // a FAILED alignment must not overwrite a slot an earlier record
          // decoded successfully.
          if (sk && (sk[1] !== null || !S.skills.has(sk[0]))) {
            S.skills.set(sk[0], sk[1]);
          }
        }
      });
      return;
    }
    if (kind === null) {
      if (prevBodyTick !== null) {
        var d = tick - prevBodyTick;
        if (d > 0 && d <= 500) tickDeltas.set(d, (tickDeltas.get(d) || 0) + 1);
      }
      prevBodyTick = tick;
    }
    if (S === null) {
      records(b, p0, p1, function () { nRecords++; });
      return;
    }
    records(b, p0, p1, function (vid, indexed, f, L, a, e) {
      nRecords++;
      if (L < 1) return;
      var ty = b[a];
      if (indexed) {
        var low = vid & 0x7ff;
        // A PLAYER'S ROSTER RECORD IN AN ORDINARY CHUNK. SaveMGO's first day of recordings
        // started before anyone had joined: the roster chunk holds only its header record and
        // each player arrives later as the same type-0x07 record on vid 0x1001 (12 of them in
        // match 45315). Taken ONLY when the roster chunk named nobody: in a normal file these
        // records are the NEXT game's players arriving in the lobby after the match (matches
        // 49390, 49553, 49879, 50042 each gained 12 strangers when they were accepted).
        if (low === 0x001 && ty === TYPE_ROSTER && L >= ROSTER_MIN_LEN) {
          // ... and only in the file's FIRST session: a later session with an empty roster is the
          // lobby filling up for the next game (49390: ROSTER#1 at the end, then 12 joins).
          var lateP = (S.rosterEmpty && S.index === 0) ? rosterPlayer(b, dv, a, e, L) : null;
          if (lateP && !S.bySlot.has(lateP.slot)) { S.players.push(lateP); S.bySlot.set(lateP.slot, lateP); S.lateRoster = (S.lateRoster || 0) + 1; }
          return;
        }
        if (low === OPS_VID_LOW) pushList(S.ops, ty, [t, a, L]);
        else if (low === 0x250 || low === 0x25a) S.world.push([t, low, ty, a, L]);
        // 0x28 is the SAME notice in SaveMGO recordings from match 68994 on (2026-10-08: same [slot, kind, count] and the same
        // kind / count spread over 468 records; 0x29 is never seen in those files, 0x28 never in older ones).
        else if (low === 0x258 && (ty === 0x29 || ty === 0x28) && L === 4) S.notice.push([t, b[a + 1], b[a + 2], b[a + 3]]);
        else if (low === 0x258 && (ty === 0x13 || ty === 0x16) && L === 2) S.destab.push([t, b[a + 1], ty]);
        else if ((low === 0x256 || low === 0x257) && ((ty === 0 && L === 2) || (ty === 1 && L === 9))) S.target.push([t, low, ty, a]);
        else if (low === 0x001 && ty === TYPE_SETTINGS && L >= 23 && L <= 45 && b[a + 1] >= 1 && b[a + 1] <= 23) S.stat.push([t, a, L]);
        else if (low >= GAUGE_LOW_MIN && low <= GAUGE_LOW_MAX
                 && ty === TYPE_HEALTH && L === 2) pushList(S.gauge, low, [t, b[a + 1]]);
        else if (TEAM_ARRAY_TYPES.indexOf(ty) >= 0 && L === TEAM_ARRAY_LEN) {
          // a live per-lobby-slot TEAM + OCCUPANCY array, NOT pad input.  The
          // record INDEX is the 4th framing byte of an indexed record, at a-1.
          S.teamArr.push([t, b[a - 1], ty, b.subarray(a, e)]);
        } else if (ty === TYPE_SETTINGS && LOADOUT_LEN.indexOf(L) >= 0) {
          // 37 of the 342 loadout records sit in ORDINARY chunks.
          var ap2 = appearanceRecord(b, a, L);
          if (ap2) S.appear.set(ap2[0], ap2[1]);
          var sk2 = skillRecord(b, a, L);
          if (sk2 && (sk2[1] !== null || !S.skills.has(sk2[0]))) {
            S.skills.set(sk2[0], sk2[1]);
          }
        }
        return;
      }
      if (vid === RULES_VIDS[0] || vid === RULES_VIDS[1]) {
        if (ty === TYPE_DAMAGE) {
          S.rulesRecords++;
          var groups = rulesRoster(b, a, L);
          if (groups) {
            S.rulesGrammarOk++;
            groups.forEach(function (gm) {
              var set = S.rosterGroups.get(gm[0]);
              if (!set) { set = new Set(); S.rosterGroups.set(gm[0], set); }
              gm[1].forEach(function (m) { set.add(m); });
            });
          }
        }
        return;
      }
      if (ty === TYPE_POS && POS_NOT_A_POSITION.indexOf(L) < 0) {
        // SHORT forms carry the block at a FIXED HEAD OFFSET and vary in their
        // TRAILER; LONG forms carry an extension block and do put it last.
        var xo = posXOffset(ver, L);
        if (xo === null) {
          // COUNTED, not silently dropped (see POS_SHORT_LEN).
          nPosUnknown[L] = (nPosUnknown[L] || 0) + 1;
          return;
        }
        var px = dv.getInt16(a + xo, true), py = dv.getInt16(a + xo + 2, true),
            pz = dv.getInt16(a + xo + 4, true),
            pyaw = dv.getUint16(a + xo + 6, true);
        // the declared constants are READ AND CHECKED here, which is what
        // earns them their verifiedConstantBytes credit.
        var pc = posConst(ver, L);
        if (pc) {
          Object.keys(pc).forEach(function (ck) {
            if (b[a + (+ck)] !== pc[ck]) nPosConst++;
          });
        }
        var pyd = posYawDup(ver, L);
        if (pyd !== null && dv.getUint16(a + pyd, true) !== pyaw) nPosConst++;
        // v2 payload[5] is the DOWN-EPISODE counter, NOT a death counter.
        var kd = (ver >= 2 && POS_KD_LEN.indexOf(L) >= 0) ? b[a + 5] : null;
        if (POS_BASE_LEN.indexOf(L) >= 0) {
          pushList(S.pos, vid, [t, px, py, pz, pyaw, b[a + 1], b[a + 2], kd]);
          if (L === 14 && b[a + 13]) nPos13++;
        } else {
          var anchor = null, ragdoll = null;
          if (POS_ANCHOR_LEN.indexOf(L) >= 0) {
            anchor = [dv.getFloat32(a + 4, true), dv.getFloat32(a + 8, true),
                      dv.getFloat32(a + 12, true)];
          }
          var ro = POS_RAGDOLL_OFF[L];
          if (ro !== undefined) {              // the f32 triple is (Z, Y, X)
            ragdoll = [dv.getFloat32(a + ro + 8, true),
                       dv.getFloat32(a + ro + 4, true),
                       dv.getFloat32(a + ro, true)];
          }
          S.pvar.push([vid, t, L, px, py, pz, pyaw, b[a + 1], b[a + 2], kd,
                       anchor, ragdoll, L === 39 ? b[a + 3] : null]);
          // DESTABILIZED. The one-byte trailer of the LEN 15 (v2: 18) form. Over 158 SaveMGO files it is 0x00-0x16 or 0xff
          // everywhere EXCEPT match 52613, where from 0.2 s after the destabilizer goes off (161.5 s) until each is knocked
          // out, exactly the five living enemies carry 0x2a-0xf3 (one value each: b7, cd, f3, b8, ec) on every sample -
          // 1,063 records, no other file, no other player. MEASURED 2026-10-08 (scratch loop/trailer.js, trailer2.js).
          // What the value itself encodes is not known; what the low values mean is not known either.
          if ((L === 15 || L === 18) && b[a + L - 1] >= 0x20 && b[a + L - 1] !== 0xff) S.frozen.push([vid, t, b[a + L - 1]]);
          // The same byte below 0x20 is a LOBBY SLOT: the player this one is acting on. MEASURED 2026-10-08 on 53 files: for
          // 26 of 26 CQC throws the thrower's record names the victim (the viewer's nearest-player guess had 24 right, 1 wrong,
          // 1 missing); over 1,709 such records the named player is a median 0.9 m away. 0xff = nobody.
          else if ((L === 15 || L === 18) && b[a + L - 1] < 0x20) S.partner.push([vid, t, b[a + L - 1]]);
        }
      } else if (ty === TYPE_HEALTH && L === 3) {
        pushList(S.hp, vid - VID_HEALTH, [t, b[a + 1]]);
        pushList(S.st, vid - VID_HEALTH, [t, b[a + 2]]);
      } else if (ty === TYPE_WEAPON && L === 6) {
        var pv = vid - VID_WEAPON;
        pushList(S.weapon, pv, [t, b[a + 2]]);
        // payload[3] -> itemAttr, payload[1] -> itemFlag.  Both are emitted
        // as the RAW BYTE with the PROPERTY left [U].
        nestedSet(S.item, pv, t, [b[a + 2], b[a + 5], b[a + 3], b[a + 1]]);
      } else if (ty === TYPE_INCAP && (L === 2 || L === 3)) {
        if (L === 3 && b[a + 2] !== 0x02) nIncap2++;
        if (b[a + 1] & 0x01) nestedSet(S.incap, vid - VID_WEAPON, t, (b[a + 1] >> 3) & 0x0f);
      } else if (ty === TYPE_AIM && L === 7) {
        nestedSet(S.aim, vid - VID_AIM, t, [dv.getInt16(a + 1, true),
                                            dv.getInt16(a + 3, true),
                                            dv.getInt16(a + 5, true)]);
      } else if (ty === TYPE_STATE && (L === 4 || L === 10)) {
        var tgt = b[a + 2];
        // bit 0x40 is the SIDE OF THE AIM ORIGIN; which side is [U].
        nestedSet(S.act, vid - VID_STATE, t,
                  [b[a + 1] & 0x3f, tgt === 0xff ? null : tgt,
                   (b[a + 1] & 0x40) ? 1 : 0]);
        if (L === 10) {
          // payload[4..9]: a point on the body of the player named by
          // payload[2], in the same stage-local frame as the aim ray.
          nestedSet(S.tail, vid - VID_STATE, t, [dv.getInt16(a + 4, true),
                                                 dv.getInt16(a + 6, true),
                                                 dv.getInt16(a + 8, true)]);
        }
      } else if (ty === TYPE_THROW && L === 14) {
        // RPDT_FORMAT.md 7.9: a THROWABLE launch at positionVid - 2.
        // payload[2..7] is the launch point in the c-translated frame;
        // payload[8..13] is the LAUNCH VELOCITY in cm/s (itembox/nade_vel.js).
        S.throw_.push([vid - VID_STATE, t, b[a + 1],
                       [dv.getInt16(a + 2, true), dv.getInt16(a + 4, true),
                        dv.getInt16(a + 6, true)],
                       [dv.getInt16(a + 8, true), dv.getInt16(a + 10, true),
                        dv.getInt16(a + 12, true)]]);
      // L 29 = an EXPLOSION hit (2026-10-10; layout from doos's notes, checked on replay_352519_7: 37 records, kind 0x13, weapon
      // 52): the 12-byte head, then 01 01 00, then THREE FLOATS = the VICTIM's own position (stored z, y, x - NOT the blast centre: that is the head's own point, as in the short form), then 2 bytes. Newer-format files
      // send every grenade hit this way; it used to be dropped, so those matches had no real blast point at all.
      } else if (ty === TYPE_DAMAGE && (L === 12 || L === 16 || L === 29)) {
        if (b[a + 2]) nDmg2++;
        S.dmg.push({ vid: vid - VID_DAMAGE, t: t, a: a, L: L });
      }
    });
  });
  if (w.walkEnd !== n) throw new Error('chunk walk ended at ' + w.walkEnd + ' of ' + n);
  if (w.truncatedAt != null) warnings.push('TRUNCATED FILE: the recording ends inside its last chunk (byte '
    + w.truncatedAt + ' of ' + n + '). Everything before that chunk was read.');
  if (!sessions.length) throw new Error('No roster found - the file may be truncated.');

  /* ---- roster union, teams, identity ---------------------------------- */
  var union = new Map(), conflicts = new Set();
  sessions.forEach(function (S2) {
    S2.players.forEach(function (p) {
      var prev = union.get(p.slot);
      if (prev !== undefined && prev.charId !== p.charId) { conflicts.add(p.slot); return; }
      union.set(p.slot, p);
    });
  });
  conflicts.forEach(function (s) { union.delete(s); });
  var slotConflicts = Array.from(conflicts).sort(function (a, c) { return a - c; });

  var votes = new Map();
  sessions.forEach(function (S2) {
    S2.teams.forEach(function (tv, slot) {
      if (!union.has(slot)) return;
      if (!votes.has(slot)) votes.set(slot, new Map());
      var c = votes.get(slot);
      c.set(tv, (c.get(tv) || 0) + 1);
    });
  });
  var teams = new Map();
  votes.forEach(function (c, slot) {
    var bt = null, bn = -1;
    c.forEach(function (v, k) { if (v > bn) { bn = v; bt = k; } });
    teams.set(slot, bt);
  });
  var teamValues = Array.from(new Set(Array.from(teams.values())))
                        .sort(function (a, c) { return a - c; });
  var teamSingle = (teamValues.length <= 1 && teams.size > 1);
  if (teamSingle) teams = new Map();

  var ident = resolveIdentity(sessions, union);

  if (slotConflicts.length) {
    warnings.push('ROSTER: slots ' + JSON.stringify(slotConflicts) + ' carried different '
      + 'charIds in different roster snapshots and were dropped from the identity table.');
  }
  if (teamSingle) {
    warnings.push('TEAMS UNKNOWN: every rostered slot carries the same team byte ('
      + JSON.stringify(teamValues) + '), which is the unassigned default rather than a '
      + 'real team assignment. All team values are null.');
  }

  // game mode: the rotation entry whose map equals the header map
  var modeRule = null;
  for (var si = 0; si < sessions.length && modeRule === null; si++) {
    for (var ri = 0; ri < sessions[si].rotation.length; ri++) {
      if (sessions[si].rotation[ri][0] === mapId) { modeRule = sessions[si].rotation[ri][1]; break; }
    }
  }
  // A recording that starts without its settings block (seen on SaveMGO match 68982: no
  // type-0x0B records at all) carries no rotation, so no mode. The caller may pass the mode
  // SaveMGO's own match record gives (opts.modeHint); it is used ONLY when the file has none.
  var modeFromHint = false;
  if (modeRule === null && opts && opts.modeHint != null && MODE_NAMES[opts.modeHint] !== undefined) {
    modeRule = opts.modeHint; modeFromHint = true;
  }
  var modeName = (modeRule !== null && MODE_NAMES[modeRule] !== undefined)
                 ? MODE_NAMES[modeRule] : null;

  /* ---- global player list --------------------------------------------- */
  var players = [], indexOf = new Map();
  function keyOf(p) {
    return p.charId ? 'id ' + p.charId : 'nick ' + p.nick + ' ' + p.clan;
  }
  sessions.forEach(function (S2) {
    S2.players.forEach(function (p) {
      var k = keyOf(p);
      if (!indexOf.has(k)) {
        indexOf.set(k, players.length);
        players.push({ index: players.length, name: p.nick, clan: p.clan || null,
                       team: null, id: p.charId || null, clanId: p.clanId || null,
                       slot: null, teamGroup: null, skills: null,
                       slots: [], anonymous: false });
      }
      var pi = indexOf.get(k);
      players[pi].slots.push({ session: S2.index, slot: p.slot, joinCounter: p.joinCounter });
    });
  });
  var ta = teamArray(sessions);
  var teamGroup = ta[0], nTeamPairs = ta[1], nTeamOcc = ta[2];
  var allSkills = new Map();
  sessions.forEach(function (S2) {
    S2.skills.forEach(function (v, k) { allSkills.set(k, v); });
  });
  var allAppear = new Map();
  sessions.forEach(function (S2) {
    S2.appear.forEach(function (v, k) { allAppear.set(k, v); });
  });
  /* SKILL ID BASE 1, MEASURED (not assumed): under base 0 every name shifts
     by one.  id 21 -> SHIELD_MAS holders carry item 73 SHIELD on 47.1% of
     their held-item samples against 0.1% for non-holders (x799); id 2 ->
     SBMC_MAS holders carry an SMG 32.1% vs 0.3% (x95) and assault rifles 2.2%
     vs 30.6% -- the opposite of base-0's ASSLT_MAS; id 3 -> ASSLT_MAS holders
     carry assault rifles 32.4% vs 13.8% while NO player in the corpus ever
     holds a shotgun, refuting base-0's SHOT_MAS outright. */
  function skillsFor(slot) {
    var sk = allSkills.get(slot);
    if (sk === undefined || sk === null) return null;
    return sk.map(function (pr) {
      return { id: pr[0], name: SKILL_NAMES[pr[0] - 1], level: pr[1] };
    });
  }
  players.forEach(function (p) {
    var i;
    for (i = 0; i < p.slots.length; i++) {
      if (teams.has(p.slots[i].slot)) { p.team = teams.get(p.slots[i].slot); break; }
    }
    for (i = 0; i < p.slots.length; i++) {
      if (teamGroup.has(p.slots[i].slot)) {
        p.teamGroup = teamGroup.get(p.slots[i].slot); break;
      }
    }
    // Same file: with no settings block NOBODY has a team value, but the roster's own
    // team grouping is still there. Measured on 30 SaveMGO matches (2026-10-08): group 0
    // is team value 0 and group 1 is team value 2 for 323 of 323 players that have both.
    if (p.team == null && p.teamGroup != null && teams.size === 0) p.team = p.teamGroup ? 2 : 0;
    for (i = 0; i < p.slots.length; i++) {
      if (allSkills.has(p.slots[i].slot)) {
        p.skills = skillsFor(p.slots[i].slot); break;
      }
    }
    for (i = 0; i < p.slots.length; i++) {
      if (allAppear.has(p.slots[i].slot)) {
        p.appearance = allAppear.get(p.slots[i].slot); break;
      }
    }
  });
  var slot2idx = new Map();
  union.forEach(function (p, slot) {
    var pi = indexOf.get(keyOf(p));
    slot2idx.set(slot, pi);
    players[pi].slot = slot;
  });

  var anon = new Map(), nAnon = 0;
  sessions.forEach(function (S2) {
    Array.from(S2.stream).sort(function (x, y) { return x - y; }).forEach(function (vid) {
      var slot = S2.slotOf.get(vid);
      if (slot !== undefined && slot2idx.get(slot) !== undefined) return;
      anon.set(S2.index + ':' + vid, players.length);
      players.push({ index: players.length, name: null, clan: null, team: null,
                     id: null, clanId: null,
                     slot: (slot === undefined ? null : slot),
                     teamGroup: (slot === undefined || !teamGroup.has(slot))
                                ? null : teamGroup.get(slot),
                     skills: (slot === undefined ? null : skillsFor(slot)),
                     appearance: (slot === undefined ? null : (allAppear.get(slot) || null)),
                     slots: [], anonymous: true });
      nAnon++;
    });
  });
  function pidx(S2, vid) {
    var slot = S2.slotOf.get(vid);
    if (slot !== undefined) {
      var i = slot2idx.get(slot);
      if (i !== undefined) return i;
    }
    var k = anon.get(S2.index + ':' + vid);
    return k === undefined ? null : k;
  }

  /* ---- tracks ---------------------------------------------------------- */
  function cmpSample(x, y) {
    for (var i = 0; i < x.length; i++) { if (x[i] !== y[i]) return x[i] - y[i]; }
    return 0;
  }
  var tracks = [];
  sessions.forEach(function (S2) {
    Array.from(S2.stream).sort(function (x, y) { return x - y; }).forEach(function (vid) {
      var pi = pidx(S2, vid);
      if (pi === null) return;
      tracks.push({ session: S2.index, gen: S2.genOf.get(vid), vid: vid,
                    slot: S2.slotOf.has(vid) ? S2.slotOf.get(vid) : null, player: pi,
                    samples: S2.pos.get(vid).slice().sort(cmpSample) });
    });
  });

  /* ---- frame translation c (RPDT_FORMAT.md 7.2.1) ---------------------- */
  var dxs = [], dys = [], dzs = [];
  sessions.forEach(function (S2) {
    var times = new Map(), table = new Map();
    S2.stream.forEach(function (vid) {
      var series = S2.pos.get(vid).slice().sort(cmpSample);
      if (!series.length) return;
      times.set(vid, series.map(function (s) { return s[0]; }));
      table.set(vid, series.map(function (s) { return [s[1], s[2], s[3]]; }));
    });
    S2.dmg.forEach(function (d) {
      if (!times.has(d.vid) || d.L < 12) return;
      var i = nearest(times.get(d.vid), d.t);
      if (i < 0) return;
      var p = table.get(d.vid)[i];
      dxs.push(dv.getInt16(d.a + 6, true) - p[0]);
      dys.push(dv.getInt16(d.a + 8, true) - p[1]);
      dzs.push(dv.getInt16(d.a + 10, true) - p[2]);
    });
  });
  var cSamples = dxs.length;
  var cvec = (cSamples < MIN_C_SAMPLES) ? null
             : [medianInt(dxs), medianInt(dys), medianInt(dzs)];
  if (cvec === null) {
    warnings.push('FRAME TRANSLATION UNKNOWN: only ' + cSamples + ' damage record(s) with a '
      + 'locatable victim in this file, below the ' + MIN_C_SAMPLES + ' needed to fit '
      + 'the per-map constant c. Every field carried in the stage-local frame - the '
      + 'damage impact point and the type-0x05 aim ray - is emitted as null rather than '
      + 'in an unresolved frame.');
  }
  function toWorld(x, y, z) {
    if (cvec === null) return null;
    return [(x - cvec[0]) * SCALE, (y - cvec[1]) * SCALE - ydrop, (z - cvec[2]) * SCALE];
  }

  /* ---- slot -> player index, for the KILL FEED, which names slots ------ */
  var slotStreams = new Map();
  sessions.forEach(function (S2) {
    Array.from(S2.stream).sort(function (x, y) { return x - y; }).forEach(function (vid) {
      var slot = S2.slotOf.get(vid);
      var pi = pidx(S2, vid);
      if (slot === undefined || pi === null) return;
      var ser = (S2.pos.get(vid) || []).slice().sort(cmpSample);
      pushList(slotStreams, slot, [vid, pi, ser.map(function (r) { return r[0]; })]);
    });
  });
  function slotPlayer(slot, t) {
    if (slot === null || slot === undefined) return null;
    var i = slot2idx.get(slot);
    if (i !== undefined) return i;
    var best = null;
    (slotStreams.get(slot) || []).forEach(function (row) {
      var times = row[2];
      if (!times.length) return;
      var j = bisectLeft(times, t), d = null;
      for (var k = j - 1; k <= j; k++) {
        if (k >= 0 && k < times.length) {
          var dd = Math.abs(times[k] - t);
          if (d === null || dd < d) d = dd;
        }
      }
      if (d === null) return;
      if (best === null || d < best[0] || (d === best[0] && row[0] < best[1])) {
        best = [d, row[0], row[1]];
      }
    });
    return best === null ? null : best[2];
  }

  /* ---- frames ---------------------------------------------------------- */
  var npl = players.length;
  var frameMap = new Map(), collisions = 0, samplesPer = new Map();
  tracks.forEach(function (tr) {
    var S2 = sessions[tr.session], vid = tr.vid, pi = tr.player;
    samplesPer.set(pi, (samplesPer.get(pi) || 0) + tr.samples.length);
    tr.samples.forEach(function (s) {
      var t = s[0], row = frameMap.get(t);
      if (!row) { row = new Array(npl).fill(null); frameMap.set(t, row); }
      if (row[pi] !== null) collisions++;
      row[pi] = [s[1], s[2], s[3], s[4], s[5], s[6],
                 nestedGet(S2.aim, vid, t), carryGet(S2.item, vid, t),
                 carryGet(S2.act, vid, t), nestedGet(S2.incap, vid, t),
                 nestedGet(S2.tail, vid, t), s[7]];
    });
  });

  var vitT = [], vitH = [], vitS = [];
  for (var i0 = 0; i0 < npl; i0++) { vitT.push([]); vitH.push([]); vitS.push([]); }
  sessions.forEach(function (S2) {
    S2.hp.forEach(function (series, vid) {
      if (!S2.stream.has(vid)) return;
      var pi = pidx(S2, vid);
      if (pi === null) return;
      series.forEach(function (r) { vitT[pi].push(r[0]); vitH[pi].push(r[1]); });
    });
    S2.st.forEach(function (series, vid) {
      if (!S2.stream.has(vid)) return;
      var pi = pidx(S2, vid);
      if (pi === null) return;
      series.forEach(function (r) { vitS[pi].push(r[1]); });
    });
  });
  var vitST = [];
  for (var i1 = 0; i1 < npl; i1++) vitST.push(vitT[i1].slice());
  function stableOrder(ts) {
    var ord = ts.map(function (_, k) { return k; });
    ord.sort(function (x, y) { return ts[x] - ts[y]; });
    return ord;
  }
  for (var pi2 = 0; pi2 < npl; pi2++) {
    var o1 = stableOrder(vitT[pi2]);
    var tt = o1.map(function (k) { return vitT[pi2][k]; });
    var hh = o1.map(function (k) { return vitH[pi2][k]; });
    var o2 = stableOrder(vitST[pi2]);
    var t2 = o2.map(function (k) { return vitST[pi2][k]; });
    var ss = o2.map(function (k) { return vitS[pi2][k]; });
    vitT[pi2] = tt; vitH[pi2] = hh; vitST[pi2] = t2; vitS[pi2] = ss;
  }
  function sampleVital(ts, vs, t) {
    if (!ts.length) return null;
    var i = bisectRight(ts, t) - 1;
    if (i < 0) return null;
    return round(Math.min(1, Math.max(0, vs[i] / VITAL_MAX)), 4);
  }
  function aliveAtP(pi, t) {
    var ts = vitT[pi];
    if (!ts.length) return null;
    var i = bisectRight(ts, t) - 1;
    return i < 0 ? null : vitH[pi][i] > 0;
  }

  var times = Array.from(frameMap.keys()).sort(function (x, y) { return x - y; });
  var t0 = times.length ? times[0] : (firstT || 0);
  var frames = [], filled = 0, nAliveNull = 0;
  var nAim = 0, nItem = 0, nAct = 0, nIncap = 0, nTail = 0;
  times.forEach(function (t) {
    var row = frameMap.get(t), p = new Array(npl);
    for (var k = 0; k < npl; k++) {
      var v = row[k];
      if (v === null) { p[k] = null; continue; }
      filled++;
      var al = aliveAtP(k, t);
      if (al === null) nAliveNull++;
      var aim = v[6], item = v[7], act = v[8], incap = v[9], tail = v[10];
      var kd = v[11] === undefined ? null : v[11];
      var ax = null, ay = null, az = null;
      if (aim !== undefined && cvec !== null) {
        nAim++;
        ax = (aim[0] - cvec[0]) * SCALE;
        ay = (aim[1] - cvec[1]) * SCALE - ydrop;
        az = (aim[2] - cvec[2]) * SCALE;
      }
      if (item !== undefined) nItem++;
      if (act !== undefined) nAct++;
      if (incap !== undefined) nIncap++;
      // the type-0x01 LEN-10 tail: a point on the body of the player named by
      // targetSlot, in the same stage-local frame as the aim ray.
      var tx = null, ty2 = null, tz = null;
      if (tail !== undefined && cvec !== null) {
        nTail++;
        tx = (tail[0] - cvec[0]) * SCALE;
        ty2 = (tail[1] - cvec[1]) * SCALE - ydrop;
        tz = (tail[2] - cvec[2]) * SCALE;
      }
      // The cell is built as a NAME -> VALUE map and materialised THROUGH
      // POSITION_CELL, so the emission order is the documented order by
      // construction and a transposition is impossible.
      var cellMap = {
        x: v[0] * SCALE, y: v[1] * SCALE - ydrop, z: v[2] * SCALE,
        headingDeg: round(v[3] * 360 / 65536, 2), alive: al,
        health01: sampleVital(vitT[k], vitH[k], t),
        stamina01: sampleVital(vitST[k], vitS[k], t),
        stateFlags: v[4], motionState: v[5],
        aimX: ax, aimY: ay, aimZ: az,
        itemId: item === undefined ? null : item[0],
        ammo: item === undefined ? null : item[1],
        action: act === undefined ? null : act[0],
        targetSlot: act === undefined ? null : act[1],
        incapLevel: incap === undefined ? null : incap,
        targetPointX: tx, targetPointY: ty2, targetPointZ: tz,
        motionName: motionName(v[5], hdr06),
        motionGroup: motionGroup(v[5], hdr06),
        aimOriginSide: act === undefined ? null : act[2],
        itemAttr: (item !== undefined && hdr06 === 0) ? item[2] : null,
        knockdownCount: kd,
        itemFlag: item === undefined ? null : item[3]
      };
      p[k] = POSITION_CELL.map(function (nm2) { return cellMap[nm2]; });
    }
    frames.push({ t: round((t - t0) / 1000, 3), p: p });
  });
  // type-0x04 payload[1] == 16 per player index; {} when it never occurs.
  var itemFlagPlayers = {};
  (function () {
    var ifi = POSITION_CELL.indexOf('itemFlag');
    frames.forEach(function (fr2) {
      fr2.p.forEach(function (cc2, pi2) {
        if (cc2 !== null && cc2[ifi] === WEAPON_SNEAK) {
          itemFlagPlayers[pi2] = (itemFlagPlayers[pi2] || 0) + 1;
        }
      });
    });
  })();
  players.forEach(function (p) {
    p.positionSamples = samplesPer.get(p.index) || 0;
    p.tracked = p.positionSamples > 0;
  });
  var nTracked = players.filter(function (p) { return p.tracked; }).length;
  // THE CONTRACT, revision 2026-08-21b.  Emission is now structural, so what
  // is left to assert is checked over EVERY cell rather than the first one:
  // the width, and the KIND of value in the typed slots.  The previous version
  // returned after the first non-null cell and was a complete no-op on a file
  // with no frames.
  (function () {
    // the three head slots are load-bearing for every consumer; a reorder of
    // POSITION_CELL itself is otherwise invisible to a decoder that
    // materialises through it (the Python side also compares it against
    // meta.positionCellFields, and xcheck compares the two decoders' lists).
    if (POSITION_CELL[0] !== 'x' || POSITION_CELL[1] !== 'y'
        || POSITION_CELL[2] !== 'z' || POSITION_CELL[8] !== 'motionState'
        || POSITION_CELL[12] !== 'itemId') {
      throw new Error('POSITION_CELL head slots are out of order');
    }
    var ix = POSITION_CELL.indexOf('x'), iy = POSITION_CELL.indexOf('y'),
        iz = POSITION_CELL.indexOf('z'),
        ims = POSITION_CELL.indexOf('motionState'),
        iit = POSITION_CELL.indexOf('itemId'), seen = 0;
    for (var fi = 0; fi < frames.length; fi++) {
      var fp = frames[fi].p;
      for (var fj = 0; fj < fp.length; fj++) {
        var cc = fp[fj];
        if (cc === null) continue;
        seen++;
        if (cc.length !== POSITION_CELL.length) {
          throw new Error('emitted position cell has ' + cc.length
                          + ' slots but positionCell names ' + POSITION_CELL.length);
        }
        if (typeof cc[ix] !== 'number' || typeof cc[iy] !== 'number'
            || typeof cc[iz] !== 'number') {
          throw new Error('a position cell coordinate slot is not a number');
        }
        if (!(typeof cc[ims] === 'number' && cc[ims] >= 0 && cc[ims] <= 255)) {
          throw new Error('cell motionState is not a byte');
        }
        if (cc[iit] !== null && !(typeof cc[iit] === 'number'
                                  && cc[iit] >= 0 && cc[iit] < ITEM_NAMES.length)) {
          throw new Error('cell itemId is outside meta.itemNames');
        }
      }
    }
    if (seen === 0 && nTracked) {
      throw new Error(nTracked + ' tracked player(s) but not one non-null cell');
    }
  })();

  var nGhost = players.filter(function (p) { return !p.tracked && p.name; }).length;
  if (nGhost) {
    warnings.push(nGhost + ' rostered player(s) have ZERO position samples in this '
      + 'recording (tracked=false). They are kept so the lobby roster is visible, but '
      + 'their frame column is null throughout; do not allocate an avatar for them.');
  }
  if (nAnon) {
    warnings.push(nAnon + ' streaming position object(s) could not be tied to a roster '
      + 'slot and are emitted with name/clan/team/id = null (anonymous=true). Their '
      + 'positions are real; only the identity is unknown.');
  }

  /* ---- combat ---------------------------------------------------------- */
  function deathsOf(S2) {
    var out = [];
    S2.hp.forEach(function (series, vid) {
      if (!S2.stream.has(vid)) return;
      var s = series.slice().sort(cmpSample), prev = null;
      s.forEach(function (r) {
        if (prev !== null && prev > 0 && r[1] === 0) out.push([r[0], vid]);
        prev = r[1];
      });
    });
    out.sort(function (x, y) { return x[0] - y[0] || x[1] - y[1]; });
    return out;
  }

  function causeOf(S2, vid, tDeath, tDmg) {
    if (tDmg === null) return 'unknown';
    var s = (S2.hp.get(vid) || []).slice().sort(cmpSample);
    var lifeStart = null, prev = null;
    for (var i = 0; i < s.length; i++) {
      if (s[i][0] > tDeath) break;
      if (prev !== null && s[i][1] > prev) lifeStart = s[i][0];
      prev = s[i][1];
    }
    if (lifeStart !== null && tDmg < lifeStart) return 'unknown';
    return (tDeath - tDmg) <= KILL_LOOKBACK_MS ? 'combat' : 'bleedout';
  }

  /* ---- type-0x02 sub-forms (RPDT_FORMAT.md 4.1) ------------------------
     Real position samples of the SAME object at ticks the base stream does not
     cover, kept OUT of frames[] because mixing the sub-forms into a player's
     trajectory cost 63 killer attributions.  The f32 anchor and the f32 ragdoll
     point are absolute world units and carry NO body offset. */
  // -> meta.destabFrozen [{player, t0, t1, code}]: one span per player per freeze (samples less than 2 s apart)
  var destabFrozen = [];
  sessions.forEach(function (S2) {
    var openF = {};
    S2.frozen.forEach(function (r) {
      var pi = pidx(S2, r[0]), tt = round((r[1] - t0) / 1000, 3);
      if (pi == null) return;
      var cur = openF[pi];
      if (cur && tt - cur.t1 < 2) { cur.t1 = tt; return; }
      openF[pi] = { player: pi, t0: tt, t1: tt, code: r[2], session: S2.index }; destabFrozen.push(openF[pi]);
    });
  });
  // -> meta.partnerOps [{player, on, t0, t1}]: who is acting on whom (a throw, a hold), merged over gaps under 1 s
  var partnerOps = [];
  sessions.forEach(function (S2) {
    var openP = {};
    S2.partner.forEach(function (r) {
      var pi = pidx(S2, r[0]), qi = slotPlayer(r[2], r[1]), tt = round((r[1] - t0) / 1000, 3);
      if (pi == null || qi == null || qi === pi) return;
      var cur = openP[pi];
      if (cur && cur.on === qi && tt - cur.t1 < 1) { cur.t1 = tt; return; }
      openP[pi] = { player: pi, on: qi, t0: tt, t1: tt, session: S2.index }; partnerOps.push(openP[pi]);
    });
  });
  var pvariants = [];
  sessions.forEach(function (S2) {
    S2.pvar.forEach(function (r) {
      var vid = r[0], pi = pidx(S2, vid);
      var slot = S2.slotOf.has(vid) ? S2.slotOf.get(vid) : null;
      var mname = motionName(r[8], hdr06);
      pvariants.push({
        t: round((r[1] - t0) / 1000, 3), player: pi, slot: slot, len: r[2],
        x: r[3] * SCALE, y: r[4] * SCALE - ydrop, z: r[5] * SCALE,
        headingDeg: round(r[6] * 360 / 65536, 2),
        stateFlags: r[7], motionState: r[8],
        motionName: mname === undefined ? null : mname,
        knockdownCount: r[9],
        // ROLL DIRECTION (39-byte roll rows; the byte after the state byte). MEASURED 2026-10-11 on 70 files + live rolls:
        // in state 3 (a roll while aiming) 1 = forward dive, 2 = left, 3 = back hop, 4 = right. No meaning in state 2.
        rollDir: r[12] == null ? null : r[12],
        // WHAT HE HOLDS AND DOES while only this form is sent (2026-10-10, replay_352519_7: a player back on his feet after a
        // blast drew his M4, crouched and fired for 1.3 s, all inside 45-byte rows - the item and action records keep coming)
        item: (function (it) { return it === undefined ? null : it[0]; })(carryGet(S2.item, vid, r[1])),
        ammo: (function (it) { return it === undefined ? null : it[1]; })(carryGet(S2.item, vid, r[1])),
        action: (function (ac) { return ac === undefined ? null : ac[0]; })(carryGet(S2.act, vid, r[1])),
        aim: (function (am) { return (am === undefined || cvec === null) ? null : [(am[0] - cvec[0]) * SCALE, (am[1] - cvec[1]) * SCALE - ydrop, (am[2] - cvec[2]) * SCALE]; })(nestedGet(S2.aim, vid, r[1])),
        anchor: r[10] === null ? null : [round(r[10][0], 3), round(r[10][1], 3),
                                         round(r[10][2], 3)],
        ragdoll: r[11] === null ? null : [round(r[11][0], 3), round(r[11][1], 3),
                                          round(r[11][2], 3)],
        session: S2.index });
    });
  });
  pvariants.sort(function (x, y) {
    return (x.t - y.t)
        || ((x.player === null ? -1 : x.player) - (y.player === null ? -1 : y.player))
        || (x.len - y.len);
  });

  var events = [];
  var nDmg = 0, nAttr = 0, nKillAttr = 0;
  var nOp9 = 0, nOp9Matched = 0, nOp9Orphan = 0, nDeathsUnjoined = 0;
  var nKillerOp9 = 0, nKillerUnnamed = 0, nOp9Suicide = 0;
  var op9LagMin = 1e9, op9LagMax = -1e9;
  var op9Kills = new Map(), op9Deaths = new Map();
  var objectiveEvents = 0, nObjRejected = 0, nObjInitial = 0, nThrow = 0;
  var clockPoints = [], ratingRows = [], roundEnds = [], raceOps = [], objFeed = [], worldOps = [], sneakTimer = [], streakOps = [], destabOps = [], targetOps = [], capClock = [], nameOps = [], statOps = [], statSkipped = 0, roundResults = [], matchResult = null;
  // every lobby slot this file actually carries, roster or anonymous
  var occupiedSlots = new Set();
  sessions.forEach(function (S2) {
    S2.slotOf.forEach(function (sl) { occupiedSlots.add(sl); });
  });
  union.forEach(function (p, sl) { occupiedSlots.add(sl); });

  sessions.forEach(function (S2) {
    // position / health samplers for the geometric cross-check
    var ptT = new Map(), ptX = new Map(), hpT = new Map(), hpV = new Map();
    S2.stream.forEach(function (vid) {
      var s = S2.pos.get(vid).slice().sort(cmpSample);
      if (s.length) {
        ptT.set(vid, s.map(function (r) { return r[0]; }));
        ptX.set(vid, s.map(function (r) { return [r[1], r[2], r[3]]; }));
      }
      var h = (S2.hp.get(vid) || []).slice().sort(cmpSample);
      if (h.length) {
        hpT.set(vid, h.map(function (r) { return r[0]; }));
        hpV.set(vid, h.map(function (r) { return r[1]; }));
      }
    });
    function at(vid, t) {
      var ts = ptT.get(vid);
      if (!ts) return null;
      var i = nearest(ts, t);
      return i < 0 ? null : ptX.get(vid)[i];
    }
    function aliveVid(vid, t) {
      var ts = hpT.get(vid);
      if (!ts) return null;
      var i = bisectRight(ts, t) - 1;
      return i < 0 ? null : hpV.get(vid)[i] > 0;
    }

    var dmg = [];
    S2.dmg.forEach(function (d) {
      if (!S2.stream.has(d.vid)) return;      // rules object, not a player
      dmg.push({ t: d.t, vid: d.vid, subtype: b[d.a + 1],
                 damageClass: damageClass(b[d.a + 1]),
                 damage: dv.getUint16(d.a + 3, true) / 128,
                 weapon: b[d.a + 5],
                 impact: [dv.getInt16(d.a + 6, true), dv.getInt16(d.a + 8, true),
                          dv.getInt16(d.a + 10, true)],
                 bearing: d.L === 16 ? dv.getUint16(d.a + 14, true) * 2 * Math.PI / 65536 : null,
                 blast: d.L === 29 ? [dv.getFloat32(d.a + 15, true), dv.getFloat32(d.a + 19, true), dv.getFloat32(d.a + 23, true)] : null,
                 attackerSlot: d.L === 29 ? (b[d.a + 3] & 31) : null,
                 attackerVid: null, bearingErr: null, runnerUpErr: null,
                 ambiguous: null, deadSkipped: 0 });
    });
    dmg.sort(function (x, y) { return x.t - y.t; });
    nDmg += dmg.length;

    dmg.forEach(function (ev) {
      if (ev.bearing === null) return;        // explosive splash carries no bearing
      var vgen = S2.genOf.get(ev.vid);
      var pv = at(ev.vid, ev.t);
      if (!pv) return;
      var scored = [], skipped = 0;
      S2.stream.forEach(function (vid) {
        if (vid === ev.vid || S2.genOf.get(vid) !== vgen) return;
        var pa = at(vid, ev.t);
        if (!pa) return;
        if (aliveVid(vid, ev.t) === false) { skipped++; return; }
        var ang = Math.atan2(pa[0] - pv[0], pa[2] - pv[2]) + Math.PI;
        var d = Math.abs(((ang - ev.bearing + Math.PI) % (2 * Math.PI) + 2 * Math.PI)
                         % (2 * Math.PI) - Math.PI);
        scored.push([d, vid]);
      });
      ev.deadSkipped = skipped;
      if (!scored.length) return;
      scored.sort(function (x, y) { return x[0] - y[0] || x[1] - y[1]; });
      if (scored[0][0] <= BEARING_TOL) {
        ev.attackerVid = scored[0][1];
        ev.bearingErr = scored[0][0];
        if (scored.length > 1) {
          ev.runnerUpErr = scored[1][0];
          ev.ambiguous = scored[1][0] <= Math.max(AMBIG_RATIO * scored[0][0],
                                                  scored[0][0] + AMBIG_ABS);
        } else {
          ev.ambiguous = false;
        }
      }
    });
    nAttr += dmg.filter(function (e) { return e.attackerVid !== null; }).length;
    var byVic = new Map();
    dmg.forEach(function (e) { pushList(byVic, e.vid, e); });

    // ---- op-0x09 kill feed
    var feed = [];
    (S2.ops.get(OP_KILL) || []).forEach(function (r) {
      if (r[2] < 3) return;
      var ks = b[r[1] + 1], vs = b[r[1] + 2];
      feed.push({ t: r[0], killerSlot: ks, victimSlot: vs, used: false });
      op9Deaths.set(vs, (op9Deaths.get(vs) || 0) + 1);
      if (ks !== vs) op9Kills.set(ks, (op9Kills.get(ks) || 0) + 1);
      else nOp9Suicide++;               // a suicide is a death, not a kill
    });
    feed.sort(function (x, y) { return x.t - y.t; });
    nOp9 += feed.length;
    var byVictimSlot = new Map();
    feed.forEach(function (r) { pushList(byVictimSlot, r.victimSlot, r); });

    // ORDINAL join per lobby slot: the k-th feed record naming victim slot s is
    // the k-th HP->0 death of slot s, the slot coming from the victim's own
    // position vid.  2003/2003 corpus-wide, zero orphans, no time window.
    var deaths = deathsOf(S2);
    var deathsBySlot = new Map();
    deaths.forEach(function (d) {
      var sl = S2.slotOf.has(d[1]) ? S2.slotOf.get(d[1]) : null;
      pushList(deathsBySlot, sl, d);
    });
    var matched = new Map();
    byVictimSlot.forEach(function (rows, vslot0) {
      var ds = deathsBySlot.get(vslot0) || [];
      for (var i = 0; i < rows.length; i++) {
        if (i >= ds.length) continue;
        rows[i].used = true;
        matched.set(ds[i][0] + ':' + ds[i][1], rows[i]);
        var lag = rows[i].t - ds[i][0];
        if (lag < op9LagMin) op9LagMin = lag;
        if (lag > op9LagMax) op9LagMax = lag;
        nOp9Matched++;
      }
    });
    var unmatchedDeaths = 0;
    deaths.forEach(function (d) { if (!matched.has(d[0] + ':' + d[1])) unmatchedDeaths++; });
    nDeathsUnjoined += unmatchedDeaths;

    deaths.forEach(function (d) {
      var t = d[0], vid = d[1], pi = pidx(S2, vid);
      if (pi === null) return;
      var cand = null;
      (byVic.get(vid) || []).forEach(function (e) {
        if (e.t <= t + POS_MATCH_WINDOW) {
          if (cand === null || e.t >= cand.t) cand = e;
        }
      });
      var cause = causeOf(S2, vid, t, cand === null ? null : cand.t);
      var weapon = null, geo = null, berr = null, rerr = null, ambig = null;
      if (cand !== null && cause !== 'unknown') {
        weapon = cand.weapon;
        if (cand.attackerVid !== null) {
          geo = pidx(S2, cand.attackerVid);
          berr = cand.bearingErr; rerr = cand.runnerUpErr; ambig = cand.ambiguous;
        }
      }
      if (geo === null) { berr = null; rerr = null; ambig = null; }

      var hit = matched.get(t + ':' + vid) || null;
      var vslot = S2.slotOf.has(vid) ? S2.slotOf.get(vid) : null;
      var killer = null, killerSlot = null, source = null, suicide = null;
      if (hit !== null) {
        killerSlot = hit.killerSlot;
        suicide = hit.killerSlot === hit.victimSlot;
        var ki = slotPlayer(killerSlot, t);
        if (ki !== null) { killer = ki; source = 'op09'; nKillerOp9++; }
        else nKillerUnnamed++;
      }
      if (killer !== null) nKillAttr++;
      var hzk = (cand === null) ? undefined : HIT_ZONE[cand.subtype];
      events.push({ t: round((t - t0) / 1000, 3), type: 'kill', cause: cause,
                    hitZone: (hzk === undefined ? null : hzk),
                    zoneSubtype: (cand === null ? null : cand.subtype),
                    killer: killer, victim: pi, weapon: weapon,
                    weaponName: itemName(weapon),
                    killerSource: source, killerSlot: killerSlot,
                    victimSlot: vslot,
                    suicide: suicide,
                    killerGeometric: geo,
                    killerGeometricAgrees: ((geo === null || killer === null)
                                            ? null : geo === killer),
                    killerBearingErr: (berr === null ? null : round(berr, 4)),
                    killerRunnerUpErr: (rerr === null ? null : round(rerr, 4)),
                    killerAmbiguous: ambig,
                    session: S2.index,
                    gen: (S2.genOf.has(vid) ? S2.genOf.get(vid) : null) });
    });

    // Feed records with no death to join to.  Zero corpus-wide under the
    // ordinal join; counted in meta.op9Orphan and NOT emitted.
    feed.forEach(function (r) { if (!r.used) nOp9Orphan++; });

    // ---- objective carriers: state[40..43] of the 0x1254 state block
    var state = new Array(89).fill(null);
    var holder = new Array(STATE_HOLDER1 - STATE_HOLDER0 + 1).fill(null);
    var writes = [], capPrev = [null, null], resPrev = [null, null];
    (S2.ops.get(OP_STATE) || []).forEach(function (r) {
      if (r[2] >= 90) {
        var arr = [];
        for (var k = 0; k < 89; k++) arr.push(b[r[1] + 1 + k]);
        writes.push([r[0], 0, arr]);
      }
    });
    (S2.ops.get(OP_WRITE) || []).forEach(function (r) {
      var L = r[2];
      if (L >= 3 && 3 + b[r[1] + 2] <= L) {
        var arr2 = [];
        for (var k2 = 0; k2 < b[r[1] + 2]; k2++) arr2.push(b[r[1] + 3 + k2]);
        writes.push([r[0], b[r[1] + 1], arr2]);
      }
    });
    writes.sort(function (x, y) { return x[0] - y[0]; });
    writes.forEach(function (wr) {
      for (var k = 0; k < wr[2].length; k++) {
        if (wr[1] + k >= 0 && wr[1] + k < 89) state[wr[1] + k] = wr[2][k];
      }
      // CAPTURE: state[42] / state[43] are the two teams' CAPTURE CLOCKS. Each
      // starts at 30 and loses one a second, but only while that team has a
      // target put in the goal; at 0 the round is won. MEASURED: 63 of 63 ticks
      // fall while a player of that team (42 = raw team 0, 43 = raw team 2) has
      // one in, 1.0 s apart; the winner's reads 0 at the end of both rounds of
      // replay_299954_9 (replay/itembox/cap_clock.py). Other modes reuse these
      // bytes, so the viewer only reads this in Capture.
      // WHO WON THE ROUND: state[2], written once at round end. 2 = raw team 0,
      // 3 = raw team 2, 1 = drawn, 6 = no result. MEASURED 2026-10-05 against
      // the scoreboard's rounds-won counter over 28 round ends in 16 replays
      // (replay/itembox/round_result.py): 2 <-> team 0 took the step 14 of 14,
      // 3 <-> team 2 10 of 10, and nobody stepped on the 2 draws and 2 no-results.
      // (An older note called this "not the winner" because the side with more
      // KILLS did not always match it. Kills do not decide a round.)
      // state[3] is written with the LAST round and is taken to be the match
      // result in the same coding -- 12 samples, not independently checked.
      if (state[2] !== null && state[2] !== resPrev[0]) {
        resPrev[0] = state[2];
        if (state[2] !== 255) roundResults.push({ t: round((wr[0] - t0) / 1000, 3), code: state[2],
          winner: state[2] === 2 ? 0 : state[2] === 3 ? 2 : null, draw: state[2] === 1, session: S2.index });
      }
      if (state[3] !== null && state[3] !== resPrev[1]) {
        resPrev[1] = state[3];
        if (state[3] === 2 || state[3] === 3 || state[3] === 1) matchResult = { t: round((wr[0] - t0) / 1000, 3), code: state[3],
          winner: state[3] === 2 ? 0 : state[3] === 3 ? 2 : null, draw: state[3] === 1 };
      }
      for (var cf = 42; cf <= 43; cf++) {
        if (state[cf] !== null && state[cf] !== capPrev[cf - 42]) {
          capPrev[cf - 42] = state[cf];
          capClock.push({ t: round((wr[0] - t0) / 1000, 3), team: cf === 42 ? 0 : 2, left: state[cf], session: S2.index });
        }
      }
      for (var j = STATE_HOLDER0; j <= STATE_HOLDER1; j++) {
        var v = state[j];
        if (v === null || v === holder[j - STATE_HOLDER0]) continue;
        var prev = holder[j - STATE_HOLDER0];
        holder[j - STATE_HOLDER0] = v;
        if (prev === null) { nObjInitial++; continue; }
        var slot = null, flag = null;
        if (v === HOLDER_NOBODY) {
          slot = null; flag = null;
        } else if (v !== HOLDER_NONE && occupiedSlots.has(v & 0x3f)) {
          slot = v & 0x3f;
          flag = (v & HOLDER_FLAG) !== 0;
        } else {
          nObjRejected++;
          continue;
        }
        objectiveEvents++;
        events.push({ t: round((wr[0] - t0) / 1000, 3), type: 'objective',
                      item: j - STATE_HOLDER0, carrierSlot: slot,
                      carrier: (slot === null ? null : slotPlayer(slot, wr[0])),
                      carrierFlag40: flag,
                      session: S2.index });
      }
    });

    // ---- round clock (op 0x01), change points only
    var prevUnits = null;
    (S2.ops.get(OP_CLOCK) || []).slice().sort(function (x, y) { return x[0] - y[0]; })
      .forEach(function (r) {
        if (r[2] < 3) return;
        var units = b[r[1] + 1] | (b[r[1] + 2] << 8);
        if (units !== prevUnits) {
          prevUnits = units;
          clockPoints.push([round((r[0] - t0) / 1000, 3),
                            units * CLOCK_MS_PER_UNIT / 1000]);
        }
      });

    // ---- op 0x0E round over + reason byte (see replay_parse.py)
    (S2.ops.get(0x0E) || []).slice().sort(function (x, y) { return x[0] - y[0]; })
      .forEach(function (r) {
        if (r[2] < 2) return;
        roundEnds.push({ t: round((r[0] - t0) / 1000, 3), code: dv.getUint8(r[1] + 1), session: S2.index });
      });

    // ---- op 0x12: RACE, a team's goal changed.  [0x12, flags, newGoal, spawn]
    // flags bit7 = the team SCORED (clear = its target was reset), bit0 = team.
    // newGoal / spawn are POINT NUMBERS (replay/race_points.py has where they
    // are). MEASURED on replay_347079_2: on all 14 scores the spawn written is
    // the goal the previous record gave that team; on all 14 resets the spawn
    // is unchanged; real respawn points sit on the named point.
    // ---- op 0x04: the OBJECTIVE FEED, [0x04, kind, lobby slot]. Kinds seen, by
    // mode (23 replays, 2026-10-04): Race / Capture / Team Sneaking 0x00 took a
    // target, 0x01 let go of it; Capture 0x02 PUT a target in the goal; Bomb
    // 0x0c took the bomb, 0x0d let go, 0x0e PLANTED; Race 0x12 scored a goal.
    // 0x0e equals the 0x40-flag plants 4 of 4. In Capture every 0x40-flag put
    // has a feed 0x02 (26 of 26) and the feed has 15 MORE, so the flag
    // under-counts puts and the feed is the count to use.
    (S2.ops.get(0x04) || []).slice().sort(function (x, y) { return x[0] - y[0]; })
      .forEach(function (r) {
        if (r[2] < 3) return;
        var sl = dv.getUint8(r[1] + 2);
        objFeed.push({ t: round((r[0] - t0) / 1000, 3), kind: dv.getUint8(r[1] + 1), slot: sl,
                       player: slotPlayer(sl, r[0]), session: S2.index });
      });
    // WHO scored: op 0x04 = [0x04, kind, lobby slot] is the objective feed, and
    // kind 0x12 is written in the same tick as a scoring op 0x12, naming the
    // carrier (0x04 0x12 <slot 2> right after blue's first goal on that match).
    var goalBy = (S2.ops.get(0x04) || []).filter(function (r) {
      return r[2] >= 3 && dv.getUint8(r[1] + 1) === 0x12;
    });
    (S2.ops.get(0x12) || []).slice().sort(function (x, y) { return x[0] - y[0]; })
      .forEach(function (r) {
        if (r[2] < 4) return;
        var fl = dv.getUint8(r[1] + 1), sc = !!(fl & 0x80), slot = null, who = null;
        if (sc) {
          var bestD = 600;
          goalBy.forEach(function (g) {
            var dd = Math.abs(g[0] - r[0]);
            if (dd <= bestD) { bestD = dd; slot = dv.getUint8(g[1] + 2); }
          });
          if (slot !== null) who = slotPlayer(slot, r[0]);
        }
        raceOps.push({ t: round((r[0] - t0) / 1000, 3), score: sc, team: fl & 1,
                       goal: dv.getUint8(r[1] + 2), spawn: dv.getUint8(r[1] + 3),
                       scorerSlot: slot, scorer: who, session: S2.index });
      });

    // ---- THE GAME'S OWN SCOREBOARD. Indexed type 0x0B on 0x1001, LEN 23..45:
    //   [0b, lobby slot + 1, 21-byte bit mask (LSB first), one value per set bit]
    // every value a u16 LE except bit 152 (u8). Written whenever one of that
    // player's numbers changes, carrying the NEW value. MEASURED 2026-10-05
    // (replay/itembox/statrec.py): the length rule holds on 10,135 of 10,301
    // records in 16 replays; bit 5 (kills) and bit 6 (deaths) equal the kill
    // feed for 202 of 211 players. Named so far: 5 kills, 6 deaths, 8 score,
    // 11 headshots, 152 last-result code, 153 streak. Rows: [t, player, bit,
    // value]; a record that breaks the length rule is skipped, not guessed.
    // One-byte values: bit 1 and bits 128..152 (with them the length rule holds on
    // 10,301 of 10,301 records). 131..136 are the three equipped SKILLS as
    // (id, level) pairs, changing when a player swaps skills mid-match.
    function statU8(bi) { return bi === 1 || (bi >= 128 && bi <= 152); }
    S2.stat.forEach(function (r) {
      var a = r[1], L = r[2], sl = b[a + 1] - 1, need = 0, setb = [], bi;
      for (bi = 0; bi < 168; bi++) if (b[a + 2 + (bi >> 3)] >> (bi & 7) & 1) { setb.push(bi); need += statU8(bi) ? 1 : 2; }
      if (23 + need !== L) { statSkipped++; return; }
      var pi = slotPlayer(sl, r[0]), o = a + 23, tt = round((r[0] - t0) / 1000, 3);
      setb.forEach(function (bit) {
        var v = statU8(bit) ? b[o] : (b[o] | (b[o + 1] << 8));
        o += statU8(bit) ? 1 : 2;
        if (pi != null) statOps.push([tt, pi, bit, v, S2.index]);
      });
    });

    // ---- THE CARRIED TARGETS THEMSELVES: objects 0x1256 (item 0) / 0x1257
    // (item 1). type 00 [slot] = taken by that slot; slot byte 0xfd = sent home
    // (it comes exactly 30.0 s after the target was left lying, 5 of 5 on one
    // Race match), 0xfe = Bomb only, unread. type 01 [00, s16 X, s16 Y, s16 Z,
    // u8] = WHERE IT LIES: X / Z in the usual x10 unit, Y in plain mm. MEASURED
    // on 142 such records in 12 replays: 135 are within 1.5 m of the last
    // carrier (median 0.2 m). Players' own frame, like the item boxes.
    S2.target.forEach(function (r) {
      var a = r[3], item = r[1] === 0x256 ? 0 : 1, tt = round((r[0] - t0) / 1000, 3);
      if (r[2] === 0) {
        var sl = b[a + 1];
        if (sl === 0xfd) targetOps.push({ t: tt, item: item, kind: 'home', session: S2.index });
        else if (sl === 0xfe) targetOps.push({ t: tt, item: item, kind: 'fe', session: S2.index });
        else targetOps.push({ t: tt, item: item, kind: 'take', slot: sl, player: slotPlayer(sl, r[0]), session: S2.index });
      } else {
        targetOps.push({ t: tt, item: item, kind: 'lie', session: S2.index, tail: b[a + 8],
                         p: [dv.getInt16(a + 2, true) * SCALE, dv.getInt16(a + 4, true), dv.getInt16(a + 6, true) * SCALE] });
      }
    });

    // ---- the game's own STREAK NOTICES: object 0x1258 type 0x29 [slot, kind,
    // count], 0.1-0.2 s after the kill. MEASURED on 12 replays against a kill
    // count kept from the feed: kind 2 count 3 = that player's 3rd kill in a row
    // (84 of 109 exact), kind 1 count 5 = his 5th (24 of 29), kind 3 count 5 /
    // 10 = the named player just died again with no kill since (27 of 30).
    // ---- SOP DESTABILIZER announcements (Base mission): object 0x1258, [type, lobby slot].
    //   0x13 = that player BOUGHT one (the ping everyone gets; user, 2026-10-08). 8 records in 158 SaveMGO files,
    //          all in Base, each 0.0 - 0.3 s before its player is first seen holding item 68 (8 of 8).
    //   0x16 = that player's destabilizer is GONE: 6 records, each after a 0x13 for the same slot; 5 of 6 at his
    //          death. In the one plant on record it comes 0.3 s after he is shot, the device already placed.
    // MEASURED 2026-10-08 (scratch loop/buy_raw.js, n13.js, n16.js).
    // THE TEAM'S DESTABILIZER STATUS, rule state 0x37 (one side) and 0x38 (the other), one byte, written with the
    // ordinary state-write op. MEASURED 2026-10-08 on 36 Base files (scratch loop/st37.js, fire_raw2.js):
    //   255 = none            a lobby slot = that player now carries one (at each known purchase, 0.3 - 0.4 s before
    //   254 = lost            the 0x13 notice; 11 in all)          (at each 0x16 notice / the carrier's death; 10)
    //   253 = IT WENT OFF     (once: match 52613 at 161.5 s, the same instant as the planter's stat-57 credit)
    // Present in recordings old and new, unlike the notices, so this is the first source; the notices are the fallback.
    var dsHolder = {}, dsFromState = 0;
    (S2.ops.get(OP_WRITE) || []).slice().sort(function (x, y) { return x[0] - y[0]; }).forEach(function (r) {
      var si = b[r[1] + 1];
      if (r[2] !== 4 || (si !== 0x37 && si !== 0x38) || b[r[1] + 2] !== 1) return;
      var v = b[r[1] + 3], tt = round((r[0] - t0) / 1000, 3);
      if (v < 64) { dsHolder[si] = v; destabOps.push({ t: tt, slot: v, player: slotPlayer(v, r[0]), kind: 'bought', src: 'state', session: S2.index }); dsFromState++; }
      else if ((v === 254 || v === 253) && dsHolder[si] != null) {
        destabOps.push({ t: tt, slot: dsHolder[si], player: slotPlayer(dsHolder[si], r[0]), kind: v === 253 ? 'fired' : 'gone', src: 'state', session: S2.index });
        dsFromState++;
      }
      if (v === 255 || v === 253) delete dsHolder[si];     // 254 keeps the name: 253 follows it when a placed one goes off
    });
    if (!dsFromState) S2.destab.forEach(function (r) {
      destabOps.push({ t: round((r[0] - t0) / 1000, 3), slot: r[1], player: slotPlayer(r[1], r[0]),
                       kind: r[2] === 0x13 ? 'bought' : 'gone', session: S2.index });
    });
    S2.notice.forEach(function (r) {
      streakOps.push({ t: round((r[0] - t0) / 1000, 3), slot: r[1], player: slotPlayer(r[1], r[0]),
                       kind: r[2], count: r[3], session: S2.index });
    });

    // ---- rules op 0x05 [slot]: names one player; meaning NOT decoded (kept so
    // it can be tested: replay/itembox/op05_test.js)
    (S2.ops.get(0x05) || []).forEach(function (r) {
      if (r[2] !== 2) return;
      nameOps.push({ t: round((r[0] - t0) / 1000, 3), slot: b[r[1] + 1], player: slotPlayer(b[r[1] + 1], r[0]), session: S2.index });
    });

    // ---- TEAM SNEAKING: state[87] is a 5 s countdown and state[88] a lobby
    // slot, written together (02 57 02 05 <slot>) and re-written at 5 when it
    // is retriggered. See meta.sneakTimer's note for what it is taken to mean.
    (S2.ops.get(OP_WRITE) || []).slice().sort(function (x, y) { return x[0] - y[0]; })
      .forEach(function (r) {
        if (r[2] < 5 || b[r[1] + 1] !== 87 || b[r[1] + 2] !== 2) return;
        sneakTimer.push({ t: round((r[0] - t0) / 1000, 3), secs: b[r[1] + 3], slot: b[r[1] + 4],
                          player: slotPlayer(b[r[1] + 4], r[0]), session: S2.index });
      });

    // ---- objects 0x1250 (things lying in the world: the two ITEM BOXES and
    // whatever a player drops) and 0x125a (placed devices: the BOMB).
    // One event = up to four records in one tick: type 00 LEN 6 HEAD, type 01
    // LEN 6 ITEM, type 02 LEN 10 POSITION (s16 X, Y, Z -- X / Z in the usual
    // x10 unit, Y in plain mm), type 03 LEN 9 tail. All-zero pairs are filler.
    //   0x1250  HEAD [code, n, object id, slot, ?]   ITEM[0] = item id
    //     0x74 a box appears (n = box 1 / 2, item 0x8c / 0x71), each round
    //     0x83 taken by `slot` (taker within 2.5 m of the object, 21 of 21)
    //     0x64 dropped at a player's hip; object id = dropper slot << 3 (20 of 20)
    //   0x125a  item 0x2e = the bomb
    //     0x65 planted (7 of 7 within 3 s of the 0x40-flag plant)
    //     0xa0 gone, ITEM [2e, slot, ?, 0, reason]: 3 = DEFUSED by slot (5 of 5
    //          that player is 0.6-1.4 m away using coolant), 1 = detonated
    //          (2 of 2, 60.0 s after the plant)
    // MEASURED 2026-10-04 on 12 replays (replay/itembox/obj_test.py).
    var wcur = null;
    S2.world.forEach(function (r) {
      var a = r[3], L = r[4];
      if (r[2] === 0 && L === 6) {
        wcur = null;
        if (!(b[a + 1] | b[a + 2] | b[a + 3] | b[a + 4] | b[a + 5])) return;
        wcur = { t: round((r[0] - t0) / 1000, 3), o: r[1] === 0x250 ? 0 : 1, code: b[a + 1], n: b[a + 2],
                 id: b[a + 3], slot: b[a + 4], h4: b[a + 5], item: null, it: null, p: null,
                 who: null, session: S2.index, _t: r[0], _low: r[1] };
        // who: the taker (0x83), or the dropper (0x64)
        // (the slot is the low 5 bits: Capture files set high bits on it, and
        // the low 5 name the player who then drops that same item, 4 of 4)
        if (wcur.o === 0 && wcur.code === 0x83) wcur.who = slotPlayer(wcur.slot & 0x1f, r[0]);
        else if (wcur.o === 0 && wcur.code === 0x64) wcur.who = slotPlayer(wcur.id >> 3, r[0]);
        worldOps.push(wcur);
      } else if (wcur && wcur._t === r[0] && wcur._low === r[1]) {
        if (r[2] === 1 && L === 6) {
          wcur.it = [b[a + 1], b[a + 2], b[a + 3], b[a + 4], b[a + 5]];
          wcur.item = b[a + 1];
          // the bomb going away names its defuser
          if (wcur.o === 1 && wcur.code === 0xa0 && b[a + 5] === 3) wcur.who = slotPlayer(b[a + 2], r[0]);
        } else if (r[2] === 2 && L === 10) {
          // In the players' own frame (no launch-frame shift: the dropper is
          // 0.0 m from his drop). A dropped thing is reported at his hip.
          var hip = (wcur.o === 0 && wcur.code === 0x64) ? ydrop : 0;
          wcur.p = [dv.getInt16(a + 1, true) * SCALE, dv.getInt16(a + 3, true) - hip,
                    dv.getInt16(a + 5, true) * SCALE];
        }
      }
    });

    // ---- op 0x13 per-player s16 array  [PROBABLE]
    (S2.ops.get(OP_RATING) || []).slice().sort(function (x, y) { return x[0] - y[0]; })
      .forEach(function (r) {
        if (r[2] < 7) return;
        var cnt = Math.floor((r[2] - 5) / 2), vals = [];
        for (var k = 0; k < cnt; k++) vals.push(dv.getInt16(r[1] + 5 + 2 * k, true));
        ratingRows.push({ t: round((r[0] - t0) / 1000, 3), session: S2.index, values: vals });
      });

    // ---- throwable launches (RPDT_FORMAT.md 7.9)
    S2.throw_.forEach(function (r) {
      nThrow++;
      events.push({ t: round((r[1] - t0) / 1000, 3), type: 'throw',
                    killer: pidx(S2, r[0]), victim: null,
                    killerSlot: S2.slotOf.has(r[0]) ? S2.slotOf.get(r[0]) : null,
                    weapon: r[2], weaponName: itemName(r[2]),
                    launch: toWorld(r[3][0], r[3][1], r[3][2]),
                    // the record's second point, same frame (what it is: see nade_fit.js)
                    vel: r[4] ? [r[4][0] * 10, r[4][1] * 10, r[4][2] * 10] : null,   // launch velocity, mm/s
                    session: S2.index });
    });

    // --- staminaRestore events (see STAMINA_JUMP) ---
    var ownDeaths = new Map();
    events.forEach(function (e2) {
      if (e2.type !== 'kill' || e2.victim === null || e2.victim === undefined) return;
      var lst = ownDeaths.get(e2.victim);
      if (!lst) { lst = []; ownDeaths.set(e2.victim, lst); }
      lst.push(e2.t);
    });
    var wakeRaw = [];
    S2.st.forEach(function (series, vid) {
      if (!S2.stream.has(vid)) return;
      var piW = pidx(S2, vid);
      if (piW === null) return;
      var ser = series.slice().sort(function (x, y) {
        return (x[0] - y[0]) || (x[1] - y[1]);
      });
      var prevS = null;
      ser.forEach(function (r) {
        if (prevS !== null && (r[1] - prevS) >= STAMINA_JUMP * 250) {
          wakeRaw.push([r[0], piW, prevS / 250, r[1] / 250]);
        }
        prevS = r[1];
      });
    });
    var bucket = new Map();
    wakeRaw.forEach(function (r) {
      var k = Math.floor(r[0] / 1000);
      var g = bucket.get(k);
      if (!g) { g = []; bucket.set(k, g); }
      g.push(r);
    });
    Array.from(bucket.keys()).sort(function (x, y) { return x - y; })
      .forEach(function (k) {
        var grp = bucket.get(k);
        if (grp.length >= STAMINA_MASS) return;   // round reset
        grp.forEach(function (r) {
          var et = round((r[0] - t0) / 1000, 3), back = null;
          (ownDeaths.get(r[1]) || []).forEach(function (dt2) {
            var gap = et - dt2;
            if (gap >= 0 && gap <= RESPAWN_WINDOW && (back === null || gap < back)) {
              back = gap;
            }
          });
          events.push({ t: et, type: 'staminaRestore',
                        victim: r[1], killer: null,
                        staminaFrom: round(r[2], 3), staminaTo: round(r[3], 3),
                        sincePlayerDeathSec: back === null ? null : round(back, 3),
                        session: S2.index });
        });
      });

    // Stun events are emitted ALWAYS, not only under allEvents: the in-game
    // replay killfeed shows stuns alongside kills.
    dmg.forEach(function (e) {
      if (e.damageClass !== 'nonLethal') return;
      var ai = e.attackerVid === null ? null : pidx(S2, e.attackerVid);
      var hz = STUN_SUBTYPE[e.subtype];
      events.push({ t: round((e.t - t0) / 1000, 3), type: 'stun',
                    killer: ai, victim: pidx(S2, e.vid), weapon: e.weapon,
                    weaponName: itemName(e.weapon), subtype: e.subtype,
                    hitZone: hz === undefined ? null : hz,
                    session: S2.index });
    });

    if (allEvents) {
      dmg.forEach(function (e) {
        var pi = pidx(S2, e.vid);
        var ai = e.attackerVid === null ? null : pidx(S2, e.attackerVid);
        if (ai === null && e.attackerSlot !== null && e.attackerSlot !== undefined) { var as_ = slotPlayer(e.attackerSlot, e.t); if (as_ !== null && as_ !== undefined) ai = as_; }
        events.push({ t: round((e.t - t0) / 1000, 3), type: 'damage',
                      victimAt: e.blast ? [round(e.blast[2], 1), round(e.blast[1], 1), round(e.blast[0], 1)] : null,
                      killer: ai, victim: pi, weapon: e.weapon,
                      weaponName: itemName(e.weapon),
                      amount: round(e.damage, 2), subtype: e.subtype,
                      damageClass: e.damageClass,
                      impact: toWorld(e.impact[0], e.impact[1], e.impact[2]),
                      killerBearingErr: (e.bearingErr === null ? null : round(e.bearingErr, 4)),
                      killerRunnerUpErr: (e.runnerUpErr === null ? null : round(e.runnerUpErr, 4)),
                      killerAmbiguous: e.ambiguous, bearing: (e.bearing === null || e.bearing === undefined ? null : round(e.bearing, 4)),
                      session: S2.index,
                      gen: (S2.genOf.has(e.vid) ? S2.genOf.get(e.vid) : null) });
      });
    }
  });

  function sortKey(e, k) {
    var v = e[k];
    return (v === undefined || v === null) ? -1 : v;
  }
  events.sort(function (x, y) {
    return (x.t - y.t)
        || (x.type < y.type ? -1 : x.type > y.type ? 1 : 0)
        || (sortKey(x, 'victim') - sortKey(y, 'victim'))
        || (sortKey(x, 'killer') - sortKey(y, 'killer'));
  });
  var nKills = events.filter(function (e) { return e.type === 'kill'; }).length;
  if (nDeathsUnjoined || nOp9Orphan) {
    warnings.push('KILL FEED PARTIAL: ' + nDeathsUnjoined + ' HP->0 death(s) could not be '
      + 'joined to a 0x1254 op-0x09 kill-feed record and ' + nOp9Orphan + ' feed record(s) '
      + 'could not be joined to a death. Those kills carry killer=null (or the geometric '
      + 'cross-check, see killerSource) and the unjoined feed records are NOT emitted as '
      + 'extra events, because in this file they cannot be told apart from the unjoined '
      + 'deaths and emitting them would double-count. This is the known incomplete-roster '
      + 'case: 16 of the 18 corpus files join every death exactly.');
  }

  players.forEach(function (p) {
    if (nOp9 === 0 || p.slot === null) { p.kills = null; p.deaths = null; }
    else {
      p.kills = op9Kills.get(p.slot) || 0;
      p.deaths = op9Deaths.get(p.slot) || 0;
    }
  });

  /* ---- coverage accounting (identical credit table to replay_parse.py) -- */
  var role = new Map();
  sessions.forEach(function (S2) {
    S2.stream.forEach(function (vid) {
      role.set(vid, TYPE_POS);
      role.set(vid + VID_DAMAGE, TYPE_DAMAGE);
      role.set(vid + VID_STATE, TYPE_STATE);
      role.set(vid + VID_HEALTH, TYPE_HEALTH);
      role.set(vid + VID_WEAPON, TYPE_WEAPON);
      role.set(vid + VID_AIM, TYPE_AIM);
    });
  });
  // returns [total, typeBytes, constBytes, structBytes]
  // -- see replay_parse._known_payload
  function knownPayload(vid, indexed, L, a) {
    if (L < 1) return [0, 0, 0, 0];
    var ty = b[a];
    if (indexed) {
      var low = vid & 0x7ff;
      // the opcode object FIRST: it is vid-scoped, and its opcodes collide with
      // the roster/settings type bytes.
      if (low === OPS_VID_LOW) {
        if (ty === OP_KILL && L >= 3) return [3, 1, 0, 0];
        if (ty === OP_CLOCK && L >= 3) return [3, 1, 0, 0];
        if (ty === OP_WRITE && L >= 3 && 3 + b[a + 2] <= L) return [3 + b[a + 2], 1, 0, 0];
        if (ty === OP_STATE && L >= 90) {
          return [1 + (STATE_HOLDER1 - STATE_HOLDER0 + 1), 1, 0, 0];
        }
        if (ty === OP_RATING && L >= 7) return [1 + 2 * Math.floor((L - 5) / 2), 1, 0, 0];
        return [0, 0, 0, 0];
      }
      if (low >= GAUGE_LOW_MIN && low <= GAUGE_LOW_MAX && ty === TYPE_HEALTH && L === 2) {
        return [2, 1, 0, 0];
      }
      if (TEAM_ARRAY_TYPES.indexOf(ty) >= 0 && L === TEAM_ARRAY_LEN) {
        // reads [0] type, [1] the shared sequence byte and [3..25];
        // payload[2] is the slot-0 sentinel and is NEVER read.
        return [25, 1, 0, 0];
      }
      if (ty === TYPE_ROSTER && L >= ROSTER_MIN_LEN && L >= rosterNameOff(b, a, a + L) + 1) {
        return [9 + Math.max(0, L - rosterNameOff(b, a, a + L)), 1, 0, 0];
      }
      if (ty === TYPE_SETTINGS && L >= 3) {
        if (b[a + 1] === 0x00 && L >= 71) return [2 + 45, 1, 0, 0];
        if (LOADOUT_LEN.indexOf(L) >= 0) {
          var sk3 = skillRecord(b, a, L);
          // [0] type, [1] slot, [2] team, [21] marker, [22] N, the pairs and
          // the 0xFF terminator.  Prefix, [18..19], the N block and the 23-byte
          // appearance tail all stay [U].
          if (sk3) {
            // [0] type, [1] slot, [2] team, [22] N, the pairs, the optional
            // 0x10 suffix and the 0xFF terminator.  payload[21] is NEVER
            // dereferenced -- the old comment named it as a "marker".
            if (sk3[1] === null) return [5, 1, 1, 0];
            return [5 + 2 * sk3[1].length, 1, 1, 0];
          }
        }
        return [3, 1, 0, 0];
      }
      return [0, 0, 0, 0];
    }
    if (vid === RULES_VIDS[0] || vid === RULES_VIDS[1]) {
      if (ty === TYPE_DAMAGE) {
        if (!rulesRoster(b, a, L)) return [0, 0, 0, 0];
        var term = 0;
        for (var i = a; i < a + L; i++) if (b[i] === LIST_END) term++;
        return [L, 1, term, 0];
      }
      if (ty === TYPE_STATE && L === 2 && b[a + 1] === LIST_END) return [2, 1, 1, 0];
      return [0, 0, 0, 0];
    }
    var r = role.get(vid);
    if (r === undefined) return [0, 0, 0, 0];
    if (r === TYPE_POS && ty === TYPE_POS) {
      var nb = posBytesRead(ver, L);
      if (nb < 0) return [0, 0, 0, 0];
      return [nb, 1, posConstCount(ver, L), 0];
    }
    if (r === TYPE_HEALTH && ty === TYPE_HEALTH && L === 3) return [3, 1, 0, 0];
    if (r === TYPE_DAMAGE && ty === TYPE_DAMAGE && L === 16) return [14, 1, 1, 0];
    if (r === TYPE_DAMAGE && ty === TYPE_DAMAGE && L === 12) return [12, 1, 1, 0];
    // [0] type, [1] itemFlag, [2] item id, [3] itemAttr, [5] ammo.  itemFlag
    // and itemAttr are emitted with an UNKNOWN property -> structBytes.  On
    // v1/v2 payload[3] is 0 in 100% of records, so it is a constant there.
    if (r === TYPE_WEAPON && ty === TYPE_WEAPON && L === 6) {
      return ver === 0 ? [5, 1, 0, 2] : [5, 1, 1, 1];
    }
    if (r === TYPE_WEAPON && ty === TYPE_INCAP && L === 2) return [2, 1, 0, 0];
    if (r === TYPE_WEAPON && ty === TYPE_INCAP && L === 3) return [3, 1, 1, 0];
    if (r === TYPE_AIM && ty === TYPE_AIM && L === 7) return [7, 1, 0, 0];
    // [0] type, [1] item id, [2..7] the launch point; [8..13] stays [U].
    if (r === TYPE_STATE && ty === TYPE_THROW && L === 14) return [8, 1, 0, 0];
    if (r === TYPE_STATE && ty === TYPE_STATE && L === 4) return [3, 1, 0, 0];
    if (r === TYPE_STATE && ty === TYPE_STATE && L === 10) return [9, 1, 0, 0];
    return [0, 0, 0, 0];
  }
  var framing = HDR_LEN, decoded = 0, payloadTotal = 0, undec = new Map();
  var typeBytes = 0, constBytes = 0, structBytes = 0;
  var wc = walk(b, dv, n, function (off, tick, size, kind, hdrlen, p0, p1) {
    framing += hdrlen;
    records(b, p0, p1, function (vid, indexed, f, L, a, e) {
      framing += f;
      payloadTotal += L;
      var kk = knownPayload(vid, indexed, L, a);
      var k = kk[0];
      decoded += k;
      typeBytes += kk[1];
      constBytes += kk[2];
      structBytes += kk[3];
      if (L >= 1 && L - k) {
        var key = (indexed ? 1 : 0) + '|' + b[a] + '|' + L;
        undec.set(key, (undec.get(key) || 0) + (L - k));
      }
    });
  });
  var undecTop = Array.from(undec.entries())
    .sort(function (x, y) { return y[1] - x[1]; }).slice(0, 12)
    .map(function (kv) {
      var parts = kv[0].split('|');
      return { indexed: parts[0] === '1',
               type: '0x' + ('0' + Number(parts[1]).toString(16).toUpperCase()).slice(-2),
               len: Number(parts[2]), bytes: kv[1] };
    });
  var coverage = {
    fileBytes: n, tiles: wc.walkEnd === n, structuralBytes: wc.walkEnd,
    structuralPct: round(100 * wc.walkEnd / n, 3),
    framingBytes: framing, framingPct: round(100 * framing / n, 3),
    payloadBytes: payloadTotal, payloadDecodedBytes: decoded,
    payloadDecodedPct: round(100 * decoded / (payloadTotal || 1), 3),
    typeByteBytes: typeBytes, verifiedConstantBytes: constBytes,
    structureOnlyBytes: structBytes,
    structureOnlyPct: round(100 * structBytes / (payloadTotal || 1), 3),
    fieldValueBytes: decoded - typeBytes - constBytes - structBytes,
    fieldValuePct: round(100 * (decoded - typeBytes - constBytes - structBytes)
                         / (payloadTotal || 1), 3),
    fileDecodedPct: round(100 * (framing + decoded) / n, 3),
    topUndecoded: undecTop
  };

  /* ---- tick rate, duration, degeneracy --------------------------------- */
  var tickMs = null, tickRate = null;
  if (tickDeltas.size) {
    var tot = 0;
    tickDeltas.forEach(function (v) { tot += v; });
    var ds = Array.from(tickDeltas.keys()).sort(function (x, y) { return x - y; });
    var acc = 0;
    for (var k3 = 0; k3 < ds.length; k3++) {
      acc += tickDeltas.get(ds[k3]);
      if (acc * 2 >= tot) { tickMs = ds[k3]; break; }
    }
    if (tickMs) tickRate = round(1000 / tickMs, 3);
  }
  var frameGridHz = null;
  if (times.length > 2) {
    var deltas = [];
    for (var k4 = 0; k4 + 1 < times.length; k4++) deltas.push(times[k4 + 1] - times[k4]);
    deltas.sort(function (x, y) { return x - y; });
    var med = deltas[Math.floor(deltas.length / 2)];
    frameGridHz = med > 0 ? round(1000 / med, 3) : null;
  }
  var duration = times.length >= 2 ? round((times[times.length - 1] - times[0]) / 1000, 3) : null;
  var tickSpan = firstT === null ? null : round(((lastT || 0) - (firstT || 0)) / 1000, 3);

  var posSamples = 0;
  tracks.forEach(function (tr) { posSamples += tr.samples.length; });
  var degenerate = (posSamples === 0 || nTracked === 0);
  var mp = MAPS[mapId] || null;
  var stage = mp ? mp[0] : null, stageGuess = mp ? mp[1] : null;
  if (degenerate) {
    warnings.push('DEGENERATE: this file contains no player position stream at all ('
      + posSamples + ' position samples, ' + nTracked + ' tracked players over '
      + w.chunks + ' chunks, ' + union.size + ' roster player records). It is not a '
      + 'playable match recording; duration and stage are null.');
    stage = null; stageGuess = null; duration = null;
  }

  /* ---- respawn ratio / one-life mode ----------------------------------- */
  var nDeathTr = 0, nRespawnTr = 0;
  sessions.forEach(function (S2) {
    S2.hp.forEach(function (series, vid) {
      if (!S2.stream.has(vid)) return;
      var s = series.slice().sort(cmpSample), prev = null;
      s.forEach(function (r) {
        if (prev !== null) {
          if (prev > 0 && r[1] === 0) nDeathTr++;
          else if (prev === 0 && r[1] > 0) nRespawnTr++;
        }
        prev = r[1];
      });
    });
  });
  var respawnRatio = nDeathTr ? round(nRespawnTr / nDeathTr, 4) : null;
  var oneLife = (respawnRatio !== null && respawnRatio < ONE_LIFE_RESPAWN_RATIO);
  var hpVariety = [];
  for (var k5 = 0; k5 < npl; k5++) {
    if (vitH[k5].length) hpVariety.push(new Set(vitH[k5]).size);
  }
  hpVariety.sort(function (x, y) { return x - y; });
  var hpMedVariety = hpVariety.length ? hpVariety[Math.floor(hpVariety.length / 2)] : null;
  if (oneLife) {
    warnings.push('ONE-LIFE MODE: ' + nDeathTr + ' HP->0 transitions and ' + nRespawnTr
      + ' respawns (ratio ' + respawnRatio.toFixed(2) + ', against 0.96-1.00 in every '
      + 'free-respawn file in the corpus). This is a round-based one-life mode (rotation '
      + 'rule ' + modeRule + '), so the low kill count is REAL and the kill list is '
      + 'complete. This is NOT a decode fault.');
  } else if (duration && nTracked) {
    var kpm = nKills / (duration / 60) / nTracked;
    if (kpm < LOW_KILL_RATE) {
      warnings.push('LOW DEATH RATE: ' + nKills + ' kill events over '
        + Math.trunc(duration) + ' s with ' + nTracked + ' tracked players; the '
        + 'respawn ratio is '
        + (respawnRatio === null ? 'null' : respawnRatio.toFixed(2))
        + ' (a free-respawn mode), so this is not simply a one-life mode. The health '
        + 'stream here carries a median of only '
        + (hpMedVariety === null ? 'null' : hpMedVariety)
        + ' distinct values per player; its kill list may be incomplete.');
    }
  }

  /* ---- parked intervals ------------------------------------------------ */
  var parked = [];
  tracks.forEach(function (tr) {
    var s = tr.samples;
    if (!s.length) return;
    var ox = s[0][1], oy = s[0][2], oz = s[0][3], run = null;
    function flush() {
      if (run && run[2] >= PARK_MIN_SAMPLES) {
        parked.push({ player: tr.player, t0: round((run[0] - t0) / 1000, 3),
                      t1: round((run[1] - t0) / 1000, 3), samples: run[2],
                      pos: [ox * SCALE, oy * SCALE - ydrop, oz * SCALE] });
      }
      run = null;
    }
    s.forEach(function (r) {
      if (r[1] === ox && r[2] === oy && r[3] === oz) {
        if (run === null) run = [r[0], r[0], 0];
        run[1] = r[0]; run[2] += 1;
      } else { flush(); }
    });
    flush();
  });

  /* ---- base gauge change points ---------------------------------------- */
  var baseGauge = {};
  var gaugeKeys = new Set();
  sessions.forEach(function (S2) { S2.gauge.forEach(function (_v, k) { gaugeKeys.add(k); }); });
  Array.from(gaugeKeys).sort(function (x, y) { return x - y; }).forEach(function (k) {
    var all = [];
    sessions.forEach(function (S2) { (S2.gauge.get(k) || []).forEach(function (r) { all.push(r); }); });
    all.sort(cmpSample);
    var pts = [], prev = null;
    all.forEach(function (r) {
      if (r[1] !== prev) { prev = r[1]; pts.push([round((r[0] - t0) / 1000, 3), r[1]]); }
    });
    baseGauge['0x' + ('00' + k.toString(16).toUpperCase()).slice(-3)] = pts;
  });

  var rosterGroups = {};
  var groupKeys = new Set();
  sessions.forEach(function (S2) { S2.rosterGroups.forEach(function (_v, g) { groupKeys.add(g); }); });
  Array.from(groupKeys).sort(function (x, y) { return x - y; }).forEach(function (g) {
    var u = new Set();
    sessions.forEach(function (S2) {
      var s2 = S2.rosterGroups.get(g);
      if (s2) s2.forEach(function (m) { u.add(m); });
    });
    rosterGroups['0x' + ('0' + g.toString(16).toUpperCase()).slice(-2)] =
      Array.from(u).sort(function (x, y) { return x - y; });
  });

  var namedCount = players.filter(function (p) { return p.name; }).length;
  var kindsOut = {};
  Array.from(kinds.keys()).sort(function (x, y) { return x - y; }).forEach(function (k) {
    kindsOut['0x' + ('0' + k.toString(16).toUpperCase()).slice(-2)] = kinds.get(k);
  });
  var slotToPlayer = {};
  Array.from(slot2idx.keys()).sort(function (x, y) { return x - y; }).forEach(function (s) {
    slotToPlayer[String(s)] = slot2idx.get(s);
  });

  return {
    file: filename || 'replay.dat',
    degenerate: !!degenerate,
    stage: stage,
    stageGuess: stageGuess,
    mesh: null,
    tickRate: tickRate,
    duration: duration,
    players: players.map(function (p) {
      return { index: p.index, name: p.name, clan: p.clan, team: p.team, id: p.id,
               slot: p.slot, kills: p.kills, deaths: p.deaths, tracked: p.tracked,
               positionSamples: p.positionSamples, anonymous: !!p.anonymous,
               teamGroup: p.teamGroup, skills: p.skills,
               appearance: p.appearance || null,
               clanId: p.clanId || null };
    }),
    positionVariants: pvariants,
    frames: frames,
    events: events,
    unknown: {
      notes: 'Parsed in-browser by rpdt.js (revision 2026-08-20d). ' + sessions.length
        + ' lobby session(s), ' + resets + ' tick reset(s) flattened into one monotonic '
        + 'timeline. ' + namedCount + '/' + players.length + ' players named by the '
        + 'identity fit; unnamed streams keep their positions and their arithmetic lobby '
        + 'slot but never borrow a name. '
        + 'Killers come from the 0x1254 op-0x09 kill feed ONLY (' + nKillerOp9
        + ' named, ' + nKillerUnnamed + ' feed slots with no player object); the '
        + 'geometric bearing method is a cross-check and is never published. ' + events.filter(function (e) { return e.cause === 'bleedout'; }).length
        + ' bleed-outs, '
        + events.filter(function (e) { return e.cause === 'unknown'; }).length
        + ' with no traceable cause.',
      warnings: warnings
    },
    meta: {
      mapId: mapId, formatVersion: hdr06,
      mapIdRaw: mapIdRaw, header0x06: hdr06, header0x08: hdr8,
      mode: modeRule, modeName: modeName, modeFromHint: modeFromHint, mapFromHint: mapFromHint,
      worldScale: SCALE, worldUnitsPerMetre: WORLD_UNITS_PER_METRE,
      yOffsetApplied: -ydrop,
      headingUnits: 'degrees, from u16 yaw * 360/65536; 0 deg = +Z, increasing toward +X',
      positionCell: POSITION_CELL, motionStates: motionStates(hdr06),
      frameTranslation: cvec, frameTranslationSamples: cSamples,
      itemNames: ITEM_NAMES, slotToPlayer: slotToPlayer,
      tickMs: tickMs, frameGridHz: frameGridHz, tickSpanSec: tickSpan,
      sessions: sessions.length, rosterSnapshots: rosterAt,
      rosterSlotConflicts: slotConflicts,
      chunks: w.chunks, records: nRecords, extChunks: nExt, kinds: kindsOut,
      rotation: sessions.length ? sessions[0].rotation : [],
      fileBytes: n, truncated: false, truncatedAtByte: null,
      coverage: coverage, identity: ident,
      teamsResolved: !teamSingle, teamValuesSeen: teamValues,
      tracks: tracks.map(function (tr) {
        return { session: tr.session, gen: tr.gen, vid: tr.vid, slot: tr.slot,
                 player: tr.player, samples: tr.samples.length };
      }),
      trackCount: tracks.length, trackedPlayers: nTracked,
      untrackedRosterPlayers: nGhost, anonymousTracks: nAnon,
      positionSamples: posSamples,
      damageEvents: nDmg, damageAttributed: nAttr,
      kills: nKills, killsAttributed: nKillAttr,
      healthDistinctValuesMedian: hpMedVariety,
      aimSamples: nAim, targetPointSamples: nTail,
      itemSamples: nItem, actionSamples: nAct, incapSamples: nIncap,
      positionVariantSamples: pvariants.length, throwEvents: nThrow,
      teamArrayPairs: nTeamPairs, teamArrayOccupiedMarks: nTeamOcc,
      teamGroupPlayers: players.filter(function (p) {
        return p.teamGroup !== null; }).length,
      skillPlayers: players.filter(function (p) {
        return p.skills !== null && p.skills.length; }).length,
      skillRecordsUnaligned: (function () {
        var q = 0;
        allSkills.forEach(function (v) { if (v === null) q++; });
        return q;
      })(),
      itemFlagPlayers: itemFlagPlayers,
      op9Records: nOp9, op9Matched: nOp9Matched, op9Orphan: nOp9Orphan,
      op9Suicides: nOp9Suicide,
      op9LagMs: (nOp9Matched === 0 ? null : [op9LagMin, op9LagMax]),
      killersFromOp9: nKillerOp9, killersUnnamedSlot: nKillerUnnamed,
      objectiveEventCount: objectiveEvents,
      objectiveHolderRejected: nObjRejected,
      objectiveInitialSuppressed: nObjInitial,
      chunkTickAnomalies: nTickAnom,
      roundClock: clockPoints, roundEnds: roundEnds, ratingDeltaUnverified: ratingRows,
      raceOps: raceOps, objFeed: objFeed, sneakTimer: sneakTimer, streakOps: streakOps, destabOps: destabOps, destabFrozen: destabFrozen, partnerOps: partnerOps, targetOps: targetOps, capClock: capClock, nameOps: nameOps, statOps: statOps, statSkipped: statSkipped, roundResults: roundResults, matchResult: matchResult,
      worldOps: worldOps.map(function (o) { delete o._t; delete o._low; return o; }),
      constantViolations: { positionByte13NonZero: nPos13, damageByte2NonZero: nDmg2,
                            incapByte2NotTwo: nIncap2,
                            positionDeclaredConstant: nPosConst },
      unknownPositionForms: nPosUnknown,
      respawnRatio: respawnRatio, oneLifeMode: !!oneLife,
      rulesRosterRecords: sessions.reduce(function (s2, S2) { return s2 + S2.rulesRecords; }, 0),
      rulesRosterGrammarOk: sessions.reduce(function (s2, S2) { return s2 + S2.rulesGrammarOk; }, 0),
      rulesRosterGroups: rosterGroups,
      baseGauge: baseGauge,
      parkedIntervals: parked,
      collisions: collisions, filledCells: filled, aliveNullCells: nAliveNull
    }
  };
}

/* ---- extras ------------------------------------------------------------
   Compact streams built from the cell slots the hosted build cuts off
   (targetSlot 15, incapLevel 16, aimOriginSide 22) and from the gas damage
   rows. The page calls this on a freshly parsed .dat; replay/inject_extras.js
   calls the same function to add the block to an already-built hosted replay.

     aim   [[t0, seconds, shooter, target], ...]   weapon engaging a player
     ko    [[t0, t1, player, peakLevel], ...]      incapacitated (stun / sleep / hold)
     side  [[cellsWithBitSet, cells], ...]         per player, aim-origin side
     gas   [[t, attacker (-1 unknown), victim, weaponId], ...]
     sneak [[roundStart, roundEnd, [players...]], ...]   Team Sneaking only: who
           carried the sneaking mark (itemFlag 16) in that round. It sits on one
           team at a time and swaps at the round change.

   Cells are change-triggered, so a run ends at the first cell that says
   something else, or after AIM_GAP / KO_GAP seconds of silence. */
var AIM_GAP = 0.25, KO_GAP = 2.0;
function extras(rep) {
  var names = (rep.meta && rep.meta.positionCell) || [];
  var iT = names.indexOf('targetSlot'), iK = names.indexOf('incapLevel');
  var iS = names.indexOf('aimOriginSide');
  var s2p = (rep.meta && rep.meta.slotToPlayer) || {};
  var n = rep.players.length, i;
  var aim = [], ko = [], side = [], gas = [];
  var openA = [], openK = [];
  for (i = 0; i < n; i++) { side.push([0, 0]); openA.push(null); openK.push(null); }
  function r3(v) { return Math.round(v * 1000) / 1000; }
  function closeAim(pi, t) {
    var o = openA[pi];
    if (!o) return;
    var end = Math.min(t, o.last + AIM_GAP);
    aim.push([r3(o.t0), r3(Math.max(end - o.t0, 0.033)), pi, o.tg]);
    openA[pi] = null;
  }
  function closeKo(pi, t) {
    var o = openK[pi];
    if (!o) return;
    ko.push([r3(o.t0), r3(Math.min(t, o.last + KO_GAP)), pi, o.lv]);
    openK[pi] = null;
  }
  (rep.frames || []).forEach(function (fr) {
    var row = fr.p || [], t = fr.t;
    for (var pi = 0; pi < n; pi++) {
      var c = row[pi];
      if (!c) {
        if (openA[pi] && t - openA[pi].last > AIM_GAP) closeAim(pi, t);
        if (openK[pi] && t - openK[pi].last > KO_GAP) closeKo(pi, t);
        continue;
      }
      if (iS >= 0 && c.length > iS && c[iS] != null) {
        side[pi][1]++;
        if (c[iS]) side[pi][0]++;
      }
      var tg = (iT >= 0 && c.length > iT && c[iT] != null) ? s2p[String(c[iT])] : null;
      if (tg === undefined || tg === pi) tg = null;
      if (openA[pi] && openA[pi].tg !== tg) closeAim(pi, t);
      if (tg != null) {
        if (!openA[pi]) openA[pi] = { t0: t, last: t, tg: tg };
        else openA[pi].last = t;
      }
      var lv = (iK >= 0 && c.length > iK && c[4]) ? c[iK] : null;
      if (lv == null) closeKo(pi, t);
      else if (!openK[pi]) openK[pi] = { t0: t, last: t, lv: lv };
      else { openK[pi].last = t; if (lv > openK[pi].lv) openK[pi].lv = lv; }
    }
  });
  var tEnd = rep.duration || 0;
  for (i = 0; i < n; i++) { closeAim(i, tEnd); closeKo(i, tEnd); }
  aim.sort(function (x, y) { return x[0] - y[0]; });
  ko.sort(function (x, y) { return x[0] - y[0]; });
  (rep.events || []).forEach(function (e) {
    if (e.type !== 'damage' || e.damageClass !== 'gas' || e.victim == null) return;
    gas.push([e.t, e.killer == null ? -1 : e.killer, e.victim, e.weapon]);
  });
  var sneak = [], iF = names.indexOf('itemFlag');
  if (iF >= 0) {
    var bounds = [0], rc = (rep.meta && rep.meta.roundClock) || [];
    for (i = 1; i < rc.length; i++) if (rc[i][1] > rc[i - 1][1] + 1.0) bounds.push(rc[i][0]);
    bounds.push(Infinity);
    var seen = bounds.map(function () { return {}; }), bi = 0;
    (rep.frames || []).forEach(function (fr) {
      while (fr.t >= bounds[bi + 1]) bi++;
      (fr.p || []).forEach(function (c, pi) {
        if (c && c.length > iF && c[iF] === 16) seen[bi][pi] = (seen[bi][pi] || 0) + 1;
      });
    });
    for (i = 0; i < bounds.length - 1; i++) {
      var who = Object.keys(seen[i]).map(Number).sort(function (x, y) { return x - y; });
      if (who.length) sneak.push([r3(bounds[i]), bounds[i + 1] === Infinity ? r3(tEnd) : r3(bounds[i + 1]), who]);
    }
  }
  // GEAR STATES from the same flag byte: [t0, t1, player, flag].
  //   22 = ENVG switched ON. MEASURED over 17 replays: it is set for 113 s, all
  //        of it while that player carries the ENVG box item, and never else.
  //   10 = INSIDE THE DRUM CAN: 82 s, all of it while carrying the drum can.
  //   20 = one player at a time, never a box carrier, long runs (median 15 s):
  //        taken to be the CARDBOARD BOX -- not confirmed.
  // (replay/itembox/itemflag_box.js)
  var gear = [], GEAR_GAP = 0.6;
  if (iF >= 0) {
    var openG = [];
    (rep.frames || []).forEach(function (fr) {
      (fr.p || []).forEach(function (c, pi) {
        if (!c) return;
        var fl = (c.length > iF && c[4]) ? c[iF] : null;
        if (fl !== 10 && fl !== 20 && fl !== 22) fl = null;
        var o = openG[pi];
        if (o && (o.fl !== fl || fr.t - o.last > GEAR_GAP)) {
          gear.push([r3(o.t0), r3(Math.min(fr.t, o.last + GEAR_GAP)), pi, o.fl]); o = openG[pi] = null;
        }
        if (fl != null) { if (!o) openG[pi] = { t0: fr.t, last: fr.t, fl: fl }; else o.last = fr.t; }
      });
    });
    openG.forEach(function (o, pi) { if (o) gear.push([r3(o.t0), r3(o.last + 0.033), pi, o.fl]); });
    gear.sort(function (x, y) { return x[0] - y[0]; });
  }
  return { v: 3, aim: aim, ko: ko, side: side, gas: gas, sneak: sneak, gear: gear };
}

/* LAST RESORT for a file with no settings block and no caller hint (a dropped .dat): three
   modes leave a record no other mode has. Measured 2026-10-08 on 51 SaveMGO matches over
   2 minutes long: Race goal records (meta.raceOps) in 17 of 17 Race and nowhere else; base
   gauges in 10 of 10 Base and nowhere else; the sneak timer in Team Sneaking only. Capture,
   Bomb and Rescue share one signature and are NOT guessed. Flagged meta.modeInferred. */
function parseWithModeFallback(buf, filename, opts) {
  var r = parse(buf, filename, opts), m = r && r.meta;
  if (m && m.mode == null) {
    var bases = 0, bg = m.baseGauge || {};
    for (var k in bg) if (bg[k] && bg[k].length > 3) bases++;
    var guess = (m.raceOps && m.raceOps.length) ? 16 : bases ? 5 : (m.sneakTimer && m.sneakTimer.length) ? 7 : null;
    if (guess !== null) { m.mode = guess; m.modeName = MODE_NAMES[guess]; m.modeInferred = true; }
  }
  return r;
}
global.RPDT = { parse: parseWithModeFallback, extras: extras, MAPS: MAPS, MODE_NAMES: MODE_NAMES,
                ITEM_NAMES: ITEM_NAMES };

})(typeof window !== 'undefined' ? window : this);
