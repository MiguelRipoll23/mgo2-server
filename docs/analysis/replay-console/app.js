/* MGO2 Replay Console — playback engine, renderer and HUD.
   Data contract: see VISUALIZER_CONTRACT.md
     replay = {file, stage, stageGuess, tickRate, duration, players[], frames[], events[], unknown}
     frame  = {t, p:[ [x,y,z,heading,alive] | null, ... ]}
     map    = {stage, name, bounds:{min,max}, positions:[], indices:[]}
   Game space is Y-up, ~100 world units per metre. We scale by 1/100 and
   recentre on the map so the camera works in metres. */
(function () {
'use strict';

var S = 0.01;                 // world units -> metres
// Team 0 is RED and team 1 is BLUE: SaveMGO's redTeam is this viewer's team 0 in 113 of 113
// matches (overnight audit, 2026-10-08). Until then team 0 was drawn blue as 'Team Alpha'.
var TEAM = ['#E2574C', '#4EA8DE', '#E8B33C', '#9B8FD1'];
var AMBER = '#FF7A18';

// ── tiny helpers ─────────────────────────────────────────────────────────
function $(id) { return document.getElementById(id); }
function clamp(v, a, b) { return v < a ? a : v > b ? b : v; }
function lerp(a, b, t) { return a + (b - a) * t; }
function fmt(s) {
  if (!isFinite(s)) s = 0;
  s = Math.max(0, s);
  var m = Math.floor(s / 60), r = Math.floor(s % 60);
  return m + ':' + (r < 10 ? '0' : '') + r;
}
function esc(s) {
  return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c];
  });
}
function teamColor(t) { return TEAM[(t == null ? 2 : t) % TEAM.length]; }
// meta.itemNames indexes the equipped-item id from frame cell 12
// The binary's item table is right for ~297 entries but wrong for a couple at
// the low end, confirmed against a player's own account of the match:
// The binary's ST KNIFE string sits ONE ROW LATE: it names id 2, but id 2 is
// used at a median 12.56 m (p90 31.4 m) across 30 events, which no knife
// reaches. The name belongs to id 1, measured at a median 0.88 m (p90 1.86 m)
// over 10 events -- melee, and the only id that is.
//   id 1 = ST KNIFE, the melee stun knife: 14 kills + 7 stuns, all point-blank.
//   id 2 = MK2 ranged tranquilliser: 33 events, 100% stun, 17 head / 16 body.
// Both confirmed by a player's account of his own death -- stunned in the head
// at 25.78s by id 2, then finished at 29.78s from 0.88 m by id 1.
// THE SAME ONE-ROW SLIP REACHES THE PISTOLS (user, 2026-10-04: "is the fiveseven
// the gsr?"). Magazine size settles it, read off the ammo cell over 21 replays:
//   id 7, named FIVESEVEN: 77 reloads from empty all land on 8, top value 9
//         (8 + one chambered). The GSR holds 8; a Five-seveN holds 20. And the
//         string on the NEXT row, id 8, is "SIG GSR".
//   id 3, named RUGER: tops out at 8 (7 + 1) = the Operator, the string on id 4.
//   id 2 tops out at 11 (10 + 1) = the Mk.2, whose string "RUGER" is on id 3.
// The slip has ended by id 12: COOLANT is only ever out with the coolant action.
// Ids 4-6 and 8-11 never appear in a replay here, so they are left alone.
var ITEM_FIX = { 1: 'ST KNIFE', 2: 'MK2', 3: 'OPERATOR', 7: 'GSR' };
var MESH_FIX = { 2: 'rugr', 3: 'oprt', 7: 'sgsr' };
// Ids that are a KIND OF DAMAGE in the game's list, not something a player
// holds. 117 "TORNADO" sits between the explosions and the Gekko attacks.
// MEASURED over 21 replays: 6 hits in all, each 0.01-0.11 of a point, a single
// tick with no burn series after it, nobody ever has it equipped. 5 of the 6
// follow a grenade throw by 0.3-2.4 s (the fuse is 2 s), one has no throw in
// the 8 s before it. So probably the outer edge of a blast -- NOT settled, and
// not fire. Not shown as a weapon.
var NOT_A_WEAPON = { 117: 1 };
function itemName(id) {
  if (id == null || !R || !R.meta || !R.meta.itemNames) return null;
  if (NOT_A_WEAPON[id]) return null;
  if (ITEM_FIX[id]) return ITEM_FIX[id];
  var n = R.meta.itemNames[id];
  return (!n || n === 'EMPTY' || n === 'NONE') ? null : n;
}
function feedWeapon(e) {
  if (e._knife) return e._knife;
  if (NOT_A_WEAPON[e.weapon]) return e.cause === 'bleedout' ? 'bled out' : null;
  return itemName(e.weapon) || e.weaponName ||
         (typeof e.weapon === 'number' ? 'wpn ' + e.weapon : null);
}
// sRGB -> linear: three's working colour space is linear from r152 on.
// ?surf=0 goes back to the height-ramp look.
var SURF_ON = (function () {
  try { return new URLSearchParams(location.search).get('surf') !== '0'; }
  catch (e) { return true; }
})();
var hemiLight = null;
// What each collision surface is made of, as a display colour. The names are the
// game's own material stems (66 materials, <stem>_<variant>); the colours are
// MINE, picked to read as that material -- nothing in the files says what shade
// concrete is.
var SURF_MAT = {
  CONC: 0x8b8a84, BRIC: 0x9a5642, SAND: 0xc4ad78, SOIL: 0x6d543a, GRAS: 0x5f7a3c,
  TURF: 0x55753a, LEAF: 0x3f6a30, RLEF: 0x6a5a30, SNOW: 0xe4eaee, ICED: 0xcfe2ec,
  ROCK: 0x7a766d, GRAV: 0x8d887d, WOOD: 0x7d5c3c, FWOD: 0x705c42, IRON: 0x666e76,
  TIN: 0x8f989f, RUST: 0x8c4d2c, GRIL: 0x4d535a, FENC: 0x5c636b, TILE: 0xb8b3a5,
  GLAS: 0x7fb2c2, SHAT: 0x9bc4cc, PLAS: 0x737e8a, VINL: 0x545e6a, RUBB: 0x333336,
  CLOT: 0x7c6c7c, PAPE: 0xd6d0ba, WATE: 0x2f6f9c, TLRF: 0x8a5a48, AIR: 0x566058
};
// The 43-entry colour palette (C + 3 letters). Readings of the names; index 0
// of every world is an unnamed default and is left to the material.
var SURF_COL = {
  CBLK: 0x242424, CWHT: 0xdcdcdc, CGRY: 0x8a8a8a, CDGY: 0x565656, CLGY: 0xbcbcbc,
  CBRW: 0x6b4a2e, CDBR: 0x4a3220, CLBR: 0x9a7650, CGRN: 0x3f7a3a, CDGR: 0x2f5230,
  CLGR: 0x7fae6a, COLV: 0x6b7040, CDOL: 0x4a4f2c, CLOL: 0x8f9460, CRED: 0xb03a30,
  CDRD: 0x7a2620, CBLU: 0x3a5fae, CDBL: 0x26407a, CSBL: 0x6fa6d8, CLSB: 0x9cc8e8,
  CYLW: 0xd8c23a, CORN: 0xd87a2a, CGLD: 0xc8a23a, CSLV: 0xb8bcc0, CCYN: 0x3ab0b8,
  CAQU: 0x4ab8a0, CDAQ: 0x2a7a6a, CLAQ: 0x8ad8c0, CVIO: 0x7a4ab0, CDVO: 0x4a2a7a,
  CLVO: 0xa88ad8, CPUP: 0x8a3a9a, CDPP: 0x5a2466, CLPP: 0xb87ac8, CCPP: 0xb87a4a,
  CMGN: 0xc03a9a, CCHO: 0x4a2c1c, CAMO: 0x5a6040, CSLM: 0xc87a6a, CLSL: 0xe0a898
};
// Colour tags the map files carry as bare hashes: names recovered 2026-10-04 by
// trying every 'C' + 3 character name against the game's name hash.
var SURF_TAG = {'22b932':'CLGR','22b939':'CLGY','228d72':'CAIR','22baac':'CLSL','22ba2c':'CLOL','22a699':'CGRY','22c5d6':'COLV','22954f':'CCHO','2291cb':'CBLK','22edd7':'CYLW','229d6c':'CEIL','22a68e':'CGRN','22ba50':'CLPP','22d5cd':'CSLM','22baa2':'CLSB','228e75':'CAQU','229b0f':'CDVO','229871':'CDAQ','22d48c':'CSBL','22b871':'CLAQ','22d0e4':'CRED','22c68e':'CORN','2291d5':'CBLU','22bb0f':'CLVO','22976e':'CCYN','228def':'CAMO','229650':'CCPP','22bd2e':'CMGN','22caf0':'CPUP','229a50':'CDPP','22d5d6':'CSLV','22e16f':'CVIO','22a5c4':'CGLD'};
function surfaceRgb(pair) {
  var mat = SURF_MAT[String(pair[0]).split('_')[0]];
  if (mat === undefined) return null;
  var r = (mat >> 16 & 255) / 255, g = (mat >> 8 & 255) / 255, b = (mat & 255) / 255;
  var c = SURF_COL[SURF_TAG[pair[1]] || pair[1]];
  if (c !== undefined) {                   // tint toward the surface's own colour tag
    r = lerp(r, (c >> 16 & 255) / 255, 0.55);
    g = lerp(g, (c >> 8 & 255) / 255, 0.55);
    b = lerp(b, (c & 255) / 255, 0.55);
  }
  return [r, g, b];
}

function srgb(c) { c = clamp(c, 0, 1); return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); }

// ── state ────────────────────────────────────────────────────────────────
var R = null;        // replay
var M = null;        // map
var T = 0;           // playhead seconds
var playing = false;
var speed = 1;
var camMode = 'orbit';
var showTrails = true;
var follow = -1;     // selected player index
// Screen rects for every drawn nametag, refreshed each frame, so a tap on the
// 3D view can hit a player directly. On a phone the roster is a pane you have
// to open, tap and close again just to change who you are watching.
var hudPick = [];
// Director mode: chase whoever the next kill involves. Off by default.
var autoFollow = false, dirLastSwitch = -1e9;
var live = [];       // per-player interpolated {x,y,z,h,alive,seen}
var score = [];      // per-player {k,d}
var feedShown = -1;
var mobile = false;
var headScale = 1;   // multiplier to convert a replay's heading into radians
var HOLD_S = 0.75;   // how long a missing player keeps their last known sample
// Interpolating a player ACROSS a gap rather than parking them in it. Bounds
// are measured off real rolls: 0.45-0.95 s covering 3.0-4.4 m. Past these it
// is an absence or a respawn, not a move, and the old hold-then-hide applies.
var GAP_LERP_MAX_S = 1.20;
var GAP_LERP_MAX_MM2 = 6000 * 6000;

// ── three.js scene ───────────────────────────────────────────────────────
var renderer, scene, cam, mapMesh, mapEdges, groundGrid, clipMesh, objGroup;
var showClip = false;
var showObj = true;
// KEROTAN AND GA-KO, the game's own frog and duck (replay/prop_pack.py ->
// props.json + wtex/). They load in the background: a target is first drawn as
// the old diamond and swapped for its model the moment the pack is in.
// WHICH target is the frog and which the duck is NOT in any file decoded so far
// (placements are named TGT_01 / TGT_02): the first is drawn as Kerotan and the
// second as GA-KO, the same order the colours already used.
var PROPS = null, propsAsked = false, PROP_MATS = {}, tgtProps = [];
var GOAL_HALF_MM = 2000;
var TGT_SCALE = 5, CARRY_SCALE = 1.8;      // times life size (they are 29 cm tall)
var CARRY_UP = 10.5, CARRY_BACK = 2.6;      // scene units (1 = 10 cm): shoulder-blade height, just behind the spine
function loadProps() {
  if (propsAsked) return;
  propsAsked = true;
  fetch(av('props.json') + (ASSET_V ? '&' : '?') + 'p=2').then(function (r) { return r.ok ? r.json() : null; }).then(function (P) {
    if (!P) return;
    var out = {};
    Object.keys(P).forEach(function (k) {
      var m = P[k], pos = new Float32Array(m.pos.length), uv = new Float32Array(m.uv.length), i;
      for (i = 0; i < pos.length; i++) pos[i] = m.pos[i] / m.q;
      for (i = 0; i < uv.length; i++) uv[i] = m.uv[i] / m.uvq;
      var g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
      g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
      g.setIndex(m.idx);
      g.computeVertexNormals();
      out[k] = { geo: g, tex: m.tex, h: m.h, ymin: m.ymin || 0 };
    });
    PROPS = out;
    applyTargetProps();
  }).catch(function () {});
}
function propMat(kind) {
  if (!PROP_MATS[kind]) {
    PROP_MATS[kind] = new THREE.MeshLambertMaterial({ map: weaponTexture(PROPS[kind].tex), side: THREE.DoubleSide });
  }
  return PROP_MATS[kind];
}
// turn one mesh into the frog or the duck, standing on (x, y, z) in scene space
function setProp(mesh, kind, scale) {
  if (!PROPS || !PROPS[kind]) return false;
  if (mesh.geometry !== PROPS[kind].geo) {
    if (!mesh.__own) { mesh.__own = true; mesh.geometry.dispose(); mesh.material.dispose(); }
    mesh.geometry = PROPS[kind].geo;
    mesh.material = propMat(kind);
    mesh.scale.setScalar(MM * scale);
  }
  return true;
}
// A pool of meshes per prop kind, for things that come and go every frame
// (item boxes, magazines, the drum can): resetPropPool() hides them all, then
// pooledProp() hands out the next free one. null until the pack has loaded.
var propPool = {}, propUsed = {};
function resetPropPool() {
  Object.keys(propPool).forEach(function (k) {
    propUsed[k] = 0;
    for (var i = 0; i < propPool[k].length; i++) propPool[k][i].visible = false;
  });
}
function pooledProp(kind, scale) {
  if (!PROPS || !PROPS[kind] || !scene) return null;
  var pool = propPool[kind] || (propPool[kind] = []), n = propUsed[kind] || 0;
  if (pool.length <= n) {
    var m = new THREE.Mesh(PROPS[kind].geo, propMat(kind));
    scene.add(m); pool.push(m);
  }
  propUsed[kind] = n + 1;
  pool[n].scale.setScalar(MM * scale);
  return pool[n];
}
// Which model a carried / dropped objective is. In the Bomb mission the thing being carried is
// THE BOMB, not a frog (user, 2026-10-05, on seeing Kerotan on a bomb carrier's back). The model
// used is the game's C4 charge as a stand-in: the time bomb's own model has not been found.
function targetKind(item, mark) {
  if (R && R.meta && R.meta.mode === 6) return 'bomb';
  return (mark && mark.prop) || (item === 1 ? 'gako' : 'kerotan');
}
function applyTargetProps() {
  tgtProps.forEach(function (t) {
    if (setProp(t.mesh, t.kind, TGT_SCALE)) t.mesh.position.y = t.y;      // off its float, onto the ground
  });
}
var carryMarks = [];    // one rider per simultaneously-carried objective
function carryMarkAt(n) {
  while (carryMarks.length <= n) {
    var m = new THREE.Mesh(new THREE.OctahedronGeometry(5),
      new THREE.MeshBasicMaterial({ color: 0xE8C547, transparent: true,
                                    opacity: 0.95, depthTest: false }));
    m.renderOrder = 6; m.visible = false;
    scene.add(m); carryMarks.push(m);
  }
  return carryMarks[n];
}
var carryWarned = false;
// A real MGO2 soldier mesh, decimated from the figure export (376,626 -> 3,716
// triangles, 76 KB) and shared by every player. It replaces the box figure but
// does NOT replace the pose rig: the boxes stay in the scene graph, hidden, so
// applyPose() keeps working untouched and the figure just inherits `dir`'s
// rotation. If the fetch fails the boxes simply stay visible.
var FIG_GEO = {};       // pose key -> BufferGeometry
// Which pose to draw is decided by the item actually in hand (frame cell 12).
// The three meshes depict different things and the mapping follows that:
//   rifle_carry  a rifle held at the side      -> a primary weapon is out
//   rifle_back   rifle slung, hands free       -> sidearm / knife / thrown
//   unarmed      no weapon on the model at all -> nothing equipped
// Primaries measured from what players actually hold across the corpus.
var FIG_PRIMARY = { 20: 1, 23: 1, 24: 1, 25: 1, 43: 1, 26: 1, 27: 1, 28: 1,
                    29: 1, 30: 1, 31: 1, 32: 1, 33: 1, 21: 1, 22: 1 };
function figKeyFor(p) {
  if (!p || p.item == null) return 'rifle_carry';
  if (p.item === 0) return 'unarmed';
  return FIG_PRIMARY[p.item] ? 'rifle_carry' : 'rifle_back';
}
var objMarks = [];      // {o, group, col} per drawn objective
var carrySpans = null;  // pickup -> drop, each bound to the target it came from
var mapCeil = null, showRoofs = false;
var marks = [], trailLines = [], trailBuf = [], rings = [];
var tracers = [];   // shot lines: red = lethal, blue = non-lethal
var TRAIL_N = 90;
var RING_SEG = 48;
// The nametags float over the game's own geometry, so they get the game's own
// typeface. The K/D chip under them stays monospaced: MGO2 UI has no tabular
// figures, so digits change width as they tick and the chip would jitter.
var LBL_FONT = '500 13px "MGO2 UI","IBM Plex Mono",monospace';
// Canvas text silently falls back to the fallback stack if the webfont has not
// finished loading -- unlike CSS text, it never re-lays-out once it arrives.
// So ask for it explicitly and redraw when it lands.
if (document.fonts && document.fonts.load) {
  document.fonts.load('500 13px "MGO2 UI"').catch(function () {});
}
var labelPos = {};   // playerIndex -> smoothed label position
var orbit = { az: 0.9, el: 0.62, dist: 420, tx: 0, ty: 0, tz: 0 };
// Free camera (key V): a fly-through eye with no target. Position is in scene
// units (1 unit = 100 mm), yaw 0 looks down +Z. `keys` holds the movement keys
// that are down right now, by KeyboardEvent.code.
var free = { x: 0, y: 60, z: 0, yaw: 0, pitch: -0.3, keys: {} };
window.__mgo2Free = free;   // read-out for testing
// Fly speed scales with height above the floor (crossing a whole map from high
// up, creeping at head height), in units/s; Shift is 4x.
function freeSpeed() {
  var fy = (M && M.floorY != null ? M.floorY : 0) * S;
  return clamp(Math.abs(free.y - fy) * 1.2, 80, 2500);
}
var FREE_KEYS = { KeyW: 1, KeyA: 1, KeyS: 1, KeyD: 1, KeyQ: 1, KeyE: 1 };
var bounds = { min: [-40000, -7000, -40000], max: [40000, 20000, 40000] };
var ctr = [0, 0, 0], span = 800;

function initGL() {
  var c = $('gl');
  renderer = new THREE.WebGLRenderer({ canvas: c, antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2));
  renderer.setClearColor(0x0b0d0c, 1);
  scene = new THREE.Scene();
  scene.fog = new THREE.Fog(0x0b0d0c, 400, 2600);
  cam = new THREE.PerspectiveCamera(52, 1, 0.3, 12000);
  // With surface colours on the scene is lit like an overcast day: neutral sky,
  // enough fill that a black uniform still reads. The original green cast and
  // low levels belong to the height-ramp "scan" look and stay with ?surf=0.
  hemiLight = SURF_ON ? new THREE.HemisphereLight(0xdfe6ee, 0x4a4d45, 2.3)
                      : new THREE.HemisphereLight(0x9ec4ac, 0x101810, 1.15);
  scene.add(hemiLight);
  var d = SURF_ON ? new THREE.DirectionalLight(0xfff1dc, 2.2)
                  : new THREE.DirectionalLight(0xffe2c4, 1.25);
  d.position.set(0.42, 0.9, 0.25);
  scene.add(d);
  resize();
  addEventListener('resize', resize);
}

function resize() {
  var w = innerWidth, h = innerHeight;
  // a phone held sideways is wide but short: it is still a phone
  var short_ = h < 520;
  mobile = w < 760 || short_;
  document.body.dataset.short = short_ ? '1' : '0';
  document.body.dataset.mobile = mobile ? '1' : '0';
  renderer.setSize(w, h, false);
  cam.aspect = w / h; cam.updateProjectionMatrix();
  var hud = $('hud'), dpr = Math.min(devicePixelRatio || 1, 2);
  hud.width = w * dpr; hud.height = h * dpr;
  hud.style.width = w + 'px'; hud.style.height = h + 'px';
  hud.getContext('2d').setTransform(dpr, 0, 0, dpr, 0, 0);
  sizeTimeline();
  if (M && camMode !== 'follow') frameCameraKeepAngle();
}

// ── HD scenery ───────────────────────────────────────────────────────────
// The default map is the COLLISION world: simple boxes, which is why it reads
// as low-poly. Where maps_hd/<stage>.json exists (replay/stage_pack.py), HD
// draws the stage's real render models instead, in the same coordinates, lit
// by the vertex colours the game baked into them. On by default on a desktop,
// off on a phone (the file is ~16 MB); ?hd=1 / ?hd=0 force it, key H toggles.
// The roof / level slicers work on the collision mesh only, so they do nothing
// while HD is showing.
var HD = { on: (function () {
  var m = /[?&]hd=([01])/.exec(location.search);
  if (m) return m[1] === '1';
  // Phones used to get the bare collision view (flat colour blotches, invisible walls and all), which is
  // what people arriving from the mgo2pc "View in browser" button saw first (2026-10-06). HD is now on
  // for phones too unless the phone is short on memory or asked to save data; H / the HD button still
  // switches it off.
  var phone = ('ontouchstart' in window) && Math.min(innerWidth, innerHeight) < 820;
  if (!phone) return true;
  var conn = navigator.connection || {};
  if (conn.saveData) return false;
  return !(navigator.deviceMemory && navigator.deviceMemory < 4);
})(), mesh: null, want: null,
  // ALTERNATE LAYOUT. Some stages ship a second set of scenery that shrinks the
  // map: GG's shut red gates, and on Tomb of Tubes a whole set of fences
  // (n008a_stop_wall.mdn) closing a lane and four other spots -- a layout the
  // players who found it here on 2026-10-04 had never seen in the game. The
  // pack flags those groups alt; they are drawn only with the Alt map switch on
  // (?alt=1). The button appears only on maps that have such a set.
  alt: /[?&]alt=1/.test(location.search) };
var HD_GAIN = 0.9;       // 128 (neutral baked light) -> a mid grey under the viewer's lights
// A map's HD scenery is a 10-30 MB download; on a slow line nothing visibly
// happens for a while, so say so until it is on screen.
function hdNote(msg) {
  var el = document.getElementById('hdnote');
  if (!el) {
    if (!msg) return;
    el = document.createElement('div');
    el.id = 'hdnote';
    el.style.cssText = 'position:fixed;left:50%;top:44px;transform:translateX(-50%);z-index:60;' +
      'background:rgba(0,0,0,.7);color:#e8e2d0;font:12px/1.4 ui-monospace,Menlo,Consolas,monospace;' +
      'padding:6px 12px;border-radius:5px;pointer-events:none';
    document.body.appendChild(el);
  }
  el.textContent = msg || '';
  el.style.display = msg ? '' : 'none';
}
function disposeHD() {
  if (HD.mesh) { scene.remove(HD.mesh); HD.mesh.geometry.dispose(); HD.mesh = null; }
}
function applyHD() {
  var show = !!(HD.on && HD.mesh);
  if (HD.mesh || !HD.on) hdNote('');
  if (HD.mesh) HD.mesh.visible = show;
  if (mapMesh) mapMesh.visible = !show;
  if (mapEdges) mapEdges.visible = !show && showRoofs && !levelMode;
  var b = document.getElementById('b-hd');
  if (b) { b.dataset.on = HD.on ? '1' : '0'; b.style.opacity = HD.on && !HD.mesh ? 0.5 : 1; }
  applyXray();        // a pack that finishes loading must pick up the current X-ray state
}
function loadHD(stage) {
  disposeHD();
  var ba0 = document.getElementById('b-alt');
  if (ba0) ba0.style.display = 'none';
  HD.want = stage;
  applyHD();
  hdNote('');
  if (!HD.on || !stage) return;
  hdNote('Loading HD scenery\u2026 (H switches HD off)');
  fetch(avHD('maps_hd/' + stage + '.json')).then(function (r) { return r.ok ? r.json() : null; })
    .then(function (h) {
      if (HD.want === stage && (!h || !HD.on)) hdNote('');
      if (!h || HD.want !== stage || !HD.on || HD.mesh) return;
      var n = h.positions.length / 3, v = new Float32Array(n * 3), c = new Float32Array(n * 3);
      for (var i = 0; i < n; i++) {
        v[i * 3] = (h.positions[i * 3] - ctr[0]) * S;
        v[i * 3 + 1] = h.positions[i * 3 + 1] * S;
        v[i * 3 + 2] = (h.positions[i * 3 + 2] - ctr[2]) * S;
        c[i * 3] = Math.min(1, h.colors[i * 3] / 255 * HD_GAIN);
        c[i * 3 + 1] = Math.min(1, h.colors[i * 3 + 1] / 255 * HD_GAIN);
        c[i * 3 + 2] = Math.min(1, h.colors[i * 3 + 2] / 255 * HD_GAIN);
      }
      var g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(v, 3));
      g.setAttribute('color', new THREE.BufferAttribute(c, 3));
      g.setIndex(new THREE.Uint32BufferAttribute(h.indices, 1));
      g.computeVertexNormals();
      if (HD_TEX && h.uv && h.groups) { hdTextured(stage, h, g, n); return; }
      // The vertex colours are the game's baked light in the "128 = neutral"
      // convention (they multiply a texture at 2x), so on their own nearly the
      // whole playable area is plain mid-level: gain 2 unlit drew a white map.
      // Until the textures are in, they are kept at half strength and the
      // viewer's daylight supplies the shape.
      HD.mesh = new THREE.Mesh(g, new THREE.MeshLambertMaterial({
        vertexColors: true, side: THREE.DoubleSide }));
      scene.add(HD.mesh);
      applyHD();
    }).catch(function () { hdNote(''); });
}
// TEXTURED HD. The pack's indices are grouped by albedo texture, so each group
// becomes one draw with its own material. The game shades scenery as
// texture x vertex colour x 2 (128 = neutral baked light), which is reproduced
// by an UNLIT material whose vertex colours are scaled by 2 / 255. Groups whose
// texture is not shipped fall back to the plain shaded grey. Needs
// maps_hd/<stage>.tex.json + maps_hd/tex/<stage>/<hash>.webp
// (replay/build_stage_tex.py). ?hdtex=0 shows the untextured grey version.
var HD_TEX = !/[?&]hdtex=0/.test(location.search);
function hdTextured(stage, h, g, n) {
  fetch(avHD('maps_hd/' + stage + '.tex.json')).then(function (r) { return r.ok ? r.json() : null; })
    .catch(function () { return null; })
    .then(function (ix) {
      if (HD.want !== stage || !HD.on || HD.mesh) return;
      // Surfaces whose texture is not shipped (0.5% of a map; on Blood Bath they
      // are the mud pools, whose texture is a reflection the game renders live)
      // get a dark neutral tone, not the near-white the bare vertex light gives.
      var plain = new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide,
                                                  color: ix ? 0x4a453d : 0xffffff });
      if (!ix) { HD.mesh = new THREE.Mesh(g, plain); scene.add(HD.mesh); applyHD(); return; }
      var uv = new Float32Array(n * 2), sc = h.uvScale || 4096;
      for (var i = 0; i < n * 2; i++) uv[i] = h.uv[i] / sc;
      g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
      // a second colour set at full strength for the textured (unlit) groups
      var lit = new Float32Array(n * 3);
      for (var k = 0; k < n * 3; k++) lit[k] = h.colors[k] / 127.5;
      var loader = new THREE.TextureLoader(), mats = [plain], plainUsed = false;
      // textured groups read `color` at full strength; the grey fallback wants
      // the dim copy, so it gets its own geometry sharing everything else
      var gT = g.clone();
      gT.setAttribute('color', new THREE.BufferAttribute(lit, 3));
      var matsT = [];
      var gP = g;
      var idxP = [];
      var hasAlt = h.groups.some(function (gr) { return gr.alt; });
      var ba = document.getElementById('b-alt');
      if (ba) { ba.style.display = hasAlt ? '' : 'none'; ba.dataset.on = HD.alt ? '1' : '0'; }
      h.groups.forEach(function (gr) {
        if (gr.alt && !HD.alt) return;
        // Konami's own leftover placeholder (red text: "this texture is scheduled for
        // deletion"), on one flat 223-triangle sheet in Icebound Inferno (n018a). Not drawn.
        if (gr.tex === '64672e') return;
        // Eastern Europe (n020a): 540 small crossed panels sharing one texture, a brown
        // square in a white frame. The pack lists glight / windowlight meshes, so these are
        // taken to be light-glow sprites the game blends; drawn solid they are floating cards.
        if (stage === 'n020a' && gr.tex === 'c2e46d') return;
        var fl = gr.tex ? ix[gr.tex] : 0;
        if (!fl) {
          for (var q = gr.start; q < gr.start + gr.count; q++) idxP.push(h.indices[q]);
          plainUsed = true;
          return;
        }
        var t = loader.load(avHD('maps_hd/tex/' + stage + '/' + gr.tex + '.webp'));
        t.wrapS = t.wrapT = THREE.RepeatWrapping;
        t.flipY = false;
        t.colorSpace = THREE.SRGBColorSpace;
        var mt = new THREE.MeshBasicMaterial({ map: t, vertexColors: true, side: THREE.DoubleSide });
        if (fl & 2) { mt.alphaTest = 0.5; }
        gT.addGroup(gr.start, gr.count, matsT.length);
        matsT.push(mt);
      });
      var root = new THREE.Group();
      root.add(new THREE.Mesh(gT, matsT));
      if (plainUsed) {
        gP.setIndex(new THREE.Uint32BufferAttribute(idxP, 1));
        root.add(new THREE.Mesh(gP, plain));
      }
      // disposeHD() expects .geometry on HD.mesh
      root.geometry = { dispose: function () { gT.dispose(); gP.dispose(); matsT.forEach(function (m) { if (m.map) m.map.dispose(); m.dispose(); }); } };
      HD.mesh = root;
      scene.add(root);
      applyHD();
    });
}
function setHD(on) {
  HD.on = on;
  if (on && !HD.mesh && M) loadHD(M.stage); else applyHD();
}

// ── map ──────────────────────────────────────────────────────────────────
function buildMap(m) {
  M = m;
  disposeHD();
  [mapMesh, mapEdges, groundGrid, clipMesh, objGroup].forEach(function (o) {
    if (o) { scene.remove(o); if (o.geometry) o.geometry.dispose(); }
  });
  mapMesh = mapEdges = groundGrid = clipMesh = objGroup = null;
  objMarks = []; carrySpans = null;
  if (spawnGroup) { scene.remove(spawnGroup); spawnGroup = null; }
  // the riders are scene children too -- leaving them attached across a reload
  // orphans one marker per replay opened
  carryMarks.forEach(function (m) { if (m.parent) m.parent.remove(m); });
  carryMarks = [];
  if (!m) {
    // no stage: centre and size the view on the tracks themselves
    var fr0 = (R && R.frames) || [], x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
    for (var fi = 0; fi < fr0.length; fi += Math.max(1, Math.floor(fr0.length / 400))) {
      var row0 = fr0[fi].p || [];
      for (var ri = 0; ri < row0.length; ri++) {
        var c0 = row0[ri];
        if (!c0) continue;
        if (c0[0] < x0) x0 = c0[0];
        if (c0[0] > x1) x1 = c0[0];
        if (c0[2] < z0) z0 = c0[2];
        if (c0[2] > z1) z1 = c0[2];
      }
    }
    if (isFinite(x0)) {
      ctr = [(x0 + x1) / 2, 0, (z0 + z1) / 2];
      span = Math.max(x1 - x0, z1 - z0, 40000) * S;
    }
    frameCamera();
    return;
  }

  bounds = m.bounds || bounds;
  // Centre on the ARENA, not the bounding box: replay coordinates are
  // arena-local, so this puts map and players in the same frame without a
  // per-map origin constant.
  ctr = m.arenaCenter
      ? [m.arenaCenter[0], 0, m.arenaCenter[1]]
      : [(bounds.min[0] + bounds.max[0]) / 2, 0, (bounds.min[2] + bounds.max[2]) / 2];
  // Frame on the MESH, not the declared bounds: several stages carry a world
  // box far larger than the arena (Ravaged Riverfront's spans 235k units),
  // which parked the camera so far out the map was a speck.
  var mnx = Infinity, mxx = -Infinity, mnz = Infinity, mxz = -Infinity;
  for (var q = 0; q < m.positions.length; q += 3) {
    var px = m.positions[q], pz = m.positions[q + 2];
    if (px < mnx) mnx = px;
    if (px > mxx) mxx = px;
    if (pz < mnz) mnz = pz;
    if (pz > mxz) mxz = pz;
  }
  span = (isFinite(mnx)
    ? Math.max(mxx - mnx, mxz - mnz)
    : Math.max(bounds.max[0] - bounds.min[0], bounds.max[2] - bounds.min[2])) * S;

  var pos = m.positions, idx = m.indices, n = pos.length / 3;
  var v = new Float32Array(pos.length), col = new Float32Array(pos.length);
  // Colour relative to the arena FLOOR over a +/-1 storey-ish window. The
  // world box is far taller than the playable slice, so a ramp over
  // min..max collapses the whole map to one tone.
  var fY = (m.floorY != null ? m.floorY : 0);
  var lo = fY - 7000, hi = fY + 9000;
  for (var i = 0; i < n; i++) {
    var x = pos[i * 3] - ctr[0], y = pos[i * 3 + 1], z = pos[i * 3 + 2] - ctr[2];
    v[i * 3] = x * S; v[i * 3 + 1] = y * S; v[i * 3 + 2] = z * S;
    var u = clamp((y - lo) / (hi - lo), 0, 1);
    // deep teal below grade -> olive at grade -> warm bone up high
    var r, g2, b;
    if (u < 0.44) { var k = u / 0.44;
      r = lerp(0.030, 0.115, k); g2 = lerp(0.085, 0.150, k); b = lerp(0.105, 0.120, k); }
    else { var k2 = (u - 0.44) / 0.56;
      r = lerp(0.115, 0.400, k2); g2 = lerp(0.150, 0.395, k2); b = lerp(0.120, 0.300, k2); }
    // contour stripe every 500 units so slabs and stairs read as steps
    var stripe = (Math.abs(((y - fY) / 500) % 1) < 0.09) ? 0.055 : 0;
    col[i * 3]     = srgb(r + stripe);
    col[i * 3 + 1] = srgb(g2 + stripe * 1.3);
    col[i * 3 + 2] = srgb(b + stripe);
  }
  // SURFACE COLOURS. Each triangle carries what it is made of (add_mat.py:
  // the collision face's own material + colour tag). Vertices are shared, so a
  // vertex takes the average of its faces -- surfaces blend softly at their
  // borders instead of needing unshared vertices. Height still shades it a
  // little so stacked floors separate, and the contour stripe is kept.
  if (SURF_ON && m.surf && m.surfNames) {
    var rgbOf = m.surfNames.map(surfaceRgb);
    var acc = new Float32Array(pos.length), cnt = new Uint16Array(n);
    for (var ft = 0; ft < m.surf.length; ft++) {
      var c3 = rgbOf[m.surf[ft]];
      if (!c3) continue;
      for (var fk = 0; fk < 3; fk++) {
        var vi = idx[ft * 3 + fk];
        acc[vi * 3] += c3[0]; acc[vi * 3 + 1] += c3[1]; acc[vi * 3 + 2] += c3[2];
        cnt[vi]++;
      }
    }
    for (i = 0; i < n; i++) {
      if (!cnt[i]) continue;                // untagged: keep the ramp colour
      var yv = pos[i * 3 + 1];
      var sh = lerp(0.72, 1.05, clamp((yv - lo) / (hi - lo), 0, 1));
      var st2 = (Math.abs(((yv - fY) / 500) % 1) < 0.09) ? 0.03 : 0;
      col[i * 3]     = srgb(acc[i * 3] / cnt[i] * sh + st2);
      col[i * 3 + 1] = srgb(acc[i * 3 + 1] / cnt[i] * sh + st2);
      col[i * 3 + 2] = srgb(acc[i * 3 + 2] / cnt[i] * sh + st2);
    }
  }
  // Sort every face by height. With the index buffer ordered by faceY, any
  // height band is a CONTIGUOUS draw range — which makes the roof cull and the
  // level slicer the same mechanism instead of two.
  var order = idx, ceilCount = 0, sortedY = null;
  if (idx.length) {
    var nt = idx.length / 3, fY = m.faceY;
    var ord = new Array(nt);
    for (var t = 0; t < nt; t++) {
      var h = fY ? fY[t] : Math.max(pos[idx[t*3]*3+1], pos[idx[t*3+1]*3+1], pos[idx[t*3+2]*3+1]);
      ord[t] = [h, t];
    }
    ord.sort(function (a, b) { return a[0] - b[0]; });
    // Collision-only clip brushes go into their own mesh so they can be hidden
    // without disturbing the height ordering the level slicer depends on.
    var cl = m.clip;
    // Faces whose material is AIR_* are invisible barriers in the game (1.5-4%
    // of every map, mostly missed by the geometric clip test), so they go with
    // the clip brushes. ?air=1 draws them again for comparison.
    var airIdx = {}, sf = m.surf;
    if (sf && m.surfNames && !/[?&]air=1/.test(location.search)) {
      m.surfNames.forEach(function (nm, i) { if (/^AIR/.test(nm[0])) airIdx[i] = 1; });
    }
    // BOUNDARY CURTAINS (Forest Firefight and the other outdoor maps): rock
    // faces that carry a colour tag and stand EXACTLY vertical. Natural rock
    // uses the default tag and is never plumb; these are extruded walls the
    // game uses to fence off cliffs and tree lines, drawn here as tall rainbow
    // sheets. MEASURED: of the tagged rock faces that are near-vertical,
    // 7264 of 7267 on Forest Firefight are plumb to within 1.7 deg (3699 of
    // 3699 on Virtuous Vista, 1480 of 1480 on Outer Outlet); maps with no rock
    // have none, so buildings are untouched. ?curtain=1 draws them again.
    var curtIdx = {};
    if (sf && m.surfNames && !/[?&]curtain=1/.test(location.search)) {
      m.surfNames.forEach(function (nm, i) {
        if (/^ROCK/.test(nm[0]) && nm[1] !== '61c291') curtIdx[i] = 1;
      });
    }
    // The same fences in OTHER materials. Loose ground cannot stand as a plumb
    // wall, so a tagged, plumb gravel / soil / turf / sand / snow face taller
    // than 1.5 m is a fence on any map (Forest Firefight: 88 gravel faces,
    // 7-8 m tall, 446 m2). Forest Firefight also fences with "concrete":
    // 120 tagged plumb faces, 809 m2, and NOT ONE has a concrete top anywhere
    // near it, while its real hut walls (WOOD_D) are capped on 490 of 492.
    // That no-top test does not carry to building maps (tiled walls there are
    // bare too), so the concrete rule is this one stage only. The 8 m tagged
    // WOOD_A / WOOD_B poles (0.2-0.7 m wide) are tree trunks and stay.
    var looseIdx = {};
    if (sf && m.surfNames && !/[?&]curtain=1/.test(location.search)) {
      var looseRe = m.stage === 'n001a' ? /^(GRAV|SOIL|TURF|GRAS|SAND|SNOW|CONC)/
                                         : /^(GRAV|SOIL|TURF|GRAS|SAND|SNOW)/;
      m.surfNames.forEach(function (nm, i) {
        if (looseRe.test(nm[0]) && nm[1] !== '61c291') looseIdx[i] = 1;
      });
    }
    // y-span of a face if it stands plumb (normal within 1.7 deg of level), else -1
    function plumbSpan(tri) {
      var a = idx[tri * 3] * 3, b = idx[tri * 3 + 1] * 3, c = idx[tri * 3 + 2] * 3;
      var ux = pos[b] - pos[a], uy = pos[b + 1] - pos[a + 1], uz = pos[b + 2] - pos[a + 2];
      var vx = pos[c] - pos[a], vy = pos[c + 1] - pos[a + 1], vz = pos[c + 2] - pos[a + 2];
      var nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
      var L = Math.sqrt(nx * nx + ny * ny + nz * nz) || 1;
      if (Math.abs(ny / L) >= 0.03) return -1;
      return Math.max(pos[a + 1], pos[b + 1], pos[c + 1]) - Math.min(pos[a + 1], pos[b + 1], pos[c + 1]);
    }
    function plumb(tri) { return plumbSpan(tri) >= 0; }
    order = [];
    var clipIdx = [];
    var yList = [];
    for (var q = 0; q < nt; q++) {
      var src = ord[q][1];
      var dst = ((cl && cl[src]) || (sf && airIdx[sf[src]]) ||
                 (sf && curtIdx[sf[src]] && plumb(src)) ||
                 (sf && looseIdx[sf[src]] && plumbSpan(src) > 1500)) ? clipIdx : order;
      if (dst === order) yList.push(ord[q][0]);
      dst.push(idx[src*3], idx[src*3+1], idx[src*3+2]);
    }
    sortedY = new Float64Array(yList);
    nt = order.length / 3;
    m.__clipIdx = clipIdx;
    var cY = m.ceilingY;
    if (cY != null) {
      for (var q2 = nt - 1; q2 >= 0 && sortedY[q2] >= cY - 1; q2--) ceilCount++;
    }
  }
  var g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(v, 3));
  g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  g.setIndex(order.length ? (n > 65535 ? new THREE.Uint32BufferAttribute(order, 1)
                                       : new THREE.Uint16BufferAttribute(order, 1)) : null);
  mapCeil = { total: order.length, ceil: ceilCount, y: sortedY };
  g.computeVertexNormals();
  mapMesh = new THREE.Mesh(g, new THREE.MeshLambertMaterial({
    vertexColors: true, side: THREE.DoubleSide, flatShading: true
  }));
  scene.add(mapMesh);

  // Emissive edge pass: reads as a scan overlay, and it is what makes
  // interior structure legible through the dark faces.
  if (idx.length / 3 < 140000) {
    try {
      mapEdges = new THREE.LineSegments(
        new THREE.EdgesGeometry(g, 24),
        new THREE.LineBasicMaterial({ color: 0x76a98d, transparent: true, opacity: 0.22 }));
      scene.add(mapEdges);
    } catch (e) { mapEdges = null; }
  }

  var ci = m.__clipIdx;
  if (ci && ci.length) {
    var cg = new THREE.BufferGeometry();
    cg.setAttribute('position', new THREE.BufferAttribute(v, 3));
    cg.setIndex(n > 65535 ? new THREE.Uint32BufferAttribute(ci, 1)
                          : new THREE.Uint16BufferAttribute(ci, 1));
    cg.computeVertexNormals();
    clipMesh = new THREE.Mesh(cg, new THREE.MeshBasicMaterial({
      color: 0xE2574C, transparent: true, opacity: 0.28,
      side: THREE.DoubleSide, depthWrite: false }));
    clipMesh.visible = showClip;
    scene.add(clipMesh);
  }
  buildObjectives(m);
  applyRoofs();
  applyXray();
  loadHD(m.stage);

  var gr = new THREE.GridHelper(Math.ceil(span * 1.1), 26, 0x2a3329, 0x1c231b);
  gr.position.y = (bounds.min[1] - 40) * S;
  gr.material.transparent = true; gr.material.opacity = 0.5;
  groundGrid = gr; scene.add(gr);
  frameCamera();
}

// LEVELS: 0 = all, 1 = basement, 2 = ground, 3 = upper. Bands are cut relative
// to the stage's own floor plane, so they mean the same thing on every map.
var levelMode = 0;
var LEVEL_NAMES = ['All', 'Base', 'Ground', 'Upper'];

function levelBand() {
  var yArr = mapCeil && mapCeil.y;
  var top = mapCeil ? (showRoofs ? mapCeil.total : mapCeil.total - mapCeil.ceil) : 0;
  if (!yArr || !levelMode) return [0, top];
  var fy = (M && M.floorY != null ? M.floorY : 0);
  var lo = -Infinity, hi = Infinity;
  if (levelMode === 1) { hi = fy - 600; }                    // below the floor
  else if (levelMode === 2) { lo = fy - 600; hi = fy + 2600; }  // the arena deck
  else { lo = fy + 2600; }                                   // upper storeys
  var n = top, a = 0, b = n;
  while (a < n && yArr[a] < lo) a++;
  while (b > a && yArr[b - 1] >= hi) b--;
  return [a, Math.max(0, b - a)];
}

// ---- CUTAWAY -------------------------------------------------------------
// TriggerHappy, 2026-10-09: "a way to occlude obstructions. often times walls and
// such get in the way of me trying to view a player". X-Ray ghosts the whole map;
// this opens a round hole only in what stands BETWEEN the camera and the selected
// player: any scenery pixel inside a circle around the player on screen, and
// nearer to the camera than the player, is not drawn (dithered at the rim so the
// edge is soft without sorting transparent walls). The floor under the player and
// the walls behind stay, because they are not nearer than the player.
var CUT = { on: true, u: { uCut: { value: new THREE.Vector4(0, 0, 0, 0) }, uCutScr: { value: new THREE.Vector2() }, uCutR: { value: 160 } },
            v: new THREE.Vector3(), w: new THREE.Vector3(), sz: new THREE.Vector2() };
try { CUT.on = localStorage.getItem('mgo2rv.cut') !== '0'; } catch (e) {}
function addCutaway(mat) {
  if (!mat || mat.__cut) return;
  mat.__cut = true;
  var prev = mat.onBeforeCompile, pk = mat.customProgramCacheKey;
  mat.onBeforeCompile = function (sh, r) {
    if (prev) prev.call(this, sh, r);
    sh.uniforms.uCut = CUT.u.uCut; sh.uniforms.uCutScr = CUT.u.uCutScr; sh.uniforms.uCutR = CUT.u.uCutR;
    sh.vertexShader = 'varying vec3 vCutView;\n' +
      sh.vertexShader.replace('#include <project_vertex>', '#include <project_vertex>\n\tvCutView = mvPosition.xyz;');
    sh.fragmentShader = 'uniform vec4 uCut;\nuniform vec2 uCutScr;\nuniform float uCutR;\nvarying vec3 vCutView;\n' +
      sh.fragmentShader.replace('#include <clipping_planes_fragment>',
        '#include <clipping_planes_fragment>\n\tif ( uCut.w > 0.5 && -vCutView.z < -uCut.z - 9.0 ) {' +
        ' float cd = distance( gl_FragCoord.xy, uCutScr ) / uCutR;' +
        ' if ( cd < 1.0 && ( cd < 0.93 || fract( sin( dot( floor( gl_FragCoord.xy * 0.5 ), vec2( 12.9898, 78.233 ) ) ) * 43758.5453 ) > ( cd - 0.93 ) / 0.07 ) ) discard; }');
  };
  mat.customProgramCacheKey = function () { return (pk ? pk.call(this) : '') + '+cut'; };
  mat.needsUpdate = true;
}
function applyCutaway() {
  if (mapMesh) addCutaway(mapMesh.material);
  if (HD.mesh) HD.mesh.traverse(function (o) {
    if (o.isMesh) (Array.isArray(o.material) ? o.material : [o.material]).forEach(addCutaway);
  });
}
// once a frame, just before drawing: where the selected player is on screen and how far away
function updateCutaway() {
  var u = CUT.u.uCut.value, on = CUT.on && follow >= 0 && live[follow] && (live[follow].x || live[follow].z) && cam && !camEyes();
  if (on) {
    var p = live[follow];
    CUT.v.set((p.x - ctr[0]) * S, p.y * S + 10, (p.z - ctr[2]) * S);
    CUT.w.copy(CUT.v).applyMatrix4(cam.matrixWorldInverse);
    if (CUT.w.z < -1) {
      renderer.getDrawingBufferSize(CUT.sz);
      CUT.v.project(cam);
      CUT.u.uCutScr.value.set((CUT.v.x * 0.5 + 0.5) * CUT.sz.x, (CUT.v.y * 0.5 + 0.5) * CUT.sz.y);
      // about 3 m around the player, whatever the zoom; never tiny, never most of the screen
      var px = 30 * (CUT.sz.y / (2 * Math.tan((cam.fov || 50) * Math.PI / 360))) / -CUT.w.z;
      CUT.u.uCutR.value = Math.max(CUT.sz.y * 0.07, Math.min(CUT.sz.y * 0.34, px));
      // BEHIND-THE-PLAYER VIEWS (Shoulder, Chase): the camera rides a fixed 2-6 m behind and is often INSIDE a wall or under
      // a roof, which then fills the whole screen - a 3 m hole round the player showed a patch of him and nothing of what he
      // is looking at (user's phone screenshot, 2026-10-09, Chase on Groznyj Grad). There the hole is most of the picture:
      // whatever is nearer than the player goes, except at the far edges of a wide screen.
      if (camMode === 'follow' && followStyle && !camSubj) CUT.u.uCutR.value = Math.max(CUT.sz.y * 0.62, CUT.sz.x * 0.45);
      u.set(CUT.w.x, CUT.w.y, CUT.w.z, 1);
      return;
    }
  }
  u.w = 0;
}

var xray = false;
// Translucent stage so a followed player stays visible through walls.
function applyXray() {
  applyCutaway();
  var on = xray || (camMode === 'follow' && xrayAuto);
  if (mapMesh) {
    mapMesh.material.transparent = on;
    mapMesh.material.opacity = on ? 0.35 : 1.0;
    mapMesh.material.depthWrite = !on;
    mapMesh.material.needsUpdate = true;
  }
  // HD scenery: every textured group has its own material. Only the X-ray
  // BUTTON see-throughs it -- the automatic see-through that follow mode turns
  // on for the grey collision map would leave the textured world permanently
  // ghosted, which defeats the point of HD.
  if (HD.mesh) {
    var hx = xray;
    HD.mesh.traverse(function (o) {
      if (!o.isMesh) return;
      (Array.isArray(o.material) ? o.material : [o.material]).forEach(function (mt) {
        mt.transparent = hx;
        mt.opacity = hx ? 0.35 : 1.0;
        mt.depthWrite = !hx;
        mt.needsUpdate = true;
      });
    });
  }
}
var xrayAuto = false;   // was true: the cutaway (above) now clears the view to a followed player instead


// ---------------------------------------------------------------------------
// Objective markers: TGT / GOAL entities read out of the stage .geom entity
// table (section 0x060000) by extract_objectives.py.
//
// These are PLACEMENTS, not replay state -- the replay carries only who is
// holding an item, never where it started. The two are cross-checked though:
// on Urban Ultimatum PRP_TEAM_SNEAKING_TGT_01 sits 0.72 m in XZ and 0.09 m in
// Y from where a carrier actually picked the item up in replay_351754_9.dat.
//
// Colours follow the player's own reading of the map: green kerotan, yellow
// gako. Which of TGT_01 / TGT_02 is which frog is NOT confirmed -- the names
// are positional, so the pairing may be the other way round.
// Targets are the two frogs -- kerotan green, gako yellow -- so colour them by
// their ordinal within the mode's target list rather than naming every family.
// WHICH frog is which is NOT established: the entity names are positional
// (TGT_01/_02, TGT_00..02) and nothing decoded so far binds a model to a slot.
// BASE capture. `meta.baseGauge` is keyed 0x267..0x26B and its value encodes
// OWNERSHIP, not progress: 1 = fully the team whose raw byte is 0, 255 = fully
// the team whose raw byte is 2, so 128 is neutral. Confirmed in the parser
// against which team held the majority near the point, 95.06% of 3601 changes.
//
// The five points are PRP_BASE01..05 in the stage .geom. That name has no
// underscore before the number, which is why every earlier search missed them.
var BASE_VIDS = ['0x267', '0x268', '0x269', '0x26A', '0x26B'];
function baseSeries(i) {
  var g = R && R.meta && R.meta.baseGauge;
  return (g && g[BASE_VIDS[i]]) || null;
}
// |progress| at which a capture completes. Run peaks cluster at 38-45 in both
// test matches; the spread is the last tick overshooting when two players are
// on the point and it steps by 2.
var BASE_CAP = 37;

function baseSigned(v) { return v > 127 ? v - 256 : v; }

// Every capture the file records, as {t, team, base}. Built once per replay.
// A run is a stretch of progress by one side; it becomes a CAPTURE the moment
// it first reaches the threshold. Progress resetting to 1 does not lose the
// base -- ownership only moves on the other side completing its own run.
function buildBaseCaptures() {
  if (!R || R.baseCaptures) return;
  var caps = [];
  for (var i = 0; i < BASE_VIDS.length; i++) {
    var ser = baseSeries(i);
    if (!ser) continue;
    var armed = true, lastSign = 0, owner = 0;
    for (var k = 0; k < ser.length; k++) {
      var v = baseSigned(ser[k][1]), sg = v < 0 ? -1 : 1;
      if (sg !== lastSign) { armed = true; lastSign = sg; }
      else if (Math.abs(v) <= 2) { armed = true; }   // the meter was reset
      if (armed && Math.abs(v) >= BASE_CAP) {
        // A capture is the base CHANGING HANDS. A side that already holds it
        // filling the meter again (it flickers around the threshold while the
        // point is contested) takes nothing: counted that way, one match read
        // 36 "captures" for 20 real ownership changes.
        if (owner !== sg) caps.push({ t: ser[k][0], base: i, sign: sg });
        owner = sg;
        armed = false;                                // one capture per run
      }
    }
  }
  caps.sort(function (a, b) { return a.t - b.t; });
  R.baseCaptures = caps;
}

// State of base `i` at time `t`.
//   owner   -1 / +1 / 0    which side has actually captured it (0 = nobody yet)
//   sign    -1 / +1        which side is currently making progress
//   prog    0..1           how far that progress has got toward a capture
function baseStateAt(i, t) {
  buildBaseCaptures();
  var ser = baseSeries(i);
  var out = { owner: 0, sign: 0, prog: 0, raw: 0 };
  if (!ser || !ser.length) return out;
  var caps = R.baseCaptures || [];
  for (var c = 0; c < caps.length; c++) {
    if (caps[c].t > t) break;
    if (caps[c].base === i) out.owner = caps[c].sign;
  }
  if (t < ser[0][0]) return out;
  var lo = 0, hi = ser.length - 1;
  while (lo < hi) {
    var mid = (lo + hi + 1) >> 1;
    if (ser[mid][0] <= t) lo = mid; else hi = mid - 1;
  }
  var v = baseSigned(ser[lo][1]);
  out.raw = v;
  out.sign = v < 0 ? -1 : 1;
  out.prog = clamp(Math.abs(v) / BASE_CAP, 0, 1);
  return out;
}
// Team raw 0 and raw 2 remap to team indices in loadReplay; recover the two
// colours by asking the roster rather than assuming 0 and 1 exist.
// Returned in SIGN order: [colour for sign +1, colour for sign -1].
// +1 is the team whose raw byte is 2 -- see the module note; measured against
// respawn positions, 62.9% vs 28.6% for the other way round.
function baseTeamColors() {
  var a = null, b = null;
  if (R) {
    for (var i = 0; i < R.players.length; i++) {
      var pl = R.players[i];
      if (pl.teamRaw === 2 && a == null) a = teamColor(pl.team);
      if (pl.teamRaw === 0 && b == null) b = teamColor(pl.team);
    }
  }
  return [a || TEAM[1], b || TEAM[0]];
}
// team index (as remapped in loadReplay) for a gauge sign, or null
function baseSignTeam(sign) {
  if (!R || !sign) return null;
  var want = sign > 0 ? 2 : 0;
  for (var i = 0; i < R.players.length; i++) {
    if (R.players[i].teamRaw === want) return R.players[i].team;
  }
  return null;
}

var OBJ_TGT_COLORS = [0x6BD16B, 0xE8C547, 0xD08A4E, 0xB07CC6];
var OBJ_GOAL_COLOR = 0x4EA8DE;
function objColor(o, ord) {
  if (o.kind === 'goal') return OBJ_GOAL_COLOR;
  return OBJ_TGT_COLORS[(ord || 0) % OBJ_TGT_COLORS.length];
}
// RACE and CAP share one objective set, tagged RUGBY by the extractor.
function modeMatches(objMode, fileMode) {
  if (!fileMode || objMode === 'ANY') return true;
  if (objMode === 'RUGBY') return fileMode === 'RACE' || fileMode === 'CAP';
  // the map data tags Rescue placements 'RESCUE'; the replay's mode name is 'RES'. Until
  // 2026-10-08 the two never matched and a Rescue match drew no target and no goal at all.
  if (objMode === 'RESCUE') return fileMode === 'RES' || fileMode === 'RESCUE';
  return objMode === fileMode;
}
// CAPTURE GOALS THE STAGE FILE DOES NOT NAME. Tomb of Tubes names one goal
// (PRP_RUGBY_GOAL_A0 and A1 sit on the same point) and nothing at all where the
// other team scores: in replay_284191_13 all 8 puts at the far end land within
// 1.3 m of (29.1, 2.0, -25.0) m and no entity of any name is there. Most stages
// only name an A pair, so the second goal is read from the match itself:
// targets set down (carrierFlag40) more than 7 m from every named goal are
// clustered (6 m) and a cluster with 2+ puts becomes a goal marked `derived`.
// Puts land 0-3 m from the goal point (13 of 14 measured), so that is its error.
function frameAtT(t) {                   // last frame with frames[i].t <= t
  var fr = R.frames, lo = 0, hi = fr.length - 1;
  if (t <= fr[0].t) return 0;
  if (t >= fr[hi].t) return hi;
  while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (fr[mid].t <= t) lo = mid; else hi = mid - 1; }
  return lo;
}
function posBefore(pi, t) {              // the player's last known cell at or before t
  for (var f = frameAtT(t); f >= 0 && R.frames[f].t > t - 3; f--) {
    var c = (R.frames[f].p || [])[pi];
    if (c && c[0] != null) return c;
  }
  return null;
}
function medianOf(a) { var s = a.slice().sort(function (p, q) { return p - q; }); return s[s.length >> 1]; }
// Team Sneaking has the same gap worse: only 11 PRP_TEAM_SNEAKING_GOAL entries across 20
// stages, while every pickup lands 0.6-0.9 m from its named TGT (obj_audit, 11 replays). A
// Team Sneaking put ENDS the round, so a single put is trusted there; Capture needs 2.
var DERIVE_GOALS = { CAP: { objMode: 'RUGBY', minPuts: 2 }, TSNE: { objMode: 'TSNE', minPuts: 1 } };
function withDerivedGoals(list) {
  list = list ? list.slice() : [];
  var rule = R && R.meta && DERIVE_GOALS[R.meta.modeName];
  if (!rule || !R.events || !R.frames || !R.frames.length) return list;
  var goals = list.filter(function (o) { return o.kind === 'goal' && o.mode === rule.objMode; });
  var clusters = [], i, k;
  for (i = 0; i < R.events.length; i++) {
    var e = R.events[i];
    if (e.type !== 'objective' || !e.carrierFlag40 || e.carrier == null) continue;
    var c = posBefore(e.carrier, e.t);
    if (!c) continue;
    var near = false;
    for (k = 0; k < goals.length; k++) if (Math.hypot(c[0] - goals[k].x, c[2] - goals[k].z) < 7000) { near = true; break; }
    if (near) continue;
    var cl = null;
    for (k = 0; k < clusters.length; k++) if (Math.hypot(c[0] - clusters[k].x, c[2] - clusters[k].z) < 6000) { cl = clusters[k]; break; }
    if (!cl) { cl = { xs: [], ys: [], zs: [], x: c[0], z: c[2] }; clusters.push(cl); }
    cl.xs.push(c[0]); cl.ys.push(c[1]); cl.zs.push(c[2]);
    cl.x = medianOf(cl.xs); cl.z = medianOf(cl.zs);
  }
  // A named goal at the wrong HEIGHT: Winter Warehouse's file puts the goal at
  // y=0.5 m, every put on it is at 11.2 m (top floor), so the arrival test
  // (|dy| < 3 m) never fired. 2+ puts within 7 m and more than 3 m above or
  // below the mark move the mark to the puts' height.
  goals.forEach(function (g) {
    var ys = [];
    for (var q = 0; q < R.events.length; q++) {
      var ev = R.events[q];
      if (ev.type !== 'objective' || !ev.carrierFlag40 || ev.carrier == null) continue;
      var pc = posBefore(ev.carrier, ev.t);
      if (pc && Math.hypot(pc[0] - g.x, pc[2] - g.z) < 7000) ys.push(pc[1]);
    }
    if (ys.length >= 2 && Math.abs(medianOf(ys) - g.y) > 3000) {
      var gi = list.indexOf(g), fixed = {};
      Object.keys(g).forEach(function (kk) { fixed[kk] = g[kk]; });
      fixed.y = Math.round(medianOf(ys)); fixed.yMeasured = true;
      list[gi] = fixed;
    }
  });
  // A NAMED goal the match never used. n009a names A0 at (26, -44) and A1 at
  // (15, -54); in three Capture replays A1 took 22 puts, the unnamed west goal
  // 16, and A0 none. Capture has exactly two goals, so once two goals have puts
  // (named or derived) a named goal with none is not drawn. Team Sneaking has
  // one goal and no replay rotates it, so the rule is Capture-only.
  if (R.meta.modeName === 'CAP') {
    var used = clusters.filter(function (cl) { return cl.xs.length >= rule.minPuts; }).length, putsAt = {}, usedAt = {};
    goals.forEach(function (g, gi) {
      putsAt[gi] = 0;
      for (var q2 = 0; q2 < R.events.length; q2++) {
        var ev2 = R.events[q2];
        if (ev2.type !== 'objective' || !ev2.carrierFlag40 || ev2.carrier == null) continue;
        var pc2 = posBefore(ev2.carrier, ev2.t);
        if (pc2 && Math.hypot(pc2[0] - g.x, pc2[2] - g.z) < 7000) putsAt[gi]++;
      }
      // A0 and A1 are two ENTRIES on one spot: count the spot once, or a match
      // scored at one end only (n015a 358716_14, 6 puts) reads as two used
      // goals and hides the measured goal at the other end.
      if (putsAt[gi] >= 2) usedAt[Math.round(g.x / 1000) + ',' + Math.round(g.z / 1000)] = 1;
    });
    used += Object.keys(usedAt).length;
    if (used >= 2) {
      goals.forEach(function (g, gi) {
        if (putsAt[gi] === 0) list = list.filter(function (o) { return !(o.kind === 'goal' && o.mode === rule.objMode && o.x === g.x && o.z === g.z); });
      });
    }
  }
  var n = 0;
  clusters.forEach(function (cl) {
    if (cl.xs.length < rule.minPuts) return;
    list.push({ name: 'PRP_' + rule.objMode + '_GOAL_DERIVED_' + (n++), mode: rule.objMode, kind: 'goal', derived: true,
                x: Math.round(cl.x), y: Math.round(medianOf(cl.ys)), z: Math.round(cl.z), puts: cl.xs.length });
  });
  return list;
}
function buildObjectives(m) {
  var list = withDerivedGoals(m && m.objectives);
  tgtProps = [];
  if (!list || !list.length) return;
  var mode = R && R.meta && R.meta.modeName;
  objGroup = new THREE.Group();
  // MINI_* usually duplicates the standard placement exactly, so drawing both
  // stacks two markers in one spot. Skip a MINI only when a non-MINI really
  // does occupy that point -- Urban Ultimatum has NO PRP_TEAM_SNEAKING_GOAL_01,
  // its only goal is the MINI one, and blanket-skipping MINI hid the goal.
  var taken = {}, drawnAt = {}, tgtOrd = 0;
  for (var k = 0; k < list.length; k++) {
    if (!/^PRP_MINI_/.test(list[k].name)) {
      taken[list[k].x + ',' + list[k].y + ',' + list[k].z] = 1;
    }
  }
  for (var i = 0; i < list.length; i++) {
    var o = list[i];
    // Only show what this match is actually playing, when the mode is known.
    // PRP_STAGE_CENTER is stage METADATA, not an objective -- it marks the
    // middle of the map and is tagged ANY, so it drew in every single mode and
    // put a permanent marker in the centre that belongs to no game rule.
    if (o.kind === 'center') continue;
    if (!modeMatches(o.mode, mode)) continue;
    // RESCUE lists every point twice (_00 and _01). Where the two differ the match uses _01:
    // 4 of 5 pickups in 4 Rescue replays (n004a x2, n005a, n008a) are 0.6-0.8 m from TGT_01,
    // and on n004a TGT_00 is 40 m away. So a _00 that sits somewhere else is not drawn.
    if (o.mode === 'RESCUE' && /_00$/.test(o.name)) {
      var tw = null;
      for (var q0 = 0; q0 < list.length; q0++) if (list[q0].name === o.name.replace(/_00$/, '_01')) { tw = list[q0]; break; }
      if (tw && (tw.x !== o.x || tw.z !== o.z)) continue;
    }
    // CAPTURE, same idea: where a goal letter has two different points (Ravaged Riverfront,
    // Midtown, Groznyj Grad), the match scores at the one numbered 1. Measured 2026-10-08: 40 of
    // 40 puts on n009a + n012a land on A1 / B1 (an 'A0' reading on Midtown is 1.4 m from B1).
    // Dropping the unused 0 leaves one A and one B, so the goal colour rule works there too.
    // Capture only: Race moves its goal and is drawn from the match's own records.
    if (mode === 'CAP' && o.mode === 'RUGBY' && o.kind === 'goal' && /_GOAL_[AB]0$/.test(o.name)) {
      var tw1 = null;
      for (var q1 = 0; q1 < list.length; q1++) if (list[q1].name === o.name.replace(/0$/, '1')) { tw1 = list[q1]; break; }
      if (tw1 && (tw1.x !== o.x || tw1.z !== o.z)) continue;
    }
    if (/^PRP_MINI_/.test(o.name) && taken[o.x + ',' + o.y + ',' + o.z]) continue;
    // Coincident entities are ONE thing with several names -- on Coppertown
    // GOAL_A0/A1 share a position exactly, as do B0/B1, so drawing every entry
    // stacks two markers in one spot. Draw each distinct position once.
    var pk = o.kind + '@' + o.x + ',' + o.y + ',' + o.z;
    if (drawnAt[pk]) continue;
    drawnAt[pk] = 1;
    var ord = (o.kind === 'tgt') ? (tgtOrd++) : 0;
    var col = objColor(o, ord);
    var x = (o.x - ctr[0]) * S, y = o.y * S, z = (o.z - ctr[2]) * S;
    var sub = new THREE.Group();
    objGroup.add(sub);

    // A base is an AREA you stand in, not a point you touch, so it is drawn as
    // a square footprint on the ground rather than a floating marker.
    // PRP_BASE_MARK_* was labelled SPAWN on nothing but its name and its y=0.
    // Measured against 103 respawns on Groznyj Grad: MARK_05 has 56 of them
    // within 15 m, but MARK_04 has ZERO and the nearest is 77 m away -- that
    // team actually spawns at (-29000, 0, -107000). One of the pair happening
    // to sit near a spawn is not an identification, so neither is drawn.
    if (o.kind === 'spawnbase') continue;
    if (o.kind === 'base') {
      var bi = parseInt((o.name.match(/(\d+)$/) || [0, 0])[1], 10);
      // Sized from the game, not guessed. Base 3 on Groznyj Grad sits on a
      // bridge and the capture area is the deck's width; a principal-axis fit
      // to 4524 player samples within 10 m of it reads 18.3 m along the deck
      // and only 2.1 m across (2.95 m at p0.5-p99.5, 4.2 m min-max). The
      // original 14 m square was about four times too wide.
      var half = 1750 * S;
      var fill = new THREE.Mesh(
        new THREE.PlaneGeometry(half * 2, half * 2),
        new THREE.MeshBasicMaterial({ color: 0x888888, transparent: true,
                                      opacity: 0.16, side: THREE.DoubleSide,
                                      depthTest: false }));
      fill.rotation.x = -Math.PI / 2;
      fill.position.set(x, y + 0.5, z);
      fill.renderOrder = 4;
      sub.add(fill);
      var eg = new THREE.BufferGeometry();
      eg.setAttribute('position', new THREE.BufferAttribute(new Float32Array([
        x - half, y + 0.7, z - half, x + half, y + 0.7, z - half,
        x + half, y + 0.7, z + half, x - half, y + 0.7, z + half]), 3));
      var edge = new THREE.LineLoop(eg, new THREE.LineBasicMaterial({
        color: 0xC9D0C2, transparent: true, opacity: 0.8, depthTest: false }));
      edge.renderOrder = 5;
      sub.add(edge);
      objMarks.push({ o: o, group: sub, col: col,
                      base: (o.kind === 'base' ? bi - 1 : null),
                      spawn: o.kind === 'spawnbase',
                      fill: fill, edge: edge,
                      wx: o.x, wy: o.y, wz: o.z });
      continue;
    }

    // A GOAL IS A PATCH OF GROUND, not an object (user, 2026-10-04: "they aren't
    // spinning diamonds, they're just square locations on the map"). Drawn as a
    // flat square in the goal's colour with a thin locator beam and nothing
    // floating over it. GOAL_HALF_MM is NOT read from the game: targets are set
    // down within 0-3 m of the goal point in 13 of 14 measured puts.
    if (o.kind === 'goal') {
      var gh = GOAL_HALF_MM * S;
      var gfill = new THREE.Mesh(new THREE.PlaneGeometry(gh * 2, gh * 2),
        new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.24,
                                      side: THREE.DoubleSide, depthTest: false }));
      gfill.rotation.x = -Math.PI / 2;
      gfill.position.set(x, y + 0.5, z);
      gfill.renderOrder = 4;
      sub.add(gfill);
      var gg = new THREE.BufferGeometry();
      gg.setAttribute('position', new THREE.BufferAttribute(new Float32Array([
        x - gh, y + 0.7, z - gh, x + gh, y + 0.7, z - gh,
        x + gh, y + 0.7, z + gh, x - gh, y + 0.7, z + gh]), 3));
      var gedge = new THREE.LineLoop(gg, new THREE.LineBasicMaterial({
        color: col, transparent: true, opacity: 0.95, depthTest: false }));
      gedge.renderOrder = 5;
      sub.add(gedge);
      var gb = new THREE.BufferGeometry();
      gb.setAttribute('position', new THREE.BufferAttribute(
        new Float32Array([x, y, z, x, y + 130, z]), 3));
      var gbeam = new THREE.Line(gb, new THREE.LineBasicMaterial({
        color: col, transparent: true, opacity: 0.3, depthTest: false }));
      gbeam.renderOrder = 5;
      sub.add(gbeam);
      objMarks.push({ o: o, group: sub, col: col, prop: null, goal: true,
                      gfill: gfill, gedge: gedge, gbeam: gbeam, wx: o.x, wy: o.y, wz: o.z });
      continue;
    }
    // RACE targets do not start on the RUGBY_TGT spots (those are Capture's):
    // the first pickups of a Race round are 14 and 33 m from the nearest one.
    // Where they do start is not decoded, so no resting target is drawn there.
    if (o.kind === 'tgt' && mode === 'RACE') continue;
    var prop = o.kind === 'tgt' ? (ord % 2 ? 'gako' : 'kerotan') : null;
    objMarks.push({ o: o, group: sub, col: col, prop: prop });
    var mk = new THREE.Mesh(
      new THREE.OctahedronGeometry(o.kind === 'goal' ? 9 : 7),
      new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.95,
                                    depthTest: false }));
    mk.position.set(x, y + 12, z);
    mk.renderOrder = 5;
    sub.add(mk);
    if (prop) { tgtProps.push({ mesh: mk, kind: prop, y: y }); loadProps(); }
    // A beam tall enough to clear the skyline, so a rooftop target is findable
    // from anywhere; depthTest off so a building never swallows the marker.
    var bg = new THREE.BufferGeometry();
    bg.setAttribute('position', new THREE.BufferAttribute(
      new Float32Array([x, y, z, x, y + 130, z]), 3));
    var ln = new THREE.Line(bg, new THREE.LineBasicMaterial({
      color: col, transparent: true, opacity: 0.5, depthTest: false }));
    ln.renderOrder = 5;
    sub.add(ln);
    // a ring on the surface it actually sits on
    var rg = new THREE.Mesh(new THREE.RingGeometry(9, 12, 28),
      new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.55,
                                    side: THREE.DoubleSide, depthTest: false }));
    rg.rotation.x = -Math.PI / 2;
    rg.position.set(x, y + 0.6, z);
    rg.renderOrder = 5;
    sub.add(rg);
  }
  objGroup.visible = showObj;
  scene.add(objGroup);
  applyTargetProps();
  buildCarrySpans();
}


// The replay never stores the target's own position -- only WHO is holding it
// (§ objective events, carrierSlot). So while it is held the target is drawn on
// its carrier, and the static .geom marker for that spot is dimmed to show the
// item is no longer sitting there. Between a drop and the next pickup nothing
// is drawn, because the file genuinely does not say where it landed.
function carrierAt(t) {
  if (!R || !R.events) return null;
  var cur = null;
  for (var i = 0; i < R.events.length; i++) {
    var e = R.events[i];
    if (e.type !== 'objective' || e.t > t) continue;
    cur = (e.carrier == null) ? null : e.carrier;
  }
  return cur;
}

// Bind each carry to the TARGET IT CAME FROM, so the thing riding the carrier
// is that target -- green or yellow -- and not a generic token. The replay
// never says which target was taken, so it is resolved geometrically: the
// nearest drawn tgt to where the carrier stood at the moment of pickup. On
// Urban Ultimatum that lands 0.72 m from TEAM_SNEAKING_TGT_01, which is the
// same match that validated the extraction in the first place.

// Round boundaries, from meta.roundClock: the clock counts DOWN from 300 s, so
// a jump UP is a new round starting. Needed because the objective stream only
// reports CHANGES of holder -- if a round ends while someone is carrying, the
// next event can be minutes away in the following round, and a naive span runs
// straight through the reset and shows a stale carrier holding a target he no
// longer has. A carry cannot survive a round change.
function roundStarts() {
  var out = [];
  var rc = R && R.meta && R.meta.roundClock;
  if (!rc) return out;
  // The clock also jumps UP inside a round: in Bomb it is swapped for the fuse
  // at a plant and restored at a defuse (Finsletown read "new rounds" at 149 s
  // and 484 s, 284375_11 at 306 s). Where the file has the game's own
  // round-over records, a jump only starts a round if it is the FIRST jump
  // after one of them (measured gaps 22-62 s; a time window alone is not enough,
  // 290654_4 has a false jump 73 s after a round-over and a real one 62 s after).
  var ends = (R.meta.roundEnds && R.meta.roundEnds.length) ? R.meta.roundEnds : null;
  var usedEnd = -1;
  for (var i = 1; i < rc.length; i++) {
    if (rc[i][1] <= rc[i - 1][1] + 1.0) continue;
    if (ends) {
      var last = -1;
      for (var k = 0; k < ends.length; k++) if (ends[k].t <= rc[i][0] + 1) last = k;
      if (last < 0 || last === usedEnd || rc[i][0] - ends[last].t > 120) continue;
      usedEnd = last;
    }
    out.push(rc[i][0]);
  }
  return out;
}
function clipToRound(t0, t1) {
  var rs = roundStarts();
  for (var i = 0; i < rs.length; i++) {
    if (rs[i] > t0 && rs[i] < t1) return rs[i];
  }
  return t1;
}

function buildCarrySpans() {
  // Leave carrySpans NULL if the replay is not ready, so carrySpanAt() retries.
  // Setting it to [] here would cache "no carries" forever if the stage mesh
  // happened to finish before the frames did.
  if (!R || !R.events || !R.frames || !R.frames.length) return;
  carrySpans = [];
  dropSpans = [];
  goalArrivals = null;
  goalOwners = null;
  // Objectives are tracked PER ITEM. RACE runs two at once -- one target per
  // team -- and the parser already separates them as item 0 and item 1 (41 and
  // 43 events on Coppertown, each alternating cleanly on its own). Collapsing
  // them into a single open span made one marker flip between two targets and
  // truncated each carry at the other item's next event.
  var open = {};
  for (var i = 0; i < R.events.length; i++) {
    var e = R.events[i];
    if (e.type !== 'objective') continue;
    var key = (e.item == null) ? 0 : e.item;
    var cur = open[key];
    if (cur) {
      cur.t1 = Math.min(cur.t1, e.t); open[key] = null;
      // A DROPPED TARGET LIES WHERE IT FELL. The file names only the holder, so
      // the spot is the holder's own position at the drop. It stays there until
      // someone picks it up or dropResetS() passes, when the game sends it home
      // (user, 2026-10-04; measured on a Race match: 21 of 23 pickups inside
      // 30 s are 0-3 m from the drop spot, every one after 30 s is elsewhere).
      if (e.carrier == null && cur.t1 === e.t) {
        var dc = insCell(R, cur.i, e.t);
        // THE FILE SAYS WHERE IT LIES AND WHEN IT IS SENT HOME (meta.targetOps,
        // found 2026-10-05): every drop has a "lies at" record (119 of 119 over 5
        // replays; 135 of 142 are within 1.5 m of the last carrier, the rest are
        // targets that fell or were knocked away), and in Race the "sent home"
        // record comes exactly 30.0 s later, 12 of 12. Use them when present; the
        // carrier's own position and the 30 s rule stay as the fallback.
        var tops = (R.meta && R.meta.targetOps) || [], lie = null, home = null;
        for (var tq = 0; tq < tops.length; tq++) {
          var to_ = tops[tq];
          if (to_.item !== key || to_.t < e.t - 1.5) continue;
          if (!lie) { if (to_.kind === 'lie' && to_.t <= e.t + 1.5) lie = to_; else if (to_.t > e.t + 1.5) break; continue; }
          if (to_.kind === 'home') { home = to_; break; }
          if (to_.kind === 'take' || to_.kind === 'lie') break;
        }
        var dEnd = home ? home.t : e.t + dropResetS();
        if (dc || lie) dropSpans.push({ item: key, t0: e.t, t1: Math.min(dEnd, clipToRound(e.t, Infinity)),
                                 x: lie ? lie.p[0] : dc[0], y: dc ? dc[1] : lie.p[1] - 800, z: lie ? lie.p[2] : dc[2],
                                 mark: cur.mark, exact: !!lie, home: !!home,
                                 team: R.players[cur.i] ? R.players[cur.i].team : null });
      }
    } else if (dropSpans.length) {
      // picked up again: the last drop of this item ends here
      for (var dq = dropSpans.length - 1; dq >= 0; dq--) {
        if (dropSpans[dq].item === key) { if (dropSpans[dq].t1 > e.t) dropSpans[dq].t1 = e.t; break; }
      }
    }
    if (e.carrier == null) continue;
    // In Bomb the holder re-reported with the 0x40 flag IS the plant: the bomb
    // is on the ground from then on, so no new carry starts (seen on screen:
    // the planter kept the carrier mark for the whole fuse).
    if (e.carrierFlag40 && R.meta && R.meta.mode === 6) continue;
    var mk = nearestTargetTo(e.carrier, e.t);
    var sp = { i: e.carrier, item: key, t0: e.t,
               t1: clipToRound(e.t, Infinity), mark: mk };
    open[key] = sp;
    carrySpans.push(sp);
  }
  // A CARRY ENDS WHEN THE TARGET IS USED UP (target record 0xfe: in Bomb that is
  // the plant, to the tenth of a second, 10 of 10). The holder field keeps
  // naming the planter afterwards, so without this he wore the carrier mark
  // while the bomb sat on the ground -- seen on screen on Finsletown at 2:02,
  // eight seconds after Fin Diesel planted.
  var gone = ((R.meta && R.meta.targetOps) || []).filter(function (o) { return o.kind === 'fe'; });
  carrySpans.forEach(function (sp2) {
    for (var g = 0; g < gone.length; g++) {
      if (gone[g].item === sp2.item && gone[g].t > sp2.t0 && gone[g].t < sp2.t1) { sp2.t1 = gone[g].t; break; }
    }
  });
}
function nearestTargetTo(pi, t) {
  var fi = frameIndexAt(t), best = null, bd = Infinity;
  var row = R.frames[fi] && R.frames[fi].p;
  var e = row && row[pi];
  if (!e) return null;
  for (var k = 0; k < objMarks.length; k++) {
    var o = objMarks[k].o;
    if (o.kind !== 'tgt') continue;
    var dx = o.x - e[0], dy = o.y - e[1], dz = o.z - e[2];
    var d = dx * dx + dy * dy + dz * dz;
    if (d < bd) { bd = d; best = objMarks[k]; }
  }
  return best;
}
// HOW LONG A DROPPED TARGET LIES BEFORE THE GAME SENDS IT HOME. Measured 2026-10-08 on the file's own 'sent home' records
// (scratch loop/capreset.js): RACE 30.0 s (423 of 423), CAPTURE 60.0 s (9 of 9; and in Capture a target was picked up where
// it lay as late as 57.2 s after the drop). The viewer used 30 s for both (user: "in capture the targets dont reset in
// 30 seconds").
function dropResetS() { return (R && R.meta && R.meta.modeName === 'CAP') ? 60 : 30; }
var dropSpans = [], dropMarks = [], restMarks = [];

// THE TWO ITEM BOXES (meta.worldOps, world object). Each round the game puts
// two boxes on fixed spots; the file then says who takes each one and where it
// lies when its carrier goes down. MEASURED on 12 replays: the named taker is
// within 2.5 m of the box 21 of 21 times, the named dropper is on the drop
// 20 of 20 times and died within 2 s in 16 of them.
// WHICH is which is INFERRED from what carriers do (the game's name table has
// the wrong names for both ids). Over 17 replays, carriers of 0x71 spend 3.8%
// of their time in "run, fastest variant" and 3.4% in "walk backward" -- motion
// states nobody else shows (0.00% / 0.04% over 72,000 player-seconds) -- and
// have empty hands twice as often: that is moving about inside the drum can.
// Carriers of 0x8c just fight as normal, so that one is the ENVG. Team Sneaking
// sets out only 0x71. Shapes from the user: ENVG = a small box, drum can = a
// large rectangular one.
var BOX_ITEMS = { 0x8c: { label: 'ENVG', color: 0x58C4DD, css: '#58C4DD', size: [0.55, 0.55, 0.55], prop: 'ibox_small', scale: 1.6 },
                  0x71: { label: 'DRUM CAN', color: 0xE8964B, css: '#E8964B', size: [1.5, 1.0, 1.0], prop: 'ibox_large', scale: 1.6 },
                  0x45: { label: 'MAG', color: 0xD9D4C0, css: '#D9D4C0', size: [0.45, 0.06, 0.6], prop: 'mag', scale: 2 },
                  // SOP DESTABILIZER as placed (item 0x44; emin_put_def_mid.mdn, model confirmed by the user 2026-10-08).
                  // It uses the magazine's two device codes: 0x65 set down, 0xa5 taken away. One case on record
                  // (52613): set down 156.7 s, credited 161.5 s, taken away 166.9 s. Drawn 3x life size (it is 40 cm).
                  0x44: { label: 'DESTABILIZER', color: 0x9CCB6B, css: '#9CCB6B', size: [0.4, 0.8, 0.4], prop: 'destab', scale: 3 } };
var boxMarks = [];
function buildItemBoxes() {
  if (!R || R.boxes) return;
  var out = R.boxes = { lie: [], carry: [] };
  var ops = ((R.meta && R.meta.worldOps) || []).filter(function (o) { return o.o === 0 || o.item === 0x45 || o.item === 0x44; });
  if (!ops.length) return;
  // Round boundaries come from the game's own round-over records, NOT from
  // roundStarts(): in Bomb the clock jumps back up when a bomb is defused,
  // which roundStarts() reads as a new round (Finsletown: 149 s and 484 s).
  // A box appearing somewhere new is a new round as well (see below).
  var sess = ((R.meta && R.meta.roundEnds) || []).map(function (e) { return e.t; });
  function clipToRound(t) {            // shadows the clock-based one on purpose
    for (var s = 0; s < sess.length; s++) if (sess[s] > t) return sess[s];
    return Infinity;
  }
  var open = {}, deaths = {};
  (R.events || []).forEach(function (e) {
    if (e.type === 'kill' && e.victim != null) (deaths[e.victim] || (deaths[e.victim] = [])).push(e.t);
  });
  ops.forEach(function (o) {
    var key = o.n + ':' + o.id, cur = open[key];
    if (o.o === 1) {
      // A MAGAZINE set down as a trap (device object, item 0x45). MEASURED on
      // 20 placements in 6 replays: the nearest player is 0.6-0.8 m away with
      // the magazine in his hands every time, and the object id is his lobby
      // slot << 3. It lies there until the game takes it away (code 0xa5; left
      // alone it goes after 119.4 s, 3 of 3). WHY it went is not decoded.
      var mk = 'm' + o.item + ':' + key, mc = open[mk];
      if (o.code === 0x65 && o.p) {
        if (mc && mc.t1 > o.t && mc.x === o.p[0] && mc.z === o.p[2]) return;
        open[mk] = { item: o.item, t0: o.t, t1: Math.min(o.t + 120, clipToRound(o.t)), x: o.p[0], y: o.p[1], z: o.p[2] };
        out.lie.push(open[mk]);
      } else if (o.code === 0xa5 && mc && mc.t1 > o.t) mc.t1 = o.t;
      return;
    }
    if (o.code === 0x74 || o.code === 0x64) {
      if (!o.p || (o.item !== 0x71 && o.item !== 0x8c)) return;   // other dropped things: not drawn
      // the game re-sends whatever is still lying there: not a new object
      if (cur && cur.t1 > o.t && cur.item === o.item && cur.x === o.p[0] && cur.z === o.p[2]) return;
      if (o.code === 0x74) {
        // a box set out afresh = the round restarted: nothing from before survives
        out.lie.concat(out.carry).forEach(function (sp) { if (sp.t0 < o.t - 5 && sp.t1 > o.t) sp.t1 = o.t; });
      }
      if (o.code === 0x64) {
        // the carrier let go of it here
        for (var q = out.carry.length - 1; q >= 0; q--) {
          var cs = out.carry[q];
          if (cs.item === o.item && cs.t0 <= o.t && cs.t1 > o.t && (o.who == null || cs.i === o.who)) { cs.t1 = o.t; break; }
        }
      }
      open[key] = { item: o.item, t0: o.t, t1: clipToRound(o.t, Infinity), x: o.p[0], y: o.p[1], z: o.p[2],
                    drop: o.code === 0x64 };
      out.lie.push(open[key]);
    } else if (o.code === 0x83 && cur && cur.t1 > o.t) {
      cur.t1 = o.t;
      if (o.who == null) return;
      var t1 = clipToRound(o.t, Infinity);
      // a dead player carries nothing, whether or not a drop was recorded
      (deaths[o.who] || []).forEach(function (dt) { if (dt > o.t && dt < t1) t1 = dt; });
      out.carry.push({ item: cur.item, i: o.who, t0: o.t, t1: t1 });
    }
  });
}
// WHICH TEAM IS A GOAL FOR? Not in the file. Inferred from where targets are
// actually taken: an "arrival" is a carrier coming within GOAL_NEAR_MM of a
// goal, and a team's goal at time t is the one its NEXT arrival (in the same
// round) lands on. In Race the goal moves when a target resets, so this is
// re-read per moment; a goal nobody is about to reach stays uncoloured.
var goalArrivals = null, GOAL_NEAR_MM = 4500;
// CAPTURE: a team's goal is FIXED for the half. Tomb of Tubes replay_284191_13:
// team 0 put 4 of 4 at the east goal in the first half and 6 of 6 at the west
// goal in the second; team 2 the other way round. So once a team has put a
// target down in this half, its goal is known for the WHOLE half -- not only
// while a carrier happens to be within 4.5 m of it (the arrival inference,
// which is kept for halves with no put yet and for Race).
var goalOwners = null;
function halfStarts() { return [0].concat(roundStarts()); }
function halfStartAt(t) {
  var hs = halfStarts(), h0 = 0;
  for (var i = 0; i < hs.length; i++) if (hs[i] <= t) h0 = hs[i];
  return h0;
}
function buildGoalOwners() {
  goalOwners = [];
  var goals = objMarks.filter(function (m) { return m.goal; });
  if (!goals.length || !R || !R.events) return;
  var votes = {};
  for (var i = 0; i < R.events.length; i++) {
    var e = R.events[i];
    if (e.type !== 'objective' || !e.carrierFlag40 || e.carrier == null) continue;
    var pl = R.players[e.carrier];
    if (!pl || pl.team == null) continue;
    var c = posBefore(e.carrier, e.t);
    if (!c) continue;
    var best = null, bd = 7000;
    for (var g = 0; g < goals.length; g++) {
      var d = Math.hypot(c[0] - goals[g].wx, c[2] - goals[g].wz);
      if (d < bd) { bd = d; best = goals[g]; }
    }
    if (!best) continue;
    var key = pl.team + '@' + halfStartAt(e.t);
    var v = votes[key] || (votes[key] = { team: pl.team, h0: halfStartAt(e.t), n: {} });
    var gi = goals.indexOf(best);
    v.n[gi] = (v.n[gi] || 0) + 1;
  }
  Object.keys(votes).forEach(function (key) {
    var v = votes[key], top = null, tn = 0;
    Object.keys(v.n).forEach(function (gi) { if (v.n[gi] > tn) { tn = v.n[gi]; top = goals[+gi]; } });
    if (top) goalOwners.push({ team: v.team, h0: v.h0, goal: top, puts: tn });
  });
}
function buildGoalArrivals() {
  goalArrivals = [];
  var goals = objMarks.filter(function (m) { return m.goal; });
  if (!goals.length || !carrySpans) return;
  carrySpans.forEach(function (sp) {
    var pl = R.players[sp.i];
    if (!pl || pl.team == null) return;
    var seen = {};
    for (var f = frameIndexAt(sp.t0); f < R.frames.length && R.frames[f].t < sp.t1; f++) {
      var c = (R.frames[f].p || [])[sp.i];
      if (!c || !c[4]) continue;
      for (var g = 0; g < goals.length; g++) {
        if (seen[g]) continue;
        var dx = c[0] - goals[g].wx, dz = c[2] - goals[g].wz;
        if (dx * dx + dz * dz <= GOAL_NEAR_MM * GOAL_NEAR_MM && Math.abs(c[1] - goals[g].wy) < 3000) {
          seen[g] = 1;
          goalArrivals.push({ t: R.frames[f].t, team: pl.team, goal: goals[g] });
        }
      }
    }
  });
  goalArrivals.sort(function (a, b) { return a.t - b.t; });
}
function paintGoals() {
  if (!R || !objMarks.length) return;
  if (R.race) {
    objMarks.forEach(function (mk0) {
      if (!mk0.goal) return;
      mk0.asGoal = false;
      mk0.gfill.visible = mk0.gedge.visible = mk0.gbeam.visible = false;
    });
    return;
  }
  if (!carrySpans) return;
  if (!goalArrivals) buildGoalArrivals();
  var end = clipToRound(T, Infinity), next = {}, k;
  if (R.meta && R.meta.modeName === 'CAP') {
    if (!goalOwners) buildGoalOwners();
    var h0 = halfStartAt(T);
    for (k = 0; k < goalOwners.length; k++) {
      if (goalOwners[k].h0 === h0 && next[goalOwners[k].team] === undefined) next[goalOwners[k].team] = goalOwners[k].goal;
    }
    // Both goals are NAMED in the stage file (PRP_RUGBY_GOAL_A* / _B*), so the half's
    // colours need not wait for a put. Measured on 4 Capture replays (n001a, n005a x2,
    // n008a; 55 puts; re-measured 2026-10-08: 328 of 328 puts over 24 matches on 8 maps): in
    // the first half the file's team 0 scores at A and its team 2 at B (0 and 1 here), and they
    // swap every half. Used only when the map draws exactly one A and one B goal and
    // no put in THIS match contradicts it; a put in this half always wins.
    var gAB = { A: [], B: [] };
    objMarks.forEach(function (m) {
      var mm = m.goal && m.o && /_GOAL_([AB])\d$/.exec(m.o.name || '');
      if (mm) gAB[mm[1]].push(m);
    });
    if (gAB.A.length === 1 && gAB.B.length === 1) {
      var gA = gAB.A[0], gB = gAB.B[0], hsL = halfStarts(), hi = hsL.indexOf(h0), okRule = hi >= 0;
      for (k = 0; k < goalOwners.length && okRule; k++) {
        var ow = goalOwners[k], oi = hsL.indexOf(ow.h0);
        // TEAM NUMBERS HERE ARE 0 AND 1: applyReplay renumbers the file's team values (0 and 2)
        // to their rank. This block first shipped testing for 2, which coloured the second
        // goal for a team that does not exist whenever a half had no put yet (fixed 2026-10-08).
        if (ow.team !== 0 && ow.team !== 1) continue;
        if (ow.goal !== gA && ow.goal !== gB) continue;
        if (ow.goal !== (((oi % 2 === 0) === (ow.team === 0)) ? gA : gB)) okRule = false;
      }
      if (next[0] !== undefined && next[1] === undefined && (next[0] === gA || next[0] === gB)) next[1] = next[0] === gA ? gB : gA;
      else if (next[1] !== undefined && next[0] === undefined && (next[1] === gA || next[1] === gB)) next[0] = next[1] === gA ? gB : gA;
      else if (okRule && next[0] === undefined && next[1] === undefined) {
        next[0] = hi % 2 === 0 ? gA : gB;
        next[1] = hi % 2 === 0 ? gB : gA;
      }
    }
  }
  for (k = 0; k < goalArrivals.length; k++) {
    var a = goalArrivals[k];
    if (a.t < T - 1.5 || a.t >= end) continue;
    if (next[a.team] === undefined) next[a.team] = a.goal;
  }
  for (k = 0; k < objMarks.length; k++) {
    var mk = objMarks[k];
    if (!mk.goal) continue;
    var teams = [];
    Object.keys(next).forEach(function (tm) { if (next[tm] === mk) teams.push(+tm); });
    teams.sort();
    mk.cur = teams;
    var c0 = teams.length ? teamColor(teams[0]) : '#C9D0C2';
    var c1 = teams.length > 1 ? teamColor(teams[1]) : c0;
    mk.gfill.material.color.set(c0);
    mk.gfill.material.opacity = teams.length ? 0.28 : 0.10;
    mk.gedge.material.color.set(c1);
    mk.gbeam.material.color.set(c1);
    // A GOAL IS A GOAL UNTIL IT BECOMES A SPAWN (user, 2026-10-04). In Race the
    // goal moves when a target resets: the old goal corner is then just a spawn,
    // and the corner that was a spawn is the goal. So a goal square with no team
    // heading for it, standing on a spawn corner, is not drawn as a goal at all.
    mk.spawnHere = null;
    var sps = (R && R.spawns) || [];
    for (var q = 0; q < sps.length; q++) {
      if (Math.abs(sps[q].wx - mk.wx) < 9000 && Math.abs(sps[q].wz - mk.wz) < 9000) { mk.spawnHere = sps[q]; break; }
    }
    mk.asGoal = teams.length > 0 || !mk.spawnHere;
    mk.gfill.visible = mk.gedge.visible = mk.gbeam.visible = mk.asGoal;
    if (mk.spawnHere) mk.spawnHere.goalNow = teams.length > 0;
  }
}
// every span live at time t -- there can be one per item
function carrySpansAt(t) {
  if (!carrySpans) buildCarrySpans();
  if (!carrySpans) return [];
  var out = [];
  for (var i = 0; i < carrySpans.length; i++) {
    if (t >= carrySpans[i].t0 && t < carrySpans[i].t1) out.push(carrySpans[i]);
  }
  return out;
}


// ---------------------------------------------------------------------------
// Between-round flash. When the playhead crosses a round boundary, show what
// that round scored for a few seconds. Boundaries come from roundStarts(),
// which reads meta.roundClock -- the same detector the carry spans use, so the
// flash and the carry can never disagree about where a round ended.
var RF_HOLD = 5.0;      // seconds of playback the panel stays up
function roundWindowFor(t) {
  var rs = roundStarts();
  if (!rs.length) return null;
  for (var i = 0; i < rs.length; i++) {
    if (t >= rs[i] && t < rs[i] + RF_HOLD) {
      return { n: i + 1, from: i === 0 ? 0 : rs[i - 1], to: rs[i] };
    }
  }
  return null;
}
function statsBetween(a, b) {
  var n = R.players.length;
  var k = new Array(n).fill(0), d = new Array(n).fill(0), s = new Array(n).fill(0);
  (R.events || []).forEach(function (e) {
    if (e.t < a || e.t >= b) return;
    if (e.type === 'kill') {
      if (e.killer != null && k[e.killer] != null) k[e.killer]++;
      if (e.victim != null && d[e.victim] != null) d[e.victim]++;
    } else if (e.type === 'stun') {
      if (e.killer != null && s[e.killer] != null) s[e.killer]++;
    }
  });
  return { k: k, d: d, s: s };
}
var rfShown = null;
function updateRoundFlash() {
  var host = $('roundflash');
  if (!host || !R) return;
  var w = roundWindowFor(T);
  if (!w) {
    if (host.dataset.open === '1') { host.dataset.open = '0'; rfShown = null; }
    return;
  }
  var key = w.n;
  if (rfShown === key && host.dataset.open === '1') return;
  rfShown = key;
  var st = statsBetween(w.from, w.to);
  var rows = R.players.map(function (p, i) {
    return { p: p, i: i, k: st.k[i], d: st.d[i], s: st.s[i] };
  }).filter(function (r) { return r.k || r.d || r.s; })
    .sort(function (a, b2) { return (b2.k - a.k) || (b2.s - a.s) || (a.d - b2.d); });
  var html = '<div class="rf-hd"><span>Round ' + w.n + ' complete</span>' +
             '<span>' + fmt(w.to - w.from) + '</span></div>';
  if (!rows.length) {
    html += '<table><tbody><tr><td class="nm">No scoring events in this round.</td></tr></tbody></table>';
  } else {
    html += '<table><thead><tr><th></th><th>Player</th><th>K</th><th>D</th>' +
            '<th>Stun</th></tr></thead><tbody>';
    rows.forEach(function (r) {
      html += '<tr data-team="' + (r.p.team == null ? '' : r.p.team) + '">' +
        '<td class="bar"></td><td class="nm">' + esc(r.p.name || '?') + '</td>' +
        '<td>' + (r.k || '—') + '</td><td>' + (r.d || '—') + '</td>' +
        '<td>' + (r.s || '—') + '</td></tr>';
    });
    html += '</tbody></table>';
  }
  host.innerHTML = html;
  host.dataset.open = '1';
}

function updateCarry() {
  if (!scene) return;
  carryMarkAt(0);   // ensure at least one exists
  // `live` is indexed BY PLAYER INDEX and is sparse -- updateMarks() guards
  // every read with `if (!p)`. Scanning it and dereferencing live[i].i threw a
  // TypeError the instant a carrier existed, which killed the rAF loop and
  // froze the whole console at the moment of the pickup.
  var spans = carrySpansAt(T);
  var drops = dropSpans.filter(function (d) { return T >= d.t0 && T < d.t1; });
  // In Bomb the loose bomb is drawn by bombLooseAt() for as long as it lies
  // there, not for the 30 s the frog and duck get.
  if (R.meta && R.meta.mode === 6) drops = [];
  // Race has no resting target on the map to ask for the frog and duck models,
  // so ask here: anything carried or dropped needs them.
  if (spans.length || drops.length) loadProps();
  // a static marker hides while ITS OWN target is in someone's hands, or lying
  // where it was dropped
  for (var k = 0; k < objMarks.length; k++) {
    var held = false;
    for (var q = 0; q < spans.length; q++) {
      if (spans[q].mark === objMarks[k]) { held = true; break; }
    }
    for (var q2 = 0; !held && q2 < drops.length; q2++) {
      if (drops[q2].mark && drops[q2].mark === objMarks[k]) held = true;
    }
    objMarks[k].group.visible = !held;
  }
  // the dropped ones, standing on the spot they fell
  for (var dm = 0; dm < dropMarks.length; dm++) dropMarks[dm].visible = false;
  for (var dn = 0; dn < drops.length; dn++) {
    while (dropMarks.length <= dn) {
      var nm_ = new THREE.Mesh(new THREE.OctahedronGeometry(5),
        new THREE.MeshBasicMaterial({ color: 0xE8C547, transparent: true, opacity: 0.9, depthTest: false }));
      nm_.visible = false; scene.add(nm_); dropMarks.push(nm_);
    }
    var dd = drops[dn], dmk = dropMarks[dn];
    var dk = targetKind(dd.item, dd.mark);
    var onGround = setProp(dmk, dk, TGT_SCALE);
    dmk.position.set((dd.x - ctr[0]) * S, dd.y * S + (onGround ? 0 : 8), (dd.z - ctr[2]) * S);
    dmk.visible = showObj;
  }
  // the item boxes: a turning cube where one stands or lies
  buildItemBoxes();
  for (var bm = 0; bm < boxMarks.length; bm++) boxMarks[bm].visible = false;
  resetPropPool();
  if (R.boxes.lie.length || (R.x && R.x.gear && R.x.gear.length)) loadProps();
  var nb_ = 0;
  for (var bl = 0; showObj && bl < R.boxes.lie.length; bl++) {
    var bs = R.boxes.lie[bl];
    if (T < bs.t0 || T >= bs.t1) continue;
    while (boxMarks.length <= nb_) {
      var bmk = new THREE.Mesh(new THREE.BoxGeometry(6, 6, 6),
        new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.85 }));
      bmk.visible = false; scene.add(bmk); boxMarks.push(bmk);
    }
    // THE GAME'S OWN MODELS where the prop pack has them (replay/prop_pack2.py):
    // the small item box for the ENVG, the large one for the drum can, the
    // placed magazine. Drawn BOX_SCALE times life size so they read from above
    // (the frog and duck are drawn 5x for the same reason). The cube stays as
    // the stand-in until the pack has loaded.
    var bz_ = BOX_ITEMS[bs.item].size, bkind = BOX_ITEMS[bs.item].prop;
    var pm = bkind ? pooledProp(bkind, BOX_ITEMS[bs.item].scale) : null;
    if (pm) {
      pm.position.set((bs.x - ctr[0]) * S, bs.y * S - PROPS[bkind].ymin * MM * BOX_ITEMS[bs.item].scale + 0.3, (bs.z - ctr[2]) * S);
      pm.rotation.y = (bs.item === 0x45 || bs.item === 0x44) ? 0 : T * 1.2;
      pm.visible = true;
      continue;
    }
    var bk = boxMarks[nb_++];
    bk.material.color.setHex(BOX_ITEMS[bs.item].color);
    bk.scale.set(bz_[0], bz_[1], bz_[2]);
    bk.position.set((bs.x - ctr[0]) * S, bs.y * S + 1 + 3 * bz_[1], (bs.z - ctr[2]) * S);
    bk.rotation.y = T * 1.6;
    bk.visible = true;
  }
  // INSIDE THE DRUM CAN: the can itself, life size, over the player in it
  // (R.x.gear flag 10; the body is hidden while it applies, see updateMarks)
  var gsp = (showObj && R.x && R.x.gear) || [];
  for (var gq = 0; gq < gsp.length; gq++) {
    if (gsp[gq][0] > T) break;
    if (gsp[gq][3] !== 10 || T > gsp[gq][1]) continue;
    var lpd = live[gsp[gq][2]];
    if (!lpd || !lpd.alive) continue;
    var dm_ = pooledProp('drum', 1);
    if (!dm_) break;
    dm_.position.set((lpd.x - ctr[0]) * S, lpd.y * S, (lpd.z - ctr[2]) * S);
    dm_.rotation.y = 0;
    dm_.visible = true;
  }
  // BOMB PLANT SITES: an amber ring on the ground at each one (see bombSites)
  var bsl = showObj ? bombSites() : [];
  for (var sm = 0; sm < siteMarks.length; sm++) siteMarks[sm].visible = false;
  for (var sq_ = 0; sq_ < bsl.length; sq_++) {
    while (siteMarks.length <= sq_) {
      var smk = new THREE.Mesh(new THREE.RingGeometry(19, 22, 4, 1, Math.PI / 4),
        new THREE.MeshBasicMaterial({ color: 0xE8C547, side: THREE.DoubleSide, transparent: true, opacity: 0.75, depthTest: false }));
      smk.rotation.x = -Math.PI / 2; smk.visible = false; scene.add(smk); siteMarks.push(smk);
    }
    siteMarks[sq_].position.set((bsl[sq_].x - ctr[0]) * S, bsl[sq_].y * S + 0.6, (bsl[sq_].z - ctr[2]) * S);
    siteMarks[sq_].visible = true;
  }
  // THE PLANTED BOMB, on the spot its own record gives: a block that turns
  // from red to ice-blue as the freeze gauge fills, inside a ring that drains
  // over the 60 s fuse.
  var lb = liveBombAt(T);
  if (bombMk) bombMk.g.visible = false;
  if (lb && showObj && lb.x != null) {
    if (!bombMk) {
      var bg = new THREE.Group();
      var bb = new THREE.Mesh(new THREE.BoxGeometry(4.2, 2.6, 3.2), new THREE.MeshBasicMaterial({ color: 0xE2574C }));
      bb.position.y = 1.5;
      var br = new THREE.Mesh(new THREE.RingGeometry(7.5, 9.2, RING_SEG, 1, Math.PI / 2, Math.PI * 2),
        new THREE.MeshBasicMaterial({ color: 0xE2574C, side: THREE.DoubleSide, transparent: true, opacity: 0.9, depthTest: false }));
      br.rotation.x = -Math.PI / 2; br.position.y = 0.4; br.renderOrder = 5;
      bg.add(bb); bg.add(br); scene.add(bg);
      bombMk = { g: bg, body: bb, ring: br, cA: new THREE.Color(0xE2574C), cB: new THREE.Color(0x7FD4F0) };
    }
    var fz = clamp(bombGaugeAt(lb, T) || 0, 0, 1), fuse_ = lb.exact ? BOMB_FUSE_EXACT : BOMB_FUSE;
    bombMk.g.position.set((lb.x - ctr[0]) * S, (lb.y != null ? lb.y : 0) * S, (lb.z - ctr[2]) * S);
    bombMk.body.material.color.copy(bombMk.cA).lerp(bombMk.cB, fz);
    bombMk.body.rotation.y = T * 0.8;
    // the game's own time bomb (tmbm_put_def), and its frozen-over twin
    // (tmbm_put_snow) once the gauge is past half; 3x life size (6x stood as tall as a player's waist)
    loadProps();
    var tb = pooledProp(fz > 0.5 ? 'bomb_ice' : 'bomb', 3);
    bombMk.body.visible = !tb;
    if (tb) { tb.position.copy(bombMk.g.position); tb.rotation.y = 0; tb.visible = true; }
    setArc(bombMk.ring, clamp((lb.t + fuse_ - T) / fuse_, 0, 1), fz > 0.02 ? 0x7FD4F0 : 0xE2574C);
    bombMk.g.visible = true;
  }
  // THE BOMB IS ALWAYS ON THE MAP (user, 2026-10-05): when nobody carries it and
  // it is not planted, it is drawn where it lies -- on its dropped spot, or
  // waiting where it will next be picked up.
  var loose = showObj ? bombLooseAt(T) : null;
  if (loose) {
    loadProps();
    var lbp = pooledProp('bomb', 3);
    if (lbp) { lbp.position.set((loose.x - ctr[0]) * S, loose.y * S + 0.3, (loose.z - ctr[2]) * S); lbp.rotation.y = T * 0.9; lbp.visible = true; }
  }
  paintBases();
  paintSpawns();
  // RACE: a target nobody holds, and that is not lying where it was dropped, is
  // AT ITS TEAM'S SPAWN -- that is where it starts and where a reset sends it.
  // MEASURED on replay_347079_2: blue's pickups after its three resets are all
  // at (22.4, 6.6), its spawn point then; its first pickup of the round is 5 m
  // from its base. Target k belongs to team k (26 of 26 carries, two matches).
  for (var rm = 0; rm < restMarks.length; rm++) restMarks[rm].visible = false;
  if (R.race && R.race.exact) {
    loadProps();
    R.race.teams.forEach(function (tm, bit) {
      var busy = false, q3;
      for (q3 = 0; q3 < spans.length; q3++) if (spans[q3].item === bit) busy = true;
      for (q3 = 0; q3 < drops.length; q3++) if (drops[q3].item === bit) busy = true;
      if (busy) return;
      var sq = R.race.seq[tm], at = null;
      for (q3 = 0; q3 < sq.length; q3++) { if (sq[q3].t <= T) at = sq[q3]; else break; }
      if (!at || !at.corner) return;
      while (restMarks.length <= bit) {
        var rmk = new THREE.Mesh(new THREE.OctahedronGeometry(5),
          new THREE.MeshBasicMaterial({ color: 0xE8C547, transparent: true, opacity: 0.9, depthTest: false }));
        rmk.visible = false; scene.add(rmk); restMarks.push(rmk);
      }
      var rk = restMarks[bit];
      var okR = setProp(rk, bit === 1 ? 'gako' : 'kerotan', TGT_SCALE);
      rk.position.set((at.corner.x - ctr[0]) * S, at.corner.y * S + (okR ? 0 : 8), (at.corner.z - ctr[2]) * S);
      rk.visible = showObj;
    });
  }
  try { paintGoals(); } catch (errG) { if (!window.__goalErr) { window.__goalErr = errG; console.warn('goal colours off:', errG); } }
  // Recolour every base square for the moment on the playhead. A base nobody
// has touched yet stays neutral grey rather than guessing an owner.
function paintBases() {
  if (!objMarks.length) return;
  var tc = baseTeamColors();
  for (var k = 0; k < objMarks.length; k++) {
    var mk = objMarks[k];
    if (mk.base == null && !mk.spawn) continue;
    var col = '#6E7A6B', op = 0.14;
    if (mk.spawn) {
      col = '#6E7A6B'; op = 0.10;
    } else {
      var st = baseStateAt(mk.base, T);
      // ONLY an actually-captured base is coloured. Progress lives in the bar;
      // lighting the square up as soon as someone stands on it was the bug.
      if (st.owner !== 0) {
        col = st.owner > 0 ? tc[0] : tc[1];
        op = 0.30;
      }
      mk.state = st;
    }
    mk.fill.material.color.set(col);
    mk.fill.material.opacity = op;
    mk.edge.material.color.set(col === '#6E7A6B' ? '#C9D0C2' : col);
  }
}

// one rider per carried item, so two targets in flight both show
  for (var s = 0; s < carryMarks.length; s++) carryMarks[s].visible = false;
  for (var s2 = 0; s2 < spans.length; s2++) {
    var sp = spans[s2];
    var p = (sp.i != null && live[sp.i]) ? live[sp.i] : null;
    if (!p) continue;
    var mk2 = carryMarkAt(s2);
    mk2.visible = showObj;
    var kindC = targetKind(sp.item, sp.mark);
    if (setProp(mk2, kindC, CARRY_SCALE)) {
      // ON THE CARRIER (user, 2026-10-04), not hovering over his head: seated
      // on his upper back, facing the way he faces, and moving with him.
      var hd = p.h == null ? 0 : p.h;
      mk2.position.set((p.x - ctr[0]) * S - Math.sin(hd) * CARRY_BACK, p.y * S + CARRY_UP,
                       (p.z - ctr[2]) * S - Math.cos(hd) * CARRY_BACK);
      mk2.rotation.set(0, hd, 0);
    } else {
      mk2.material.color.setHex(sp.mark ? sp.mark.col : 0xE8C547);
      mk2.position.set((p.x - ctr[0]) * S, p.y * S + 23, (p.z - ctr[2]) * S);
      mk2.rotation.y += 0.05;
    }
  }
}



// ---------------------------------------------------------------------------
// Held props. The three figure meshes are RIGID -- they have no skeleton, so
// arms cannot bend and there is no "aiming" mesh. What is honest to do with
// them is put the right object in the hand and move that object: a grenade, a
// blade, or a gun, raised to chest height and pushed forward when the player is
// aiming or firing. The body itself does not aim; only the prop moves.
// Real limb motion needs the .mtar keyframe codec, which is not decoded.
// 68 = the SOP destabilizer: carried and set down in one hand (user, 2026-10-08: "should be in his hand when he is
// holding it and planting it"), so it rides the same one-hand seat as a grenade.
var THROWN = { 52: 1, 54: 1, 55: 1, 56: 1, 57: 1, 59: 1, 63: 1, 46: 1, 68: 1 };
// THREE TABLES USED TO DISAGREE ABOUT WHAT A KNIFE IS.
// FIREARM listed 2 and 29, which the game's own 297-entry item pool calls
// ST KNIFE and TANTO, so item 2 -- 12,550 samples -- was classed as a gun and
// its 261 mm blade was drawn held like a rifle. MELEE meanwhile claimed item 1,
// which that same pool calls NONE, i.e. nothing in the hand at all (134,938
// samples). weapons.json agreed with neither: it maps 2/16/29/36 to the cqck
// mesh. The knife branch of the palm-seating code was consequently reached on
// 0 of 2,180,603 samples.
// Now all three agree, and the names come from the pool rather than a guess:
// 2 ST KNIFE, 16 R KNIFE, 29 TANTO, 36 V KNIFE, 48 HF BLADE.
// CORRECTED 2026-10-04: item 2 is NOT a knife. The pool's ST KNIFE string sits
// one row late (see ITEM_FIX above): id 2 stuns at a median 12.56 m and pulses
// the firing action like any gun (38 pulses, 15 stun hits in one TSNE match).
// It is the MK2 tranquilliser pistol, so it is held and counted as a pistol.
var MELEE  = { 16: 1, 29: 1, 36: 1, 48: 1 };
var FIREARM = { 2: 1, 7: 1, 20: 1, 21: 1, 22: 1, 23: 1, 24: 1, 25: 1, 26: 1,
                27: 1, 28: 1, 30: 1, 31: 1, 32: 1, 33: 1, 43: 1 };
function propKind(item) {
  if (item == null) return null;
  if (THROWN[item]) return 'nade';
  if (MELEE[item]) return 'knife';
  if (FIREARM[item]) return 'gun';
  return null;
}
// ---- real weapon models -------------------------------------------------
var WEAPONS = null;          // code -> BufferGeometry
var WEAPON_OF = null;        // item id -> code
var weaponsAsked = false;
function loadWeapons() {
  if (weaponsAsked) return;
  weaponsAsked = true;
  fetch(av('weapons.json') + (ASSET_V ? '&' : '?') + 'w=3').then(function (r) { return r.ok ? r.json() : null; })
    .then(function (W) {
      if (!W || !W.meshes) return;
      WEAPONS = {};
      for (var code in W.meshes) {
        var m = W.meshes[code];
        var pos = new Float32Array(m.pos.length);
        for (var i = 0; i < m.pos.length; i++) pos[i] = m.pos[i] / m.q;
        var g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        g.setIndex(m.idx);
        // weapon_tex.py: the model's own UVs, and its faces grouped by material
        // with the albedo texture each one uses (wtex/<hash>.webp)
        if (m.uv && m.groups) {
          var uv = new Float32Array(m.uv.length), uq = m.uvq || 4096;
          for (var u = 0; u < m.uv.length; u++) uv[u] = m.uv[u] / uq;
          g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
          g.userData.tex = [];
          for (var gi = 0; gi < m.groups.length; gi++) {
            g.addGroup(m.groups[gi][1], m.groups[gi][2], gi);
            g.userData.tex.push(m.groups[gi][0]);
          }
        }
        g.computeVertexNormals();
        WEAPONS[code] = g;
      }
      WEAPON_OF = W.itemMesh || {};
      if (typeof warn === 'function') { /* silent success */ }
    })
    .catch(function () { WEAPONS = null; });
}
// The real skin of a weapon model: one material per draw group, textured with
// that group's albedo. Null for a model with no UVs (then the flat colour is
// used). Kept on the geometry, so every player holding an M4 shares one set.
var WTEX_CACHE = {};
function weaponTexture(hash) {
  var t = WTEX_CACHE[hash];
  if (!t) {
    t = WTEX_CACHE[hash] = new THREE.TextureLoader().load(av('wtex/' + hash + '.webp'));
    t.flipY = false;                    // the pack keeps the game's own UVs
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    if (THREE.SRGBColorSpace) t.colorSpace = THREE.SRGBColorSpace;
  }
  return t;
}
function weaponMats(geo) {
  if (!geo || !geo.userData || !geo.userData.tex) return null;
  if (geo.userData.mats) return geo.userData.mats;
  return (geo.userData.mats = geo.userData.tex.map(function (h) {
    return h ? new THREE.MeshLambertMaterial({ map: weaponTexture(h), side: THREE.DoubleSide })
             : new THREE.MeshLambertMaterial({ color: 0x23261f });
  }));
}
// the model for an item id, or null to fall back to the primitive
function weaponGeo(item) {
  if (!WEAPONS || !WEAPON_OF || item == null) return null;
  // weapons.json maps item 2 to the knife mesh by its (misplaced) name; it is
  // the MK2 pistol, whose model is the Ruger mesh
  var code = (MESH_FIX[item] && WEAPONS[MESH_FIX[item]]) ? MESH_FIX[item] : WEAPON_OF[String(item)];
  return code ? (WEAPONS[code] || null) : null;
}

var PROP_GEO = null;
function propGeos() {
  if (!PROP_GEO) {
    PROP_GEO = {
      nade:  new THREE.SphereGeometry(0.75, 8, 6),
      knife: new THREE.BoxGeometry(0.22, 2.6, 0.5),
      gun:   new THREE.BoxGeometry(0.7, 0.7, 3.4)
    };
  }
  return PROP_GEO;
}
function attachProp(m) {
  if (m.prop) return;
  var g = propGeos();
  var mesh = new THREE.Mesh(g.gun, new THREE.MeshLambertMaterial({ color: 0x2b2f28 }));
  mesh.visible = false;
  m.dir.add(mesh);
  m.prop = mesh;
}
// Where a held object actually sits in the hand. The hand bone's origin is the
// WRIST, so a prop parented there with no offset floats at the back of the
// hand -- visible on a grenade, hidden on a rifle because the rifle's own
// origin is its grip. MEASURED: the finger roots off bone 8 (thumb 21, and
// chains 24/27/30/34) have a centroid at (60.3, 8.0, -0.9) mm in the hand's
// own frame, stable to 1 mm across run, idle and grenade-run alike. Two thirds
// along that is the middle of the palm, where a fist closes on something.
var PALM_MM = { x: 40, y: 6, z: -0.6 };
var _centred = {};
// A grenade model's origin is not its centre (m67g spans y -35.9..+83.5), so
// centring the geometry is what actually seats it in the fist rather than
// hanging it above the knuckles.
function centredGeo(g) {
  if (!g) return g;
  if (g.__centred) return g.__centred;
  var c = g.clone();
  c.computeBoundingBox();
  var b = c.boundingBox;
  c.translate(-(b.min.x + b.max.x) / 2, -(b.min.y + b.max.y) / 2, -(b.min.z + b.max.z) / 2);
  g.__centred = c;
  return c;
}

var THROW_EMPTY_S = 1.1;
function justThrew(pi, t) {
  if (pi < 0 || !R) return false;
  var by = R.__throwsBy;
  if (!by) {
    by = R.__throwsBy = [];
    (R.events || []).forEach(function (e) {
      if (e.type === 'throw' && e.killer != null) (by[e.killer] || (by[e.killer] = [])).push(e.t);
    });
  }
  var a = by[pi];
  if (!a) return false;
  for (var k = 0; k < a.length; k++) {
    if (a[k] > t) break;
    if (t - a[k] <= THROW_EMPTY_S) return true;
  }
  return false;
}
var SHIELD_ROT = [0, 0, 0], SHIELD_POS = [0, 0, 0], _shq = null, _shb = null, _she = null, _shf = null;
function updateProp(m, p, aiming) {
  loadWeapons();
  // THE SHIELD (item 73; the game's own model shid_shd_def_mgo, 62 x 109 cm). It is carried on the LEFT arm, so it hangs on
  // the left hand bone rather than in the weapon hand. Until now it had no model at all (user, 2026-10-08: "looks like
  // white square"). SHIELD_ROT / SHIELD_POS seat it on the arm; set by eye, not measured.
  var shOn_ = !!(p && p.alive && p.item === 73 && m.bones && SKIN && SKIN.hand2 != null && m.bones[SKIN.hand2]);
  if (shOn_) {
    var shG_ = weaponGeo(73);
    if (shG_) {
      if (!m.shieldProp) {
        m.shieldProp = new THREE.Mesh(shG_, weaponMats(shG_) || new THREE.MeshLambertMaterial({ color: 0x2a2d27, side: THREE.DoubleSide }));
        m.bones[SKIN.hand2].add(m.shieldProp);
      }
      // UPRIGHT AND FACING THE WAY HE FACES. Left to the hand bone it hung at about 45 degrees (seen 2026-10-08 with the
      // test camera): the hand's own rotation is cancelled and the body's put in its place, then SHIELD_ROT turns the
      // model's face to the front.
      if (!_shq) { _shq = new THREE.Quaternion(); _shb = new THREE.Quaternion(); _she = new THREE.Euler(); _shf = new THREE.Quaternion(); }
      m.bones[SKIN.hand2].getWorldQuaternion(_shq);
      (m.dir || m.g).getWorldQuaternion(_shb);
      _shf.setFromEuler(_she.set(SHIELD_ROT[0], SHIELD_ROT[1], SHIELD_ROT[2]));
      m.shieldProp.quaternion.copy(_shq.invert()).multiply(_shb).multiply(_shf);
      m.shieldProp.position.set(SHIELD_POS[0], SHIELD_POS[1], SHIELD_POS[2]);
      m.shieldProp.visible = true;
    }
  } else if (m.shieldProp) m.shieldProp.visible = false;
  var kind = p && p.alive ? propKind(p.item) : null;
  // A thrown item with none left is not in the hand. `ammo` is [CONFIRMED] as
  // the count remaining for the held item id, so 0 means the pouch is empty
  // and the model should go with it.
  if (kind === 'nade' && p && p.ammo === 0) kind = null;
  // THE ONE IN THE HAND IS THE ONE THAT FLIES (user, 2026-10-04): from the
  // moment a throw is recorded until the next grenade would be drawn out, the
  // hand is empty -- otherwise one grenade is in the air while an identical one
  // is still in the fist.
  if (kind === 'nade' && justThrew(marks.indexOf(m), T)) kind = null;
  var real = kind ? weaponGeo(p.item) : null;

  // SKINNED: the model hangs on the hand bone, so the pose puts it where the
  // player's hand actually is and none of the old offsets apply.
  if (m.handProp) {
    if (real) {
      // small held objects are seated in the palm; a rifle keeps its own grip
      // origin, which is already correct
      var seat = (kind === 'nade' || kind === 'knife');
      var geo = seat ? centredGeo(real) : real;
      if (m.handProp.geometry !== geo) m.handProp.geometry = geo;
      // A grenade in the near-black gun colour disappears against a glove, so
      // thrown items get their own olive. Shown whenever one is equipped and
      // at least one is left (the ammo test above).
      var wantCol = kind === 'nade' ? 0x5d6b3a : 0x23261f;
      if (!m.handPropFlat) m.handPropFlat = m.handProp.material;
      var skin = weaponMats(geo);
      if (skin) {
        if (m.handProp.material !== skin) m.handProp.material = skin;
      } else {
        if (m.handProp.material !== m.handPropFlat) m.handProp.material = m.handPropFlat;
        if (m.handPropFlat.color.getHex() !== wantCol) m.handPropFlat.color.setHex(wantCol);
      }
      // a pistol's grip also belongs in the palm, not on the wrist; it keeps
      // its own origin (assumed to be the grip, like the rifles -- not measured)
      if (seat || SECONDARY[p.item]) m.handProp.position.set(PALM_MM.x, PALM_MM.y, PALM_MM.z);
      else m.handProp.position.set(0, 0, 0);
      m.handProp.visible = true;
      if (m.prop) m.prop.visible = false;
      if (window.__PROPDIAG) {
        var c = (WEAPON_OF && WEAPON_OF[String(p.item)]) || ('item' + p.item);
        window.__PROPDIAG[c] = (window.__PROPDIAG[c] || 0) + 1;
      }
      return;
    }
    m.handProp.visible = false;      // no model for this id: fall through
    if (window.__PROPDIAG) {
      var k = 'NO MESH item ' + (p ? p.item : '?');
      window.__PROPDIAG[k] = (window.__PROPDIAG[k] || 0) + 1;
    }
  }

  if (!m.prop) return;
  if (!kind) { m.prop.visible = false; return; }
  var g = propGeos();
  var want = g[kind];
  if (m.prop.geometry !== want) m.prop.geometry = want;
  m.prop.visible = true;
  // hand at the right hip when carried, raised to the chest and forward when
  // the player is aiming, firing, or winding up a throw
  if (aiming) {
    m.prop.position.set(kind === 'nade' ? 2.2 : 1.6, 11.4, kind === 'nade' ? -1.2 : 2.6);
    m.prop.rotation.set(kind === 'nade' ? 0 : -0.15, 0, kind === 'knife' ? -0.5 : 0);
  } else {
    m.prop.position.set(3.0, 8.6, 0.4);
    m.prop.rotation.set(kind === 'gun' ? 0.5 : 0, 0, 0);
  }
}

// ---- SKINNED FIGURE ------------------------------------------------------
// The three meshes above are 3D-print exports: rigid, already posed, no bone
// assignment. figure_skinned.json is built straight from the .mdn instead, in
// BIND pose, carrying one bone index per vertex, and anims.json holds clips
// captured off the running game (anim_capture.py / anim_bake.py).
//
// Skinning is exact rather than approximate because of two measured facts:
// MGO2 binds exactly ONE bone per vertex (no weight channel exists), and the
// bind pose is translation-only -- in mgo_sopF.mdn the local offset equals the
// world-space parent difference to 0.000 mm on all 94 non-root bones, which
// can only happen when every bind rotation is identity. So the bind matrix of
// bone i is just translate(rest[i]), and posing is
//     v' = (v - rest[i]) * R[i](t) + T[i](t)
// which is precisely a three.js Skeleton whose boneInverse is translate(-rest)
// and whose bone carries R and T directly. No hierarchy is composed at
// runtime: clips store every bone in world form, so nothing can drift.
//
// If either file is missing the rigid meshes stay, and the viewer looks
// exactly as it did before.
// Bone 4 is the head: it is the parent of the whole SKL_4xx face group, its
// origin sits at the base of the skull (rest Y 560 mm, with head vertices
// reaching 743 mm), and the topmost vertex of the mesh binds to it.
var HEAD_BONE = 4;
// bone 8 is the RIGHT hand: its children are the four finger chains
// (thumb 21-23, index 24-26, middle 27-29, ring/little 30-36). 12 is the
// left hand by the same test. Measured off the .mdn hierarchy, not assumed.
var SPINE_BONE = 1;
var HAND_BONE = 8;
// 12 is the support hand. A two-handed weapon lies along the line between the
// two, which is what lets the barrel be MEASURED per frame instead of guessed.
var OFF_HAND_BONE = 12;
// Knives sit inside FIREARM in the table above; they are not two-handed.
var KNIFE_ITEM = MELEE;            // one table, not two that drift apart
var SKIN = null;        // {geo, map} shared by every player
var ANIM = null;        // {fps, bones, clips:{}}
var MM = 0.01;          // millimetres -> scene units (1 unit = 100 mm)
var SKIN_LIFT = 0;      // scene units to raise the bind pose so feet reach y=0

// ?anims=<file> swaps the clip source, e.g. ?anims=anims_mtar.json to play clips
// baked straight from the game's .mtar files (mtar_bake.py) instead of the live
// captures. Same JSON format either way; only same-directory names are allowed.
function animsFile() {
  try {
    var v = new URLSearchParams(location.search).get('anims');
    if (v && /^[A-Za-z0-9_\-]+\.json$/.test(v)) return v;
  } catch (e) {}
  // Defaults are the MERGED sets (replay/merge_anims.py): live captures first,
  // .mtar-baked clips filling the gaps. The female rig especially -- anims.json
  // only ever had rigtest, so the female figure had no motion at all; the merge
  // takes it to 16 clips. The unmerged sources stay reachable by hand with
  // ?anims=anims.json / ?anims=anims_male.json / ?anims=anims_mtar_female.json.
  return figureSex() === 'male' ? 'anims_male_merged.json' : FEMALE_ANIMS;
}
// THE FEMALE SET IS THE MALE LIVE CAPTURES RE-POSED ON HER SKELETON
// (replay/retarget_female.py, 2026-10-05). The set baked from the game's
// animation files did not match them: measured, her run leaned 67 degrees
// against his 47 (user: "female leans forward when running weird"), her rifle
// clips did not hold a fixed two-handed gap, her climb was a 0.8 s loop on wall
// time and nothing carried a measured speed. Re-posed, every clip has the male
// clip's lean, timing, speed and play-once flag, on her own bone lengths (worst
// error 1.4 mm). Her narrower shoulders bring the hands ~100 mm closer on a
// rifle (251 mm against 351). ?femanims=old brings the baked set back.
var FEMALE_ANIMS = /[?&]femanims=old/.test(location.search) ? 'anims_female.json' : 'anims_female_rt.json';
function animsFileFor(sex) {
  if (sex === figureSex()) return animsFile();
  return sex === 'male' ? 'anims_male_merged.json' : FEMALE_ANIMS;
}
// ?sex=male|female in the address FORCES every player onto that body. Without
// it each player gets the sex their own appearance block says (gender 0 = male).
function forcedSex() {
  try {
    var v = new URLSearchParams(location.search).get('sex');
    return (v === 'male' || v === 'female') ? v : null;
  } catch (e) { return null; }
}
function sexOfPlayer(pl) {
  var f = forcedSex();
  if (f) return f;
  var ap = pl && pl.appearance;
  if (ap && (ap.gender === 0 || ap.gender === 1)) return ap.gender === 1 ? 'female' : 'male';
  return figureSex();
}

// Every figure / animation / gear / texture file is fetched with the build's
// asset stamp, or a browser keeps serving the previous build's copy: a stale
// tex/<hash>.webp showed a player's camo as the old flat tan after it was fixed.
var ASSET_V = window.__MGO2_ASSETV__ || '';
// maps live in another folder and may already carry a query string
// MAP_V: bumped when only the map JSONs change (2026-10-07: Capture B goals added), so the
// heavy HD packs and textures behind ASSET_V are not re-downloaded for it.
var MAP_V = 'g3';
function mapUrlV(u) { return !u ? u : u + (u.indexOf('?') < 0 ? '?' : '&') + 'av=' + ASSET_V + MAP_V; }
function av(u) { return ASSET_V ? u + '?v=' + ASSET_V : u; }
// HD_V: bumped when the HD scenery packs are rebuilt. 2026-10-08 's1': seven packs (n002a, n003a,
// n007a, n012a, n020a, n022a, sm_dd) had been built from a game install carrying the author's own
// stage edits; they were rebuilt from the stock client's files (md5-checked against the launcher manifest).
var HD_V = 's2';   // s2: n009a rebuilt from the stock client too (SaveMGO dropped one prop from that map)
function avHD(u) { return u + '?v=' + ASSET_V + HD_V; }

// One rig per sex: {sex, skin, anim, lift, gear}. SKIN / ANIM / SKIN_LIFT stay
// as the globals every pose function reads; useRig() points them at the rig of
// the player being processed. All per-player skin work runs inside
// attachSkinned() or updateMarks() -> applyPose(), and both switch first.
var RIGS = {}, RIG_REQ = {};
function useRig(r) { SKIN = r.skin; ANIM = r.anim; SKIN_LIFT = r.lift; }

// ?gear=0 turns the per-player outfits off (everyone wears the default figure).
var GEAR_ON = (function () {
  try { return new URLSearchParams(location.search).get('gear') !== '0'; }
  catch (e) { return true; }
})();
var SKIN_TONE = { white: 0xd8b69a, black: 0x6b4a36, asian: 0xd2ad88, latino: 0xb98c68 };
var GEAR_COL = { head: 0x3a3d33, chest: 0x34372e, waist: 0x2f3129, foot: 0x26271f,
                 hand: 0x2b2c26, acc: 0x1c1d1a };

// ---- gear textures --------------------------------------------------------
// tex/index.json lists what was exported: {hash: 1} opaque, {hash: 2} has alpha.
// A texture the manifest does not list is never requested, and its triangles
// keep the flat stand-in colour.
var TEX_INDEX = null, TEX_CACHE = {}, TEX_WAIT = [];
var TEX_PAL = {};           // swatch-sheet texture -> its default colour
var VARIANTS = {};          // sex -> {"slot:key": {colour: {fromHash: toHash}}}
function loadTexIndex() {
  Promise.all([
    fetch(av('tex/index.json')).then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; }),
    fetch(av('tex/palettes.json')).then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; })
  ]).then(function (res) {
      var j = res[0];
      TEX_PAL = res[1] || {};
      TEX_INDEX = j || {};
      var w = TEX_WAIT; TEX_WAIT = [];
      for (var i = 0; i < w.length; i++) w[i]();
    });
}
function gearTexture(hash, isData) {
  if (!TEX_INDEX || !TEX_INDEX[hash]) return null;
  var t = TEX_CACHE[hash];
  if (!t) {
    t = TEX_CACHE[hash] = new THREE.TextureLoader().load(av('tex/' + hash + '.webp'));
    // The pack keeps the game's own UVs (v down, origin top-left), so the
    // image must not be flipped on upload.
    t.flipY = false;
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    // Colour textures are sRGB; a mask is plain data.
    if (!isData && THREE.SRGBColorSpace) t.colorSpace = THREE.SRGBColorSpace;
  }
  return t;
}

var TINT_CACHE = {};
function tintTexture(hex) {
  var t = TINT_CACHE[hex];
  if (!t) {
    var n = parseInt(hex, 16);
    t = TINT_CACHE[hex] = new THREE.DataTexture(
      new Uint8Array([(n >> 16) & 255, (n >> 8) & 255, n & 255, 255]), 1, 1, THREE.RGBAFormat);
    if (THREE.SRGBColorSpace) t.colorSpace = THREE.SRGBColorSpace;
    t.needsUpdate = true;
  }
  return t;
}

// The game shades a garment as  albedo x pattern  where a mask allows it:
// the albedo is greyscale cloth with its seams and shading, the pattern is a
// small tile (a flat colour by default, a camo once a colour is picked) laid
// out by its OWN UV set, and the mask keeps straps, buckles and pockets out
// of it. Read off the textures themselves (layer sheet, 2026-10-04); the 1.35
// gain is by eye, NOT measured from the game's shader.
function addRepLayer(mat, repT, maskT, tiled) {
  mat.onBeforeCompile = function (sh) {
    sh.uniforms.repMap = { value: repT };
    sh.uniforms.repMask = { value: maskT };
    sh.vertexShader = 'attribute vec2 uvr;\nvarying vec2 vUvR;\n' +
      sh.vertexShader.replace('#include <uv_vertex>', '#include <uv_vertex>\n\tvUvR = uvr;');
    sh.fragmentShader = 'uniform sampler2D repMap;\nuniform sampler2D repMask;\nvarying vec2 vUvR;\n' +
      sh.fragmentShader.replace('#include <map_fragment>',
        '#include <map_fragment>\n\t{ vec3 rc = texture2D( repMap, ' + (tiled ? 'vUvR' : 'vMapUv') + ' ).rgb;' +
        ' float rk = ' + (maskT ? 'texture2D( repMask, vMapUv ).r' : '1.0') + ';' +
        ' diffuseColor.rgb *= mix( vec3( 1.0 ), rc * 1.35, rk ); }');
  };
  // one program for "with mask", one for "without"
  mat.customProgramCacheKey = function () {
    return (maskT ? 'rep+mask' : 'rep') + (tiled ? '' : '+print');
  };
}

// Clan emblems, 32x32, fetched ahead of time by replay/fetch_emblems.py from
// SaveMGO's public site into emblems/<clanId>.png (the replay carries only the
// clan id). emblems/index.json lists which exist. This is the clan's emblem
// TODAY, not at the time of the match.
var EMBLEM_TEX = '829292';
// Bare-skin materials on short-sleeved and sleeveless tops. Their texture in the
// files is a 32x32 placeholder that NO colour row replaces (checked against
// both variant tables), so the game must fill it with the wearer's own skin.
// Drawn in the face's skin tone; without this they took the TEAM colour and
// players had red or blue arms.
var SKIN_PLACEHOLDER = { '3089af': 1, 'b0a378': 1 };
var EMB_INDEX = null, EMB_CACHE = {};
function loadEmblemIndex() {
  return fetch(av('emblems/index.json')).then(function (r) { return r.ok ? r.json() : null; })
    .catch(function () { return null; })
    .then(function (j) { EMB_INDEX = j || {}; });
}
function emblemTexture(clanId) {
  if (clanId == null || !EMB_INDEX || !EMB_INDEX[clanId]) return null;
  var t = EMB_CACHE[clanId];
  if (!t) {
    t = EMB_CACHE[clanId] = new THREE.TextureLoader().load(av('emblems/' + clanId + '.png'));
    t.flipY = false;
    // 32x32 pixel art on a small patch: keep the pixels crisp, like the game's stitched look
    t.magFilter = THREE.NearestFilter;
    if (THREE.SRGBColorSpace) t.colorSpace = THREE.SRGBColorSpace;
  }
  return t;
}

// A player's body, assembled from the gear pack by their appearance block:
// face + upper + lower + hands, then whatever they wear over it. Returns null
// when there is nothing to assemble from, and the caller keeps the shared
// default figure. Otherwise {geo, mats}: one material per texture.
//
// Each triangle group names its ALBEDO texture. The game recolours an item by
// substituting texture names (the colour variant table); VARIANTS carries that
// table, keyed by item and by the colour number the replay records.
// Where no texture was exported the group falls back to a flat colour: clothes
// take the team colour, skin the face's tone, gear a dark neutral.
var COLOUR_FIELD = { head: 'headColor', chest: 'chestColor', hand: 'handColor',
                     waist: 'waistColor', foot: 'footColor', upper: 'upperColor',
                     lower: 'lowerColor', face: 'facePaint' };
function composeGeo(rig, ap, teamCol, clanId) {
  if (!GEAR_ON || !rig || !rig.gear || !ap) return null;
  var G = rig.gear, want = [], i;
  var V = VARIANTS[rig.sex] || {};
  function add(slot, key, cls, colour) {
    var part = G[slot + ':' + key];
    if (part) want.push([part, cls, slot, (V[slot + ':' + key] || {})[colour] || null]);
    return !!part;
  }
  var face = G['face:' + ap.face];
  var tone = SKIN_TONE[(face && face.tone) || 'white'];
  add('face', ap.face, 'skin', ap.facePaint);
  // 16 is Naked (male) / Bikini (female): mostly skin either way.
  if (!add('upper', ap.upper, ap.upper === 16 ? 'skin' : 'cloth', ap.upperColor)) add('upper', 11, 'cloth', 0);
  if (!add('lower', ap.lower, 'cloth', ap.lowerColor)) add('lower', 22, 'cloth', 0);
  if (!add('hand', ap.hand, ap.hand === 46 ? 'skin' : 'gear', ap.handColor)) add('hand', 46, 'skin', 0);
  add('foot', ap.foot, 'gear', ap.footColor);
  add('chest', ap.chest, 'gear', ap.chestColor);
  add('waist', ap.waist, 'gear', ap.waistColor);
  add('head', ap.head, 'gear', ap.headColor);
  add('acc', ap.acc1, 'gear', ap.acc1Color);
  if (ap.acc2 !== ap.acc1) add('acc', ap.acc2, 'gear', ap.acc2Color);
  if (want.length < 3) return null;        // a pack with no base body: keep the default

  var nv = 0, ni = 0;
  for (i = 0; i < want.length; i++) { nv += want[i][0].pos.length / 3; ni += want[i][0].idx.length; }
  var pos = new Float32Array(nv * 3), nrm = new Float32Array(nv * 3), uv = new Float32Array(nv * 2);
  var uvr = new Float32Array(nv * 2);
  var si = new Uint16Array(nv * 4), sw = new Float32Array(nv * 4);
  var idx = new Uint32Array(ni);
  var g = new THREE.BufferGeometry();
  var mats = [], flats = [], matOf = {}, vo = 0, io_ = 0, c = new THREE.Color();
  for (i = 0; i < want.length; i++) {
    var part = want[i][0], cls = want[i][1], sub = want[i][3];
    if (cls === 'skin') c.setHex(tone);
    else if (cls === 'cloth') c.copy(teamCol);
    else c.setHex(GEAR_COL[want[i][2]] || 0x30322b).lerp(teamCol, 0.18);
    var n = part.pos.length / 3;
    var nInf = part.w ? 4 : 1;
    for (var v = 0; v < n; v++) {
      var o = (vo + v) * 3;
      pos[o] = part.pos[v * 3]; pos[o + 1] = part.pos[v * 3 + 1]; pos[o + 2] = part.pos[v * 3 + 2];
      if (part.nrm) {
        nrm[o] = part.nrm[v * 3] / 127; nrm[o + 1] = part.nrm[v * 3 + 1] / 127; nrm[o + 2] = part.nrm[v * 3 + 2] / 127;
      }
      if (part.uv) { uv[(vo + v) * 2] = part.uv[v * 2]; uv[(vo + v) * 2 + 1] = part.uv[v * 2 + 1]; }
      if (part.uvr) { uvr[(vo + v) * 2] = part.uvr[v * 2]; uvr[(vo + v) * 2 + 1] = part.uvr[v * 2 + 1]; }
      // Up to four bone influences per vertex (the game's own weights). An
      // influence on a bone the clips do not carry rides that bone's nearest
      // animated ancestor; the weights are renormalised afterwards.
      var q4 = (vo + v) * 4, tot = 0;
      for (var b4 = 0; b4 < nInf; b4++) {
        var wgt = part.w ? part.w[v * 4 + b4] : 255;
        if (!wgt) continue;
        var sl = rig.skin.slotAnc[part.bone[v * nInf + b4]];
        if (sl === undefined) continue;
        si[q4 + b4] = sl; sw[q4 + b4] = wgt; tot += wgt;
      }
      if (tot > 0) { for (b4 = 0; b4 < 4; b4++) sw[q4 + b4] /= tot; }
      else { si[q4] = 0; sw[q4] = 1; }
    }
    var groups = part.groups || [['', 0, part.idx.length]];
    for (var gi = 0; gi < groups.length; gi++) {
      var from = groups[gi][0], hash = (sub && sub[from]) || from;
      var tex = gearTexture(hash);
      // SaveMGO's emblem beret: its square patch is one material whose only
      // texture (829292) is bound at run time to the wearer's clan emblem.
      var isEmblem = from === EMBLEM_TEX;
      if (isEmblem) { tex = emblemTexture(clanId); hash = 'emb' + clanId; }
      // The pattern layer. Faces are left alone: their 6-texture material is
      // not the cloth layout, and multiplying skin by its slot 3 is wrong.
      var repFrom = (want[i][2] === 'face' || isEmblem) ? '' : (groups[gi][3] || '');
      var repH = repFrom ? ((sub && sub[repFrom]) || repFrom) : '';
      var repT = (tex && repH) ? gearTexture(repH) : null;
      // A palette-coloured item (berets): the colour is one flat swatch, already
      // looked up at build time, so the "pattern" is a 1x1 texture of that tint.
      if (tex && repFrom && sub && sub.tint) { repT = tintTexture(sub.tint); repH = '#' + sub.tint; }
      // ...and a swatch sheet with no colour row for this pick shows its default
      // cell. It is never laid over the garment as an image (that drew a rainbow
      // across a balaclava).
      else if (repT && TEX_PAL[repH]) { repT = tintTexture(TEX_PAL[repH]); repH = '#' + TEX_PAL[repH]; }
      var maskH = repT ? (groups[gi][4] || '') : '';
      var maskT = maskH ? gearTexture(maskH, true) : null;
      // untextured groups of one class share a material; textured ones are per texture set
      var mk = tex ? 't' + hash + '|' + (repT ? repH : '') + '|' + (maskT ? maskH : '')
                   : (SKIN_PLACEHOLDER[from] ? 'skin' : 'c' + c.getHexString());
      var mi = matOf[mk];
      if (mi === undefined) {
        mi = matOf[mk] = mats.length;
        var mat = new THREE.MeshLambertMaterial({ side: THREE.DoubleSide });
        if (isEmblem && !tex) { mat.visible = false; }   // no emblem known: no patch
        if (tex) {
          mat.map = tex;
          if (isEmblem) { mat.alphaTest = 0.5; }
          if (TEX_INDEX[hash] & 2) mat.alphaTest = 0.5;
          // a tile repeats along the garment's own tiling UVs; a print (the
          // 512x512 shirt designs) is one image over the main UVs. A flat tint
          // is the same either way.
          if (repT) addRepLayer(mat, repT, maskT, !!(TEX_INDEX[repH] & 4));
        }
        else if (SKIN_PLACEHOLDER[from]) mat.color.setHex(tone);
        else mat.color.copy(c);
        mats.push(mat);
        // the flat stand-in for the same triangles, used when textures are off
        var fm = new THREE.MeshLambertMaterial({ side: THREE.DoubleSide });
        if (SKIN_PLACEHOLDER[from]) fm.color.setHex(tone); else
        fm.color.copy(c);
        flats.push(fm);
      }
      for (var k = 0; k < groups[gi][2]; k++) idx[io_ + k] = part.idx[groups[gi][1] + k] + vo;
      g.addGroup(io_, groups[gi][2], mi);
      io_ += groups[gi][2];
    }
    vo += n;
  }
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  g.setAttribute('uvr', new THREE.BufferAttribute(uvr, 2));
  g.setAttribute('skinIndex', new THREE.BufferAttribute(si, 4));
  g.setAttribute('skinWeight', new THREE.BufferAttribute(sw, 4));
  g.setIndex(new THREE.BufferAttribute(idx, 1));
  if (want[0][0].nrm) g.setAttribute('normal', new THREE.BufferAttribute(nrm, 3));
  else g.computeVertexNormals();
  return { geo: g, mats: mats, flats: flats };
}

// Textures on/off (key K, the Skins button). Off = the readable look: clothes
// in the team colour, skin in the face's tone, gear dark with a team tint.
var NAMES_ON = true;
function setNames(on) {
  NAMES_ON = !!on;
  var h = $('hud');
  if (h) h.style.visibility = NAMES_ON ? '' : 'hidden';
  var d = $('animdiag');
  if (d) d.style.visibility = NAMES_ON ? '' : 'hidden';
  var b = $('b-names');
  if (b) b.dataset.on = NAMES_ON ? '1' : '0';
  try { updatePlayerCard(); } catch (e) {}
}

var TEX_ON = (function () {
  try { return localStorage.getItem('mgo2rc.skins') !== '0'; } catch (e) { return true; }
})();
function setSkins(on) {
  TEX_ON = !!on;
  try { localStorage.setItem('mgo2rc.skins', TEX_ON ? '1' : '0'); } catch (e) {}
  for (var i = 0; i < marks.length; i++) {
    var m = marks[i];
    if (m.skin && m.gearMats) m.skin.material = TEX_ON ? m.gearMats : m.gearFlats;
  }
  var b = $('b-skins');
  if (b) b.dataset.on = TEX_ON ? '1' : '0';
}

// ?sex=male loads the male rig (figure_skinned_male.json + anims_male.json);
// default stays the female figure the page was built around, unless the build
// says otherwise (build_site.py --sex sets window.__MGO2_SEX__; the hosted
// build defaults to male because that set has an idle clip and female's doesn't).
function figureSex() {
  var dflt = window.__MGO2_SEX__ === 'male' ? 'male' : 'female';
  try {
    var v = new URLSearchParams(location.search).get('sex');
    return (v === 'male' || v === 'female') ? v : dflt;
  } catch (e) { return dflt; }
}

function loadSkinned() { loadRig(figureSex()); }

function loadRig(sex) {
  if (RIG_REQ[sex]) return;
  RIG_REQ[sex] = true;
  Promise.all([
    fetch(av(sex === 'male' ? 'figure_skinned_male.json' : 'figure_skinned.json'))
      .then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; }),
    fetch(av(animsFileFor(sex))).then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; }),
    // Optional: without it every player of this sex wears the default figure.
    fetch(av('gear_' + sex + '.json')).then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; }),
    // Optional: the game's colour variant table. Without it every item shows
    // its base texture whatever colour the player picked.
    fetch(av('variants_' + sex + '.json')).then(function (r) { return r.ok ? r.json() : null; })
      .catch(function () { return null; }),
    new Promise(function (ok) {
      if (TEX_INDEX) return ok();
      TEX_WAIT.push(ok);
      if (TEX_WAIT.length === 1) loadTexIndex();
    }),
    EMB_INDEX ? null : loadEmblemIndex()
  ]).then(function (res) {
    var F = res[0], A = res[1], GP = res[2];
    VARIANTS[sex] = res[3] || {};
    if (!F || !F.pos || !F.bone) {
      animDiag('SKIN OFF - figure_skinned.json missing or malformed\n' +
               'falling back to the rigid print meshes');
      return;
    }
    if (!A || !A.clips || !A.clips.length) {
      animDiag('SKIN OFF - anims.json has no clips\n' +
               'falling back to the rigid print meshes');
      return;
    }
    // global bone index -> slot in the clip's bone list
    var slot = {};
    for (var i = 0; i < A.bones.length; i++) slot[A.bones[i]] = i;
    var n = F.verts;
    var pos = new Float32Array(F.pos.length);
    for (i = 0; i < F.pos.length; i++) pos[i] = F.pos[i];      // stays in mm
    var si = new Uint16Array(n * 4), sw = new Float32Array(n * 4);
    var unbound = 0;
    for (i = 0; i < n; i++) {
      var s = slot[F.bone[i]];
      if (s === undefined) { s = 0; unbound++; }
      si[i * 4] = s;
      sw[i * 4] = 1;                    // one bone per vertex, full weight
    }
    if (unbound) console.warn('skinned figure: ' + unbound + ' vertices bound to a bone the clips do not carry');
    var g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('skinIndex', new THREE.BufferAttribute(si, 4));
    g.setAttribute('skinWeight', new THREE.BufferAttribute(sw, 4));
    g.setIndex(F.idx);
    g.computeVertexNormals();
    // Lift so the BIND pose's lowest vertex sits on the ground. Clips are
    // stored relative to the pelvis, so this offset holds for all of them.
    var lo = Infinity;
    for (i = 1; i < pos.length; i += 3) if (pos[i] < lo) lo = pos[i];
    var lift = -lo * MM;

    var clips = {};
    for (i = 0; i < A.clips.length; i++) {
      var c = A.clips[i];
      // speed and loop MUST survive the load: without them every clip runs on
      // wall-clock time and wraps, so feet skate and one-shot moves (throw,
      // roll, landings, climb) loop instead of playing once.
      clips[c.name] = { frames: c.frames, dur: c.dur, speed: c.speed || 0,
                        loop: c.loop !== false,
                        q: Int16Array.from(c.q), t: Int16Array.from(c.t) };
    }
    var anim = { fps: A.fps, bones: A.bones, rest: A.rest, clips: clips };
    // Per-clip ground offset. One global lift cannot serve a standing body and
    // a lying one: measured against the standing bind pose, crouch floated
    // 499 mm, prone 654 and crawl 797. Each clip is grounded on its own
    // lowest skinned vertex instead.
    measureLifts(F, A, clips);
    // The head and everything hanging off it, as clip-slot indices. Needed so
    // the aim-driven head turn can carry the face bones with it instead of
    // swivelling a skull out of a stationary head.
    var parentOf = {};
    for (i = 0; i < F.bones.length; i++) parentOf[F.bones[i].i] = F.bones[i].p;
    var kin = [];
    for (i = 0; i < A.bones.length; i++) {
      var g2 = A.bones[i], hops = 0;
      while (g2 != null && g2 >= 0 && hops++ < 64) {
        if (g2 === HEAD_BONE) { kin.push(i); break; }
        g2 = parentOf[g2];
      }
    }
    // The same walk for the SPINE. Bone 1 is the only child of the root that
    // is not a leg (root 0 -> {1 spine, 13 right leg, 17 left leg}), so its
    // subtree is exactly the upper body: turning it aims the torso and leaves
    // the stance planted.
    var spineKin = [];
    for (i = 0; i < A.bones.length; i++) {
      var g3 = A.bones[i], hops3 = 0;
      while (g3 != null && g3 >= 0 && hops3++ < 64) {
        if (g3 === SPINE_BONE) { spineKin.push(i); break; }
        g3 = parentOf[g3];
      }
    }
    // Gear binds to bones the clips do not all carry (the female set only has
    // the 57 the default mesh uses). Such a vertex rides its nearest ancestor
    // that IS animated, rather than sticking to the root.
    var slotAnc = {};
    for (i = 0; i < F.bones.length; i++) {
      var ab = F.bones[i].i, hopsA = 0;
      while (ab != null && ab >= 0 && slot[ab] === undefined && hopsA++ < 64) ab = parentOf[ab];
      if (ab != null && ab >= 0 && slot[ab] !== undefined) slotAnc[F.bones[i].i] = slot[ab];
    }
    var skin = { geo: g, slotOf: slot, slotAnc: slotAnc, headKin: kin, spineKin: spineKin,
             head: slot[HEAD_BONE], hand: slot[HAND_BONE], spine: slot[SPINE_BONE],
             hand2: slot[OFF_HAND_BONE], root: slot[0] };
    RIGS[sex] = { sex: sex, skin: skin, anim: anim, lift: lift,
                  gear: (GP && GP.parts) || null };
    useRig(RIGS[sex]);
    // EXTRA CLIPS, baked straight from the game's own animation files: the CQC
    // grab / hold / throw for both sides and a carry / prone / aim / reload set
    // per weapon. Optional and late -- every use goes through an
    // ANIM.clips[name] check, so until this lands the captured clips play.
    fetch(av('anims_extra_' + sex + '.json') + (ASSET_V ? '&' : '?') + 'x=23').then(function (r) { return r.ok ? r.json() : null; }).then(function (X) {
      if (!X || !X.clips || X.bones.length !== A.bones.length) return;
      X.clips.forEach(function (c) {
        clips[c.name] = { frames: c.frames, dur: c.dur, speed: 0, loop: c.loop !== false, fps: c.fps || 0,
                          liftF: c.liftF || null, q: Int16Array.from(c.q), t: Int16Array.from(c.t) };
      });
      measureLifts(F, { bones: A.bones, rest: A.rest, clips: X.clips }, clips);
      RIGS[sex].extra = X.clips.length;
    }).catch(function () {});
    // REAL RAGDOLL FALLS (2026-10-10, user: "should in all reality be a rag doll"): the game plays no death animation, it lets
    // go of the skeleton, and the replay carries no bones - so these are death falls RECORDED off the real game while it
    // played replays back (replaydiff/ragbake.py: flat ground, upright at death, 2.4 s, drop and slide kept). Not THE fall
    // of this death, but a real one, and a different one per death. Optional and late like the pack above.
    fetch(av('anims_rag_' + sex + '.json') + (ASSET_V ? '&' : '?') + 'x=2').then(function (r) { return r.ok ? r.json() : null; }).then(function (X) {
      if (!X || !X.clips || !X.clips.length || X.bones.length !== A.bones.length) return;
      X.clips.forEach(function (c) {
        clips[c.name] = { frames: c.frames, dur: c.dur, speed: 0, loop: false, fps: c.fps || 0, liftF: null,
                          q: Int16Array.from(c.q), t: Int16Array.from(c.t) };
      });
      measureLifts(F, { bones: A.bones, rest: A.rest, clips: X.clips }, clips);
      RIGS[sex].rag = X.clips.length;
      // which way each recorded fall goes in the body's own frame (deg, 0 = forward, 180 = backward) and how far (mm)
      RIGS[sex].ragMeta = X.clips.map(function (c) { return { n: c.name, dir: c.dir, slide: c.slide, up: !!c.faceUp }; });
    }).catch(function () {});
    // PHYSICS, PROOF OF CONCEPT (2026-10-10, user: "I think the physics is the correct fix since rag dolls are kinda random in
    // their own right across the replays anyways"). Falls SIMULATED from scratch (replaydiff/physbake.py: point masses at the
    // big joints, rigid bones, the game's own measured knee / elbow limits, gravity, the ground, the measured push), baked
    // to clips. Only with ?phys=1 until the look is agreed.
    if (PHYS_ON) fetch(av('anims_phys_' + sex + '.json') + (ASSET_V ? '&' : '?') + 'x=1').then(function (r) { return r.ok ? r.json() : null; }).then(function (X) {
      if (!X || !X.clips || !X.clips.length || X.bones.length !== A.bones.length) return;
      X.clips.forEach(function (c) {
        clips[c.name] = { frames: c.frames, dur: c.dur, speed: 0, loop: false, fps: c.fps || 0, liftF: null,
                          q: Int16Array.from(c.q), t: Int16Array.from(c.t) };
      });
      measureLifts(F, { bones: A.bones, rest: A.rest, clips: X.clips }, clips);
      RIGS[sex].physMeta = X.clips.map(function (c) { return { n: c.name, dir: c.dir, slide: c.slide, up: !!c.faceUp }; });
    }).catch(function () {});
    // BLAST PACK (2026-10-10, user: "can we just get a few different rag dolls?"): real knock-downs by an explosion, recorded
    // off the game like the death falls (replaydiff/blastbake.py) - bodies that were running, rolling or crouched when the
    // blast took them. Horizontal travel is taken out of each clip (the replay's own path carries the body); the rise and
    // fall of that blast stays in. Optional and late.
    fetch(av('anims_blast_' + sex + '.json') + (ASSET_V ? '&' : '?') + 'x=1').then(function (r) { return r.ok ? r.json() : null; }).then(function (X) {
      if (!X || !X.clips || !X.clips.length || X.bones.length !== A.bones.length) return;
      X.clips.forEach(function (c) {
        clips[c.name] = { frames: c.frames, dur: c.dur, speed: 0, loop: false, fps: c.fps || 0, liftF: null,
                          q: Int16Array.from(c.q), t: Int16Array.from(c.t) };
      });
      measureLifts(F, { bones: A.bones, rest: A.rest, clips: X.clips }, clips);
      RIGS[sex].blastMeta = X.clips.map(function (c) { return { n: c.name, dir: c.dir, slide: c.slide, up: !!c.faceUp, low: !!c.low }; });
    }).catch(function () {});
    animDiag('SKIN ON  ' + sex + '  ' + n + ' verts, ' + A.bones.length + ' bones' +
             (RIGS[sex].gear ? '  gear ' + Object.keys(RIGS[sex].gear).length : '  NO GEAR PACK') + '\n' +
             'clips: ' + A.clips.map(function (c) {
               return c.name + '(' + c.frames + 'f)'; }).join(' ') +
             (A.clips.length === 1
               ? '\nONLY ONE CLIP - every motionGroup falls back to it'
               : ''));
    if (SKIN.head === undefined) console.warn('skinned figure: no head bone ' + HEAD_BONE);
    for (i = 0; i < marks.length; i++) attachSkinned(marks[i]);
  });
}

// Lowest skinned vertex of each clip, relative to its root, in millimetres.
// Sampled rather than exhaustive: a gait's extremes show up within a handful of
// frames and this runs once per replay load.
function measureLifts(F, A, clips) {
  var slot = {}, i;
  for (i = 0; i < A.bones.length; i++) slot[A.bones[i]] = i;
  var nb = A.bones.length;
  var qx, qy, qz, qw, ix, iy, iz, iw, vx, vy, vz;
  for (var ci = 0; ci < A.clips.length; ci++) {
    var raw = A.clips[ci], cl = clips[raw.name];
    if (!cl) continue;
    var lo = Infinity;
    var step = Math.max(1, Math.floor(raw.frames / 10));
    for (var f = 0; f < raw.frames; f += step) {
      for (i = 0; i < F.verts; i += 4) {
        var s = slot[F.bone[i]];
        if (s === undefined) continue;
        var qo = (f * nb + s) * 4, to = (f * nb + s) * 3, r = A.rest[s];
        qx = cl.q[qo] / 32767; qy = cl.q[qo + 1] / 32767;
        qz = cl.q[qo + 2] / 32767; qw = cl.q[qo + 3] / 32767;
        vx = F.pos[i * 3] - r[0]; vy = F.pos[i * 3 + 1] - r[1]; vz = F.pos[i * 3 + 2] - r[2];
        ix = qw * vx + qy * vz - qz * vy;
        iy = qw * vy + qz * vx - qx * vz;
        iz = qw * vz + qx * vy - qy * vx;
        iw = -qx * vx - qy * vy - qz * vz;
        var y = iy * qw + iw * -qy + iz * -qx - ix * -qz;
        y += cl.t[to + 1];
        if (y < lo) lo = y;
      }
    }
    cl.lift = (lo === Infinity) ? null : -lo;
  }
}

function attachSkinned(m) {
  if (m.skin) return;
  var rig = RIGS[m.sex || figureSex()];
  if (!rig) { loadRig(m.sex || figureSex()); return; }   // re-attached when it lands
  useRig(rig);
  m.rig = rig;
  var gear = composeGeo(rig, m.ap, m.col, m.clanId);
  m.geared = !!gear;
  var bones = [], inv = [];
  var root = new THREE.Group();
  for (var i = 0; i < ANIM.bones.length; i++) {
    var r = ANIM.rest[i];
    var b = new THREE.Bone();
    b.position.set(r[0], r[1], r[2]);
    root.add(b);                       // flat: clips are already world-form
    bones.push(b);
    inv.push(new THREE.Matrix4().makeTranslation(-r[0], -r[1], -r[2]));
  }
  var mesh = gear
    ? new THREE.SkinnedMesh(gear.geo, TEX_ON ? gear.mats : gear.flats)
    : new THREE.SkinnedMesh(SKIN.geo, new THREE.MeshLambertMaterial({ color: m.col }));
  m.gearMats = gear ? gear.mats : null;
  m.gearFlats = gear ? gear.flats : null;
  mesh.add(root);
  // The identity bindMatrix is REQUIRED, not cosmetic. bind(skeleton) with no
  // second argument calls skeleton.calculateInverses(), which overwrites
  // boneInverses with inverse(bone.matrixWorld) and throws away the
  // translate(-rest) inverses that make this rig correct.
  //
  // With an explicit identity bindMatrix and the default 'attached' bindMode,
  // three.js recomputes bindMatrixInverse = inverse(mesh.matrixWorld) every
  // frame, so the shader's
  //     mesh.matrixWorld . bindMatrixInverse . boneWorld . boneInverse . v
  // collapses to exactly
  //     mesh.matrixWorld . boneLocal . translate(-rest) . v
  // which is the skinning equation this whole pipeline is built on.
  mesh.bind(new THREE.Skeleton(bones, inv), new THREE.Matrix4());
  mesh.scale.setScalar(MM);
  mesh.position.y = SKIN_LIFT;
  // Bones move far outside the bind bounding sphere, so let the skin draw
  // regardless of where the rest pose says it is.
  mesh.frustumCulled = false;
  m.dir.add(mesh);
  m.skin = mesh;
  m.bones = bones;
  // the held weapon rides the hand bone. Bone space is millimetres and the
  // weapon meshes are exported in millimetres about their own grip origin, so
  // this needs no offset and no scale of its own.
  if (SKIN.hand !== undefined && bones[SKIN.hand]) {
    var hp = new THREE.Mesh(propGeos().gun,
      new THREE.MeshLambertMaterial({ color: 0x23261f }));
    hp.visible = false;
    hp.frustumCulled = false;
    // The weapon's own axes are not the hand bone's axes, so the mesh needs a
    // fixed rotation into grip space. MEASURED, not guessed: while running or
    // walking, both hands are on the weapon, so the vector from the grip hand
    // (bone 8) to the support hand (bone 12) IS the barrel line -- and in the
    // grip hand's own frame that vector is constant to sd 0.000 across the run
    // take, identical in the walk take: (-0.1415, -0.7691, -0.6233). Roll comes
    // from world-up carried into the same frame. The resulting basis has
    // det 1.000.
    // The barrel is the OPPOSITE end from the one the hand-to-hand vector
    // points at: grip -> support hand runs toward the STOCK here, not the
    // muzzle, so the axis is negated. Confirmed by eye against the game --
    // an idle carry points the muzzle down toward the ground, not up.
    // Bone 8 is confirmed the RIGHT hand by the throw capture: it peaks at
    // 23.27 m/s against bone 12's 11.30, and the throwing hand is the right
    // one. (The +X-is-left coordinate argument said the opposite and was
    // wrong -- the measurement wins.)
    hp.quaternion.set(-0.128455, 0.414534, 0.798092, 0.417983);
    if (!WEAPON_Q0) {
      WEAPON_Q0 = hp.quaternion.clone();
      _wq = new THREE.Quaternion();
      _wv = new THREE.Vector3();
      _wb = new THREE.Vector3();
      _wb2 = new THREE.Vector3();
      _wv2 = new THREE.Vector3();
    }
    bones[SKIN.hand].add(hp);
    m.handProp = hp;
  }
  // a per-player phase so a squad standing together does not breathe in unison
  m.phase = Math.random() * 4;
  if (m.fig) m.fig.visible = false;
  attachProp(m);
  ['legL', 'legR', 'torso', 'armL', 'armR', 'head'].forEach(function (k) {
    if (m[k]) m[k].visible = false;
  });
  if (m.cone) m.cone.visible = false;
}

// motionGroup (contract slot 21) is the field the format documentation says to
// drive animation from -- it is the decoder's own 17-group table, not a guess
// off speed. Each entry falls back down the chain until a captured clip exists,
// so a partial capture set degrades to the nearest thing rather than a T-pose.
var CLIP_FOR = {
  'standing':                 ['idle', 'aim'],
  'aiming, still':            ['aim', 'idle'],
  'walking':                  ['walk', 'run', 'idle'],
  'walking backwards':        ['walkback', 'walk', 'idle'],
  'running':                  ['run', 'walk', 'idle'],
  // MEASURED: routing 'descending' to the climb clip made 58% of all climb
  // playbacks false. motionState 18 "descend" is DROPPING OFF something, not
  // ladder work -- it is 12x more common than real climbing (635 samples vs
  // 51), and it is the state during the fall after rolling off a ledge. With
  // no fall capture yet, a neutral upright pose is the least wrong thing to
  // show while someone drops; a ladder ascent is actively wrong.
  'airborne':                 ['fall', 'idle'],
  'climbing up':              ['climb', 'idle'],
  // MEASURED 2026-10-09 on the real game: the descend state plays main 178, a short drop (15 of 23 episodes, ~30 mm).
  'descending':               ['drop', 'fall', 'idle'],
  // A ledge hang is NOT a ladder ascent. Captured separately: both grabs in
  // the session arrested at an identical rootY 3.12 m with hands 0.51 m up.
  // Routing it to the climb clip made 37% of all climb playbacks false.
  'holding a wall or ledge':  ['ledge', 'climb', 'idle'],
  'in water':                 ['walk', 'idle'],
  'hit reaction':             ['hit', 'idle'],
  // `prone` closes these chains for a set with no dead/down clip (female): a
  // body lying flat is far less wrong than one standing up while dead.
  'blown back':               ['down', 'dead', 'prone', 'idle'],
  'down on the ground':       ['down', 'dead', 'prone', 'idle'],
  'getting up':               ['down', 'idle'],
  'incapacitated':            ['down', 'dead', 'prone', 'idle'],
  // 'choking' is a player COUGHING IN SMOKE, on his feet (measured 2026-10-08: caused by a smoke grenade, upright in 54 of
  // 73 episodes, about 4 s). It was drawn with the lying `down` clip. `choke` = the game's own cough (main set 115).
  'choking':                  ['choke', 'idle'],
  // MEASURED 2026-10-10 (user phone screenshot: a dead player standing, ghosted): the clip NAMED `dead` is the old authored
  // stand-in - hips at standing height (75 of 75), only the head dropped (77 of 114) - in BOTH sets, so every death ended
  // as a bent-over standing ghost. The game's own held lying poses are `sleepBack` (main 98, on the back: head 9, hips 9)
  // and `sleep` (main 97, face down); `dead` stays only as the last resort before standing.
  'dead':                     ['sleepBack', 'sleep', 'down', 'prone', 'dead', 'idle']
};

// ?clip=NAME forces every player onto one clip. Used to show `rigtest`, the
// exaggerated synthetic clip, because the only real capture so far moves a
// vertex 40 mm over five seconds -- 1.3 px on screen -- so a working rig and a
// broken one look exactly the same without it.
var FORCE_CLIP = (function () {
  try {
    return new URLSearchParams(location.search).get('clip');
  } catch (e) { return null; }
})();

// ---- ROLL SMOOTHING -----------------------------------------------------
// A roll is motionState 2 held 19-20 frames (one roll) or 40-44 (two chained),
// covering 3.65 m +/- 6% at 5.4 m/s -- faster than running. The engine does not
// stream its motion per tick, only the endpoints, so the raw stream contains
// two ~1.5 m discontinuities per roll. 81% of all large position jumps in a
// match are exactly this, which is why players appeared to skate and to be
// yanked around.
//
// Each discontinuity is spread across neighbouring frames. Total displacement
// is preserved: only the intermediate samples move, so a player still arrives
// where the game put them.
var ROLL_SMOOTH = true;
var ROLL_RAMP = 7;            // frames to spread a jump over
var ROLL_JUMP = 700;          // world units; below this there is nothing to fix

// Roll geometry, measured rather than assumed. Both formats agree on it.
var ROLL_MIN_S = 0.45, ROLL_MAX_S = 0.95;      // 0.700 s mean, sd 0.032
var ROLL_MIN_MM = 3000, ROLL_MAX_MM = 4400;    // 3664 mm mean, CV 6.9%

// THE GAME'S OWN ROLL RECORDS (2026-10-10, doos: a roll is not a hole in the recording, it is a run of 39-byte position rows
// with state 2 = forward roll, 3 = side roll). Measured on Groznyj Grad Base: 111 recorded rolls against 93 the gap rule
// below found - every guess was a real roll, and 18 real ones were missed. Runs per player: [t0, t1, state].
var ROLL_SRC = null;
function rollRuns(rep) {
  if (rep.__rollRuns) return rep.__rollRuns;
  var out = rep.__rollRuns = [], cur = {};
  (rep.positionVariants || []).forEach(function (v) {
    if (v.len !== 39 || v.player == null) return;
    var c = cur[v.player];
    if (c && v.t - c[1] < 0.25 && c[2] === v.motionState) { c[1] = v.t; return; }
    cur[v.player] = c = [v.t, v.t, v.motionState];
    (out[v.player] || (out[v.player] = [])).push(c);
  });
  return out;
}
function rollWindows(frames, i) {
  var out = [], n = frames.length;
  function cell(k) {
    var row = frames[k] && frames[k].p;
    var e = row ? row[i] : null;
    return (e && e[0] != null) ? e : null;
  }

  // (a) GAP rolls. The stream goes silent for the whole move, so the roll is
  //     the empty span between two consecutive samples. 96% of gaps in that
  //     duration band are rolls, so the distance test is what does the work.
  var prev = -1;
  for (var k = 0; k < n; k++) {
    var e = cell(k);
    if (!e) continue;
    if (prev >= 0 && k - prev > 1) {
      var a = cell(prev);
      var dt = frames[k].t - frames[prev].t;
      if (dt >= ROLL_MIN_S && dt <= ROLL_MAX_S && a[4] && e[4]) {
        var dx = e[0] - a[0], dz = e[2] - a[2];
        var d = Math.sqrt(dx * dx + dz * dz);
        if (d >= ROLL_MIN_MM && d <= ROLL_MAX_MM) out.push([prev, k]);
      }
    }
    prev = k;
  }

  // (b) FRAME-RUN rolls: the older files DO stream samples through the move,
  //     as a run of motionState 2 far too fast to be the walk that state also
  //     covers. Kept because both encodings appear in the corpus.
  var f = 0;
  while (f < n) {
    var c = cell(f);
    if (!c || c.length < 9 || c[8] !== 2) { f++; continue; }
    var a0 = f;
    while (f + 1 < n) { var g = cell(f + 1); if (!g || g.length < 9 || g[8] !== 2) break; f++; }
    var len = f - a0 + 1;
    // Duration alone is not enough: a 19-frame state-2 run that covers 730 mm
    // is somebody walking, not a roll. The distance is the discriminator in
    // both encodings, so apply it here too.
    if ((len >= 19 && len <= 20) || (len >= 40 && len <= 44)) {
      var s0 = cell(a0), s1 = cell(f);
      if (s0 && s1) {
        var rdx = s1[0] - s0[0], rdz = s1[2] - s0[2];
        var rd = Math.sqrt(rdx * rdx + rdz * rdz);
        var floor = len >= 40 ? ROLL_MIN_MM * 1.7 : ROLL_MIN_MM;
        if (rd >= floor) out.push([a0, f]);
      }
    }
    f++;
  }

  // (c) RECORDED rolls the two rules above missed: forward rolls of 0.3 s and more (shorter ones were cut off; the side
  //     roll, state 3, has no clip here yet).
  var rep_ = ROLL_SRC || R, rr_ = (rep_ && rep_.frames === frames) ? rollRuns(rep_)[i] : null;
  if (rr_) rr_.forEach(function (q) {
    if ((q[2] !== 2 && q[2] !== 3) || q[1] - q[0] < (q[2] === 3 ? 0.5 : 0.3)) return;
    var lo = 0, hi = n - 1;
    while (lo < hi) { var md = (lo + hi) >> 1; if (frames[md].t < q[0]) lo = md + 1; else hi = md; }
    var a_ = lo - 1, b_ = lo;
    while (a_ >= 0 && !cell(a_) && q[0] - frames[a_].t < 1.5) a_--;
    while (b_ < n && (frames[b_].t <= q[1] || !cell(b_)) && frames[b_].t - q[1] < 1.5) b_++;
    if (a_ < 0 || b_ >= n || !cell(a_) || !cell(b_) || !cell(a_)[4] || !cell(b_)[4]) return;
    if (out.some(function (w) { return w[0] <= b_ && w[1] >= a_; })) return;
    out.push(q[2] === 3 ? [a_, b_, 3] : [a_, b_]);                              // third number 3 = a SIDE roll
  });
  out.sort(function (x, y) { return x[0] - y[0]; });
  return out;
}

// Spread one discontinuity at `at` over the frames before it, so the step
// becomes a ramp rather than a jump.
function smearBack(frames, i, at, dx, dz, ramp) {
  for (var k = 1; k <= ramp; k++) {
    var idx = at - k;
    if (idx < 0) break;
    var row = frames[idx] && frames[idx].p;
    var e = row ? row[i] : null;
    if (!e || e[0] == null) break;
    var w = 1 - k / (ramp + 1);
    e[0] += dx * w;
    e[2] += dz * w;
  }
}

function smearFwd(frames, i, at, dx, dz, ramp) {
  for (var k = 0; k < ramp; k++) {
    var idx = at + k;
    if (idx >= frames.length) break;
    var row = frames[idx] && frames[idx].p;
    var e = row ? row[i] : null;
    if (!e || e[0] == null) break;
    var w = 1 - (k + 1) / (ramp + 1);
    e[0] += dx * w;
    e[2] += dz * w;
  }
}

// Metres travelled by each player up to each frame. Built once so playback can
// key a stride to ground covered instead of to the clock.
function buildDistance(r) {
  if (!r || !r.frames) return;
  var frames = r.frames, npl = (r.players || []).length;
  r.cumDist = [];
  for (var i = 0; i < npl; i++) {
    var arr = new Float64Array(frames.length);
    var d = 0, px = null, pz = null;
    for (var f = 0; f < frames.length; f++) {
      var row = frames[f] && frames[f].p;
      var e = row ? row[i] : null;
      if (e && e[0] != null) {
        if (px != null) {
          var dx = e[0] - px, dz = e[2] - pz;
          var step = Math.sqrt(dx * dx + dz * dz) / 1000;   // world units -> m
          // A respawn teleport is not travel; it must not spin the legs.
          if (step < 3) d += step;
        }
        px = e[0]; pz = e[2];
      }
      arr[f] = d;
    }
    r.cumDist[i] = arr;
  }
}

// Distance travelled at a fractional frame, so the stride is smooth.
function distAt(pi, fi) {
  if (!R || !R.cumDist || !R.cumDist[pi]) return null;
  var a = R.cumDist[pi];
  var i0 = Math.floor(fi);
  if (i0 < 0) return a[0];
  if (i0 >= a.length - 1) return a[a.length - 1];
  var u = fi - i0;
  return a[i0] + (a[i0 + 1] - a[i0]) * u;
}

// Prone is a -400 world-unit drop in a player's own Y track, not a motionState
// (see _prone.py for the measurement). Steps of exactly +/-400 bracket it; all
// other steps in a real track are 10-130, so the size alone identifies it.
var PRONE_DROP = 400, PRONE_TOL = 40;

function buildProne(r) {
  if (!r || !r.frames) return;
  var frames = r.frames, npl = (r.players || []).length;
  var CELL = 2500, DROP = 400, TOL = 40, MIN_OTHERS = 8;
  // cell -> rounded height -> per-player sample counts
  var cells = {};
  var f, i, e, row, k, m, per, y;
  for (f = 0; f < frames.length; f++) {
    row = frames[f] && frames[f].p;
    if (!row) continue;
    for (i = 0; i < npl; i++) {
      e = row[i];
      if (!e || e[0] == null) continue;
      k = Math.round(e[0] / CELL) + ',' + Math.round(e[2] / CELL);
      m = cells[k] || (cells[k] = {});
      y = Math.round(e[1] / 10) * 10;
      per = m[y] || (m[y] = new Int32Array(npl));
      per[i]++;
    }
  }
  // floor of a cell, ignoring one player's own contribution
  function floorFor(key, skip) {
    var mm = cells[key];
    if (!mm) return null;
    var best = null, bc = 0;
    for (var yy in mm) {
      var arr = mm[yy], c = 0;
      for (var j = 0; j < npl; j++) if (j !== skip) c += arr[j];
      if (c > bc) { bc = c; best = +yy; }
    }
    return bc >= MIN_OTHERS ? best : null;
  }
  // THE -400 DROP IS TWO POSTURES, NOT ONE.
  // It was read as prone alone. It is not: the same drop happens when a player
  // CROUCHES, and what separates them is the motionState the player is in
  // while they are low. Measured over 8 files, restricted to no horizontal
  // movement: 96 crouch entries whose state stays upright, against a separate
  // population whose state is `downed, lying on the ground`. Magnitudes are the
  // same encoding either way -- 91 of 96 land between -380 and -410.
  //
  // This is why crouching players' feet clipped through the floor: the drop was
  // in the data, the body was still drawn standing, and nothing put it back.
  //
  // `descend` and `airborne` are deliberately in NEITHER set. That is real
  // terrain, and it is also where deploying a cardboard box shows up, so a
  // posture read off those states would be wrong in two different ways.
  var MS_LOW = { 6: 1, 26: 1, 27: 1, 28: 1, 25: 1 };            // genuinely down
  var MS_UP = { 0: 1, 1: 1, 2: 1, 5: 1, 40: 1, 41: 1,
                129: 1, 168: 1, 169: 1 };                        // on their feet
  r.prone = [];
  r.crouch = [];
  var total = 0, ctotal = 0;
  for (i = 0; i < npl; i++) {
    var flag = new Uint8Array(frames.length);
    var cflag = new Uint8Array(frames.length);
    // HIS OWN DROP, for cells where nobody else ever stood (no floor consensus): VAMP crawling through a cut-out at 2:08
    // of match 52613 was drawn running upright because that corridor had no floor. Going low is a step of DROP between
    // two samples under 0.6 s and 1.5 m apart; he is up again when his height is back where it was.
    var selfLow_ = false, selfBase_ = 0, selfT_ = 0, pe_ = null, pt_ = 0;
    for (f = 0; f < frames.length; f++) {
      row = frames[f] && frames[f].p;
      e = row ? row[i] : null;
      if (!e || e[0] == null) continue;
      var fl = floorFor(Math.round(e[0] / CELL) + ',' + Math.round(e[2] / CELL), i);
      if (pe_ && frames[f].t - pt_ < 0.6 && Math.abs(e[0] - pe_[0]) < 1500 && Math.abs(e[2] - pe_[2]) < 1500) {
        var sd_ = pe_[1] - e[1];
        if (!selfLow_ && sd_ >= DROP - TOL && sd_ <= DROP + TOL) { selfLow_ = true; selfBase_ = pe_[1]; selfT_ = frames[f].t; }
      }
      if (selfLow_ && (Math.abs(e[1] - selfBase_) <= TOL || Math.abs(selfBase_ - e[1] - DROP) > 2 * TOL || !e[4])) selfLow_ = false;
      // nobody moves at running pace while low: a real 40 cm step down (a kerb, a ledge) ends here
      // (not in the first second: a dive to prone from a run arrives fast)
      if (selfLow_ && pe_ && frames[f].t - selfT_ > 1.0 && frames[f].t > pt_ && frames[f].t - pt_ < 0.6 &&
          Math.sqrt((e[0] - pe_[0]) * (e[0] - pe_[0]) + (e[2] - pe_[2]) * (e[2] - pe_[2])) / 1000 / (frames[f].t - pt_) > 3.0) selfLow_ = false;
      pe_ = e; pt_ = frames[f].t;
      // Also where the cell's floor IS known but sits at his lowered height: in a crawl-only gap the only samples are
      // other crawlers, so the consensus floor is 40 cm too low and he reads as standing on it.
      // ... and where the consensus is another STOREY: this corridor's cell reads 7990 because most samples in it are on
      // the roof above. His own step down is the better witness in every case, so it wins whenever it is set.
      if (selfLow_) fl = e[1] + DROP;
      // POSTURE FROM THE FLAG BYTE, CHECKED AGAINST THE REAL SKELETON (2026-10-10; doos's reading of 440,000 rows, then our own
      // recordings: pelvis height over the feet in 8 matches). State 0 (not moving): top three bits 1 = standing (1076 mm,
      // 16,339 samples), 2 = CROUCHED (528 mm, 4,171 - and NO height step in the recording), 3 / 4 = LYING (165 / 176 mm, with the
      // 40 cm step). Moving (states 1 / 129): 2 = upright run (974), 3 = crouched run (887), 4 = crawl (333), 7 = crouched with
      // the gun up (871). State 5 (aiming still): 6 = standing (1054), 7 = crouched (538). State 11 (wall cover): 2 = crouched (512).
      // The old rule read the 40 cm step with an "upright" state as a crouch, which drew every lying-still player crouching
      // and never drew a real crouch at all. It stays only for states the table does not cover and for files with no flag byte.
      var fms_ = e.length > 8 ? e[8] : null, ft3_ = (e.length > 7 && e[7] != null) ? e[7] >> 5 : null;
      if (ft3_ != null && (fms_ === 0 || fms_ === 1 || fms_ === 129 || fms_ === 5 || fms_ === 11)) {
        if ((fms_ === 0 && (ft3_ === 3 || ft3_ === 4)) || ((fms_ === 1 || fms_ === 129) && ft3_ === 4)) { flag[f] = 1; total++; }
        else if ((fms_ === 0 && ft3_ === 2) || ((fms_ === 1 || fms_ === 129) && (ft3_ === 3 || ft3_ === 7)) ||
                 (fms_ === 5 && ft3_ === 7) || (fms_ === 11 && ft3_ === 2)) { cflag[f] = 1; ctotal++; }
      } else if (fl != null) {
        var d = fl - e[1];
        if (d >= DROP - TOL && d <= DROP + TOL) {
          var ms_ = e.length > 8 ? e[8] : null;
          // LYING OR CROUCHED, FROM THE FLAG BYTE (2026-10-10, doos's table of 440,000 rows + Groznyj Grad Base at 0:26, where a
          // player blown over lay still, crawled and got up but was drawn crouching and walking upright): for a body that is
          // not moving (state 0) the flag byte's top three bits read 2 = crouched, 3 = lying, 4 = lying after a knock-down;
          // for one that is moving (states 1 / 129) 3 = crouched run, 4 = crawling. Only these clear cases override.
          var t3_ = (e.length > 7 && e[7] != null) ? e[7] >> 5 : null;
          var lie_ = t3_ != null && ((ms_ === 0 && (t3_ === 3 || t3_ === 4)) || ((ms_ === 1 || ms_ === 129) && t3_ === 4));
          var cro_ = t3_ != null && ((ms_ === 0 && t3_ === 2) || ((ms_ === 1 || ms_ === 129) && t3_ === 3));
          if (lie_) { flag[f] = 1; total++; }
          else if (cro_) { cflag[f] = 1; ctotal++; }
          else if (ms_ != null && MS_UP[ms_]) { cflag[f] = 1; ctotal++; }
          else if (ms_ == null || MS_LOW[ms_]) { flag[f] = 1; total++; }
        }
      }
    }
    r.prone[i] = closePostureGaps(flag, frames);
    r.crouch[i] = closePostureGaps(cflag, frames);
    // A LOW RUN THAT MOVES AT CRAWLING SPEED IS PRONE, NOT A CROUCH (user, 2026-10-08: VAMP at 2:08 of match 52613 crawls
    // through a cut-out and was drawn crouch-walking). The movement state stays upright ("run forward") while a player
    // crawls, so the state test above files every voluntary prone under crouch. Speed separates them: over 29 moving low
    // runs in 5 matches the median speed falls in two groups, 0.3 - 1.25 m/s (21 runs: crawling) and 1.5 m/s up (8 runs:
    // crouch-walking and faster), with one run between. A low run that never moves cannot be told apart and stays a crouch.
    var cr_ = r.crouch[i], pr_ = r.prone[i], f0_ = 0;
    while (f0_ < frames.length) {
      if (!cr_[f0_]) { f0_++; continue; }
      var f1_ = f0_, sp_ = [], pp_ = null;
      while (f1_ < frames.length && cr_[f1_]) {
        var ce_ = frames[f1_].p ? frames[f1_].p[i] : null;
        if (ce_ && ce_[0] != null) {
          if (pp_ && frames[f1_].t - pp_[0] > 0.12 && frames[f1_].t - pp_[0] < 0.6) {
            var v_ = Math.sqrt((ce_[0] - pp_[1]) * (ce_[0] - pp_[1]) + (ce_[2] - pp_[2]) * (ce_[2] - pp_[2])) / 1000 / (frames[f1_].t - pp_[0]);
            if (v_ > 0.3) sp_.push(v_);
            pp_ = [frames[f1_].t, ce_[0], ce_[2]];
          }
          if (!pp_) pp_ = [frames[f1_].t, ce_[0], ce_[2]];
        }
        f1_++;
      }
      if (sp_.length >= 4) {
        sp_.sort(function (x, y) { return x - y; });
        if (sp_[sp_.length >> 1] < 1.25) { for (var fz_ = f0_; fz_ < f1_; fz_++) { cr_[fz_] = 0; pr_[fz_] = 1; } r.crawlRuns = (r.crawlRuns || 0) + 1; }
      }
      f0_ = f1_;
    }
  }
  r.proneStats = { frames: total, crouchFrames: ctotal };
}

// A posture is held; the DETECTION of it is not continuous. The floor of a map
// cell is a consensus of other players' samples, so it goes unavailable
// whenever a cell is thinly populated, and the raw flag drops out for a few
// frames at a time. Left raw, the body pops upright and back -- reported as
// "it keeps flashing him standing".
// Nobody crouches for a tenth of a second and nobody stands up for one either,
// so close short holes and drop short runs. Both bounds are in TIME, because
// frames are change-triggered and their spacing is not uniform.
var POSTURE_GAP_S = 0.60, POSTURE_MIN_S = 0.15;
function closePostureGaps(flag, frames) {
  var n = flag.length, i, j;
  // fill holes shorter than POSTURE_GAP_S that have the posture on both sides
  i = 0;
  while (i < n) {
    if (flag[i]) { i++; continue; }
    j = i;
    while (j < n && !flag[j]) j++;
    if (i > 0 && j < n && frames[j] && frames[i - 1] &&
        (frames[j].t - frames[i - 1].t) <= POSTURE_GAP_S) {
      for (var k = i; k < j; k++) flag[k] = 1;
    }
    i = j + 1;
  }
  // then discard runs too short to be a real posture
  i = 0;
  while (i < n) {
    if (!flag[i]) { i++; continue; }
    j = i;
    while (j < n && flag[j]) j++;
    // End-EXCLUSIVE: a run covering frames [i, j) lasts until frames[j], not
    // until frames[j-1]. Measuring to j-1 under-stated every run by one gap
    // (median 34 ms) and discarded 137 borderline runs, ~2.1% of flagged frames.
    var runEnd = (j < n && frames[j]) ? frames[j].t : (frames[j - 1] && frames[j - 1].t);
    if (runEnd != null && frames[i] && (runEnd - frames[i].t) < POSTURE_MIN_S) {
      for (var k2 = i; k2 < j; k2++) flag[k2] = 0;
    }
    i = j;
  }
  return flag;
}

function proneAt(pi, fi) {
  if (!R || !R.prone || !R.prone[pi]) return false;
  var a = R.prone[pi];
  var k = Math.round(fi);
  if (k < 0) k = 0;
  if (k >= a.length) k = a.length - 1;
  return !!a[k];
}

function crouchAt(pi, fi) {
  if (live && live[pi] && live[pi].rowCrouch != null) return live[pi].rowCrouch;
  if (!R || !R.crouch || !R.crouch[pi]) return false;
  var a = R.crouch[pi];
  var k = Math.round(fi);
  if (k < 0) k = 0;
  if (k >= a.length) k = a.length - 1;
  return !!a[k];
}

function smoothRolls(r) {
  if (!ROLL_SMOOTH || !r || !r.frames) return;
  var frames = r.frames, npl = (r.players || []).length, wins = 0, fixed = 0;
  for (var i = 0; i < npl; i++) {
    ROLL_SRC = r; var ws = rollWindows(frames, i);
    for (var w = 0; w < ws.length; w++) {
      var a = ws[w][0], b = ws[w][1];
      wins++;
      // A gap roll carries its whole displacement between two real samples;
      // that distance is the move, not an artefact, so there is nothing to
      // smooth. Only the streamed kind has spurious endpoint snaps.
      var gapRoll = true;
      for (var q = a + 1; q < b; q++) {
        var rowq = frames[q] && frames[q].p;
        if (rowq && rowq[i] && rowq[i][0] != null) { gapRoll = false; break; }
      }
      if (gapRoll) { (r.rollSpans || (r.rollSpans = [])).push([i, a, b, ws[w][2] || 2]); continue; }
      // Keep the window: motionGroup reports "walking" through a roll, so this
      // is the ONLY way playback can know a roll is happening.
      (r.rollSpans || (r.rollSpans = [])).push([i, a, b, ws[w][2] || 2]);
      // entry: the step INTO the first roll frame
      var prev = null, cur = null, rowP, rowC;
      rowP = frames[a - 1] && frames[a - 1].p; prev = rowP ? rowP[i] : null;
      rowC = frames[a] && frames[a].p; cur = rowC ? rowC[i] : null;
      if (prev && cur && prev[0] != null && cur[0] != null) {
        var dx = cur[0] - prev[0], dz = cur[2] - prev[2];
        if (Math.sqrt(dx * dx + dz * dz) > ROLL_JUMP) {
          smearBack(frames, i, a, dx, dz, ROLL_RAMP);
          fixed++;
        }
      }
      // exit: the step OUT of the last roll frame
      var last = null, nxt = null;
      rowP = frames[b] && frames[b].p; last = rowP ? rowP[i] : null;
      rowC = frames[b + 1] && frames[b + 1].p; nxt = rowC ? rowC[i] : null;
      if (last && nxt && last[0] != null && nxt[0] != null) {
        var ex = nxt[0] - last[0], ez = nxt[2] - last[2];
        if (Math.sqrt(ex * ex + ez * ez) > ROLL_JUMP) {
          smearFwd(frames, i, b + 1, -ex, -ez, ROLL_RAMP);
          fixed++;
        }
      }
    }
  }
  // per-player, sorted, so playback can binary-search
  r.rollByPlayer = [];
  var sp = r.rollSpans || [];
  for (var q = 0; q < sp.length; q++) {
    (r.rollByPlayer[sp[q][0]] || (r.rollByPlayer[sp[q][0]] = [])).push([sp[q][1], sp[q][2], sp[q][3] || 2]);
  }
  for (q = 0; q < r.rollByPlayer.length; q++) {
    if (r.rollByPlayer[q]) r.rollByPlayer[q].sort(function (x, y) { return x[0] - y[0]; });
  }
  r.rollStats = { windows: wins, smoothed: fixed };
}

// WEAPON VARIANTS OF THE LOCOMOTION CLIPS.
// The body pose depends on what is in the hands, and the weapon MODEL being
// right does not fix it: a player jogging with a grenade out still read as
// carrying a rifle because `run` was captured holding one.
//
// Measured in the body frame over the 2026-08-23 captures:
//   rifle-run vs grenade-run   median 109 mm, p90 231
//   rifle-run vs pistol-run    median 111 mm, p90 230
//   grenade-run vs pistol-run  median   4 mm, p90  15   <- the same animation
// So SPRINTING has exactly two poses, rifle and everything-else, and one
// captured clip covers both. Walking genuinely differs per weapon (the grenade
// walk sits 418 mm from the rifle walk at p90, all of it on the arm bones).
//
// Only the THROWN set is wired. `propKind` already defines it exactly, whereas
// pulling pistols out of FIREARM would be a guess at which ids are pistols --
// and a plausible-looking mapping that was never counted is what put players
// on ladders where there were none.
// Handguns are their own class, NOT lumped in with grenades. The run POSE of
// the two is the same to 4 mm, but they diverge the moment the player acts --
// a pistol is fired from the carry and a grenade is thrown -- so they get
// separate names now rather than a shared clip that has to be split later.
// The set is the contiguous handgun block of the game's own item table
// (3 RUGER .. 15 GLOCK), by NAME, minus 12 COOLANT which is a spray.
var SECONDARY = { 2: 1, 3: 1, 4: 1, 5: 1, 6: 1, 7: 1, 8: 1, 9: 1, 10: 1, 11: 1,
                  13: 1, 14: 1, 15: 1 };
var NADE_CLIP   = { walk: 'walkNade',   run: 'runNade',   idle: 'idleNade' };
var PISTOL_CLIP = { walk: 'walkPistol', run: 'runPistol' };
function withWeapon(name, p) {
  if (!name || !p || !p.alive || p.item == null) return name;
  // idleNade is a real capture, not an inference: the arms of two separate
  // standing stretches in the take sat 151 mm and 130 mm from the measured
  // grenade hold and 367 mm / 431 mm from the rifle idle.
  // The same ammo test updateProp uses. Without it a player with 0 grenades
  // left keeps the grenade-hold pose while the model is hidden -- measured at
  // 187,363 samples, 8.6% of all living samples, miming a nade that is not
  // there. One rule, used by both, or they drift apart again.
  var kind_ = propKind(p.item);
  // REVERSED 2026-10-04 on the user's word for how the game behaves: with the
  // grenade slot selected and none left, the player still runs the grenade run
  // (empty-handed), not the rifle run. The early return that used to sit here
  // sent those samples to the rifle clips, so they ran cradling a rifle that is
  // not drawn. The model stays hidden at 0 (updateProp); only the pose changed.
  var map = kind_ === 'nade' ? NADE_CLIP
          : (SECONDARY[p.item] ? PISTOL_CLIP : null);
  if (map) {
    var v = map[name];
    if (v && ANIM.clips[v]) return v;
  }
  return name;
}

// PER-WEAPON POSES. Every weapon has its own small animation file in the game;
// the first three clips of each MEASURE as standing carry (hands low), prone
// hold and standing aim (hands at head height), and the fourth is the standing
// reload. Keyed by the replay's item id. A weapon with no file of its own that
// shares a pose family uses a relative's (measured: handguns share one pose).
// (The Mk.23 and GSR files' aim clip does not decode -- it bakes as a T-pose --
// so those two, and the handguns with no file at all, use the Operator's.)
var WPN_BANK = { 3: 'ruger', 4: 'oprt', 5: 'oprt', 6: 'denb', 7: 'oprt', 8: 'oprt', 9: 'denb', 10: 'thor',
                 11: 'oprt', 13: 'oprt', 14: 'oprt', 15: 'g18c',
                 18: 'mp5', 20: 'p90s', 22: 'patriot', 23: 'vz83', 24: 'm4', 25: 'ak', 26: 'g3a3',
                 27: 'xm8c', 30: 'scar', 31: 'xm8c', 35: 'm60e4', 38: 'saig', 39: 'vss',
                 42: 'm14ebr', 44: 'dragnov' };
// Files that carry no reload of their own borrow the nearest relative's.
var WPN_RELOAD = { m4: 'ak', denb: 'g18c', oprt: 'g18c', patriot: 'vz83', m14ebr: 'g3a3' };
function wpnClip(kind, p) {
  if (!p || p.item == null || !ANIM || FORCE_CLIP) return null;
  var b = WPN_BANK[p.item];
  if (!b) return null;
  if (kind === 'wr' && !ANIM.clips['wr_' + b]) b = WPN_RELOAD[b];
  var n = b ? kind + '_' + b : null;
  return n && ANIM.clips[n] ? n : null;
}
// RELOAD is action 0x0B (2,267 runs over 20 files, median 2.0 s). It is an
// upper-body move: 82% of reload samples are taken on the run, so the reload
// clip supplies the spine subtree and the legs stay on whatever gait is playing.
// SPRAYING COOLANT (user 2026-10-10: "This is spraying coolant. But the legs arent always moving"): actions 0x32 / 0x30, which
// only ever occur with the coolant can in hand. Like a reload it is an ARMS layer over whatever the legs are doing. The clip
// is the spray set's standing loop - the nearest the matcher got, not a confirmed match.
function sprayClip(p) {
  if (!p || !p.alive || (p.act !== 0x32 && p.act !== 0x30) || !ANIM.clips.spray) return null;
  var c = ANIM.clips.spray;
  return { n: 'spray', t: T % (c.frames / (c.fps || ANIM.fps)) };
}
function reloadClip(p, pi) {
  if (!p || !p.alive || p.act !== 0x0B || pi == null) return null;
  var n = wpnClip('wr', p);
  if (!n) return null;
  var st = entryRunStartT(pi, frameIndexAt(T), function (e) { return e.length > 14 && e[14] === 0x0B; });
  return { n: n, t: st != null ? Math.max(0, T - st) : 0 };
}
// CQC, read off the replay:
//   action 0x17  the reach (always first, 0.13 s, empty hands)
//   action 0x19  THROW -- its victim sits in motionState 0x33 for a fixed 1.68 s
//                and then goes airborne; the victim clip is 99 frames = 1.65 s
//   action 0x18  HOLD / choke -- the player next to it reads "choking"
// The "choking" state is shared with gas, so a held victim is only drawn as
// held when somebody within 1.5 m is actually holding.
var CQC_ACT = { 0x17: 1, 0x18: 1, 0x19: 1 };
// WHO THREW WHOM. The victim's state starts the moment he is taken; the
// attacker's own action byte only shows 1.43-1.73 s later (8 of 8 paired throws
// over 20 files), i.e. as the victim's 1.65 s is ending. Played from the action
// byte the attacker would start his throw after the body had already landed.
// So each throw is paired once, at load, and the attacker runs on the victim's
// clock: the attacker is whoever starts a reach 0.8-2.4 s after the victim's
// state begins, nearest within 3 m; failing that, whoever stood within 1.5 m.
function buildCqc(r) {
  var out = r.cqcThrows = [];
  var fr = r.frames || [], open = {}, reach = [], pa = {}, i, k, e, thrA = [], koS = [], pst = {};
  for (k = 0; k < fr.length; k++) {
    var row = fr[k].p;
    if (!row) continue;
    for (i = 0; i < row.length; i++) {
      e = row[i];
      if (!e) continue;
      if (e[8] === 0x33) {
        if (!open[i]) open[i] = { v: i, a: -1, t0: fr[k].t, t1: fr[k].t, x: e[0], z: e[2], k: k };
        else open[i].t1 = fr[k].t;
      } else if (open[i]) { out.push(open[i]); open[i] = null; }
      var a = (e.length > 14 && e[4]) ? e[14] : null;
      if (a === 0x17 && pa[i] !== 0x17) reach.push({ p: i, t: fr[k].t, x: e[0], z: e[2] });
      if (a === 0x19 && pa[i] !== 0x19) thrA.push({ p: i, t: fr[k].t, x: e[0], z: e[2], item: e.length > 12 ? e[12] : null });
      if (e[4] && e[6] === 0 && pst[i] > 0.5) koS.push({ p: i, t: fr[k].t, x: e[0], z: e[2], k: k });
      if (e[6] != null) pst[i] = e[6];
      pa[i] = a;
    }
  }
  for (i in open) if (open[i]) out.push(open[i]);
  out.forEach(function (ev) {
    var best = -1, bd = 3000 * 3000, q, d;
    for (q = 0; q < reach.length; q++) {
      var c = reach[q], dt = c.t - ev.t0;
      if (c.p === ev.v || dt < 0.8 || dt > 2.4) continue;
      d = (c.x - ev.x) * (c.x - ev.x) + (c.z - ev.z) * (c.z - ev.z);
      if (d < bd) { bd = d; best = c.p; }
    }
    if (best < 0) {
      // frames are change-triggered, so take each player's last entry in the
      // two seconds before the grab rather than only the grab's own row
      var seen = {};
      bd = 1500 * 1500;
      for (var j = ev.k; j >= 0 && ev.t0 - fr[j].t < 2.0; j--) {
        var rw = fr[j].p;
        if (!rw) continue;
        for (q = 0; q < rw.length; q++) {
          e = rw[q];
          if (!e || seen[q] || q === ev.v) continue;
          seen[q] = 1;
          if (!e[4]) continue;
          d = (e[0] - ev.x) * (e[0] - ev.x) + (e[2] - ev.z) * (e[2] - ev.z);
          if (d < bd) { bd = d; best = q; }
        }
      }
    }
    ev.a = best;
    // THE FILE'S OWN ANSWER wins over the guess above: the thrower's position record names the player he is acting on
    // (meta.partnerOps; 26 of 26 throws over 53 files, where the guess was wrong once and empty once).
    var po = (r.meta && r.meta.partnerOps) || [];
    for (var pq = 0; pq < po.length; pq++) {
      if (po[pq].on === ev.v && po[pq].t1 > ev.t0 - 2.5 && po[pq].t0 < ev.t1 + 1.5) { ev.a = po[pq].player; ev.fromFile = true; break; }
    }
  });
  // A THROW WHOSE VICTIM NEVER SHOWS THE THROWN STATE. A destabilized player is frozen in his own state, so when he is
  // thrown the file has only the thrower's side (empty hands, action 0x17 then 0x19) and, about half a second later, the
  // victim's stamina going straight to zero. Match 52613 has four of these at 2:46 - 3:01 and the viewer drew none of
  // them (user, 2026-10-08). Each throw action that no ordinary throw accounts for is paired with the player within
  // 3 m whose stamina drops to zero in the next 1.5 s; the victim's clip is timed to END at that drop.
  var VT_ = 1.65;
  thrA.forEach(function (ta) {
    if (out.some(function (ev) { return ev.a === ta.p && ta.t > ev.t0 - 1 && ta.t < ev.t1 + 2.5; })) return;
    var bestK = null, bdK = 3000;
    koS.forEach(function (ko) {
      if (ko.p === ta.p || ko.t < ta.t - 0.3 || ko.t > ta.t + 1.5) return;
      var dK = Math.sqrt((ko.x - ta.x) * (ko.x - ta.x) + (ko.z - ta.z) * (ko.z - ta.z));
      if (dK < bdK) { bdK = dK; bestK = ko; }
    });
    // TIMING (user, 2026-10-08, watching Cipher throw Seykai at 2:45 of 52613: the victim went down BEFORE the thrower had
    // come over). Ending the victim's clip at the stamina drop started it 1.1 s too early. The thrower's record is the
    // clock: his reach (0x17) comes ~0.06 s before the throw action and the drop ~0.5 s after it, so both clips now start
    // at the reach. The thrower's clip stops when his action ends (0x19 lasts a median 0.4 s, 0.7 at most); the victim's
    // runs its full length and then he lies still.
    if (bestK) {
      var rT_ = ta.t - 0.1;
      for (var rq_ = reach.length - 1; rq_ >= 0; rq_--) { if (reach[rq_].p === ta.p && reach[rq_].t <= ta.t && ta.t - reach[rq_].t < 0.8) { rT_ = reach[rq_].t; break; } }
      // WITH THE STUN KNIFE IN HAND THIS IS A STUN STAB, NOT A THROW (see cqcClip): no throw, no fall onto the back - the
      // victim just drops where he stands. Item unknown (newer recordings) is taken as the knife too, because a real
      // throw always shows the victim's thrown state and these never do.
      out.push({ v: bestK.p, a: ta.p, stab: ta.item == null || ta.item === 1, t0: rT_, t1: rT_ + 0.25 + 1.1, aEnd: ta.t + 0.75, x: bestK.x, z: bestK.z, k: bestK.k, synth: true });
    }
  });
}
// Every Scan EX injection in the match -> r.scans [{ a, v, t0, t1 }] (see cqcClip). The scanner's rows are the action-form
// position records in state 65; who he scans is the player his record names (meta.partnerOps), else the nearest enemy.
// THE CARDBOARD BOX. Measured on the real game 2026-10-09 (five matches, replaydiff): movement state 168 is running WITH THE BOX
// (19 episodes, one fixed hunched pose: pelvis 742 mm, hands 623 mm apart = the box set's clip 2) and state 40 next to such a run
// is SITTING INSIDE IT (46 of 46: pelvis 184 mm = clip 0). State 40 with no 168 beside it is something else (22 upright cases),
// so a box spell = one unbroken run of samples in states 40 / 168 that contains at least one 168.  -> r.boxRuns[player] = [[t0, t1]]
// NOT EVERY RUN OF THESE STATES IS THE BOX (2026-10-10): the same state bytes are the quick drop into a crouch out of a run
// (doos: 1,871 episodes of 0.13 s in nine matches), which showed as "32 box uses, 4 s" for one player in a match with no box at
// all. What tells them apart is the HANDS: over 25 recorded matches every long run of these states has no weapon in hand
// (the item reads empty), while the short ones hold a grenade (2,426), an M4 (609) and so on. So a box spell is a run of a
// second or more in which most samples have nothing in hand (empty / the box item 176; item 1 only for runs of 3 s and up,
// because the stun knife shares that number).
var BOX_MIN_S = 1.0;
function buildBox(r) {
  var out = r.boxRuns = [], fr = r.frames || [];
  function keep(c) {
    if (!c || !c[2] || c[1] - c[0] < BOX_MIN_S || !c[3]) return false;
    if (c[4] / c[3] >= 0.6) return true;
    return (c[4] + c[5]) / c[3] >= 0.6 && c[1] - c[0] >= 3.0;
  }
  for (var i = 0; i < r.players.length; i++) {
    var a = out[i] = [], cur = null;
    for (var k = 0; k < fr.length; k++) {
      var e = fr[k].p && fr[k].p[i];
      if (!e) continue;
      var alive = !(e[4] === 0 || e[4] === false), inb = alive && (e[8] === 40 || e[8] === 168);
      var it = e.length > 12 ? e[12] : null, empty = (it == null || it === 0 || it === 176) ? 1 : 0, one = it === 1 ? 1 : 0;
      if (inb && cur && fr[k].t - cur[1] < 1.0) { cur[1] = fr[k].t; if (e[8] === 168) cur[2] = 1; cur[3]++; cur[4] += empty; cur[5] += one; }
      else {
        if (keep(cur)) a.push([cur[0], cur[1]]);
        cur = inb ? [fr[k].t, fr[k].t, e[8] === 168 ? 1 : 0, 1, empty, one] : null;
      }
    }
    if (keep(cur)) a.push([cur[0], cur[1]]);
  }
}
function inBox(pi, t) {
  var a = R && R.boxRuns && R.boxRuns[pi];
  if (!a) return false;
  for (var k = 0; k < a.length; k++) { if (a[k][0] > t + 0.2) break; if (t <= a[k][1] + 0.2) return true; }
  return false;
}
function buildScans(r) {
  var out = r.scans = [], cur = {}, po = (r.meta && r.meta.partnerOps) || [];
  (r.positionVariants || []).forEach(function (v) {
    if ((v.len !== 15 && v.len !== 18) || v.motionState !== 65 || v.player == null) return;
    var c = cur[v.player];
    if (c && v.t - c.t1 < 0.7) { c.t1 = v.t; return; }
    cur[v.player] = c = { a: v.player, v: null, t0: v.t, t1: v.t, x: v.x, z: v.z }; out.push(c);
  });
  out.forEach(function (c) {
    for (var q = 0; q < po.length; q++) { if (po[q].player === c.a && po[q].on != null && po[q].on !== c.a && po[q].t1 >= c.t0 - 0.3 && po[q].t0 <= c.t1 + 0.3) { c.v = po[q].on; break; } }
    if (c.v != null) return;
    var fi = frameIndexAtIn(r, c.t0), best = 2500 * 2500, me = r.players[c.a];
    for (var j = 0; j < r.players.length; j++) {
      if (j === c.a || !me || r.players[j].team === me.team) continue;
      for (var k = fi; k >= 0 && c.t0 - r.frames[k].t < 6; k--) {
        var e = r.frames[k].p && r.frames[k].p[j];
        if (!e) continue;
        var dx = e[0] - c.x, dz = e[2] - c.z, d2 = dx * dx + dz * dz;
        if (d2 < best) { best = d2; c.v = j; }
        break;
      }
    }
  });
  out.sort(function (a, b) { return a.t0 - b.t0; });
}
function frameIndexAtIn(r, t) {
  var lo = 0, hi = r.frames.length - 1;
  while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (r.frames[mid].t <= t) lo = mid; else hi = mid - 1; }
  return lo;
}
// WHICH WAY HE FELL in his last knock-down flight before time t (within 8 s): true = onto his back. Same test the flight clip
// uses (the body travelled against the way it faced), so the get-up matches the fall that was drawn.
function fellOnBack(pi, t) {
  var a = R && R.ins && R.ins.rag && R.ins.rag[pi];
  if (!a || !a.length) return false;
  var lo = 0, hi = a.length - 1;
  while (lo < hi) { var md = (lo + hi + 1) >> 1; if (a[md][0] <= t) lo = md; else hi = md - 1; }
  var f1 = lo;
  while (f1 >= 0 && a[f1][7] !== 1 && t - a[f1][0] < 8) f1--;
  if (f1 < 0 || a[f1][7] !== 1) return false;
  var f0 = f1;
  while (f0 > 0 && a[f0 - 1][7] === 1 && a[f0][0] - a[f0 - 1][0] < 0.6) f0--;
  if (a[f0][4] == null) return false;
  var h = a[f0][4] * headScale, dx = a[f1][1] - a[f0][1], dz = a[f1][3] - a[f0][3];
  return Math.hypot(dx, dz) > 400 && Math.sin(h) * dx + Math.cos(h) * dz < 0;
}
// TURNING OVER ON THE GROUND (measured 2026-10-10, Groznyj Grad Base 0:30.5 - 0:31.1; user: "he kinda stutter glitches in that
// nade sequence"). A player knocked onto his BACK lies with his head pointing AWAY from his recorded heading; when he turns
// onto his front the heading flips by about 180 degrees in ONE sample while his head stays where it was. Drawn face down
// the whole time, his body swapped ends in a frame (the head jumped 1.1 m). So: a heading flip of more than 120 degrees
// between two lying samples = he was on his back before it and on his front after it.
// headingFlip(pi, t0, t1): is there such a flip among the ordinary samples from t0 to t1 (all of them lying / low)?
function headingFlip(pi, t0, t1) {
  if (!R || !R.frames || pi == null) return false;
  var fr = R.frames, k = Math.max(0, Math.floor(frameIndexAt(t0))), h0 = null, tf = null;
  for (; k < fr.length && fr[k].t <= t1; k++) {
    var e = fr[k].p ? fr[k].p[pi] : null;
    if (!e || fr[k].t < t0 - 0.05 || e[3] == null) continue;
    if (e[4] === false || e[4] === 0) return false;
    if (tf == null) tf = fr[k].t;
    if (fr[k].t - tf > 1.8) return false;                                     // only the lying spell that starts here
    var low = e[8] === 4 || e[8] === 6 || ((e[8] === 0 || e[8] === 129 || e[8] === 1) && e[7] != null && ((e[7] >> 5) === 3 || (e[7] >> 5) === 4));
    if (!low) return false;
    if (h0 == null) { h0 = e[3]; continue; }
    var d = Math.abs(((e[3] - h0) % 360 + 540) % 360 - 180);
    if (d > 120) return true;
  }
  return false;
}
function getUpName(C, pi, t) {
  if (pi != null && headingFlip(pi, t - 2.5, t)) return 'getUpFront';           // he turned onto his front before rising
  return (C.getUpBack && pi != null && fellOnBack(pi, t)) ? 'getUpBack' : 'getUpFront';
}
function cqcClip(p, pi) {
  var C = ANIM.clips, fi = frameIndexAt(T), st;
  // kneeling to place a device: the game's own clip, held on its last pose until he is done (see applySubTrack)
  // THE CQC HOLD, both sides, from the game's own states (measured 2026-10-08 over 53 files, scratch loop/actstates.js and
  // vicstates.js). The holder is in the action form: state 37 = taking hold (1.2 s), then 36 = holding (2.5 s), and his record
  // names the victim 0.4 m away. The victim is in ordinary state 0x37 / 0x36 for the same span, named by a holder in 16 of 17
  // episodes. The old test below (state 0x1c next to an action-0x18 player) is really GAS: only 8 of 71 such episodes have a holder.
  if (p.subForm && (p.ms === 37 || p.ms === 36) && (C.cqcGrab || C.cqcHold)) {
    return (p.ms === 37 && C.cqcGrab) ? { n: 'cqcGrab', t: Math.max(0, T - p.subT0) } : { n: C.cqcHold ? 'cqcHold' : 'cqcGrab', t: Math.max(0, T - p.subT0) };
  }
  if (!p.subForm && (p.ms === 0x36 || p.ms === 0x37) && C.cqcVictimHeld) return { n: 'cqcVictimHeld', t: T };
  // SCAN EX ON A KNOCKED-OUT ENEMY (2026-10-09, match 68780 at 2:07, user: "he scans them with scan ex but it's not showing").
  // Action-form state 65 = going in, a fixed 0.70 s, his record naming the enemy; then ordinary state 66 = coming back up,
  // 0.65 s. 30 episodes over 370 matches: 29 of the actors carry Scanning EX and every target is lying knocked out about
  // 0.8 m away (scratch loop/scan65.js, scan66.js). Clips = main set 694 / 696, MEASURED on the real game playing this replay.
  if (p.subForm && p.ms === 65 && C.scanDown) return { n: 'scanDown', t: Math.max(0, T - p.subT0) };
  // in the cardboard box (see buildBox): running with it / sitting in it
  if (!p.subForm && p.alive && pi != null && (p.ms === 168 || p.ms === 40) && C.boxRun && C.boxHide && inBox(pi, T)) {
    var bx_ = p.ms === 168 ? 'boxRun' : 'boxHide';
    return { n: bx_, t: T % (C[bx_].frames / (C[bx_].fps || ANIM.fps)) };
  }
  // BACK TO THE WALL. States 10 / 11 were read as "holding a ledge" and drawn HANGING; on the real game state 11 is the
  // cover pose against a wall (main 138 standing at 2 mm, 161 crouched at 5 mm - measured 2026-10-09 over three matches,
  // replaydiff). The real ledge hang is state 21 (main 234 = arms straight up, 6 mm).
  if (!p.subForm && p.alive && (p.ms === 11 || p.ms === 10) && C.coverStand) {
    var cv_ = (pi != null && (crouchAt(pi, fi) || proneAt(pi, fi)) && C.coverCrouch) ? 'coverCrouch' : 'coverStand';
    return { n: cv_, t: T % (C[cv_].frames / (C[cv_].fps || ANIM.fps)) };
  }
  if (!p.subForm && p.alive && p.ms === 21 && C.ledge) return { n: 'ledge', t: 99 };
  // ARMING THE BOMB (Bomb mode): the kneel state held 2 - 3 s is main 675, kneeling with both hands at the device (3 - 8 mm),
  // where an ordinary placement passes through it in ~0.7 s. Then 676 to get up.
  if (p.subForm && p.ms === 8 && p.subT1 != null && p.subT1 - p.subT0 > 1.5 && C.armKneel) return { n: 'armKneel', t: Math.max(0, T - p.subT0) % (C.armKneel.frames / (C.armKneel.fps || ANIM.fps)) };
  // WAKING A TEAM-MATE (action-form state 10, 2.6 s beside a knocked-out team-mate): the real game holds the crouch-over-a-body
  // loop, main 695 - the middle of the scan pair (measured 6 mm, 2026-10-09).
  if (p.subForm && p.ms === 10 && C.bodyLoop) return { n: 'bodyLoop', t: Math.max(0, T - p.subT0) % (C.bodyLoop.frames / (C.bodyLoop.fps || ANIM.fps)) };
  // STOPPED BY A MAGAZINE (state 48, ~4.5 s): the magazine set's own "crouch down and read" clip (measured 12 / 30 mm).
  if (!p.subForm && p.ms === 48 && p.alive && C.bookRead) {
    st = pi == null ? null : entryRunStartT(pi, fi, function (e) { return e[8] === 48; });
    return { n: 'bookRead', t: Math.min(st != null ? Math.max(0, T - st) : 0, C.bookRead.frames / (C.bookRead.fps || ANIM.fps) - 0.04) };
  }
  if (!p.subForm && p.ms === 66 && p.alive && C.scanUp) {
    st = pi == null ? null : entryRunStartT(pi, fi, function (e) { return e[8] === 66; });
    return { n: 'scanUp', t: st != null ? Math.max(0, T - st) : 0 };
  }
  // THE BOOST AND THE PULL-UP, four roles (named by the user 2026-10-10; states read off match 69273, clips measured on the real
  // game - three playbacks agree - see bake_extra.py):
  //   helper below   action-form 49 (down) -> action-form 50 (waits) -> ordinary 50 (lifts)        boostIn / boostHold / boostUp
  //   the one going up   action-form 50 beside a team-mate who is lifting, ~3.4 s, ends ~2 m up    boostClimb
  //   puller on top  the same three states, his ordinary 50 beside a team-mate in action-form 51   pullIn / pullHold / pullUp
  //   pulled up      action-form 51, 4.4 s                                                         boostPulled (by length, unmeasured)
  // "beside" = a team-mate within 2.5 m on the ground plane. A waiting player counts as the puller when a team-mate stands
  // more than 1.2 m BELOW him.
  var bMate_ = function (test) {
    if (pi == null || !R.players[pi]) return null;
    for (var bj_ = 0; bj_ < live.length; bj_++) {
      var bq_ = live[bj_];
      if (bj_ === pi || !bq_ || !bq_.alive || !R.players[bj_] || R.players[bj_].team !== R.players[pi].team) continue;
      if (Math.abs(bq_.x - p.x) < 2500 && Math.abs(bq_.z - p.z) < 2500 && test(bq_)) return bq_;
    }
    return null;
  };
  // ON A LADDER: action-form states 19 / 20 / 147 / 148 arrive in the same longer row form as the boost helper, so these players
  // were not drawn at all until 2026-10-10. Measured on the real game they are the ladder family (main 264 / 272 / 265); the
  // viewer's own ladder clip stands in for all four.
  if (p.subForm && (p.ms === 19 || p.ms === 20 || p.ms === 147 || p.ms === 148) && C.climb) return { n: 'climb', t: Math.max(0, T - p.subT0) % (C.climb.frames / (C.climb.fps || ANIM.fps)) };
  var bOnce_ = function (n, t0) { return { n: n, t: Math.min(Math.max(0, T - t0), C[n].frames / (C[n].fps || ANIM.fps) - 0.04) }; };
  if (p.subForm && p.ms === 51 && C.boostPulled) return bOnce_('boostPulled', p.subT0);
  if (p.subForm && p.ms === 49 && C.boostIn) {
    return bOnce_((C.pullIn && bMate_(function (q) { return q.y != null && p.y != null && q.y < p.y - 1200; })) ? 'pullIn' : 'boostIn', p.subT0);
  }
  if (p.subForm && p.ms === 50 && C.boostHold) {
    if (C.boostClimb && bMate_(function (q) { return !q.subForm && q.ms === 50; })) return bOnce_('boostClimb', p.subT0);
    var bh_ = (C.pullHold && bMate_(function (q) { return q.y != null && p.y != null && q.y < p.y - 1200; })) ? 'pullHold' : 'boostHold';
    return { n: bh_, t: Math.max(0, T - p.subT0) % (C[bh_].frames / (C[bh_].fps || ANIM.fps)) };
  }
  if (!p.subForm && p.alive && p.ms === 50 && C.boostUp) {
    st = pi == null ? null : entryRunStartT(pi, fi, function (e) { return e[8] === 50; });
    return bOnce_((C.pullUp && bMate_(function (q) { return q.subForm && q.ms === 51; })) ? 'pullUp' : 'boostUp', st != null ? st : T);
  }
  // ROLLING IN THE DRUM CAN (state 169; named by the user 2026-10-10, his clip pick = the box set's 17). Only the body is drawn:
  // the drum can itself is not modelled yet.
  if (!p.subForm && p.alive && p.ms === 169 && C.drumRoll) return { n: 'drumRoll', t: T % (C.drumRoll.frames / (C.drumRoll.fps || ANIM.fps)) };
  // PRONE ROLL (state 3, 0.67 s; user: "flipping sides prone") and VAULT (state 19, 0.96 s; user: "vaulting a ledge"). Clip
  // numbers from doos's reading of the program's motion tables; the vault is not yet checked against a recording.
  // STATE 19 IS TWO MOVES (2026-10-10, doos's live join of replay rows to the program's action, checked by flag byte): with the
  // flag byte's top bits = 1 it is the vault over a rail (86 % of 448 rows), with 2 it is GETTING ON A LADDER (83 % of 498).
  // GOING PRONE: state 0 with top bits 5 (81 % of 327 rows; our skeleton is half-way down there) = main 46, played once.
  if (!p.subForm && p.alive && p.ms === 0 && p.sf != null && (p.sf >> 5) === 5 && C.goProne && pi != null) {
    st = entryRunStartT(pi, fi, function (e) { return e[8] === 0 && e[7] != null && (e[7] >> 5) === 5; });
    return { n: 'goProne', t: Math.min(st != null ? Math.max(0, T - st) : 0, C.goProne.frames / (C.goProne.fps || ANIM.fps) - 0.04) };
  }
  if (!p.subForm && p.alive && (p.ms === 3 || p.ms === 19) && C[p.ms === 3 ? 'proneRoll' : 'vault']) {
    var pv_ = p.ms === 3 ? 'proneRoll' : (C.ladderOn && p.sf != null && (p.sf >> 5) === 2) ? 'ladderOn' : 'vault', pm_ = p.ms;
    st = pi == null ? null : entryRunStartT(pi, fi, function (e) { return e[8] === pm_; });
    return { n: pv_, t: Math.min(st != null ? Math.max(0, T - st) : 0, C[pv_].frames / (C[pv_].fps || ANIM.fps) - 0.04) };
  }
  // A SINGLE PUNCH (state 31, 0.37 s; user: "single punches, not the punch-punch-kick combo") = main 72.
  if (!p.subForm && p.alive && p.ms === 31 && C.punch) {
    st = pi == null ? null : entryRunStartT(pi, fi, function (e) { return e[8] === 31; });
    return { n: 'punch', t: Math.min(st != null ? Math.max(0, T - st) : 0, C.punch.frames / (C.punch.fps || ANIM.fps) - 0.04) };
  }
  // WATER: state 46 = the water set's swim loop (male 13 of 13 at 11 mm; female by slot), state 45 = going in (the user's pick).
  if (!p.subForm && p.alive && p.ms === 46 && C.swim) return { n: 'swim', t: T % (C.swim.frames / (C.swim.fps || ANIM.fps)) };
  if (!p.subForm && p.alive && p.ms === 45 && C.swimIn) {
    st = pi == null ? null : entryRunStartT(pi, fi, function (e) { return e[8] === 45; });
    return { n: 'swimIn', t: Math.min(st != null ? Math.max(0, T - st) : 0, C.swimIn.frames / (C.swimIn.fps || ANIM.fps) - 0.04) };
  }
  // on a ladder (see buildLadders / ladderPlace): the game's own rung loop - main 266 up, 270 down - and the step off at the top
  if (p.ladder && p.alive && !p.subForm) {
    var ln_ = p.ladder === 3 && C.ladderOff ? 'ladderOff' : (p.ladder === 2 || p.ladder === 4) ? (C.ladderDown ? 'ladderDown' : 'climb') : (C.ladderUp ? 'ladderUp' : 'climb');
    if (C[ln_]) {
      var ldd_ = C[ln_].frames / (C[ln_].fps || ANIM.fps);
      if (ln_ === 'ladderOff') return { n: ln_, t: Math.min(Math.max(0, T - p.ladderOffT) * (ldd_ / Math.max(0.2, (R.frames[ladderSpan(pi, fi)[1]].t - p.ladderOffT))), ldd_ - 0.04) };
      return { n: ln_, t: Math.max(0, T - p.ladderT0) % ldd_ };
    }
  }
  if (p.rollT0 != null && p.alive && C.roll) {
    var rl_ = (p.rollSide && C.sideRollR) ? 'sideRollR' : 'roll', rld_ = C[rl_].frames / (C[rl_].fps || ANIM.fps);
    return { n: rl_, t: Math.min(Math.max(0, T - p.rollT0), rld_ - 0.04) };
  }
  // COMING OUT OF A ROLL: state 2 with the flag byte's top bits = 4 is the roll recovery (doos's table: 8,345 rows, 0.73 s), and
  // the real skeleton matches main 57 (71 of 118 recorded state-2 episodes). It used to be drawn as a slow walk.
  if (!p.subForm && p.alive && p.ms === 2 && C.rollRecover && pi != null && (p.sf == null || (p.sf >> 5) === 4)) {
    st = entryRunStartT(pi, fi, function (e) { return e[8] === 2; });
    var rrd_ = C.rollRecover.frames / (C.rollRecover.fps || ANIM.fps);
    if (st != null && T - st < rrd_) return { n: 'rollRecover', t: Math.max(0, T - st) };
  }
  // LYING AFTER A KNOCK-DOWN (ordinary states 6 "down" and 4 "settling"): on his back until the turn-over, face down after it
  // or when there is none. One still pose each - state 4 used to flicker between the lying and the crawling clip with his speed.
  if (!p.subForm && !p.rowForm && p.alive && pi != null && (p.ms === 6 || p.ms === 4)) {
    var lyb_ = C.sleepBack && headingFlip(pi, T, T + 2.0);
    var lyn_ = lyb_ ? 'sleepBack' : (C.prone ? 'prone' : null);
    if (lyn_) return { n: lyn_, t: 0 };
  }
  // GETTING UP AFTER A KNOCK-DOWN (user, 2026-10-10, Groznyj Grad Base 0:26 - 0:31: "that sequence is still pretty off" - the
  // player snapped from lying to walking, there was no get-up at all). The game's own clip, main 100 / 99.
  // WHEN IT STARTS, MEASURED on the real skeleton (getupcheck.py, 89 episodes in 10 recordings): through the whole of
  // state 4 (0.42 s) the hips stay ON THE GROUND (160 -> 215 mm); they are at 405 mm 0.3 s after it ends and 837 mm (up)
  // after 0.6 s. So state 4 is the body settling / turning over, and the rise begins when it ENDS and takes about 0.7 s -
  // the clip (1.13 s) is played 1.6x. In 55 of 89 the next sample is lying or crawling: then there is no get-up at all.
  var GUP_K = 1.6;
  if (p.getUpT0 != null && p.alive && C.getUpFront) {
    // UP AND RUNNING (user screenshot, 2026-10-10, 0:27: "he like skates like this for a little" - bent double, sliding along
    // at 2 - 3 m/s). With the stick pushed the game does not do the slow rise: doos's reading of the program gives a
    // scramble of 0.43 s over 1.17 m straight into the run. So when the rows say he is already travelling, the rise is
    // played in 0.43 s instead of 0.7 s and the run takes over.
    var gk_ = (p.getUpFast || (p.spd != null && p.spd > 1.0)) ? 2.6 : GUP_K;
    var gn_ = getUpName(C, pi, p.getUpT0), gt2_ = (T - p.getUpT0) * gk_;
    if (gt2_ < C[gn_].frames / (C[gn_].fps || ANIM.fps)) return { n: gn_, t: Math.max(0, gt2_) };
  }
  if (!p.subForm && !p.rowForm && p.alive && pi != null && C.getUpFront && (p.ms === 0 || p.ms === 1 || p.ms === 129) && (p.sf == null || ((p.sf >> 5) !== 3 && (p.sf >> 5) !== 4))) {
    // upright again in ordinary samples: was he in state 4 a moment ago?
    for (var gk_ = Math.round(fi); gk_ >= 0 && T - R.frames[gk_].t < 0.75; gk_--) {
      var gc_ = R.frames[gk_].p ? R.frames[gk_].p[pi] : null;
      if (!gc_) continue;
      if (gc_[8] === 4) {
        var gm_ = getUpName(C, pi, R.frames[gk_].t), gt3_ = (T - R.frames[gk_].t) * GUP_K;
        if (gt3_ < C[gm_].frames / (C[gm_].fps || ANIM.fps)) return { n: gm_, t: Math.max(0, gt3_) };
        break;
      }
      if (gc_[8] !== 0 && gc_[8] !== 1 && gc_[8] !== 129) break;
    }
  }
  // state 35 opens every CQC move. If it is not one of the paired throws (handled below), it is the reach into a hold.
  if (p.subForm && p.ms === 35 && C.cqcGrab && !(R.cqcThrows || []).some(function (e) { return e.a === pi && T >= e.t0 - 2.5 && T <= e.t1 + 1.6; })) {
    return { n: 'cqcGrab', t: Math.max(0, T - p.subT0) };
  }
  // BEHIND THE SHIELD: action 0x1B with the shield out (2,842 of 5,019 shield samples in match 69195). Crouched behind it
  // when still, advancing behind it when moving. Clip choice INFERRED from the shield set's postures.
  if (pi != null && !p.subForm && p.alive && p.item === 73 && p.act === 0x1B && C.shieldUp) {
    var sm_ = (p.spd != null && p.spd > 0.5 && C.shieldMove) ? 'shieldMove' : 'shieldUp';
    return { n: sm_, t: (T + pi * 0.31) % (C[sm_].frames / (C[sm_].fps || ANIM.fps)) };
  }
  // COUGHING IN SMOKE (state 28) and the BULLET FLINCH (state 23), in the posture the player was in when it started: the
  // game has one clip of each for standing, crouched and lying (user, 2026-10-08). The states themselves do not say the
  // posture, so it is read from the frame just before the state began. The flinch is played once from its first frame.
  // STATE 28 IS ALSO "LYING ASLEEP" (user, 2026-10-10, Urban Ultimatum Team Sneaking 0:46: "Jason stood back up to cough after
  // getting knocked out"). The recording: 41.0 s state 28, stamina full = coughing in smoke; 42.5 s state 27 = the knock-out
  // collapse, stamina 0; 45.6 s state 28 AGAIN with stamina 0 and the 40 cm step down = out cold on the ground. Our own
  // recordings say the same (state 28 matches the cough, main 115, in 67 episodes and the lying pose, main 97, in 15).
  // So with no stamina left - or inside a knock-out the game's own tracker reports - state 28 is the sleeper, not the cough.
  // (Only AFTER the collapse: his stamina reads 0 a fifth of a second before state 27 starts, while he is still on his feet.)
  if (!p.subForm && p.alive && pi != null && p.ms === 28 && ((p.st != null && p.st <= 0.001) || koAt(pi, T)) && (C.sleep || C.prone)) {
    var s28_ = entryRunStartT(pi, fi, function (e) { return e[8] === 28; }), sp_ = null;
    if (s28_ != null) { for (var sk_ = Math.max(0, Math.floor(frameIndexAt(s28_)) - 1); sk_ >= 0 && s28_ - R.frames[sk_].t < 0.5; sk_--) { sp_ = R.frames[sk_].p ? R.frames[sk_].p[pi] : null; if (sp_) break; } }
    if ((sp_ && sp_[8] === 27) || proneAt(pi, fi)) {
      var sl_ = C.sleep ? 'sleep' : 'prone';
      return { n: sl_, t: T % (C[sl_].frames / (C[sl_].fps || ANIM.fps)) };
    }
  }
  if (!p.subForm && p.alive && pi != null && (p.ms === 28 || p.ms === 23) && C[p.ms === 28 ? 'choke' : 'hit']) {
    var rm_ = p.ms, rs_ = entryRunStartT(pi, fi, function (e) { return e[8] === rm_; });
    if (rs_ != null) {
      var rf_ = Math.max(0, frameIndexAt(rs_) - 1);
      var rp_ = proneAt(pi, rf_) ? 'Prone' : crouchAt(pi, rf_) ? 'Crouch' : '';
      var rn_ = (rm_ === 28 ? 'choke' : 'hit') + rp_;
      if (!C[rn_]) rn_ = rm_ === 28 ? 'choke' : 'hit';
      var rd_ = C[rn_].frames / (C[rn_].fps || ANIM.fps);
      return { n: rn_, t: rm_ === 28 ? (T - rs_) % rd_ : Math.min(T - rs_, rd_ - 0.04) };
    }
  }
  // BLOWN OFF THE FEET (movement state 24, a median 1.97 s in 738 episodes): the game's own knock-down clip, face down
  // when the body travels the way it faces (21 of 31 in one match) and onto the back when it travels the other way.
  // The flight itself is physics in the game and is not recorded limb by limb; this is the closest canned motion.
  if (p.ragT0 != null && p.alive && C.blownFwd) {
    var rh_ = p.ragH0 != null ? p.ragH0 : (p.h || 0), rback_ = C.blownBack && Math.hypot(p.ragDX || 0, p.ragDZ || 0) > 400 && Math.sin(rh_) * p.ragDX + Math.cos(rh_) * p.ragDZ < 0;
    // ... and if he turns over afterwards (the flip above), he landed on his back whatever the travel says
    if (C.blownBack && pi != null && headingFlip(pi, T, T + 4.5)) rback_ = true;
    var rn_ = rback_ ? 'blownBack' : 'blownFwd';
    // THROWN, NOT KNOCKED OVER (user, 2026-10-10: naded mid-roll "kinda just looks like a huge roll"): the canned knock-down
    // is a man toppling where he stands. When the blast really carries him (2 m or more), one of the recorded ragdoll
    // falls is played instead - the limp, flailing body the real game shows - picked by the way he was thrown and by
    // whether he ends on his back or his front. Caught low (mid-roll), the fall starts a third of a second in.
    var rrig_ = (pi != null && marks[pi] && marks[pi].rig) ? marks[pi].rig : null, rdl_ = Math.hypot(p.ragDX || 0, p.ragDZ || 0);
    var rgm_ = rrig_ ? (rrig_.blastMeta || rrig_.ragMeta) : null, rbl_ = !!(rrig_ && rrig_.blastMeta);
    if (RAG_ON && rgm_ && rgm_.length && rdl_ >= (rbl_ ? 900 : 2000)) {
      var memo_ = R.__br || (R.__br = {}), rkey_ = pi + ':' + p.ragT0 + (rbl_ ? 'b' : 'r'), rpick_ = memo_[rkey_];
      if (!rpick_) {
        var lx_ = p.ragDX * Math.cos(rh_) - p.ragDZ * Math.sin(rh_), lz_ = p.ragDX * Math.sin(rh_) + p.ragDZ * Math.cos(rh_), want_ = Math.atan2(lx_, lz_) * 180 / Math.PI;
        // score: off the thrown direction (deg) + a wrong end side (60) + a wrong start height (45) + how far off the distance is;
        // the two best alternate by player and time so twins differ
        var sc_ = rgm_.filter(function (c) { return C[c.n]; }).map(function (c) {
          return [Math.abs(((c.dir - want_) % 360 + 540) % 360 - 180) + (c.up === !!rback_ ? 0 : 60) + (rbl_ && c.low !== !!p.ragLow ? 45 : 0) + Math.abs(c.slide - rdl_) * 0.012, c.n];
        }).sort(function (a, b) { return a[0] - b[0]; });
        rpick_ = memo_[rkey_] = !sc_.length ? null : (sc_.length > 1 && sc_[1][0] - sc_[0][0] < 25 && ((pi + Math.floor(p.ragT0 * 10)) & 1)) ? sc_[1][1] : sc_[0][1];
      }
      if (rpick_) return { n: rpick_, t: Math.min(Math.max(0, T - p.ragT0) + (!rbl_ && p.ragLow ? 0.33 : 0) + (rbl_ ? 0.1 : 0), C[rpick_].frames / (C[rpick_].fps || ANIM.fps) - 0.04) };
    }
    return { n: rn_, t: Math.min(Math.max(0, T - p.ragT0), C[rn_].frames / (C[rn_].fps || ANIM.fps) - 0.04) };
  }
  if (!p.subForm && p.ms === 24 && p.alive && C.blownFwd && pi != null) {
    var kf_ = fi, k0_ = null, k1_ = null;
    for (; kf_ >= 0 && T - R.frames[kf_].t < 4; kf_--) {
      var ke_ = R.frames[kf_].p ? R.frames[kf_].p[pi] : null;
      if (!ke_) continue;
      if (ke_[8] !== 24) break;
      if (!k1_) k1_ = ke_;
      k0_ = [R.frames[kf_].t, ke_];
    }
    if (k0_) {
      var kh_ = (k0_[1][3] || 0) * Math.PI / 180, kdx_ = k1_[0] - k0_[1][0], kdz_ = k1_[2] - k0_[1][2];
      var kn_ = (C.blownBack && Math.sqrt(kdx_ * kdx_ + kdz_ * kdz_) > 400 && Math.sin(kh_) * kdx_ + Math.cos(kh_) * kdz_ < 0) ? 'blownBack' : 'blownFwd';
      return { n: kn_, t: Math.min(T - k0_[0], C[kn_].frames / (C[kn_].fps || ANIM.fps) - 0.04) };
    }
  }
  // the victim's half of a throw that only the thrower's record shows (see buildCqc)
  if (pi != null && C.cqcVictimThrown && R && R.cqcThrows) {
    for (var sq = 0; sq < R.cqcThrows.length; sq++) {
      var se = R.cqcThrows[sq];
      // he is put on the ground on his back (the game's backward knock-down, which ends exactly in the lying-still pose
      // used after it), a quarter of a second into the thrower's move
      if (se.synth && se.stab) continue;          // stabbed, not thrown: he stays as he is until the knock-out drops him
      if (se.synth && se.v === pi && C.blownBack && T >= se.t0 + 0.25 && T <= se.t1) {
        return { n: 'blownBack', t: Math.min(T - se.t0 - 0.25, C.blownBack.frames / (C.blownBack.fps || ANIM.fps) - 0.04) };
      }
      if (se.synth && se.v === pi && !C.blownBack && T >= se.t0 && T <= se.t1) return { n: 'cqcVictimThrown', t: T - se.t0 };
    }
  }
  // A KNIFE SWING: action 7 (or its one-sample partner 6) with a knife in hand. Measured over 158 matches: a swing lasts
  // about 0.9 s (285 of 533) or is a 0.1 s tap (178). Until now it drew the rifle aiming pose. The clip is PROVISIONAL
  // (see bake_extra.py): the game has three attacks - stab, stun stab, slice (user, 2026-10-08) - and the hit records do
  // tell a knock-out stab (damage type 66) from an ordinary hit (65 / 193), but which clip is which is not known yet.
  // CORRECTED the same night. The stun knife is item 1 (the name table calls it NONE; see ITEM_FIX), and its attacks are
  // not the gun-firing action at all. Measured on 105 hits made with it over 158 matches (scratch loop/knife4.js), the
  // nearest enemy 0.7 m away, his action byte: reach 0x17, then
  //   0x19 -> a NON-LETHAL 250-point hit, damage type 45 / 173 (61 hits)   = the STUN STAB (knock-out)
  //   0x18 -> a LETHAL hit, type 17, 34 of 36 victims dead, on a held or downed enemy = the STAB (kill)
  //   0x1A -> a lethal 62-point hit, type 145, on a running enemy 1.4 m away (8)       = the SLICE
  // All three draw the one provisional knife clip until the right three are picked out.
  if (pi != null && !p.subForm && p.alive && C.knifeStab && p.item === 1 && (p.act === 0x19 || p.act === 0x1A) &&
      !(R.cqcThrows || []).some(function (e) { return !e.synth && e.a === pi && T >= e.t0 - 0.5 && T <= e.t1 + 2.5; })) {
    var ks_ = entryRunStartT(pi, fi, function (e) { return e.length > 14 && (e[14] === 0x19 || e[14] === 0x1A || e[14] === 0x17); });
    if (ks_ != null) return { n: 'knifeStab', t: Math.min(Math.max(0, T - ks_), C.knifeStab.frames / (C.knifeStab.fps || ANIM.fps) - 0.04) };
  }
  // KNOCKED OUT (asleep or stunned, the "Zzz KO" tag): limp on the ground for the WHOLE knock-out. The state under it is
  // one the parser calls "dead", which drew the dead pose at first; then, because he is alive and low, the prone rule took
  // over and laid him out aiming a rifle (user, 2026-10-08, watching a thrown destabilized player in match 52613).
  if (pi != null && !p.subForm && p.alive && koAt(pi, T) && (C.sleep || C.down || C.dead)) {
    // The game's own knock-out: a collapse, then lying still (user: "there is definitely a sleeping animation").
    //   put to sleep (Mk.2 / Mosin tranquilliser, sleep gas)  the slow 3.1 s drowsy collapse
    //   stunned any other way                                  the limp 2.0 s collapse
    //   already on the ground, or just thrown                  no collapse: straight to lying still
    // Which collapse the game really plays for which cause is INFERRED from the clips' pace, not measured.
    var kr_ = null, ka_ = R.ins.koBy[pi];
    for (var kk_ = 0; kk_ < ka_.length; kk_++) { if (ka_[kk_][0] > T) break; if (T <= ka_[kk_][1]) kr_ = ka_[kk_]; }
    if (C.sleep && kr_) {
      var ke2_ = T - kr_[0], kf0_ = Math.max(0, frameIndexAt(kr_[0]) - 1);
      var thrown_ = (R.cqcThrows || []).some(function (e) { return !e.stab && e.v === pi && kr_[0] > e.t0 - 0.5 && kr_[0] < e.t1 + 1.2; });
      if (thrown_) return { n: C.sleepBack ? 'sleepBack' : 'sleep', t: ke2_ % 3.3 };
      if (!proneAt(pi, kf0_) && kr_.__fall === undefined) {
        var zz_ = false;
        (R.events || []).forEach(function (e) {
          if (e.type === 'stun' && e.victim === pi && Math.abs(e.t - kr_[0]) < 1.5 && /RUGER|MOSIN|SLEEP/i.test(e.weaponName || '')) zz_ = true;
        });
        // MEASURED 2026-10-09 against the real game playing the recording (replaydiff/statemap.py, matches 68780 + 53628): the
        // knock-out state lasts 3.0 s and the game plays the slow collapse (main 101, or 102 from a crouch) in 13 of 13
        // episodes, whatever caused it - never the limp 2 s one. So: always the slow collapse.
        kr_.__fall = C.sleepFall ? 'sleepFall' : (C.koFall ? 'koFall' : null);
        // crouched when it hit (an ordinary crouch, or the crouched destabilized state 60): the collapse from a crouch
        var kst_ = subTrack()[pi], kcr_ = crouchAt(pi, kf0_);
        if (kst_) for (var kq_ = kst_.length - 1; kq_ >= 0; kq_--) { if (kst_[kq_][0] <= kr_[0] + 0.2) { if (kr_[0] - kst_[kq_][0] < 1.5 && kst_[kq_][6] === 60) kcr_ = true; break; } }
        if (kcr_ && C.sleepFallCrouch) kr_.__fall = 'sleepFallCrouch';
        else if (kcr_ && kr_.__fall === 'koFall' && C.koFallCrouch) kr_.__fall = 'koFallCrouch';
      }
      if (kr_.__fall) {
        var kd2_ = C[kr_.__fall].frames / (C[kr_.__fall].fps || ANIM.fps);
        if (ke2_ < kd2_) return { n: kr_.__fall, t: ke2_ };
      }
      return { n: 'sleep', t: ke2_ % 3.3 };
    }
    var kon_ = C.down ? 'down' : 'dead';
    return { n: kon_, t: Math.max(0, C[kon_].frames / (C[kon_].fps || ANIM.fps) - 0.04) };
  }
  // DESTABILIZED: the thrashing loop for the posture the game's state gives (59 standing, 60 crouched, 61 lying - the
  // pairing of state to posture is INFERRED, see bake_extra.py). Looped by hand because the ground offset is per frame.
  if (pi != null && C.destabStand && destabHit(pi, T)) {
    var dn_ = (p.subForm && p.ms === 60 && C.destabCrouch) ? 'destabCrouch' : (p.subForm && p.ms === 61 && C.destabProne) ? 'destabProne' : 'destabStand';
    var dd_ = C[dn_].frames / (C[dn_].fps || ANIM.fps);
    return { n: dn_, t: (T + pi * 0.37) % dd_ };
  }
  if (p.placeT != null && C.placeDown) return { n: 'placeDown', t: Math.min(p.placeT, C.placeDown.frames / (C.placeDown.fps || ANIM.fps) - 0.04) };
  var ths = R && R.cqcThrows, paired = false;
  // The attacker's half of the takedown is the move recorded live against the
  // victim clip (main bank 189 against sna_captured 14, same instant).
  var tk = C.cqcTakedown || C.cqcThrow;
  if (ths && tk) {
    for (var q = 0; q < ths.length; q++) {
      var ev = ths[q];
      if (ev.a !== pi || T < ev.t0 || T > ev.t1 + 1.6) continue;
      if (ev.synth && T > ev.aEnd) continue;
      // A throw the file shows only from the thrower's side is SHORT (his action lasts under a second), so it gets the
      // 0.73 s lunging throw (main set 693), not the long move. Seen frame by frame in the catalogue 2026-10-08: the long
      // pair used until now (189 against sna_captured 14) is an arm sweep and a stagger that ends back on the feet - the
      // user called it "letting go of CQC", which is what it shows.
      if (ev.synth && ev.stab && C.knifeStab) return { n: 'knifeStab', t: Math.min(T - ev.t0, C.knifeStab.frames / (C.knifeStab.fps || ANIM.fps) - 0.04) };
      if (ev.synth && C.cqcThrow) return { n: 'cqcThrow', t: Math.min(T - ev.t0, C.cqcThrow.frames / (C.cqcThrow.fps || ANIM.fps) - 0.04) };
      // the move itself, then nothing: his late action byte must not replay it
      // held on its last pose until his own samples resume (he sends none meanwhile)
      if (T <= ev.t1 || T - ev.t0 <= tk.frames / (tk.fps || ANIM.fps)) return { n: C.cqcTakedown ? 'cqcTakedown' : 'cqcThrow', t: T - ev.t0 };
      paired = true;
    }
  }
  if (!paired && p.act != null && CQC_ACT[p.act]) {
    st = entryRunStartT(pi, fi, function (e) { return e.length > 14 && CQC_ACT[e[14]] === 1; });
    var el = st != null ? Math.max(0, T - st) : 0;
    if (p.act === 0x19 && C.cqcThrow) return { n: 'cqcThrow', t: el };
    if (C.cqcGrab) {
      var gd = C.cqcGrab.frames / (C.cqcGrab.fps || ANIM.fps);
      if (p.act === 0x18 && el > gd && C.cqcHold) return { n: 'cqcHold', t: el - gd };
      return { n: 'cqcGrab', t: el };
    }
  }
  if (p.ms === 0x33 && C.cqcVictimThrown) {
    st = entryRunStartT(pi, fi, function (e) { return e[8] === 0x33; });
    return { n: 'cqcVictimThrown', t: st != null ? Math.max(0, T - st) : 0 };
  }
  if (p.ms === 0x1c && C.cqcVictimHeld) {
    for (var j = 0; j < live.length; j++) {
      var o = live[j];
      if (!o || j === pi || !o.alive || o.act !== 0x18) continue;
      var dx = o.x - p.x, dz = o.z - p.z;
      if (dx * dx + dz * dz < 1500 * 1500) return { n: 'cqcVictimHeld', t: T };
    }
  }
  return null;
}

function clipNameFor(p) {
  if (!ANIM) return null;
  if (FORCE_CLIP && ANIM.clips[FORCE_CLIP]) return FORCE_CLIP;
  var chain = null;
  if (p && !p.alive) chain = CLIP_FOR['dead'];
  // FIRING OVERRIDES THE MOTION GROUP. 74.8% of killers read motionGroup
  // 'standing' at the moment of their kill, which resolves to the idle clip --
  // so three quarters of every kill in the file was animated as standing
  // around. The action byte is what carries the firing state, and it is 18.3x
  // enriched at kills against its base rate. Only override while the player is
  // not obviously in transit, so a run-and-gun still runs.
  else if (p && p.alive && p.act != null && FIRING[p.act] && ANIM.clips.aim &&
           (p.mg == null || p.mg === 'standing' || p.mg === 'aiming, still' ||
            p.mg === 'walking')) {
    return wpnClip('wa', p) || 'aim';
  }
  else if (p && p.mg && CLIP_FOR[p.mg]) chain = CLIP_FOR[p.mg];
  if (chain) {
    for (var i = 0; i < chain.length; i++) {
      if (ANIM.clips[chain[i]]) {
        var wn = withWeapon(chain[i], p);
        // the weapon's own aim; and a handgun's own carry, which the captured
        // set never had (a pistol was held like the rifle it replaced)
        // EMPTY HANDS (item 0 / 1). The captured idle and aim both hold a rifle,
        // so a bare-handed player stood cradling one that is not drawn.
        // STUN KNIFE (item 1) held while standing: the game's own ready stance, hands up in front (main 403, measured on the
        // real game 2026-10-09), not arms hanging down.
        if ((wn === 'idle' || wn === 'aim') && p && p.alive && p.item === 1 && ANIM.clips.knifeIdle && !FORCE_CLIP) return 'knifeIdle';
        if ((wn === 'idle' || wn === 'aim') && p && p.alive && (p.item === 0 || p.item === 1) &&
            ANIM.clips.idleUnarmed && !FORCE_CLIP) return 'idleUnarmed';
        if (wn === 'aim') return wpnClip('wa', p) || wn;
        if (wn === 'idle' && p && p.alive && SECONDARY[p.item]) return wpnClip('wc', p) || wn;
        return wn;
      }
    }
  }
  if (ANIM.clips.idle) return 'idle';
  // The female set has no idle yet. A held aim pose is the nearest upright
  // stand-in; `rigtest` is a synthetic march and must never be a fallback.
  if (ANIM.clips.aim) return 'aim';
  for (var k in ANIM.clips) if (k !== 'rigtest') return k;
  for (k in ANIM.clips) return k;          // whatever exists, rather than nothing
  return null;
}

// Sampled from replay time, NOT from a free-running accumulator, so scrubbing
// the timeline lands on the same pose every time instead of drifting.
// CLIP-LOCAL TIME FOR NON-LOOPING CLIPS.
// poseSkin clamps f = time * fps into [0, frames-1] for a clip with
// loop === false. Every caller was passing ABSOLUTE REPLAY TIME (T + phase),
// so from a few seconds into any replay those clips sat permanently on their
// final frame: at T = 200 s the throw clip asks for frame 6000 and is clamped
// to 63. Measured: 11 of the 12 non-looping clips were pinned. Only `roll`
// escaped, because rollProgress is the one path that already passed a
// clip-local time. A pinned climb is the visible one -- its last frame is 4 m
// up the ladder, so the body floated.
//
// The clock has to come from the DATA, not from a latch set when the clip is
// entered, or scrubbing the timeline would land on a different pose each time
// it passed the same moment. So: walk back through the frames while the thing
// that SELECTED this clip is still true, and use the start of that run.
var RUN_SCAN_MAX_S = 8.0;
function entryRunStartT(pi, fi, test) {
  if (!R || !R.frames || pi == null) return null;
  var frames = R.frames;
  var i = Math.max(0, Math.min(Math.round(fi), frames.length - 1));
  var tEnd = frames[i].t, start = frames[i].t;
  for (var j = i; j >= 0; j--) {
    var f = frames[j];
    if (tEnd - f.t > RUN_SCAN_MAX_S) break;
    var row = f.p, e = row ? row[pi] : null;
    if (!e) continue;            // frames are change-triggered: a gap is not a break
    if (!test(e)) break;
    start = f.t;
  }
  return start;
}
function flagRunStartT(pi, fi, arr) {
  if (!R || !R.frames || !arr) return null;
  var frames = R.frames;
  var i = Math.max(0, Math.min(Math.round(fi), frames.length - 1));
  var tEnd = frames[i].t, start = frames[i].t;
  for (var j = i; j >= 0; j--) {
    if (tEnd - frames[j].t > RUN_SCAN_MAX_S) break;
    if (!arr[j]) break;
    start = frames[j].t;
  }
  return start;
}
// The motionGroup is what picks the clip on the default path, so its run is the
// clip's run. Slot 15 carries it as an index into the per-file table.
function groupRunStartT(pi, fi) {
  var frames = R && R.frames;
  if (!frames) return null;
  var i = Math.max(0, Math.min(Math.round(fi), frames.length - 1));
  var row = frames[i] && frames[i].p, cur = row && row[pi];
  if (!cur || cur.length <= 15) return null;
  var want = cur[15];
  return entryRunStartT(pi, fi, function (e) {
    return e.length > 15 && e[15] === want;
  });
}
// Clip-local time for `name`, or null when the clip loops and absolute time is
// the right thing to use. Non-looping clips get NO per-player phase offset --
// that offset exists to desynchronise idling bodies and would only push a
// one-shot further into its clamp.
function clipLocalT(m, pi, name, startT) {
  var cl = ANIM && ANIM.clips[name];
  if (!cl || cl.loop !== false) return null;
  if (startT == null) return 0;
  return Math.max(0, T - startT);
}

// A throw on the move: the throw clip was captured standing, so on its own it
// slides a planted pair of feet across the ground. Keep the throw's UPPER body
// (the spine subtree) and put it on the legs and pelvis of the gait the player
// is actually moving at. Call right after poseSkin(m, p, 'throw', ...).
// Returns the gait clip used, or null when nothing was layered.
// MEASURED over 5 replays / 929 throws: the grenade leaves the hand 0.50 s
// (p10 0.42, p90 0.57) after action 0x16 starts, and the action lasts ~1.4 s.
// The clips do not all release at that moment (the male live capture swings at
// 1.0 s, the female .mtar clip at 0.59 s), so the clip is slid until its own
// release -- the frame where the throwing hand moves fastest -- lands on it.
var THROW_RELEASE = 0.50;
function throwRelease(cl) {
  if (cl.__rel != null) return cl.__rel;
  var nb = ANIM.bones.length, h = SKIN && SKIN.hand, r = SKIN && SKIN.root;
  if (h == null || r == null || !cl.t) return (cl.__rel = THROW_RELEASE);
  var t = cl.t, best = 0, bi = 0;
  for (var k = 4; k < cl.frames; k++) {      // the first frames can hold a capture snap
    var a = (k * nb + h) * 3, b = ((k - 1) * nb + h) * 3;
    var ra = (k * nb + r) * 3, rb = ((k - 1) * nb + r) * 3;
    var dx = (t[a] - t[ra]) - (t[b] - t[rb]), dy = (t[a + 1] - t[ra + 1]) - (t[b + 1] - t[rb + 1]),
        dz = (t[a + 2] - t[ra + 2]) - (t[b + 2] - t[rb + 2]);
    var d = dx * dx + dy * dy + dz * dz;
    if (d > best) { best = d; bi = k; }
  }
  return (cl.__rel = bi / ANIM.fps);
}
// Pose `baseName` for the legs and pelvis and `upName` for everything above the
// spine, the upper body carried from its own clip's pelvis onto the base
// clip's pelvis (same construction as throwOverGait below).
var _uq = [], _up = [];
function poseLayered(m, p, baseName, baseT, upName, upT) {
  if (!SKIN || !SKIN.spineKin || SKIN.root === undefined || !m.bones) { poseSkin(m, p, baseName, baseT); return; }
  var kin = SKIN.spineKin, root = m.bones[SKIN.root], i, b;
  poseSkin(m, p, upName, upT);
  var inv = root.quaternion.clone().invert(), rp = root.position.clone();
  for (i = 0; i < kin.length; i++) {
    b = m.bones[kin[i]];
    (_uq[i] || (_uq[i] = new THREE.Quaternion())).copy(b.quaternion);
    (_up[i] || (_up[i] = new THREE.Vector3())).copy(b.position);
  }
  poseSkin(m, p, baseName, baseT);
  var rel = root.quaternion.clone().multiply(inv);
  for (i = 0; i < kin.length; i++) {
    b = m.bones[kin[i]];
    b.quaternion.copy(_uq[i]).premultiply(rel);
    b.position.copy(_up[i]).sub(rp).applyQuaternion(rel).add(root.position);
  }
}
var _lq = [], _lp = [];
function throwOverGait(m, p, pi) {
  if (!SKIN || !SKIN.spineKin || SKIN.root === undefined || !m.bones) return null;
  if (p.spd == null || p.spd < 0.6) return null;
  var gn = p.spd > 3 ? (ANIM.clips.runNade ? 'runNade' : 'run')
                     : (ANIM.clips.walkNade ? 'walkNade' : 'walk');
  var gc = ANIM.clips[gn];
  if (!gc) return null;
  var kin = SKIN.spineKin, root = m.bones[SKIN.root], i, b;
  var inv = root.quaternion.clone().invert(), rp = root.position.clone();
  for (i = 0; i < kin.length; i++) {
    b = m.bones[kin[i]];
    (_lq[i] || (_lq[i] = new THREE.Quaternion())).copy(b.quaternion);
    (_lp[i] || (_lp[i] = new THREE.Vector3())).copy(b.position);
  }
  var gt = T + (m.phase || 0);
  var d = pi != null ? distAt(pi, frameIndexAt(T)) : null;
  if (d != null && gc.speed > 0.2) gt = d / gc.speed + (m.phase || 0);
  poseSkin(m, p, gn, gt);
  // carry the saved upper body from the throw's pelvis onto the gait's pelvis
  var rel = root.quaternion.clone().multiply(inv);
  for (i = 0; i < kin.length; i++) {
    b = m.bones[kin[i]];
    b.quaternion.copy(_lq[i]).premultiply(rel);
    b.position.copy(_lp[i]).sub(rp).applyQuaternion(rel).add(root.position);
  }
  return gn;
}

function poseSkin(m, p, name, time) {
  var cl = ANIM.clips[name];
  if (!cl || !m.bones) return;
  var nb = ANIM.bones.length;
  var f = time * (cl.fps || ANIM.fps);
  var i0, i1, u;
  if (cl.loop === false) {
    // A traversal, not a cycle: a climb rises ~4 m and a throw ends mid-follow
    // -through, so wrapping them back to frame 0 snaps the body. Hold the last
    // pose instead.
    // DIAGNOSTIC: set window.__CLIPDIAG = {} to see how far into each
    // non-looping clip playback actually gets. All 1.000 means pinned.
    if (window.__CLIPDIAG) {
      var raw = cl.frames > 1 ? f / (cl.frames - 1) : 0;
      var d0 = window.__CLIPDIAG[name] || (window.__CLIPDIAG[name] = { n: 0, pinned: 0, max: 0, min: 9 });
      d0.n++;
      if (raw >= 1) d0.pinned++;
      if (raw < d0.min) d0.min = raw;
      if (raw > d0.max) d0.max = Math.min(raw, 99);
    }
    f = Math.max(0, Math.min(f, cl.frames - 1));
    i0 = Math.floor(f);
    i1 = Math.min(i0 + 1, cl.frames - 1);
    u = f - i0;
  } else {
    i0 = Math.floor(f) % cl.frames;
    if (i0 < 0) i0 += cl.frames;
    i1 = (i0 + 1) % cl.frames;
    u = f - Math.floor(f);
  }
  var q = cl.q, t = cl.t;
  for (var b = 0; b < nb; b++) {
    var a = (i0 * nb + b) * 4, c = (i1 * nb + b) * 4;
    var ax = q[a] / 32767, ay = q[a + 1] / 32767, az = q[a + 2] / 32767, aw = q[a + 3] / 32767;
    var bx = q[c] / 32767, by = q[c + 1] / 32767, bz = q[c + 2] / 32767, bw = q[c + 3] / 32767;
    // nlerp on the near hemisphere: without the sign test a quantised sign
    // flip between neighbouring frames spins the bone the long way round.
    if (ax * bx + ay * by + az * bz + aw * bw < 0) { bx = -bx; by = -by; bz = -bz; bw = -bw; }
    var x = ax + (bx - ax) * u, y = ay + (by - ay) * u,
        z = az + (bz - az) * u, w = aw + (bw - aw) * u;
    var len = Math.sqrt(x * x + y * y + z * z + w * w) || 1;
    m.bones[b].quaternion.set(x / len, y / len, z / len, w / len);
    var pa = (i0 * nb + b) * 3, pc = (i1 * nb + b) * 3;
    m.bones[b].position.set(
      t[pa] + (t[pc] - t[pa]) * u,
      t[pa + 1] + (t[pc + 1] - t[pa + 1]) * u,
      t[pa + 2] + (t[pc + 2] - t[pa + 2]) * u);
  }
}

// ---- HEAD LOOK -----------------------------------------------------------
// A captured clip carries whatever the head happened to be doing at capture
// time. The verified idle take was recorded with the head held +70.1 deg off
// the body axis for all 302 frames -- a real thing that character was doing,
// but replaying it would make every player on the map stare over the same
// shoulder. So the clip's own head yaw is measured, cancelled, and replaced
// with this player's actual look direction from slots 9-11 (`aimX/Y/Z`, a
// [CONFIRMED] world point on the aim ray, `aimDir = normalise(aim - pos)`).
//
// Both the baked yaw and the wanted yaw are measured against the body's own
// frame, so the still-uncalibrated constant between a clip's canonical heading
// and the replay's heading cancels here: the head is correct even while the
// body facing is not.
var HEAD_YAW_LIMIT = 1.4;   // rad, ~80 deg; past this a neck is not a neck

// On-page diagnostic. Whether the skinned figure loaded is invisible from the
// outside -- if it fails, app.js falls back to the rigid meshes and the result
// still looks like "players sliding around", which is also what a correct
// build with only an idle clip looks like. Those two are indistinguishable by
// eye, so the build says which one it is.
// The read-out is a builder's tool (clip counts, weapon models, the selected
// player's map coordinates). Now that the page is public it starts hidden;
// the I key or ?diag=1 shows it, and the choice is remembered on this PC.
var DIAG = null, diagTick = 0;
var DIAG_ON = /[?&]diag=1/.test(location.search);
try { if (!DIAG_ON && localStorage.getItem('mgo2diag') === '1') DIAG_ON = true; } catch (e) {}
function toggleDiag() {
  DIAG_ON = !DIAG_ON;
  try { localStorage.setItem('mgo2diag', DIAG_ON ? '1' : '0'); } catch (e) {}
  if (DIAG) DIAG.style.display = DIAG_ON ? '' : 'none';
}
function animDiag(msg) {
  if (!DIAG) {
    DIAG = document.createElement('div');
    DIAG.id = 'animdiag';
    DIAG.style.cssText = 'position:fixed;left:8px;top:8px;z-index:99;' +
      'background:rgba(0,0,0,.62);color:#9fe8ff;font:11px/1.45 ui-monospace,' +
      'Menlo,Consolas,monospace;padding:6px 9px;border-radius:5px;' +
      'pointer-events:none;white-space:pre;max-width:60vw';
    document.body.appendChild(DIAG);
    DIAG.style.display = DIAG_ON ? '' : 'none';
  }
  DIAG.textContent = msg;
}

// yaw of a quaternion's local +Z axis, flattened onto the ground plane
function quatYaw(q) {
  return Math.atan2(2 * (q.x * q.z + q.w * q.y),
                    1 - 2 * (q.x * q.x + q.y * q.y));
}

var UP_Y = null;
var _hq = null, _hv = null;
// TURN THE UPPER BODY ONTO THE SHOT, not just the weapon.
// Rotating only the prop points the barrel correctly but leaves a man jogging
// past with a rifle stuck out sideways. The torso is what actually turns when
// someone shoots, and the arms and the weapon come with it for free because
// they hang off the spine.
// Same construction as applyHeadLook: yaw the subtree about the pivot bone's
// own origin so it twists at the waist rather than orbiting the body. The head
// pass runs AFTER this and corrects on top of it.
var TORSO_YAW_LIMIT = 1.20;        // ~69 deg; past this a person steps instead
var _tq = null, _tv = null;
var TORSO_PITCH_LIMIT = 0.87, _ta = null, _tp = null;
function applyTorsoAim(m, p, pi) {
  if (!SKIN || SKIN.spine === undefined || !SKIN.spineKin || !m.bones) return;
  if (!p || !p.alive) return;
  // WHERE THE UPPER BODY POINTS. Around a kill it is the line to the victim.
  // Otherwise, while the player is aiming or firing, it is their own aim ray
  // (cells 9-11, a world point ~26.5 m along it) -- so the torso, the arms and
  // the gun they carry track the aim the whole time, up and down as well as
  // side to side, not only for the 0.8 s around a kill.
  // HIS OWN AIM RAY FIRST (2026-10-10, user: "he kinda stutter glitches ... on the crouch shot"): the line-to-the-victim used to
  // win for the 0.3 s before a kill, so a player already aiming and firing had his whole upper body SNAP in one frame (head
  // 14 units sideways and 7 down, measured) - and that line is taken from a bone that is not in the scene graph. When the
  // recording gives his own aim and he is aiming or firing, that is where he points; the kill line is only the fallback.
  var dir = null, aimed = false;
  if (p.ax != null && p.az != null &&
      ((p.act != null && FIRING[p.act]) || (p.ms != null && MS_AIM[p.ms]))) {
    // From the player's own replay position at chest height, in world units.
    // (The first version took the spine bone's "world position"; the pose bones
    // are not in the scene graph, so that was a bone-space number and the
    // resulting pitch folded the torso flat or blew it away entirely.)
    if (!_ta) _ta = new THREE.Vector3();
    _ta.set(p.ax - p.x, (p.ay != null ? p.ay : p.y + 1300) - (p.y + 1300), p.az - p.z);
    var tl = _ta.length();
    if (tl > 2000 && isFinite(tl)) { dir = _ta.divideScalar(tl); aimed = true; }
  }
  if (!dir) { dir = killAimDir(m, pi, SKIN.spine); aimed = !!dir; }
  if (!dir) return;
  // into the figure's own frame -- m.dir already carries the body heading
  if (!_tq) { _tq = new THREE.Quaternion(); _tv = new THREE.Vector3(); }
  _tv.copy(dir).applyQuaternion(
    m.dir.getWorldQuaternion(new THREE.Quaternion()).invert());
  var want = Math.atan2(_tv.x, _tv.z);
  var spine = m.bones[SKIN.spine];
  var baked = quatYaw(spine.quaternion)
            - (SKIN.root === undefined ? 0 : quatYaw(m.bones[SKIN.root].quaternion));
  // WITH A RIFLE IN BOTH HANDS, IT IS THE GUN THAT HAS TO POINT AT THE AIM, not
  // the chest. Measured on the clips: in the aim pose the line through the two
  // hands is 14-18 degrees off the body's straight-ahead, so squaring the spine
  // to the aim left the barrel that far off the line of sight (user top-down
  // shot, 2026-10-04). The upper body is turned by whatever puts the hand line
  // on the aim; the hands, and the gun in them, come round with it.
  var ha = m.bones[SKIN.hand], hb = m.bones[SKIN.hand2];
  if (ha && hb && p.item != null && isGun(p.item) && !SECONDARY[p.item] &&
      gripClass(m.lastClip) === 'two') {
    if (!_tg) { _tg = new THREE.Vector3(); _th = new THREE.Vector3(); }
    _tg.set(GRIP_A.x, GRIP_A.y, GRIP_A.z).applyQuaternion(ha.quaternion).add(ha.position);
    _th.set(GRIP_B.x, GRIP_B.y, GRIP_B.z).applyQuaternion(hb.quaternion).add(hb.position);
    _tg.sub(_th);
    var gl = _tg.length();
    if (gl >= TWO_HAND_GAP_MIN && gl <= TWO_HAND_GAP_MAX) {
      baked = Math.atan2(_tg.x, _tg.z)
            - (SKIN.root === undefined ? 0 : quatYaw(m.bones[SKIN.root].quaternion));
    }
  }
  var d = want - baked;
  while (d > Math.PI) d -= Math.PI * 2;
  while (d < -Math.PI) d += Math.PI * 2;
  d = clamp(d, -TORSO_YAW_LIMIT, TORSO_YAW_LIMIT);
  // PITCH: the aim clips hold the gun level (-0.4 deg measured), so the whole
  // elevation of the aim ray is added, about the axis across the aim
  // direction. Limited to 50 deg; a steeper shot bends the arms, not the spine.
  var pitch = aimed ? clamp(Math.atan2(_tv.y, Math.sqrt(_tv.x * _tv.x + _tv.z * _tv.z)),
                            -TORSO_PITCH_LIMIT, TORSO_PITCH_LIMIT) : 0;
  if (!isFinite(d) || !isFinite(pitch)) return;
  if (Math.abs(d) < 0.005 && Math.abs(pitch) < 0.005) return;
  _tq.setFromAxisAngle(new THREE.Vector3(0, 1, 0), d);
  if (Math.abs(pitch) >= 0.005) {
    var ya = baked + d;                    // the yaw the torso ends up facing
    if (!_tp) _tp = new THREE.Quaternion();
    _tp.setFromAxisAngle(new THREE.Vector3(-Math.cos(ya), 0, Math.sin(ya)), pitch);
    _tq.premultiply(_tp);
  }
  var px = spine.position.x, py = spine.position.y, pz = spine.position.z;
  var kin = SKIN.spineKin;
  for (var i = 0; i < kin.length; i++) {
    var b = m.bones[kin[i]];
    b.quaternion.premultiply(_tq);
    if (b === spine) continue;
    _tv.set(b.position.x - px, b.position.y - py, b.position.z - pz)
       .applyQuaternion(_tq);
    b.position.set(px + _tv.x, py + _tv.y, pz + _tv.z);
  }
}

function applyHeadLook(m, p) {
  if (!SKIN || SKIN.head === undefined || !m.bones) return;
  if (!p || !p.alive || p.ax == null) return;      // no aim decoded: leave the clip alone
  var dx = p.ax - p.x, dz = p.az - p.z;
  if (dx * dx + dz * dz < 1) return;               // degenerate ray
  // The figure hangs under m.dir, which the replay has already turned to the
  // body heading, so everything here is in body-local terms.
  var want = Math.atan2(dx, dz) - (p.h == null ? 0 : p.h);
  var head = m.bones[SKIN.head];
  var baked = quatYaw(head.quaternion)
            - (SKIN.root === undefined ? 0 : quatYaw(m.bones[SKIN.root].quaternion));
  var d = want - baked;
  while (d > Math.PI) d -= Math.PI * 2;
  while (d < -Math.PI) d += Math.PI * 2;
  d = clamp(d, -HEAD_YAW_LIMIT, HEAD_YAW_LIMIT);
  if (Math.abs(d) < 0.005) return;
  if (!_hq) {
    _hq = new THREE.Quaternion(); _hv = new THREE.Vector3();
    UP_Y = new THREE.Vector3(0, 1, 0);
  }
  _hq.setFromAxisAngle(UP_Y, d);
  // Pivot on the head bone's own origin, which sits at the base of the skull,
  // so the head turns on the neck instead of orbiting the body.
  var px = head.position.x, py = head.position.y, pz = head.position.z;
  var kin = SKIN.headKin;
  for (var i = 0; i < kin.length; i++) {
    var b = m.bones[kin[i]];
    b.quaternion.premultiply(_hq);
    if (b === head) continue;
    _hv.set(b.position.x - px, b.position.y - py, b.position.z - pz)
       .applyQuaternion(_hq);
    b.position.set(px + _hv.x, py + _hv.y, pz + _hv.z);
  }
}

// The measured carry orientation, as a vector, so the aim rotation can be
// composed onto it without discarding the roll that was solved separately.
var WEAPON_Q0 = null, _wq = null, _wv = null, _wb = null;
var WEAPON_AIM_LIMIT = 1.75;
// The aim-ray rotation is OFF by default: it visibly over-rotates. A user
// screenshot showed an M4 ~45 deg nose-up while the source says the aim ray
// is level and straight ahead -- over 8,769 firing samples the yaw is a
// median 0.6 deg off the body heading and the pitch a median 0.2, with
// 79.7% inside +/-9. The picture disagreeing with the data makes it this
// function's fault. Likely cause: the turn happens about the GRIP, so aiming
// up swings the stock down instead of the character raising the weapon --
// nothing here touches the arm chain. ?weaponaim=1 to work on it.
var WEAPON_AIM = (function () {
  try { return new URLSearchParams(location.search).get('weaponaim') === '1'; }
  catch (e) { return false; }
})();
var WAIM_DIAG = WEAPON_AIM, WAIM_ERR = [], _wb2 = null, _wv2 = null;    // rad, ~100 deg; past this a shoulder does not go

// AIM DOWN THE KILL TRACE.
// The red tracer already draws attacker -> victim from the damage record, so
// the shot direction is known exactly, for the one moment it matters. Pointing
// the weapon along that same line costs nothing extra and makes a kill read as
// a kill instead of a man jogging past.
//
// This is NOT the aim-ray feature that was reverted. That one rotated the prop
// about the grip and over-rotated visibly. This substitutes the BARREL
// DIRECTION before the basis is built, so the same up-solve that keeps the
// sights up applies to it -- the weapon turns the way a held weapon turns.
//
// ?killaim=0 disables it. The window is centred on the event: a little before,
// because the shot precedes the death record, and a little after so the pose
// does not snap back mid-tracer.
var KILL_AIM = (function () {
  try { return new URLSearchParams(location.search).get('killaim') !== '0'; }
  catch (e) { return true; }
})();
var KILL_AIM_BEFORE = 0.35, KILL_AIM_AFTER = 0.45;
var _shotIdx = null, _shotFor = null, _sv = null, _sq = null;
function shotIndex() {
  if (_shotIdx && _shotFor === R) return _shotIdx;
  _shotIdx = {}; _shotFor = R;
  var ev = (R && R.events) || [];
  for (var i = 0; i < ev.length; i++) {
    var e = ev[i];
    if ((e.type !== 'kill' && e.type !== 'stun') || e.killer == null || e.victim == null) continue;
    (_shotIdx[e.killer] || (_shotIdx[e.killer] = [])).push(e);
  }
  return _shotIdx;
}
// world-space (scene) direction from this player's weapon to whoever they are
// shooting right now, or null
function killAimDir(m, pi, fromBone) {
  if (!KILL_AIM || pi == null || !R) return null;
  var list = shotIndex()[pi];
  if (!list) return null;
  var best = null;
  for (var i = 0; i < list.length; i++) {
    var dt = T - list[i].t;
    if (dt >= -KILL_AIM_BEFORE && dt <= KILL_AIM_AFTER) { best = list[i]; break; }
  }
  if (!best) return null;
  var b = posAt(best.victim, best.t);
  if (!b) return null;
  if (!_sv) { _sv = new THREE.Vector3(); _sq = new THREE.Quaternion(); }
  var org = m.bones[fromBone === undefined ? SKIN.hand : fromBone];
  if (!org) return null;
  org.getWorldPosition(_sv);
  var vx = (b[0] - ctr[0]) * S, vy = b[1] * S + 11, vz = (b[2] - ctr[2]) * S;
  _sv.set(vx - _sv.x, vy - _sv.y, vz - _sv.z);
  if (_sv.lengthSq() < 1e-9) return null;
  return _sv.normalize();
}

// THE GRIP IS MEASURED, NOT CONSTANT.
// WEAPON_Q0 was a single hand-tuned orientation, and a constant can only ever
// be right in one pose. Decomposed, it is exactly
//     align(+Z -> carry axis) * roll(124.7 deg about the barrel)
// and the direction half of that is pose-specific: the vector from the grip
// hand to the support hand, expressed in the GRIP HAND'S OWN frame, measures
//     carry (run/walk/idle)  (-0.141, -0.769, -0.623)
//     aim                    (-0.392, -0.026,  0.920)
// which are 119.9 deg apart. So a weapon pinned to the constant is correct
// while carrying and 60 deg out while aiming -- the reported symptom exactly.
//
// A two-handed weapon lies along the line between the hands, so that line IS
// the barrel and it can be read off the pose every frame. The roll about the
// barrel was solved separately and is kept.
//
// Only for two-handed weapons: with a pistol or a grenade the off hand is not
// on the weapon at all (the hand gap measures 556 mm against 271-351 mm for a
// real two-handed hold), so those keep the constant.
var WEAPON_ROLL = null, _gv = null, _gq = null, _gz = null, _gb = null;
// MILLIMETRES -- bone space is mm, not scene units. This was 5.0 on the
// scene-unit assumption and rejected every frame: the live gap reads 271 mm,
// which is exactly the two-handed hold measured off the capture (271 +- 0).
// A one-handed grenade hold measures 556 mm, so 450 separates them cleanly.
var TWO_HAND_GAP_MAX = 450.0;
var TWO_HAND_GAP_MIN = 30.0;
// Two knobs that are settled BY EYE, because nothing in the replay says which
// end of the weapon is the muzzle or which way up the sights go. Both are
// exposed as URL parameters so they can be dialled against the real game in
// seconds instead of guessed a rebuild at a time:
//     ?muzzle=up      flip the barrel end to end
//     ?roll=<deg>     rotation about the barrel (default 124.7, the value the
//                     original hand-tuned constant decomposes to)
// Whatever settles is meant to be written back in as the default, not left in
// a query string.
var MUZZLE_FLIP = (function () {
  try { return new URLSearchParams(location.search).get('muzzle') === 'up'; }
  catch (e) { return false; }
})();
// Pitch the barrel up/down while the player is AIMING, in degrees, about the
// horizontal axis across the barrel. Positive raises the muzzle.
//     ?aimpitch=<deg>
// This is the "rotate it forward when aiming down sight" adjustment. It is a
// knob rather than a fitted value because the two sources disagree about
// whether it is needed at all: the re-cut aim clip's own barrel measures
// -0.4 deg +- 0.1 (level), and the replay's aim ray is level too (pitch
// median 0.2 deg over 8769 firing samples) -- yet it reads nose-down on
// screen. Until that is resolved, the eye gets the casting vote.
var AIM_PITCH = (function () {
  try {
    var v = parseFloat(new URLSearchParams(location.search).get('aimpitch'));
    return isNaN(v) ? 0 : v;
  } catch (e) { return 0; }
})();
var _ap = null, _apAxis = null, _apUp = null;
var ROLL_DEG = (function () {
  try {
    var v = parseFloat(new URLSearchParams(location.search).get('roll'));
    return isNaN(v) ? null : v;
  } catch (e) { return null; }
})();
// Counts WHY the measured grip declined, for debugging. Set window.__GRIP_BAIL
// = {} in the console to collect. Zero samples while rifles are on screen is
// the signal that the grip never ran at all -- which is how two silent
// wiring bugs were found, and is invisible from the picture alone.
function _bail(why) {
  if (window.__GRIP_BAIL) {
    window.__GRIP_BAIL[why] = (window.__GRIP_BAIL[why] || 0) + 1;
    window.__SKIN = SKIN;
  }
  return false;
}
// HOW A CLIP HOLDS A LONG GUN, measured from the clip itself (2026-10-04).
// The hand-to-hand line is only the barrel while both hands are on the weapon.
// Measured per clip: idle / run / walk / roll / crouchRun keep the hands a
// constant 351 mm apart and aim / walkback / crouchAim 271 mm -- both hands on
// the gun. Falls and landings swing the off hand 500-1050 mm away, and a
// climb / throw is in range for only 16-18% of its frames, so there the old
// code flipped between the hand line and a fixed angle mid-clip.
//   'two'  both hands on the gun for >= 95% of the clip: barrel = hand line
//   'one'  carried in the right hand: barrel held at the RUN clip's carry
//          direction relative to the body, so it stays steady while arms swing
//   'none' the gun is not in the hands at all (climbing, hanging, saluting,
//          knocked down): hidden, as the game puts it away or drops it
var GUN_AIM_OVERRIDE = /[?&]gunaim=1/.test(location.search);
// The bore sits ABOVE the hands: the trigger hand wraps a grip that hangs below
// the barrel and the support hand cups the handguard from underneath, so the
// gun's axis rides about this far (mm) above the line between the two grips.
// An estimate from the rifle models' proportions (grip ~45 mm below the bore,
// handguard underside ~30 mm), not a measurement. ?gunup=<mm> tunes it.
var GUN_UP_MM = (function () {
  var m = /[?&]gunup=(-?\d+)/.exec(location.search);
  return m ? parseInt(m[1], 10) : 25;
})();
var _gback = null, _pa = null, _pb = null, _gup = null, _gtq = null, _gtv = null, _gtx = null, _gaim = null, _tg = null, _th = null;
var GUN_YAW_TO_AIM = /[?&]gunyaw=1/.test(location.search);
// The underside of a gun model along its barrel, from the SURFACE (triangles
// sampled on a grid -- a stretch of handguard can hold no vertex at all), plus
// the pistol grip: gz = where it is along the barrel, gy = the middle of its
// depth below the axis. Same method as replay/grip_check.py.
function gripProfile(geo) {
  if (geo.__prof) return geo.__prof;
  var p = geo.attributes.position.array, ix = geo.index ? geo.index.array : null;
  var z0 = Infinity, z1 = -Infinity, i;
  for (i = 2; i < p.length; i += 3) { if (p[i] < z0) z0 = p[i]; if (p[i] > z1) z1 = p[i]; }
  var n = Math.max(1, Math.ceil((z1 - z0) / 40)), low = new Array(n).fill(Infinity);
  function put(y, z) { var k = Math.min(n - 1, Math.max(0, Math.floor((z - z0) / 40))); if (y < low[k]) low[k] = y; }
  var nt = ix ? ix.length / 3 : p.length / 9, N = 6;
  for (var t = 0; t < nt; t++) {
    var a = (ix ? ix[t * 3] : t * 3) * 3, b = (ix ? ix[t * 3 + 1] : t * 3 + 1) * 3, c = (ix ? ix[t * 3 + 2] : t * 3 + 2) * 3;
    for (var u = 0; u <= N; u++) for (var v = 0; v <= N - u; v++) {
      var fu = u / N, fv = v / N, fw = 1 - fu - fv;
      put(p[a + 1] * fw + p[b + 1] * fu + p[c + 1] * fv, p[a + 2] * fw + p[b + 2] * fu + p[c + 2] * fv);
    }
  }
  var best = -1;
  for (var s = 3; s < Math.floor(n * 0.55); s++) if (best < 0 || low[s] < low[best]) best = s;
  var gz = best < 0 ? -85 : z0 + (best + 0.5) * 40;
  var prof = { gz: gz, gy: (best < 0 ? -86 : low[best]) / 2,
               under: function (z) {
                 var k = Math.min(n - 1, Math.max(0, Math.floor((z - z0) / 40)));
                 return isFinite(low[k]) ? low[k] : 0;
               } };
  return (geo.__prof = prof);
}
// CLOSE THE FINGERS. The clips carry the hands almost flat open (fingertips
// 95 mm from the palm in every clip, rifle or grenade alike -- the captures
// never recorded the fingers closing), so a held gun sat on open palms (user
// screenshots, 2026-10-04). Each finger is re-posed as a chain off the hand
// bone, every joint bent toward the palm. The palm faces -Y in the hand's own
// frame and the fingers run along +X on bone 8 (left) and -X on bone 12
// (right), so the bend is about the hand's Z axis, opposite sign per hand.
// The thumb is left as the clip has it.
var FINGER_CHAINS = {
  8:  [[24, 8, 50], [25, 24, 60], [26, 25, 40], [27, 8, 50], [28, 27, 60], [29, 28, 40],
       [30, 8, 10], [31, 30, 45], [32, 31, 60], [33, 32, 40], [34, 30, 45], [35, 34, 60], [36, 35, 40]],
  12: [[40, 12, 50], [41, 40, 60], [42, 41, 40], [43, 12, 50], [44, 43, 60], [45, 44, 40],
       [46, 12, 10], [47, 46, 45], [48, 47, 60], [49, 48, 40], [50, 46, 45], [51, 50, 60], [52, 51, 40]]
};
var _cq = null, _cz = null, _cv = null;
function curlHand(m, hand, amount) {
  if (!SKIN || !SKIN.slotOf || !ANIM || !ANIM.rest || !m.bones) return;
  if (!_cq) { _cq = new THREE.Quaternion(); _cz = new THREE.Vector3(0, 0, 1); _cv = new THREE.Vector3(); }
  var sign = hand === 8 ? -1 : 1, list = FINGER_CHAINS[hand];
  for (var i = 0; i < list.length; i++) {
    var sb = SKIN.slotOf[list[i][0]], sp = SKIN.slotOf[list[i][1]];
    if (sb === undefined || sp === undefined) continue;        // the female set drops a few
    var b = m.bones[sb], par = m.bones[sp], rb = ANIM.rest[sb], rp = ANIM.rest[sp];
    if (!b || !par || !rb || !rp) continue;
    _cv.set(rb[0] - rp[0], rb[1] - rp[1], rb[2] - rp[2]).applyQuaternion(par.quaternion);
    b.position.copy(par.position).add(_cv);
    _cq.setFromAxisAngle(_cz, sign * list[i][2] * amount * Math.PI / 180);
    b.quaternion.copy(par.quaternion).multiply(_cq);
  }
}
// Palm side, not knuckle side (see SEAT_B): y was +11, which ran the gun along
// the BACKS of both hands -- the reason it never looked held.
// MOVED into the cup of the open support hand (user close-up, 2026-10-04: "the
// barrel fits into the hand here", pointing at the hollow between thumb and
// fingers). Rest pose: finger roots at x 93, y +19; thumb joint at (66, -21, 51).
// The old point (60, -22, 10) was the heel of the hand, so the handguard passed
// beside the hand, short of the fingers. And it is now the handguard's MIDDLE
// (underside + HANDGUARD_HALF) that is put there, not its underside: the hand is
// on the side of the barrel, not under it.
// (100, -30, 0) left a gap between the handguard and the web of the hand in the
// user's next close-ups ("so close, just minor adjustments"): 12 mm deeper into
// the hand and 4 mm nearer the palm.
var GRIP_A = { x: 88, y: -26, z: 0 };    // bone 8 (left hand), mm, hand-local
var HANDGUARD_HALF = 20;
// The trigger hand's point was the HEEL of the hand (-60, -22, 10): once the
// fingers were closed the fist shut in front of the pistol grip and the rifle
// rode beside the hand (user, idle close-up, 2026-10-04). Finger roots are at
// x -93, y +19 and curl round a point about (-92, -26): the grip goes there.
var GRIP_B = { x: -92, y: -26, z: 0 };   // bone 12 (right hand)
var GUN_BACK_MM = (function () {
  var m = /[?&]gunback=(-?\d+)/.exec(location.search);
  return m ? parseInt(m[1], 10) : null;      // null = measured per weapon and pose
})();
// Where a long gun's PISTOL GRIP sits along its barrel (model z, mm; negative =
// behind the origin). Found from the mesh: the lowest-hanging 40 mm slice in
// the rear 55% of the gun, skipping the first three slices (the stock's toe).
// Checked by hand on the profiles of m4a1, ak12, g3a3, xm8m, scar, m14e, mosi:
// each picks the grip (m4a1 -> about -85 mm), not the magazine or the stock.
function gripZ(geo) {
  if (!geo) return -85;
  if (geo.__gripZ != null) return geo.__gripZ;
  var p = geo.attributes.position.array, z0 = Infinity, z1 = -Infinity, i;
  for (i = 2; i < p.length; i += 3) { if (p[i] < z0) z0 = p[i]; if (p[i] > z1) z1 = p[i]; }
  var n = Math.max(1, Math.ceil((z1 - z0) / 40)), low = new Array(n).fill(Infinity);
  for (i = 0; i < p.length; i += 3) {
    var k = Math.min(n - 1, Math.floor((p[i + 2] - z0) / 40));
    if (p[i + 1] < low[k]) low[k] = p[i + 1];
  }
  var best = -1;
  for (var s = 3; s < Math.floor(n * 0.55); s++) if (best < 0 || low[s] < low[best]) best = s;
  return (geo.__gripZ = best < 0 ? -85 : z0 + (best + 0.5) * 40);
}
var GRIP_NONE = { climb: 1, ledge: 1, ledgedrop: 1, fallHang: 1, dead: 1, down: 1, salute: 1 };
function gripClass(name) {
  var cl = ANIM && ANIM.clips[name];
  if (!cl) return 'two';
  if (cl.__grip) return cl.__grip;
  if (GRIP_NONE[name]) return (cl.__grip = 'none');
  var nb = ANIM.bones.length, h = SKIN.hand, o = SKIN.hand2, t = cl.t, ok = 0;
  for (var k = 0; k < cl.frames; k++) {
    var a = (k * nb + h) * 3, b = (k * nb + o) * 3;
    var dx = t[a] - t[b], dy = t[a + 1] - t[b + 1], dz = t[a + 2] - t[b + 2];
    var g = Math.sqrt(dx * dx + dy * dy + dz * dz);
    if (g >= TWO_HAND_GAP_MIN && g <= TWO_HAND_GAP_MAX) ok++;
  }
  return (cl.__grip = ok >= cl.frames * 0.95 ? 'two' : 'one');
}
// The barrel direction of the two-handed RUN carry, in the body (root) frame,
// averaged over the clip. Per rig, because the two rigs carry differently.
function carryDir() {
  if (ANIM.__carry) return ANIM.__carry;
  var v = new THREE.Vector3(0.97, -0.16, 0.19), cl = ANIM.clips.run;
  if (cl && SKIN.root !== undefined) {
    var nb = ANIM.bones.length, h = SKIN.hand, o = SKIN.hand2, r = SKIN.root;
    var sum = new THREE.Vector3(), d = new THREE.Vector3(), q = new THREE.Quaternion();
    for (var k = 0; k < cl.frames; k++) {
      var a = (k * nb + h) * 3, b = (k * nb + o) * 3, qa = (k * nb + r) * 4;
      d.set(cl.t[a] - cl.t[b], cl.t[a + 1] - cl.t[b + 1], cl.t[a + 2] - cl.t[b + 2]).normalize();
      q.set(cl.q[qa], cl.q[qa + 1], cl.q[qa + 2], cl.q[qa + 3]).normalize().invert();
      sum.add(d.applyQuaternion(q));
    }
    if (sum.lengthSq() > 1e-6) v.copy(sum.normalize());
  }
  return (ANIM.__carry = v);
}
// ONE-HANDED ITEMS GO IN THE RIGHT HAND. The prop mesh hangs off bone 8, and
// bone 8 is the LEFT hand: it rests at +X, and a body facing +Z has its left
// on +X. (Older comments here call it the right hand; they are wrong, and it
// is why a grenade was drawn in the left hand -- user, 2026-10-04: "should be
// grenade in right hand when running".) For a rifle that is correct, the left
// hand is the forward one. A grenade, knife or pistol belongs in bone 12, the
// right hand, so the prop is placed and turned in bone 12's frame and that is
// re-expressed relative to bone 8, its actual parent.
// WHERE THE FIST CLOSES, in bone 12's frame. MEASURED on the rest pose: the
// finger roots sit at y +16..+20 above the wrist axis (the knuckle side), the
// thumb root at y -7 with its tip at y -21 and z +51. So the palm faces -Y and
// the thumb is on +Z; the hollow of the fist is below the wrist axis, between
// the fingers and the thumb. The first seat used y = +6, which is the BACK of
// the hand -- a close-up showed the grenade riding on the knuckles.
// (y was -18 at first: a user close-up showed the 60 mm grenade still poking out
// through the back of the glove, so its centre sits a full radius further into
// the palm.)
var SEAT_B = { x: -55, y: -38, z: 10 };
// The grenade is centred on its whole length, fuse included, so with the fuse
// turned toward the thumb the BODY's centre is ~22 mm back along that axis:
// the seat is moved 22 mm toward the thumb to keep the body in the palm.
// RE-SEATED from measurements (user close-ups, 2026-10-04: the body sat at the
// heel of the hand with the fingers closing in front of it, and the fuse stood
// well clear of the fist). Right-hand rest pose, wrist at 0: finger roots at
// x -93, y +19, z -40..+25; finger segments 48 + 23 + ~20 mm. The m67g body is
// a ball of radius ~42 mm whose centre is 22 mm below the centred model's
// middle; the fuse is the top 43 mm. For the fingers to wrap it the BODY centre
// belongs at (-90, -47, -5): under the knuckles, touching the palm, centred
// across the four fingers. The seat is that plus 22 mm toward the thumb, which
// leaves the fuse coming out past the forefinger, beside the thumb.
var SEAT_NADE = { x: -96, y: -47, z: 17 };   // x -6: keeps the ball where it was under the 15 degree lean
// THE THUMB GOES ROUND THE GRENADE, so the fuse stands in the "C" between thumb
// and forefinger (user, 2026-10-04; before this the clip's straight thumb lay
// across the top of the ball and poked through the fuse). Right hand only.
// Directions are in the hand's own frame, worked out from the rest pose: thumb
// root at (-10, -7, 31), ball centre at (-90, -47, -5) radius 42 -- the first
// bone runs down the wrist side of the ball, the second curls under it, both
// about 60 mm from its centre.
var THUMB_WRAP = [[-35, -43, 9], [-20, -28, -5]];
var _tw = null;
function thumbWrap(m) {
  if (!SKIN || !SKIN.slotOf || !ANIM || !ANIM.rest || !m.bones) return;
  var s = [SKIN.slotOf[12], SKIN.slotOf[37], SKIN.slotOf[38], SKIN.slotOf[39]];
  for (var i = 0; i < 4; i++) if (s[i] === undefined || !m.bones[s[i]] || !ANIM.rest[s[i]]) return;
  if (!_tw) _tw = { a: new THREE.Vector3(), b: new THREE.Vector3(), q: new THREE.Quaternion() };
  var h = m.bones[s[0]], par = h, parQ = h.quaternion;
  for (var k = 1; k < 4; k++) {
    var b = m.bones[s[k]], r = ANIM.rest[s[k]], rp = ANIM.rest[s[k - 1]];
    _tw.a.set(r[0] - rp[0], r[1] - rp[1], r[2] - rp[2]);
    b.position.copy(_tw.a).applyQuaternion(parQ).add(par.position);
    if (k < 3) {
      var rn = ANIM.rest[s[k + 1]], d = THUMB_WRAP[k - 1];
      _tw.a.set(rn[0] - r[0], rn[1] - r[1], rn[2] - r[2]).normalize();
      _tw.b.set(d[0], d[1], d[2]).normalize();
      b.quaternion.copy(h.quaternion).multiply(_tw.q.setFromUnitVectors(_tw.a, _tw.b));
    } else {
      b.quaternion.copy(par.quaternion);
    }
    par = b; parQ = b.quaternion;
  }
}
var _ob = null;
function oneHandProp(m, p) {
  var a = m.bones[SKIN.hand], b = m.bones[SKIN.hand2];
  if (!a || !b) return _bail('hand bones missing');
  if (!THROWN[p.item] && gripClass(m.lastClip) === 'none') { m.handProp.visible = false; return true; }
  if (!_ob) {
    _ob = { inv: new THREE.Quaternion(), q: new THREE.Quaternion(), v: new THREE.Vector3(),
            f: new THREE.Vector3(), x: new THREE.Vector3(), y: new THREE.Vector3(),
            up: new THREE.Vector3(0, 1, 0), m: new THREE.Matrix4() };
  }
  _ob.inv.copy(a.quaternion).invert();
  if (SECONDARY[p.item]) {
    // a pistol points where the hand points: bone 12's fingers run along its -X
    _ob.f.set(-1, 0, 0).applyQuaternion(b.quaternion).normalize();
    _ob.x.crossVectors(_ob.up, _ob.f);
    if (_ob.x.lengthSq() < 1e-6) _ob.x.set(1, 0, 0);
    _ob.x.normalize();
    _ob.y.crossVectors(_ob.f, _ob.x).normalize();
    _ob.q.setFromRotationMatrix(_ob.m.makeBasis(_ob.x, _ob.y, _ob.f));
  } else if (THROWN[p.item]) {
    // FUSE UP THROUGH THE THUMB-AND-FOREFINGER "C" (user, 2026-10-04). The
    // grenade model's fuse is its +Y end; the opening of the fist, between the
    // thumb and the forefinger, faces the hand's +Z (the thumb side). A quarter
    // turn about the hand's X takes the model's +Y onto +Z.
    // ... then leaned 15 degrees toward the fingers (about the hand's Y; user,
    // 2026-10-04: "just that little bit more counter clockwise", seen from the
    // palm side with the fingers on the left).
    if (!_ob.t) _ob.t = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), -15 * Math.PI / 180)
      .multiply(new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(1, 0, 0), Math.PI / 2));
    _ob.q.copy(b.quaternion).multiply(_ob.t);
  } else {
    _ob.q.copy(b.quaternion);                  // a knife just rides the hand
  }
  m.handProp.quaternion.copy(_ob.inv).multiply(_ob.q);
  // 0.7 = fingers bent 35 / 42 / 28 degrees, which lays them on the ball's
  // surface (40-57 mm from its centre) instead of closing through it
  curlHand(m, 12, THROWN[p.item] ? 0.7 : 1.0);
  if (THROWN[p.item]) thumbWrap(m);
  var seat = THROWN[p.item] ? SEAT_NADE : SEAT_B;
  _ob.v.set(seat.x, seat.y, seat.z).applyQuaternion(b.quaternion)
       .add(b.position).sub(a.position).applyQuaternion(_ob.inv);
  m.handProp.position.copy(_ob.v);
  return true;
}
// A LONG GUN IS FIXED IN THE TRIGGER HAND (bone 12). MEASURED 2026-10-05 on the
// live captures (replay/itembox/trigger_frame.js): the line from the trigger
// wrist to the support wrist, in the trigger hand's OWN frame, is the same in
// idle, run, walk, roll and crouch-run to 0.2 degrees, and 14 degrees from the
// aim clip's -- whereas in the support hand's frame it swings 120 degrees. So
// the gun rides the trigger hand and the support hand moves about on it.
// The gun was being laid along the line between the two hands and rolled
// sights-to-the-sky every frame instead, which in the carry poses turned it
// ~20 degrees off and stood it upright when it is really canted (user: "all
// the rifles aren't oriented right in the hands").
// The one fixed orientation comes from the AIM clip, where the truth is known:
// sights up, barrel level, pointing where the head faces. Checked: with it the
// support palm sits 3 mm off the barrel's line and 55 mm above the grip there
// (replay/itembox/rigid_gun.js). ?gunrigid=0 goes back to the two-hand line.
var GUN_RIGID = !/[?&]gunrigid=0/.test(location.search);
function rigidGunQ() {
  if (ANIM.__rigidQ !== undefined) return ANIM.__rigidQ;
  var cl = ANIM.clips.aim, out = null;
  if (cl && SKIN.head !== undefined && SKIN.hand2 !== undefined) {
    var nb = ANIM.bones.length, q = new THREE.Quaternion(), v = new THREE.Vector3();
    var f = new THREE.Vector3(), x = new THREE.Vector3(), up = new THREE.Vector3(0, 1, 0);
    var sx = new THREE.Vector3(), sy = new THREE.Vector3(), sz = new THREE.Vector3(), n = 0;
    for (var k = 0; k < cl.frames; k++) {
      var o = (k * nb + SKIN.head) * 4;
      q.set(cl.q[o], cl.q[o + 1], cl.q[o + 2], cl.q[o + 3]).normalize();
      f.set(0, 0, 1).applyQuaternion(q); f.y = 0;
      if (f.lengthSq() < 1e-6) continue;
      f.normalize(); x.crossVectors(up, f).normalize();
      o = (k * nb + SKIN.hand2) * 4;
      q.set(cl.q[o], cl.q[o + 1], cl.q[o + 2], cl.q[o + 3]).normalize().invert();
      sx.add(v.copy(x).applyQuaternion(q)); sz.add(v.copy(f).applyQuaternion(q)); n++;
    }
    if (n && sz.lengthSq() > 1e-6) {
      sz.normalize(); sx.addScaledVector(sz, -sx.dot(sz)).normalize(); sy.crossVectors(sz, sx);
      out = new THREE.Quaternion().setFromRotationMatrix(new THREE.Matrix4().makeBasis(sx, sy, sz));
    }
  }
  return (ANIM.__rigidQ = out);
}
function gripFromHands(m, p) {
  if (!m.handProp || !m.bones || !SKIN) return _bail('no handProp/bones/SKIN');
  if (SKIN.hand === undefined) return _bail('SKIN.hand undefined');
  if (SKIN.hand2 === undefined) return _bail('SKIN.hand2 undefined');
  if (!p || !p.alive || p.item == null) return _bail('no player/item');
  if (THROWN[p.item] || KNIFE_ITEM[p.item] || SECONDARY[p.item]) return oneHandProp(m, p);
  if (!FIREARM[p.item]) return _bail('item not a firearm');
  if (KNIFE_ITEM[p.item]) return _bail('item is knife');
  // A PISTOL is held in one hand, so its barrel is not a hand-to-hand line. It
  // points where the hand points: the hand bone's own +X runs wrist -> finger
  // roots. MEASURED in the body frame, that axis is forward and near level in
  // the pistol clips (male walkPistol (-0.52, +0.10, +0.85), runPistol
  // (-0.24, -0.20, +0.95); female the same within ~0.3), which is where a
  // pistol at the ready points. It used to fall through to WEAPON_Q0, a fixed
  // angle fitted to the RIFLE carry.
  var pistol = !!SECONDARY[p.item];
  var a = m.bones[SKIN.hand], b = m.bones[SKIN.hand2];
  if (!a || !b) return _bail('hand bones missing');
  if (!_gv) {
    _gv = new THREE.Vector3(); _gq = new THREE.Quaternion();
    _gz = new THREE.Vector3(0, 0, 1);
  }
  var gcls = gripClass(m.lastClip);
  if (gcls === 'none') { m.handProp.visible = false; return true; }
  // THROUGH THE PALMS, NOT THE WRISTS. The hand bones' origins are the wrists,
  // and a gun laid on the wrist-to-wrist line passes beside both hands (user
  // screenshot 2026-10-04, standing idle: open hands ~10 cm off the rifle).
  // MEASURED on both rigs' rest poses: the finger roots sit (+55.5, 12, 7.5) mm
  // from bone 8 and (-55.5, 12, 7.5) mm from bone 12 -- mirror images. A point
  // 50 mm along that, where the fingers wrap, is used for each hand.
  if (!_pa) { _pa = new THREE.Vector3(); _pb = new THREE.Vector3(); }
  _pa.set(GRIP_A.x, GRIP_A.y, GRIP_A.z).applyQuaternion(a.quaternion).add(a.position);
  _pb.set(GRIP_B.x, GRIP_B.y, GRIP_B.z).applyQuaternion(b.quaternion).add(b.position);
  _gv.subVectors(_pa, _pb);
  var gap = _gv.length();
  if (pistol) {
    _gv.set(1, 0, 0).applyQuaternion(a.quaternion).normalize();
  } else if (gcls === 'one' || gap < TWO_HAND_GAP_MIN || gap > TWO_HAND_GAP_MAX) {
    // one-handed: the steady carry direction, turned with the body
    if (SKIN.root === undefined) return _bail('hand gap out of range');
    _gv.copy(carryDir()).applyQuaternion(m.bones[SKIN.root].quaternion).normalize();
  } else {
    _gv.divideScalar(gap);
  }
  if (MUZZLE_FLIP) _gv.negate();
  // A live shot overrides the hand line. The direction arrives in SCENE space
  // and the bones work in their own frame, so it has to come back through the
  // figure group's rotation before it can be used with the hand quaternions.
  // THE GUN STAYS IN THE HANDS WHILE SHOOTING. This used to swing the barrel
  // straight at the victim for 0.8 s around every kill, whatever the hands were
  // doing -- so at exactly the moments people watch, the rifle left the trigger
  // hand and floated on its own line. The upper body is already turned onto
  // the shot (applyTorsoAim runs before this), and the hands carry the gun
  // with it. ?gunaim=1 restores the old override for comparison.
  var kd = GUN_AIM_OVERRIDE ? killAimDir(m, m.__pi) : null;
  if (kd) {
    _sq.copy(m.dir ? m.dir.getWorldQuaternion(new THREE.Quaternion()) : new THREE.Quaternion()).invert();
    _gv.copy(kd).applyQuaternion(_sq).normalize();
    m.__killAiming = true;
  } else {
    m.__killAiming = false;
  }
  // pitch it while aiming, in WORLD space, about the horizontal axis across
  // the barrel -- so "up" means up on screen regardless of which way the
  // player faces
  // Applies to CARRY as well as aiming. The replay flags only 1266 samples as
  // aiming (motionState 5), all of which already route to the level aim clip,
  // so a knob gated on aiming would never fire for the players who look wrong.
  // The nose-down is in the carry captures themselves: idle -27.8 deg,
  // walk -36.1, run -12.3, against the aim clip's -0.4.
  if (AIM_PITCH) {
    {
      if (!_ap) {
        _ap = new THREE.Quaternion();
        _apAxis = new THREE.Vector3();
        _apUp = new THREE.Vector3(0, 1, 0);
      }
      _apAxis.crossVectors(_gv, _apUp);
      if (_apAxis.lengthSq() > 1e-6) {
        _apAxis.normalize();
        // Sign was inverted against the contract above: +aimpitch lowered the
        // muzzle by exactly 1.00 deg per degree asked for. Fix the code, not
        // the comment -- positive must raise.
        _ap.setFromAxisAngle(_apAxis, AIM_PITCH * Math.PI / 180);
        _gv.applyQuaternion(_ap).normalize();
      }
    }
  }
  // ROLL IS SOLVED PER FRAME TOO, not carried as a constant.
  // The old constant's roll was measured against the carry pose, and with it
  // the weapon model's own up axis points world-up in every carry clip
  // (run +0.972, idle +0.882, walk +0.806) but -0.565 in the aim clip -- the
  // rifle is rolled 124 deg over, near enough upside down, exactly where it
  // was reported wonky. A weapon is held sights-up in every pose, so the roll
  // that satisfies that is derivable: put the barrel on the measured line and
  // then rotate about it until the model's up axis is as close to world up as
  // it can get. That is a basis, not a guess.
  if (!_gb) {
    _gb = { x: new THREE.Vector3(), y: new THREE.Vector3(),
            up: new THREE.Vector3(0, 1, 0), m: new THREE.Matrix4(),
            q: new THREE.Quaternion() };
  }
  _gb.x.crossVectors(_gb.up, _gv);
  if (_gb.x.lengthSq() < 1e-6) _gb.x.set(1, 0, 0);   // barrel straight up/down
  _gb.x.normalize();
  _gb.y.crossVectors(_gv, _gb.x).normalize();
  _gb.m.makeBasis(_gb.x, _gb.y, _gv);
  _gb.q.setFromRotationMatrix(_gb.m);                // weapon orientation, WORLD
  _gq.copy(a.quaternion).invert();
  m.handProp.quaternion.copy(_gq).multiply(_gb.q);   // into the hand's frame
  if (ROLL_DEG != null) {
    m.handProp.quaternion.multiply(new THREE.Quaternion().setFromAxisAngle(
      new THREE.Vector3(0, 0, 1), ROLL_DEG * Math.PI / 180));
  }
  // SLIDE THE LONG GUN BACK ALONG ITS OWN BARREL. With the model's origin on
  // the wrist the whole rifle sat too far forward: the pistol grip ahead of
  // the firing hand and the stock short of the shoulder. Measured off a user
  // screenshot (2026-10-04, M4 while aiming): the outline he drew is ~190 px
  // behind the drawn gun, and the 271 mm aim hand gap spans ~230 px there, so
  // about 220 mm. One value for every long gun for now; ?gunback=<mm> tunes it.
  // CORRECTED the same day ("hands still off on gun", in the carry pose): a
  // fixed 220 mm only suits one pose. What is actually true: the gun hangs off
  // bone 8, and bone 8 is the FORWARD hand (in the aim clip it sits 222 mm
  // ahead of bone 12 along the body's forward axis). The model's pistol grip is
  // only ~85 mm behind its origin, so the trigger hand, a whole hand-gap
  // further back, was holding air. The gun is therefore slid back until its own
  // grip lands on the rear hand: (hand gap) - (grip distance behind the
  // origin), measured per weapon model and per frame.
  // THE HANDS HOLD A TILTED LINE, NOT THE BARREL (2026-10-04, measured with
  // replay/grip_check.py). The trigger hand wraps the pistol grip, which hangs
  // BELOW the barrel axis; the support hand cups the handguard, whose underside
  // is ABOVE it. The line through the two hands is therefore tilted from the
  // barrel -- 14.4 deg on the M4, 13.6 deg on the AK -- and laying the barrel
  // itself along the hands left the support hand 57 mm off the gun's surface in
  // every two-handed clip. Mapping the model's own hold line (middle of the grip
  // -> handguard underside one hand-gap forward) onto the hands brings both to
  // within 3-13 mm, on both guns, in every clip.
  var twoHand = !pistol && GUN_BACK_MM == null && gcls === 'two' &&
                gap >= TWO_HAND_GAP_MIN && gap <= TWO_HAND_GAP_MAX;
  if (twoHand) {
    var gp = gripProfile(m.handProp.geometry);
    var th = Math.atan2(gp.under(gp.gz + gap) + HANDGUARD_HALF - gp.gy, gap);
    if (!_gtq) { _gtq = new THREE.Quaternion(); _gtv = new THREE.Vector3(); _gtx = new THREE.Vector3(1, 0, 0); }
    var rq = GUN_RIGID && !kd ? rigidGunQ() : null;
    if (rq) _gb.q.copy(b.quaternion).multiply(rq);             // fixed in the trigger hand
    else _gb.q.multiply(_gtq.setFromAxisAngle(_gtx, th));      // world orientation, tilted
    // WHILE AIMING THE BARREL POINTS DOWN THE LINE OF SIGHT (user, 2026-10-04:
    // a top-down shot with a line drawn straight out from the face, the rifle a
    // few degrees off it toward the support hand). Measured on the clips: the
    // line through the two hands in the aim pose is 14-18 degrees off straight
    // ahead whichever grip points are used, so the hands alone cannot give it.
    // The gun is turned about the vertical onto the player's aim ray, pivoting
    // on the trigger hand (placed just below), by at most 17 degrees.
    // SUPERSEDED minutes later: turning the gun alone took the handguard out of
    // the support hand ("barrel should fit in here", pointing at the empty cup
    // of the hand). The whole upper body is turned instead -- applyTorsoAim now
    // lines the HAND LINE up with the aim, so the gun stays in both hands and
    // still points down the line of sight. Kept behind ?gunyaw=1.
    if (GUN_YAW_TO_AIM && p && p.ax != null && p.az != null &&
        ((p.act != null && FIRING[p.act]) || (p.ms != null && MS_AIM[p.ms])) && m.dir) {
      if (!_gaim) _gaim = { v: new THREE.Vector3(), q: new THREE.Quaternion(), y: new THREE.Vector3(0, 1, 0) };
      _gaim.v.set(p.ax - p.x, 0, p.az - p.z);
      if (_gaim.v.lengthSq() > 4e6) {
        _gaim.v.applyQuaternion(m.dir.getWorldQuaternion(_gaim.q).invert());
        var dyaw = Math.atan2(_gaim.v.x, _gaim.v.z) - Math.atan2(_gv.x, _gv.z);
        while (dyaw > Math.PI) dyaw -= Math.PI * 2;
        while (dyaw < -Math.PI) dyaw += Math.PI * 2;
        dyaw = clamp(dyaw, -0.3, 0.3);
        if (isFinite(dyaw)) _gb.q.premultiply(_gaim.q.setFromAxisAngle(_gaim.y, dyaw));
      }
    }
    m.handProp.quaternion.copy(_gq).multiply(_gb.q);
    // put the model's grip point on the right hand's grip point, then express
    // the gun's origin relative to bone 8, its parent
    _gtv.set(0, gp.gy, gp.gz).applyQuaternion(_gb.q);
    m.handProp.position.copy(_pb).sub(_gtv).sub(a.position).applyQuaternion(_gq);
    // Only the TRIGGER hand closes. The support hand does not grip the barrel
    // in this game: it is about two fingers resting along the side of it (user,
    // 2026-10-04, on seeing the left hand drawn as a fist round the handguard),
    // which is what the clip's own open hand already shows.
    curlHand(m, 12, 1.0);
  } else if (!pistol) {
    if (!_gback) _gback = new THREE.Vector3();
    var back = GUN_BACK_MM != null ? GUN_BACK_MM : 250;
    _gback.set(0, 0, 1).applyQuaternion(m.handProp.quaternion).multiplyScalar(-back);
    // start from the forward hand's grip point (hand-local), then slide back
    m.handProp.position.set(GRIP_A.x, GRIP_A.y, GRIP_A.z).add(_gback);
    if (!_gup) _gup = new THREE.Vector3();
    _gup.set(0, 1, 0).applyQuaternion(m.handProp.quaternion).multiplyScalar(GUN_UP_MM);
    m.handProp.position.add(_gup);
  }
  // DIAGNOSTIC, same shape as WAIM_ERR above: a weapon held sights-up has its
  // model +Y pointing world-up (+1). Reading it off the LIVE scene is the only
  // way to confirm the basis survives the hand bone's own transform.
  if (window.__GUNDIAG) {
    var wq = a.quaternion.clone().multiply(m.handProp.quaternion);
    var up = new THREE.Vector3(0, 1, 0).applyQuaternion(wq);
    var bz = new THREE.Vector3(0, 0, 1).applyQuaternion(wq);
    var rec = { upY: up.y, pitch: Math.asin(Math.max(-1, Math.min(1, bz.y))) * 180 / Math.PI,
                ka: !!m.__killAiming };
    // when aiming at a victim, how far off the actual kill line does the
    // barrel end up? anything but ~0 means the transform is wrong, not the data
    if (m.__killAiming && m.dir) {
      var wb = bz.clone().applyQuaternion(m.dir.getWorldQuaternion(new THREE.Quaternion())).normalize();
      var want = killAimDir(m, m.__pi);
      if (want) rec.errDeg = Math.acos(Math.max(-1, Math.min(1, wb.dot(want)))) * 180 / Math.PI;
    }
    window.__GUNDIAG.push(rec);
    if (window.__GUNDIAG.length > 4000) window.__GUNDIAG.length = 0;
  }
  return true;
}

// Turn the held weapon onto the player's actual aim ray. Same idea as
// applyHeadLook: the clip supplies the pose, the replay supplies the direction.
// slots 9-11 are a [CONFIRMED] world point on the aim ray.
function applyWeaponAim(m, p, pi) {
  if (pi !== undefined) m.__pi = pi;
  if (!m.handProp || !m.handProp.visible || !m.bones) return;
  if (!SKIN || SKIN.hand === undefined) return;
  // The GRIP runs first and unconditionally. It needs only the two hand bones.
  // It used to sit below the aim-ray guard on the next line, which returns for
  // any player with no aim ray -- so the measured grip never executed at all
  // and every weapon silently kept the old constant. The symptom was a fix
  // that changed nothing: zero diagnostic samples while the HUD showed rifles
  // on screen.
  var gripped = gripFromHands(m, p);
  if (!p || !p.alive || p.ax == null || p.az == null) {
    if (!gripped) m.handProp.quaternion.copy(WEAPON_Q0);
    return;
  }
  var aiming = (p.act != null && FIRING[p.act]) || (p.ms != null && MS_AIM[p.ms]);
  if (!WEAPON_AIM || !aiming) {
    if (!gripped) m.handProp.quaternion.copy(WEAPON_Q0);
    return;
  }

  var dx = p.ax - p.x, dz = p.az - p.z;
  var dy = (p.ay != null ? p.ay - p.y : 0);
  var flat = Math.sqrt(dx * dx + dz * dz);
  // Every other exit from this function restores the grip; this one did not,
  // which is the same shape of hole that left gripFromHands dead for a session.
  if (flat < 1) { if (!gripped) m.handProp.quaternion.copy(WEAPON_Q0); return; }
  // into the figure's own frame: m.dir already carries the body heading
  var yaw = Math.atan2(dx, dz) - (p.h == null ? 0 : p.h);
  var pitch = Math.atan2(dy, flat);
  var cp = Math.cos(pitch);
  _wv.set(Math.sin(yaw) * cp, Math.sin(pitch), Math.cos(yaw) * cp);

  // express it in the hand bone's frame; the bones are flat world-form, so the
  // bone's own quaternion is the whole transform
  var hb = m.bones[SKIN.hand];
  _wq.copy(hb.quaternion).invert();
  _wv.applyQuaternion(_wq).normalize();

  // where the carry orientation currently points the barrel
  _wb.set(0, 0, 1).applyQuaternion(WEAPON_Q0).normalize();
  var dot = Math.max(-1, Math.min(1, _wb.dot(_wv)));
  if (Math.acos(dot) > WEAPON_AIM_LIMIT) { m.handProp.quaternion.copy(WEAPON_Q0); return; }
  _wq.setFromUnitVectors(_wb, _wv);           // minimal rotation: roll survives
  m.handProp.quaternion.copy(_wq).multiply(WEAPON_Q0);
  // DIAGNOSTIC: where does the barrel actually end up, against where it was
  // asked to go? Anything but ~0 means this function is wrong, not the data.
  if (WAIM_DIAG) {
    _wb2.set(0, 0, 1).applyQuaternion(m.handProp.quaternion)
        .applyQuaternion(m.bones[SKIN.hand].quaternion).normalize();
    _wv2.set(Math.sin(yaw) * cp, Math.sin(pitch), Math.cos(yaw) * cp).normalize();
    WAIM_ERR.push(THREE.MathUtils.radToDeg(
      Math.acos(Math.max(-1, Math.min(1, _wb2.dot(_wv2))))));
  }
}

function loadFigure() {
  loadSkinned();
  ['figure_rifle_carry', 'figure_rifle_back', 'figure_unarmed'].forEach(function (nm) {
    fetch(nm + '.json').then(function (r) { return r.ok ? r.json() : null; })
      .then(function (F) {
        if (!F || !F.pos || !F.idx) return;
        var pos = new Float32Array(F.pos.length);
        for (var i = 0; i < F.pos.length; i++) pos[i] = F.pos[i] / F.q;
        var g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        g.setIndex(F.idx);
        g.computeVertexNormals();
        FIG_GEO[nm.replace('figure_', '')] = g;
        retrofitFigures();
      }).catch(function () { /* boxes remain for this pose */ });
  });
}
// The mesh is normalised to 1.0 tall; a player is 1.7 m = 17 scene units.
var FIG_H = 17;
function attachFigure(m) {
  if (m.fig) return;
  var base = FIG_GEO.rifle_carry || FIG_GEO.rifle_back || FIG_GEO.unarmed;
  if (!base) return;
  var mesh = new THREE.Mesh(base, new THREE.MeshLambertMaterial({ color: m.col }));
  mesh.scale.set(FIG_H, FIG_H, FIG_H);
  // The two fetches race. If the skinned figure already won, the rigid mesh
  // must not come back visible on top of it.
  if (m.skin) mesh.visible = false;
  m.dir.add(mesh);
  m.fig = mesh;
  attachProp(m);
  // hide the box rig -- it stays in the graph so applyPose() is unchanged
  ['legL', 'legR', 'torso', 'armL', 'armR', 'head'].forEach(function (k) {
    if (m[k]) m[k].visible = false;
  });
  if (m.cone) m.cone.visible = false;
}
function retrofitFigures() {
  for (var i = 0; i < marks.length; i++) attachFigure(marks[i]);
}

function applyRoofs() {
  if (!mapMesh || !mapCeil) return;
  var band = levelBand();
  mapMesh.geometry.setDrawRange(band[0], band[1]);
  if (mapEdges) mapEdges.visible = showRoofs && !levelMode && !(HD.on && HD.mesh);
  var b = $('b-level');
  if (b) {
    b.textContent = LEVEL_NAMES[levelMode];
    b.dataset.on = levelMode ? '1' : '0';
  }
}

function aspectPad() {
  var a = cam && cam.aspect ? cam.aspect : 1.6;
  return Math.max(1, 1.35 / Math.max(0.35, a));
}

function frameCamera() {
  orbit.tx = 0; orbit.tz = 0;
  orbit.ty = (M && M.floorY != null ? M.floorY : 0) * S + 8;
  orbit.dist = clamp(span * 0.78 * aspectPad(), 60, 8000);
  orbit.az = 0.9; orbit.el = 0.58;
}

// ── player markers ───────────────────────────────────────────────────────
function buildMarkers() {
  marks.forEach(function (o) { scene.remove(o.g); });
  trailLines.forEach(function (o) { scene.remove(o); });
  marks = []; trailLines = []; trailBuf = []; rings = [];
  if (!R) return;

  R.players.forEach(function (p, i) {
    var c = new THREE.Color(teamColor(p.team));
    var g = new THREE.Group();
    // body pip
    // heading cone
    // A rudimentary soldier at TRUE scale. 1000 world units = 1 metre and a
    // player is ~1.7 m, which is 17 scene units at S = 0.01. Everything below
    // is in those units, so the figure is life-sized relative to the map; the
    // distance-based scale in updateMarks() is what keeps it visible when
    // zoomed out to the whole stage.
    var dir = new THREE.Group();
    var solid = new THREE.MeshLambertMaterial({ color: c });
    var dark = new THREE.MeshLambertMaterial({ color: c.clone().multiplyScalar(0.55) });

    function part(geo, mat, x, y, z) {
      var mesh = new THREE.Mesh(geo, mat);
      mesh.position.set(x, y, z);
      dir.add(mesh);
      return mesh;
    }
    // legs, torso, head, and a muzzle stub that reads as "facing this way"
    var legL = part(new THREE.BoxGeometry(1.5, 7.5, 1.6), dark, -1.5, 3.75, 0);
    var legR = part(new THREE.BoxGeometry(1.5, 7.5, 1.6), dark,  1.5, 3.75, 0);
    var torso = part(new THREE.BoxGeometry(4.6, 6.0, 2.6), solid, 0, 10.5, 0);
    var armL = part(new THREE.BoxGeometry(1.3, 5.4, 1.3), dark, -3.1, 10.6, 0.3);
    var armR = part(new THREE.BoxGeometry(1.3, 5.4, 1.3), dark,  3.1, 10.6, 0.3);
    var head = part(new THREE.SphereGeometry(1.9, 10, 8), solid, 0, 15.3, 0);
    // shoulders-forward wedge so heading is readable from directly above
    var nose = part(new THREE.ConeGeometry(1.5, 4.0, 4), solid, 0, 11.0, 3.4);
    nose.rotation.x = Math.PI / 2;
    g.add(dir);
    var cone = nose;
    var body = head;
    // Ground rings: a dim full track in the team colour, and a bright arc on
    // top whose sweep is the current value. RingGeometry emits 6 indices per
    // theta segment in order, so an arc is just a shortened draw range.
    function ring(r0, r1, col, op, y) {
      var m = new THREE.Mesh(new THREE.RingGeometry(r0, r1, RING_SEG, 1, Math.PI / 2, Math.PI * 2),
        new THREE.MeshBasicMaterial({ color: col, transparent: true,
                                      opacity: op, side: THREE.DoubleSide,
                                      depthWrite: false }));
      m.rotation.x = -Math.PI / 2; m.position.y = y;
      g.add(m);
      return m;
    }
    var disc   = ring(3.4, 4.4, c, 0.16, 0.4);        // health track
    var hpArc  = ring(3.4, 4.4, 0x9BD17A, 0.95, 0.5); // health value
    var stTrk  = ring(2.1, 2.9, c, 0.16, 0.4);        // stamina track
    var stArc  = ring(2.1, 2.9, 0x4EA8DE, 0.9, 0.5);  // stamina value
    // vertical stalk to tie the pip to the ground
    var stalk = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, 0.4, 0), new THREE.Vector3(0, 17.5, 0)]),
      new THREE.LineBasicMaterial({ color: c, transparent: true, opacity: 0.30 }));
    g.add(stalk);
    scene.add(g);
    var mk_ = { g: g, dir: dir, body: body, cone: cone, disc: disc, hpArc: hpArc,
                 legL: legL, legR: legR, torso: torso, armL: armL, armR: armR, head: head,
                 stTrk: stTrk, stArc: stArc, stalk: stalk, col: c,
                 sex: sexOfPlayer(p), ap: p.appearance || null, clanId: p.clanId || null };
    marks.push(mk_);
    attachFigure(mk_);
    attachSkinned(mk_);

    var tb = new Float32Array(TRAIL_N * 3);
    var tg = new THREE.BufferGeometry();
    tg.setAttribute('position', new THREE.BufferAttribute(tb, 3));
    tg.setDrawRange(0, 0);
    var tl = new THREE.Line(tg, new THREE.LineBasicMaterial({
      color: c, transparent: true, opacity: 0.42
    }));
    tl.frustumCulled = false;
    scene.add(tl); trailLines.push(tl); trailBuf.push({ buf: tb, n: 0, last: -1 });
  });
}

// ── sampling ─────────────────────────────────────────────────────────────
var FT = null;   // Float64Array of frame timestamps, built at load

function buildTimeIndex() {
  FT = null;
  if (!R || !R.frames.length) return;
  FT = new Float64Array(R.frames.length);
  for (var i = 0; i < R.frames.length; i++) FT[i] = R.frames[i].t;
}

function frameIndexAt(t) {
  if (!FT || !FT.length) return 0;
  var lo = 0, hi = FT.length - 1;
  if (t <= FT[0]) return 0;
  if (t >= FT[hi]) return hi;
  while (lo < hi) {                    // last index with FT[i] <= t
    var mid = (lo + hi + 1) >> 1;
    if (FT[mid] <= t) lo = mid; else hi = mid - 1;
  }
  return lo;
}

function sample(t) {
  live = [];
  if (!R || !R.frames.length) return;
  var fi = frameIndexAt(t);
  var f0 = R.frames[fi], f1 = R.frames[Math.min(fi + 1, R.frames.length - 1)];
  var span = f1.t - f0.t;
  // A long span means a round reset or a recording gap, not motion: hold the
  // last known pose rather than sliding players across the map.
  var u = (span > 0 && span < 1.0) ? clamp((t - f0.t) / span, 0, 1) : 0;
  if (span >= 1.0) f1 = f0;
  for (var i = 0; i < R.players.length; i++) {
    var a = f0.p ? f0.p[i] : null, b = f1.p ? f1.p[i] : null;
    var uu = u;                       // per-player: a gap has its own timebase
    var gapDt_ = null;                // real seconds between a and b, when gap-lerped
    // Frames are CHANGE-TRIGGERED: a player with no update this tick simply has
    // no entry, which is not the same as being gone. Measured on
    // replay_351754_9, players are absent from 10.5%-75.9% of frames and 96% of
    // those gaps are <=3 frames long -- hiding on a gap is what made figures
    // and their nametags flicker. Hold the last known sample across a short
    // gap; a gap longer than HOLD_S is a real absence (the longest run in that
    // file is 1,696 frames) and still hides.
    if (!a) {
      var tq = null;
      // A SIDE ROLL lasts 1.3 s, longer than both limits below, so the player froze and then vanished half-way through it
      // (measured 2026-10-10, Groznyj Grad Base 1:07). Inside one of the game's own recorded rolls the gap is a move by definition.
      var rsp_ = ladderSpan(i, fi) ? 17 : inRollSpan(i, fi) ? 2.5 : 0;
      for (var q = fi - 1; q >= 0; q--) {
        if (t - R.frames[q].t > (rsp_ || HOLD_S)) break;
        var pa = R.frames[q].p ? R.frames[q].p[i] : null;
        if (pa) { a = pa; b = null; tq = R.frames[q].t; break; }
      }
      // CARRY THE PLAYER ACROSS THE GAP INSTEAD OF PARKING THEM IN IT.
      // The engine sends NO position for the duration of a canned move and
      // resumes at the far end, so a roll arrives as one gap of 0.45-0.95 s
      // covering 3.0-4.4 m (mean 3664 mm, CV 6.9%, n=93). Holding the last
      // sample rolls the body on the spot and then teleports it to the far
      // end. Interpolating to the NEXT known sample makes the clip cover the
      // ground it actually covered.
      // Two guards, both measured: the gap must be short enough to be a move
      // rather than an absence, and short enough in DISTANCE that it is not a
      // respawn -- a roll tops out around 4.4 m, a respawn crosses the map.
      if (a && tq != null) {
        for (var r2 = fi + 1; r2 < R.frames.length; r2++) {
          var tr = R.frames[r2].t;
          if (tr - tq > (rsp_ || GAP_LERP_MAX_S)) break;
          var pb = R.frames[r2].p ? R.frames[r2].p[i] : null;
          if (!pb) continue;
          var dx_ = pb[0] - a[0], dz_ = pb[2] - a[2];
          if (dx_ * dx_ + dz_ * dz_ <= GAP_LERP_MAX_MM2 && tr > tq) {
            b = pb;
            uu = clamp((t - tq) / (tr - tq), 0, 1);
            gapDt_ = tr - tq;
          }
          break;
        }
      }
    }
    // A CQC ATTACKER SENDS NOTHING FOR THE WHOLE TAKEDOWN (1.7 s on the paired
    // throws, longer than any gap the hold above bridges), so he vanished at the
    // exact moment he should be on screen. Carry him from his last sample to
    // his next one across the paired window.
    if (!a && R.cqcThrows) {
      for (var cq = 0; cq < R.cqcThrows.length; cq++) {
        var ce = R.cqcThrows[cq];
        if (ce.a !== i || t < ce.t0 - 0.5 || t > ce.t1 + 0.5) continue;
        var qa = null, qb = null, ta_ = 0, tb_ = 0, s2;
        for (s2 = fi; s2 >= 0 && t - R.frames[s2].t < 3.0; s2--) {
          var e1 = R.frames[s2].p ? R.frames[s2].p[i] : null;
          if (e1) { qa = e1; ta_ = R.frames[s2].t; break; }
        }
        for (s2 = fi + 1; s2 < R.frames.length && R.frames[s2].t - t < 3.0; s2++) {
          var e2 = R.frames[s2].p ? R.frames[s2].p[i] : null;
          if (e2) { qb = e2; tb_ = R.frames[s2].t; break; }
        }
        if (qa) {
          a = qa; b = qb;
          if (qb && tb_ > ta_) { uu = clamp((t - ta_) / (tb_ - ta_), 0, 1); gapDt_ = tb_ - ta_; }
        }
        break;
      }
    }
    if (!a) { live.push(null); continue; }
    // instantaneous ground speed in m/s, for picking crawl vs lying still
    var spd_ = null;
    if (b && a[0] != null && b[0] != null && R && R.frames.length > 1) {
      // Divide by the REAL interval between a and b. This used the average
      // frame period unconditionally, which was near enough while b was always
      // the next frame -- but gap interpolation can put b up to 1.2 s away, and
      // a 3.6 m roll gap over an assumed 33 ms reads as ~110 m/s. Measured
      // overstatement x3.40 median / x35.2 max, and 231 samples cross the
      // 0.25 m/s threshold that picks crawl vs lying still.
      var dtf_ = (R.duration || 0) / R.frames.length;
      var dt_ = gapDt_ != null ? gapDt_
              : ((span > 0 && span < 1.0) ? span : dtf_);
      if (dt_ > 0) {
        spd_ = Math.sqrt((b[0] - a[0]) * (b[0] - a[0]) +
                         (b[2] - a[2]) * (b[2] - a[2])) / 1000 / dt_;
      }
    }
    var h = a[3] == null ? null : a[3] * headScale;
    var hb = b && b[3] != null ? b[3] * headScale : h;
    if (h != null && hb != null) {                                  // shortest-arc
      var d = hb - h;
      while (d > Math.PI) d -= Math.PI * 2;
      while (d < -Math.PI) d += Math.PI * 2;
      hb = h + d;
    }
    live.push({
      x: b ? lerp(a[0], b[0], uu) : a[0],
      y: b ? lerp(a[1], b[1], uu) : a[1],
      z: b ? lerp(a[2], b[2], uu) : a[2],
      h: h == null ? null : (hb == null ? h : lerp(h, hb, uu)),
      alive: a[4] == null ? 1 : a[4],
      item: a.length > 12 ? a[12] : null,
      ammo: a.length > 13 ? a[13] : null,
      spd:  spd_,
      ms:   a.length > 8  ? a[8]  : null,
      // The coarse motion group -- what the format documentation says to
      // drive animation from. The hosted build re-encodes it as an index into
      // R.motionGroups at slot 15 to avoid repeating the string on every
      // player every frame; a raw parse still carries it at slot 21.
      mg:   (R && R.motionGroups && a.length > 15 && a[15] != null)
              ? (R.motionGroups[a[15]] || null)
              : (a.length > 21 ? a[21] : null),
      // slots 9-11: a world point on the aim ray, same space as x/y/z. Null in
      // files whose frame translation never resolved -- head look is skipped
      // there rather than pointed at the origin.
      ax:   a.length > 9  ? a[9]  : null,
      ay:   a.length > 10 ? a[10] : null,
      az:   a.length > 11 ? a[11] : null,
      sf:   a.length > 7  ? a[7]  : null,
      act:  a.length > 14 ? a[14] : null,
      // hp/st are normalised 0..1, or null when the field is not decoded for
      // this file. Never interpolate a null into a number.
      hp: a[5] == null ? null : (b && b[5] != null ? lerp(a[5], b[5], uu) : a[5]),
      st: a[6] == null ? null : (b && b[6] != null ? lerp(a[6], b[6], uu) : a[6])
    });
    var ldp_ = ladderSpan(i, fi);
    if (ldp_ && live[live.length - 1]) ladderPlace(live[live.length - 1], ldp_, i, t);
  }
}
// WHERE A PLAYER IS ON A LADDER (user, 2026-10-10: "the ladder climb is still very messed up" - he drifted diagonally through
// the air from the foot to the top, facing wherever he had been running). A ladder is vertical: the body stays over the
// LOWER end of the gap and faces from there toward the upper end (the wall the ladder is on); it goes straight up, and
// only over the last part does it move across onto the landing (going down: across first, then straight down).
function ladderPlace(p, sp, pi, t) {
  var A = R.frames[sp[0]].p[pi], B = R.frames[sp[1]].p[pi], ta = R.frames[sp[0]].t, tb = R.frames[sp[1]].t;
  if (!A || !B || tb <= ta) return;
  var up = !!sp[2], lo = up ? A : B, hi = up ? B : A, off = Math.min(1.6, 0.3 * (tb - ta));
  var k, v;                                       // k: 0 over the ladder foot .. 1 on the landing; v: 0 bottom .. 1 top
  if (up) { k = clamp((t - (tb - off)) / off, 0, 1); v = clamp((t - ta) / Math.max(0.01, tb - ta - off * 0.5), 0, 1); }
  else { k = 1 - clamp((t - ta) / off, 0, 1); v = 1 - clamp((t - ta - off * 0.5) / Math.max(0.01, tb - ta - off * 0.5), 0, 1); }
  k = k * k * (3 - 2 * k);
  p.x = lerp(lo[0], hi[0], k); p.z = lerp(lo[2], hi[2], k); p.y = lerp(lo[1], hi[1], v);
  var dx = hi[0] - lo[0], dz = hi[2] - lo[2];
  if (dx * dx + dz * dz > 100 * 100) p.h = Math.atan2(dx, dz);
  p.spd = 0; p.ax = p.ay = p.az = null; p.act = 0; p.ms = 129; p.ladder = up ? (k > 0 ? 3 : 1) : (k > 0 ? 4 : 2); p.ladderT0 = ta; p.ladderOffT = up ? tb - off : null;
}

// A PLAYER PLACING A DEVICE. While he kneels to set down a destabilizer the game stops sending his ordinary position
// record and sends the 15-byte form instead (18 in newer files), motion state 8, for about 2 s. Those rows are kept out of
// frames[] (positionVariants), so after HOLD_S with no ordinary sample the viewer hid him: VAMP in match 52613 vanished
// at 154.9 s, placed at 156.65 s and was shot at 156.8 s, all unseen (user, 2026-10-08). Here such a player is put back
// from his last ordinary sample, moved to where the rows say he is, with placeT = seconds into the action so the
// kneel clip (the game's own, sna_wp_emine 1) can play. Only players with NO live entry are touched.
var SUB_HOLD_S = 0.6;
// WAITING IN THE BOOST (user 2026-10-10, match 69273 at 2:18: "xin is invisible before boosting. They were getting into boost
// position then holding"): rows of this form are only sent when something CHANGES, and a helper holding still for 4 s sends
// none - so his last row timed out after SUB_HOLD_S, his last ordinary sample was stale too, and he vanished. In the boost
// states (49 going down, 50 waiting / going up, 51 being pulled up) the last row now stands until a newer record arrives.
var SUB_WAIT_S = 8, SUB_WAIT = { 49: 1, 50: 1, 51: 1 };
function subTrack() {
  if (R.__sub) return R.__sub;
  var out = R.__sub = [];
  (R.positionVariants || []).forEach(function (v) {
    if (v.ragdoll || v.anchor || v.player == null || v.x == null) return;
    // EVERY action sent in this form hides its player the same way (AZTECK, same match, 203.5 - 213.8 s, states 42 / 43
    // while acting on a downed enemy), so all of them are carried; only 8 (going down, 0.7 s) and 9 (down, setting it,
    // 1.4 s) are the placing action and get the kneel clip.
    // len 19 = the same form with one more byte: the boost HELPER's rows (match 69273, Xinsomnia 138 - 143 s, states 49 / 50) are all
    // this length, and leaving it out is what made her vanish while she waited (user 2026-10-10)
    if (v.len !== 15 && v.len !== 18 && v.len !== 19) return;
    var a = out[v.player] || (out[v.player] = []), last = a[a.length - 1];
    var plc = v.motionState === 7 || v.motionState === 8 || v.motionState === 9;   // 7 = the 0.6 s lead-in (then 8 in 35 of 36)
    a.push([v.t, v.x, v.y, v.z, v.headingDeg, !plc ? null : (last && last[5] != null && v.t - last[0] < SUB_HOLD_S) ? last[5] : v.t, v.motionState,
            (last && last[6] === v.motionState && v.t - last[0] < (SUB_WAIT[v.motionState] ? SUB_WAIT_S : SUB_HOLD_S)) ? last[7] : v.t]);
  });
  // r[8] = when this run of one state ENDS (a Bomb-mode arming kneel is told from an ordinary placement by how long state 8 lasts)
  out.forEach(function (a) {
    if (!a) return;
    for (var q = a.length - 1, end = null; q >= 0; q--) {
      if (end == null || q === a.length - 1 || a[q + 1][7] !== a[q][7]) end = a[q][0];
      a[q][8] = end;
    }
  });
  return out;
}
function applySubTrack(t) {
  var sub = subTrack();
  if (!sub.length) return;
  var fi = frameIndexAt(t);
  for (var i = 0; i < R.players.length; i++) {
    var a = sub[i];
    if (!a || !a.length || t < a[0][0]) continue;
    var lo = 0, hi = a.length - 1;
    while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (a[mid][0] <= t) lo = mid; else hi = mid - 1; }
    var r0 = a[lo];
    if (t - r0[0] > (SUB_WAIT[r0[6]] ? SUB_WAIT_S : SUB_HOLD_S)) continue;
    var c = null, ct = -1e9;
    for (var q = fi; q >= 0 && t - R.frames[q].t < 8; q--) { if (R.frames[q].p && R.frames[q].p[i]) { c = R.frames[q].p[i]; ct = R.frames[q].t; break; } }
    if (!c || c[4] === 0 || c[4] === false) continue;
    // A SHORT action (the 0.7 s Scan EX jab, match 68780 at 2:07) is over before the player's last ordinary sample has
    // timed out, so that stale sample - usually "running" - used to be drawn right through it. The action rows win
    // whenever they are NEWER than his last ordinary sample.
    if (live[i] && (SUB_WAIT[r0[6]] ? ct > r0[0] + 0.05 : ct >= r0[0])) continue;
    live[i] = { x: r0[1], y: r0[2], z: r0[3], h: r0[4] == null ? null : r0[4] * headScale, alive: 1,
                item: c.length > 12 ? c[12] : null, ammo: c.length > 13 ? c[13] : null, spd: 0, ms: r0[6], mg: null,
                ax: null, ay: null, az: null, sf: c.length > 7 ? c[7] : null, act: null, hp: c[5], st: c[6],
                placeT: r0[5] == null ? null : t - r0[5], subForm: true, subT0: r0[7], subT1: r0[8] };
  }
}
// Put knocked-down and dead bodies where the ragdoll rows say they are. A row
// applies when it is newer than the player's last ordinary sample and no more
// than RAG_HOLD_S old (the rows stop at the respawn).
var RAG_HOLD_S = 1.2;
function applyRagdolls(t) {
  var rag = R && R.ins && R.ins.rag;
  if (!rag || !rag.length) return;
  var fi = frameIndexAt(t);
  for (var i = 0; i < R.players.length; i++) {
    var a = rag[i];
    if (!a || !a.length || t < a[0][0]) continue;
    var lo = 0, hi = a.length - 1;
    while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (a[mid][0] <= t) lo = mid; else hi = mid - 1; }
    var r0 = a[lo];
    if (t - r0[0] > RAG_HOLD_S) continue;
    // the player's last ordinary sample: if that is newer, he is back on his feet
    var tc = -1;
    for (var q = fi; q >= 0 && R.frames[q].t >= r0[0]; q--) {
      if (R.frames[q].p && R.frames[q].p[i]) { tc = R.frames[q].t; break; }
    }
    if (tc > r0[0]) continue;
    var r1 = a[lo + 1], u = (r1 && r1[0] - r0[0] < 0.5) ? clamp((t - r0[0]) / (r1[0] - r0[0]), 0, 1) : 0;
    if (!r1 || !u) r1 = r0;
    var p = live[i];
    if (!p) {
      // alive unless the row says dead: this used to start at alive 0, so a player with no ordinary sample that frame -
      // every player thrown by a grenade, and every one back on his feet while the game still sent this form - was
      // drawn as a ghosted corpse standing up (user's screenshots, 2026-10-09)
      p = live[i] = { x: 0, y: 0, z: 0, h: null, alive: r0[5] ? 0 : 1, item: null, ammo: null, spd: 0, ms: null,
                      mg: null, ax: null, ay: null, az: null, sf: null, act: null, hp: r0[5] ? 0 : 1, st: null };
    }
    {
      // HEALTH, STAMINA AND WHAT HE HOLDS while only this form is being sent (user, 2026-10-09: "HP acts sus on those same
      // nade clips"): the form carries none of them, so the bar read empty (later: full) for the whole flight. Measured on
      // replay_352519_7: 1.00 before the blast, no ordinary sample for 2.3 s, then 0.64 - the damage lands AT the blast.
      // So a living player takes the NEXT ordinary sample's health when it is lower than the last one (same life), else
      // the last one; stamina and the held item carry over from the last sample.
      if (!r0[5]) {
        var hb_ = null, ha_ = null, q_;
        for (q_ = fi; q_ >= 0 && t - R.frames[q_].t < 12; q_--) {
          if (R.frames[q_].p && R.frames[q_].p[i]) { hb_ = R.frames[q_].p[i]; break; }
        }
        for (q_ = fi + 1; q_ < R.frames.length && R.frames[q_].t - t < 8; q_++) {
          if (R.frames[q_].p && R.frames[q_].p[i]) { ha_ = R.frames[q_].p[i]; break; }
        }
        if (hb_) {
          p.hp = hb_[5] != null ? hb_[5] : p.hp;
          p.st = hb_.length > 6 ? hb_[6] : null;
          p.item = hb_.length > 12 ? hb_[12] : null; p.ammo = hb_.length > 13 ? hb_[13] : null;
        }
        if (ha_ && ha_[5] != null && (ha_[4] == null || ha_[4]) && (!hb_ || hb_[5] == null || ha_[5] <= hb_[5])) p.hp = ha_[5];
      }
    }
    p.x = lerp(r0[1], r1[1], u); p.y = lerp(r0[2], r1[2], u); p.z = lerp(r0[3], r1[3], u);
    if (r0[4] != null) p.h = r0[4] * headScale;
    p.spd = 0;
    // A DEAD BODY FALLS WHERE IT STOOD (user, 2026-10-09: "people are sliding all over they kinda just need to fall in
    // place"). The game's ragdoll really does carry a body ~0.8 m (measured), but drawn with one lying pose that reads as
    // a corpse skating across the floor. So the ground position is held at the first row of this death; the height still
    // follows the rows. A body that truly ends up far away (blown off a ledge, thrown by a blast: over 2.5 m) keeps its
    // recorded path. Knock-downs that are NOT deaths are untouched: they follow the rows to where the player gets up.
    if (r0[5]) {
      var k0 = lo, k1 = lo;
      while (k0 > 0 && a[k0 - 1][5] && a[k0][0] - a[k0 - 1][0] < RAG_HOLD_S) k0--;
      while (k1 < a.length - 1 && a[k1 + 1][5] && a[k1 + 1][0] - a[k1][0] < RAG_HOLD_S) k1++;
      if (Math.hypot(a[k1][1] - a[k0][1], a[k1][3] - a[k0][3]) < 2500) { p.x = a[k0][1]; p.z = a[k0][3]; }
    }
    if (r0[5]) { p.alive = 0; p.mg = 'dead'; }
    else if (r0[7] === 1) {
      // THROWN BY A BLAST: alive, flying. Where the flight started (for the clip's clock) and which way it goes.
      var f0 = lo;
      while (f0 > 0 && a[f0 - 1][7] === 1 && a[f0][0] - a[f0 - 1][0] < 0.6) f0--;
      var f1 = lo;
      while (f1 < a.length - 1 && a[f1 + 1][7] === 1 && a[f1 + 1][0] - a[f1][0] < 0.6) f1++;
      p.alive = 1; p.mg = 'blown back'; p.ms = 24;
      p.ragT0 = a[f0][0]; p.ragH = r0[6] != null ? lerp(r0[6], r1[6] != null ? r1[6] : r0[6], u) : null;
      p.ragDX = a[f1][1] - a[f0][1]; p.ragDZ = a[f1][3] - a[f0][3];
      p.ragLow = f0 > 0 && a[f0 - 1][7] === 3 && a[f0][0] - a[f0 - 1][0] < 0.4;        // caught mid-roll
      // THE BODY DOES NOT SPIN IN THE AIR: the rows' heading turns through the flight (70 -> 84 -> 38 deg in 0.7 s here), which
      // spun the body and flipped the forward / backward fall clip twice in one flight (user, 2026-10-10: "it's all funky").
      // The heading is held at the flight's first row, as it already is for a death.
      if (a[f0][4] != null) { p.ragH0 = a[f0][4] * headScale; p.h = p.ragH0; }
    } else if (r0[7] === 3) {
      // rolling (67-byte rows): alive, the roll clip on its own clock from the first row of this roll
      var q0_ = lo;
      while (q0_ > 0 && a[q0_ - 1][7] === 3 && a[q0_][0] - a[q0_ - 1][0] < 0.4) q0_--;
      p.alive = 1; p.ms = 2; p.mg = 'running'; p.rowForm = true; p.rollT0 = a[q0_][0]; p.rollSide = r0[8] === 3;
      if (r0[11] != null) { p.item = r0[11]; p.ammo = r0[12]; }
    } else if (r0[9] && r0[7] === 0) {
      // back on his feet (run / walk / stand) while the game still sends this form: an ordinary living player
      p.alive = 1; p.ms = r0[8];
      p.mg = /run/.test(r0[9]) ? 'running' : /walk/.test(r0[9]) ? 'walking' : 'standing';
      // ... and he can draw a weapon, crouch, aim and fire in it (user, 2026-10-10, Groznyj Grad Base at 0:31: "before Jason
      // dies I feel like he is aiming his gun here and not just standing up to die" - the rows say M4 out at 31.56 s,
      // crouched and firing from 31.63 s until he is shot at 32.5 s). The row's own flag byte, item and action are used;
      // crouch is the flag byte's top three bits, and what they mean depends on the state byte (doos's table, 2026-10-10, 440,000
      // rows: running + 2 = an upright run at 4.6 m/s, running + 3 = a crouched run at 3.4 m/s; still + 1 = standing,
      // still + 2 = crouched). This form has no height step.
      if (r0[10] != null) {
        var t3_ = r0[10] >> 5;
        p.sf = r0[10]; p.rowCrouch = r0[8] === 0 ? t3_ === 2 : (r0[8] === 129 || r0[8] === 1) ? (t3_ === 3 || t3_ === 7) : r0[8] === 5 ? t3_ === 7 : false;
      }
      if (r0[11] != null) { p.item = r0[11]; p.ammo = r0[12]; }
      if (r0[13] != null) p.act = r0[13];
      if (r0[14] != null) { p.ax = r0[14]; p.ay = r0[15]; p.az = r0[16]; }                 // where he is aiming: the rows carry it
      // getting up inside this form: state 1 with flag byte 0x01. The clip's clock starts where the get-up started, which
      // may be an ordinary state-4 run just before these rows.
      p.getUpT0 = null; p.rowForm = true;
      if (r0[8] === 1 && r0[10] === 1) {
        var g0_ = lo;
        while (g0_ > 0 && a[g0_ - 1][8] === 1 && a[g0_ - 1][10] === 1 && a[g0_][0] - a[g0_ - 1][0] < 0.5) g0_--;
        var gt_ = a[g0_][0];                                  // the rise starts with these rows (state 4 before them is still on the ground)
        p.getUpT0 = gt_;
        // travelling during this get-up? (speed over the whole run of rows, so the choice does not flip half-way)
        var g1_ = lo; while (g1_ < a.length - 1 && a[g1_ + 1][8] === 1 && a[g1_ + 1][10] === 1 && a[g1_ + 1][0] - a[g1_][0] < 0.5) g1_++;
        p.getUpFast = a[g1_][0] > a[g0_][0] && Math.hypot(a[g1_][1] - a[g0_][1], a[g1_][3] - a[g0_][3]) / 1000 / (a[g1_][0] - a[g0_][0]) > 1.0;
      }
      p.spd = (r1 !== r0 && r1[0] > r0[0]) ? Math.hypot(r1[1] - r0[1], r1[3] - r0[3]) / (r1[0] - r0[0]) / 1000 : 0;
    } else if (p.alive) p.mg = 'down on the ground';
  }
}

function scoreAt(t) {
  var s = R.players.map(function () { return { k: 0, d: 0 }; });
  var ev = R.events || [];
  for (var i = 0; i < ev.length; i++) {
    if (ev[i].t > t) break;
    if (ev[i].type !== 'kill') continue;
    if (s[ev[i].killer]) s[ev[i].killer].k++;
    if (s[ev[i].victim]) s[ev[i].victim].d++;
  }
  return s;
}

// ── camera ───────────────────────────────────────────────────────────────
function freeMove(fwd, right, up) {
  var cp = Math.cos(free.pitch), sy = Math.sin(free.yaw), cy = Math.cos(free.yaw);
  free.x += fwd * sy * cp - right * cy;
  free.y += fwd * Math.sin(free.pitch) + up;
  free.z += fwd * cy * cp + right * sy;
}
function updateFreeCamera(dt) {
  var k = free.keys;
  var v = freeSpeed() * (k.ShiftLeft || k.ShiftRight ? 4 : 1) * dt;
  freeMove(((k.KeyW ? 1 : 0) - (k.KeyS ? 1 : 0)) * v,
           ((k.KeyD ? 1 : 0) - (k.KeyA ? 1 : 0)) * v,
           ((k.KeyE ? 1 : 0) - (k.KeyQ ? 1 : 0)) * v);
  var cp = Math.cos(free.pitch);
  cam.position.set(free.x, free.y, free.z);
  cam.lookAt(free.x + Math.sin(free.yaw) * cp,
             free.y + Math.sin(free.pitch),
             free.z + Math.cos(free.yaw) * cp);
  scene.fog.near = 40;
  scene.fog.far = span * 2.5 + 800;
}
// Controller (standard mapping). Polled every frame because the Gamepad API
// has no button events. Y toggles free cam, A play/pause and d-pad left/right
// seek in any mode; the sticks and triggers only fly the free cam:
// left stick move, right stick look, RT up, LT down, RB fast, LB slow.
var padPrev = {};
function padAxis(v) {
  var a = Math.abs(v || 0);
  return a < 0.15 ? 0 : (v < 0 ? -1 : 1) * (a - 0.15) / 0.85;
}
function pollPad(dt) {
  if (!navigator.getGamepads) return;
  var pads = navigator.getGamepads(), gp = null;
  for (var i = 0; i < pads.length; i++) if (pads[i] && pads[i].connected) { gp = pads[i]; break; }
  if (!gp) return;
  function val(n) { var b = gp.buttons[n]; return b ? (b.value || (b.pressed ? 1 : 0)) : 0; }
  function hit(n) { var d = val(n) > 0.5, was = padPrev[n]; padPrev[n] = d; return d && !was; }
  if (hit(3)) setCam(camMode === 'free' ? 'orbit' : 'free');
  if (hit(0)) { playing = !playing; syncPlay(); }
  if (hit(14)) seek(T - 5);
  if (hit(15)) seek(T + 5);
  if (camMode !== 'free') return;
  var lx = padAxis(gp.axes[0]), ly = padAxis(gp.axes[1]);
  var rx = padAxis(gp.axes[2]), ry = padAxis(gp.axes[3]);
  var v = freeSpeed() * (val(5) > 0.5 ? 4 : val(4) > 0.5 ? 0.25 : 1) * dt;
  freeMove(-ly * v, lx * v, (val(7) - val(6)) * v);
  // squared response: fine aim near the centre, full turn rate at the edge
  free.yaw -= rx * Math.abs(rx) * 2.4 * dt;
  free.pitch = clamp(free.pitch - ry * Math.abs(ry) * 1.8 * dt, -1.5, 1.5);
}
// FOLLOW STYLES (user, 2026-10-05): 0 = the orbiting follow camera, 1 = over the
// shoulder, 2 = through the player's eyes. Both new ones look along the player's
// own aim ray (the aim point is in every position record); with no aim point
// they look along the heading. Clicking Follow again, or key C, steps on.
// The shoulder is always the right one: the file's "aim side" is not wired here.
var followStyle = 0, fpDir = null, chaseAz = 0, chaseEl = 0, chasePrev = null;
// 3 = CHASE: the game's own spectator distance, 6.2 m behind and 1.1 m up
// (measured from live memory, VIEWER_IDEAS B8).
var FOLLOW_NAMES = ['Follow', 'Shoulder', 'Eyes', 'Chase'];
function setFollowStyle(s) {
  followStyle = ((s % 4) + 4) % 4; fpDir = null; chaseAz = 0; chaseEl = 0; chasePrev = null;
  document.querySelectorAll('[data-cam="follow"]').forEach(function (b) { b.textContent = FOLLOW_NAMES[followStyle]; });
}
function camEyes() { return camMode === 'follow' && followStyle === 2 && !camSubj && follow >= 0; }
function updateBodyCamera(dt) {
  var p = live[follow];
  if (!p) return false;
  var c = insCell(R, follow, T), low = false;
  try { var fi = frameIndexAt(T); low = p.alive && (proneAt(follow, fi) ? 2 : crouchAt(follow, fi) ? 1 : 0); } catch (e) {}
  var eyeH = !p.alive ? 300 : low === 2 ? 700 : low === 1 ? 1050 : 1550;
  var ex = p.x, ey = p.y + (low ? 400 : 0) + eyeH, ez = p.z, dx, dy, dz;
  if (c && c[9] != null && c[11] != null) {
    dx = c[9] - ex; dy = (c[10] != null ? c[10] : ey) - ey; dz = c[11] - ez;
  } else { dx = Math.sin(p.h || 0); dy = 0; dz = Math.cos(p.h || 0); }
  var L = Math.hypot(dx, dy, dz) || 1; dx /= L; dy /= L; dz /= L;
  dy = clamp(dy, -0.85, 0.85);
  if (!fpDir) fpDir = [dx, dy, dz];
  var k = Math.min(1, dt * 10);
  fpDir[0] += (dx - fpDir[0]) * k; fpDir[1] += (dy - fpDir[1]) * k; fpDir[2] += (dz - fpDir[2]) * k;
  var fl = Math.hypot(fpDir[0], fpDir[2]) || 1, hx = fpDir[0] / fl, hz = fpDir[2] / fl;
  var rx = -hz, rz = hx;                       // the player's right
  var px, py, pz;
  // WHICH SHOULDER: the file's aim-origin side, per record. Unset is taken as the
  // right shoulder (the default is assumed, not read from the file).
  var iSd = R.meta && R.meta.positionCell ? R.meta.positionCell.indexOf('aimOriginSide') : -1;
  var sd = (iSd >= 0 && c && c[iSd]) ? -1 : 1;
  if (followStyle === 2) { px = ex + hx * 120; py = ey; pz = ez + hz * 120; }
  else if (followStyle === 3) {
    // CHASE can be swung round the player (user, same day): a drag turns the
    // ordinary orbit angles, and whatever they moved by since the last frame is
    // added to this camera's own offsets. It always looks at the player.
    if (chasePrev) { chaseAz += orbit.az - chasePrev[0]; chaseEl += orbit.el - chasePrev[1]; }
    chasePrev = [orbit.az, orbit.el];
    chaseEl = clamp(chaseEl, -0.35, 1.25);
    var cA = Math.cos(chaseAz), sA = Math.sin(chaseAz), bx0 = -hx, bz0 = -hz;
    var bx1 = bx0 * cA + bz0 * sA, bz1 = -bx0 * sA + bz0 * cA;
    // IT LOOKS WHERE THEY LOOK (user, 2026-10-09: "Chase should follow where theyre looking"). It already stood behind the
    // player along their aim, but it kept a fixed height and stared at their back. Now their up / down look tips the
    // camera the opposite way round them (look up -> the camera drops and looks up past them), and it looks at a point 4 m
    // out along the aim instead of at the player, so what they are looking at is in the middle of the picture.
    var pitchC = Math.asin(clamp(fpDir[1], -0.85, 0.85));
    var elC = clamp(Math.atan2(1100, 6200) + chaseEl - pitchC * 0.75, -0.3, 1.3), RC = Math.hypot(6200, 1100);
    var tyC = p.y + 800 + 300;
    px = ex + bx1 * Math.cos(elC) * RC; py = tyC + Math.sin(elC) * RC; pz = ez + bz1 * Math.cos(elC) * RC;
    cam.position.set((px - ctr[0]) * S, py * S, (pz - ctr[2]) * S);
    // a drag that has swung the camera round still looks at the player: only the un-turned camera leads the view
    var leadC = Math.abs(chaseAz) < 0.2 ? 4000 : 0;
    cam.lookAt((ex + fpDir[0] * leadC - ctr[0]) * S, (tyC + 500 + fpDir[1] * leadC) * S, (ez + fpDir[2] * leadC - ctr[2]) * S);
    orbit.tx = (ex - ctr[0]) * S; orbit.ty = ey * S; orbit.tz = (ez - ctr[2]) * S;
    scene.fog.near = 40; scene.fog.far = 900 + span;
    return true;
  }
  else { px = ex - hx * 1900 + rx * 520 * sd; py = ey + 250 - fpDir[1] * 600; pz = ez - hz * 1900 + rz * 520 * sd; }
  cam.position.set((px - ctr[0]) * S, py * S, (pz - ctr[2]) * S);
  cam.lookAt((px + fpDir[0] * 20000 - ctr[0]) * S, (py + fpDir[1] * 20000) * S, (pz + fpDir[2] * 20000 - ctr[2]) * S);
  orbit.tx = (ex - ctr[0]) * S; orbit.ty = ey * S; orbit.tz = (ez - ctr[2]) * S;
  scene.fog.near = 40; scene.fog.far = 900 + span;
  return true;
}
function updateCamera(dt) {
  // TEST HOOK (2026-10-08): window.__camLock = { player, yaw, dist, up, look } pins the camera on a circle round one player,
  // so a headless check can look at a prop or a pose from a chosen side. The follow camera kept ending up inside walls and
  // the free camera ignores a position set from script, which left a day's animations wired without being seen.
  if (window.__camLock && cam) {
    var CL = window.__camLock, clp = live[CL.player];
    if (clp) {
      var cgx = (clp.x - ctr[0]) * S, cgy = clp.y * S, cgz = (clp.z - ctr[2]) * S, cya = CL.yaw || 0, cdi = CL.dist || 24;
      cam.position.set(cgx + Math.sin(cya) * cdi, cgy + (CL.up == null ? 14 : CL.up), cgz + Math.cos(cya) * cdi);
      cam.lookAt(cgx, cgy + (CL.look == null ? 10 : CL.look), cgz);
      return;
    }
  }
  pollPad(dt);
  if (camMode === 'free') { updateFreeCamera(dt); return; }
  if (camMode === 'follow' && followStyle && follow >= 0 && !camSubj && cam && updateBodyCamera(dt)) return;
  var tgt = new THREE.Vector3(orbit.tx, orbit.ty, orbit.tz);
  var subj = camMode === 'follow' ? camSubjPoint() : null;
  if (subj || (camMode === 'follow' && follow >= 0 && live[follow])) {
    var p = subj ? { x: subj[0], y: subj[1] - 10 / S + subj[3], z: subj[2] } : live[follow];
    tgt.set((p.x - ctr[0]) * S, p.y * S + 10, (p.z - ctr[2]) * S);
    orbit.tx = lerp(orbit.tx, tgt.x, Math.min(1, dt * 6));
    orbit.ty = lerp(orbit.ty, tgt.y, Math.min(1, dt * 6));
    orbit.tz = lerp(orbit.tz, tgt.z, Math.min(1, dt * 6));
    tgt.set(orbit.tx, orbit.ty, orbit.tz);
  }
  var el = camMode === 'top' ? 1.4 : orbit.el;
  var az = camMode === 'top' ? 0 : orbit.az;
  var dist = camMode === 'top' ? clamp(span * 0.8 * aspectPad(), 60, 8000)
           : camMode === 'follow' ? clamp(orbit.dist, 5, 220) : orbit.dist;
  cam.position.set(
    tgt.x + Math.cos(el) * Math.sin(az) * dist,
    tgt.y + Math.sin(el) * dist,
    tgt.z + Math.cos(el) * Math.cos(az) * dist);
  cam.lookAt(tgt);
  scene.fog.near = dist * 0.35;
  scene.fog.far = dist * 3.6 + span;
}

// Base labels and capture bars. Drawn before the nametags so a player always
// reads on top of the furniture.
//
// The bar is centre-split on purpose: the gauge is an OWNERSHIP axis, not a
// progress-to-100% counter, so "how close is this to being capped" only means
// anything relative to the middle. Full left is one team, full right the
// other, and a bar sitting at the centre is genuinely contested.
function drawBaseHUD(x, w, h, v) {
  if (!showObj) return;
  var tc = baseTeamColors();
  // spawn labels, coloured by whoever is using that corner right now
  var sps = (R && R.spawns) || [];
  var gls = objMarks.filter(function (m) { return m.goal && m.group.visible && m.asGoal !== false; });
  for (var gq = 0; gq < gls.length; gq++) {
    var gm = gls[gq];
    v.set((gm.wx - ctr[0]) * S, gm.wy * S + 4, (gm.wz - ctr[2]) * S).project(cam);
    if (v.z > 1) continue;
    var gx_ = (v.x * 0.5 + 0.5) * w, gy_ = (-v.y * 0.5 + 0.5) * h;
    if (gx_ < -60 || gx_ > w + 60 || gy_ < -40 || gy_ > h + 40) continue;
    x.font = '500 10px "MGO2 UI","IBM Plex Mono",monospace';
    x.textAlign = 'center'; x.textBaseline = 'middle';
    var gt = gm.cur || [];
    x.globalAlpha = 0.9;
    if (gt.length === 2) {
      x.fillStyle = teamColor(gt[0]); x.fillText('GO', gx_ - 8, gy_);
      x.fillStyle = teamColor(gt[1]); x.fillText('AL', gx_ + 8, gy_);
    } else {
      x.fillStyle = gt.length ? teamColor(gt[0]) : 'rgba(201,208,194,.8)';
      x.fillText('GOAL', gx_, gy_);
    }
    // a goal the stage file does not name, placed from this match's puts
    if (gm.o && gm.o.derived) {
      x.font = '500 8px "MGO2 UI","IBM Plex Mono",monospace';
      x.fillStyle = 'rgba(201,208,194,.7)';
      x.fillText('measured', gx_, gy_ + 10);
    }
    x.globalAlpha = 1;
  }
  if (R && R.race) {
    R.race.corners.forEach(function (g) {
      if (!g.role) return;
      v.set((g.x - ctr[0]) * S, g.y * S + 4, (g.z - ctr[2]) * S).project(cam);
      if (v.z > 1) return;
      var rx = (v.x * 0.5 + 0.5) * w, ry = (-v.y * 0.5 + 0.5) * h;
      if (rx < -60 || rx > w + 60 || ry < -40 || ry > h + 40) return;
      x.font = (g.role.kind !== 'SPAWN' ? '600 11px' : '500 10px') + ' "MGO2 UI","IBM Plex Mono",monospace';
      x.textAlign = 'center'; x.textBaseline = 'middle';
      x.fillStyle = teamColor(g.role.team);
      x.globalAlpha = g.role.kind === 'GOAL' ? 1 : 0.8;
      x.fillText(g.role.kind, rx, ry);
      x.globalAlpha = 1;
    });
  }
  // ITEM BOXES: a label over each one on the ground, and over whoever carries it
  if (R && R.boxes) {
    var boxTag = function (wx, wy, wz, up, info, text) {
      v.set((wx - ctr[0]) * S, wy * S + up, (wz - ctr[2]) * S).project(cam);
      if (v.z > 1) return;
      var bx_ = (v.x * 0.5 + 0.5) * w, by_ = (-v.y * 0.5 + 0.5) * h;
      if (bx_ < -60 || bx_ > w + 60 || by_ < -40 || by_ > h + 40) return;
      x.font = '600 10px "MGO2 UI","IBM Plex Mono",monospace';
      x.textAlign = 'center'; x.textBaseline = 'middle';
      var tw_ = x.measureText(text).width + 8;
      x.fillStyle = 'rgba(11,13,12,.72)';
      x.fillRect(bx_ - tw_ / 2, by_ - 7, tw_, 14);
      x.fillStyle = info.css;
      x.fillText(text, bx_, by_);
    };
    R.boxes.lie.forEach(function (bs) {
      if (T >= bs.t0 && T < bs.t1) boxTag(bs.x, bs.y, bs.z, 14, BOX_ITEMS[bs.item], BOX_ITEMS[bs.item].label);
    });
    bombSites().forEach(function (st_) { boxTag(st_.x, st_.y, st_.z, 4, { css: '#E8C547' }, st_.label); });
    var lse = bombLooseAt(T);
    if (lse) {
      // the countdown runs to the file's own 'home' record for this drop when there is one
      // (the gap is 15-30 s, not a fixed 15); a bomb that has just respawned says so
      var bl = bombLosses(), nextL = null, lastB = null, btxt = 'BOMB';
      for (var bq = 0; bq < bl.length; bq++) { if (bl[bq].t <= T) lastB = bl[bq]; else if (!nextL) nextL = bl[bq]; }
      if (lse.state === 'dropped') {
        var gone = nextL && nextL.how === 'timed out' && nextL.t - lse.t0 <= 31 ? nextL.t : null;
        if (gone != null) btxt = 'BOMB  lost in ' + Math.max(0, Math.ceil(gone - T)) + 's';
      } else if (lastB && T - lastB.t < 6 && lastB.left) {
        btxt = 'NEW BOMB  ' + lastB.left + ' left';
      }
      boxTag(lse.x, lse.y, lse.z, 9, { css: '#E2574C' }, btxt);
    }
    var lbm = liveBombAt(T);
    if (lbm && lbm.x != null) {
      var left_ = Math.max(0, Math.ceil(lbm.t + (lbm.exact ? BOMB_FUSE_EXACT : BOMB_FUSE) - T)), fzp = bombGaugeAt(lbm, T);
      boxTag(lbm.x, lbm.y != null ? lbm.y : 0, lbm.z, 12, { css: fzp ? '#7FD4F0' : '#E2574C' },
             'BOMB ' + left_ + 's' + (fzp ? '  ' + Math.round(clamp(fzp, 0, 1) * 100) + '%' : ''));
    }
    R.boxes.carry.forEach(function (cs) {
      var lp = live[cs.i];
      if (T >= cs.t0 && T < cs.t1 && lp && lp.alive) boxTag(lp.x, lp.y, lp.z, 7, BOX_ITEMS[cs.item], BOX_ITEMS[cs.item].label);
    });
  }
  // RESET COUNTDOWN over each dropped target: the seconds left before the game
  // sends it back (Race and Capture; dropResetS() after the drop)
  var rmode = R && R.meta && R.meta.modeName;
  if (rmode === 'RACE' || rmode === 'CAP') {
    for (var dq = 0; dq < dropSpans.length; dq++) {
      var dd = dropSpans[dq];
      if (T < dd.t0 || T >= dd.t1) continue;
      v.set((dd.x - ctr[0]) * S, dd.y * S + 22, (dd.z - ctr[2]) * S).project(cam);
      if (v.z > 1) continue;
      var dx_ = (v.x * 0.5 + 0.5) * w, dy_ = (-v.y * 0.5 + 0.5) * h;
      var left = Math.max(0, Math.ceil(dd.t0 + dropResetS() - T));
      var txt = left + 's';
      x.font = '600 12px "MGO2 UI","IBM Plex Mono",monospace';
      x.textAlign = 'center'; x.textBaseline = 'middle';
      var tw = x.measureText(txt).width + 10;
      x.fillStyle = 'rgba(11,13,12,.78)';
      x.fillRect(dx_ - tw / 2, dy_ - 9, tw, 18);
      x.strokeStyle = dd.team == null ? '#C9D0C2' : teamColor(dd.team);
      x.lineWidth = 1;
      x.strokeRect(dx_ - tw / 2 + .5, dy_ - 9 + .5, tw - 1, 17);
      // the bar under the number drains over the 30 s
      x.fillStyle = x.strokeStyle;
      x.fillRect(dx_ - tw / 2 + 1, dy_ + 7, (tw - 2) * clamp((dd.t0 + dropResetS() - T) / dropResetS(), 0, 1), 2);
      x.fillStyle = left <= 5 ? '#E2574C' : '#E6EADF';
      x.fillText(txt, dx_, dy_);
    }
  }
  for (var q = 0; q < sps.length; q++) {
    var sp = sps[q];
    // a spawn corner that is the goal RIGHT NOW is labelled as the goal, once;
    // when the goal moves away it reads SPAWN again
    if (sp.goalNow) continue;
    v.set((sp.wx - ctr[0]) * S, sp.wy * S + 4, (sp.wz - ctr[2]) * S).project(cam);
    if (v.z > 1) continue;
    var qx = (v.x * 0.5 + 0.5) * w, qy = (-v.y * 0.5 + 0.5) * h;
    if (qx < -60 || qx > w + 60 || qy < -40 || qy > h + 40) continue;
    x.font = '500 10px "MGO2 UI","IBM Plex Mono",monospace';
    x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillStyle = sp.cur == null ? 'rgba(124,136,120,.7)' : teamColor(sp.cur);
    x.globalAlpha = 0.85;
    x.fillText('SPAWN', qx, qy);
    x.globalAlpha = 1;
  }
  if (!objMarks.length) return;
  for (var k = 0; k < objMarks.length; k++) {
    var mk = objMarks[k];
    if (mk.base == null && !mk.spawn) continue;
    v.set((mk.wx - ctr[0]) * S, mk.wy * S + 6, (mk.wz - ctr[2]) * S).project(cam);
    if (v.z > 1) continue;
    var sx = (v.x * 0.5 + 0.5) * w, sy = (-v.y * 0.5 + 0.5) * h;
    if (sx < -60 || sx > w + 60 || sy < -40 || sy > h + 40) continue;


    var st = baseStateAt(mk.base, T);
    var lab = String(mk.base + 1);
    var BW = 54, BH = 6, by = sy - 20, bx = sx - BW / 2;

    // the number, in a chip so it stays readable over any geometry
    x.font = '600 13px "MGO2 UI","IBM Plex Mono",monospace';
    x.textAlign = 'center'; x.textBaseline = 'middle';
    var own = st.owner === 0 ? '#C9D0C2' : (st.owner > 0 ? tc[0] : tc[1]);
    x.fillStyle = 'rgba(11,13,12,.78)';
    x.fillRect(sx - 11, by - 20, 22, 17);
    x.strokeStyle = own;
    x.globalAlpha = 0.85;
    x.strokeRect(sx - 10.5, by - 19.5, 21, 16);
    x.globalAlpha = 1;
    x.fillStyle = own;
    x.fillText(lab, sx, by - 11.5);

    // The bar is progress toward the NEXT capture, in the colour of whoever is
    // pushing it -- a single left-to-right fill, because progress belongs to
    // one side at a time and resets to nothing when the other side interrupts.
    x.fillStyle = 'rgba(11,13,12,.8)';
    x.fillRect(bx, by, BW, BH);
    if (st.prog > 0.02) {
      x.fillStyle = st.sign > 0 ? tc[0] : tc[1];
      x.globalAlpha = st.prog >= 1 ? 1 : 0.75;
      x.fillRect(bx + 1, by + 1, (BW - 2) * st.prog, BH - 2);
      x.globalAlpha = 1;
    }
    x.strokeStyle = 'rgba(78,87,75,.9)';
    x.strokeRect(bx + .5, by + .5, BW - 1, BH - 1);
  }
}

// ── team spawns, measured from the replay ────────────────────────────────
var spawnGroup = null;
function buildSpawns() {
  if (!R || R.spawns) return;
  // CAPTURE: respawns come from a pool both teams share. Tomb of Tubes,
  // replay_284191_13, first half: team 0 respawned 6 times at (34, 0) m and
  // team 2 six times at (33, -6) m, 6 m apart; other pool points are 9 m
  // apart or on top of each other. A pool point has no team, so the generic
  // top-four-cells pass labelled mid-map points SPAWN in whichever colour used
  // them last. What IS a team's spawn in Capture is where its six stand when a
  // half starts (6 of 6 within 3 m on both sides, both halves); the teams swap
  // ends at the break, which spawnTeamAt() reads from the most recent use.
  // Rescue and Team Sneaking have NO respawns (measured: 0 get-ups, the only
  // dead-to-alive flips are the next half's start), so their spawns are the
  // formations too. Bomb and Deathmatch keep the respawn clusters below: Bomb
  // respawns 90-110 times a match and its defuse makes the clock jump, which
  // roundStarts() reads as a half (52733 at 281 s: 3 of 6 together, not 6).
  // Base: respawns are at the team's HOME, which is its formation spot (n010a:
  // 57 of 114 at (50, -3), 54 at (-10, -18); n013a: 47 at (53, 6), 47 at
  // (-48, -6)), 16-34 m from the nearest capture point -- so the formation is
  // the same answer without the 12 m base-exclusion ever eating a home.
  if (R.meta && /^(CAP|RES|TSNE|BASE)$/.test(R.meta.modeName) && R.frames && R.frames.length) {
    var starts = [0].concat(roundStarts()), capSpawns = [];
    for (var si = 0; si < starts.length; si++) {
      var firstPos = {}, fi = frameAtT(starts[si]);
      // frameAtT gives the LAST frame at or before the start: on a half change
      // that is the final frame of the previous half, whose cells are where
      // everyone stood at the buzzer (Base 52721: scattered, so no formation
      // was found and the second half kept the first half's colours).
      if (R.frames[fi].t < starts[si]) fi++;
      for (; fi < R.frames.length && R.frames[fi].t < starts[si] + 6; fi++) {
        var row0 = R.frames[fi].p || [];
        for (var pi = 0; pi < row0.length; pi++) {
          var c0 = row0[pi];
          if (c0 && c0[0] != null && firstPos[pi] === undefined) firstPos[pi] = c0;
        }
      }
      var byTeam = {};
      Object.keys(firstPos).forEach(function (pk) {
        var pl = R.players[+pk];
        if (!pl || pl.team == null) return;
        (byTeam[pl.team] || (byTeam[pl.team] = [])).push({ i: +pk, c: firstPos[pk] });
      });
      Object.keys(byTeam).forEach(function (tm) {
        var mem = byTeam[tm];
        if (mem.length < 3) return;
        var mx = medianOf(mem.map(function (o) { return o.c[0]; }));
        var my = medianOf(mem.map(function (o) { return o.c[1]; }));
        var mz = medianOf(mem.map(function (o) { return o.c[2]; }));
        var tight = mem.filter(function (o) { return Math.hypot(o.c[0] - mx, o.c[2] - mz) < 8000; });
        // a real formation is the whole team (6 of 6 in 60 of 60 half starts measured)
        if (tight.length < 4 || tight.length < mem.length - 1) return;
        var sp = null;
        for (var q = 0; q < capSpawns.length; q++) {
          if (Math.hypot(capSpawns[q].x - mx, capSpawns[q].z - mz) < 10000) { sp = capSpawns[q]; break; }
        }
        if (!sp) { sp = { x: mx, y: my, z: mz, n: 0, half: 4000, uses: [] }; capSpawns.push(sp); }
        sp.n += tight.length;
        var t0 = starts[si];
        tight.forEach(function (o) { sp.uses.push([t0, o.i]); });
        sp.uses.sort(function (a, b) { return a[0] - b[0]; });
      });
    }
    if (capSpawns.length) { R.spawns = capSpawns; return; }
  }
  var bases = [];
  for (var q = 0; q < objMarks.length; q++) {
    if (objMarks[q].base != null) {
      bases.push({ x: objMarks[q].wx, z: objMarks[q].wz });
    }
  }
  // every dead -> alive transition, with who and when
  var ev = [], prev = {};
  var fr = R.frames;
  for (var k = 0; k < fr.length; k++) {
    var row = fr[k].p;
    for (var i = 0; i < row.length; i++) {
      var c = row[i];
      if (!c || c[0] == null) continue;
      var al = !!c[4];
      if (prev[i] === false && al) {
        var onBase = false;
        for (var b = 0; b < bases.length; b++) {
          if (Math.hypot(c[0] - bases[b].x, c[2] - bases[b].z) < 12000) { onBase = true; break; }
        }
        if (!onBase) {
          ev.push({ t: fr[k].t, i: i, x: c[0], y: c[1], z: c[2] });
        }
      }
      prev[i] = al;
    }
  }
  // cluster on a 10 m grid, then keep anything with real support
  var cells = {};
  for (var e = 0; e < ev.length; e++) {
    var key = Math.round(ev[e].x / 10000) + ',' + Math.round(ev[e].z / 10000);
    (cells[key] || (cells[key] = [])).push(ev[e]);
  }
  var out = [];
  for (var ck in cells) {
    var mem = cells[ck];
    if (mem.length < 4) continue;
    mem.sort(function (a, b) { return a.x - b.x; });
    var mx = mem[mem.length >> 1].x;
    var my = mem[mem.length >> 1].y;
    mem.sort(function (a, b) { return a.z - b.z; });
    var mz = mem[mem.length >> 1].z;
    var lo = Math.floor(mem.length * 0.1), hi = Math.ceil(mem.length * 0.9) - 1;
    var ez = Math.abs(mem[hi].z - mem[lo].z);
    mem.sort(function (a, b) { return a.x - b.x; });
    var ex = Math.abs(mem[hi].x - mem[lo].x);
    mem.sort(function (a, b) { return a.t - b.t; });
    out.push({ x: mx, y: my, z: mz, n: mem.length,
               half: clamp(Math.max(ex, ez) / 2, 2500, 12000),
               uses: mem.map(function (o) { return [o.t, o.i]; }) });
  }
  // A grid cut can split one spawn across neighbouring cells -- Groznyj Grad
  // produced cells at x=59000 and x=61000 for the same corner, which drew the
  // label twice. Merge anything closer than 15 m and keep the uses together.
  out.sort(function (a, b) { return b.n - a.n; });
  var merged = [];
  for (var o = 0; o < out.length; o++) {
    var got = null;
    for (var mI = 0; mI < merged.length; mI++) {
      if (Math.hypot(merged[mI].x - out[o].x, merged[mI].z - out[o].z) < 15000) {
        got = merged[mI]; break;
      }
    }
    if (got) {
      got.uses = got.uses.concat(out[o].uses);
      got.uses.sort(function (a, b) { return a[0] - b[0]; });
      got.n += out[o].n;
      got.half = Math.max(got.half, out[o].half);
    } else {
      merged.push(out[o]);
    }
  }
  R.spawns = merged.slice(0, 4);
}

// Who is spawning here at time t? The most recent use, because the two teams
// trade spawns at the round change.
function spawnTeamAt(sp, t) {
  var team = null;
  for (var k = 0; k < sp.uses.length; k++) {
    if (sp.uses[k][0] > t) break;
    var pl = R.players[sp.uses[k][1]];
    if (pl) team = pl.team;
  }
  return team;
}

function buildSpawnMeshes() {
  if (!R || !R.spawns) return;
  spawnGroup = new THREE.Group();
  R.spawns.forEach(function (sp) {
    var x = (sp.x - ctr[0]) * S, y = sp.y * S, z = (sp.z - ctr[2]) * S;
    var h = sp.half * S;
    var fill = new THREE.Mesh(new THREE.PlaneGeometry(h * 2, h * 2),
      new THREE.MeshBasicMaterial({ color: 0x8A8F86, transparent: true,
                                    opacity: 0.10, side: THREE.DoubleSide,
                                    depthTest: false }));
    fill.rotation.x = -Math.PI / 2;
    fill.position.set(x, y + 0.4, z);
    fill.renderOrder = 4;
    var eg = new THREE.BufferGeometry();
    eg.setAttribute('position', new THREE.BufferAttribute(new Float32Array([
      x - h, y + 0.6, z - h, x + h, y + 0.6, z - h,
      x + h, y + 0.6, z + h, x - h, y + 0.6, z + h]), 3));
    var edge = new THREE.LineLoop(eg, new THREE.LineBasicMaterial({
      color: 0x8A8F86, transparent: true, opacity: 0.6, depthTest: false }));
    edge.renderOrder = 5;
    spawnGroup.add(fill); spawnGroup.add(edge);
    sp.fill = fill; sp.edge = edge; sp.wx = sp.x; sp.wy = sp.y; sp.wz = sp.z;
  });
  spawnGroup.visible = showObj;
  scene.add(spawnGroup);
}

// ── RACE: goals that turn into spawns ────────────────────────────────────
// How the mode works (user, 2026-10-04, drawn on a Blood Bath screenshot): each
// team starts at its spawn with a goal elsewhere. When it SCORES, the goal it
// scored at becomes its new spawn and a new goal is chosen. The PRP_RUGBY_GOAL
// squares and the map centre are not where the first goals are.
// None of that is a field in the file, but it leaves a trail: a team's respawn
// corner moves, and one of its carriers reaches the new corner just before.
// MEASURED on that match, team Alpha round 1: starts at (-23, 22); a carrier
// reaches (21, 6) at 0:19 and the team respawns there from 0:32 (4 in a row);
// reaches (-23, 21) at 2:28, respawns there from 2:33; reaches (23, 24) at 3:53,
// respawns there from 4:10. So:
//     a team's SPAWN at time t = the corner it most recently scored at
//     a team's GOAL  at time t = the corner it scores at NEXT
// A corner counts as the new spawn when 2 of the team's next 3 real respawns
// are on it. The last goal of a round, never reached, cannot be known and is
// not drawn; neither is a goal before the target's last reset (it moves then).
var raceGroup = null, RACE_CORNER_MM = 12000, RACE_HALF_MM = 3000;

// THE FILE DOES SAY (found 2026-10-04, after the inference below was built):
// the rules object writes a record at every score and every reset --
// meta.raceOps = {t, score, team, goal, spawn} -- naming the team's NEW goal
// and its spawn by point number, and the stage file has those points
// (race_points.json, from replay/race_points.py). With both in hand nothing is
// guessed: the goal a team is after is known at every moment after its first
// record of the round. Before that first record: the spawn is the team's base
// (point 0 or 1, whichever it starts on) and the goal is known only when the
// first record is a score (it names the goal just scored as the new spawn).
var RACE_PTS, racePtsAsked = false;
function loadRacePoints() {
  if (racePtsAsked) return;
  racePtsAsked = true;
  fetch(av('race_points.json')).then(function (r) { return r.ok ? r.json() : null; })
    .then(function (j) { RACE_PTS = j || null; if (R) R.race = undefined; })
    .catch(function () { RACE_PTS = null; });
}
function racePointsFor() {
  if (!RACE_PTS) return null;
  var st = (M && M.stage) || R.__fit || (R.stage ? String(R.stage).split('__')[0] : null);
  if (!st) return null;
  var best = null, bn = 0;
  Object.keys(RACE_PTS).forEach(function (k) {
    if (k.split('__')[0] !== st) return;
    // the DLC geom is the one the game loads; otherwise the fullest table
    var n = Object.keys(RACE_PTS[k]).length + (/__dlp_/.test(k) ? 100 : 0);
    if (n > bn) { bn = n; best = RACE_PTS[k]; }
  });
  return best;
}
function raceMeshes(corners) {
  raceGroup = new THREE.Group();
  corners.forEach(function (g) {
    var x = (g.x - ctr[0]) * S, y = g.y * S, z = (g.z - ctr[2]) * S, h = RACE_HALF_MM * S;
    g.fill = new THREE.Mesh(new THREE.PlaneGeometry(h * 2, h * 2),
      new THREE.MeshBasicMaterial({ color: 0x8A8F86, transparent: true, opacity: 0.2,
                                    side: THREE.DoubleSide, depthTest: false }));
    g.fill.rotation.x = -Math.PI / 2;
    g.fill.position.set(x, y + 0.5, z);
    g.fill.renderOrder = 4;
    var eg = new THREE.BufferGeometry();
    eg.setAttribute('position', new THREE.BufferAttribute(new Float32Array([
      x - h, y + 0.7, z - h, x + h, y + 0.7, z - h, x + h, y + 0.7, z + h, x - h, y + 0.7, z + h]), 3));
    g.edge = new THREE.LineLoop(eg, new THREE.LineBasicMaterial({
      color: 0x8A8F86, transparent: true, opacity: 0.9, depthTest: false }));
    g.edge.renderOrder = 5;
    raceGroup.add(g.fill); raceGroup.add(g.edge);
  });
  scene.add(raceGroup);
}
function buildRaceExact(ops, pts) {
  var P = {}, corners = [];
  Object.keys(pts).forEach(function (k) {
    if (!/^\d+$/.test(k)) return;               // "first" is the first-goal hint, not a point
    P[k] = { x: pts[k][0], y: pts[k][1], z: pts[k][2], id: +k };
    corners.push(P[k]);
  });
  var rounds = [0].concat(roundStarts()), fr = R.frames, teams = [], seq = {}, legs = {};
  R.players.forEach(function (pl) { if (pl.team != null && teams.indexOf(pl.team) < 0) teams.push(pl.team); });
  teams.sort();
  // A point number with no entity in the table (Coppertown uses an 11): learn it
  // from the file -- the middle of the team's real respawns while it is the spawn.
  var need = {};
  ops.forEach(function (o) { if (!P[o.goal]) need[o.goal] = 1; if (!P[o.spawn]) need[o.spawn] = 1; });
  if (Object.keys(need).length) {
    var prevA = {}, fellAt = {}, got = {};
    for (var fk = 0; fk < fr.length; fk++) {
      var rowK = fr[fk].p || [];
      for (var pk2 = 0; pk2 < rowK.length; pk2++) {
        var ck = rowK[pk2];
        if (!ck || ck[0] == null) continue;
        if (prevA[pk2] === false && ck[4] && R.players[pk2].team != null &&
            !(fellAt[pk2] && Math.hypot(ck[0] - fellAt[pk2][0], ck[2] - fellAt[pk2][1]) < 3000)) {
          var bitK = teams.indexOf(R.players[pk2].team), cur = null;
          for (var ok = 0; ok < ops.length && ops[ok].t <= fr[fk].t - 1; ok++) if (ops[ok].team === bitK) cur = ops[ok];
          if (cur && need[cur.spawn]) (got[cur.spawn] || (got[cur.spawn] = [])).push(ck);
        }
        if (!ck[4]) fellAt[pk2] = [ck[0], ck[2]];
        prevA[pk2] = !!ck[4];
      }
    }
    Object.keys(got).forEach(function (k) {
      var a_ = got[k];
      if (a_.length < 3) return;
      var xs = a_.map(function (c3) { return c3[0]; }).sort(function (u, w) { return u - w; });
      var zs = a_.map(function (c3) { return c3[2]; }).sort(function (u, w) { return u - w; });
      P[k] = { x: xs[xs.length >> 1], y: a_[0][1], z: zs[zs.length >> 1], id: +k, learned: true };
      corners.push(P[k]);
    });
  }
  teams.forEach(function (tm, bit) {
    var S_ = seq[tm] = [], L = legs[tm] = [];
    for (var ri = 0; ri < rounds.length; ri++) {
      var t0 = rounds[ri], t1 = ri + 1 < rounds.length ? rounds[ri + 1] : (R.duration || t0) + 1;
      var mine = ops.filter(function (o) { return o.team === bit && o.t >= t0 && o.t < t1; });
      // the base the team opens the round on: point 0 or 1, whichever it stands nearer
      var f0 = Math.min(frameIndexAtIn(R, t0 + 3), fr.length - 1), sx = 0, sz = 0, sn = 0;
      (fr[f0].p || []).forEach(function (c2, pi) {
        if (c2 && c2[0] != null && R.players[pi].team === tm) { sx += c2[0]; sz += c2[2]; sn++; }
      });
      var base = null;
      if (sn && P[0] && P[1]) {
        base = Math.hypot(P[0].x - sx / sn, P[0].z - sz / sn) <= Math.hypot(P[1].x - sx / sn, P[1].z - sz / sn) ? P[0] : P[1];
      }
      S_.push({ t: t0, corner: base, round: ri });
      var first = mine[0];
      // the opening goal: named by the first record when that is a score;
      // otherwise the goal this stage has been SEEN to give a team on that base
      // (race_points.py FIRST_GOAL), drawn as an estimate
      var open0 = (first && first.score && P[first.spawn]) ? P[first.spawn] : null, est0 = false;
      if (!open0 && base && pts.first && pts.first[base.id] != null && P[pts.first[base.id]]) {
        open0 = P[pts.first[base.id]]; est0 = true;
      }
      L.push({ t0: t0, t1: first ? first.t : t1, est: est0, corner: open0 });
      mine.forEach(function (o, k) {
        S_.push({ t: o.t, corner: P[o.spawn] || null, round: ri });
        L.push({ t0: o.t, t1: k + 1 < mine.length ? mine[k + 1].t : t1, corner: P[o.goal] || null, est: false });
      });
    }
  });
  raceMeshes(corners);
  R.race = { corners: corners, seq: seq, teams: teams, legs: legs, exact: true };
  try { window.__mgo2Race = R.race; } catch (e) {}
}
function buildRace() {
  if (raceGroup) { scene.remove(raceGroup); raceGroup = null; }
  R.race = null;
  if (!R.meta || R.meta.modeName !== 'RACE' || !R.frames.length) return;
  if (R.meta.raceOps && R.meta.raceOps.length) {
    loadRacePoints();
    var rp = racePointsFor();
    if (rp) { buildRaceExact(R.meta.raceOps, rp); return; }
  }
  // ---- no records (an older parse) or no point table: the inference ----
  var resp = [], prev = {}, fell = {}, fr = R.frames, k, i;
  for (k = 0; k < fr.length; k++) {
    var row = fr[k].p || [];
    for (i = 0; i < row.length; i++) {
      var c = row[i];
      if (!c || c[0] == null) continue;
      // A REAL respawn puts the player somewhere else. Coming back to life on
      // the very spot he went down is a get-up (stun worn off, woken by a
      // team-mate): on this match those were about half of all dead-to-alive
      // flips and every one of the stray "respawns" away from the spawn.
      if (prev[i] === false && c[4] && R.players[i].team != null &&
          !(fell[i] && Math.hypot(c[0] - fell[i][0], c[2] - fell[i][1]) < 3000)) {
        resp.push({ t: fr[k].t, x: c[0], y: c[1], z: c[2], team: R.players[i].team });
      }
      if (!c[4]) fell[i] = [c[0], c[2]];
      prev[i] = !!c[4];
    }
  }
  // when each team's target was last SENT HOME (dropped and left for 30 s): the
  // goal moves then, so a goal is only known from the last reset before the
  // team scored on it. Earlier than that it was somewhere else, unknown.
  if (!carrySpans) buildCarrySpans();
  var resets = {};
  dropSpans.forEach(function (d) {
    if (d.team != null && (d.home || d.t1 - d.t0 >= dropResetS() - 0.01)) (resets[d.team] || (resets[d.team] = [])).push(d.t1);
  });
  var corners = [];
  function cornerOf(x, y, z, make) {
    for (var q = 0; q < corners.length; q++) {
      if (Math.hypot(corners[q].x - x, corners[q].z - z) < RACE_CORNER_MM) return corners[q];
    }
    if (!make) return null;
    var g = { x: x, y: y, z: z, n: 0, id: corners.length };
    corners.push(g);
    return g;
  }
  resp.forEach(function (r) {
    var g = cornerOf(r.x, r.y, r.z, true);
    g.x = (g.x * g.n + r.x) / (g.n + 1); g.z = (g.z * g.n + r.z) / (g.n + 1); g.n++;
    r.c = g;
  });
  var rounds = [0].concat(roundStarts()), teams = [];
  R.players.forEach(function (pl) { if (pl.team != null && teams.indexOf(pl.team) < 0) teams.push(pl.team); });
  if (!carrySpans) buildCarrySpans();
  var seq = {};
  teams.forEach(function (tm) {
    var out = seq[tm] = [];
    for (var ri = 0; ri < rounds.length; ri++) {
      var t0 = rounds[ri], t1 = ri + 1 < rounds.length ? rounds[ri + 1] : Infinity;
      // where the team stands as the round opens = its first spawn
      var f0 = Math.min(frameIndexAtIn(R, t0 + 3), fr.length - 1), sx = 0, sy = 0, sz = 0, sn = 0;
      (fr[f0].p || []).forEach(function (c2, pi) {
        if (c2 && c2[0] != null && R.players[pi].team === tm) { sx += c2[0]; sy += c2[1]; sz += c2[2]; sn++; }
      });
      var cur = sn ? cornerOf(sx / sn, sy / sn, sz / sn, true) : null;
      out.push({ t: t0, corner: cur, round: ri });
      var mine = resp.filter(function (r) { return r.team === tm && r.t >= t0 && r.t < t1; });
      var lastScore = t0;
      for (var m = 0; m < mine.length; m++) {
        var cand = mine[m].c;
        if (cand === cur) continue;
        var hits = 0;
        for (var w = m; w < Math.min(m + 3, mine.length); w++) if (mine[w].c === cand) hits++;
        if (hits < 2) continue;
        // the score itself: the team's last carrier to reach that corner before the first respawn on it
        var ts = mine[m].t;
        (carrySpans || []).forEach(function (sp) {
          if (!R.players[sp.i] || R.players[sp.i].team !== tm || sp.t0 > mine[m].t || sp.t1 < lastScore) return;
          for (var f = frameIndexAt(Math.max(sp.t0, lastScore)); f < fr.length && fr[f].t < Math.min(sp.t1, mine[m].t + 1); f++) {
            var cc = (fr[f].p || [])[sp.i];
            if (cc && cc[4] && Math.hypot(cc[0] - cand.x, cc[2] - cand.z) < 6000) { ts = Math.min(ts, fr[f].t); break; }
          }
        });
        var from = lastScore;
        (resets[tm] || []).forEach(function (rt) { if (rt > from && rt < ts) from = rt; });
        cur = cand; lastScore = ts;
        out.push({ t: ts, corner: cand, round: ri, from: from });
      }
    }
    out.sort(function (a, b) { return a.t - b.t; });
  });
  // THE GOAL A TEAM IS AFTER RIGHT NOW (user, 2026-10-04), including the ones it
  // never reached. A goal moves at every score and every reset, so each team's
  // round is cut into stretches at those moments. A stretch that ends in a score
  // has a known goal. One that ends in a reset (or the round's end) does not --
  // nothing in the file names it -- so it is ESTIMATED: of the corners that are
  // somebody's goal at some point in the match, the one the team's carriers
  // closed the most distance on during that stretch. Marked est, drawn fainter
  // and labelled "GOAL?".
  var known = [];
  teams.forEach(function (tm) { seq[tm].forEach(function (e) { if (e.corner && known.indexOf(e.corner) < 0) known.push(e.corner); }); });
  var legs = {};
  teams.forEach(function (tm) {
    var L = legs[tm] = [], a = seq[tm];
    for (var q = 0; q < a.length; q++) {
      var t0 = a[q].t, nx = a[q + 1], sameRound = nx && nx.round === a[q].round;
      var t1 = sameRound ? nx.t : (a[q].round + 1 < rounds.length ? rounds[a[q].round + 1] : (R.duration || t0));
      var cuts = [t0];
      (resets[tm] || []).forEach(function (rt) { if (rt > t0 + 0.5 && rt < t1 - 0.5) cuts.push(rt); });
      cuts.push(t1);
      for (var w = 0; w + 1 < cuts.length; w++) {
        var last = w + 2 === cuts.length;
        if (last && sameRound) { L.push({ t0: cuts[w], t1: cuts[w + 1], corner: nx.corner, est: false }); continue; }
        // estimate: net ground closed on each candidate corner by this team's carriers
        var best = null, bv = 5000;
        known.forEach(function (cand) {
          if (cand === a[q].corner) return;
          var gain = 0;
          (carrySpans || []).forEach(function (sp) {
            if (!R.players[sp.i] || R.players[sp.i].team !== tm) return;
            var s0 = Math.max(sp.t0, cuts[w]), s1 = Math.min(sp.t1, cuts[w + 1]);
            if (s1 <= s0) return;
            var d0 = null, dmin = null;
            for (var f = frameIndexAt(s0); f < fr.length && fr[f].t < s1; f++) {
              var cc = (fr[f].p || [])[sp.i];
              if (!cc || !cc[4]) continue;
              var d = Math.hypot(cc[0] - cand.x, cc[2] - cand.z);
              if (d0 == null) d0 = d;
              if (dmin == null || d < dmin) dmin = d;
            }
            if (d0 != null) gain += d0 - dmin;
          });
          if (gain > bv) { bv = gain; best = cand; }
        });
        L.push({ t0: cuts[w], t1: cuts[w + 1], corner: best, est: true });
      }
    }
  });
  // one square per corner, shown only while it is somebody's spawn or goal
  raceGroup = new THREE.Group();
  corners.forEach(function (g) {
    var x = (g.x - ctr[0]) * S, y = g.y * S, z = (g.z - ctr[2]) * S, h = RACE_HALF_MM * S;
    g.fill = new THREE.Mesh(new THREE.PlaneGeometry(h * 2, h * 2),
      new THREE.MeshBasicMaterial({ color: 0x8A8F86, transparent: true, opacity: 0.2,
                                    side: THREE.DoubleSide, depthTest: false }));
    g.fill.rotation.x = -Math.PI / 2;
    g.fill.position.set(x, y + 0.5, z);
    g.fill.renderOrder = 4;
    var eg = new THREE.BufferGeometry();
    eg.setAttribute('position', new THREE.BufferAttribute(new Float32Array([
      x - h, y + 0.7, z - h, x + h, y + 0.7, z - h, x + h, y + 0.7, z + h, x - h, y + 0.7, z + h]), 3));
    g.edge = new THREE.LineLoop(eg, new THREE.LineBasicMaterial({
      color: 0x8A8F86, transparent: true, opacity: 0.9, depthTest: false }));
    g.edge.renderOrder = 5;
    raceGroup.add(g.fill); raceGroup.add(g.edge);
  });
  scene.add(raceGroup);
  R.race = { corners: corners, seq: seq, teams: teams, legs: legs };
  try { window.__mgo2Race = R.race; } catch (e) {}
}
// One label per corner. When the two teams want it for different things, a
// goal somebody really scored on beats an estimated one, which beats a spawn.
var ROLE_RANK = { 'SPAWN': 1, 'GOAL?': 2, 'GOAL': 3 };
function setRole(corner, kind, team) {
  if (corner.role && ROLE_RANK[corner.role.kind] >= ROLE_RANK[kind]) return;
  corner.role = { kind: kind, team: team };
}
function paintRace() {
  var rc = R.race;
  rc.corners.forEach(function (g) { g.role = null; });
  rc.teams.forEach(function (tm) {
    var a = rc.seq[tm], at = -1;
    for (var k = 0; k < a.length; k++) { if (a[k].t <= T) at = k; else break; }
    if (at < 0) return;
    if (a[at].corner) setRole(a[at].corner, 'SPAWN', tm);
    // a goal outranks a spawn on the same corner (the other team may spawn where this one scores)
    var L = rc.legs[tm] || [];
    for (var q = 0; q < L.length; q++) {
      if (T >= L[q].t0 && T < L[q].t1 && L[q].corner) {
        setRole(L[q].corner, L[q].est ? 'GOAL?' : 'GOAL', tm);
        break;
      }
    }
  });
  rc.corners.forEach(function (g) {
    var on = !!g.role && showObj;
    g.fill.visible = g.edge.visible = on;
    if (!on) return;
    var col = teamColor(g.role.team);
    g.fill.material.color.set(col);
    g.fill.material.opacity = g.role.kind === 'GOAL' ? 0.34 : g.role.kind === 'GOAL?' ? 0.2 : 0.13;
    g.edge.material.color.set(col);
  });
}

function paintSpawns() {
  if (!R) return;
  if (!R.spawns) { buildSpawns(); buildSpawnMeshes(); }
  if (R.race === undefined) { try { buildRace(); } catch (errRc) { R.race = null; console.warn('race corners off:', errRc); } }
  if (R.race) {
    // RACE draws its own corners; the generic spawn squares stand down
    (R.spawns || []).forEach(function (sp0) {
      if (sp0.fill) { sp0.fill.visible = false; sp0.edge.visible = false; }
      sp0.goalNow = true;
    });
    paintRace();
    return;
  }
  if (!R.spawns) return;
  for (var k = 0; k < R.spawns.length; k++) {
    var sp = R.spawns[k];
    if (!sp.fill) continue;
    var tm = spawnTeamAt(sp, T);
    var col = tm == null ? '#8A8F86' : teamColor(tm);
    sp.cur = tm;
    sp.fill.material.color.set(col);
    sp.fill.material.opacity = tm == null ? 0.08 : 0.16;
    sp.edge.material.color.set(col);
  }
}

// ── HUD overlay (nametags, kill rings) ───────────────────────────────────
var hudCtx;
// CORNER RADAR for the followed player (VIEWER_IDEAS D3): team-mates only, as
// the in-game link shows them, their heading as a tick, the dead as a cross.
// Up is where the followed player faces; the ring is RADAR_M metres.
var RADAR_M = 40, RADAR_ON = !/[?&]radar=0/.test(location.search);
function drawRadar(x, w, h) {
  if (!RADAR_ON || follow < 0 || !live[follow] || !R.players[follow]) return;
  var me = live[follow], team = R.players[follow].team, r = mobile ? 44 : 62;
  var cx = w - (mobile ? 8 : 280) - r - 14, cy = (mobile ? 70 : 150) + r;
  var hd = me.h || 0, fx = Math.sin(hd), fz = Math.cos(hd), rx = -fz, rz = fx;
  x.save();
  x.globalAlpha = 0.72; x.fillStyle = '#0B0F0C';
  x.beginPath(); x.arc(cx, cy, r, 0, Math.PI * 2); x.fill();
  x.globalAlpha = 1; x.strokeStyle = 'rgba(118,169,141,.55)'; x.lineWidth = 1;
  x.beginPath(); x.arc(cx, cy, r, 0, Math.PI * 2); x.stroke();
  x.beginPath(); x.arc(cx, cy, r / 2, 0, Math.PI * 2); x.stroke();
  function put(wx, wz) {
    var dx = (wx - me.x) / 1000, dz = (wz - me.z) / 1000;
    var u = (dx * rx + dz * rz) / RADAR_M, v = (dx * fx + dz * fz) / RADAR_M, L = Math.hypot(u, v);
    var edge = L > 1; if (edge) { u /= L; v /= L; }
    return [cx + u * (r - 4), cy - v * (r - 4), edge];
  }
  (showObj ? bombSites() : []).forEach(function (s) {
    var q = put(s.x, s.z); x.strokeStyle = '#E8C547'; x.strokeRect(q[0] - 3, q[1] - 3, 6, 6);
  });
  var ob = objPoint(T);
  if (ob) { var oq = put(ob[0], ob[2]); x.fillStyle = '#E8C547'; x.beginPath(); x.moveTo(oq[0], oq[1] - 5); x.lineTo(oq[0] + 4, oq[1]); x.lineTo(oq[0], oq[1] + 5); x.lineTo(oq[0] - 4, oq[1]); x.fill(); }
  for (var i = 0; i < live.length; i++) {
    var p = live[i], pl = R.players[i];
    if (!p || !pl || i === follow || pl.team !== team) continue;
    var q2 = put(p.x, p.z), col = teamColor(team);
    x.globalAlpha = q2[2] ? 0.5 : 1;
    if (!p.alive) {
      x.strokeStyle = col; x.globalAlpha *= 0.6;
      x.beginPath(); x.moveTo(q2[0] - 3, q2[1] - 3); x.lineTo(q2[0] + 3, q2[1] + 3); x.moveTo(q2[0] + 3, q2[1] - 3); x.lineTo(q2[0] - 3, q2[1] + 3); x.stroke();
    } else {
      x.fillStyle = col; x.beginPath(); x.arc(q2[0], q2[1], 3.2, 0, Math.PI * 2); x.fill();
      var rel = (p.h || 0) - hd;
      x.strokeStyle = col; x.beginPath(); x.moveTo(q2[0], q2[1]); x.lineTo(q2[0] - Math.sin(rel) * 8, q2[1] - Math.cos(rel) * 8); x.stroke();
    }
    x.globalAlpha = 1;
  }
  // me: an arrow pointing up
  x.fillStyle = '#FFFFFF'; x.beginPath(); x.moveTo(cx, cy - 6); x.lineTo(cx + 4, cy + 4); x.lineTo(cx, cy + 2); x.lineTo(cx - 4, cy + 4); x.fill();
  x.font = '400 9px "MGO2 UI","IBM Plex Mono",monospace'; x.fillStyle = 'rgba(201,208,194,.6)'; x.textAlign = 'center'; x.textBaseline = 'top';
  x.fillText(RADAR_M + ' M', cx, cy + r + 3);
  x.restore();
}
function drawHUD() {
  var w = innerWidth, h = innerHeight, x = hudCtx;
  x.clearRect(0, 0, w, h);
  hudPick.length = 0;
  if (!R) return;
  try { drawRadar(x, w, h); } catch (eR) {}

  var railL = mobile ? 8 : 296, railR = mobile ? 8 : 280;
  var v = new THREE.Vector3();
  drawBaseHUD(x, w, h, v);
  var items = [];
  for (var i = 0; i < live.length; i++) {
    var p = live[i];
    if (!p) continue;
    v.set((p.x - ctr[0]) * S, p.y * S + 16, (p.z - ctr[2]) * S).project(cam);
    if (v.z > 1) continue;
    var sx = (v.x * 0.5 + 0.5) * w, sy = (-v.y * 0.5 + 0.5) * h;
    if (sx < railL - 60 || sx > w - railR + 60 || sy < 0 || sy > h - 130) continue;
    items.push({ i: i, x: sx, y: sy, z: v.z, dead: !p.alive });
  }
  // Layout order must be STABLE. Sorting by camera depth reshuffles the stack
  // every frame as players move, which is what makes clustered tags flicker.
  // Screen-Y with an index tie-break is spatially stable instead.
  items.sort(function (a, b) {
    if (a.dead !== b.dead) return a.dead ? 1 : -1;   // live first
    return (a.y - b.y) || (a.i - b.i);
  });

  var carrying = {};
  try {
    carrySpansAt(T).forEach(function (sp) {
      if (sp.i != null) carrying[sp.i] = targetKind(sp.item, sp.mark);
    });
  } catch (errCt) {}
  x.font = LBL_FONT;
  items.forEach(function (it) {
    var p = R.players[it.i];
    // a live streak mark from 3 kills in a row, as the game's own tracker counts it
    var run_ = it.dead ? 0 : killRunAt(it.i, T);
    it.tag = p.name + (it.dead ? '  KIA' : (koAt(it.i, T) ? '  ' + 'Zzz'.slice(0, 1 + Math.floor(T * 2) % 3) + '   '.slice(0, 2 - Math.floor(T * 2) % 3) + ' KO ' + Math.ceil(koLeft(it.i, T)) + 's' : '')) + '' +
             (run_ >= 3 ? '  x' + run_ : '') + (!it.dead && destabHit(it.i, T) ? '  DESTABILIZED' : '');
    it.w = x.measureText(it.tag).width + 12;
    // GEAR ICON instead of words (user, 2026-10-05: a little drum can takes less room
    // than "IN DRUM"): 10 = inside the drum can, 22 = night vision switched on.
    it.gear = it.dead ? null : gearAt(it.i, T);
    if (it.gear !== 10 && it.gear !== 22) it.gear = null;
    if (it.gear) it.w += 16;
    // WHO HAS THE TARGET, readable from across the map (user, 2026-10-04): the
    // carrier's tag gets a diamond in the target's colour and a bright frame.
    it.carry = carrying[it.i] || null;
    if (it.carry) it.w += 16;
    it.hasHp = !it.dead && live[it.i] && live[it.i].hp != null;
    it.h = it.dead ? 16 : 16 + (it.hasHp ? 7 : 0) + 11;
  });

  // Greedy de-overlap on real rectangles, alternating above/below the head so
  // a cluster fans out instead of growing one tall column.
  var placed = [];
  items.forEach(function (it) {
    var base = it.y - 20, step = it.h + 3, ly = base;
    for (var k = 0; k < 26; k++) {
      var clash = false;
      for (var q = 0; q < placed.length; q++) {
        var u = placed[q];
        if (Math.abs(u.x - it.x) < (u.w + it.w) / 2 + 5 &&
            Math.abs(u.ly - ly) < (u.h + it.h) / 2 + 2) { clash = true; break; }
      }
      if (!clash) break;
      var rank = Math.floor(k / 2) + 1;
      ly = base + (k % 2 ? rank : -rank) * step;
    }
    it.ly = ly;
    placed.push(it);
  });

  // Glide to the new slot rather than snapping. A large jump means the player
  // teleported (respawn) or the camera cut, so reset instead of sliding.
  items.forEach(function (it) {
    var st = labelPos[it.i];
    if (!st || Math.abs(st.y - it.ly) > 110 || Math.abs(st.x - it.x) > 220) {
      st = labelPos[it.i] = { x: it.x, y: it.ly };
    }
    st.x += (it.x - st.x) * 0.5;
    st.y += (it.ly - st.y) * 0.3;
    it.lx = st.x;
    it.ly = st.y;
    hudPick.push({ i: it.i, x: it.x, y: it.y, lx: it.lx, ly: it.ly,
                   w: it.w, h: it.h, dead: it.dead });
  });

  items.forEach(function (it) {
    var p = R.players[it.i], c = teamColor(p.team);
    var sel = it.i === follow;
    x.globalAlpha = it.dead ? 0.26 : 1;
    // leader line
    x.strokeStyle = c; x.lineWidth = 1; x.globalAlpha *= 0.5;
    x.beginPath(); x.moveTo(it.x, it.y - 4); x.lineTo(it.lx, it.ly + 8); x.stroke();
    x.globalAlpha = it.dead ? 0.26 : 1;

    var kd = score[it.i] || { k: 0, d: 0 };
    var tag = it.tag;
    x.font = LBL_FONT;
    var bx = it.lx - it.w / 2, by = it.ly - 8, bw = it.w, bh = 16;

    x.fillStyle = sel ? 'rgba(255,122,24,.20)' : 'rgba(11,13,12,.74)';
    x.fillRect(bx, by, bw, bh);
    x.strokeStyle = sel ? AMBER : c;
    x.globalAlpha *= sel ? 1 : 0.55;
    x.strokeRect(bx + .5, by + .5, bw - 1, bh - 1);
    x.globalAlpha = it.dead ? 0.26 : 1;
    if (it.carry) {
      // frog green / duck yellow, the same as the model he is carrying
      var cc = it.carry === 'gako' ? '#F2CE3F' : it.carry === 'bomb' ? '#E2574C' : '#6BD16B';
      x.strokeStyle = cc; x.lineWidth = 2;
      x.strokeRect(bx - 1, by - 1, bw + 2, bh + 2);
      x.lineWidth = 1;
      var dx0 = bx + 10, dy0 = by + bh / 2;
      x.fillStyle = cc;
      x.beginPath();
      x.moveTo(dx0, dy0 - 5); x.lineTo(dx0 + 5, dy0); x.lineTo(dx0, dy0 + 5); x.lineTo(dx0 - 5, dy0);
      x.closePath(); x.fill();
    }
    x.fillStyle = sel ? AMBER : (it.carry ? '#FFFFFF' : '#C9D0C2');
    x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillText(tag, it.lx + (it.carry ? 8 : 0) - (it.gear ? 8 : 0), by + bh / 2 + .5);
    if (it.gear) drawGearIcon(x, bx + bw - 10, by + bh / 2, it.gear);
    // the destabilizer's own icon beside the name of whoever is carrying one (user, 2026-10-08)
    if (!it.dead && destabCarrier(it.i, T)) {
      x.fillStyle = 'rgba(11,13,12,.74)'; x.fillRect(bx - 18, by, 17, bh);
      tlIcon(x, bx - 9.5, by + bh / 2, 'destab', 14, '#FFFFFF');
    }

    // Health bar welded to the tag: the ground rings foreshorten to nothing
    // at a low chase angle, so the number has to live here too.
    var live_p = live[it.i];
    var hp = it.dead ? null : (live_p ? live_p.hp : null);
    if (hp != null) {
      var bwid = bw - 2, bxx = bx + 1, byy = by + bh + 1;
      x.fillStyle = 'rgba(11,13,12,.8)';
      x.fillRect(bxx, byy, bwid, 3);
      x.fillStyle = hp > 0.6 ? '#9BD17A' : hp > 0.25 ? '#E8B33C' : '#E2574C';
      x.fillRect(bxx, byy, bwid * clamp(hp, 0, 1), 3);
      var st = live_p.st;
      if (st != null) {
        x.fillStyle = 'rgba(11,13,12,.8)';
        x.fillRect(bxx, byy + 4, bwid, 2);
        x.fillStyle = '#4EA8DE';
        x.fillRect(bxx, byy + 4, bwid * clamp(st, 0, 1), 2);
      }
    }

    // kill/death chip under the tag (live players only -- a KIA tag is a
    // location marker, not a scoreboard row)
    if (!it.dead) {
      x.font = '400 10.5px "MGO2 UI","IBM Plex Mono",monospace';
      x.fillStyle = '#4E574B';
      var lab = kd.k + '-' + kd.d;
    var inm = itemName(live_p && live_p.item);
    if (inm) lab += '  ' + inm + (live_p.ammo != null ? ' ' + live_p.ammo : '');
    x.fillText(lab, it.lx, by + bh + (hp != null ? 15 : 7));
    }
    x.globalAlpha = 1;
  });

  // THE DESTABILIZER GOING OFF (user, 2026-10-08: "should have some electrical pulse or something that goes off"): three
  // blue-white shock rings and a burst of jagged arcs from the device, then a crackle on it until the game takes it away.
  // Drawn from the clock alone, so it is the same whichever way the playhead got here. The look is invented: the game's
  // own effect has not been extracted.
  destabEvents().forEach(function (de, dk) {
    if (de.kind !== 'success' || T < de.t || !R.boxes) return;
    var dev = null;
    R.boxes.lie.forEach(function (b) { if (b.item === 0x44 && b.t0 <= de.t && (!dev || b.t0 > dev.t0)) dev = b; });
    if (!dev) return;
    var da = T - de.t, live_ = T < dev.t1;
    if (da > 3.2 && !live_) return;
    v.set((dev.x - ctr[0]) * S, dev.y * S + 4, (dev.z - ctr[2]) * S).project(cam);
    if (v.z > 1) return;
    var px = (v.x * 0.5 + 0.5) * w, py = (-v.y * 0.5 + 0.5) * h, rr, ra;
    for (var ri = 0; ri < 3; ri++) {
      ra = da - ri * 0.38;
      if (ra < 0 || ra > 2.2) continue;
      rr = 14 + ra * 300;
      x.globalAlpha = (1 - ra / 2.2) * 0.85;
      x.strokeStyle = ri ? '#7FD4FF' : '#EAF8FF'; x.lineWidth = ri ? 2 : 3.5;
      x.beginPath(); x.ellipse(px, py, rr, rr * 0.42, 0, 0, Math.PI * 2); x.stroke();
    }
    // arcs: re-rolled about 14 times a second from a hash of the clock, so a paused frame holds still
    var tick = Math.floor(T * 14), seed = (tick * 2654435761 + dk * 97) >>> 0;
    function rnd() { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; }
    var nb = da < 1.2 ? 9 : (live_ ? 3 : 0), reachPx = da < 1.2 ? 150 : 34;
    x.strokeStyle = '#BFEAFF'; x.lineWidth = 1.5; x.globalAlpha = da < 1.2 ? 0.95 : 0.7;
    for (var bi2 = 0; bi2 < nb; bi2++) {
      var an = rnd() * Math.PI * 2, len = reachPx * (0.45 + rnd() * 0.55), sx = px, sy = py - 10;
      x.beginPath(); x.moveTo(sx, sy);
      for (var sg = 1; sg <= 5; sg++) {
        var fr2 = sg / 5, jx = (rnd() - 0.5) * 22, jy = (rnd() - 0.5) * 22;
        x.lineTo(px + Math.cos(an) * len * fr2 + jx, py - 10 + Math.sin(an) * len * fr2 * 0.5 + jy);
      }
      x.stroke();
    }
    if (da < 0.18) { x.globalAlpha = (0.18 - da) / 0.18 * 0.35; x.fillStyle = '#DFF4FF'; x.fillRect(0, 0, w, h); }
    x.globalAlpha = 1; x.lineWidth = 1;
  });

  // kill rings: expanding amber pulse at the victim's last position
  for (var r = rings.length - 1; r >= 0; r--) {
    var g = rings[r];
    g.age += 1 / 60;
    if (g.age > 1.35) { rings.splice(r, 1); continue; }
    v.set((g.x - ctr[0]) * S, g.y * S + 6, (g.z - ctr[2]) * S).project(cam);
    if (v.z > 1) continue;
    var gx = (v.x * 0.5 + 0.5) * w, gy = (-v.y * 0.5 + 0.5) * h;
    var a = 1 - g.age / 1.35;
    x.globalAlpha = a * 0.9;
    x.strokeStyle = AMBER; x.lineWidth = 2;
    x.beginPath(); x.arc(gx, gy, 8 + g.age * 46, 0, Math.PI * 2); x.stroke();
    x.lineWidth = 1;
    x.beginPath(); x.moveTo(gx - 13, gy - 13); x.lineTo(gx + 13, gy + 13);
    x.moveTo(gx + 13, gy - 13); x.lineTo(gx - 13, gy + 13); x.stroke();
    x.globalAlpha = 1;
  }
}

// ── timeline ─────────────────────────────────────────────────────────────
var tlCtx, tlW = 0, tlH = 0;
function sizeTimeline() {
  var c = $('tl'), dpr = Math.min(devicePixelRatio || 1, 2);
  var r = c.getBoundingClientRect();
  tlW = Math.max(1, r.width); tlH = Math.max(1, r.height);
  c.width = tlW * dpr; c.height = tlH * dpr;
  tlCtx = c.getContext('2d');
  tlCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
}
// Alive/dead spans for one player, walked once and cached. A cell is absent
// on frames where that player sent no update, so the last known state carries
// forward rather than reading as a gap.
var lifeCache = { key: null, spans: null };
function lifeSpans(pi) {
    var key = (R && R.file) + '#' + pi;
    if (lifeCache.key === key) return lifeCache.spans;
    var spans = [], fr = R.frames, cur = null, t0 = 0;
    for (var k = 0; k < fr.length; k++) {
        var c = fr[k].p[pi];
        if (!c) continue;
        var a = !!c[4];
        if (cur === null) { cur = a; t0 = fr[k].t; continue; }
        if (a !== cur) { spans.push([t0, fr[k].t, cur]); cur = a; t0 = fr[k].t; }
    }
    if (cur !== null) spans.push([t0, R.duration, cur]);
    lifeCache = { key: key, spans: spans };
    return spans;
}

// The same icons drawn on the timeline canvas. `tint` recolours a white icon (a base capture in the
// capturing side's colour); the tinted copy is made once per colour. Returns false until the image is in.
var HUDIC = {};
function hudIcon(name) {
  var o = HUDIC[name];
  if (!o) {
    o = HUDIC[name] = { img: new Image(), ok: false, tint: {} };
    o.img.onload = function () { o.ok = true; };
    o.img.src = 'fx/' + name + '.png?v=1';
  }
  return o;
}
function tlIcon(x, cx, cy, name, size, tint) {
  var o = hudIcon(name);
  if (!o.ok) return false;
  var src = o.img;
  if (tint) {
    src = o.tint[tint];
    if (!src) {
      src = o.tint[tint] = document.createElement('canvas');
      src.width = o.img.naturalWidth; src.height = o.img.naturalHeight;
      var c = src.getContext('2d');
      c.drawImage(o.img, 0, 0);
      // '~colour' = a wash that keeps the icon's own shading (frog green, duck yellow);
      // a plain colour repaints the icon flat (a base in its captor's colour)
      if (tint.charAt(0) === '~') { c.globalCompositeOperation = 'source-atop'; c.globalAlpha = 0.62; c.fillStyle = tint.slice(1); }
      else { c.globalCompositeOperation = 'source-in'; c.fillStyle = tint; }
      c.fillRect(0, 0, src.width, src.height);
    }
  }
  var w = size * (o.img.naturalWidth / o.img.naturalHeight);
  x.drawImage(src, cx - w / 2, cy - size / 2, w, size);
  return true;
}
// SOP DESTABILIZER (Base mission; item 'BASE BUSTER', id 68; HUD texture 237 = fx/destab.png - the user
// corrected an earlier reading of that icon as a base, 2026-10-08). The file has no destabilizer event, so each
// one's story is read from two things. Measured on 37 Base matches: 9 destabilizers, 1 planted.
//   got      BOUGHT. No stat or record marks the purchase (buy_scan.js: nothing steps for the owner), but in 9 of 9
//            cases the item is first in hand 1.4 - 4.8 s after its owner respawns, so it is bought on the respawn
//            screen and the mark is put on that respawn (INFERRED; first sight of the item if no respawn within 8 s).
//   placed   a world record (meta.worldOps, code 101) for the item appears within 5 m of its holder and his count
//            drops to 0. In the one case seen it is written 0.1 s before the planter is shot; a second record
//            (code 165) follows at the same spot 10.2 s later - what that one means is NOT known.
//   success  THE GAME'S OWN COUNT: scoreboard stat 57 steps for the planter (user: "it counts the successful destab
//            stat to give points"). In Base it steps exactly once in 37 matches, on that one plant, 4.8 s after it,
//            with his score going up in the same record. Stat 57 means other things in other modes, so Base only.
//   lost     the holder is killed while still holding one. No world record is written then (seen once), which is
//            what separates a plant from a drop.
// -> [{t, kind: 'got' | 'placed' | 'success' | 'lost', player}]
function destabEvents() {
  if (!R) return [];
  if (R.__destab) return R.__destab;
  var out = [];
  if (!R.meta || R.meta.modeName !== 'BASE') return (R.__destab = out);
  // A parser that carries neither the world records nor the score stream (the main console's older one)
  // cannot tell a plant from a death: it read match 52613's plant as an elimination. Show nothing instead.
  if (!Array.isArray(R.meta.worldOps) || !Array.isArray(R.meta.statOps)) return (R.__destab = out);
  var id = (R.meta.itemNames || []).indexOf('BASE BUSTER');
  if (id < 0) id = 68;
  var plants = (R.meta.worldOps || []).filter(function (o) { return o.item === id && o.code === 101 && o.p; })
    .sort(function (a, b) { return a.t - b.t; });
  var deaths = (R.events || []).filter(function (e) { return e.type === 'kill' && e.victim != null; })
    .sort(function (a, b) { return a.t - b.t; });
  var own = {}, pi = 0, di = 0, fr = R.frames || [], up_ = {}, was_ = {};
  for (var f = 0; f < fr.length; f++) {
    var t = fr[f].t, row = fr[f].p || [];
    for (; pi < plants.length && plants[pi].t <= t; pi++) {
      var best = null, bd = 5000;
      for (var k in own) {
        var d0 = Math.hypot(own[k].pos[0] - plants[pi].p[0], own[k].pos[1] - plants[pi].p[2]);
        if (d0 < bd) { bd = d0; best = k; }
      }
      if (best !== null) { out.push({ t: plants[pi].t, kind: 'placed', player: +best }); delete own[best]; }
    }
    for (; di < deaths.length && deaths[di].t <= t; di++) {
      var v = deaths[di].victim;
      if (own[v]) { out.push({ t: deaths[di].t, kind: 'lost', player: v }); delete own[v]; }
    }
    for (var i = 0; i < row.length; i++) {
      var c = row[i];
      if (c) { if (c[4] && !was_[i]) up_[i] = t; was_[i] = c[4]; }
      if (!c || c[12] !== id) continue;
      if (own[i]) { own[i].pos = [c[0], c[2]]; continue; }
      if (c[4] && c[13] > 0) { own[i] = { pos: [c[0], c[2]] }; out.push({ t: (up_[i] != null && t - up_[i] < 8) ? up_[i] : t, kind: 'got', player: i }); }
    }
  }
  ((R.meta && R.meta.statOps) || []).forEach(function (so) {
    if (so[2] === 57 && R.players[so[1]]) out.push({ t: so[0], kind: 'success', player: so[1] });
  });
  // THE GAME'S OWN ANNOUNCEMENTS, when the file has them (meta.destabOps): bought and gone replace the two
  // readings above, which stay as the fallback for a file without them. A 'gone' within 3 s of that player's
  // own placement is the planter being shot, not a lost destabilizer, so it draws no cross.
  var dops = (R.meta.destabOps || []).filter(function (o) { return o.player != null; });
  if (dops.length) {
    out = out.filter(function (e) { return e.kind === 'placed' || e.kind === 'success'; });
    dops.forEach(function (o) {
      if (o.kind === 'bought') { out.push({ t: o.t, kind: 'got', player: o.player }); return; }
      // 'fired' = the team's status byte says it went off (the game's own flag; the stat-57 credit lands the same instant)
      if (o.kind === 'fired') { if (!out.some(function (e) { return e.kind === 'success' && Math.abs(e.t - o.t) < 2; })) out.push({ t: o.t, kind: 'success', player: o.player }); return; }
      var pl = out.some(function (e) { return e.kind === 'placed' && e.player === o.player && o.t - e.t >= 0 && o.t - e.t < 3; });
      if (!pl) out.push({ t: o.t, kind: 'lost', player: o.player });
    });
  }
  out.sort(function (a, b) { return a.t - b.t; });
  return (R.__destab = out);
}
// WHO A DESTABILIZER HIT. Not written as an event. The user (2026-10-08): a destabilized player does not lose stamina,
// he FREEZES IN PLACE "going mad", and the other side then knocks him out to take bases while he sleeps. The one
// success on record (match 52613, 161.5 s) shows exactly that: every living enemy, 2 m to 65 m from the device, stops
// dead within a second and sends no new state until something is done to him - four are knocked out one by one
// (166.8, 171.1, 178.4, 180.7 s, stamina straight to zero), one is box-tackled at 179.5 s. So: every enemy alive at
// the success is destabilized from then until he is knocked out, dies or moves again. How long it lasts untouched
// is NOT known (nobody was left alone longer than 18 s), so it is capped at 30 s. -> [{player, t0, t1}]
function destabVictims() {
  if (!R) return [];
  if (R.__dvic) return R.__dvic;
  // THE GAME'S OWN FLAG (meta.destabFrozen, 2026-10-08): the position record of a destabilized player carries a marker
  // byte for exactly as long as he is frozen. When the file has it, it IS the answer and nothing below is used.
  var fz = R.meta && R.meta.destabFrozen;
  if (fz && fz.length) return (R.__dvic = fz.map(function (z) { return { player: z.player, t0: z.t0, t1: z.t1 + 0.3 }; }));
  var out = [], succ = destabEvents().filter(function (e) { return e.kind === 'success'; });
  if (!succ.length) return (R.__dvic = out);
  var fr = R.frames || [];
  succ.forEach(function (s) {
    var pl = R.players[s.player], at = {}, open = {};
    if (!pl) return;
    for (var f = 0; f < fr.length; f++) {
      var t = fr[f].t, row = fr[f].p || [];
      if (t > s.t + 31) break;
      for (var i = 0; i < row.length; i++) {
        var c = row[i]; if (!c) continue;
        if (t <= s.t + 1.5) { at[i] = c; continue; }          // where he is when it goes off (the freeze lands within ~1 s)
        if (open[i] === 0) continue;
        if (open[i] === undefined) {
          var a = at[i];
          if (!a || !a[4] || R.players[i].team === pl.team) { open[i] = 0; continue; }
          open[i] = { player: i, t0: s.t, t1: s.t + 30 }; out.push(open[i]);
        }
        var a2 = at[i];
        if (!c[4] || c[6] === 0 || Math.hypot(c[0] - a2[0], c[2] - a2[2]) > 1500) { open[i].t1 = t; open[i] = 0; }
      }
    }
  });
  return (R.__dvic = out);
}
function destabHit(i, t) {
  var v = destabVictims();
  for (var k = 0; k < v.length; k++) if (v[k].player === i && t >= v[k].t0 && t < v[k].t1) return true;
  return false;
}
// Is this player carrying a destabilizer at t? From bought until placed or eliminated.
function destabCarrier(i, t) {
  var ev = destabEvents(), on = false;
  for (var k = 0; k < ev.length && ev[k].t <= t; k++) {
    if (ev[k].player !== i) continue;
    on = ev[k].kind === 'got' ? true : ev[k].kind === 'success' ? on : false;
  }
  return on;
}
// Kerotan (frog) / GA-KO (duck) on the timeline: the game's own HUD icon once it has loaded, the
// hand-drawn face before that. `put` adds a ring.
function tlHead(x, cx, cy, kind, put) {
  if (tlIcon(x, cx, cy, kind === 'gako' ? 'gako' : 'kerotan', 21, kind === 'gako' ? '~#F2CE3F' : '~#6BD16B')) {
    if (put) { x.beginPath(); x.arc(cx, cy, 11.5, 0, Math.PI * 2); x.strokeStyle = '#F4F6EE'; x.lineWidth = 1.5; x.stroke(); x.lineWidth = 1; }
    return;
  }
  var duck = kind === 'gako', body = duck ? '#F2CE3F' : '#6BD16B';
  x.lineWidth = 1;
  if (put) { x.beginPath(); x.arc(cx, cy, 8.5, 0, Math.PI * 2); x.strokeStyle = '#F4F6EE'; x.lineWidth = 1.5; x.stroke(); x.lineWidth = 1; }
  x.fillStyle = body; x.strokeStyle = '#0B0D0C';
  if (!duck) {                                   // frog: two eye bumps on top of the head
    x.beginPath(); x.arc(cx - 3.2, cy - 4, 2.6, 0, Math.PI * 2); x.fill(); x.stroke();
    x.beginPath(); x.arc(cx + 3.2, cy - 4, 2.6, 0, Math.PI * 2); x.fill(); x.stroke();
  }
  x.beginPath(); x.arc(cx, cy, 5.5, 0, Math.PI * 2); x.fill(); x.stroke();
  x.fillStyle = '#0B0D0C';
  if (duck) {
    x.beginPath(); x.arc(cx - 1, cy - 1.8, 1.1, 0, Math.PI * 2); x.fill();
    x.fillStyle = '#F08A24';                     // the bill, off to one side
    x.beginPath(); x.moveTo(cx + 2.5, cy - 1); x.lineTo(cx + 8.5, cy + 1); x.lineTo(cx + 2.5, cy + 3); x.closePath(); x.fill(); x.stroke();
  } else {
    x.beginPath(); x.arc(cx - 3.2, cy - 4.2, 1, 0, Math.PI * 2); x.fill();
    x.beginPath(); x.arc(cx + 3.2, cy - 4.2, 1, 0, Math.PI * 2); x.fill();
    x.beginPath(); x.moveTo(cx - 3, cy + 1.5); x.lineTo(cx + 3, cy + 1.5); x.strokeStyle = '#0B0D0C'; x.stroke();
  }
}
// Base captures one player took part in: the scoreboard's rule (alive, on the capturing
// side, within 6 m of the point when the gauge completes). Cached per replay.
function tlBaseCapsOf(pi) {
  if (!R || !R.meta || !R.meta.baseGauge || !R.players[pi]) return [];
  if (!R.__tlCaps) R.__tlCaps = {};
  if (R.__tlCaps[pi]) return R.__tlCaps[pi];
  var out = [];
  try { buildBaseCaptures(); } catch (e0) {}
  (R.baseCaptures || []).forEach(function (c) {
    if (R.players[pi].team !== baseSignTeam(c.sign)) return;
    var mk = null;
    for (var q = 0; q < objMarks.length; q++) if (objMarks[q].base === c.base) { mk = objMarks[q]; break; }
    if (!mk || mk.wx == null) return;
    var cell = ((R.frames[frameIndexAt(c.t)] || {}).p || [])[pi];
    if (!cell || !cell[4]) return;
    var dx = cell[0] - mk.wx, dz = cell[2] - mk.wz;
    if (dx * dx + dz * dz <= 6000 * 6000) out.push({ t: c.t, base: c.base });
  });
  if (objMarks.length) R.__tlCaps[pi] = out;
  return out;
}

// HOVER + CLICK ON TIMELINE MARKS (user, 2026-10-08: "hovering over them they should tell you
// what they are, and when you click them it brings you to them"). Every mark drawTimeline puts
// down also records [x, y, radius, time, kind, source] here; the pointer handlers in wire() pick
// the nearest one. The label is built only when a mark is actually hovered.
var tlHits = [];
// TIMELINE ZOOM (user, 2026-10-08: "a way to pinch zoom on the timeline"). The bar shows the slice
// tlView.a .. tlView.b of the match (fractions of its length; 0 .. 1 = all of it). Pinch with two
// fingers, or turn the mouse wheel over the bar; shift + wheel (or a sideways wheel) slides it;
// double-click / double-tap puts the whole match back. While playing, the slice follows the playhead.
var tlView = { a: 0, b: 1, key: null };
function tlZoomAt(u, f) {
  var sp = tlView.b - tlView.a, minSp = Math.min(1, 6 / Math.max(1, R ? R.duration : 1));
  var ns = clamp(sp * f, minSp, 1), c = tlView.a + u * sp, na = clamp(c - u * ns, 0, 1 - ns);
  tlView.a = na; tlView.b = na + ns;
}
function tlPanBy(du) {
  var sp = tlView.b - tlView.a, na = clamp(tlView.a + du * sp, 0, 1 - sp);
  tlView.a = na; tlView.b = na + sp;
}
function tlPlayerName(i) { var p = R && R.players[i]; return p ? esc(p.name || ('Player ' + (i + 1))) : 'someone'; }
function tlTeamSpan(tm) { return '<b style="color:' + teamColor(tm) + '">' + esc(teamLabel(tm)) + '</b>'; }
function tlLabel(h) {
  var k = h[4], o = h[5], w;
  if (k === 'kill' || k === 'mykill' || k === 'mydeath') {
    w = feedWeapon(o); w = w ? ' · ' + esc(w) : '';
    var hs = o.hitZone === 'head' ? ' · headshot' : '';
    if (k === 'mykill') return 'Killed ' + tlPlayerName(o.victim) + w + hs;
    if (k === 'mydeath') return (o.killer == null || o.killer === o.victim ? 'Died' : 'Killed by ' + tlPlayerName(o.killer)) + w + hs;
    return (o.killer == null ? 'Unknown' : tlPlayerName(o.killer)) + ' killed ' + tlPlayerName(o.victim) + w + hs;
  }
  if (k === 'obj') {
    var tk = targetKind(o.item, null);
    if (tk === 'bomb') return o.carrierFlag40 ? 'Planted the bomb' : 'Picked up the bomb';
    return (o.carrierFlag40 ? 'Set down ' : 'Picked up ') + (tk === 'gako' ? 'GA-KO (the duck)' : 'Kerotan (the frog)');
  }
  if (k === 'destab') return tlPlayerName(o.player) + (o.kind === 'got' ? ' bought a destabilizer' : o.kind === 'placed' ? ' placed a destabilizer' : o.kind === 'success' ? ' \u2014 destabilizer successful' : ' was eliminated carrying a destabilizer');
  if (k === 'mybase') return 'Helped capture base ' + (o.base + 1);
  if (k === 'capbase') return 'Base ' + (o.base + 1) + ' captured by ' + tlTeamSpan(baseSignTeam(o.sign));
  if (k === 'capput') return tlPlayerName(o.carrier) + ' set a target down for ' + tlTeamSpan(R.players[o.carrier].team);
  if (k === 'bomblost') return 'Bomb ' + o.how + ' · ' + (o.left ? o.left + ' of ' + BOMBS_PER_ROUND + ' left' : 'none left, round over');
  if (k === 'glyph') return ({ up: 'Bomb planted', down: 'Bomb defused', sq: 'Bomb detonated', dia: 'Race goal scored' })[o] || 'Kill streak';
  return '';
}

function drawTimeline() {
  if (!tlCtx) return;
  tlHits.length = 0;
  var x = tlCtx, w = tlW, h = tlH, pad = 10;
  x.clearRect(0, 0, w, h);
  if (!R) return;
  var dur = R.duration || 1, iw = w - pad * 2;
  var trackY = h - 19;
  // the visible slice (see tlView). A new replay starts un-zoomed.
  if (tlView.key !== (R.file || '')) { tlView.key = R.file || ''; tlView.a = 0; tlView.b = 1; }
  var zoomed = tlView.b - tlView.a < 0.999;
  if (zoomed && playing) {
    var tf = clamp(T / dur, 0, 1), spv = tlView.b - tlView.a;
    if (tf < tlView.a || tf > tlView.b - spv * 0.04) { tlView.a = clamp(tf - spv * 0.1, 0, 1 - spv); tlView.b = tlView.a + spv; }
  }
  var v0 = tlView.a * dur, vspan = Math.max(0.001, (tlView.b - tlView.a) * dur), v1 = v0 + vspan;
  function X_(tt) { return pad + ((tt - v0) / vspan) * iw; }
  x.save();
  x.beginPath(); x.rect(pad - 8, 0, iw + 16, h); x.clip();   // marks outside the slice are cut off at the bar's ends

  // minute ticks
  x.strokeStyle = '#2A332A'; x.lineWidth = 1;
  x.font = '400 10.5px "MGO2 UI","IBM Plex Mono",monospace';
  x.textAlign = 'center'; x.textBaseline = 'top';
  var stepChoices = [1, 2, 5, 10, 15, 30, 60, 120, 300];
  var step = stepChoices.find(function (s) { return vspan / s <= 14; }) || 600;
  for (var s = Math.ceil(v0 / step - 1e-6) * step; s <= v1 + 1e-6; s += step) {
    var tx = X_(s);
    x.beginPath(); x.moveTo(tx + .5, trackY - 5); x.lineTo(tx + .5, trackY + 5); x.stroke();
    x.fillStyle = '#4E574B';
    x.textAlign = tx < pad + 14 ? 'left' : (tx > w - pad - 14 ? 'right' : 'center');
    x.fillText(fmt(s), clamp(tx, pad, w - pad), trackY + 8);
  }

  // track
  x.fillStyle = '#1C231B';
  x.fillRect(pad, trackY - 2, iw, 4);
  x.fillStyle = 'rgba(255,122,24,.30)';
  x.fillRect(pad, trackY - 2, clamp(X_(T) - pad, 0, iw), 4);

  var ev = R.events || [];
  var me = follow;

  if (me >= 0 && R.players[me]) {
    // ---- one player's match ---------------------------------------------
    // Their life laid over the track, so deaths read as gaps in it, then only
    // the events they were actually part of. Kills and objectives go above the
    // track, things done TO them go below: the sign carries the meaning, so a
    // good run and a bad one are distinguishable without reading colours.
    var mine = teamColor(R.players[me].team);
    lifeSpans(me).forEach(function (sp) {
      var a = X_(sp[0]);
      var b = X_(sp[1]);
      // Dead reads as a GAP, not as another colour: colouring it red makes it
      // identical to the life line of anyone on the red team.
      x.fillStyle = sp[2] ? mine : '#0B0D0C';
      x.globalAlpha = sp[2] ? 0.85 : 1;
      x.fillRect(a, trackY - 2, Math.max(1, b - a), 4);
    });
    x.globalAlpha = 1;
    // knocked out: an amber underline beneath the life band
    ((R.ins && R.ins.koBy[me]) || []).forEach(function (k) {
      var ka = X_(k[0]), kb = X_(k[1]);
      x.fillStyle = '#E8B33C';
      x.fillRect(ka, trackY + 3, Math.max(1, kb - ka), 2);
    });
    if (R.ins && !R.__tlEv) R.__tlEv = ev.concat(R.ins.cqc, (R.scans || []).map(function (sc) { return { t: sc.t0, type: 'scan', killer: sc.a, victim: sc.v }; })).sort(function (a, b) { return a.t - b.t; });
    if (R.__tlEv) ev = R.__tlEv;

    for (var i = 0; i < ev.length; i++) {
      var e = ev[i];
      var up = null, col = null, len = 5;
      if (e.type === 'kill') {
        if (e.killer === me) { up = true; col = mine; len = 7; }
        else if (e.victim === me) { up = false; col = '#E2574C'; len = 7; }
      } else if (e.type === 'stun' || e.type === 'cqc') {
        if (e.killer === me) { up = true; col = '#E8B33C'; }
        else if (e.victim === me) { up = false; col = '#E8B33C'; }
      } else if (e.type === 'scan') {
        if (e.killer === me) { up = true; col = '#58D6E8'; len = 8; }
        else if (e.victim === me) { up = false; col = '#58D6E8'; len = 8; }
      } else if (e.type === 'objective') {
        if (e.carrier === me) { up = true; col = '#4EA8DE'; len = 9; }
      } else if (e.type === 'throw') {
        if (e.killer === me) { up = true; col = 'rgba(201,208,194,.5)'; len = 3; }
      }
      if (up === null) continue;
      var ex = X_(e.t);
      x.strokeStyle = col;
      x.globalAlpha = e.t <= T ? 1 : 0.42;
      x.beginPath();
      // Kills, deaths and objectives are SHAPES, not hairlines (user, 2026-10-07: they
      // were hard to tell apart): a kill is a solid triangle above the track, a death a
      // red cross ON the track where the life band breaks, an objective a diamond
      // (solid = target set down, hollow = picked up). Stuns and throws stay as ticks.
      if (e.type === 'kill' && up) {
        x.moveTo(ex, trackY - 18); x.lineTo(ex + 6, trackY - 5); x.lineTo(ex - 6, trackY - 5); x.closePath();
        x.fillStyle = col; x.fill();
        x.strokeStyle = '#F4F6EE'; x.lineWidth = 1.25; x.stroke();
        tlHits.push([ex, trackY - 11, 8, e.t, 'mykill', e]);
      } else if (e.type === 'kill') {
        // a death: a red stem down from the top of the lane to a bold cross on the track
        x.moveTo(ex, trackY - 18); x.lineTo(ex, trackY - 6);
        x.strokeStyle = 'rgba(255,90,77,.55)'; x.lineWidth = 1.5; x.stroke();
        x.beginPath();
        x.moveTo(ex - 5.5, trackY - 5.5); x.lineTo(ex + 5.5, trackY + 5.5);
        x.moveTo(ex + 5.5, trackY - 5.5); x.lineTo(ex - 5.5, trackY + 5.5);
        x.strokeStyle = '#0B0D0C'; x.lineWidth = 6; x.stroke();
        x.strokeStyle = '#FF5A4D'; x.lineWidth = 3; x.stroke();
        tlHits.push([ex, trackY, 8, e.t, 'mydeath', e]);
      } else if (e.type === 'objective' && targetKind(e.item, null) !== 'bomb') {
        // the frog or the duck he picked up / set down, as a face (ring = set down at a goal)
        tlHead(x, ex, trackY - 26, targetKind(e.item, null), !!e.carrierFlag40);
        tlHits.push([ex, trackY - 26, 9, e.t, 'obj', e]);
      } else if (e.type === 'objective') {
        x.moveTo(ex, trackY - 15); x.lineTo(ex + 4.5, trackY - 10); x.lineTo(ex, trackY - 5); x.lineTo(ex - 4.5, trackY - 10); x.closePath();
        if (e.carrierFlag40) { x.fillStyle = col; x.fill(); x.strokeStyle = '#0B0D0C'; x.stroke(); }
        else { x.lineWidth = 1.5; x.stroke(); }
      } else {
        if (e.type !== 'throw') x.lineWidth = 1.5;
        if (up) { x.moveTo(ex + .5, trackY - 4); x.lineTo(ex + .5, trackY - 4 - len); }
        else { x.moveTo(ex + .5, trackY + 4); x.lineTo(ex + .5, trackY + 4 + len); }
        x.stroke();
      }
      x.lineWidth = 1;
      x.globalAlpha = 1;
    }

    // bases this player helped take: a badge in his colour carrying the base's number
    tlBaseCapsOf(me).forEach(function (c_) {
      var bx_ = X_(c_.t), by_ = trackY - 26;
      x.globalAlpha = c_.t <= T ? 1 : 0.45;
      x.fillStyle = mine; x.strokeStyle = '#F4F6EE'; x.lineWidth = 1.25;
      x.beginPath(); x.rect(bx_ - 6.5, by_ - 6.5, 13, 13); x.fill(); x.stroke();
      x.fillStyle = '#0B0D0C'; x.font = '700 10px "IBM Plex Mono",monospace';
      x.textAlign = 'center'; x.textBaseline = 'middle';
      x.fillText(String(c_.base + 1), bx_, by_ + 0.5);
      tlHits.push([bx_, by_, 9, c_.t, 'mybase', c_]);
      x.lineWidth = 1; x.globalAlpha = 1;
    });

    // who this is, and how they are doing, at the moment on the playhead
    var kd = score[me] || { k: 0, d: 0 };
    x.font = '500 11px "MGO2 UI","IBM Plex Mono",monospace';
    x.textAlign = 'left'; x.textBaseline = 'top';
    x.fillStyle = AMBER;
    x.fillText(R.players[me].name, pad, 3);
    var nw = x.measureText(R.players[me].name).width;
    x.font = '400 11px "MGO2 UI","IBM Plex Mono",monospace';
    x.fillStyle = '#7C8878';
    x.fillText(kd.k + ' / ' + kd.d, pad + nw + 9, 3);
  } else {
    // ---- the whole match -------------------------------------------------
    // kill ticks, stacked above the track, coloured by killer's team
    var lanes = {};
    for (var q = 0; q < ev.length; q++) {
      if (ev[q].type !== 'kill') continue;
      var qx = X_(ev[q].t);
      // 3 x 5 blocks in the killer's colour (were 1 px hairlines), four deep at most
      var key = Math.round(qx / 4);
      lanes[key] = (lanes[key] || 0) + 1;
      var lane = Math.min(lanes[key], 4);
      var kp = R.players[ev[q].killer];
      x.fillStyle = teamColor(kp ? kp.team : null);
      x.globalAlpha = ev[q].t <= T ? 1 : 0.42;
      x.fillRect(key * 4 - 2, trackY - lane * 7, 4, 6);
      tlHits.push([key * 4, trackY - lane * 7 + 3, 4, ev[q].t, 'kill', ev[q]]);
      x.globalAlpha = 1;
    }
  }

  // MATCH EVENTS along the top edge (VIEWER_IDEAS A8): a full-height line where
  // a round ended, in the winner's colour; bomb plants (amber up-triangle),
  // defuses (blue down-triangle), detonations (red square); goals (amber
  // diamond); the game's streak notices of 5 and up (small dot, killer's colour).
  var two = []; R.players.forEach(function (p_) { if (p_.team != null && two.indexOf(p_.team) < 0) two.push(p_.team); }); two.sort();
  function TX(tt) { return X_(tt); }
  ((R.meta && R.meta.roundResults) || []).forEach(function (rr) {
    var wt = rr.winner == null ? null : two.indexOf(rr.winner) >= 0 ? rr.winner : rr.winner === 2 ? two[1] : rr.winner;
    x.strokeStyle = wt != null ? teamColor(wt) : '#888'; x.globalAlpha = rr.t <= T ? 0.9 : 0.45; x.setLineDash([2, 2]);
    x.beginPath(); x.moveTo(TX(rr.t) + .5, 2); x.lineTo(TX(rr.t) + .5, trackY + 4); x.stroke(); x.setLineDash([]);
  });
  function glyph(tt, kind, col) {
    var gx = TX(tt), gy = 6; x.fillStyle = col; x.globalAlpha = tt <= T ? 1 : 0.45; x.beginPath();
    if (kind === 'up') { x.moveTo(gx, gy - 4); x.lineTo(gx + 4, gy + 3); x.lineTo(gx - 4, gy + 3); }
    else if (kind === 'down') { x.moveTo(gx, gy + 4); x.lineTo(gx + 4, gy - 3); x.lineTo(gx - 4, gy - 3); }
    else if (kind === 'sq') x.rect(gx - 3, gy - 3, 6, 6);
    else if (kind === 'dia') { x.moveTo(gx, gy - 4); x.lineTo(gx + 4, gy); x.lineTo(gx, gy + 4); x.lineTo(gx - 4, gy); }
    else if (kind === 'ring') { x.arc(gx, gy, 4.5, 0, Math.PI * 2); x.arc(gx, gy, 2.2, 0, Math.PI * 2, true); }
    else x.arc(gx, gy + 7, 2, 0, Math.PI * 2);
    x.fill();
    if (kind !== 'ring') tlHits.push([gx, (kind === 'up' || kind === 'down' || kind === 'sq' || kind === 'dia') ? gy : gy + 7, 6, tt, 'glyph', kind]);
  }
  if (R.meta && R.meta.mode === 6) {
    try { buildBombPlays(); } catch (eB) {}
    (R.bombPlays || []).forEach(function (pl) {
      glyph(pl.t, 'up', '#E8C547');
      if (pl.defuser != null && pl.last) glyph(pl.last.t, 'down', '#4EA8DE');
      else if (pl.boom) glyph(pl.boom, 'sq', '#E2574C');
    });
    // a bomb lost: a red ring, with how it went and how many are left on hover
    bombLosses().forEach(function (l_) {
      glyph(l_.t, 'ring', '#E2574C');
      tlHits.push([TX(l_.t), 6, 6, l_.t, 'bomblost', l_]);
    });
  }
  ((R.meta && R.meta.raceOps) || []).forEach(function (o_) {
    if (!o_.score) return;
    // a Race goal: the game's checkered flag (the diamond until the image is in)
    x.globalAlpha = o_.t <= T ? 1 : 0.45;
    if (tlIcon(x, TX(o_.t), 7, 'flag', 12, '#E8C547')) tlHits.push([TX(o_.t), 7, 7, o_.t, 'glyph', 'dia']);
    else glyph(o_.t, 'dia', '#E8C547');
    x.globalAlpha = 1;
  });
  // OBJECTIVE CAPTURES in the capturing side's colour: a base changing hands (BASE),
  // or a target set down (Capture, Team Sneaking, Rescue). Bomb and Race have their
  // own marks above.
  function capMark(tt, col) {
    var gx = TX(tt), gy = 7;
    x.globalAlpha = tt <= T ? 1 : 0.45;
    x.beginPath(); x.moveTo(gx, gy - 5.5); x.lineTo(gx + 5, gy); x.lineTo(gx, gy + 5.5); x.lineTo(gx - 5, gy); x.closePath();
    x.fillStyle = col; x.fill();
    x.strokeStyle = '#0B0D0C'; x.lineWidth = 1; x.stroke();
  }
  if (R.meta && R.meta.baseGauge) {
    try { buildBaseCaptures(); } catch (eC) {}
    (R.baseCaptures || []).forEach(function (c_) {
      // the game's checkered goal in the capturing side's colour, with the BASE NUMBER between the posts
      // (user, 2026-10-08; the diamond until the image is in)
      var bc_ = teamColor(baseSignTeam(c_.sign)), bx2 = TX(c_.t);
      x.globalAlpha = c_.t <= T ? 1 : 0.45;
      if (tlIcon(x, bx2, 9, 'flag', 19, bc_)) {
        x.fillStyle = bc_; x.font = '700 8.5px "IBM Plex Mono",monospace'; x.textAlign = 'center'; x.textBaseline = 'middle';
        x.fillText(String(c_.base + 1), bx2, 13.5);
      } else capMark(c_.t, bc_);
      x.globalAlpha = 1;
      tlHits.push([bx2, 9, 8, c_.t, 'capbase', c_]);
    });
    // destabilizers: the game's icon in its owner's colour; a green check once planted, a red cross if the carrier
    // is eliminated first. Drawn a row below the captures so the two never sit on top of each other.
    destabEvents().forEach(function (de) {
      var dp = R.players[de.player], dx_ = TX(de.t), dy_ = 25;
      if (!dp) return;
      x.globalAlpha = de.t <= T ? 1 : 0.45;
      if (!tlIcon(x, dx_, dy_, 'destab', 13, teamColor(dp.team))) { x.fillStyle = teamColor(dp.team); x.fillRect(dx_ - 3, dy_ - 5, 6, 10); }
      if (de.kind === 'success' || de.kind === 'lost') {
        x.beginPath();
        if (de.kind === 'success') { x.moveTo(dx_ + 1, dy_ + 2); x.lineTo(dx_ + 3.5, dy_ + 5); x.lineTo(dx_ + 8, dy_ - 2); }
        else { x.moveTo(dx_ + 2, dy_ - 1); x.lineTo(dx_ + 8, dy_ + 5); x.moveTo(dx_ + 8, dy_ - 1); x.lineTo(dx_ + 2, dy_ + 5); }
        x.strokeStyle = '#0B0D0C'; x.lineWidth = 4; x.stroke();
        x.strokeStyle = de.kind === 'success' ? '#6BD16B' : '#FF5A4D'; x.lineWidth = 2; x.stroke();
        x.lineWidth = 1;
      }
      x.globalAlpha = 1;
      tlHits.push([dx_, dy_, 8, de.t, 'destab', de]);
    });
  }
  if (!R.race && !(R.meta && R.meta.mode === 6)) {
    (R.events || []).forEach(function (e_) {
      if (e_.type !== 'objective' || !e_.carrierFlag40 || e_.carrier == null || !R.players[e_.carrier]) return;
      capMark(e_.t, teamColor(R.players[e_.carrier].team));
      tlHits.push([TX(e_.t), 7, 7, e_.t, 'capput', e_]);
    });
  }
  ((R.meta && R.meta.streakOps) || []).forEach(function (s_) {
    if (s_.kind === 3 || s_.count < 5 || s_.player == null || !R.players[s_.player]) return;
    glyph(s_.t, 'dot', teamColor(R.players[s_.player].team));
  });
  x.globalAlpha = 1;

  // playhead
  var px = X_(T);
  x.strokeStyle = AMBER; x.lineWidth = 1.5;
  x.beginPath(); x.moveTo(px, 4); x.lineTo(px, trackY + 5); x.stroke();
  x.fillStyle = AMBER;
  x.beginPath();
  x.moveTo(px - 4, 4); x.lineTo(px + 4, 4); x.lineTo(px, 10); x.closePath(); x.fill();
  x.lineWidth = 1;
  x.restore();
  if (zoomed) {
    // where this slice sits in the whole match, along the very bottom, and how far in it is
    x.fillStyle = '#1C231B'; x.fillRect(pad, h - 2, iw, 2);
    x.fillStyle = AMBER; x.fillRect(pad + tlView.a * iw, h - 2, Math.max(3, (tlView.b - tlView.a) * iw), 2);
    var zt = 'ZOOM ' + (1 / (tlView.b - tlView.a)).toFixed(1).replace(/\.0$/, '') + 'x  ·  double-click to reset';
    x.font = '500 10px "MGO2 UI","IBM Plex Mono",monospace'; x.textAlign = 'right'; x.textBaseline = 'top';
    var zw = x.measureText(zt).width + 8;
    x.fillStyle = 'rgba(11,13,12,.8)'; x.fillRect(w - pad - zw, 14, zw, 13);
    x.fillStyle = AMBER; x.fillText(zt, w - pad - 4, 15.5);
  }
}

// ── roster + feed DOM ────────────────────────────────────────────────────
function buildRoster() {
  var host = $('r-body');
  host.innerHTML = '';
  if (!R) return;
  var teams = {};
  R.players.forEach(function (p) { (teams[p.team == null ? 'x' : p.team] = teams[p.team == null ? 'x' : p.team] || []).push(p); });
  Object.keys(teams).sort().forEach(function (k) {
    var hd = document.createElement('div');
    hd.className = 'team-hd';
    var label = k === 'x' ? 'Unassigned'
      : ['Red Team', 'Blue Team', 'Team Charlie', 'Team Delta'][+k] || 'Team ' + k;
    hd.innerHTML = '<span class="sw" style="background:' + teamColor(k === 'x' ? null : +k) + '"></span>' +
                   '<span>' + label + '</span><span class="side" data-team-side="' + k + '"></span><span class="sc" data-team-score="' + k + '">0</span>';
    host.appendChild(hd);
    teams[k].forEach(function (p) {
      var b = document.createElement('button');
      b.className = 'prow';
      b.dataset.team = p.team == null ? '' : p.team;
      b.dataset.i = p.index;
      b.innerHTML = '<span class="bar"></span>' +
        '<span class="nm">' + esc(p.name) + '<span class="dst" data-dst></span></span>' +
        '<span class="kd"><b data-k>0</b>/<span data-d>0</span><em class="gp" data-gp></em></span>' +
        '<span class="cl">' + esc(p.clan || '—') + '</span>' +
        '<span class="hp"><i></i></span>';
      // Picking someone by hand ends director mode, same as tapping a body or
      // a strip chip -- otherwise the director cuts away again within 2.5 s.
      b.addEventListener('click', function () { setAutoFollow(false); selectPlayer(p.index); });
      host.appendChild(b);
    });
  });
  $('r-n').textContent = R.players.length + ' PL';
}

function teamLabel(t) {
  return ['Red Team', 'Blue Team', 'Team Charlie', 'Team Delta'][t] || ('Team ' + t);
}

function updateRoster() {
  if (!R) return;
  // crossed rifles on the attacking side, shield on the defending side, re-read as rounds change
  var atkNow = attackTeamAt(T);
  document.querySelectorAll('[data-team-side]').forEach(function (el) {
    var sk = el.dataset.teamSide, want = (atkNow == null || sk === 'x') ? '' : (+sk === atkNow ? 'attack' : 'defend');
    if (el.dataset.cur === want) return;
    el.dataset.cur = want;
    el.innerHTML = want ? feedIcon(want) : '';
    el.title = want === 'attack' ? 'Attacking this round' : want === 'defend' ? 'Defending this round' : '';
  });
  var tot = {};
  // GP (level points) gained or lost over the rounds finished so far, under each player's K / D. Same figures as the
  // scoreboard's GP column; blank until the first round ends, which is when the game first writes them.
  var gpKey = Math.floor(T);
  if (R.__gpKey !== gpKey) { R.__gpKey = gpKey; R.__gpChg = levelChange(T); R.__gpTot = levelPoints(T); R.__gpRnd = levelRounds(T); }
  var gpChg = R.__gpChg, gpTot = R.__gpTot;
  document.querySelectorAll('.prow').forEach(function (b) {
    var ge = b.querySelector('[data-gp]');
    if (ge) {
      var gi = +b.dataset.i, gv = gpChg ? gpChg[gi] : null, gr_ = R.__gpRnd, gl_ = gr_ && gr_.length ? gr_[gr_.length - 1][gi] : null;
      var gs = (gv == null || (!gv && !(gpTot && gpTot[gi]))) ? '' : ((gpTot && gpTot[gi] != null ? 'Lv ' + levelOf(gpTot[gi]) + ' ' : '') + expMark(gl_));
      gv = gl_;                                                                // colour by the last round's change
      if (ge.dataset.cur !== gs) {
        ge.dataset.cur = gs; ge.textContent = gs;
        ge.style.color = gv > 0 ? '#6BD16B' : gv < 0 ? '#FF5A4D' : '';
        ge.title = gs ? 'Level, and the game\'s mark for the last finished round (++ / + gained, - / -- lost). The recording holds level points, not GP' + (gpTot && gpTot[gi] ? ' — ' + gpTot[gi] + ' level points' : '') : '';
      }
    }
    var i = +b.dataset.i, kd = score[i] || { k: 0, d: 0 };
    b.querySelector('[data-k]').textContent = kd.k;
    b.querySelector('[data-d]').textContent = kd.d;
    b.dataset.dead = live[i] && !live[i].alive ? '1' : (live[i] ? '0' : '1');
    var bar = b.querySelector('.hp'), hpv = live[i] ? live[i].hp : null;
    if (hpv == null) bar.dataset.lvl = 'none';
    else {
      bar.dataset.lvl = hpv > 0.6 ? 'ok' : hpv > 0.25 ? 'mid' : 'low';
      bar.firstChild.style.width = (clamp(hpv, 0, 1) * 100).toFixed(1) + '%';
    }
    b.dataset.sel = i === follow ? '1' : '0';
    var dse = b.querySelector('[data-dst]'), dsw = destabCarrier(i, T) ? '1' : destabHit(i, T) ? '2' : '';
    if (dse && dse.dataset.cur !== dsw) {
      dse.dataset.cur = dsw;
      dse.innerHTML = dsw === '1' ? feedIcon('destab') : dsw === '2' ? '<span style="color:#FF5A4D;font-size:9px;letter-spacing:.08em;margin-left:6px">DESTABILIZED</span>' : '';
      dse.title = dsw === '1' ? 'Carrying a destabilizer' : dsw === '2' ? 'Destabilized: frozen in place' : '';
    }
    var t = R.players[i].team == null ? 'x' : R.players[i].team;
    tot[t] = (tot[t] || 0) + kd.k;
  });
  document.querySelectorAll('[data-team-score]').forEach(function (e) {
    e.textContent = tot[e.dataset.teamScore] || 0;
  });
}

// THE GAME'S OWN HEADSHOT ICON (user, 2026-10-08): the skull in a circle the game's kill log shows on a
// headshot kill. Texture 0x6A of the in-game HUD set 00bb4c06 (resident_nodld.dlz of r_sna01_n, DXT5 32 x 32,
// decoded by scratch stock/hud_final.py from the STOCK client) -> fx/headshot.png.
// MORE OF THE GAME'S OWN HUD ICONS (2026-10-08), all from the stock client's always-loaded HUD sets
// (memory: reference-mgo2-hud-textures): fx/kerotan.png + fx/gako.png (tri_layout_icon), fx/flag.png (hud 165),
// fx/base.png (hud 237), fx/stun.png (hud 54). feedIcon() = one of them inline in a feed line.
function feedIcon(name, wide) {
  if (!name) return '';
  if (!HS_STYLE) hsTag();
  return '<img class="hsic' + (wide ? ' w' : '') + '" alt="" src="fx/' + name + '.png?v=1">';
}
// WHO ATTACKS THIS ROUND (Rescue, Bomb, Team Sneaking). The game marks the attacking side with crossed rifles
// and the defending side with a shield (user, 2026-10-08; HUD textures 253 and 252). The file does not state the
// side, so: the attackers are the team whose player takes the target that round. Measured on 76 rounds of 44
// SaveMGO matches: only ONE team ever holds the target in a round (55 of 55 rounds with a take), and it is the
// red side in round 1 and the blue side in round 2 in 54 of those 55. A round with no take uses that order.
var SIDE_MODES = { 2: 1, 6: 1, 7: 1 };
function attackTeamAt(t) {
  if (!R || !R.meta || !SIDE_MODES[R.meta.mode]) return null;
  var hs = halfStarts(), ri = 0;
  for (var i = 0; i < hs.length; i++) if (hs[i] <= t) ri = i;
  if (!R.__atk) R.__atk = {};
  if (R.__atk[ri] !== undefined) return R.__atk[ri];
  var t0 = hs[ri], t1 = ri + 1 < hs.length ? hs[ri + 1] : Infinity, tm = null, ev = R.events || [];
  for (var k = 0; k < ev.length; k++) {
    var e = ev[k];
    if (e.type !== 'objective' || e.carrier == null || e.t < t0 || e.t >= t1) continue;
    var pl = R.players[e.carrier];
    if (pl && pl.team != null) { tm = pl.team; break; }
  }
  if (tm === null) tm = ri % 2;
  return (R.__atk[ri] = tm);
}
// The match winner as a viewer team number, once the playhead (or the scoreboard's limit) is past the result.
// From the file's own match-result record; null when the file has none (cut-short games, older parser).
function matchWinnerTeam(lim) {
  var mr = R && R.meta && R.meta.matchResult;
  if (!mr || mr.winner == null || mr.draw || (lim != null && lim < mr.t)) return null;
  var raws = [];
  R.players.forEach(function (p) { var rw = p.teamRaw != null ? p.teamRaw : p.team; if (rw != null && raws.indexOf(rw) < 0) raws.push(rw); });
  raws.sort(function (a, b) { return a - b; });
  var ix = raws.indexOf(mr.winner);
  return ix >= 0 ? ix : null;
}
// A white game icon painted in any colour (CSS mask), for use inside HTML: the gold / silver / bronze star, the trophy.
function iconMask(name, colour, px) {
  if (!HS_STYLE) hsTag();
  var u = 'url(fx/' + name + '.png?v=1) center / contain no-repeat';
  return '<span class="icm" style="width:' + px + 'px;height:' + px + 'px;background:' + colour + ';-webkit-mask:' + u + ';mask:' + u + '"></span>';
}
var HS_STYLE = false;
function hsTag() {
  if (!HS_STYLE) {
    HS_STYLE = true;
    var st = document.createElement('style');
    st.textContent = '.hsic{height:14px;width:14px;vertical-align:-3px;margin:0 3px 0 1px;image-rendering:auto}.hsic.w{width:28px}.icm{display:inline-block;vertical-align:-3px;margin:0 4px 0 1px}.prow .kd .gp{display:block;font-style:normal;font-size:9.5px;letter-spacing:.04em;text-align:right;margin-top:2px;color:var(--ink-faint)}.prow .kd .gp:empty{display:none}.prow .dst .hsic{height:14px;width:14px;margin:0 0 0 5px;vertical-align:-2px}.team-hd .side{margin-left:6px;line-height:0}.team-hd .side .hsic{height:15px;width:15px;margin:0;vertical-align:middle}';
    document.head.appendChild(st);
  }
  return ' \u00b7 <img class="hsic" alt="" src="fx/headshot.png?v=1"> headshot';
}
function renderFeed(fresh) {
  var host = $('feed');
  if (!R) return;
  // `wake` was renamed to `staminaRestore` and is no longer claimed to be a
  // revive: 18 of 20 such events land 8.07-8.27 s after the victim's OWN
  // death, i.e. they are the respawn.  Only the ones with no preceding death
  // reach the feed, and they are labelled by what was measured.
  function feedable(e) {
    if (e.type === 'kill' || e.type === 'stun' || e.type === 'objective') return true;
    if (e.type === 'cqc' || e.type === 'join' || e.type === 'leave' || e.type === 'goal' || e.type === 'scan') return true;
    // The parser renamed `wake` to `staminaRestore` mid-flight. Accept both, or
    // replays built before the rename lose their rows -- two here are confirmed
    // by the player who was there (1:59 and 2:04 on replay_351754_9). The
    // respawn filter applies only when the newer field exists; absent means
    // "not measured", not "is a respawn".
    if (e.type !== 'staminaRestore' && e.type !== 'wake') return false;
    return e.sincePlayerDeathSec == null;
  }
  var ev = feedList().filter(function (e) {
    return feedable(e) && e.t <= T;
  });
  var hasKills = feedList().some(feedable);
  if (!hasKills) {
    host.innerHTML = '<div class="empty"><b>No kill data in this replay.</b><br>' +
      'The event stream is not decoded for this file, so the feed stays empty rather ' +
      'than showing invented kills. Positions and identities below are real.</div>';
    $('f-n').textContent = 'N/A';
    return;
  }
  if (!ev.length) {
    host.innerHTML = '<div class="empty">No kills yet at ' + fmt(T) + '.<br>' +
      'Scrub forward or press play &mdash; kill ticks are marked on the timeline.</div>';
    $('f-n').textContent = '0';
    return;
  }
  var recent = ev.slice(-40).reverse();
  host.innerHTML = recent.map(function (e, n) {
    var k = R.players[e.killer], v = R.players[e.victim];
    var stun = e.type === 'stun';
    var wake = (e.type === 'staminaRestore' || e.type === 'wake');
    if (e.type === 'objective') {
      var oc = e.carrier == null ? null : R.players[e.carrier];
      // RACE runs two targets at once, so name which one this row is about
      var tgt = (e.item == null) ? '' : ' ' + (e.item + 1);
      return '<div class="kill obj' + (fresh && n === 0 ? ' fresh' : '') + '">' +
        '<span class="who">' +
        (oc ? '<span class="t' + oc.team + '">' + esc(oc.name) + '</span>' +
              '<span class="arr">&#9670;</span>' + feedIcon(targetKind(e.item, null) === 'bomb' ? null : targetKind(e.item, null)) + '<span>has target' + tgt + '</span>'
            : '<span>target' + tgt + ' dropped</span>') +
        '</span><span class="ts">' + fmt(e.t) + '</span></div>';
    }
    if (e.type === 'goal') {
      var gv = e.victim == null ? null : R.players[e.victim];
      return '<div class="kill obj' + (fresh && n === 0 ? ' fresh' : '') + '">' +
        '<span class="who"><span class="t' + (gv ? gv.team : e.bit) + '">' + esc(gv ? gv.name : 'Team') + '</span>' +
        '<span class="arr">&#9733;</span>' + feedIcon('flag') + '<span>GOAL</span></span>' +
        '<span class="ts">' + fmt(e.t) + '</span></div>';
    }
    if (e.type === 'join' || e.type === 'leave') {
      var lv = R.players[e.victim];
      return '<div class="kill wake' + (fresh && n === 0 ? ' fresh' : '') + '">' +
        '<span class="who"><span class="t' + (lv ? lv.team : 0) + '">' + esc(lv ? lv.name : '?') + '</span>' +
        '<span>' + (e.type === 'join' ? 'joined' : 'left') + '</span></span>' +
        '<span class="ts">' + fmt(e.t) + '</span></div>';
    }
    if (e.type === 'scan') {
      return '<div class="kill stun' + (fresh && n === 0 ? ' fresh' : '') + '">' +
        '<span class="who"><span class="t' + (k ? k.team : 0) + '">' + esc(k ? k.name : '?') + '</span>' +
        '<span class="arr">&#8982;</span>' +
        '<span class="t' + (v ? v.team : 1) + '">' + esc(v ? v.name : '?') + '</span></span>' +
        '<span class="ts">' + fmt(e.t) + '</span><span class="wp">SCAN</span></div>';
    }
    if (e.type === 'cqc') {
      return '<div class="kill stun' + (fresh && n === 0 ? ' fresh' : '') + '">' +
        '<span class="who"><span class="t' + (k ? k.team : 0) + '">' + esc(k ? k.name : '?') + '</span>' +
        '<span class="arr">&#9679;</span>' +
        '<span class="t' + (v ? v.team : 1) + '">' + esc(v ? v.name : '?') + '</span></span>' +
        '<span class="ts">' + fmt(e.t) + '</span><span class="wp">melee</span></div>';
    }
    if (wake) {
      var wv = R.players[e.victim];
      return '<div class="kill wake' + (fresh && n === 0 ? ' fresh' : '') + '">' +
        '<span class="who"><span class="t' + (wv ? wv.team : 0) + '">' +
        esc(wv ? wv.name : '?') + '</span>' +
        '<span class="arr">&uarr;</span><span>woken</span></span>' +
        '<span class="ts">' + fmt(e.t) + '</span></div>';
    }
    var who = k ? esc(k.name)
      : (e.suicide ? 'self' : e.cause === 'unknown' ? 'no killer' : '?');
    return '<div class="kill' + (stun ? ' stun' : '') +
      (fresh && n === 0 ? ' fresh' : '') + '">' +
      '<span class="who"><span class="t' + (k ? k.team : 0) + '">' + who + '</span>' +
      '<span class="arr">' + (stun ? '&#9679;' : '&rarr;') + '</span>' +
      '<span class="t' + (v ? v.team : 1) + '">' + esc(v ? v.name : '?') + '</span></span>' +
      '<span class="ts">' + fmt(e.t) + '</span>' +
      (feedWeapon(e)
        ? '<span class="wp">' + esc(feedWeapon(e)) +
          (stun ? ' · ' + feedIcon('stun', true) + 'stun' + (e.hitZone ? ' ' + e.hitZone : '')
                : ((e._ghs != null ? e._ghs : e.hitZone === 'head') ? hsTag() : '')) +
          (e.suicide ? ' · self' : '') +
          (e._assist && e._assist.length ? ' · assist ' + e._assist.map(function (q) {
            return esc(R.players[q] ? R.players[q].name : '?'); }).join(', ') : '') +
          (e._fb ? ' · first blood' : '') +
          (e._multi >= 2 ? ' · ' + (MULTI_NAME[e._multi] || e._multi + '×') + ' kill' : '') +
          (e._trade ? ' · trade' : '') +
          (e._streak ? ' · ' + e._streak + ' in a row' : '') +
          (e._dstreak ? ' · ' + e._dstreak + ' deaths in a row' : '') +
          (e._range != null && e._range >= 40 ? ' · ' + Math.round(e._range) + ' m' : '') + '</span>'
        : '') +
      '</div>';
  }).join('');
  var total = (R.events || []).filter(function (e) { return e.type === 'kill'; }).length;
  $('f-n').textContent = ev.length + ' / ' + total;
  // CLICK A LINE TO GO THERE, PAUSED (user, 2026-10-08). Each row remembers its moment; one
  // listener on the feed does the rest. It lands exactly ON the event, so the clicked line is
  // still the newest one in the feed after the jump.
  for (var fr = 0; fr < host.children.length && fr < recent.length; fr++) {
    host.children[fr].dataset.t = recent[fr].t;
    host.children[fr].title = 'Go to ' + fmt(recent[fr].t);
  }
  if (!host.__jump) {
    host.__jump = true;
    host.addEventListener('click', function (ce) {
      var row = ce.target && ce.target.closest ? ce.target.closest('.kill') : null;
      if (!row || row.dataset.t == null || !R) return;
      playing = false;
      try { syncPlay(); } catch (eS) {}
      seek(Math.max(0, +row.dataset.t));
    });
    var fst = document.createElement('style');
    fst.textContent = '#feed .kill{cursor:pointer}#feed .kill:hover{background:rgba(255,122,24,.10)}';
    document.head.appendChild(fst);
  }
}

// Where player `pi` last reported a position at or before time t. Frames are
// change-triggered, so someone standing still to plant a bomb has no cell in
// the plant's own frame -- walk back to the last one that exists.
function cellBefore(pi, t) {
  for (var f = frameIndexAt(t); f >= 0; f--) {
    var c = (R.frames[f].p || [])[pi];
    if (c && c[0] != null) return c;
  }
  return null;
}

// Bomb plants, each with the DEFUSE inferred for it.
// A plant is the carrier re-reported with bit 0x40 (see renderScore). The file
// has no defuse event, but it does record the COOLANT action (0x30, which only
// exists in Bomb): a defuse is taken to be the LAST enemy to spray within 4 m
// of the planted bomb, provided that spray ends within 6 s of the round ending.
// Seen on 9 plants in 5 files: the last close spray ends either 1-5 s before
// the round does (5 plants) or 15-45 s before (the bomb was not saved).
// INFERRED from that split, not read from the game -- shown as "likely".
var BOMB_FUSE = 62;      // seconds from plant to detonation, measured 61-64
var BOMB_FUSE_EXACT = 60; // the same, from the bomb's own records: 60.0 s, 2 of 2
// Round-over reason by mode. BOMB (6): measured. BASE (5): 1 = time up in 4 of 4
// rounds that ran the full clock; its code 2 ends rounds early for a reason not
// yet proven, so it gets no text.
var ROUND_END_TEXT = { 6: { 4: 'time up', 5: 'bomb detonated', 7: 'out of bombs' }, 5: { 1: 'time up' } };
// The bomb that is live at time t (planted, not yet defused or detonated), and
// how frozen it is then (0..1, or null when nobody is spraying it).
var bombMk = null;
// A dropped bomb nobody touches goes back to its start after 15.0 s in Bomb (measured: the
// 'home' record follows the 'lies at' record by 15.0 s on Yonkos; the frog and duck take 30.0 s).
// Each side gets BOMBS_PER_ROUND bombs a round (user, 2026-10-05; not read from the file).
var BOMB_HOME_S = 15, BOMBS_PER_ROUND = 3;
// BOMBS LOST, READ FROM THE FILE (user's idea, 2026-10-08: "tell the bombs left based on the
// timer hitting 0, because then you can see it respawn also"). Every time a bomb is lost the
// bomb's own records (meta.targetOps, item 0) write a 'home' record - the next bomb standing at
// the start. It follows a drop that nobody picked up (24 seen, 15-30 s after the 'lies at'
// record) or a plant that was defused (10 seen). Measured on all 34 Bomb rounds in 18 SaveMGO
// matches: no round has more than 3; the 4 rounds with exactly 3 end 0.5-0.6 s after the third
// with round-over reason 7; one more reason-7 round has 2 and ends on its third bomb's plant
// (defused, nothing left to respawn). So a round has 3 bombs and reason 7 is "out of bombs".
// -> [{t, how: 'timed out' | 'defused', left}] over the whole replay, `left` counted per round.
function bombLosses() {
  if (!R || !R.meta || R.meta.mode !== 6) return [];
  if (R.__bombLoss) return R.__bombLoss;
  var ops = (R.meta.targetOps || []).filter(function (o) { return o.item === 0; }).sort(function (a, b) { return a.t - b.t; });
  var rounds = (R.meta.roundResults || []).map(function (r) { return r.t; }).sort(function (a, b) { return a - b; });
  var ends = R.meta.roundEnds || [], out = [], n = 0, ri = 0, prev = null;
  function closeRound(tEnd) {
    // the third bomb planted and defused: the round is over, so no 'home' record follows
    var why = null;
    for (var q = 0; q < ends.length; q++) if (Math.abs(ends[q].t - tEnd) < 3) why = ends[q].code;
    if (why === 7 && n === BOMBS_PER_ROUND - 1 && prev && prev.kind === 'fe') out.push({ t: tEnd, how: 'defused', left: 0 });
    n = 0; prev = null;
  }
  for (var i = 0; i < ops.length; i++) {
    while (ri < rounds.length && ops[i].t > rounds[ri] + 0.5) closeRound(rounds[ri++]);
    if (ops[i].kind === 'home') {
      n++;
      out.push({ t: ops[i].t, how: prev && prev.kind === 'fe' ? 'defused' : 'timed out', left: Math.max(0, BOMBS_PER_ROUND - n) });
    }
    prev = ops[i];
  }
  while (ri < rounds.length) closeRound(rounds[ri++]);
  out.sort(function (a, b) { return a.t - b.t; });
  return (R.__bombLoss = out);
}
function bombsLeftAt(t) {
  var rs0 = 0, n = 0;
  ((R.meta && R.meta.roundResults) || []).forEach(function (rr) { if (rr.t <= t && rr.t > rs0) rs0 = rr.t; });
  bombLosses().forEach(function (l) { if (l.t <= t && l.t > rs0) n++; });
  return Math.max(0, BOMBS_PER_ROUND - n);
}
// WHERE BOMBS ARE PLANTED. The stage files name the plant sites
// (PRP_BOMB_AREA01 / 02 -> bomb_sites.json, replay/bomb_sites.py): on Silo
// Sunset AREA02 is 1.4 m from a bomb planted in a replay, and the two are the
// two spots the user circled (2026-10-05). 16 stages have them. For a stage
// without, or a plant away from every listed site, the plant spots recorded in
// THIS replay are used instead and marked `derived`.
var BOMB_SITES = null, bombSitesAsked = false, siteMarks = [];
function bombSites() {
  if (!R || !R.meta || R.meta.mode !== 6) return [];
  if (!bombSitesAsked) {
    bombSitesAsked = true;
    fetch(av('bomb_sites.json')).then(function (r) { return r.ok ? r.json() : {}; })
      .then(function (j) { BOMB_SITES = j || {}; if (R) R.__sites = null; }).catch(function () { BOMB_SITES = {}; });
  }
  if (R.__sites) return R.__sites;
  var out = ((BOMB_SITES && BOMB_SITES[R.stage]) || []).map(function (s, k) {
    return { x: s.x, y: s.y, z: s.z, label: 'PLANT SITE ' + (k + 1) };
  });
  buildBombPlays();
  R.bombPlays.forEach(function (pl) {
    if (pl.x == null) return;
    for (var k = 0; k < out.length; k++) if (Math.hypot(out[k].x - pl.x, out[k].z - pl.z) < 8000) return;
    out.push({ x: pl.x, y: pl.y != null ? pl.y : 0, z: pl.z, label: 'PLANT SITE', derived: true });
  });
  if (BOMB_SITES) R.__sites = out;
  return out;
}
// Where the bomb is at time t when it is neither carried nor planted, from the
// bomb's own records (meta.targetOps, item 0): after a "lies at" record it is on
// that spot until the next record; before the round's first pickup, or after it
// has been sent back, it waits where it is next picked up (the taker's position
// at that pickup -- the file does not write the waiting spot itself). A round
// result starts the next round. null while carried, planted, or unknown.
function bombLooseAt(t) {
  if (!R || !R.meta || R.meta.mode !== 6) return null;
  var ops = R.__bombOps;
  if (!ops) {
    ops = R.__bombOps = (R.meta.targetOps || []).filter(function (o) { return o.item === 0; })
      .concat((R.meta.roundResults || []).map(function (r) { return { t: r.t, kind: 'round' }; }))
      .sort(function (a, b) { return a.t - b.t; });
  }
  if (!ops.length) return null;
  var k = -1;
  for (var i = 0; i < ops.length; i++) { if (ops[i].t <= t) k = i; else break; }
  var cur = k >= 0 ? ops[k] : { kind: 'round' };
  if (cur.kind === 'take' || cur.kind === 'fe') return null;
  if (cur.kind === 'lie' && cur.p) {
    var by = null;
    for (var q = k - 1; q >= 0; q--) if (ops[q].kind === 'take') { by = ops[q].player; break; }
    var c0 = by != null ? insCell(R, by, cur.t) : null;
    return { x: cur.p[0], y: c0 && Math.hypot(c0[0] - cur.p[0], c0[2] - cur.p[2]) < 2500 ? c0[1] : cur.p[1] - 800, z: cur.p[2], state: 'dropped', t0: cur.t };
  }
  // waiting: look ahead to the next pickup of this round
  for (var n = k + 1; n < ops.length; n++) {
    if (ops[n].kind === 'round') break;
    if (ops[n].kind === 'take' && ops[n].player != null) {
      var c1 = insCell(R, ops[n].player, ops[n].t);
      return c1 ? { x: c1[0], y: c1[1], z: c1[2], state: 'waiting' } : null;
    }
  }
  return null;
}
function liveBombAt(t) {
  if (!R || !R.meta || R.meta.mode !== 6) return null;
  buildBombPlays();
  var live_ = null;
  R.bombPlays.forEach(function (pl) {
    var fuse = pl.exact ? BOMB_FUSE_EXACT : BOMB_FUSE;
    var until = pl.boom || (pl.defuser != null ? pl.last.t : Math.min(pl.t + fuse, pl.tEnd));
    if (pl.t <= t && t < until) live_ = pl;
  });
  return live_;
}
function bombGaugeAt(pl, t) {
  var g = pl && pl.gauge;
  if (!g) return null;
  for (var i = g.length - 1; i >= 0; i--) {
    if (g[i][0] <= t) return t - g[i][0] < 0.6 ? g[i][1] : null;
  }
  return null;
}
function buildBombPlays() {
  if (!R || R.bombPlays) return;
  var plays = [], ends = roundStarts().concat([R.duration]);
  (R.events || []).forEach(function (e) {
    if (e.type !== 'objective' || !e.carrierFlag40 || e.carrier == null) return;
    var c = cellBefore(e.carrier, e.t), tEnd = R.duration;
    for (var k = 0; k < ends.length; k++) if (ends[k] > e.t) { tEnd = ends[k]; break; }
    plays.push({ t: e.t, by: e.carrier, team: R.players[e.carrier].team,
                 x: c ? c[0] : null, y: c ? c[1] : null, z: c ? c[2] : null, tEnd: tEnd, last: null, defuser: null });
  });
  // The file records WHY each round ended (meta.roundEnds). In BOMB, reason 5
  // is the bomb going off: measured, 3 of 3 such rounds end 61-64 s after a
  // plant. So a plant followed by that is NOT a defuse however much coolant was
  // sprayed at it, and any other plant whose fuse time passes was defused --
  // the round simply carries on (the old rule only saw round-ending defuses).
  // THE BOMB'S OWN RECORDS (meta.worldOps, device object): planted at a point,
  // then gone with a reason -- 3 = defused by a named player (5 of 5 he is
  // 0.6-1.4 m from it using coolant), 1 = detonated (2 of 2, 60.0 s after the
  // plant). Where a plant has them, nothing below is inferred.
  var dev = ((R.meta && R.meta.worldOps) || []).filter(function (o) { return o.o === 1 && o.item === 0x2e; });
  plays.forEach(function (pl) {
    var pk = null, k2;
    for (k2 = 0; k2 < dev.length; k2++) {
      if (dev[k2].code === 0x65 && Math.abs(dev[k2].t - pl.t) < 3) { pk = k2; break; }
    }
    if (pk == null) return;
    pl.exact = true;
    if (dev[pk].p) { pl.x = dev[pk].p[0]; pl.y = dev[pk].p[1]; pl.z = dev[pk].p[2]; }
    // THE FREEZE GAUGE. While the bomb is sprayed the game writes a 16-bit number
    // every 0.2 s: its low 5 bits are the planter's lobby slot, the rest is a
    // gauge that climbs 13 a step and falls back the same way when the spraying
    // stops. MEASURED on 6 defuses: it ends at 246-253 every time (250 = frozen,
    // the same 0..250 scale as health), and on bombs that went off it never got
    // past 129. About 3.9 s of unbroken spray. (replay/itembox/defuse_gauge.js)
    pl.gauge = [];
    for (k2 = pk + 1; k2 < dev.length; k2++) {
      if (dev[k2].code === 0x65) break;
      if (dev[k2].code === 0xc1) { pl.gauge.push([dev[k2].t, ((dev[k2].slot | (dev[k2].h4 << 8)) >> 5) / 250]); continue; }
      if (dev[k2].code !== 0xa0) continue;
      var why = dev[k2].it ? dev[k2].it[4] : 0;
      if (why === 1) pl.boom = dev[k2].t;
      else if (why === 3 && dev[k2].who != null) { pl.defuser = dev[k2].who; pl.last = { i: dev[k2].who, t: dev[k2].t }; }
      break;
    }
  });
  R.bombExact = plays.length > 0 && plays.every(function (pl) { return pl.exact; });
  var roundEndsKnown = !!(R.meta && R.meta.roundEnds);
  if (roundEndsKnown) plays.forEach(function (pl) {
    if (pl.exact) return;
    R.meta.roundEnds.forEach(function (e) {
      if (e.code === 5 && e.t > pl.t + BOMB_FUSE - 12 && e.t < pl.t + BOMB_FUSE + 12) pl.boom = e.t;
    });
  });
  if (plays.length && R.meta && R.meta.mode === 6) {
    for (var f = 0; f < R.frames.length; f++) {
      var t = R.frames[f].t, row = R.frames[f].p || [];
      for (var q = 0; q < plays.length; q++) {
        var pl = plays[q];
        if (pl.exact || t < pl.t || t > pl.tEnd || pl.x == null) continue;
        if (roundEndsKnown && t > pl.t + BOMB_FUSE + 4) continue;
        for (var i = 0; i < row.length; i++) {
          var c2 = row[i];
          if (!c2 || c2[14] !== 0x30 || R.players[i].team === pl.team) continue;
          var dx = c2[0] - pl.x, dz = c2[2] - pl.z;
          if (dx * dx + dz * dz <= 4000 * 4000) pl.last = { i: i, t: t };
        }
      }
    }
    plays.forEach(function (pl) {
      if (pl.exact) return;
      if (roundEndsKnown) { if (pl.last && !pl.boom) pl.defuser = pl.last.i; }
      else if (pl.last && pl.tEnd - pl.last.t <= 6) pl.defuser = pl.last.i;
    });
  }
  R.bombPlays = plays;
}

// One line of objective state at the current time, top centre:
//   Base      how many bases each side OWNS right now
//   Capture   targets each side has put in the goal so far
//   Bomb      bombs planted / likely defused so far, per side
// Only what the file supports. Bombs remaining and race goals remaining are
// not in any decoded field, so they are not shown.
function updateObjHud() {
  var el = $('objhud');
  if (!el) {
    el = document.createElement('div');
    el.id = 'objhud';
    el.style.cssText = 'position:absolute;top:46px;left:50%;transform:translateX(-50%);' +
      'z-index:15;pointer-events:none;font-family:var(--f-disp);font-size:12px;' +
      'letter-spacing:.06em;text-transform:uppercase;color:var(--ink-dim);' +
      'background:rgba(11,13,12,.72);border:1px solid var(--hair);padding:4px 10px;white-space:nowrap';
    $('stage').appendChild(el);
  }
  if (!R) { el.style.display = 'none'; return; }
  var teams = [];
  R.players.forEach(function (p) { if (p.team != null && teams.indexOf(p.team) < 0) teams.push(p.team); });
  teams.sort();
  function line(label, per, suffix) {
    return label + ' &nbsp;' + teams.map(function (tm) {
      return '<b style="color:' + teamColor(tm) + '">' + (per[tm] || 0) + '</b>';
    }).join(' <span style="opacity:.5">:</span> ') + (suffix || '');
  }
  var out = [], i;
  if (R.meta && R.meta.baseGauge) {
    var own = {}, nb = 0;
    for (i = 0; i < BASE_VIDS.length; i++) {
      if (!baseSeries(i)) continue;
      nb++;
      var o = baseStateAt(i, T).owner;
      if (o) { var tm = baseSignTeam(o); own[tm] = (own[tm] || 0) + 1; }
    }
    if (nb) out.push(line('Bases held', own, ' <span style="opacity:.5">of ' + nb + '</span>'));
  }
  var mode = R.meta && R.meta.mode;
  // Team Sneaking: the side carrying the sneaking mark this round
  ((R.x && R.x.sneak) || []).forEach(function (sn) {
    if (T < sn[0] || T >= sn[1]) return;
    var votes = {}, bestT = null;
    sn[2].forEach(function (pi) { var p = R.players[pi]; if (p && p.team != null) votes[p.team] = (votes[p.team] || 0) + 1; });
    Object.keys(votes).forEach(function (k) { if (bestT == null || votes[k] > votes[bestT]) bestT = k; });
    if (bestT != null) out.push('Sneaking &nbsp;<b style="color:' + teamColor(+bestT) + '">' + esc(teamLabel(+bestT)) + '</b>');
  });
  // Team Sneaking: the rules record runs a 5 s countdown naming one player, and
  // restarts it when retriggered. MEASURED on 2 replays: the named player is on
  // the side that is NOT sneaking 15 of 15 times, and in 12 of 15 he hit someone
  // or fired in the 1.5 s before. CONFIRMED by the user (2026-10-05): when a
  // sneaker is hit, the whole sneaking team's stealth drops for a few seconds.
  // So this is that window, and the named player is the defender who landed
  // the hit.
  var stm = (R.meta && R.meta.sneakTimer) || [], alertBy = null, alertEnd = 0;
  for (i = 0; i < stm.length; i++) {
    if (stm[i].t > T) break;
    if (stm[i].secs === 5) { alertBy = stm[i].player; alertEnd = stm[i].t + 5; }
  }
  if (alertEnd > T) {
    // The named player is the DEFENDER in the exchange that broke stealth, on
    // either end of it: seen on screen (TheReals 0:25) he was the one STUNNED by
    // a sneaker, and in the corpus he is the one hitting in 7 of 15 and the one
    // hit in 2. So no "hit by" -- just who it happened with.
    out.push('<b style="color:#E2574C">Stealth down &nbsp;' + Math.ceil(alertEnd - T) + 's</b>' +
      (alertBy != null && R.players[alertBy] ? ' <span style="opacity:.6">contact:</span> <span style="color:' +
        teamColor(R.players[alertBy].team) + '">' + esc(R.players[alertBy].name || '') + '</span>' : ''));
  }
  // ROUNDS WON per team, from the game's own counter (it steps for a whole team
  // at once, 256 of 256 times, 56% within 1.5 s of a round-over record). A
  // team's figure is its highest player's, since late joiners have fewer.
  var rw = gameStat(STAT.roundsWon, T);
  if (rw) {
    var rwt = {}, anyRw = false;
    R.players.forEach(function (p, pi) { if (p.team != null && rw[pi] > (rwt[p.team] || 0)) { rwt[p.team] = rw[pi]; anyRw = true; } });
    if (anyRw) out.push(line('Rounds', rwt));
  }
  // CAPTURE: each team's capture clock (meta.capClock). 30 s to win, and it only
  // runs while that team has a target in the goal -- this is the mode's score.
  if (R.meta && R.meta.modeName === 'CAP' && R.meta.capClock && R.meta.capClock.length) {
    var cl_ = {}, ccs = R.meta.capClock, sess_ = null;
    for (i = 0; i < ccs.length; i++) {
      if (ccs[i].t > T) break;
      if (ccs[i].session !== sess_) { sess_ = ccs[i].session; cl_ = {}; }
      cl_[ccs[i].team] = ccs[i].left;
    }
    // one draining bar per team, like the game's own capture meters
    out.push('Capture &nbsp;' + teams.map(function (tm) {
      var v = cl_[tm] == null || cl_[tm] > 30 ? 30 : cl_[tm];
      return '<span style="display:inline-block;vertical-align:middle;width:64px;height:7px;border:1px solid var(--hair);margin:0 4px">' +
             '<span style="display:block;height:100%;width:' + Math.round(64 * v / 30) + 'px;background:' + teamColor(tm) + '"></span></span>' +
             '<b style="color:' + teamColor(tm) + '">' + v + 's</b>';
    }).join(' &nbsp; '));
  }
  if (R.meta && R.meta.modeName === 'RACE' && R.meta.raceOps && R.meta.raceOps.length) {
    var gl = {};
    R.meta.raceOps.forEach(function (o_) { if (o_.score && o_.t <= T) { var tm_ = teams[o_.team]; gl[tm_] = (gl[tm_] || 0) + 1; } });
    out.push(line('Goals', gl));
  }
  if (R.meta && (R.meta.modeName === 'RACE' || R.meta.modeName === 'CAP')) {
    dropSpans.forEach(function (dd) {
      if (T < dd.t0 || T >= dd.t1) return;
      var leftS = Math.max(0, Math.ceil(dd.t0 + dropResetS() - T));
      out.push('<span style="color:' + (dd.team == null ? 'var(--ink-dim)' : teamColor(dd.team)) + '">' +
               (dd.team == null ? 'Target' : esc(teamLabel(dd.team)) + ' target') + '</span> resets in ' +
               '<b style="color:' + (leftS <= 5 ? '#E2574C' : 'var(--ink)') + '">' + leftS + 's</b>');
    });
  }
  buildBombPlays();
  if (R.bombPlays.length) {
    var put = {}, def = {};
    R.bombPlays.forEach(function (pl) {
      if (pl.t > T) return;
      put[pl.team] = (put[pl.team] || 0) + 1;
      if (pl.defuser != null && pl.last.t <= T) {
        var dt = R.players[pl.defuser].team;
        def[dt] = (def[dt] || 0) + 1;
      }
    });
    out.push(line(mode === 6 ? 'Bombs planted' : 'Targets put', put));
    if (mode === 6) out.push(line(R.bombExact ? 'Defused' : 'Defused (likely)', def));
    if (mode === 6) {
      // bombs left this round = 3 minus the bombs LOST since the last round ended (bombLosses).
      // Until 2026-10-08 this subtracted PLANTS, so a bomb that timed out on the ground cost
      // nothing and a planted bomb counted as gone while it was still ticking.
      out.push('Bombs left &nbsp;<b>' + bombsLeftAt(T) + ' / ' + BOMBS_PER_ROUND + '</b>');
      var lastL = null; bombLosses().forEach(function (l) { if (l.t <= T) lastL = l; });
      if (lastL && T - lastL.t < 6) out.push('<b style="color:#E2574C">Bomb ' + lastL.how + '</b>' + (lastL.left ? ' &middot; new bomb at the start' : ' &middot; none left'));
    }
    if (mode === 6) {
      var liveBomb = null;
      R.bombPlays.forEach(function (pl) {
        var fuse = pl.exact ? BOMB_FUSE_EXACT : BOMB_FUSE;
        var until = pl.boom || (pl.defuser != null ? pl.last.t : Math.min(pl.t + fuse, pl.tEnd));
        if (pl.t <= T && T < until) liveBomb = pl;
      });
      if (liveBomb) out.push('<b style="color:var(--amber)">Bomb live &nbsp;' + (liveBomb.exact ? '' : '~') +
        Math.max(0, Math.ceil(liveBomb.t + (liveBomb.exact ? BOMB_FUSE_EXACT : BOMB_FUSE) - T)) + 's</b>');
      // how frozen it is right now (the gauge is only written while it moves)
      var gz_ = liveBomb && liveBomb.gauge, gpct = null;
      if (gz_) for (var gi = gz_.length - 1; gi >= 0; gi--) {
        if (gz_[gi][0] <= T) { if (T - gz_[gi][0] < 0.6) gpct = gz_[gi][1]; break; }
      }
      if (gpct != null && gpct > 0.02) {
        var gw = Math.round(clamp(gpct, 0, 1) * 60);
        out.push('Freezing <span style="display:inline-block;vertical-align:middle;width:60px;height:6px;' +
          'border:1px solid var(--hair);margin:0 4px"><span style="display:block;height:100%;width:' + gw +
          'px;background:#7FD4F0"></span></span><b>' + Math.round(clamp(gpct, 0, 1) * 100) + '%</b>');
      }
    }
  }
  ((R.meta && R.meta.roundEnds) || []).forEach(function (e) {
    if (T >= e.t && T - e.t < 10) {
      var why = (ROUND_END_TEXT[mode] || {})[e.code];
      out.push('<b style="color:var(--amber)">Round over' + (why ? ' &middot; ' + why : '') + '</b>');
    }
  });
  // WHO WON, from the rules record itself (meta.roundResults / matchResult):
  // shown for 12 s after each round, and the match result from the last one on.
  ((R.meta && R.meta.roundResults) || []).forEach(function (rr) {
    if (T < rr.t || T - rr.t >= 12) return;
    out.push(rr.winner != null ? '<b style="color:' + teamColor(rr.winner) + '">' + esc(teamLabel(rr.winner)) + ' wins the round</b>'
           : rr.draw ? '<b style="color:var(--amber)">Round drawn</b>' : '<b style="color:var(--ink-dim)">Round ended, no result</b>');
  });
  var mres = R.meta && R.meta.matchResult;
  if (mres && T >= mres.t) {
    out.push(mres.winner != null ? '<b style="color:' + teamColor(mres.winner) + '">Match: ' + esc(teamLabel(mres.winner)) + '</b>'
           : '<b style="color:var(--amber)">Match drawn</b>');
  }
  el.style.display = out.length ? 'block' : 'none';
  el.innerHTML = out.join(' &nbsp;<span style="opacity:.35">|</span>&nbsp; ');
}

// End-of-match scoreboard. kills/deaths come from the op-0x09 feed and are
// CONFIRMED (per-slot death counts match HP->0 on 196/197 slot-generations).
// Distance and time-alive are DERIVED here from the position stream, and are
// labelled as such so they are not mistaken for fields the file records.
// WHOLE GAME or SO FAR (user, 2026-10-04): the scoreboard can show the final
// card or the card as it stood at the playhead. LIM is the cut-off time; every
// count below ignores anything after it.
var scoreLive = false, scoreLiveSec = -1;
function renderScore() {
  if (!R) return;
  var LIM = scoreLive ? T : Infinity;
  var n = R.players.length;
  var dist = new Array(n).fill(0), aliveT = new Array(n).fill(0), last = {};
  var carry = new Array(n).fill(0);
  // Carry distance: the replay names the holder, never the item's position, so
  // distance carried is the CARRIER's own ground travel while holding it.
  // Intervals run pickup -> next pickup or drop.
  var spans = [];
  (R.events || []).forEach(function (e) {
    if (e.type !== 'objective' || e.t > LIM) return;
    var open = spans.length ? spans[spans.length - 1] : null;
    if (open && open.t1 == null) open.t1 = Math.min(open.cap, e.t);
    if (e.carrier != null) {
      spans.push({ i: e.carrier, t0: e.t, t1: null,
                   cap: clipToRound(e.t, R.duration) });
    }
  });
  if (spans.length && spans[spans.length - 1].t1 == null) {
    spans[spans.length - 1].t1 = spans[spans.length - 1].cap;
  }
  function carrierAtT(t) {
    for (var s = 0; s < spans.length; s++) {
      if (t >= spans[s].t0 && t <= spans[s].t1) return spans[s].i;
    }
    return null;
  }
  // Stuns dealt, and times this player was woken (the event names the person
  // who was woken, not who did the waking).
  var stuns = new Array(n).fill(0), woken = new Array(n).fill(0);
  // kills and deaths up to the cut-off, from the kill events (the whole-game
  // card uses the file's own totals instead)
  var liveK = new Array(n).fill(0), liveD = new Array(n).fill(0);
  (R.events || []).forEach(function (e) {
    if (e.t > LIM) return;
    if (e.type === 'kill') {
      if (e.victim != null && liveD[e.victim] != null) liveD[e.victim]++;
      if (e.killer != null && e.killer !== e.victim && liveK[e.killer] != null) liveK[e.killer]++;
    }
    if (e.type === 'stun' && e.killer != null) stuns[e.killer]++;
    if ((e.type === 'wake' || e.type === 'staminaRestore')
        && e.sincePlayerDeathSec == null && e.victim != null) woken[e.victim]++;
  });
  // Damage dealt to ENEMIES and damage taken, in points where 100 = one full
  // health bar. Hits the file could not attribute count only as damage taken.
  var showDmg = !!(R.dmg && R.dmg.length);
  var dmgOut = new Array(n).fill(0), dmgIn = new Array(n).fill(0);
  (R.dmg || []).forEach(function (h) {
    if (h[0] > LIM) return;
    var a = h[1], v = h[2], pts = h[3] / 2.5;
    if (dmgIn[v] != null) dmgIn[v] += pts;
    if (a >= 0 && a !== v && R.players[a] && R.players[v] &&
        R.players[a].team !== R.players[v].team) dmgOut[a] += pts;
  });
  for (var f = 0; f < R.frames.length; f++) {
    var row = R.frames[f].p || [], t = R.frames[f].t;
    if (t > LIM) break;
    for (var i = 0; i < n; i++) {
      var e = row[i];
      if (!e) continue;
      var pv = last[i];
      if (pv) {
        var dt = t - pv[3];
        if (dt > 0 && dt < 1) {
          if (e[4]) {
            aliveT[i] += dt;
            var dx = e[0] - pv[0], dz = e[2] - pv[2];
            var step = Math.sqrt(dx * dx + dz * dz);
            if (step < 2000) {
              dist[i] += step;   // ignore respawn teleports
              if (carrierAtT(t) === i) carry[i] += step;
            }
          }
        }
      }
      last[i] = [e[0], e[1], e[2], t];
    }
  }
  // ---- objectives, per player ---------------------------------------------
  // PUT / PLANT: the objective stream re-reports the SAME carrier with bit 0x40
  // set at the moment he sets the item down where it scores. MEASURED on the
  // Capture files: 13 of 13 such events have the carrier within 3 m of a goal
  // (one more sits 49 m out -- unexplained). In Bomb the same bit is the plant.
  // A DEFUSE is not an event in the file, so it is not shown.
  // BASE: a capture is the gauge completing (buildBaseCaptures); it is credited
  // to every living player of the capturing side standing on the point then.
  var mode = R.meta && R.meta.mode;
  var puts = new Array(n).fill(0), bcaps = new Array(n).fill(0);
  // CAPTURE: the game's own objective feed names every put (kind 0x02). The
  // 0x40 flag used before finds only some of them: over two matches all 26 of
  // its puts are in the feed and the feed has 15 more. Bomb plants are the same
  // by either route (4 of 4), so the flag stays there.
  var feedPuts = (R.meta && R.meta.modeName === 'CAP' && R.meta.objFeed) ? R.meta.objFeed.filter(function (e) { return e.kind === 2; }) : null;
  if (feedPuts && feedPuts.length) {
    feedPuts.forEach(function (e) { if (e.t <= LIM && e.player != null && puts[e.player] != null) puts[e.player]++; });
  } else {
    (R.events || []).forEach(function (e) {
      if (e.type === 'objective' && e.t <= LIM && e.carrierFlag40 && e.carrier != null && puts[e.carrier] != null) {
        puts[e.carrier]++;
      }
    });
  }
  buildBaseCaptures();
  var teamCaps = {};
  (R.baseCaptures || []).forEach(function (c) {
    if (c.t > LIM) return;
    var tm = baseSignTeam(c.sign);
    teamCaps[tm] = (teamCaps[tm] || 0) + 1;
    var mk = null;
    for (var q = 0; q < objMarks.length; q++) if (objMarks[q].base === c.base) { mk = objMarks[q]; break; }
    if (!mk || mk.wx == null) return;
    var row = (R.frames[frameIndexAt(c.t)] || {}).p || [];
    for (var pi = 0; pi < n; pi++) {
      var cell = row[pi];
      if (!cell || !cell[4] || R.players[pi].team !== tm) continue;
      var bx = cell[0] - mk.wx, bz = cell[2] - mk.wz;
      if (bx * bx + bz * bz <= 6000 * 6000) bcaps[pi]++;
    }
  });
  buildBombPlays();
  var defs = new Array(n).fill(0);
  R.bombPlays.forEach(function (pl) { if (pl.defuser != null && pl.last.t <= LIM) defs[pl.defuser]++; });
  var showDef = mode === 6 && R.bombPlays.length > 0;
  var hasBase = !!(R.baseCaptures && R.baseCaptures.length);
  // RACE: a goal is the rules record itself (meta.raceOps, score flag), and the
  // game names the scorer alongside it.
  var isRace = !!(R.meta && R.meta.modeName === 'RACE' && R.meta.raceOps && R.meta.raceOps.length);
  var raceTeamGoals = {};
  if (isRace) {
    var rteams = [];
    R.players.forEach(function (pl) { if (pl.team != null && rteams.indexOf(pl.team) < 0) rteams.push(pl.team); });
    rteams.sort();
    R.meta.raceOps.forEach(function (o_) {
      if (!o_.score || o_.t > LIM) return;
      var tm_ = rteams[o_.team];
      raceTeamGoals[tm_] = (raceTeamGoals[tm_] || 0) + 1;
      if (o_.scorer != null && puts[o_.scorer] != null) puts[o_.scorer]++;
    });
  }
  var hasPuts = puts.some(function (v) { return v > 0; }) || isRace;
  var objName = hasBase ? 'Cap' : isRace ? 'Goal' : (mode === 6 ? 'Plant' : 'Put');
  var showObj = hasBase || hasPuts;
  var insO = null;
  try { insO = insightRows(LIM); } catch (errO) { console.warn('insights off:', errO); }
  var showAst = showDmg && !!insO;
  var rows = R.players.map(function (p, i) {
    return { p: p, i: i, a: insO ? insO[i].ast : 0, k: scoreLive ? liveK[i] : p.kills, d: scoreLive ? liveD[i] : p.deaths,
             m: dist[i] / 1000, alive: aliveT[i],
             st: stuns[i], wk: woken[i], cm: carry[i] / 1000,
             ob: hasBase ? bcaps[i] : puts[i], df: defs[i],
             dOut: dmgOut[i], dIn: dmgIn[i],
             sh: showDmg ? shotStats(LIM).shots[i] : 0, ht: showDmg ? shotStats(LIM).hits[i] : 0 };
  });
  var teams = {};
  rows.forEach(function (r) {
    var t = r.p.team == null ? 'x' : r.p.team;
    (teams[t] = teams[t] || []).push(r);
  });
  // THE GAME'S OWN SCORE (meta.statOps, counter 8): the points the real
  // scoreboard shows, read from the file -- not computed here.
  var pts = gamePoints(LIM);
  // headshot kills and assists as the GAME counts them (same stream)
  var ghs = pts ? gameStat(STAT.headshots, LIM) : null, gas_ = pts ? gameStat(STAT.assists, LIM) : null;
  var lvl = pts ? levelChange(LIM) : null, lvp = pts ? levelPoints(LIM) : null;
  var lvr = pts ? levelRounds(LIM) : null;
  rows.forEach(function (r) { r.pts = pts ? pts[r.i] : null; r.ghs = ghs ? ghs[r.i] : 0; r.gas = gas_ ? gas_[r.i] : 0; r.lvl = lvl ? lvl[r.i] : null;
                              r.lvp = lvp ? lvp[r.i] : null; r.lvr = lvr ? lvr.map(function (d) { return d[r.i]; }) : null; });
  // What the points are made of, as far as the counters are named: the game's
  // own counts (not points -- only kills +3, assists +1, carry +1, base +1 and
  // rounds won +5 have a measured worth). Shown when hovering the Pts cell.
  var PTS_PARTS = [[STAT.kills, 'kills'], [STAT.headshots, 'headshots'], [STAT.assists, 'assists'],
                   [STAT.goals, 'goals / plants'], [51, 'carry points'],
                   // counter 55 is a per-mode objective slot: base capture only in Base
                   [55, (R.meta && R.meta.modeName === 'BASE') ? 'base capture points' : 'objective points'],
                   [STAT.stuns, 'stuns'], [STAT.roundsWon, 'rounds won']];
  var partVals = pts ? PTS_PARTS.map(function (q) { return gameStat(q[0], LIM); }) : [];
  var banked = capBanked(LIM);
  function ptsNote(pi) {
    var bits = [];
    PTS_PARTS.forEach(function (q, k) { if (partVals[k] && partVals[k][pi]) bits.push(partVals[k][pi] + ' ' + q[1]); });
    if (banked && banked[pi]) bits.push(banked[pi] + ' s banked on the capture clock');
    return bits.length ? 'From the game: ' + bits.join(', ') : 'The game\'s own score';
  }
  var html = '<table><thead><tr><th></th><th>Player</th><th>Clan</th>' +
    (pts ? '<th title="Score: the game\'s own points for this player, read from the replay">Pts</th>' : '') +
    '<th>K</th><th>D</th>' +
    (pts ? '<th title="Headshot kills, as the game counts them">HS</th>' +
           '<th title="Assists, as the game counts them (+1 point each)">Ast</th>' : '') +
    (lvl ? '<th title="The player\'s level (0 - 22, from his level points after the last finished round) and the game\'s own mark for each finished round: ++ gained 10 or more, + gained, - lost, -- lost 10 or more. This is EXP / level, not GP: the recording does not carry GP">Level</th>' : '') +
    (showAst ? '<th title="Assists: hurt an enemy that a team-mate then killed, in that same life">A</th>' : '') +
    '<th>K/D</th>' + (showObj ? '<th>' + objName + '</th>' : '') +
    (showDef ? '<th title="' + (R.bombExact ? 'Bombs the game records this player as defusing'
      : 'Likely defuse: last enemy to spray coolant on the planted bomb') + '">Defuse</th>' : '') +
    (showDmg ? '<th title="Damage dealt to enemies. 100 = one full health bar">Dmg</th>' +
               '<th title="Damage taken. 100 = one full health bar">Taken</th>' +
               '<th title="Bullets that hit someone out of shots fired with guns. Approximate: shots are counted from the firing action and may run up to 10% high">Acc</th>' : '') +
    '<th>Stun</th><th>Woken</th>' +
    '<th>Carry</th><th>Dist</th><th>Alive</th></tr></thead><tbody>';
  Object.keys(teams).sort().forEach(function (t) {
    var list = teams[t].sort(function (a, b) {
      return pts ? ((b.pts || 0) - (a.pts || 0)) || ((b.k || 0) - (a.k || 0)) : (b.k || 0) - (a.k || 0); });
    var tk = list.reduce(function (s, r) { return s + (r.k || 0); }, 0);
    var label = t === 'x' ? 'Unassigned'
      : ['Red Team', 'Blue Team', 'Team Charlie', 'Team Delta'][+t] || 'Team ' + t;
    // the team's objective total: its own base captures, or its players' puts
    var tob = hasBase ? (teamCaps[t] || 0)
            : isRace ? (raceTeamGoals[t] || 0)
            : list.reduce(function (s2, r) { return s2 + (r.ob || 0); }, 0);
    html += '<tr class="hdr"><td colspan="3">' + label + (t !== 'x' && matchWinnerTeam(LIM) === +t ? ' ' + iconMask('trophy', '#E8C547', 16) : '') + '</td>' +
            (pts ? '<td>' + list.reduce(function (s0, r) { return s0 + (r.pts || 0); }, 0) + '</td>' : '') + '<td>' + tk +
            '</td><td colspan="' + ((showAst ? 3 : 2) + (pts ? 2 : 0) + (lvl ? 1 : 0)) + '"></td>' + (showObj ? '<td>' + tob + '</td>' : '') +
            (showDef ? '<td>' + list.reduce(function (s3, r) { return s3 + (r.df || 0); }, 0) + '</td>' : '') +
            (showDmg ? '<td>' + Math.round(list.reduce(function (s4, r) { return s4 + r.dOut; }, 0)) +
                       '</td><td></td><td>' + (function () {
                         var s5 = 0, h5 = 0;
                         list.forEach(function (r) { s5 += r.sh; h5 += r.ht; });
                         return s5 ? Math.round(100 * Math.min(1, h5 / s5)) + '%' : '';
                       })() + '</td>' : '') +
            '<td colspan="5"></td></tr>';
    list.forEach(function (r) {
      var kd = (r.d ? (r.k / r.d) : r.k) || 0;
      html += '<tr data-team="' + (r.p.team == null ? '' : r.p.team) + '">' +
        '<td class="bar"></td>' +
        '<td class="nm">' + esc(r.p.name || '(unidentified)') + '</td>' +
        '<td class="cl">' + esc(r.p.clan || '—') + '</td>' +
        (pts ? '<td title="' + esc(ptsNote(r.i)) + '"><b>' + (r.pts || 0) + '</b></td>' : '') +
        '<td>' + (r.k == null ? '—' : r.k) + '</td>' +
        '<td>' + (r.d == null ? '—' : r.d) + '</td>' +
        (pts ? '<td>' + (r.ghs || '—') + '</td><td>' + (r.gas || '—') + '</td>' : '') +
        (lvl ? '<td style="white-space:nowrap"' +
               (r.lvp != null ? ' title="Level points after the last finished round: ' + r.lvp + ' (' + (r.lvl > 0 ? '+' : '') + (r.lvl || 0) + ' over the rounds so far)"' : '') + '>' +
               (r.lvp != null ? '<b>' + levelOf(r.lvp) + '</b> ' : '') + expMarksHtml(r.lvr) + '</td>' : '') +
        (showAst ? '<td>' + (r.a || '—') + '</td>' : '') +
        '<td>' + (r.k == null ? '—' : kd.toFixed(2)) + '</td>' +
        (showObj ? '<td' + (banked ? ' title="Targets put, and seconds this player ran his team\'s capture clock down"' : '') + '>' +
                   (r.ob || '—') + (banked && banked[r.i] ? ' <span style="opacity:.6">' + banked[r.i] + 's</span>' : '') + '</td>' : '') +
        (showDef ? '<td>' + (r.df || '—') + '</td>' : '') +
        (showDmg ? '<td>' + (Math.round(r.dOut) || '—') + '</td><td>' +
                   (Math.round(r.dIn) || '—') + '</td><td title="' + r.ht + ' hits / ' + r.sh + ' shots">' +
                   (r.sh >= 10 ? Math.round(100 * Math.min(1, r.ht / r.sh)) + '%' : '—') + '</td>' : '') +
        '<td>' + (r.st || '—') + '</td>' +
        '<td>' + (r.wk || '—') + '</td>' +
        '<td>' + (r.cm > 0 ? r.cm.toFixed(0) + ' m' : '—') + '</td>' +
        '<td>' + r.m.toFixed(0) + ' m</td>' +
        '<td>' + fmt(r.alive) + '</td></tr>';
    });
  });
  html += '</tbody></table>';
  try { html += renderInsights(LIM, teams, showObj ? objName : null, insO); } catch (errI) { console.warn('insights table off:', errI); }
  $('s-body').innerHTML = html;
  $('s-n').textContent = (R.stageGuess || '') + '  ·  ' + (scoreLive ? 'so far, at ' + fmt(T) + ' of ' : '') + fmt(R.duration);
  if ($('s-mode')) $('s-mode').textContent = scoreLive ? 'So far' : 'Whole game';
  // Base captures, when the file carries capture gauges at all.
  buildBaseCaptures();
  var bcap = '';
  if (R.baseCaptures && R.baseCaptures.length) {
    var per = {}, perBase = {};
    R.baseCaptures.forEach(function (c) {
      if (c.t > LIM) return;
      var tm = baseSignTeam(c.sign);
      per[tm] = (per[tm] || 0) + 1;
      perBase[c.base] = (perBase[c.base] || 0) + 1;
    });
    var parts = [];
    Object.keys(per).sort().forEach(function (tm) {
      parts.push('<b style="color:' + teamColor(+tm) + '">' + per[tm] + '</b> ' +
                 esc(teamLabel(+tm)));
    });
    var byb = [];
    for (var b = 0; b < 5; b++) if (perBase[b]) byb.push((b + 1) + '\u00d7' + perBase[b]);
    bcap = '<div style="margin-top:6px">Base captures: ' + parts.join(' &nbsp; ') +
           (byb.length ? ' <span style="color:var(--ink-faint)">(base ' +
            byb.join(', ') + ')</span>' : '') + '</div>';
  }
  var stuns = (R.events || []).filter(function (e) { return e.type === 'stun' && e.t <= LIM; }).length;
  var objNote = !showObj ? ''
    : hasBase ? 'Cap = bases taken for the team (ownership changed) while this player stood on the point (within 6 m). '
    : mode === 6 ? (R.bombExact ? 'Plant = bombs this player planted. Defuse = bombs the game records this player as defusing. '
      : 'Plant = bombs this player planted. Defuse is INFERRED: the file has no ' +
      'defuse event, so it is the last enemy to spray coolant on the bomb just before the round ended. ')
    : isRace ? 'Goal = goals this player carried the target into, as the game records them. '
    : 'Put = targets this player set down in the goal. ';
  $('s-foot').innerHTML = bcap + objNote +
    'K / D are read from the game’s own kill feed. ' +
    'Dist and Alive are computed here from the position stream, not recorded in the file. ' +
    stuns + ' stun' + (stuns === 1 ? '' : 's') + (scoreLive ? ' so far.' : ' this match.');
}

// ---- MATCH INSIGHTS -------------------------------------------------------
// Two kinds of thing, kept apart on purpose.
//   R.x    streams the file RECORDS but the hosted build cuts off the cell:
//          who a weapon is engaging, incapacitation, aim-origin side, gas hits.
//          Built by RPDT.extras for a dropped .dat, or injected ahead of time
//          by replay/inject_extras.js for a hosted replay.
//   R.ins  things COUNTED here from what the file records: first blood,
//          multi-kills, trades, kill range, CQC, rubber-band snaps, rolls,
//          late joins. None of these is a field in the file.
var CQC_ACT = { 23: 1, 24: 1, 25: 1, 26: 1 };   // actions 0x17-0x1A: the unarmed / CQC family
var MULTI_S = 4.0, TRADE_S = 4.0, LATE_S = 20, AIM_SHOW_S = 0.3;
var OBJ_STAND_MM = 2500;
var ASSIST_MIN = 25;          // health bytes: a tenth of a full bar
var ACT_SHIELD = 27, ACT_COOLANT = { 48: 1, 50: 1 };   // actions 0x1B, 0x30 / 0x32
var ZONE_HEAD = 0x12, ZONE_KNOWN = { 0x11: 1, 0x12: 1, 0x91: 1 };

function buildExtras(r) {
  if (!r || r.x || r.motionGroups || typeof RPDT === 'undefined' || !RPDT.extras) return;
  if (!r.meta || !r.meta.positionCell || r.meta.positionCell.indexOf('targetSlot') < 0) return;
  try { r.x = RPDT.extras(r); } catch (e) { console.warn('extras off:', e); }
}

// Player pi's last reported cell at or before t.
function insCell(r, pi, t) {
  var fi = Math.min(frameIndexAtIn(r, t), r.frames.length - 1);
  if (r.frames[fi].t > t && fi > 0) fi--;
  for (var k = fi; k >= 0 && k > fi - 120; k--) {
    var c = (r.frames[k].p || [])[pi];
    if (c && c[0] != null) return c;
  }
  return null;
}

function buildInsights(r) {
  if (!r || !r.frames || !r.frames.length || r.ins) return;
  var n = r.players.length, frames = r.frames, i, f;
  var ins = r.ins = { cqc: [], lobby: [], snaps: [], rolls: [], aimBy: [], koBy: [], rag: [] };
  // BODY TRACKS (newer-format files). While a player is knocked flying or dead
  // the game stops sending ordinary position records and sends a ragdoll form
  // instead, ~30 a second for the 8 s until the respawn. Those rows are kept
  // out of frames[] (positionVariants), so the body used to be left standing
  // at its last ordinary sample: a median 1 m, and up to 6.7 m, from where it
  // actually ended up (measured over 69 deaths in two matches).
  (r.positionVariants || []).forEach(function (v) {
    // A ROLL INSIDE THIS FORM (user, 2026-10-10, Groznyj Grad Base 0:28: "he's still skating. I think maybe he got naded while
    // rolling away?" - he did: forward roll at 28.06 s, blast at 28.33 s). While the game is sending the long rows after a
    // knock-down, a roll comes as 67-byte rows (state 2 / 3, with the roll's start point) - they carry no ragdoll point, so
    // they were dropped and he slid along in his last running pose. They are kept now, marked 3 = rolling.
    if ((!v.ragdoll && v.len !== 67) || v.player == null || v.x == null) return;
    // [6] HIPS HEIGHT above the ground and [7] WHAT HE IS DOING (2026-10-09, read off replay_352519_7, player thrown by
    // grenades at 24.2 s and 28.7 s): the row's own `ragdoll` point is the body in the air - 1001, 1383, 977, 590, 303,
    // 226 mm over 2 s, a real arc - and its motion name is 'explosion knockback' while he flies, then 'run forward' /
    // 'stand idle' / 'walk' once he is back up and the game is STILL sending this form. 0 = up and moving normally,
    // 1 = thrown by a blast, 2 = getting up; [8] is the motion state byte for the ordinary clip chain.
    var rk_ = v.len === 67 ? 3 : v.motionName === 'dead' ? 0 : (v.motionState === 24 || v.motionName === 'explosion knockback') ? 1 : /get up/.test(v.motionName || '') ? 2 : 0;
    (ins.rag[v.player] || (ins.rag[v.player] = [])).push([v.t, v.x, v.y, v.z, v.headingDeg, v.motionName === 'dead' ? 1 : 0,
      (v.ragdoll && v.ragdoll[1] != null && v.y != null) ? v.ragdoll[1] - v.y : null, rk_, v.motionState, v.motionName || null,
      v.stateFlags == null ? null : v.stateFlags, v.item == null ? null : v.item, v.ammo == null ? null : v.ammo, v.action == null ? null : v.action,
      v.aim ? v.aim[0] : null, v.aim ? v.aim[1] : null, v.aim ? v.aim[2] : null]);
  });
  ins.rag.forEach(function (a) { if (a) a.sort(function (x, y) { return x[0] - y[0]; }); });
  // BLOWN OVER IS NOT KILLED (user, 2026-10-09, Groznyj Grad Base: a player thrown by a grenade, getting up, thrown again and
  // only then killed was shown dead - ghosted, "KIA" - through all three). The ragdoll form's motion name reads 'dead' for a
  // body that is merely flying, so on its own it is not proof of a death. A run of 'dead' rows now counts only when the kill
  // feed has this player as a victim around it (from 2 s before the run to its end); otherwise it is a knock-down and he
  // stays alive. ins.ragFalse counts the runs corrected, for checking.
  ins.ragFalse = 0;
  var deathsBy = {};
  (r.events || []).forEach(function (e) {
    if (e.type === 'kill' && e.victim != null) (deathsBy[e.victim] || (deathsBy[e.victim] = [])).push(e.t);
  });
  ins.rag.forEach(function (a, pi) {
    if (!a) return;
    var k = 0;
    while (k < a.length) {
      if (!a[k][5]) { k++; continue; }
      var e2 = k;
      while (e2 + 1 < a.length && a[e2 + 1][5] && a[e2 + 1][0] - a[e2][0] < 1.2) e2++;
      var t0 = a[k][0] - 2.0, t1 = a[e2][0] + 0.2;
      var tds = (deathsBy[pi] || []).filter(function (td) { return td >= t0 && td <= t1; });
      if (!tds.length) { for (var q = k; q <= e2; q++) a[q][5] = 0; ins.ragFalse++; }
      // killed part-way through a knock-down: the rows before the kill are still only the knock-down
      else { for (var q2 = k; q2 <= e2 && a[q2][0] < tds[0] - 0.3; q2++) a[q2][5] = 0; }
      k = e2 + 1;
    }
  });
  function enemy(a, b) {
    var pa = r.players[a], pb = r.players[b];
    return a !== b && !!pa && !!pb && (pa.team == null || pb.team == null || pa.team !== pb.team);
  }
  for (i = 0; i < n; i++) { ins.aimBy.push([]); ins.koBy.push([]); ins.snaps.push([]); ins.rolls.push([]); }
  if (r.x) {
    (r.x.aim || []).forEach(function (a) { if (ins.aimBy[a[2]]) ins.aimBy[a[2]].push(a); });
    (r.x.ko || []).forEach(function (k) { if (ins.koBy[k[2]]) ins.koBy[k[2]].push(k); });
  }

  // ---- kills: first blood per round, multi-kills, trades, range -----------
  var rounds = [0], rc = r.meta && r.meta.roundClock;
  if (rc) for (i = 1; i < rc.length; i++) if (rc[i][1] > rc[i - 1][1] + 1.0) rounds.push(rc[i][0]);
  var kills = (r.events || []).filter(function (e) { return e.type === 'kill' && e.victim != null; });
  var ri = 0, fbDone = -1, lastK = {}, chain = {};
  kills.forEach(function (e, idx) {
    while (ri + 1 < rounds.length && e.t >= rounds[ri + 1]) ri++;
    var real = e.killer != null && e.killer !== e.victim && !e.suicide;
    e._fb = false; e._multi = 0; e._trade = false; e._range = null;
    if (!real) return;
    if (fbDone < ri) { fbDone = ri; e._fb = true; }
    chain[e.killer] = (lastK[e.killer] != null && e.t - lastK[e.killer] <= MULTI_S) ? chain[e.killer] + 1 : 1;
    lastK[e.killer] = e.t;
    if (chain[e.killer] >= 2) e._multi = chain[e.killer];
    // a trade: the victim had just killed one of the killer's team-mates
    for (var b = idx - 1; b >= 0 && e.t - kills[b].t <= TRADE_S; b--) {
      var pk = kills[b];
      if (pk.killer === e.victim && pk.victim !== e.killer && pk.victim !== e.victim &&
          !enemy(pk.victim, e.killer)) { e._trade = true; break; }
    }
    var ck = insCell(r, e.killer, e.t), cv = insCell(r, e.victim, e.t);
    if (ck && cv) {
      var dx = ck[0] - cv[0], dy = ck[1] - cv[1], dz = ck[2] - cv[2];
      e._range = Math.sqrt(dx * dx + dy * dy + dz * dz) / 1000;
    }
  });

  // ---- assists ---------------------------------------------------------------
  // A team-mate of the killer who hurt the victim during the life that just
  // ended (there is no healing, so every hit in a life counted towards it).
  // ASSIST_MIN keeps a single graze from counting.
  var hitsOn = {}, lastDeath = {};
  (r.dmg || []).forEach(function (h) { (hitsOn[h[2]] = hitsOn[h[2]] || []).push(h); });
  kills.forEach(function (e) {
    e._assist = [];
    var from = lastDeath[e.victim] == null ? -1 : lastDeath[e.victim];
    lastDeath[e.victim] = e.t;
    if (e.killer == null || e.killer === e.victim) return;
    var per = {};
    (hitsOn[e.victim] || []).forEach(function (h) {
      if (h[0] > from && h[0] <= e.t + 0.05 && h[1] >= 0 && h[1] !== e.killer && h[1] !== e.victim &&
          !enemy(h[1], e.killer)) per[h[1]] = (per[h[1]] || 0) + h[3];
    });
    Object.keys(per).forEach(function (k) { if (per[k] >= ASSIST_MIN) e._assist.push(+k); });
  });

  // ---- per-player passes over the position stream --------------------------
  var cqcRuns = [];
  for (i = 0; i < n; i++) {
    var first = null, last = null, lastAlive = false, prev = null, hist = [], open = null;
    for (f = 0; f < frames.length; f++) {
      var c = (frames[f].p || [])[i];
      if (!c || c[0] == null) continue;
      var t = frames[f].t;
      if (first == null) first = t;
      last = t; lastAlive = !!c[4];
      // CQC: a run of the unarmed-attack actions
      var on = c.length > 14 && CQC_ACT[c[14]] && c[4];
      if (on) { if (!open) open = { t0: t, last: t, a: i }; else open.last = t; }
      else if (open) { cqcRuns.push(open); open = null; }
      // RUBBER BAND: yanked, faster than anyone can move, back to within 0.4 m
      // of where they stood 0.2-0.8 s earlier. That is the measured shape of a
      // host correction; a roll or a respawn never lands on the player's own
      // recent position.
      if (prev && c[4] && prev.c[4]) {
        var dt = t - prev.t;
        var sx = c[0] - prev.c[0], sz = c[2] - prev.c[2];
        var step = Math.sqrt(sx * sx + sz * sz);
        if (dt > 0 && dt <= 0.2 && step > 700 && step < 6000 && step / dt > 9000) {
          for (var q = 0; q < hist.length; q++) {
            var age = t - hist[q][0];
            if (age < 0.2 || age > 0.8) continue;
            var hx = c[0] - hist[q][1], hz = c[2] - hist[q][2];
            if (hx * hx + hz * hz < 400 * 400) { ins.snaps[i].push(t); break; }
          }
        }
      }
      hist.push([t, c[0], c[2]]);
      while (hist.length && t - hist[0][0] > 0.9) hist.shift();
      prev = { t: t, c: c };
    }
    if (open) cqcRuns.push(open);
    ROLL_SRC = r; ins.rolls[i] = rollWindows(frames, i).map(function (w) { return frames[w[0]].t; });
    var dur = r.duration || frames[frames.length - 1].t;
    if (first != null && first > LATE_S) ins.lobby.push({ t: first, type: 'join', victim: i });
    // Someone who died in a one-life round and sat out the rest has not left:
    // only a player whose LAST sample is alive, well before the end, is gone.
    if (last != null && lastAlive && last < dur - LATE_S) ins.lobby.push({ t: last, type: 'leave', victim: i });
  }

  // ---- CQC: name the victim -------------------------------------------------
  // The weapon's own target field when the file gives one; otherwise the
  // nearest living enemy inside arm's reach. A swing with nobody there is not
  // an event.
  cqcRuns.sort(function (a, b) { return a.t0 - b.t0; });
  var lastCqc = {};
  cqcRuns.forEach(function (run) {
    var v = null, src = null;
    ins.aimBy[run.a].forEach(function (a) {
      if (v == null && a[0] <= run.last + 0.5 && a[0] + a[1] >= run.t0 - 0.5 && enemy(run.a, a[3])) {
        v = a[3]; src = 'target';
      }
    });
    if (v == null) {
      var ca = insCell(r, run.a, run.t0), best = 2500 * 2500;
      for (var j = 0; ca && j < n; j++) {
        if (!enemy(run.a, j)) continue;
        var cj = insCell(r, j, run.t0);
        if (!cj || !cj[4] || Math.abs(cj[1] - ca[1]) > 1500) continue;
        var ex = cj[0] - ca[0], ez = cj[2] - ca[2], d2 = ex * ex + ez * ez;
        if (d2 < best) { best = d2; v = j; src = 'near'; }
      }
    }
    if (v == null) return;
    var key = run.a + ':' + v;
    if (lastCqc[key] != null && run.t0 - lastCqc[key] < 2.0) { lastCqc[key] = run.last; return; }
    lastCqc[key] = run.last;
    ins.cqc.push({ t: run.t0, type: 'cqc', killer: run.a, victim: v, by: src });
  });
  ins.lobby.sort(function (a, b) { return a.t - b.t; });
}

// Who player pi's weapon is engaging at time t, or null. A run is held on
// screen for AIM_SHOW_S because most are one or two ticks long.
function aimAt(pi, t) {
  var a = R && R.ins && R.ins.aimBy[pi];
  if (!a || !a.length) return null;
  var lo = 0, hi = a.length - 1;
  if (t < a[0][0]) return null;
  while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (a[mid][0] <= t) lo = mid; else hi = mid - 1; }
  return t <= a[lo][0] + Math.max(a[lo][1], AIM_SHOW_S) ? a[lo][3] : null;
}
// GEAR STATE on the name tag (R.x.gear, from the item record's flag byte):
// the ENVG switched on, or the player inside the drum can. Both MEASURED: each
// flag is only ever set while that player carries that item (ENVG 113 s, drum
// can 82 s, zero seconds otherwise, 17 replays). Flag 20 is NOT labelled: it
// was guessed to be the cardboard box, but its players run at a median 3.9 m/s
// with a weapon out (replay/itembox/itemflag_what.js), so that guess is dead.
var GEAR_TAG = { 22: '  ENVG ON', 10: '  IN DRUM' };
function gearAt(pi, t) {
  var g = R && R.x && R.x.gear;
  if (!g) return null;
  for (var k = 0; k < g.length; k++) {
    if (g[k][0] > t) break;
    if (g[k][2] === pi && t <= g[k][1]) return g[k][3];
  }
  return null;
}
// 12 px icons for the name tag: a drum can (olive barrel with two hoops) and night
// vision goggles (two green lenses on a bar).
function drawGearIcon(x, cx, cy, kind) {
  x.save();
  if (kind === 10) {
    x.fillStyle = '#7C8A4A'; x.strokeStyle = '#D9E0B8'; x.lineWidth = 1;
    x.beginPath(); x.ellipse(cx, cy - 4, 4, 1.6, 0, 0, Math.PI * 2); x.rect(cx - 4, cy - 4, 8, 8); x.fill();
    x.beginPath(); x.ellipse(cx, cy + 4, 4, 1.6, 0, 0, Math.PI * 2); x.fill();
    x.beginPath(); x.ellipse(cx, cy - 4, 4, 1.6, 0, 0, Math.PI * 2); x.stroke();
    x.beginPath(); x.moveTo(cx - 4, cy - 4); x.lineTo(cx - 4, cy + 4); x.ellipse(cx, cy + 4, 4, 1.6, 0, Math.PI, 0, true); x.lineTo(cx + 4, cy - 4); x.stroke();
    x.globalAlpha *= 0.8;
    x.beginPath(); x.ellipse(cx, cy - 1.2, 4, 1.4, 0, 0, Math.PI); x.stroke();
    x.beginPath(); x.ellipse(cx, cy + 1.6, 4, 1.4, 0, 0, Math.PI); x.stroke();
  } else {
    x.strokeStyle = '#C9D0C2'; x.lineWidth = 1.5;
    x.beginPath(); x.moveTo(cx - 6, cy - 3); x.lineTo(cx + 6, cy - 3); x.stroke();
    x.fillStyle = '#6BE07A';
    x.beginPath(); x.arc(cx - 3, cy + 1, 3, 0, Math.PI * 2); x.arc(cx + 3, cy + 1, 3, 0, Math.PI * 2); x.fill();
    x.fillStyle = '#0B0D0C';
    x.beginPath(); x.arc(cx - 3, cy + 1, 1.2, 0, Math.PI * 2); x.arc(cx + 3, cy + 1, 1.2, 0, Math.PI * 2); x.fill();
  }
  x.restore();
}
function gearTag(pi, t) { return GEAR_TAG[gearAt(pi, t)] || ''; }
// Is this player on the sneaking side of the current round with its stealth
// intact? (R.x.sneak = who carried the sneaking mark each round; meta.sneakTimer
// = the 5 s windows in which stealth is down.)
function sneakCloaked(pi, t) {
  var sn = R && R.x && R.x.sneak;
  if (!sn || !sn.length) return false;
  var inRound = false;
  for (var k = 0; k < sn.length; k++) {
    if (t >= sn[k][0] && t < sn[k][1] && sn[k][2].indexOf(pi) >= 0) { inRound = true; break; }
  }
  if (!inRound) return false;
  var tm = (R.meta && R.meta.sneakTimer) || [];
  for (var q = 0; q < tm.length; q++) {
    if (tm[q].t > t) break;
    if (tm[q].secs === 5 && t < tm[q].t + 5) return false;
  }
  return true;
}
// THE GAME'S LIVE STREAK for a player at time t, from the scoreboard stream's
// run tracker: counter 152 = kind of the current run (5 headshot kill, 1 other
// kill, 2 back from a death), counter 153 = its length. Returns the number of
// kills in a row, or 0 when the current run is not a kill run.
function killRunAt(pi, t) {
  var by = R && R.__runs;
  if (!by) {
    by = R.__runs = {};
    ((R.meta && R.meta.statOps) || []).forEach(function (r) {
      if (r[2] === 152 || r[2] === 153) (by[r[1]] || (by[r[1]] = [])).push(r);
    });
  }
  var a = by[pi], kind = 0, len = 0;
  if (!a) return 0;
  for (var k = 0; k < a.length; k++) {
    if (a[k][0] > t) break;
    if (a[k][2] === 152) kind = a[k][3]; else len = a[k][3];
  }
  return (kind === 1 || kind === 5) ? len : 0;
}
// seconds until player pi wakes, or 0 when not knocked out
function koLeft(pi, t) {
  var a = R && R.ins && R.ins.koBy[pi];
  if (!a) return 0;
  for (var k = 0; k < a.length; k++) { if (a[k][0] > t) break; if (t <= a[k][1]) return a[k][1] - t; }
  return 0;
}
function koAt(pi, t) {
  var a = R && R.ins && R.ins.koBy[pi];
  if (!a) return false;
  for (var k = 0; k < a.length; k++) { if (a[k][0] > t) break; if (t <= a[k][1]) return true; }
  return false;
}

// Kill feed + the derived rows (CQC, late joins, early leaves), in time order.
function feedList() {
  if (!R) return [];
  if (R.__feed) return R.__feed;
  var out = (R.events || []).slice();
  if (R.ins) out = out.concat(R.ins.cqc, R.ins.lobby);
  (R.scans || []).forEach(function (sc) { out.push({ t: sc.t0, type: 'scan', killer: sc.a, victim: sc.v }); });
  ((R.meta && R.meta.raceOps) || []).forEach(function (o_) {
    if (o_.score) out.push({ t: o_.t, type: 'goal', victim: o_.scorer, bit: o_.team });
  });
  // TEAM SNEAKING: the target being USED UP at its goal (target record 0xfe). In
  // Bomb that same record is the plant, to the tenth of a second, 10 of 10; in
  // Team Sneaking it has been seen ONCE, 6.5 s before that round ended, so the
  // reading "delivered" rests on the Bomb case. Credited to whoever took the
  // target last.
  // CORRECTED 2026-10-05 after seeing it on screen (TheReals): the round was won
  // at 65 s and the 0xfe record came at 81.5 s, so 0xfe is the target being
  // CLEARED AWAY (after the win here; at the plant in Bomb), not the delivery.
  // The delivery is the round result itself: when the side that wins the round
  // has a player CARRYING a target at that instant, he is credited with it.
  if (R.meta && R.meta.modeName === 'TSNE') {
    var tgo = R.meta.targetOps || [];
    ((R.meta && R.meta.roundResults) || []).forEach(function (rr) {
      if (rr.winner == null) return;
      var held = {};
      for (var q2 = 0; q2 < tgo.length; q2++) {
        if (tgo[q2].t > rr.t + 0.5) break;
        held[tgo[q2].item] = tgo[q2].kind === 'take' ? tgo[q2] : null;
      }
      Object.keys(held).forEach(function (k) {
        var h = held[k];
        if (h && h.player != null && R.players[h.player] && R.players[h.player].team === rr.winner) {
          out.push({ t: rr.t, type: 'goal', victim: h.player, bit: 0 });
        }
      });
    });
  }
  out.sort(function (a, b) { return a.t - b.t; });
  // STUN-KNIFE HITS, NAMED AND CREDITED (2026-10-08). A hit with item 1 comes in three kinds, told apart by the damage
  // type on its record: 45 / 173 = the knock-out STUN STAB, 17 = the lethal STAB, 145 = the SLICE (105 hits over 158
  // matches, scratch loop/knife4.js). The file usually leaves the attacker blank on them, which the feed showed as "?",
  // while a separate derived "melee" row a second earlier named him. That row is the same event: its attacker is put
  // on the knife line and the duplicate is dropped.
  var KNIFE_KIND = { 45: 'STUN STAB', 173: 'STUN STAB', 17: 'STAB', 145: 'SLICE' };
  out.forEach(function (e, ei) {
    if (e.weapon !== 1 || (e.type !== 'stun' && e.type !== 'kill')) return;
    var st_ = e.subtype;
    if (st_ == null) for (var dq = ei; dq >= 0 && e.t - out[dq].t < 0.5; dq--) { if (out[dq].type === 'damage' && out[dq].victim === e.victim && out[dq].weapon === 1) { st_ = out[dq].subtype; break; } }
    if (KNIFE_KIND[st_]) e._knife = KNIFE_KIND[st_];
    for (var cq = 0; cq < out.length; cq++) {
      var cr = out[cq];
      if (cr.type !== 'cqc' || cr.victim !== e.victim || Math.abs(cr.t - e.t) > 1.6) continue;
      if (e.killer == null) { e.killer = cr.killer; e._credited = 'melee row'; }
      if (e.killer === cr.killer) cr._dup = true;
    }
  });
  out = out.filter(function (e) { return !e._dup; });
  // HEADSHOT OR NOT, AS THE GAME SCORED IT. The scoreboard stream writes a
  // "kind" byte for the killer at each kill: 5 = headshot kill, 1 = any other
  // (all 1,599 fives are his kill and 88% are head by hit zone; of 771 ones,
  // 79% are his kill and 3% head -- replay/itembox/stat_when_152.js). Where a
  // kill has one within half a second it decides the feed's "headshot" tag;
  // the hit zone stays as the fallback.
  // (The kind byte is only written when it CHANGES, so two headshots in a row
  // leave one row: it covered just 70% of kills. The COUNTERS always change, so
  // they are used instead: the killer's kill count stepping is the kill, and
  // his headshot count stepping in the same record makes it a headshot.)
  var kRows = {}, hRows = {};
  ((R.meta && R.meta.statOps) || []).forEach(function (r) {
    if (r[2] === STAT.kills) (kRows[r[1]] || (kRows[r[1]] = [])).push({ t: r[0], used: false });
    else if (r[2] === STAT.headshots) (hRows[r[1]] || (hRows[r[1]] = [])).push(r[0]);
  });
  out.forEach(function (e) {
    if (e.type !== 'kill' || e.killer == null || e.suicide) return;
    var a = kRows[e.killer], best = null;
    if (!a) return;
    for (var q = 0; q < a.length; q++) {
      var dt = a[q].t - e.t;
      if (dt < -0.15) continue;
      if (dt > 0.6) break;
      if (!a[q].used) { best = a[q]; break; }
    }
    if (!best) return;
    best.used = true;
    var hs = hRows[e.killer] || [];
    e._ghs = false;
    for (var q2 = 0; q2 < hs.length; q2++) { if (Math.abs(hs[q2] - best.t) < 0.06) { e._ghs = true; break; } if (hs[q2] > best.t + 0.06) break; }
  });
  // THE GAME'S OWN STREAK NOTICES (meta.streakOps), hung on the kill that
  // earned them: the named player's kill (kinds 1, 2) or death (kind 3) in the
  // half second before the notice.
  ((R.meta && R.meta.streakOps) || []).forEach(function (s) {
    if (s.player == null) return;
    for (var q = out.length - 1; q >= 0; q--) {
      var e = out[q];
      if (e.type !== 'kill' || e.t > s.t + 0.05) continue;
      if (e.t < s.t - 0.6) break;
      if (s.kind === 3 ? e.victim === s.player : e.killer === s.player) {
        // one kill can carry two notices (3 and 5 in the same tick): keep the bigger
        if (s.kind === 3) e._dstreak = Math.max(e._dstreak || 0, s.count);
        else e._streak = Math.max(e._streak || 0, s.count);
        break;
      }
    }
  });
  return (R.__feed = out);
}

// Lines from a player to whoever their weapon is engaging. With somebody
// selected only their own lines (and lines AT them) are drawn.
var AIM_ON = true, aimLines = [];
function updateAimLines() {
  var used = 0;
  if (AIM_ON && R && R.ins && R.x) {
    for (var i = 0; i < live.length; i++) {
      var p = live[i];
      if (!p || !p.alive) continue;
      var tg = aimAt(i, T);
      if (tg == null || !live[tg] || !live[tg].alive) continue;
      if (follow >= 0 && i !== follow && tg !== follow) continue;
      var q = live[tg], ln = aimLines[used];
      if (!ln) {
        ln = new THREE.Line(new THREE.BufferGeometry().setAttribute('position',
               new THREE.BufferAttribute(new Float32Array(6), 3)),
             new THREE.LineBasicMaterial({ transparent: true, opacity: 0.55, depthTest: false }));
        ln.renderOrder = 5; ln.frustumCulled = false;
        scene.add(ln); aimLines.push(ln);
      }
      var a = ln.geometry.attributes.position.array;
      var mz = muzzleOf(i);
      if (mz) { a[0] = mz.x; a[1] = mz.y; a[2] = mz.z; }
      else { a[0] = (p.x - ctr[0]) * S; a[1] = p.y * S + 13; a[2] = (p.z - ctr[2]) * S; }
      a[3] = (q.x - ctr[0]) * S; a[4] = q.y * S + 11; a[5] = (q.z - ctr[2]) * S;
      ln.geometry.attributes.position.needsUpdate = true;
      ln.material.color.set(teamColor(R.players[i].team));
      ln.visible = true;
      used++;
    }
  }
  for (var k = used; k < aimLines.length; k++) aimLines[k].visible = false;
}
function setAim(on) {
  AIM_ON = on;
  if ($('b-aim')) $('b-aim').dataset.on = on ? '1' : '0';
}

// HEAT: where people moved, died or killed, as dots at the real positions (so
// a multi-storey map keeps its floors apart). Whole match; with a player
// selected, only theirs.
var HEAT = { mode: 0, pts: null };
var HEAT_LABEL = ['Heat', 'Heat: moves', 'Heat: deaths', 'Heat: kills'];
function buildHeat() {
  if (HEAT.pts) { scene.remove(HEAT.pts); HEAT.pts.geometry.dispose(); HEAT.pts.material.dispose(); HEAT.pts = null; }
  if ($('b-heat')) { $('b-heat').textContent = HEAT_LABEL[HEAT.mode]; $('b-heat').dataset.on = HEAT.mode ? '1' : '0'; }
  if (!HEAT.mode || !R || !scene) return;
  var pos = [], col = [], tmp = new THREE.Color();
  function put(c, team, lift) {
    if (!c) return;
    pos.push((c[0] - ctr[0]) * S, c[1] * S + lift, (c[2] - ctr[2]) * S);
    tmp.set(teamColor(team));
    col.push(tmp.r, tmp.g, tmp.b);
  }
  if (HEAT.mode === 1) {
    var lastT = [];
    for (var f = 0; f < R.frames.length; f++) {
      var row = R.frames[f].p || [], t = R.frames[f].t;
      for (var i = 0; i < row.length; i++) {
        var c = row[i];
        if (!c || c[0] == null || !c[4] || (follow >= 0 && i !== follow)) continue;
        if (lastT[i] != null && t - lastT[i] < 0.4) continue;
        lastT[i] = t;
        put(c, R.players[i].team, 1);
      }
    }
  } else {
    (R.events || []).forEach(function (e) {
      if (e.type !== 'kill' || e.victim == null) return;
      var who = HEAT.mode === 2 ? e.victim : e.killer;
      if (who == null || (HEAT.mode === 3 && who === e.victim)) return;
      if (follow >= 0 && who !== follow) return;
      put(insCell(R, who, e.t), R.players[who].team, 9);
    });
  }
  if (!pos.length) return;
  var g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
  HEAT.pts = new THREE.Points(g, new THREE.PointsMaterial({
    size: HEAT.mode === 1 ? 3 : 9, sizeAttenuation: false, vertexColors: true, transparent: true,
    opacity: HEAT.mode === 1 ? 0.35 : 0.9, depthTest: false, blending: THREE.AdditiveBlending }));
  HEAT.pts.renderOrder = 4; HEAT.pts.frustumCulled = false;
  scene.add(HEAT.pts);
}
function setHeat(mode) { HEAT.mode = mode % 4; buildHeat(); }

// The second scoreboard table and the match facts under it. Every number is
// cut off at LIM, like the table above it.
var MULTI_NAME = { 2: 'double', 3: 'triple', 4: 'quad' };
function seekLink(t) { return '<a href="#" data-seek="' + t.toFixed(2) + '" style="color:var(--amber);text-decoration:none">' + fmt(t) + '</a>'; }
function insightRows(LIM) {
  var n = R.players.length, ins = R.ins, i;
  var o = R.players.map(function () {
    return { hsK: 0, hd: 0, zn: 0, best: 0, fb: 0, trade: 0, cqc: 0, koN: 0, koS: 0, aimN: 0, aimS: 0,
             rgS: 0, rgN: 0, rgMax: 0, crouch: 0, prone: 0, alive: 0, rolls: 0, snaps: 0, gas: 0, top: null,
             streak: 0, run: 0, ast: 0, shN: 0, shS: 0, coN: 0, coS: 0, objS: 0, bxN: 0, bxS: 0, nvN: 0, nvS: 0 };
  });
  (R.events || []).forEach(function (e) {
    if (e.type !== 'kill' || e.t > LIM) return;
    (e._assist || []).forEach(function (q) { if (o[q]) o[q].ast++; });
    // kill streak: kills in a row without dying. Any death ends the run.
    if (e.victim != null && o[e.victim]) o[e.victim].run = 0;
    if (e.killer == null || !o[e.killer] || e.killer === e.victim) return;
    var k = o[e.killer];
    if (++k.run > k.streak) k.streak = k.run;
    if (e.hitZone === 'head') k.hsK++;
    if (e._fb) k.fb++;
    if (e._trade) k.trade++;
    if (e._multi > k.best) k.best = e._multi;
    if (e._range != null) { k.rgS += e._range; k.rgN++; if (e._range > k.rgMax) k.rgMax = e._range; }
  });
  (R.dmg || []).forEach(function (h) {
    if (h[0] > LIM || h[1] < 0 || h[1] === h[2] || !o[h[1]] || h.length < 9 || !ZONE_KNOWN[h[8]]) return;
    o[h[1]].zn++;
    if (h[8] === ZONE_HEAD) o[h[1]].hd++;
  });
  if (!ins) return o;
  ins.cqc.forEach(function (e) { if (e.t <= LIM) o[e.killer].cqc++; });
  for (i = 0; i < n; i++) {
    ins.rolls[i].forEach(function (t) { if (t <= LIM) o[i].rolls++; });
    ins.snaps[i].forEach(function (t) { if (t <= LIM) o[i].snaps++; });
    ins.koBy[i].forEach(function (k) { if (k[0] <= LIM) { o[i].koN++; o[i].koS += Math.min(k[1], LIM) - k[0]; } });
    var per = {};
    ins.aimBy[i].forEach(function (a) {
      var pa = R.players[i], pb = R.players[a[3]];
      if (a[0] > LIM || !pb || (pa.team != null && pa.team === pb.team)) return;
      o[i].aimN++; o[i].aimS += a[1];
      per[a[3]] = (per[a[3]] || 0) + a[1];
    });
    Object.keys(per).forEach(function (k) { if (o[i].top == null || per[k] > per[o[i].top]) o[i].top = +k; });
  }
  ((R.x && R.x.gas) || []).forEach(function (g) { if (g[0] <= LIM && o[g[2]]) o[g[2]].gas++; });
  // CARDBOARD BOX AND NIGHT VISION (user, 2026-10-10: "scoreboard additions, cbox uses, cbox time, envg use time").
  // A box spell = one unbroken run in the box states (buildBox); night vision = the game's own "switched on" state (22).
  (R.boxRuns || []).forEach(function (a, pi) {
    if (!a || !o[pi]) return;
    a.forEach(function (b) { if (b[0] <= LIM) { o[pi].bxN++; o[pi].bxS += Math.max(0, Math.min(b[1], LIM) - b[0]); } });
  });
  ((R.x && R.x.gear) || []).forEach(function (g) {
    if (g[3] !== 22 || g[0] > LIM || !o[g[2]]) return;
    o[g[2]].nvN++; o[g[2]].nvS += Math.max(0, Math.min(g[1], LIM) - g[0]);
  });
  // time crouched / prone, out of time alive
  // (lying knocked out is not lying prone, so those stretches count as neither)
  // BASE: the capture points, for time spent standing on one. The footprint is
  // OBJ_STAND_MM either side of the point (the drawn square is 1.75 m; a little
  // slack for the edge), on the same floor.
  var pts = [];
  objMarks.forEach(function (mk) { if (mk.base != null && mk.wx != null) pts.push(mk); });
  var last = [], kp = [], pk = [];
  for (var f = 0; f < R.frames.length; f++) {
    var row = R.frames[f].p || [], t = R.frames[f].t;
    if (t > LIM) break;
    for (i = 0; i < n; i++) {
      var c = row[i];
      if (!c) continue;
      var dt = last[i] == null ? 0 : t - last[i];
      last[i] = t;
      // shield up / coolant spraying: a use is the action starting
      var act = c.length > 14 ? c[14] : null;
      var kind = !c[4] ? 0 : act === ACT_SHIELD ? 1 : ACT_COOLANT[act] ? 2 : 0;
      if (kind !== (pk[i] || 0)) { if (kind === 1) o[i].shN++; else if (kind === 2) o[i].coN++; }
      pk[i] = kind;
      if (dt <= 0 || dt >= 1 || !c[4]) continue;
      o[i].alive += dt;
      if (kind === 1) o[i].shS += dt; else if (kind === 2) o[i].coS += dt;
      for (var b = 0; b < pts.length; b++) {
        if (Math.abs(c[0] - pts[b].wx) <= OBJ_STAND_MM && Math.abs(c[2] - pts[b].wz) <= OBJ_STAND_MM &&
            Math.abs(c[1] - pts[b].wy) <= 2500) { o[i].objS += dt; break; }
      }
      var kr = ins.koBy[i];
      kp[i] = kp[i] || 0;
      while (kp[i] < kr.length && kr[kp[i]][1] < t) kp[i]++;
      if (kp[i] < kr.length && kr[kp[i]][0] <= t) continue;
      if (R.crouch && R.crouch[i] && R.crouch[i][f]) o[i].crouch += dt;
      else if (R.prone && R.prone[i] && R.prone[i][f]) o[i].prone += dt;
    }
  }
  return o;
}

var scoreTab = 'detail';
var SCORE_TABS = [['detail', 'Detail'], ['grid', 'Head to head'], ['weapons', 'Weapons'], ['per10', 'Per 10 min']];
// EVERYONE'S NUMBERS AS A RATE (user, 2026-10-10: "a tab that shows everyone's avg everything across 10 minutes including one
// for team / 10 minutes"). Each count is scaled to ten minutes of match time played so far, so a 6-minute round and a
// 15-minute one can be compared. A team row is the team's total per ten minutes.
function renderPer10(LIM, teams, o, objName) {
  var D = (R.duration || (R.frames.length ? R.frames[R.frames.length - 1].t : LIM)), lim = Math.max(1, Math.min(LIM, D)), k10 = 600 / lim;
  var cols = [
    ['Kills', 'Kills per ten minutes', function (r) { return r.k; }],
    ['Deaths', 'Deaths per ten minutes', function (r) { return r.d; }],
    ['Assists', 'Assists per ten minutes', function (r) { return r.a; }],
    ['HS', 'Head-shot kills per ten minutes', function (r, s) { return s.hsK; }],
    ['Dmg', 'Damage dealt per ten minutes', function (r) { return r.dOut; }, 0],
    ['Taken', 'Damage taken per ten minutes', function (r) { return r.dIn; }, 0],
    ['Shots', 'Shots fired per ten minutes', function (r) { return r.sh; }, 0],
    ['Hits', 'Shots that hit per ten minutes', function (r) { return r.ht; }, 0],
    ['Stuns', 'Stuns per ten minutes', function (r) { return r.st; }],
    [objName || 'Obj', 'Objective plays per ten minutes', function (r) { return r.ob; }],
    ['Melee', 'Close-quarters attacks per ten minutes', function (r, s) { return s.cqc; }],
    ["KO'd", 'Times knocked out per ten minutes', function (r, s) { return s.koN; }],
    ['Rolls', 'Rolls per ten minutes', function (r, s) { return s.rolls; }],
    ['Box', 'Times in the cardboard box per ten minutes', function (r, s) { return s.bxN; }],
    ['Metres', 'Distance covered per ten minutes', function (r) { return r.m; }, 0]
  ];
  var th = function (label, tip) { return '<th title="' + esc(tip) + '">' + label + '</th>'; };
  function cell(v, dp) { v = (v || 0) * k10; return '<td>' + (v ? (dp === 0 ? Math.round(v) : v.toFixed(1)) : '—') + '</td>'; }
  var html = '<div style="margin:8px 8px 0;color:var(--ink-faint);font-size:11px">every number is per ten minutes of match time (' + fmt(lim) + ' played)</div>' +
    '<table style="margin-top:4px"><thead><tr><th></th><th>Player</th>' + cols.map(function (c) { return th(c[0], c[1]); }).join('') + '</tr></thead><tbody>';
  Object.keys(teams).sort().forEach(function (t) {
    var sum = cols.map(function () { return 0; });
    teams[t].forEach(function (r) {
      var s = o[r.i];
      html += '<tr data-team="' + (r.p.team == null ? '' : r.p.team) + '"><td class="bar"></td><td class="nm">' + esc(r.p.name || '(unidentified)') + '</td>' +
        cols.map(function (c, ci) { var v = c[2](r, s) || 0; sum[ci] += v; return cell(v, c[3]); }).join('') + '</tr>';
    });
    html += '<tr data-team="' + (t === 'x' ? '' : t) + '" style="font-weight:700"><td class="bar"></td><td class="nm">TEAM / 10 MIN</td>' +
      cols.map(function (c, ci) { return cell(sum[ci], c[3]); }).join('') + '</tr>';
  });
  return html + '</tbody></table>';
}
function renderInsights(LIM, teams, objName, o) {
  o = o || insightRows(LIM);
  var tabs = '<div style="display:flex;gap:6px;margin:10px 8px 0">' + SCORE_TABS.map(function (tb) {
    return '<button class="tbtn wide" data-tab="' + tb[0] + '" data-on="' + (scoreTab === tb[0] ? 1 : 0) + '">' + tb[1] + '</button>';
  }).join('') + '</div>';
  var body = scoreTab === 'grid' ? renderGrid(LIM, teams)
           : scoreTab === 'weapons' ? renderWeapons(LIM, teams)
           : scoreTab === 'per10' ? renderPer10(LIM, teams, o, objName)
           : renderDetail(LIM, teams, o);
  return tabs + body + renderStars(LIM, teams, o, objName) + scoreGraph(LIM);
}
// SCORE OVER TIME, from the game's own points (every change of every player's
// score is timed in the scoreboard stream). Two team totals climbing side by
// side are two lines on top of each other (user, 2026-10-05: "looks a little
// weird"), so the chart shows the LEAD instead: above the middle line the first
// team is ahead, below it the second, filled in the leader's colour, to the
// playhead only. Round ends are marked and named. Click to seek.
function scoreGraph(LIM) {
  var st = R && R.meta && R.meta.statOps;
  if (!st || !st.length || !R.duration) return '';
  var teams = [], cur = {}, tot = {}, W = 1000, H = 150, MID = H / 2, D = R.duration;
  R.players.forEach(function (p) { if (p.team != null && teams.indexOf(p.team) < 0) teams.push(p.team); });
  teams.sort();
  if (teams.length !== 2) return '';
  var A = teams[0], B = teams[1], lim = Math.min(LIM, D);
  tot[A] = 0; tot[B] = 0;
  var lead = [[0, 0]], maxAbs = 1, peak = null, changes = 0, lastSign = 0, nowTot = [0, 0];
  for (var i = 0; i < st.length; i++) {
    var r = st[i];
    if (r[2] !== STAT.score) continue;
    var pl = R.players[r[1]], key = r[4] + ':' + r[1];
    if (!pl || pl.team == null || tot[pl.team] == null) continue;
    tot[pl.team] += r[3] - (cur[key] || 0);
    cur[key] = r[3];
    var l = tot[A] - tot[B];
    lead.push([r[0], l]);
    if (Math.abs(l) > maxAbs) maxAbs = Math.abs(l);
    if (r[0] <= lim) {
      nowTot = [tot[A], tot[B]];
      if (!peak || Math.abs(l) > Math.abs(peak[1])) peak = [r[0], l];
      var sg = l > 0 ? 1 : l < 0 ? -1 : 0;
      if (sg && lastSign && sg !== lastSign) changes++;
      if (sg) lastSign = sg;
    }
  }
  lead.push([D, lead[lead.length - 1][1]]);
  maxAbs = Math.max(5, Math.ceil(maxAbs / 5) * 5);
  var X = function (tt) { return (tt / D * W).toFixed(1); };
  var Y = function (v) { return (MID - v / maxAbs * (MID - 8)).toFixed(1); };
  function stepPath(upTo, close) {
    var d = 'M0 ' + Y(0), last = 0;
    for (var k = 0; k < lead.length; k++) {
      if (lead[k][0] > upTo) break;
      d += ' H' + X(lead[k][0]) + ' V' + Y(lead[k][1]); last = lead[k][1];
    }
    d += ' H' + X(upTo);
    return close ? d + ' V' + Y(0) + ' Z' : d;
  }
  var uid = 'sg' + Math.floor(lim * 10);
  var svg = '<defs><clipPath id="' + uid + 'a"><rect x="0" y="0" width="' + W + '" height="' + MID + '"/></clipPath>' +
            '<clipPath id="' + uid + 'b"><rect x="0" y="' + MID + '" width="' + W + '" height="' + MID + '"/></clipPath></defs>';
  // minute grid
  for (var m = 60; m < D; m += 60)
    svg += '<line x1="' + X(m) + '" x2="' + X(m) + '" y1="0" y2="' + H + '" stroke="#fff" opacity=".05" vector-effect="non-scaling-stroke"/>';
  [0.5, 1].forEach(function (fr) { [1, -1].forEach(function (sn) {
    svg += '<line x1="0" x2="' + W + '" y1="' + Y(sn * fr * maxAbs) + '" y2="' + Y(sn * fr * maxAbs) + '" stroke="#fff" opacity=".06" vector-effect="non-scaling-stroke"/>'; }); });
  // the whole match faint, the part already played solid
  svg += '<path d="' + stepPath(D, false) + '" fill="none" stroke="#fff" opacity=".18" stroke-width="1" vector-effect="non-scaling-stroke"/>';
  svg += '<path d="' + stepPath(lim, true) + '" fill="' + teamColor(A) + '" opacity=".55" clip-path="url(#' + uid + 'a)"/>';
  svg += '<path d="' + stepPath(lim, true) + '" fill="' + teamColor(B) + '" opacity=".55" clip-path="url(#' + uid + 'b)"/>';
  svg += '<path d="' + stepPath(lim, false) + '" fill="none" stroke="#fff" opacity=".85" stroke-width="1.5" vector-effect="non-scaling-stroke"/>';
  svg += '<line x1="0" x2="' + W + '" y1="' + MID + '" y2="' + MID + '" stroke="#fff" opacity=".35" vector-effect="non-scaling-stroke"/>';
  var tags = '', rn = 0;
  (R.meta.roundResults || []).forEach(function (rr) {
    rn++;
    // a round result names the second side as team 2 whatever the roster calls it
    var wt = rr.winner == null ? null : teams.indexOf(rr.winner) >= 0 ? rr.winner : rr.winner === 2 ? B : rr.winner;
    var col = wt != null ? teamColor(wt) : '#888';
    svg += '<line x1="' + X(rr.t) + '" x2="' + X(rr.t) + '" y1="0" y2="' + H + '" stroke="' + col + '" stroke-width="1" stroke-dasharray="3 3" opacity=".8" vector-effect="non-scaling-stroke"/>';
    tags += '<span style="position:absolute;top:2px;right:' + (100 - rr.t / D * 100).toFixed(2) + '%;padding-right:4px;font-size:9px;color:' + col + ';white-space:nowrap">R' + rn +
            (wt != null ? ' ' + esc(teamLabel(wt)) : ' draw') + '</span>';
  });
  svg += '<line x1="' + X(lim) + '" x2="' + X(lim) + '" y1="0" y2="' + H + '" stroke="#E8964B" stroke-width="1.5" vector-effect="non-scaling-stroke"/>';
  var axis = '';
  for (var m2 = 60; m2 < D; m2 += (D > 900 ? 120 : 60))
    axis += '<span style="position:absolute;bottom:1px;left:' + (m2 / D * 100).toFixed(2) + '%;transform:translateX(-50%);font-size:9px;opacity:.45">' + (m2 / 60) + ':00</span>';
  var side = '<span style="position:absolute;left:4px;top:14px;font-size:9px;color:' + teamColor(A) + '">' + esc(teamLabel(A)) + ' +' + maxAbs + '</span>' +
             '<span style="position:absolute;left:4px;bottom:12px;font-size:9px;color:' + teamColor(B) + '">' + esc(teamLabel(B)) + ' +' + maxAbs + '</span>';
  var nowLead = nowTot[0] - nowTot[1];
  var head = '<b style="color:' + teamColor(A) + '">' + esc(teamLabel(A)) + ' ' + nowTot[0] + '</b> &nbsp; ' +
             '<b style="color:' + teamColor(B) + '">' + esc(teamLabel(B)) + ' ' + nowTot[1] + '</b> &nbsp; ' +
             (nowLead ? '<b style="color:' + teamColor(nowLead > 0 ? A : B) + '">' + esc(teamLabel(nowLead > 0 ? A : B)) + ' lead by ' + Math.abs(nowLead) + '</b>' : '<b>level</b>') +
             (peak && peak[1] ? ' &nbsp;<span style="opacity:.7">biggest lead <span style="color:' + teamColor(peak[1] > 0 ? A : B) + '">' + esc(teamLabel(peak[1] > 0 ? A : B)) +
               ' +' + Math.abs(peak[1]) + '</span> at ' + fmt(peak[0]) + ' &nbsp; lead changed hands ' + changes + (changes === 1 ? ' time' : ' times') + '</span>' : '');
  return '<div style="margin-top:10px"><div style="font-family:var(--f-disp);font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:var(--ink-dim);margin-bottom:4px">' +
    'Who is ahead &nbsp;' + head + '</div>' +
    '<div style="position:relative;font-family:var(--f-disp);letter-spacing:.06em;text-transform:uppercase">' +
    '<svg viewBox="0 0 ' + W + ' ' + H + '" preserveAspectRatio="none" style="display:block;width:100%;height:' + H + 'px;background:rgba(255,255,255,.03);border:1px solid var(--hair);cursor:pointer" ' +
    'onclick="(function(e,s){var b=s.getBoundingClientRect();window.__mgo2Seek&&window.__mgo2Seek((e.clientX-b.left)/b.width)})(event,this)">' +
    svg + '</svg><div style="position:absolute;inset:0;pointer-events:none">' + tags + axis + side + '</div></div></div>';
}

// WHO KILLED WHOM: rows are killers, columns are victims. The brightest cell in
// a row is that player's favourite victim; the diagonal is self-inflicted.
function killGrid(LIM) {
  var n = R.players.length, g = [], i;
  for (i = 0; i < n; i++) g.push(new Array(n).fill(0));
  (R.events || []).forEach(function (e) {
    if (e.type !== 'kill' || e.t > LIM || e.victim == null || !g[e.victim]) return;
    var k = (e.killer == null || e.suicide) ? e.victim : e.killer;
    if (e.killer == null && !e.suicide) return;          // nobody credited
    if (g[k]) g[k][e.victim]++;
  });
  return g;
}
// With two teams, half of a full grid is always empty (nobody's row meets a
// team-mate's column), so it folds into ONE head-to-head block instead: one
// team down the side, the other across the top, each cell "his kills on them :
// their kills on him". Anything else (no teams, three teams) keeps the grid.
function renderHeadToHead(LIM, teams, keys) {
  var g = killGrid(LIM), A = teams[keys[0]], B = teams[keys[1]];
  var ca = teamColor(A[0].p.team), cb = teamColor(B[0].p.team);
  function pair(x, y) {
    if (!x && !y) return '<span style="color:var(--ink-faint);opacity:.45">·</span>';
    return '<span style="color:' + ca + (x > y ? ';font-weight:600' : x < y ? ';opacity:.6' : '') + '">' + x + '</span>' +
           '<span style="color:var(--ink-faint)"> : </span>' +
           '<span style="color:' + cb + (y > x ? ';font-weight:600' : y < x ? ';opacity:.6' : '') + '">' + y + '</span>';
  }
  var html = '<table style="margin-top:8px"><thead><tr><th></th><th></th>' + B.map(function (c) {
    return '<th title="' + esc(c.p.name || '?') + '" style="text-align:center;color:' + cb + '">' +
           esc(String(c.p.name || '?').slice(0, 9)) + '</th>';
  }).join('') + '<th style="text-align:center">Total</th></tr></thead><tbody>';
  var colK = B.map(function () { return [0, 0]; }), odd = 0;
  A.forEach(function (r) {
    var rk = 0, rd = 0;
    html += '<tr data-team="' + r.p.team + '"><td class="bar"></td><td class="nm">' + esc(r.p.name || '(unidentified)') + '</td>' +
      B.map(function (c, j) {
        var x = g[r.i][c.i], y = g[c.i][r.i];
        rk += x; rd += y; colK[j][0] += x; colK[j][1] += y;
        return '<td style="text-align:center">' + pair(x, y) + '</td>';
      }).join('') + '<td style="text-align:center">' + pair(rk, rd) + '</td></tr>';
  });
  html += '<tr class="hdr"><td colspan="2">Total</td>' + colK.map(function (k) {
    return '<td style="text-align:center">' + pair(k[0], k[1]) + '</td>'; }).join('') + '<td></td></tr>';
  // team kills and self-inflicted deaths have no cell here: say them in words
  A.concat(B).forEach(function (r) {
    A.concat(B).forEach(function (c) { if (g[r.i][c.i] && r.p.team === c.p.team) odd += g[r.i][c.i]; });
  });
  return html + '</tbody></table><div style="width:0;min-width:100%;box-sizing:border-box;padding:4px 8px 0;font-size:11px;color:var(--ink-faint)">' +
    'Each cell: <span style="color:' + ca + '">kills by the player on the left</span> : ' +
    '<span style="color:' + cb + '">kills by the player above</span>.' +
    (odd ? ' ' + odd + ' team kill' + (odd > 1 ? 's' : '') + ' or self-inflicted death' + (odd > 1 ? 's' : '') + ' not shown.' : '') + '</div>';
}
function renderGrid(LIM, teams) {
  var tk = Object.keys(teams).sort();
  if (tk.length === 2 && tk.indexOf('x') < 0) return renderHeadToHead(LIM, teams, tk);
  var g = killGrid(LIM), order = [];
  Object.keys(teams).sort().forEach(function (t) { teams[t].forEach(function (r) { order.push(r); }); });
  var html = '<table style="margin-top:8px"><thead><tr><th></th><th>Killer \\ victim</th>' +
    order.map(function (r) {
      return '<th title="' + esc(r.p.name || '?') + '" style="text-align:center;color:' + teamColor(r.p.team) + '">' +
             esc(String(r.p.name || '?').slice(0, 6)) + '</th>';
    }).join('') + '<th style="text-align:center">Kills</th></tr></thead><tbody>';
  order.forEach(function (r) {
    var row = g[r.i], top = 0, tot = 0;
    order.forEach(function (c) { if (c.i !== r.i) { tot += row[c.i]; if (row[c.i] > top) top = row[c.i]; } });
    html += '<tr data-team="' + (r.p.team == null ? '' : r.p.team) + '"><td class="bar"></td>' +
      '<td class="nm">' + esc(r.p.name || '(unidentified)') + '</td>' +
      order.map(function (c) {
        var v = row[c.i], self = c.i === r.i;
        var mate = !self && r.p.team != null && r.p.team === c.p.team;
        var st = 'text-align:center;' + (self ? 'background:rgba(255,255,255,.05);' : '') +
                 (!v ? 'color:var(--ink-faint);opacity:.45'
                     : self || mate ? 'color:var(--ink-faint)'
                     : v === top ? 'color:var(--amber);font-weight:600' : 'color:var(--ink)');
        return '<td style="' + st + '"' + (self && v ? ' title="self-inflicted"' : mate && v ? ' title="team kill"' : '') + '>' + (v || '·') + '</td>';
      }).join('') + '<td style="text-align:center;color:var(--ink-dim)">' + tot + '</td></tr>';
  });
  return html + '</tbody></table>';
}

// WEAPONS: what each player actually used. Time in hand and shots come from the
// position stream (a shot is the firing action pulsing with that gun out), hits
// and damage from the per-hit records, kills from the kill feed.
function weaponRows(LIM) {
  var n = R.players.length, W = [], i;
  for (i = 0; i < n; i++) W.push({});
  function w(pi, item) { return W[pi][item] || (W[pi][item] = { held: 0, shots: 0, hits: 0, dmg: 0, kills: 0, hd: 0 }); }
  var last = [], prev = null;
  for (var f = 0; f < R.frames.length; f++) {
    var row = R.frames[f].p || [], t = R.frames[f].t;
    if (t > LIM) break;
    for (i = 0; i < n; i++) {
      var c = row[i];
      if (!c) continue;
      var dt = last[i] == null ? 0 : t - last[i];
      last[i] = t;
      if (c[4] && c.length > 12 && c[12] != null) {
        if (dt > 0 && dt < 1) w(i, c[12]).held += dt;
        if (prev && isShot(c, prev[i])) w(i, c[12]).shots++;
      }
    }
    prev = row;
  }
  // hits and damage per weapon use the SAME crediting as the accuracy column (hitCredits): the measured rule for gun hits,
  // the file's own attacker for everything else. Before 2026-10-08 this page used the file's attacker alone, which is
  // right 75 % of the time on gun hits and blank on one in eight.
  var hcW = hitCredits();
  (R.dmg || []).forEach(function (h, hi) {
    var hk = isGun(h[4]) ? hcW.k[hi] : h[1];
    if (h[0] > LIM || hk < 0 || hk === h[2] || !W[hk] || h[4] == null) return;
    var e = w(hk, h[4]); e.hits++; e.dmg += h[3] / 2.5;
  });
  hcW.extra.forEach(function (x) { if (x[0] <= LIM && W[x[1]] && x[3] != null && isGun(x[3])) w(x[1], x[3]).hits++; });
  (R.events || []).forEach(function (e) {
    if (e.t > LIM || e.killer == null || e.killer === e.victim || !W[e.killer] || e.weapon == null) return;
    if (e.type === 'stun') w(e.killer, e.weapon).hits++;
    if (e.type === 'kill') { var k = w(e.killer, e.weapon); k.kills++; if (e.hitZone === 'head') k.hd++; }
  });
  return W;
}
function renderWeapons(LIM, teams) {
  var W = weaponRows(LIM);
  var th = function (label, tip) { return '<th title="' + esc(tip) + '">' + label + '</th>'; };
  var html = '<table style="margin-top:8px"><thead><tr><th></th><th>Player</th><th>Weapon</th>' +
    th('In hand', 'Time alive with this out') + th('Shots', 'Shots fired. Guns only; may run up to 10% high') +
    th('Hits', 'Hits the file credits to this player with this weapon') + th('Acc', 'Hits out of shots') +
    th('Dmg', 'Damage dealt. 100 = one full health bar') + th('Kills', 'Kills, with head-shot kills in brackets') +
    '</tr></thead><tbody>';
  function cells(e) {
    return '<td>' + esc(e.name) + '</td><td>' + (e.held >= 1 ? fmt(e.held) : '—') + '</td>' +
      '<td>' + (e.shots || '—') + '</td><td>' + (e.hits || '—') + '</td>' +
      '<td>' + (e.shots >= 10 ? Math.round(100 * Math.min(1, e.hits / e.shots)) + '%' : '—') + '</td>' +
      '<td>' + (Math.round(e.dmg) || '—') + '</td>' +
      '<td>' + (e.kills ? e.kills + (e.hd ? ' <span style="color:var(--ink-faint)">(' + e.hd + ')</span>' : '') : '—') + '</td>';
  }
  function pick(map, n) {
    return Object.keys(map).map(function (id) { var e = map[id]; e.id = +id; e.name = itemName(+id); return e; })
      .filter(function (e) { return e.name && (e.kills || e.hits || e.shots >= 3 || e.held >= 20); })
      .sort(function (x, y) { return (y.kills - x.kills) || (y.dmg - x.dmg) || (y.held - x.held); }).slice(0, n);
  }
  // ONE BLOCK PER TEAM, in the team's colour: what the side used as a whole
  // first, then each of its players.
  Object.keys(teams).sort().forEach(function (t) {
    var col = t === 'x' ? 'var(--ink-dim)' : teamColor(+t), sum = {};
    teams[t].forEach(function (r) {
      Object.keys(W[r.i]).forEach(function (id) {
        var e = W[r.i][id], q = sum[id] || (sum[id] = { held: 0, shots: 0, hits: 0, dmg: 0, kills: 0, hd: 0 });
        q.held += e.held; q.shots += e.shots; q.hits += e.hits; q.dmg += e.dmg; q.kills += e.kills; q.hd += e.hd;
      });
    });
    html += '<tr class="hdr"><td colspan="9" style="color:' + col + ';border-left:3px solid ' + col + '">' +
            (t === 'x' ? 'Unassigned' : esc(teamLabel(+t))) + '</td></tr>';
    pick(sum, 5).forEach(function (e, k) {
      html += '<tr style="background:rgba(255,255,255,.03)"><td class="bar" style="border-left:3px solid ' + col + '"></td>' +
        '<td style="color:' + col + ';font-family:var(--f-disp);font-size:10.5px;letter-spacing:.1em;text-transform:uppercase">' +
        (k ? '' : 'Team total') + '</td>' + cells(e) + '</tr>';
    });
    teams[t].forEach(function (r) {
      pick(W[r.i], 4).forEach(function (e, k) {
        // a rule above each player's first weapon, so one player's rows read as a block
        html += '<tr' + (k ? '' : ' style="border-top:2px solid rgba(201,208,194,.30)"') + '>' +
          '<td class="bar" style="border-left:3px solid ' + col + '"></td>' +
          '<td class="nm" style="color:' + col + '">' + (k ? '' : esc(r.p.name || '(unidentified)')) + '</td>' + cells(e) + '</tr>';
      });
    });
  });
  return html + '</tbody></table>';
}

function renderDetail(LIM, teams, o) {
  var hasX = !!R.x, n = R.players.length;
  var anySh = o.some(function (s) { return s.shN > 0; }), anyCo = o.some(function (s) { return s.coN > 0; });
  var anyObj = o.some(function (s) { return s.objS >= 1; });
  var anyBx = o.some(function (s) { return s.bxN > 0; }), anyNv = o.some(function (s) { return s.nvS >= 0.5; });
  function dash(v) { return v ? v : '—'; }
  function pct(a, b) { return b > 0 ? Math.round(100 * a / b) + '%' : '—'; }
  var th = function (label, tip) { return '<th title="' + esc(tip) + '">' + label + '</th>'; };
  var html = '<table style="margin-top:8px"><thead><tr><th></th><th>Player</th>' +
    th('HS', 'Kills by a head shot') +
    th('Head %', 'Share of this player\'s bullets that landed on the head, out of hits whose zone the file records') +
    th('Streak', 'Longest kill streak: most kills in a row without dying') +
    th('Multi', 'Longest run of kills with no more than ' + MULTI_S + ' s between them') +
    th('Trade', 'Kills that avenged a team-mate inside ' + TRADE_S + ' s') +
    th('Melee', 'Close-quarters attacks (CQC, punches or knife) that had an enemy as the target or inside arm\'s reach') +
    (hasX ? th('KO\'d', 'Times knocked out (stunned, put to sleep or held) and seconds spent that way') +
            th('On target', 'Times this player\'s weapon locked on an enemy, and the seconds it stayed there') : '') +
    th('Range', 'Average distance of this player\'s kills (longest in brackets)') +
    th('Crouch', 'Share of time alive spent crouched') +
    th('Prone', 'Share of time alive spent lying down') +
    th('Rolls', 'Rolls') +
    (anyObj ? th('On point', 'Time alive spent standing on a capture point') : '') +
    (anySh ? th('Shield', 'Times the shield was raised, and the seconds it was up') : '') +
    (anyCo ? th('Coolant', 'Coolant sprays, and the seconds spent spraying') : '') +
    (anyBx ? th('Box', 'Times in the cardboard box, and the time spent in it') : '') +
    (anyNv ? th('ENVG', 'Time with the night vision goggles switched on') : '') +
    (hasX ? th('Swap', 'Share of the match with the aim on the other shoulder. Which shoulder is the default is assumed, not read from the file') : '') +
    '</tr></thead><tbody>';
  Object.keys(teams).sort().forEach(function (t) {
    teams[t].forEach(function (r) {
      var s = o[r.i], sd = hasX && R.x.side[r.i];
      html += '<tr data-team="' + (r.p.team == null ? '' : r.p.team) + '"><td class="bar"></td>' +
        '<td class="nm">' + esc(r.p.name || '(unidentified)') + '</td>' +
        '<td>' + dash(s.hsK) + '</td><td>' + (s.zn >= 5 ? pct(s.hd, s.zn) : '—') + '</td>' +
        '<td>' + (s.streak >= 2 ? s.streak : '—') + '</td>' +
        '<td>' + (s.best >= 2 ? '×' + s.best : '—') + '</td>' +
        '<td>' + dash(s.trade) + '</td><td>' + dash(s.cqc) + '</td>' +
        (hasX ? '<td>' + (s.koN ? s.koN + ' <span style="color:var(--ink-faint)">' + Math.round(s.koS) + 's</span>' : '—') + '</td>' +
                '<td title="' + (s.top != null ? 'most: ' + esc(R.players[s.top].name || '?') : '') + '">' +
                (s.aimN ? s.aimN + ' <span style="color:var(--ink-faint)">' + s.aimS.toFixed(1) + 's</span>' : '—') + '</td>' : '') +
        '<td>' + (s.rgN ? Math.round(s.rgS / s.rgN) + ' m <span style="color:var(--ink-faint)">(' + Math.round(s.rgMax) + ')</span>' : '—') + '</td>' +
        '<td>' + (s.alive > 5 && s.crouch > 0.5 ? pct(s.crouch, s.alive) : '—') + '</td>' +
        '<td>' + (s.alive > 5 && s.prone > 0.5 ? pct(s.prone, s.alive) : '—') + '</td>' +
        '<td>' + dash(s.rolls) + '</td>' +
        (anyObj ? '<td>' + (s.objS >= 1 ? fmt(s.objS) : '—') + '</td>' : '') +
        (anySh ? '<td>' + (s.shN ? s.shN + ' <span style="color:var(--ink-faint)">' + Math.round(s.shS) + 's</span>' : '—') + '</td>' : '') +
        (anyCo ? '<td>' + (s.coN ? s.coN + ' <span style="color:var(--ink-faint)">' + Math.round(s.coS) + 's</span>' : '—') + '</td>' : '') +
        (anyBx ? '<td>' + (s.bxN ? s.bxN + ' <span style="color:var(--ink-faint)">' + fmt(s.bxS) + '</span>' : '—') + '</td>' : '') +
        (anyNv ? '<td>' + (s.nvS >= 0.5 ? fmt(s.nvS) : '—') + '</td>' : '') +
        (hasX ? '<td>' + (sd && sd[1] > 100 ? pct(sd[0], sd[1]) : '—') + '</td>' : '') + '</tr>';
    });
  });
  return html + '</tbody></table>';
}

// ---- THREE STARS -----------------------------------------------------------
// Gold, silver and bronze for the match, ranked by a contribution score that
// is OURS, not the game's: kills and damage, the objective, stuns, and the
// kills that mattered (first blood, trades), less deaths. Each star shows the
// figures he led the lobby in.
var STAR_COL = ['#E8C547', '#C4CCD4', '#C98A4B'], STAR_NAME = ['Gold', 'Silver', 'Bronze'];
// Each player's points at time `lim`, from the game's own scoreboard stream:
// the last value of counter 8 in each round so far, added up. null when the
// replay does not carry the stream.
// Counters named so far, each by what coincides with its steps
// (replay/itembox/stat_when.js): 5 kills (94% of 3,243 steps are his kill),
// 6 deaths (93%), 8 score, 11 headshot kills (100% his kill, 89% a headshot by
// hit zone), 48 assists (95% of 351: a team-mate killed an enemy that instant),
// 9 stuns dealt, 10 times stunned, 20 rounds won (whole team at once, +5),
// 60 goals / plants by him.
var STAT = { kills: 5, deaths: 6, score: 8, headshots: 11, assists: 48, stuns: 9, stunned: 10, roundsWon: 20, goals: 60 };
function gameStat(bit, lim) {
  var st = R && R.meta && R.meta.statOps;
  if (!st || !st.length) return null;
  var last = {}, out = new Array(R.players.length).fill(0), has = false;
  for (var i = 0; i < st.length; i++) {
    var r = st[i];
    if (r[2] !== bit) continue;
    has = true;
    if (r[0] <= lim) last[r[4] + ':' + r[1]] = r;
  }
  if (!has) return null;
  Object.keys(last).forEach(function (k) { out[last[k][1]] += last[k][3]; });
  return out;
}
function gamePoints(lim) { return gameStat(STAT.score, lim); }
// CAPTURE: SECONDS BANKED per player -- who actually ran his team's capture
// clock down. Each tick of a team's clock (meta.capClock) is credited to the
// players of that team who have a target put in the goal at that instant
// (objective feed kind 2, until that player's next kind 0 / 1). All 63 ticks of
// the test match fall while such a player exists (replay/itembox/cap_clock.py).
// Two holders at once each get the tick.
function capBanked(lim) {
  var cc = R && R.meta && R.meta.capClock, feed = R && R.meta && R.meta.objFeed;
  if (!cc || !cc.length || !feed || R.meta.modeName !== 'CAP') return null;
  var out = new Array(R.players.length).fill(0), prev = {}, fi = 0, inGoal = {};
  for (var i = 0; i < cc.length; i++) {
    var c = cc[i];
    if (c.t > lim) break;
    while (fi < feed.length && feed[fi].t <= c.t + 0.05) {
      var f = feed[fi++];
      if (f.player == null) continue;
      if (f.kind === 2) inGoal[f.player] = true; else if (f.kind === 0 || f.kind === 1) delete inGoal[f.player];
    }
    var key = c.session + ':' + c.team, was = prev[key];
    prev[key] = c.left;
    if (was == null || c.left !== was - 1) continue;
    Object.keys(inGoal).forEach(function (pi) { if (R.players[+pi] && R.players[+pi].team === c.team) out[+pi]++; });
  }
  return out;
}
// LEVEL CHANGE after each round (rules op 0x13, one signed number per player).
// WHOSE number is whose was unknown until 2026-10-05: the array is in ASCENDING
// LOBBY-SLOT order. Measured over 32 round ends: in that order the numbers
// track each player's round score at a median correlation of 0.92; every other
// order tried (descending, by score, by team, parser order) gives about 0. It
// is not the score itself (a 31-point round reads +14 next to a 27-point round
// reading -9), which fits a rating that weighs who you were up against. A round
// whose count of numbers differs from the count of players is left out.
// THIS IS EXP, NOT GP (user, 2026-10-08: "GP and EXP are different stats"). Checked the same day against SaveMGO's
// profiles for 13 players: the figure sits within a few points of their XP (Arae 2945 = 2945, Vraxis 2704 / 2702)
// and 100 - 190 away from their GP. No other counter in the file is rating-sized, so GP itself is not recorded.
// Each player's LEVEL POINTS as the game holds them (scoreboard counter 158,
// written at every round end: values around 2100-2550). CONFIRMED against the
// end-of-round numbers above: Yonkos round 2 reads Instigator 2499 -> 2507 next
// to his +8, Rhaneys 2537 -> 2546 next to +9, OPTIMUS PRIME I 2521 -> 2511 next
// to -10. Latest value at or before `lim`, or null per player.
function levelPoints(lim) {
  var st = R && R.meta && R.meta.statOps;
  if (!st) return null;
  var out = new Array(R.players.length).fill(null), any = false;
  for (var i = 0; i < st.length; i++) {
    if (st[i][2] === 158 && st[i][0] <= lim) { out[st[i][1]] = st[i][3]; any = true; }
  }
  return any ? out : null;
}
// THE LEVEL NUMBER AND THE ROUND MARKS (user, 2026-10-10: "the + - between rounds would be sweet. The level number should show,
// not the exp number"). Level = how many of the game's 22 thresholds the player's level points have reached (the list is in
// the lobby script and in the program: 125 ... 4075 -> levels 0 - 22). The marks are the game's own round-result column:
// +10 or more = "++", +1 to +9 = "+", -1 to -9 = "-", -10 or less = "--" (doos's reading of the program, 2026-10-10).
var LEVEL_AT = [125, 250, 375, 500, 650, 800, 950, 1100, 1250, 1400, 1550, 1700, 1850, 2000, 2175, 2350, 2525, 2725, 2975, 3275, 3625, 4075];
function levelOf(pts) { if (pts == null) return null; var n = 0; while (n < LEVEL_AT.length && pts >= LEVEL_AT[n]) n++; return n; }
function expMark(d) { return d == null ? '' : d >= 10 ? '++' : d >= 1 ? '+' : d <= -10 ? '−−' : d <= -1 ? '−' : ''; }
function expMarksHtml(a) {
  return (a || []).map(function (d) {
    var m = expMark(d);
    return '<span title="' + (d == null ? 'not recorded' : (d > 0 ? '+' : '') + d + ' level points') + '" style="display:inline-block;min-width:15px;color:' +
           (d > 0 ? '#8FD18A' : d < 0 ? '#E2574C' : 'var(--ink-faint)') + '">' + (m || '·') + '</span>';
  }).join('');
}
// each finished round's change per player: [[d0, d1, ...] per round], null where the file does not say (same sources as levelChange)
function levelRounds(lim) {
  var rows = R && R.meta && R.meta.ratingDeltaUnverified;
  if (!rows || !rows.length) return null;
  var pl = [];
  R.players.forEach(function (p, i) { if (p.slot != null) pl.push({ i: i, slot: p.slot }); });
  pl.sort(function (a, b) { return a.slot - b.slot; });
  var lp = {}, out = [];
  ((R.meta && R.meta.statOps) || []).forEach(function (r) { if (r[2] === 158) (lp[r[1]] || (lp[r[1]] = [])).push(r); });
  rows.forEach(function (row) {
    if (row.t > lim || !row.values) return;
    var d = new Array(R.players.length).fill(null);
    if (row.values.length === pl.length) { for (var q = 0; q < pl.length; q++) d[pl[q].i] = row.values[q]; }
    else Object.keys(lp).forEach(function (pi) {
      var a = lp[pi], cur = null, prev = null;
      for (var k = 0; k < a.length; k++) { if (Math.abs(a[k][0] - row.t) < 3) { cur = a[k]; prev = k > 0 ? a[k - 1] : null; break; } }
      if (cur && prev && prev[4] === cur[4]) d[+pi] = cur[3] - prev[3];
    });
    out.push(d);
  });
  return out.length ? out : null;
}
function levelChange(lim) {
  var rows = R && R.meta && R.meta.ratingDeltaUnverified;
  if (!rows || !rows.length) return null;
  var pl = [];
  R.players.forEach(function (p, i) { if (p.slot != null) pl.push({ i: i, slot: p.slot }); });
  pl.sort(function (a, b) { return a.slot - b.slot; });
  var out = new Array(R.players.length).fill(0), used = 0;
  // A row one number short (someone joined late or left) cannot be laid on
  // the slots. For those rounds each player's change is read off his LEVEL
  // POINTS instead: this round end's figure minus the previous round end's.
  // Only possible from the second round on, and only for a player who has both.
  var lp = {};
  ((R.meta && R.meta.statOps) || []).forEach(function (r) { if (r[2] === 158) (lp[r[1]] || (lp[r[1]] = [])).push(r); });
  rows.forEach(function (row) {
    if (row.t > lim || !row.values) return;
    if (row.values.length === pl.length) {
      used++;
      for (var q = 0; q < pl.length; q++) out[pl[q].i] += row.values[q];
      return;
    }
    Object.keys(lp).forEach(function (pi) {
      var a = lp[pi], cur = null, prev = null;
      for (var k = 0; k < a.length; k++) {
        if (Math.abs(a[k][0] - row.t) < 3) { cur = a[k]; prev = k > 0 ? a[k - 1] : null; break; }
      }
      if (cur && prev && prev[4] === cur[4]) { out[+pi] += cur[3] - prev[3]; used++; }
    });
  });
  return used ? out : null;
}
function starScore(r, s, objWeight) {
  return 100 * (r.k || 0) - 40 * (r.d || 0) + 0.3 * (r.dOut || 0) + objWeight * (r.ob || 0) +
         300 * (r.df || 0) + 60 * (r.st || 0) + 40 * s.hsK + 30 * s.trade + 50 * s.fb + 40 * s.cqc +
         50 * s.ast + 1.5 * s.objS;
}
function renderStars(LIM, teams, o, objName) {
  var all = [];
  Object.keys(teams).forEach(function (t) { teams[t].forEach(function (r) { all.push(r); }); });
  // a capture is shared by everyone standing on the point; a put or a plant is one player's
  var objW = objName === 'Cap' ? 120 : 300;
  all.forEach(function (r) { r.score = starScore(r, o[r.i], objW); });
  var ranked = all.filter(function (r) { return (r.k || 0) + (r.ob || 0) + (r.st || 0) + (r.df || 0) > 0; })
                  .sort(function (a, b) { return b.score - a.score; }).slice(0, 3);
  if (!ranked.length) return '';
  // what each figure's lobby best is, so a star can be credited with leading it
  function best(fn) { var m = 0; all.forEach(function (r) { var v = fn(r) || 0; if (v > m) m = v; }); return m; }
  var kd = function (r) { return (r.k || 0) / Math.max(1, r.d || 0); };
  var acc = function (r) { return r.sh >= 20 ? Math.min(1, r.ht / r.sh) : 0; };
  var lead = [
    [function (r) { return r.k; }, function (v) { return 'Most kills'; }],
    [function (r) { return r.ob; }, function (v) { return 'Most ' + ({ Cap: 'captures', Plant: 'plants', Put: 'targets put', Goal: 'goals' }[objName] || 'objectives') + ': ' + v; }],
    [function (r) { return r.df; }, function (v) { return 'Most defuses: ' + v; }],
    [function (r) { return Math.round(r.dOut); }, function (v) { return 'Most damage: ' + v; }],
    [function (r) { return o[r.i].hsK; }, function (v) { return 'Most headshot kills: ' + v; }],
    [function (r) { return o[r.i].streak; }, function (v) { return 'Longest streak: ' + v; }],
    [function (r) { return Math.round(kd(r) * 100) / 100; }, function (v) { return 'Best K/D: ' + v.toFixed(2); }],
    [function (r) { return Math.round(acc(r) * 100); }, function (v) { return 'Best accuracy: ' + v + '%'; }],
    [function (r) { return o[r.i].ast; }, function (v) { return 'Most assists: ' + v; }],
    [function (r) { return Math.round(o[r.i].objS); }, function (v) { return 'Most time on points: ' + fmt(v); }],
    [function (r) { return r.st; }, function (v) { return 'Most stuns: ' + v; }],
    [function (r) { return o[r.i].trade; }, function (v) { return 'Most trades: ' + v; }],
    [function (r) { return Math.round(o[r.i].rgMax); }, function (v) { return 'Longest kill: ' + v + ' m'; }],
    [function (r) { return o[r.i].best; }, function (v) { return 'Biggest multi-kill: ×' + v; }],
    [function (r) { return o[r.i].fb; }, function (v) { return v > 1 ? 'First blood ×' + v : 'First blood'; }]
  ].map(function (l) { return { get: l[0], label: l[1], top: best(l[0]) }; });
  var cards = ranked.map(function (r, rank) {
    var s = o[r.i], chips = [];
    lead.forEach(function (l) {
      var v = l.get(r) || 0;
      if (chips.length < 3 && v > 0 && v >= l.top) chips.push(l.label(v));
    });
    // a star who led nothing still gets his own best figures
    var fill = [];
    if (r.ob) fill.push(r.ob + ' ' + ({ Cap: 'captures', Plant: 'plants', Put: 'targets put', Goal: 'goals' }[objName] || 'objectives'));
    if (r.dOut) fill.push(Math.round(r.dOut) + ' damage');
    if (s.hsK) fill.push(s.hsK + ' headshot kills');
    if (s.streak >= 2) fill.push('streak of ' + s.streak);
    if (r.st) fill.push(r.st + ' stuns');
    for (var q = 0; q < fill.length && chips.length < 3; q++) {
      var word = fill[q].split(' ').slice(1).join(' ');
      if (!chips.some(function (c) { return c.toLowerCase().indexOf(word.split(' ')[0]) >= 0; })) chips.push(fill[q]);
    }
    var col = STAR_COL[rank], face = null;
    try { face = playerPortrait(r.i, rank, starWeapon(r.i, LIM)); } catch (e) { if (!window.__portErr) { window.__portErr = e; console.warn('portraits off:', e); } }
    var size = rank ? 92 : 108;
    var emb = (r.p.clanId != null && EMB_INDEX && EMB_INDEX[r.p.clanId]) ? av('emblems/' + r.p.clanId + '.png') : null;
    var ini = esc(String(r.p.name || '?').replace(/[^A-Za-z0-9]/g, '').slice(0, 2).toUpperCase() || '?');
    return '<div style="flex:1 1 250px;min-width:0;display:flex;gap:10px;align-items:center;padding:8px 10px;' +
      'border:1px solid ' + col + '55;background:linear-gradient(90deg,' + col + '1c,transparent 70%)">' +
      '<div style="flex:0 0 auto;position:relative;width:' + size + 'px;height:' + size + 'px">' +
      '<div style="width:100%;height:100%;box-sizing:border-box;border-radius:50%;' +
      'border:3px solid ' + col + ';overflow:hidden;background:#151a14;display:flex;align-items:center;' +
      'justify-content:center;font-family:var(--f-disp);font-size:20px;color:' + teamColor(r.p.team) + '">' +
      (face ? '<img src="' + face + '" alt="" style="width:100%;height:100%;display:block">' : ini) + '</div>' +
      (emb ? '<img src="' + emb + '" alt="" title="' + esc((r.p.clan || '').trim()) + '" style="position:absolute;right:-5px;bottom:-4px;' +
             'width:26px;height:26px;image-rendering:pixelated;border:1px solid ' + col + ';background:#0b0d0c">' : '') +
      '</div>' +
      '<div style="min-width:0;line-height:1.45">' +
      '<div style="font-family:var(--f-disp);font-size:10px;letter-spacing:.16em;text-transform:uppercase;color:' + col + '">' +
      iconMask('star', col, 12) + STAR_NAME[rank] + '</div>' +
      '<div style="font-family:var(--f-disp);font-size:14px;color:' + teamColor(r.p.team) + ';overflow:hidden;text-overflow:ellipsis">' +
      esc(r.p.name || '(unidentified)') + '</div>' +
      '<div style="color:var(--ink)">' + (r.k || 0) + ' K &nbsp;/&nbsp; ' + (r.d || 0) + ' D</div>' +
      '<div style="font-size:11px;color:var(--ink-dim)">' + chips.map(esc).join('<br>') + '</div>' +
      '</div></div>';
  });
  // what is left of the old facts: who came and went, and the host's rotation
  var foot = [], nm = function (i) { var p = R.players[i]; return p ? '<b style="color:' + teamColor(p.team) + '">' + esc(p.name || '?') + '</b>' : '?'; };
  var lob = ((R.ins && R.ins.lobby) || []).filter(function (e) { return e.t <= LIM; });
  if (lob.length) foot.push(lob.map(function (e) {
    return nm(e.victim) + (e.type === 'join' ? ' joined ' : ' last seen ') + seekLink(e.t); }).join(' · '));
  var rot = hostRotation();
  if (rot) foot.push('Host rotation: ' + rot);
  if (!R.x) foot.push('Knock-outs, weapon lock-ons and shoulder side need the original .dat: open it with Open .dat or Link.');
  return '<div style="width:0;min-width:100%;box-sizing:border-box;padding:0 8px;margin-top:10px">' +
    '<div style="display:flex;flex-wrap:wrap;gap:10px;white-space:normal">' + cards.join('') + '</div>' +
    (foot.length ? '<div style="margin-top:6px;font-size:11px;color:var(--ink-faint);line-height:1.6">' + foot.join(' &nbsp;·&nbsp; ') + '</div>' : '') +
    '</div>';
}

// A picture of a player's own model, in the outfit the replay says he wore,
// struck in a pose from the game's own animation clips: gold salutes, silver
// aims the weapon he got most of his kills with, bronze does a charm dance
// (male rig only -- the female bank has none, so she gets the next in line).
// Drawn once into a small off-screen target and kept as an image. Null until
// his model exists.
var PORT = { scene: null, cam: null, rt: null, px: 224, tries: 0, timer: null };
var STAR_POSE = [['salute', 'idle'], ['aim', 'idle'], ['charm3', 'charm1', 'crouchAim', 'idle']];
var CHARM = { req: false };
function loadCharm() {
  if (CHARM.req) return;
  CHARM.req = true;
  try { window.__mgo2Charm = CHARM; } catch (e) {}
  fetch(av('anims_charm_male.json')).then(function (r) { return r.ok ? r.json() : null; }).then(function (A) {
    var rig = RIGS.male;
    if (!A || !A.clips || !rig || A.bones.length !== rig.anim.bones.length) { CHARM.state = 'unusable'; return; }
    CHARM.state = 'loaded';
    A.clips.forEach(function (c) {
      rig.anim.clips[c.name] = { frames: c.frames, dur: c.dur, speed: 0, loop: c.loop !== false,
                                 q: Int16Array.from(c.q), t: Int16Array.from(c.t) };
    });
    if (R) R.__faces = {};
    if ($('scorepane') && $('scorepane').dataset.open === '1') { try { renderScore(); } catch (e) {} }
  }).catch(function () {});
}
// the gun this player got most of his kills with, for the aiming pose
function starWeapon(pi, LIM) {
  var per = {}, top = null;
  (R.events || []).forEach(function (e) {
    if (e.type !== 'kill' || e.t > LIM || e.killer !== pi || !FIREARM[e.weapon] || SECONDARY[e.weapon]) return;
    per[e.weapon] = (per[e.weapon] || 0) + 1;
    if (top == null || per[e.weapon] > per[top]) top = e.weapon;
  });
  return top == null ? 24 : top;
}
// The frame of a clip where the right hand is highest against the head: the
// top of a salute, the high point of a dance.
function peakFrame(A, K, cl) {
  var nb = A.bones.length, best = 0, bv = -1e9;
  if (K.hand === undefined || K.head === undefined) return 0;
  for (var f = 0; f < cl.frames; f++) {
    // either hand: the salute clip raises the one the grip does not use
    var hy = cl.t[(f * nb + K.hand) * 3 + 1];
    if (K.hand2 !== undefined) hy = Math.max(hy, cl.t[(f * nb + K.hand2) * 3 + 1]);
    var v = hy - cl.t[(f * nb + K.head) * 3 + 1];
    if (v > bv) { bv = v; best = f; }
  }
  return best;
}
function playerPortrait(pi, rank, item) {
  if (!R || !renderer) return null;
  var m = marks[pi];
  if (!m || !m.skin || !m.rig || !m.geared) return null;
  var A = m.rig.anim, K = m.rig.skin, hb = K.head;
  if (!A || hb === undefined || !A.rest[hb]) return null;
  var want = STAR_POSE[rank] || ['idle'], pose = null;
  for (var w = 0; w < want.length && !pose; w++) if (A.clips[want[w]]) pose = want[w];
  if (m.rig.sex === 'male' && /^charm/.test(want[0]) && !A.clips[want[0]]) loadCharm();
  var cache = R.__faces || (R.__faces = {});
  var key = pi + (TEX_ON ? 't' : 'f') + (pose || '') + (pose === 'aim' ? item : '');
  if (cache[key]) return cache[key];
  if (!PORT.scene) {
    PORT.scene = new THREE.Scene();
    PORT.scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8a84, 3.4));
    var sun = new THREE.DirectionalLight(0xfff1dc, 2.4);
    sun.position.set(0.5, 0.6, 1);
    PORT.scene.add(sun);
    PORT.cam = new THREE.PerspectiveCamera(30, 1, 10, 8000);
    PORT.rt = new THREE.WebGLRenderTarget(PORT.px, PORT.px);
    PORT.rt.texture.colorSpace = THREE.SRGBColorSpace;
  }
  var bones = [], inv = [], root = new THREE.Group();
  for (var i = 0; i < A.bones.length; i++) {
    var r = A.rest[i], b = new THREE.Bone();
    b.position.set(r[0], r[1], r[2]);
    root.add(b); bones.push(b);
    inv.push(new THREE.Matrix4().makeTranslation(-r[0], -r[1], -r[2]));
  }
  var mesh = new THREE.SkinnedMesh(m.skin.geometry, m.skin.material);
  mesh.add(root);
  mesh.bind(new THREE.Skeleton(bones, inv), new THREE.Matrix4());
  mesh.frustumCulled = false;
  PORT.scene.add(mesh);
  var fm = { bones: bones, skin: mesh, lastClip: pose }, gun = null, gunPending = false, gunOwn = false;
  var yaw = 0;
  if (pose) {
    useRig(m.rig);
    var cl = A.clips[pose];
    poseSkin(fm, null, pose, (pose === 'aim' ? 0 : peakFrame(A, K, cl)) / A.fps);
    if (K.root !== undefined) yaw = quatYaw(bones[K.root].quaternion);
    // The real weapon model or nothing: the stand-in block is not shaped like
    // a gun the grip code can seat. The models load in the background, so an
    // early picture is redrawn once they are in.
    loadWeapons();
    var wg = weaponGeo(item) || weaponGeo(24);
    if (!wg && !WEAPONS) gunPending = true;
    try { window.__mgo2Port = { item: item, model: !!wg, weapons: !!WEAPONS, pose: pose, sex: m.rig.sex }; } catch (e) {}
    if (pose === 'aim' && K.hand !== undefined && wg) {
      try {
        gun = new THREE.Mesh(wg, weaponMats(wg) || new THREE.MeshLambertMaterial({ color: 0x23261f }));
        gunOwn = !weaponMats(wg);
        gun.frustumCulled = false;
        if (WEAPON_Q0) gun.quaternion.copy(WEAPON_Q0);
        bones[K.hand].add(gun);
        fm.handProp = gun;
        gripFromHands(fm, { alive: 1, item: item });
        gun.visible = true;
      } catch (e) { window.__portGunErr = e; if (gun) gun.visible = false; }
    }
  }
  // bone space is millimetres. Stand the camera in front of wherever the pose
  // has the body facing, a little to one side, framed head to belt.
  var h = bones[hb].position, tx = h.x, ty = h.y - 170, tz = h.z;
  if (pose === 'aim') { tx += Math.sin(yaw) * 110; tz += Math.cos(yaw) * 110; }
  // an aimed gun points straight out of the picture from the front, so that
  // pose is taken from well round to the side
  var ang = yaw + (pose === 'aim' ? 0.95 : 0.38), dist = pose === 'aim' ? 2050 : 1750;
  PORT.cam.position.set(tx + Math.sin(ang) * dist, ty + 120, tz + Math.cos(ang) * dist);
  PORT.cam.lookAt(tx, ty, tz);
  var prevRT = renderer.getRenderTarget(), prevCol = new THREE.Color(), prevA = renderer.getClearAlpha();
  renderer.getClearColor(prevCol);
  var buf = new Uint8Array(PORT.px * PORT.px * 4);
  try {
    renderer.setRenderTarget(PORT.rt);
    renderer.setClearColor(0x2c352b, 1);
    renderer.clear();
    renderer.render(PORT.scene, PORT.cam);
    renderer.readRenderTargetPixels(PORT.rt, 0, 0, PORT.px, PORT.px, buf);
  } finally {
    renderer.setRenderTarget(prevRT);
    renderer.setClearColor(prevCol, prevA);
    PORT.scene.remove(mesh);
    mesh.skeleton.dispose();
    if (gun && gunOwn) gun.material.dispose();
  }
  var cv = document.createElement('canvas');
  cv.width = cv.height = PORT.px;
  var cx = cv.getContext('2d'), img = cx.createImageData(PORT.px, PORT.px), row = PORT.px * 4;
  for (var y = 0; y < PORT.px; y++) {            // GL rows run bottom-up
    img.data.set(buf.subarray((PORT.px - 1 - y) * row, (PORT.px - y) * row), y * row);
  }
  cx.putImageData(img, 0, 0);
  var url = cv.toDataURL('image/png');
  // OUTFIT TEXTURES LOAD IN THE BACKGROUND. A picture taken before they land
  // shows grey cloth with no camo, or a black body (user, 2026-10-04: "the
  // portrait clothes aren't rendered properly"). Keep the picture only once
  // nothing is still loading; until then draw it again shortly.
  var pending = gunPending, hk;
  for (hk in TEX_CACHE) if (!TEX_CACHE[hk].image) { pending = true; break; }
  for (hk in WTEX_CACHE) if (!WTEX_CACHE[hk].image) { pending = true; break; }
  if (pending && PORT.tries < 40) {
    if (!PORT.timer) PORT.timer = setTimeout(function () {
      PORT.timer = null; PORT.tries++;
      if ($('scorepane') && $('scorepane').dataset.open === '1') { try { renderScore(); } catch (e) {} }
    }, 700);
    return url;
  }
  return (cache[key] = url);
}

// The host's map list (meta.rotation: 15 slots of map + mode), with repeats
// folded so a one-map room reads as one entry.
function hostRotation() {
  var rot = R.meta && R.meta.rotation;
  if (!rot || !rot.length || typeof RPDT === 'undefined') return '';
  var out = [], prev = null, count = 0;
  function flush() { if (prev != null) out.push(prev + (count > 1 ? ' ×' + count : '')); }
  rot.forEach(function (e) {
    var m = RPDT.MAPS && RPDT.MAPS[e[0]];
    var stage = m ? m[0] : null;
    var label = ((stage && MAPNAME[stage]) || (m && m[1]) || ('map ' + e[0])) + ' ' +
                ((RPDT.MODE_NAMES && RPDT.MODE_NAMES[e[1]]) || ('mode ' + e[1]));
    if (label === prev) { count++; return; }
    flush(); prev = label; count = 1;
  });
  flush();
  return esc(out.join(', '));
}

// ---- player card ---------------------------------------------------------
// Things the replay records about a player that the 3D view cannot show: the
// skills they equipped (with levels), their clan, and what each outfit slot is
// called. Skill names are the game's own internal codes; only the ones whose
// meaning is plain from the code get a friendlier label.
var SKILL_LABEL = {
  HAND_MAS: 'Handgun+', SBMC_MAS: 'SMG+', ASSLT_MAS: 'Assault Rifle+', SHOT_MAS: 'Shotgun+',
  SNIPPER_MAS: 'Sniper Rifle+', THROW: 'Throwing+', TRAP: 'Trickster', CQC: 'CQC+',
  KNIFE: 'Blades+', FASTMOV: 'Runner', SHIELD_MAS: 'Shield+', CQC_EX: 'CQC EX',
  SCANNING: 'Scanner', SCANNING_EX: 'Scanner EX', CBOX: 'Box Move', RECOVER: 'Quick Recovery'
};
var CARD_SLOTS = [['head', 'head', 'Head'], ['upper', 'upper', 'Top'], ['chest', 'chest', 'Chest'],
                  ['waist', 'waist', 'Waist'], ['hand', 'hand', 'Hands'], ['foot', 'foot', 'Feet'],
                  ['acc1', 'acc', 'Acc.'], ['acc2', 'acc', 'Acc.']];
var ITEM_NAME_FIX = { 'SaveMGO item 146': 'Beret (Emblem)' };
function updatePlayerCard() {
  var el = $('pcard');
  if (!el) {
    el = document.createElement('div');
    el.id = 'pcard';
    el.style.cssText = 'position:absolute;left:calc(var(--rail) + 26px);top:80px;z-index:14;' +
      'pointer-events:none;max-width:250px;font-size:11.5px;line-height:1.5;color:var(--ink-dim);' +
      'background:rgba(11,13,12,.78);border:1px solid var(--hair);padding:8px 10px';
    $('stage').appendChild(el);
  }
  var pl = (R && follow >= 0) ? R.players[follow] : null;
  if (!pl || !NAMES_ON || document.body.dataset.mobile === '1') { el.style.display = 'none'; return; }
  var h = '<div style="display:flex;align-items:center;gap:8px;margin-bottom:4px">';
  if (pl.clanId != null && EMB_INDEX && EMB_INDEX[pl.clanId]) {
    h += '<img src="' + av('emblems/' + pl.clanId + '.png') + '" width="32" height="32" ' +
         'style="image-rendering:pixelated;flex:0 0 auto" alt="">';
  }
  h += '<div><div style="font-family:var(--f-disp);font-size:13px;color:' + teamColor(pl.team) + '">' +
       esc(pl.name || '(unidentified)') + '</div><div style="color:var(--ink-faint)">' +
       esc((pl.clan || '').trim() || 'no clan') + '</div></div></div>';
  if (pl.skills && pl.skills.length) {
    h += '<div style="margin-bottom:4px">' + pl.skills.map(function (k) {
      return '<span title="' + esc(k.name) + '" style="white-space:nowrap">' +
             esc(SKILL_LABEL[k.name] || k.name) + ' <b style="color:var(--amber)">' + k.level + '</b></span>';
    }).join(', &nbsp;') + '</div>';
  } else if (pl.skills && !pl.skills.length) {
    h += '<div style="margin-bottom:4px;color:var(--ink-faint)">no skills equipped</div>';
  }
  var ap = pl.appearance, rig = ap && RIGS[sexOfPlayer(pl)];
  if (ap && rig && rig.gear) {
    var rows = [];
    CARD_SLOTS.forEach(function (sl) {
      var part = rig.gear[sl[1] + ':' + ap[sl[0]]];
      var nm = part && part.n && part.n !== '----' && part.n !== 'None' ? (ITEM_NAME_FIX[part.n] || part.n) : null;
      if (nm) rows.push('<span style="color:var(--ink-faint)">' + sl[2] + '</span> ' + esc(nm));
    });
    if (rows.length) h += '<div>' + rows.join('<br>') + '</div>';
  }
  // The match numbers that used to be listed here (kills, accuracy, damage per
  // weapon, range, streaks, lock-ons, nemesis...) are all on the scoreboard's
  // tabs. The card is who the player is and what he is wearing, nothing more
  // (user, 2026-10-04: "I don't think this info is really necessary on this screen").
  el.innerHTML = h;
  el.style.display = 'block';
}

function selectPlayer(i) {
  if (camSubj) { camSubj = null; syncSnapBtn(); if (follow === i) follow = -1; }
  follow = follow === i ? -1 : i;
  if (follow >= 0) orbit.dist = clamp(orbit.dist, 25, 70);
  // In free cam a tap only selects (card + timeline); the camera stays put.
  if (follow >= 0 && camMode !== 'follow' && camMode !== 'free') setCam('follow');
  if (follow < 0 && camMode === 'follow') setCam('orbit');
  updateRoster();
  updateFollowStrip(true);
  updatePlayerCard();
  if (HEAT.mode) buildHeat();
}

// -- CAMERA SUBJECTS that are not players (user, 2026-10-05) ------------------
// camSubj = { kind: 'nade', n }   a grenade, from a click on it, until it has gone off
//         | { kind: 'obj' }       the objective: its carrier, else where it lies / is planted
//         | { kind: 'site', k }   a Bomb plant site
// Picking a player, or the Target button cycling round to "off", clears it.
var camSubj = null, subjTmp = [0, 0, 0, 0];
function objPoint(t) {
  var sp = carrySpansAt(t);
  for (var i = 0; i < sp.length; i++) { var c = live[sp[i].i]; if (c) return [c.x, c.y, c.z, 10 / S]; }
  var lb = liveBombAt(t);
  if (lb && lb.x != null) {
    var pc = insCell(R, lb.planter, lb.t);
    return [lb.x, pc ? pc[1] : (lb.y != null ? lb.y : 0), lb.z, 0];
  }
  var lo = bombLooseAt(t);
  if (lo) return [lo.x, lo.y, lo.z, 0];
  for (var d = 0; d < dropSpans.length; d++)
    if (t >= dropSpans[d].t0 && t < dropSpans[d].t1 && dropSpans[d].x != null) return [dropSpans[d].x, dropSpans[d].y || 0, dropSpans[d].z, 0];
  return null;
}
function camSubjPoint() {
  if (!camSubj || !R) return null;
  if (camSubj.kind === 'nade') {
    var n = camSubj.n, tf = n.f || 2.0, age = T - n.t;
    if (age < -0.5 || age > tf + 2.5) { camSubj = null; syncSnapBtn(); return null; }   // gone off: back to the player
    nadePoint(n, clamp(age, 0, tf), subjTmp); subjTmp[3] = 0;
    return subjTmp;
  }
  if (camSubj.kind === 'site') {
    var st = bombSites()[camSubj.k];
    return st ? [st.x, st.y, st.z, 0] : null;
  }
  return objPoint(T) || camSubj.last || null;
}
// A click within reach of a grenade in flight follows that grenade.
function tapNade(px, py) {
  if (!R || !R.nades || !cam || !renderer) return false;
  var rc = renderer.domElement.getBoundingClientRect(), v = new THREE.Vector3(), o = [0, 0, 0], best = null, bd = mobile ? 44 : 26;
  for (var i = 0; i < R.nades.length; i++) {
    var n = R.nades[i], age = T - n.t;
    if (age < 0 || age > (n.f || 2.0)) continue;
    nadePoint(n, age, o);
    v.set((o[0] - ctr[0]) * S, o[1] * S, (o[2] - ctr[2]) * S).project(cam);
    if (v.z > 1) continue;
    var d = Math.hypot(rc.left + (v.x + 1) / 2 * rc.width - px, rc.top + (1 - v.y) / 2 * rc.height - py);
    if (d < bd) { bd = d; best = n; }
  }
  if (!best) return false;
  setAutoFollow(false);
  camSubj = { kind: 'nade', n: best };
  if (camMode !== 'follow') setCam('follow');
  syncSnapBtn();
  return true;
}
// TARGET button: off -> the objective (bomb / carrier) -> plant site 1 -> 2 -> off.
function snapChoices() {
  var out = [];
  if (!R) return out;
  if ((R.meta && R.meta.mode === 6) || objPoint(T) || (carrySpans && carrySpans.length))
    out.push({ kind: 'obj', label: R.meta && R.meta.mode === 6 ? 'Bomb' : 'Target' });
  bombSites().forEach(function (s, k) { out.push({ kind: 'site', k: k, label: 'Site ' + (k + 1) }); });
  return out;
}
function cycleSnap() {
  var ch = snapChoices(), at = -1;
  for (var i = 0; i < ch.length; i++)
    if (camSubj && camSubj.kind === ch[i].kind && (camSubj.k == null || camSubj.k === ch[i].k)) at = i;
  var nx = ch[at + 1] || null;
  camSubj = nx ? { kind: nx.kind, k: nx.k } : null;
  if (camSubj) {
    setAutoFollow(false);
    if (camMode !== 'follow') setCam('follow');
    if (camSubj.kind === 'site') orbit.dist = clamp(orbit.dist, 40, 90);
  }
  syncSnapBtn();
}
function syncSnapBtn() {
  var b = $('b-snap');
  if (!b) {
    var ob = $('b-obj');
    if (!ob || !ob.parentNode) return;
    b = document.createElement('button');
    b.className = 'tbtn wide'; b.id = 'b-snap';
    b.title = 'Snap the camera to the objective: the bomb or its carrier, then each plant site (B). Click a grenade in flight to ride it.';
    ob.parentNode.insertBefore(b, ob.nextSibling);
    b.addEventListener('click', cycleSnap);
  }
  var ch = snapChoices(), lab = 'Target';
  if (camSubj) {
    if (camSubj.kind === 'nade') lab = 'Grenade';
    for (var i = 0; i < ch.length; i++)
      if (camSubj.kind === ch[i].kind && (camSubj.k == null || camSubj.k === ch[i].k)) lab = ch[i].label;
  }
  b.textContent = lab;
  b.dataset.on = camSubj ? '1' : '0';
  b.style.display = ch.length || camSubj ? '' : 'none';
}

// -- MATCH INTRO CARD (VIEWER_IDEAS D8): map, mode, both rosters with clan
// emblems, and the host's rotation, over the first INTRO_S seconds. Click it,
// or move past that time, and it goes. ?intro=0 turns it off.
var INTRO_S = 6, INTRO_ON = !/[?&]intro=0/.test(location.search), introFor = null, introGone = null;
// TEAM LEAVE / REMAKE (user 2026-10-10): when someone disconnects the team often knows in voice chat before the game does, so
// "sometimes a whole team will leave at the start of a match to get a full 6", and leaving that early does not count for
// stats. In the file that is several players of ONE team whose last record of any kind falls in the first seconds
// (match 69236: four of five gone within 3 s, the other side played on 6 v 1). Flagged when at least 3 players and at
// least half of a team are last heard from inside REMAKE_S of a match longer than 90 s. r.remake = null when it is not one.
var REMAKE_S = 20;
function detectRemake(r) {
  if (!r || r.remake !== undefined) return r ? r.remake : null;
  r.remake = null;
  if (!r.frames || !r.frames.length || !r.players) return null;
  var t0 = r.frames[0].t, n = r.players.length, last = [], k, j;
  if ((r.duration || 0) - t0 < 90) return null;
  for (j = 0; j < n; j++) last.push(null);
  for (k = 0; k < r.frames.length; k++) {
    var row = r.frames[k].p; if (!row) continue;
    for (j = 0; j < n; j++) if (row[j] && row[j][0] != null) last[j] = r.frames[k].t;
  }
  var teams = {};
  r.players.forEach(function (p, i) {
    if (p.team == null) return;
    var g = teams[p.team] || (teams[p.team] = { team: p.team, n: 0, left: [], at: 0 });
    g.n++;
    if (last[i] == null || last[i] - t0 < REMAKE_S) { g.left.push(i); g.at = Math.max(g.at, last[i] == null ? 0 : last[i] - t0); }
  });
  var best = null;
  Object.keys(teams).forEach(function (key) {
    var g = teams[key];
    if (g.left.length >= 3 && g.left.length * 2 >= g.n && (!best || g.left.length > best.left.length)) best = g;
  });
  r.remake = best;
  r.leftEarly = last.map(function (v) { return v == null || v - t0 < REMAKE_S; });
  return best;
}
function updateIntroCard() {
  var el = $('introcard');
  var rm_ = R ? detectRemake(R) : null;
  var want = INTRO_ON && R && T < (rm_ ? INTRO_S + 6 : INTRO_S) && introGone !== R && !photoMode;
  if (!want) { if (el) el.style.display = 'none'; return; }
  if (!el) {
    el = document.createElement('div'); el.id = 'introcard';
    el.style.cssText = 'position:fixed;left:50%;top:46%;transform:translate(-50%,-50%);z-index:40;background:rgba(8,11,9,.9);border:1px solid var(--hair);' +
      'padding:18px 22px;min-width:min(560px,86vw);max-width:92vw;font-family:var(--f-disp);letter-spacing:.06em;text-transform:uppercase;color:var(--ink);cursor:pointer';
    el.title = 'Click to dismiss';
    el.addEventListener('click', function () { introGone = R; el.style.display = 'none'; });
    document.body.appendChild(el);
  }
  if (introFor !== R) {
    introFor = R;
    var teams = {}, order = [];
    R.players.forEach(function (p) { var k = p.team == null ? 'x' : p.team; if (!teams[k]) { teams[k] = []; order.push(k); } teams[k].push(p); });
    order.sort();
    var stage = ($('b-stage') && $('b-stage').textContent) || R.stage || '';
    var modeTxt = '';
    try { modeTxt = (R.meta && typeof RPDT !== 'undefined' && RPDT.MODE_NAMES && RPDT.MODE_NAMES[R.meta.mode]) || ''; } catch (e) {}
    var rot = ''; try { rot = hostRotation() || ''; } catch (e2) {}
    var h = '<div style="font-size:11px;color:var(--ink-dim)">' + esc(String(modeTxt)) + '</div>' +
            '<div style="font-size:26px;color:var(--amber);margin:2px 0 12px">' + esc(stage) + '</div><div style="display:flex;gap:26px;flex-wrap:wrap">';
    order.forEach(function (k) {
      h += '<div style="flex:1;min-width:200px"><div style="font-size:12px;color:' + teamColor(k === 'x' ? null : +k) + ';border-bottom:1px solid var(--hair);padding-bottom:3px;margin-bottom:5px">' +
           esc(k === 'x' ? 'Players' : teamLabel(+k)) + '</div>';
      teams[k].forEach(function (p) {
        var emb = (p.clanId != null && EMB_INDEX && EMB_INDEX[p.clanId]) ? '<img src="' + av('emblems/' + p.clanId + '.png') + '" width="18" height="18" style="vertical-align:middle;margin-right:6px;image-rendering:pixelated">' : '<span style="display:inline-block;width:24px"></span>';
        var gone_ = R.leftEarly && R.leftEarly[R.players.indexOf(p)];
        h += '<div style="font-size:13px;line-height:22px;white-space:nowrap' + (gone_ ? ';opacity:.45;text-decoration:line-through' : '') + '">' + emb + esc(p.name || '(unidentified)') +
             (p.clan ? ' <span style="font-size:10px;color:var(--ink-dim)">' + esc(p.clan) + '</span>' : '') + '</div>' +
             (gone_ ? '<div style="font-size:9px;line-height:10px;margin:-4px 0 2px;color:var(--ink-dim);text-transform:none;letter-spacing:0">left at the start</div>' : '');
      });
      h += '</div>';
    });
    h += '</div>' + (rot ? '<div style="font-size:10px;color:var(--ink-dim);margin-top:12px;text-transform:none;letter-spacing:0">Host rotation: ' + rot + '</div>' : '');
    if (rm_) {
      h = '<div style="border:1px solid var(--amber);color:var(--amber);padding:6px 10px;margin-bottom:12px;font-size:12px;line-height:17px">' +
          'Team leave / remake<div style="font-size:10px;color:var(--ink);text-transform:none;letter-spacing:0">' + rm_.left.length + ' of ' + rm_.n + ' ' +
          esc(teamLabel(+rm_.team)) + ' players left in the first ' + Math.max(1, Math.ceil(rm_.at)) + ' s. A team leaving this early usually means a remake, and the result does not count.</div></div>' + h;
    }
    el.innerHTML = h;
  }
  el.style.display = 'block';
  el.style.opacity = clamp(((rm_ ? INTRO_S + 6 : INTRO_S) - T) / 0.8, 0, 1);
}
// -- STREAK BANNER (VIEWER_IDEAS A6): the game's own streak notice, called out on
// screen for STREAK_S seconds. Kill streaks only, from 3.
var STREAK_S = 3.5;
function updateStreakBanner() {
  var el = $('streakbanner'), best = null;
  ((R && R.meta && R.meta.streakOps) || []).forEach(function (s) {
    if (s.kind === 3 || s.player == null || s.count < 3 || s.t > T || T - s.t > STREAK_S) return;
    if (!best || s.t > best.t || (s.t === best.t && s.count > best.count)) best = s;
  });
  if (!best || !R.players[best.player]) { if (el) el.style.display = 'none'; return; }
  if (!el) {
    el = document.createElement('div'); el.id = 'streakbanner';
    el.style.cssText = 'position:fixed;left:50%;top:104px;transform:translateX(-50%);z-index:30;pointer-events:none;padding:6px 16px;background:rgba(8,11,9,.82);' +
      'border:1px solid var(--hair);font-family:var(--f-disp);letter-spacing:.14em;text-transform:uppercase;font-size:15px;white-space:nowrap';
    document.body.appendChild(el);
  }
  var p = R.players[best.player], key = best.t + ':' + best.count;
  if (el.dataset.k !== key) {
    el.dataset.k = key;
    el.innerHTML = '<span style="color:' + teamColor(p.team) + '">' + esc(p.name || '?') + '</span> &nbsp;<b style="color:var(--amber)">' + best.count + ' in a row</b>';
  }
  el.style.display = 'block';
  el.style.opacity = clamp((STREAK_S - (T - best.t)) / 0.6, 0, 1);
}
// -- CLIP REVIEW (VIEWER_IDEAS B14): key R opens a list of every animation clip.
// Picking one plays it on EVERY body in the replay (so a male and a female
// player show the same clip on both skeletons), and Good / Wrong marks are
// kept in this browser and can be copied out as text.
var clipMarks = {};
try { clipMarks = JSON.parse(localStorage.getItem('mgo2clipmarks') || '{}') || {}; } catch (eC) {}
function clipNames() {
  var set = {};
  Object.keys(RIGS).forEach(function (sx) { var a = RIGS[sx] && RIGS[sx].anim; if (a && a.clips) Object.keys(a.clips).forEach(function (n) { set[n] = (set[n] || '') + sx[0]; }); });
  return Object.keys(set).sort().map(function (n) { return [n, set[n]]; });
}
function renderClipReview() {
  var el = $('clipreview'); if (!el) return;
  var names = clipNames();
  var h = '<b>Clip review</b><div style="display:flex;gap:4px;flex-wrap:wrap;margin:6px 0">' +
    '<button class="tbtn" data-cr="good">Good</button><button class="tbtn" data-cr="wrong">Wrong</button>' +
    '<button class="tbtn" data-cr="off">Live</button><button class="tbtn" data-cr="copy">Copy</button><button class="tbtn" data-cr="close">X</button></div>' +
    '<div style="font-size:10px;opacity:.6;margin-bottom:6px;text-transform:none;letter-spacing:0">Up / Down steps. m = male body has it, f = female. ' + names.length + ' clips.</div>';
  names.forEach(function (nm) {
    var mk = clipMarks[nm[0]], on = FORCE_CLIP === nm[0];
    h += '<div data-clip="' + esc(nm[0]) + '" style="cursor:pointer;padding:2px 6px;white-space:nowrap;' + (on ? 'background:rgba(255,122,24,.25);' : '') + '">' +
         '<span style="display:inline-block;width:14px;color:' + (mk === 'good' ? '#5CE06A' : mk === 'wrong' ? '#E2574C' : 'transparent') + '">' + (mk === 'wrong' ? 'x' : 'o') + '</span>' +
         esc(nm[0]) + ' <span style="opacity:.45;font-size:10px">' + nm[1] + '</span></div>';
  });
  el.innerHTML = h;
  var cur = el.querySelector('[data-clip="' + (FORCE_CLIP || '').replace(/"/g, '') + '"]');
  if (cur && cur.scrollIntoView) cur.scrollIntoView({ block: 'nearest' });
}
function toggleClipReview(show) {
  var el = $('clipreview');
  if (!el) {
    el = document.createElement('div'); el.id = 'clipreview';
    el.style.cssText = 'position:fixed;left:300px;top:190px;bottom:190px;width:270px;overflow:auto;z-index:45;background:rgba(8,11,9,.94);border:1px solid var(--hair);' +
      'padding:8px;font-family:var(--f-disp);font-size:12px;letter-spacing:.05em;text-transform:uppercase;color:var(--ink);display:none';
    document.body.appendChild(el);
    el.addEventListener('click', function (e) {
      var b = e.target.closest('[data-cr]'), c = e.target.closest('[data-clip]');
      if (c) FORCE_CLIP = c.dataset.clip;
      else if (b) {
        var a = b.dataset.cr;
        if (a === 'close') { toggleClipReview(false); return; }
        if (a === 'off') FORCE_CLIP = null;
        if ((a === 'good' || a === 'wrong') && FORCE_CLIP) {
          if (clipMarks[FORCE_CLIP] === a) delete clipMarks[FORCE_CLIP]; else clipMarks[FORCE_CLIP] = a;
          try { localStorage.setItem('mgo2clipmarks', JSON.stringify(clipMarks)); } catch (e2) {}
        }
        if (a === 'copy') {
          var txt = Object.keys(clipMarks).sort().map(function (n) { return clipMarks[n] + '\t' + n; }).join('\n');
          try { navigator.clipboard.writeText(txt); b.textContent = 'Copied'; } catch (e3) {}
          return;
        }
      }
      renderClipReview();
    });
  }
  if (show == null) show = el.style.display === 'none';
  el.style.display = show ? 'block' : 'none';
  if (!show) FORCE_CLIP = null;
  if (show) renderClipReview();
}
function stepClipReview(d) {
  var el = $('clipreview'); if (!el || el.style.display === 'none') return false;
  var names = clipNames().map(function (n) { return n[0]; }), i = names.indexOf(FORCE_CLIP);
  FORCE_CLIP = names[clamp(i + d, 0, names.length - 1)] || null;
  renderClipReview();
  return true;
}

// -- MUZZLE FLASH, SHIELD, COOLANT drawn in the world (VIEWER_IDEAS B10, D11).
// A shot is the firing pulse the sounds already use (isShot); the flash sits
// 0.9 m out along the shooter's aim from chest height -- an approximate muzzle,
// not the gun model's own. Shield = action 27, coolant = actions 48 / 50.
var fxPool = [];
var MZF = { tex: null, cells: null, asked: false };
function mzfCells() {
  if (MZF.cells) return MZF.cells;
  if (!MZF.asked) {
    MZF.asked = true;
    new THREE.TextureLoader().load(av('fx/muzzle01.png'), function (t) {
      t.colorSpace = THREE.SRGBColorSpace;
      var cs = [];
      for (var q = 0; q < 4; q++) {
        var c = t.clone();
        c.repeat.set(0.5, 0.5); c.offset.set((q % 2) * 0.5, (q >> 1) * 0.5); c.needsUpdate = true;
        cs.push(c);
      }
      MZF.cells = cs;
      // glow balls made before the image arrived are rebuilt as sprites on their next use
      fxPool.forEach(function (fx) { if (fx && !fx.flash.userData.sprite) fx.stale = true; });
    });
  }
  return null;
}
// ask for the flash image up front: loaded on first use, the first shot of every session
// was still drawn as the old glow ball (seen in the headless check, 2026-10-08)
try { mzfCells(); } catch (eMz) {}
function fxGet(i) {
  if (fxPool[i] && fxPool[i].stale) {
    var old = fxPool[i];
    [old.flash, old.shield, old.spray].forEach(function (o) { scene.remove(o); if (o.geometry) o.geometry.dispose(); if (o.material) o.material.dispose(); });
    fxPool[i] = null;
  }
  if (fxPool[i]) return fxPool[i];
  // THE GAME'S OWN FLASH: texture ef_mzf_muzzle01_iy_alp (fx/muzzle01.png), a 2 x 2 sheet of
  // four flash shapes, drawn as a camera-facing sprite with one shape picked per shot. The
  // game's colour and size for it live in an effect file not decoded, so the warm tint and
  // the ~0.5 m size are this viewer's choice. Until the image has loaded (or if it fails)
  // the old plain glow ball is used.
  var flash = new THREE.Mesh(new THREE.SphereGeometry(1, 8, 6),
    new THREE.MeshBasicMaterial({ color: 0xFFE9A8, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false }));
  var cells = mzfCells();
  if (cells) {
    flash = new THREE.Sprite(new THREE.SpriteMaterial({ map: cells[0], color: 0xFFD08A, transparent: true, opacity: 0.95,
      blending: THREE.AdditiveBlending, depthWrite: false }));
    flash.userData.sprite = true;
  }
  var shield = new THREE.Mesh(new THREE.PlaneGeometry(6.5, 10),
    new THREE.MeshBasicMaterial({ color: 0x9FD8FF, transparent: true, opacity: 0.35, side: THREE.DoubleSide, depthWrite: false }));
  var cg = new THREE.ConeGeometry(4.5, 26, 10, 1, true); cg.translate(0, -13, 0); cg.rotateX(-Math.PI / 2);   // apex at the origin, opening along +Z
  var spray = new THREE.Mesh(cg, new THREE.MeshBasicMaterial({ color: 0xBFEFFF, transparent: true, opacity: 0.4, side: THREE.DoubleSide, depthWrite: false, blending: THREE.AdditiveBlending }));
  flash.visible = shield.visible = spray.visible = false;
  scene.add(flash); scene.add(shield); scene.add(spray);
  return (fxPool[i] = { flash: flash, shield: shield, spray: spray });
}
function updateActionFx() {
  if (!R || !scene) return;
  var fi = Math.floor(frameIndexAt(T)), FLASH_S = 0.07;
  for (var i = 0; i < live.length; i++) {
    var p = live[i], fx = fxPool[i];
    if (fx) fx.flash.visible = fx.shield.visible = fx.spray.visible = false;
    if (!p || !p.alive) continue;
    // a shot in the last FLASH_S seconds
    var shotAge = -1;
    for (var k = fi; k >= 1 && k > fi - 4; k--) {
      var fr = R.frames[k]; if (!fr || T - fr.t > FLASH_S) break; if (fr.t > T) continue;
      if (isShot((fr.p || [])[i], (R.frames[k - 1].p || [])[i])) { shotAge = T - fr.t; break; }
    }
    var shield = p.act === ACT_SHIELD, cool = p.act != null && !!ACT_COOLANT[p.act];
    if (shotAge < 0 && !shield && !cool) continue;
    fx = fxGet(i);
    var c = insCell(R, i, T), by = p.y + 1300, dx, dy, dz;
    if (c && c[9] != null && c[11] != null) { dx = c[9] - p.x; dy = (c[10] != null ? c[10] : by) - by; dz = c[11] - p.z; }
    else { dx = Math.sin(p.h || 0); dy = 0; dz = Math.cos(p.h || 0); }
    var L = Math.hypot(dx, dy, dz) || 1; dx /= L; dy /= L; dz /= L;
    var ox = (p.x - ctr[0]) * S, oy = by * S, oz = (p.z - ctr[2]) * S;
    if (shotAge >= 0) {
      var fs = (0.7 + 0.6 * (1 - shotAge / FLASH_S)) * (0.8 + 0.4 * ((fi * 7 + i * 3) % 5) / 5);
      // at the TIP OF THE BARREL of the gun in his hands (muzzleOf), pushed a hand's width on
      // along the aim so the flash sits in front of the muzzle; no gun model -> the old
      // estimate 0.9 m out from the chest
      var mzp = null;
      try { mzp = muzzleOf(i); } catch (eM) { mzp = null; }
      if (mzp) fx.flash.position.set(mzp.x + dx * 1.2, mzp.y + dy * 1.2, mzp.z + dz * 1.2);
      else fx.flash.position.set(ox + dx * 9, oy + dy * 9, oz + dz * 9);
      if (fx.flash.userData.sprite) {
        var shotK = Math.round((T - shotAge) * 30) + i * 3;          // one shape and one roll per shot
        fx.flash.material.map = MZF.cells[((shotK % 4) + 4) % 4];
        fx.flash.material.rotation = (shotK * 2.399) % 6.283;
        fs *= 5.2;
      }
      fx.flash.scale.set(fs, fs, fs);
      fx.flash.material.opacity = 0.95 * (1 - 0.6 * shotAge / FLASH_S); fx.flash.visible = true;
    }
    if (shield) {
      var hx = Math.sin(p.h || 0), hz = Math.cos(p.h || 0);
      fx.shield.position.set(ox + hx * 5.5, (p.y + 900) * S, oz + hz * 5.5); fx.shield.rotation.set(0, p.h || 0, 0); fx.shield.visible = true;
    }
    if (cool) {
      fx.spray.position.set(ox + dx * 6, oy - 2 + dy * 6, oz + dz * 6);
      fx.spray.lookAt(ox + dx * 100, oy - 2 + dy * 100, oz + dz * 100);
      fx.spray.material.opacity = 0.28 + 0.18 * Math.abs(Math.sin(T * 31 + i)); fx.spray.visible = true;
    }
  }
}

// -- DROP THE MENU DOWN (user, 2026-10-05: on a phone the bottom menu "covers
// most the screen"). A tab on the top edge of the bottom panel folds it to the
// timeline plus the play buttons and clock; tap again to bring it back. Shown
// on phone layouts only; the choice is remembered in this browser.
var dockMin = false;
function setDockMin(on) {
  dockMin = !!on;
  if (!$('dockmin-css')) {
    var st = document.createElement('style'); st.id = 'dockmin-css';
    st.textContent =
      '#dock-tab{display:none;position:absolute;left:0;top:-30px;height:28px;padding:0 12px;background:rgba(11,15,12,.92);color:var(--ink);' +
      'border:1px solid var(--hair);border-bottom:0;font-family:var(--f-disp);font-size:11px;letter-spacing:.1em;text-transform:uppercase;cursor:pointer}' +
      'body[data-mobile="1"] #dock-tab{display:block}' +
      '@media (max-width:760px){#dock-tab{display:block}}' +
      // PHONE HELD SIDEWAYS (user, same day: "collapse doesnt work well in horizontal mode").
      // A sideways phone is about 850 x 390: too wide for the page's phone rules, so it
      // got the desktop layout crammed into 390 px. Any screen under 520 px tall is now
      // laid out as a phone (resize() sets data-short), with one row of buttons that
      // scrolls sideways instead of wrapping.
      'body[data-short="1"] #dock-tab{display:block}' +
      'body[data-short="1"] #brand,body[data-short="1"] #betabar,body[data-short="1"] #animdiag{display:none!important}' +
      'body[data-short="1"] #roster,body[data-short="1"] #feedpane{left:8px;right:8px;width:auto;top:8px;bottom:auto;max-height:62vh;display:none}' +
      'body[data-short="1"] #roster[data-open="1"],body[data-short="1"] #feedpane[data-open="1"]{display:flex}' +
      'body[data-short="1"] #mtabs{display:flex;order:-1}' +
      'body[data-short="1"] #dock{left:6px;right:6px;bottom:6px;height:auto}' +
      'body[data-short="1"] #tl{height:38px}' +
      'body[data-short="1"] #bar{flex-wrap:nowrap;overflow-x:auto;gap:4px;padding:0 6px 6px;justify-content:flex-start;scrollbar-width:none}' +
      'body[data-short="1"] #bar .grp{flex:0 0 auto;flex-wrap:nowrap;gap:4px}' +
      'body[data-short="1"] #bar .sep,body[data-short="1"] #bar .lbl{display:none}' +
      'body[data-short="1"] #right{width:auto;margin-left:0;flex:0 0 auto}' +
      'body[data-short="1"] .moreonly{display:inline-flex;gap:4px}' +
      'body[data-short="1"] .tbtn{width:32px;height:28px}body[data-short="1"] .tbtn.wide{width:auto;padding:0 7px;font-size:10.5px;white-space:nowrap}' +
      'body[data-short="1"] #clock{font-size:15px;min-width:0;padding:0 3px;flex:0 0 auto}' +
      'body[data-short="1"]:not(.dockmin) #ghud{display:none!important}' +
      // ROSTER / FEED on a phone (user: the buttons "do nothing"): the page hides the
      // two panels by id and re-shows them by class, and an id rule always beats a
      // class rule, so the open panel stayed hidden. Shown here by id.
      '@media (max-width:760px){#roster[data-open="1"],#feedpane[data-open="1"]{display:flex!important;z-index:60}}' +
      'body[data-short="1"] #roster[data-open="1"],body[data-short="1"] #feedpane[data-open="1"]{display:flex!important;z-index:60}' +
      'body.dockmin #fstrip{display:none!important}' +
      'body[data-short="1"] #ghud{left:8px!important;top:8px!important;bottom:auto!important;transform:scale(.78);transform-origin:top left}' +
      // with the full menu open on a phone the follow read-out sat on top of it
      'body[data-mobile="1"]:not(.dockmin) #ghud{display:none!important}' +
      '@media (max-width:760px){body:not(.dockmin) #ghud{display:none!important}}' +
      // folded = the WHOLE bottom panel gone, only the tab left (user, same day)
      'body.dockmin #dock>*:not(#dock-tab){display:none!important}' +
      'body.dockmin #dock{background:none!important;border:0!important;box-shadow:none!important;min-height:0!important;height:0!important;padding:0!important}' +
      'body.dockmin #dock-tab{border-bottom:1px solid var(--hair);top:-54px;height:34px;padding:0 16px}' +
      'body.dockmin #bar>*:not(.grp){display:none!important}' +
      'body.dockmin #bar>.grp~.grp{display:none!important}';
    document.head.appendChild(st);
    var dk = $('dock');
    if (dk) {
      if (getComputedStyle(dk).position === 'static') dk.style.position = 'relative';
      dk.style.overflow = 'visible';
      var tb = document.createElement('button'); tb.id = 'dock-tab';
      tb.addEventListener('click', function () { setDockMin(!dockMin); });
      dk.appendChild(tb);
    }
  }
  document.body.classList.toggle('dockmin', dockMin);
  var b = $('dock-tab'); if (b) b.textContent = dockMin ? '\u25B4 Menu' : '\u25BE Hide menu';
  try { localStorage.setItem('mgo2dockmin', dockMin ? '1' : '0'); } catch (e) {}
  try { window.dispatchEvent(new Event('resize')); } catch (e2) {}
}

// -- PHOTO MODE (user, 2026-10-05: "need option to turn HUD on or off so i can
// take nice pics"). The Photo button / key P steps: everything -> panels off,
// name tags and labels kept -> nothing but the 3D view -> back. Everything is
// hidden with visibility, so the layout and the canvas size do not move. A
// faint button stays in the corner to step on; Esc goes straight back. The
// bottom bar (timeline and buttons) stays in both steps (user, same day).
var photoMode = 0;
function setPhoto(m) {
  photoMode = ((m % 3) + 3) % 3;
  if (!$('photo-css')) {
    var st = document.createElement('style'); st.id = 'photo-css';
    st.textContent = 'body.photo *{visibility:hidden!important}' +
      'body.photo #gl,body.photo #hud,body.photo #photo-btn,body.photo #dock,body.photo #dock *{visibility:visible!important}' +
      'body.photo2 #hud{visibility:hidden!important}' +
      '#photo-btn{position:fixed;right:10px;top:10px;z-index:9999;opacity:.12;display:none;padding:6px 10px;' +
      'background:#000;color:#fff;border:1px solid #fff;font:11px sans-serif;letter-spacing:.08em;cursor:pointer}' +
      '#photo-btn:hover{opacity:.9}body.photo #photo-btn{display:block}';
    document.head.appendChild(st);
    var pb = document.createElement('button'); pb.id = 'photo-btn';
    pb.title = 'Photo mode: click or press P to step, Esc to bring everything back';
    pb.addEventListener('click', function () { setPhoto(photoMode + 1); });
    document.body.appendChild(pb);
  }
  document.body.classList.toggle('photo', photoMode > 0);
  document.body.classList.toggle('photo2', photoMode === 2);
  $('photo-btn').textContent = photoMode === 1 ? 'TAGS ONLY' : 'CLEAN';
  var b = $('b-photo'); if (b) b.dataset.on = photoMode ? '1' : '0';
}

// -- tap a player in the 3D view -----------------------------------------
// Hit-tests the nametag box first and the body second, because the tag is what
// a finger is actually aiming at. Touch gets a wider radius than a mouse: a
// fingertip covers about 9 mm, a cursor covers one pixel.
function pickAt(px, py) {
  var best = -1, bd = 1e9;
  for (var k = 0; k < hudPick.length; k++) {
    var it = hudPick[k];
    var dx = Math.max(Math.abs(px - it.lx) - it.w / 2, 0);
    var dy = Math.max(Math.abs(py - it.ly) - it.h / 2, 0);
    var d = Math.min(Math.hypot(dx, dy), Math.hypot(px - it.x, py - it.y));
    if (it.dead) d += 40;            // prefer the living where tags overlap
    if (d < bd) { bd = d; best = it.i; }
  }
  return (best >= 0 && bd <= (mobile ? 56 : 34)) ? best : -1;
}
function tapAt(px, py) {
  if (tapNade(px, py)) return true;
  var i = pickAt(px, py);
  if (i < 0) return false;
  setAutoFollow(false);
  selectPlayer(i);
  return true;
}

// -- follow strip ---------------------------------------------------------
// One chip per player, always visible on a phone, so following someone never
// costs an open-pane / tap / close-pane round trip.
var stripFollow = -2;
function buildFollowStrip() {
  var st = $('fstrip');
  if (!st) {
    st = document.createElement('div');
    st.id = 'fstrip';
    $('dock').insertBefore(st, $('bar'));
    st.addEventListener('click', function (e) {
      var b = e.target.closest('[data-fp]');
      if (!b) return;
      if (b.dataset.fp === 'auto') { setAutoFollow(!autoFollow); return; }
      setAutoFollow(false);
      selectPlayer(+b.dataset.fp);
    });
  }
  var html = '<button class="fchip auto" data-fp="auto" data-on="0" ' +
             'title="Follow the action automatically (A)">AUTO</button>';
  (R ? R.players : []).forEach(function (pl, i) {
    html += '<button class="fchip" data-fp="' + i + '" data-sel="0" data-dead="0">' +
            '<span class="d" style="background:' + teamColor(pl.team) + '"></span>' +
            esc(pl.name) + '</button>';
  });
  st.innerHTML = html;
  stripFollow = -2;
  updateFollowStrip(true);
}
function updateFollowStrip(scrollToSel) {
  var st = $('fstrip');
  if (!st) return;
  var a = st.querySelector('[data-fp="auto"]');
  if (a) a.dataset.on = autoFollow ? '1' : '0';
  var sel = null;
  st.querySelectorAll('[data-fp]').forEach(function (b) {
    if (b.dataset.fp === 'auto') return;
    var i = +b.dataset.fp;
    b.dataset.sel = i === follow ? '1' : '0';
    b.dataset.dead = (live[i] && !live[i].alive) ? '1' : '0';
    if (i === follow) sel = b;
  });
  // Chase the scroll only when the selection actually changed, or the strip
  // fights the user's own finger every frame.
  if (sel && (scrollToSel || follow !== stripFollow)) {
    st.scrollLeft = sel.offsetLeft - st.clientWidth / 2 + sel.offsetWidth / 2;
  }
  stripFollow = follow;
}

// -- director -------------------------------------------------------------
// The replay already knows what happens next, so the camera can be there
// first. It cuts to whoever the next kill involves; between kills it sits on
// whoever has the most company, which is where a fight is about to start.
function setAutoFollow(on) {
  if (autoFollow === on) return;
  autoFollow = on;
  if (on) {
    dirLastSwitch = -1e9;
    directorTick(true);
    if (camMode !== 'follow') setCam('follow');
  }
  updateFollowStrip(true);
}
function busiestPlayer() {
  var best = -1, bs = -1;
  for (var i = 0; i < live.length; i++) {
    var a = live[i];
    if (!a || !a.alive) continue;
    var n = 0;
    for (var j = 0; j < live.length; j++) {
      var b = live[j];
      if (j === i || !b || !b.alive) continue;
      if (Math.hypot(a.x - b.x, a.z - b.z) < 30000) n++;
    }
    if (n > bs) { bs = n; best = i; }
  }
  return best;
}
function directorTick(force) {
  if (!autoFollow || !R) return;
  if (T < dirLastSwitch) dirLastSwitch = -1e9;       // the user scrubbed back
  if (!force && T - dirLastSwitch < 2.5) return;
  var ev = R.events || [], subject = -1, bestT = 1e9;
  for (var k = 0; k < ev.length; k++) {
    var e = ev[k];
    if (e.t <= T || e.t > T + 7) continue;
    if (e.type !== 'kill' && e.type !== 'stun') continue;
    if (e.t < bestT) {
      bestT = e.t;
      subject = (e.killer != null ? e.killer : e.victim);
    }
  }
  if (subject == null || subject < 0) subject = busiestPlayer();
  if (subject >= 0 && subject !== follow) {
    dirLastSwitch = T;
    follow = subject;
    if (camMode !== 'follow') setCam('follow');
    updateRoster();
    updateFollowStrip(true);
    updatePlayerCard();
  }
}

// ── playback loop ────────────────────────────────────────────────────────
var lastT = 0, lastEvIdx = 0;
var FPS = { n: 0, t0: 0, v: 0 };
function tick(now) {
  requestAnimationFrame(tick);
  // the 'so far' scoreboard follows the playhead, refreshed once a second
  if (scoreLive && R && Math.floor(T) !== scoreLiveSec && $('scorepane') && $('scorepane').dataset.open === '1') {
    scoreLiveSec = Math.floor(T);
    try { renderScore(); } catch (e) {}
  }
  var dt = Math.min(0.1, (now - lastT) / 1000 || 0);
  lastT = now;
  // frames per second over the last second, for the read-out in the corner
  FPS.n++;
  if (now - FPS.t0 >= 1000) { FPS.v = Math.round(FPS.n * 1000 / (now - FPS.t0)); FPS.n = 0; FPS.t0 = now; }

  if (R) {
    if (playing) {
      var prev = T;
      T += dt * speed;
      if (T >= R.duration) { T = R.duration; playing = false; syncPlay(); }
      spawnRings(prev, T);
      spawnHits(prev, T);
      spawnShots(prev, T);
    }
    sample(T);
    try { applySubTrack(T); } catch (errS) { if (!window.__subErr) { window.__subErr = errS; console.warn('placing track off:', errS); } }
    try { applyRagdolls(T); } catch (errR) {
      if (!window.__ragErr) { window.__ragErr = errR; console.warn('body tracks off:', errR); }
    }
    score = scoreAt(T);
    pushTrails();
    updateMarks();
    stepTracers(dt);
    // Guarded: this is new and has not been seen on screen yet, and a throw in
    // the render loop would stop playback entirely rather than just losing an
    // arc. Fail the feature, not the viewer.
    try { updateNades(); } catch (e) {
      if (!window.__nadeErr) { window.__nadeErr = e; console.warn('nade arcs off:', e); }
    }
    try { updateFx(); } catch (eF) {
      if (!window.__fxErr) { window.__fxErr = eF; console.warn('effects off:', eF); }
    }
    // Guarded: updateCarry() sits BEFORE renderer.render(), so anything it
    // throws stops the frame from drawing at all -- the app looks frozen even
    // though rAF is still running. A carry marker is not worth a dead viewer.
    try { updateRoundFlash(); } catch (err) {
      if (!carryWarned) { carryWarned = true; console.error('roundFlash', err); }
    }
    try { updateCarry(); } catch (err) {
      if (!carryWarned) { carryWarned = true; console.error('updateCarry', err); }
    }
    try { updateAimLines(); } catch (errA) {
      if (!window.__aimErr) { window.__aimErr = errA; console.warn('aim lines off:', errA); }
    }
    directorTick(false);
    updateCamera(dt);
    try { updateRoofCut(); } catch (eRf) {}
    var fi = frameIndexAt(T);
    if (fi !== feedShown) {
      try { updateResultCard(); updateFollowHud(); } catch (errRc) {
        if (!window.__resErr) { window.__resErr = errRc; console.warn('result card / follow HUD off:', errRc); }
      }
      try { updateObjHud(); } catch (errH) {
        if (!window.__objHudErr) { window.__objHudErr = errH; console.warn('objective HUD off:', errH); }
      }
      feedShown = fi;
      renderFeed(false);
      updateRoster();
      updateFollowStrip(false);
      $('clock').innerHTML = fmt(T) + '<small> / ' + fmt(R.duration) + '</small>';
    }
    drawTimeline();
  }
  updateCutaway();
  renderer.render(scene, cam);
  drawHUD();
}

// Sample a player's world position at an arbitrary time (for tracer endpoints).
function posAt(pi, t) {
  var fi = frameIndexAt(t);
  for (var d = 0; d < 40; d++) {
    for (var s = -1; s <= 1; s += 2) {
      var k = fi + d * s;
      if (k < 0 || k >= R.frames.length) continue;
      var row = R.frames[k].p || [];
      if (row[pi]) return row[pi];
    }
  }
  return null;
}

// Shot tracer: attacker -> victim. Red for a kill, blue for a stun, matching
// what the damage record already tells us (damageClass lethal / nonLethal).
// The tip of the barrel of the gun this player is holding right now, in scene
// space, or null when no gun model is in their hands. Shots are drawn from
// here instead of from a fixed point 1.1 m above the feet, so a tracer leaves
// the muzzle wherever the pose has put it.
var _mz = null;
function muzzleOf(pi) {
  var m = marks[pi], p = live[pi];
  if (!m || !m.handProp || !m.handProp.visible || !p || !isGun(p.item)) return null;
  var g = m.handProp.geometry;
  if (!g.boundingBox) g.computeBoundingBox();
  // The tip is the far end of the model along the barrel; ACROSS the barrel it is the middle
  // of the vertices that make up that end (the bore), not the model's own origin line, which
  // on most guns runs through the grip. Worked out once per gun model.
  if (!g.userData.muzzle) {
    var bb = g.boundingBox, pa = g.attributes.position, zc = bb.max.z - (bb.max.z - bb.min.z) * 0.02, sx = 0, sy = 0, nn = 0;
    for (var vi = 0; vi < pa.count; vi++) if (pa.getZ(vi) >= zc) { sx += pa.getX(vi); sy += pa.getY(vi); nn++; }
    g.userData.muzzle = nn ? [sx / nn, sy / nn, bb.max.z] : [0, 0, bb.max.z];
  }
  var mu = g.userData.muzzle;
  m.handProp.updateWorldMatrix(true, false);
  return new THREE.Vector3(mu[0], mu[1], mu[2]).applyMatrix4(m.handProp.matrixWorld);
}
function spawnTracer(ev) {
  if (ev.killer == null || ev.victim == null) return;
  var a = posAt(ev.killer, ev.t), b = posAt(ev.victim, ev.t);
  if (!a || !b) return;
  var stun = ev.type === 'stun';
  var geo = new THREE.BufferGeometry().setFromPoints([
    muzzleOf(ev.killer) ||
    new THREE.Vector3((a[0] - ctr[0]) * S, a[1] * S + 11, (a[2] - ctr[2]) * S),
    new THREE.Vector3((b[0] - ctr[0]) * S, b[1] * S + 11, (b[2] - ctr[2]) * S)]);
  var line = new THREE.Line(geo, new THREE.LineBasicMaterial({
    color: stun ? 0x4EA8DE : 0xE2574C, transparent: true, opacity: 0.95,
    depthTest: false }));
  line.renderOrder = 5;
  scene.add(line);
  tracers.push({ line: line, age: 0, life: stun ? 1.6 : 1.2 });
}

// ── grenade arcs ─────────────────────────────────────────────────────────
// An arc is drawn ONLY where both of its ends are measured: the launch point
// from the type-0x08 throwable record, and the detonation from the damage
// record's own impact point (bit-identical across co-victims of one blast).
// The fuse is a measured 60-tick / 2.000 s timer, and two endpoints plus a
// flight time pin a ballistic parabola uniquely -- so there is no fitted launch
// speed here and nothing invented. Throws that damaged nobody have no recorded
// endpoint and get no arc; see RPDT_FORMAT.md 7.9.
var NADE_G = 9810;               // mm/s^2, the same constant the fit used
var NADE_BURST_S = 1.1;
var NADE_OWN = '#5CE06A';   // the selected player's own grenades
var NADE_FRAG = 52, NADE_KILL_M = 2, NADE_EDGE_M = 5;   // measured, see the blast drawing
var nadeRings = [];
function nadeRingGet(i) {
  if (nadeRings[i]) return nadeRings[i];
  var g = new THREE.Group();
  function ring(inner, col) {
    var m = new THREE.Mesh(new THREE.RingGeometry(inner, 1, 48),
      new THREE.MeshBasicMaterial({ color: col, side: THREE.DoubleSide, transparent: true, depthWrite: false,
                                    polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }));
    m.rotation.x = -Math.PI / 2; g.add(m); return m;
  }
  var o = { g: g, shock: ring(0.9, 0xFFC46B), kill: ring(0.93, 0xFF5A3D) };
  g.visible = false; scene.add(g);
  return (nadeRings[i] = o);
}
var NADE_SEG = 48;
var NADE_POOL = 16;
var nadeArcs = null, nadeBursts = null, nadeBalls = null;
// Burst colour by what was thrown; anything unlisted gets the frag orange.
var NADE_SCALE = 2.5;
var NADE_COL = { 'SMOKE G': 0xB8BDB5, 'SMOKE G.(R)': 0xD9604F, 'SMOKE G.(G)': 0x6FBF6A,
                 'SMOKE G.(Y)': 0xE2D05A, 'SMOKE G.(P)': 0xB07CD8, 'STUN G': 0xFFFFFF,
                 'CHAFF G': 0x8FD3FF, 'EMP GRENADE': 0x6FA8FF, 'WP G': 0xFFF1C2 };
// EVERY throw gets a flight, built here from the throw events and the per-hit
// damage rows. Two kinds, and the difference is kept visible:
//   measured  the throw damaged somebody, so its detonation point is the
//             damage record's own impact point and the flight time is the gap
//             between the two records. Full-strength arc and burst.
//   estimated the throw hit nobody, so the file holds NO endpoint. The path is
//             a guess: along the thrower's aim at the corpus-median throw
//             (13.4 m/s, 2.0 s fuse), stopped at the thrower's foot height.
//             Drawn faint, with a small puff instead of a blast. ?nadeest=0
//             hides these.
var NADE_EST = !/[?&]nadeest=0/.test(location.search);
// THE THROW RECORD HOLDS THE GRENADE'S LAUNCH VELOCITY (e.vel, mm/s; the file
// stores cm/s). Measured 2026-10-05 on 247 frag throws whose blast is known
// (itembox/nade_vel.js): its direction is within 0.6 deg of the real blast
// (median), and flying it until it lands at the thrower's foot height, then
// letting it keep NADE_ROLL of its speed, ends 2.9 m from the real blast
// (median). The old guess -- the thrower's aim at a fixed 13.4 m/s -- was 7.1 m
// off. So a throw that hurt nobody now flies on its own recorded velocity.
// Still a model past the first bounce: no walls, no slopes.
// Launch and blast points sit one body offset (800 mm) low in the event frame.
var NADE_FUSE = 2.0, NADE_LIFT = 800, NADE_ROLL = 0.25;
function buildNades(r) {
  if (!r || !r.events) return;
  var out = [], dm = r.dmg || [], claimed = {};
  r.events.forEach(function (e) {
    if (e.type !== 'throw' || !e.launch || e.killer == null) return;
    var A = [e.launch[0], e.launch[1] + NADE_LIFT, e.launch[2]];
    var c = null, fi = Math.round(frameIndexAtIn(r, e.t));
    for (var k = 0; k < 12 && !c; k++) {
      var row = r.frames[Math.max(0, fi - k)];
      c = row && row.p && row.p[e.killer];
    }
    var floor = c ? c[1] : A[1] - 1400;
    var n = { t: e.t, a: A, f: NADE_FUSE, w: e.weaponName, wi: e.weapon, by: e.killer };
    var V = e.vel || null, pred = null;      // a zero velocity is a real one: it drops at the feet
    if (V) {
      var disc = V[1] * V[1] + 2 * NADE_G * (A[1] - floor);
      var tl = clamp(disc > 0 ? (V[1] + Math.sqrt(disc)) / NADE_G : 0, 0, NADE_FUSE);
      var kk = tl + NADE_ROLL * (NADE_FUSE - tl);
      n.v = V; n.tl = tl; n.floor = floor;
      pred = [A[0] + V[0] * kk, floor, A[2] + V[2] * kk];
    } else if (e.vel) {
      pred = [A[0], floor, A[2]];                 // no velocity at all: dropped at the feet
    }
    // THE BLAST: a hit by this thrower with this item, one fuse later. Among
    // candidates the one nearest the fuse time and the predicted landing wins,
    // and a blast already given to an earlier throw is not given out twice (the
    // old first-hit-in-window rule handed two quick throws the same blast).
    var hit = null, best = 1e18, hk = null;
    for (var i = 0; i < dm.length; i++) {
      var h = dm[i];
      if (h[0] < e.t + 1.2) continue;
      if (h[0] > e.t + 3.0) break;
      if (h[4] !== e.weapon || h[5] == null) continue;
      // A blast record does not name the thrower (measured: attacker unknown on
      // every frag row), so an unnamed one is matched on the fuse alone: it must
      // fall within 0.06 s of throw + 2.00 s (247 of 247 clean pairs did).
      if (h[1] !== e.killer && !(h[1] < 0 && Math.abs(h[0] - e.t - NADE_FUSE) < 0.06)) continue;
      var key = h[0] + ':' + h[5] + ':' + h[7];
      if (claimed[key]) continue;
      var sc = Math.abs(h[0] - e.t - NADE_FUSE) * 20000 +
               (pred ? Math.hypot(pred[0] - h[5], pred[2] - h[7]) : 0);
      if (sc < best) { best = sc; hit = h; hk = key; }
    }
    if (hit) {
      claimed[hk] = true;
      n.d = [hit[5], hit[6] + NADE_LIFT, hit[7]]; n.f = hit[0] - e.t;
      if (n.v) n.tl = Math.min(n.tl, n.f);
      out.push(n);
      return;
    }
    if (!NADE_EST) return;
    n.est = true;
    if (pred) { n.d = pred; out.push(n); return; }
    // a replay built before the velocity was read: the old aim-direction guess
    if (!c) return;
    var dx, dz, el = 0.75;
    if (c[9] != null && c[11] != null) {
      dx = c[9] - c[0]; dz = c[11] - c[2];
      var fl = Math.sqrt(dx * dx + dz * dz) || 1;
      el = clamp(Math.atan2((c[10] != null ? c[10] - c[1] : 0), fl) + 0.6, 0.15, 1.15);
      dx /= fl; dz /= fl;
    } else {
      var hr = (c[3] || 0) * Math.PI / 180;
      dx = Math.sin(hr); dz = Math.cos(hr);
    }
    var v = 13400, tf = 2.0, rng = v * Math.cos(el) * tf;
    var yEnd = A[1] + v * Math.sin(el) * tf - 0.5 * NADE_G * tf * tf;
    n.guess = true;
    n.d = [A[0] + dx * rng, Math.max(yEnd, c[1]), A[2] + dz * rng];
    out.push(n);
  });
  if (out.length) r.nades = out;
}
// Where grenade n is sT seconds after the throw. With a recorded velocity: free
// flight until it lands, then a slow roll; any gap between that model and the
// recorded blast point is eased in over the flight so the path always ends ON
// the blast. Without one: the parabola through launch and end point.
function nadeModel(n, sT, o) {
  var a = n.a, v = n.v;
  if (sT <= n.tl) {
    o[0] = a[0] + v[0] * sT; o[1] = a[1] + v[1] * sT - 0.5 * NADE_G * sT * sT; o[2] = a[2] + v[2] * sT;
  } else {
    var kq = n.tl + NADE_ROLL * (sT - n.tl);
    o[0] = a[0] + v[0] * kq; o[1] = a[1] + v[1] * n.tl - 0.5 * NADE_G * n.tl * n.tl; o[2] = a[2] + v[2] * kq;
  }
  return o;
}
// WALLS AND FLOORS (user, 2026-10-05: "one nade clipped a wall and exploded
// where it shouldn't have"). The grenade is flown through the map's own
// collision mesh: 1/60 s steps on the recorded launch velocity, and at each
// surface it hits it bounces, keeping NADE_BOUNCE of its speed into the surface
// and NADE_SLIDE of its speed along it. The two numbers are a physical guess,
// not read from the game. Triangles are binned in a 2 m grid so a step tests a
// handful of them. ?nadewalls=0 goes back to the no-walls flight.
var NADE_WALLS = !/[?&]nadewalls=0/.test(location.search);
// TUNED 2026-10-05 (?nadefit=1 sweeps both numbers against the recorded blasts):
// bounce 0.4 / slide 0.45 gives a median miss of 1.5 m on Yonkos (64 blasts),
// 0.8 m on Finsletown (29) and 0.7 m on stl (20); the first guess, 0.35 / 0.6,
// gave about 2.0 / 1.2 / 1.4. Silo Sunset keeps a long tail (1 in 4 over 9 m).
var NADE_BOUNCE = 0.4, NADE_SLIDE = 0.45, NADE_CELL = 20;
var nadeGrid = null, nadeGridFor = null;
function nadeGridGet() {
  if (!mapMesh) return null;
  if (nadeGridFor === mapMesh) return nadeGrid;
  var g = mapMesh.geometry, pos = g.attributes.position.array, ix = g.index ? g.index.array : null;
  var nt = ix ? ix.length / 3 : pos.length / 9, cells = new Map(), tri = new Float32Array(nt * 9);
  for (var q = 0; q < nt; q++) {
    var mnx = 1e9, mxx = -1e9, mnz = 1e9, mxz = -1e9;
    for (var c = 0; c < 3; c++) {
      var vi = (ix ? ix[q * 3 + c] : q * 3 + c) * 3;
      var x = pos[vi], y = pos[vi + 1], z = pos[vi + 2];
      tri[q * 9 + c * 3] = x; tri[q * 9 + c * 3 + 1] = y; tri[q * 9 + c * 3 + 2] = z;
      if (x < mnx) mnx = x; if (x > mxx) mxx = x; if (z < mnz) mnz = z; if (z > mxz) mxz = z;
    }
    var x0 = Math.floor(mnx / NADE_CELL), x1 = Math.floor(mxx / NADE_CELL), z0 = Math.floor(mnz / NADE_CELL), z1 = Math.floor(mxz / NADE_CELL);
    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) { x1 = x0 + 19; z1 = z0 + 19; }   // a giant triangle: its corner only
    for (var cx = x0; cx <= x1; cx++) for (var cz = z0; cz <= z1; cz++) {
      var key = cx * 65536 + cz, lst = cells.get(key);
      if (!lst) cells.set(key, lst = []);
      lst.push(q);
    }
  }
  nadeGrid = { cells: cells, tri: tri }; nadeGridFor = mapMesh;
  return nadeGrid;
}
// first surface the segment p -> p + d meets: { f: 0..1, n: unit normal } or null
function nadeHit(G, px, py, pz, dx, dy, dz) {
  var x0 = Math.floor(Math.min(px, px + dx) / NADE_CELL), x1 = Math.floor(Math.max(px, px + dx) / NADE_CELL);
  var z0 = Math.floor(Math.min(pz, pz + dz) / NADE_CELL), z1 = Math.floor(Math.max(pz, pz + dz) / NADE_CELL);
  var best = null, T9 = G.tri;
  for (var cx = x0; cx <= x1; cx++) for (var cz = z0; cz <= z1; cz++) {
    var lst = G.cells.get(cx * 65536 + cz);
    if (!lst) continue;
    for (var k = 0; k < lst.length; k++) {
      var o = lst[k] * 9;
      var e1x = T9[o + 3] - T9[o], e1y = T9[o + 4] - T9[o + 1], e1z = T9[o + 5] - T9[o + 2];
      var e2x = T9[o + 6] - T9[o], e2y = T9[o + 7] - T9[o + 1], e2z = T9[o + 8] - T9[o + 2];
      var hx = dy * e2z - dz * e2y, hy = dz * e2x - dx * e2z, hz = dx * e2y - dy * e2x;
      var det = e1x * hx + e1y * hy + e1z * hz;
      if (det > -1e-9 && det < 1e-9) continue;
      var inv = 1 / det, sx = px - T9[o], sy = py - T9[o + 1], sz = pz - T9[o + 2];
      var u = (sx * hx + sy * hy + sz * hz) * inv;
      if (u < 0 || u > 1) continue;
      var qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
      var v = (dx * qx + dy * qy + dz * qz) * inv;
      if (v < 0 || u + v > 1) continue;
      var ft = (e2x * qx + e2y * qy + e2z * qz) * inv;
      if (ft < 1e-5 || ft > 1) continue;
      if (!best || ft < best.f) {
        var nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
        var L = Math.sqrt(nx * nx + ny * ny + nz * nz) || 1;
        nx /= L; ny /= L; nz /= L;
        if (nx * dx + ny * dy + nz * dz > 0) { nx = -nx; ny = -ny; nz = -nz; }   // face the grenade
        best = { f: ft, nx: nx, ny: ny, nz: nz };
      }
    }
  }
  return best;
}
// Fly grenade n through the map once; n.path = Float32Array of points, one per
// 1/60 s, in world mm. An estimated throw takes its end point from the path.
function nadeSim(n) {
  n.simmed = true;
  var G = NADE_WALLS ? nadeGridGet() : null;
  if (!G || !n.v) return;
  var tf = n.f || 2.0, steps = Math.max(1, Math.round(tf * 60)), dt = tf / steps, gS = NADE_G * S;
  var px = (n.a[0] - ctr[0]) * S, py = n.a[1] * S, pz = (n.a[2] - ctr[2]) * S;
  var vx = n.v[0] * S, vy = n.v[1] * S, vz = n.v[2] * S;
  var path = new Float32Array((steps + 1) * 3), bounces = 0;
  function put(i) { path[i * 3] = px / S + ctr[0]; path[i * 3 + 1] = py / S; path[i * 3 + 2] = pz / S + ctr[2]; }
  put(0);
  for (var i = 1; i <= steps; i++) {
    vy -= gS * dt;
    var left = 1, guard = 0;
    while (left > 1e-4 && guard++ < 4) {
      var dx = vx * dt * left, dy = vy * dt * left, dz = vz * dt * left;
      var h = nadeHit(G, px, py, pz, dx, dy, dz);
      if (!h) { px += dx; py += dy; pz += dz; break; }
      px += dx * h.f + h.nx * 0.02; py += dy * h.f + h.ny * 0.02; pz += dz * h.f + h.nz * 0.02;
      var vn = vx * h.nx + vy * h.ny + vz * h.nz;
      var tx = vx - vn * h.nx, ty = vy - vn * h.ny, tz = vz - vn * h.nz;
      vx = tx * NADE_SLIDE - vn * NADE_BOUNCE * h.nx;
      vy = ty * NADE_SLIDE - vn * NADE_BOUNCE * h.ny;
      vz = tz * NADE_SLIDE - vn * NADE_BOUNCE * h.nz;
      left *= 1 - h.f; bounces++;
    }
    put(i);
  }
  n.path = path; n.steps = steps; n.bounces = bounces;
  if (n.est) n.d = [path[steps * 3], path[steps * 3 + 1], path[steps * 3 + 2]];
}
var nadeEndTmp = [0, 0, 0];
function nadePoint(n, sT, o) {
  var a = n.a, d = n.d, tf = n.f || 2.0;
  if (n.v && !n.simmed) nadeSim(n);
  if (n.path) {
    var fi = clamp(sT / tf, 0, 1) * n.steps, i0 = Math.min(n.steps - 1, Math.floor(fi)), fr = fi - i0, pp = n.path;
    var uu = clamp(sT / tf, 0, 1); uu *= uu;
    for (var c3 = 0; c3 < 3; c3++) {
      o[c3] = pp[i0 * 3 + c3] + (pp[i0 * 3 + 3 + c3] - pp[i0 * 3 + c3]) * fr + (d[c3] - pp[n.steps * 3 + c3]) * uu;
    }
    return o;
  }
  if (n.v) {
    var e = nadeModel(n, tf, nadeEndTmp), ex = d[0] - e[0], ey = d[1] - e[1], ez = d[2] - e[2];
    nadeModel(n, sT, o);
    var u = tf > 0 ? sT / tf : 1; u *= u;
    o[0] += ex * u; o[1] += ey * u; o[2] += ez * u;
    return o;
  }
  var vy = (d[1] - a[1] + 0.5 * NADE_G * tf * tf) / tf, fq = sT / tf;
  o[0] = a[0] + (d[0] - a[0]) * fq; o[1] = a[1] + vy * sT - 0.5 * NADE_G * sT * sT; o[2] = a[2] + (d[2] - a[2]) * fq;
  return o;
}
// ?nadefit=1: for every throw whose blast is recorded, how far the flown path
// ends from it -- with the walls, and with the old no-walls flight.
function nadeFitReport() {
  if (!R || !R.nades || !mapMesh) return null;
  var ms = R.nades.filter(function (n) { return !n.est && n.v && n.d; });
  function md(a, p) { var s = a.slice().sort(function (x, y) { return x - y; }); return s.length ? s[Math.min(s.length - 1, Math.floor(s.length * p))] : NaN; }
  function run() {
    var w = [];
    ms.forEach(function (n) {
      n.simmed = false; n.path = null; nadeSim(n);
      if (n.path) w.push(Math.hypot(n.path[n.steps * 3] - n.d[0], n.path[n.steps * 3 + 2] - n.d[2]) / 1000);
    });
    return w;
  }
  var b0 = NADE_BOUNCE, s0 = NADE_SLIDE, out = 'blasts ' + ms.length + ' | median/p75 m by bounce (rows) x slide (cols)\n      ';
  var B = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6], Sl = [0.3, 0.45, 0.6, 0.75, 0.85, 0.95];
  Sl.forEach(function (s) { out += ('s' + s + '        ').slice(0, 10); });
  B.forEach(function (b) {
    out += '\nb' + (b + '  ').slice(0, 4) + ' ';
    Sl.forEach(function (s) {
      NADE_BOUNCE = b; NADE_SLIDE = s;
      var w = run();
      out += (md(w, .5).toFixed(1) + '/' + md(w, .75).toFixed(1) + '      ').slice(0, 10);
    });
  });
  NADE_BOUNCE = b0; NADE_SLIDE = s0; run();
  return out;
}
if (/[?&]nadefit=1/.test(location.search)) var nadeFitTimer = setInterval(function () {
  var s = nadeFitReport(); if (!s) return;
  clearInterval(nadeFitTimer);
  var el = document.createElement('pre'); el.id = 'nadefit';
  el.style.cssText = 'position:fixed;left:20px;top:20px;z-index:99999;background:#000;color:#fff;font:15px monospace;padding:12px;margin:0;visibility:visible';
  el.textContent = s; document.body.appendChild(el);
}, 1500);
function frameIndexAtIn(r, t) {
  var lo = 0, hi = r.frames.length - 1;
  while (lo < hi) { var mid = (lo + hi) >> 1; if (r.frames[mid].t < t) lo = mid + 1; else hi = mid; }
  return lo;
}
function initNades() {
  if (nadeArcs) return;
  nadeArcs = []; nadeBursts = []; nadeBalls = [];
  for (var b = 0; b < NADE_POOL; b++) {
    // a little larger than life (7.5 cm radius) and lit olive, or it vanishes
    // against a dark floor at normal viewing distance
    var ball = new THREE.Mesh(new THREE.SphereGeometry(0.75, 10, 8),
      new THREE.MeshLambertMaterial({ color: 0x5d6b3a, emissive: 0x20260f }));
    ball.visible = false;
    scene.add(ball); nadeBalls.push(ball);
  }
  for (var i = 0; i < NADE_POOL; i++) {
    var g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array((NADE_SEG + 1) * 3), 3));
    var ln = new THREE.Line(g, new THREE.LineBasicMaterial({
      color: 0xE2A34C, transparent: true, opacity: 0.9, depthTest: false }));
    ln.renderOrder = 5; ln.frustumCulled = false; ln.visible = false;
    scene.add(ln); nadeArcs.push(ln);
    var bm = new THREE.Mesh(new THREE.SphereGeometry(1, 10, 8),
      new THREE.MeshBasicMaterial({ color: 0xFFC46B, transparent: true,
                                    opacity: 0.8, depthTest: false }));
    bm.renderOrder = 6; bm.visible = false;
    scene.add(bm); nadeBursts.push(bm);
  }
}
// Rebuilt from the replay clock every frame rather than spawned on an event,
// so scrubbing backwards and forwards shows the same thing at the same time.
// ---- BLASTS, SMOKE AND COOLANT AS EFFECTS ---------------------------------------------------------------------------
// User, 2026-10-10: "can we add the actual coolant and actual explosions in? ... as well as smokes. All real game effects".
// THESE ARE NOT THE GAME'S OWN EFFECTS. The game's particle files (.cpef) and their textures are not decoded yet, so this is
// a stand-in drawn here: soft sprites placed by a formula of the effect's AGE, so scrubbing and pausing show the same
// picture every time. What IS from the game: where and when (the grenade's flown path and its 2.0 s fuse), the frag's 2 m /
// 5 m sizes (the rings, which stay exactly as they were: team colour, green for the selected player's own), which
// throwable it was, and who is spraying coolant when (actions 0x32 / 0x30).
// HOW LONG A SMOKE CLOUD HANGS (the user does not know it either, 2026-10-10). MEASURED on 1,587 smoke throws in 85 matches:
// players within 30 m START coughing far above the chance rate only in the first 6 s after the cloud forms (213 against 99
// in 0-3 s, 75 against 53 in 3-6 s, at chance from 6 s on). doos's book quotes community notes of "about 9 s and 3.5 m".
// So the choking lasts ~6 s and the cloud is drawn for 9. `?fx=0` = the plain ball as before.
var FX_ON = !/[?&]fx=0\b/.test(location.search), SMOKE_S = 9, FX_POOL = 220, fxSprites = null, fxTex = null, fxUsed = 0;
function fxKind(n) {
  if (!n) return null;
  if (n.wi === NADE_FRAG) return 'frag';
  var w = String(n.w || '');
  return /SMOKE/.test(w) ? 'smoke' : /STUN/.test(w) ? 'stun' : /CHAFF/.test(w) ? 'chaff' : /WP/.test(w) ? 'wp' : null;
}
function fxRand(a) { var x = Math.sin(a * 127.1 + 311.7) * 43758.5453; return x - Math.floor(x); }
function fxInit() {
  if (fxSprites) return;
  var cv = document.createElement('canvas'); cv.width = cv.height = 64;
  var cx = cv.getContext('2d'), gr = cx.createRadialGradient(32, 32, 0, 32, 32, 32);
  gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.35, 'rgba(255,255,255,0.62)'); gr.addColorStop(0.7, 'rgba(255,255,255,0.18)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
  cx.fillStyle = gr; cx.fillRect(0, 0, 64, 64);
  fxTex = new THREE.CanvasTexture(cv);
  fxSprites = [];
  for (var i = 0; i < FX_POOL; i++) {
    var sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: fxTex, transparent: true, depthWrite: false, opacity: 0 }));
    sp.visible = false; sp.renderOrder = 6; scene.add(sp); fxSprites.push(sp);
  }
}
// one soft blob: position in scene units, radius in scene units, colour, opacity, additive (fire / flash) or not (smoke)
function fxPut(x, y, z, r, col, op, add) {
  if (fxUsed >= FX_POOL || op <= 0.004) return;
  var sp = fxSprites[fxUsed++], mt = sp.material;
  sp.position.set(x, y, z); sp.scale.set(r * 2, r * 2, 1);
  mt.color.setHex(col); mt.opacity = Math.min(1, op);
  var bl = add ? THREE.AdditiveBlending : THREE.NormalBlending;
  if (mt.blending !== bl) { mt.blending = bl; mt.needsUpdate = true; }
  sp.visible = true;
}
function updateFx() {
  if (!FX_ON || !R || !scene) { if (fxSprites) for (var q = 0; q < fxSprites.length; q++) fxSprites[q].visible = false; return; }
  fxInit(); fxUsed = 0;
  var M1 = S * 1000, i, k;
  // ---- thrown things going off ----
  for (i = 0; R.nades && i < R.nades.length; i++) {
    var n = R.nades[i], kind = fxKind(n);
    if (!kind) continue;
    var age = T - n.t - (n.f || 2.0), life = kind === 'smoke' ? SMOKE_S : kind === 'wp' ? 6 : 2.6;
    if (age < 0 || age > life) continue;
    var dd = n.d, fy = n.path ? n.path[n.steps * 3 + 1] : dd[1];
    var bx = (dd[0] - ctr[0]) * S, by = (fy + 250) * S, bz = (dd[2] - ctr[2]) * S, sd = n.t * 7.3 + i;
    if (kind === 'frag' || kind === 'wp') {
      // the flash: one white-hot blob for a tenth of a second
      if (age < 0.09) fxPut(bx, by + 0.3 * M1, bz, (0.7 + 16 * age) * M1, 0xF4E7C4, 0.9 * (1 - age / 0.09), true);
      // the fireball: blobs thrown out from the centre, bright then orange then gone inside 0.6 s, filling the 2 m zone
      for (k = 0; k < 11; k++) {
        var u = age / (0.38 + 0.25 * fxRand(sd + k)); if (u >= 1) continue;
        var az = fxRand(sd + k * 3.1) * 6.283, el = fxRand(sd + k * 5.7) * 1.3, rr = (0.25 + 1.5 * (1 - Math.pow(1 - u, 2.4)) * (0.6 + 0.6 * fxRand(sd + k * 1.9))) * M1;
        fxPut(bx + Math.cos(az) * Math.cos(el) * rr, by + Math.sin(el) * rr * 0.9, bz + Math.sin(az) * Math.cos(el) * rr,
              (0.45 + 0.9 * u) * M1, u < 0.2 ? 0xF2D9A6 : u < 0.5 ? 0xC9762F : 0x6E3319, (kind === 'wp' ? 0.85 : 0.62) * (1 - u), true);
      }
      // sparks: small bright bits on ballistic paths
      for (k = 0; k < 9; k++) {
        var st = age / 0.75; if (st >= 1) break;
        var a2 = fxRand(sd + 40 + k) * 6.283, v2 = (3 + 5 * fxRand(sd + 60 + k)) * M1, vy = (2.5 + 5 * fxRand(sd + 80 + k)) * M1;
        fxPut(bx + Math.cos(a2) * v2 * age, Math.max(by - 0.2 * M1, by + vy * age - 4.9 * M1 * age * age), bz + Math.sin(a2) * v2 * age, 0.06 * M1, 0xE8B66C, 0.8 * (1 - st), true);
      }
      // the smoke it leaves: dark puffs that swell and rise for a couple of seconds (white and longer for white phosphorus)
      for (k = 0; k < 8; k++) {
        var t3 = age - 0.08 - 0.05 * k; if (t3 <= 0) continue;
        var u3 = t3 / (life - 0.4); if (u3 >= 1) continue;
        var a3 = fxRand(sd + 100 + k) * 6.283, r3 = (0.3 + 0.9 * fxRand(sd + 120 + k)) * M1 * Math.min(1, t3 * 2.5);
        fxPut(bx + Math.cos(a3) * r3, by + (0.3 + (0.9 + 0.7 * fxRand(sd + 140 + k)) * t3) * M1, bz + Math.sin(a3) * r3,
              (0.55 + 0.75 * u3) * M1, kind === 'wp' ? 0xD6D2C6 : 0x26221F, (kind === 'wp' ? 0.5 : 0.55) * (1 - u3) * Math.min(1, t3 * 6), false);
      }
      // GRIT (user: "effects should match the games gritty aesthetic"): a low skirt of tan dust kicked out along the ground
      for (k = 0; k < 7; k++) {
        var ud = age / 1.9; if (ud >= 1) break;
        var ad = fxRand(sd + 200 + k) * 6.283, rd = (0.6 + 3.2 * (1 - Math.pow(1 - ud, 3)) * (0.5 + 0.5 * fxRand(sd + 220 + k))) * M1;
        fxPut(bx + Math.cos(ad) * rd, by - 0.05 * M1 + 0.25 * ud * M1, bz + Math.sin(ad) * rd, (0.5 + 0.8 * ud) * M1, 0x6F6656, 0.36 * (1 - ud), false);
      }
    } else if (kind === 'stun') {
      if (age < 0.5) fxPut(bx, by + 0.4 * M1, bz, (1.2 + 9 * age) * M1, 0xFFFFFF, 1 - age / 0.5, true);
      for (k = 0; k < 6; k++) { var us = age / 1.6; if (us >= 1) break; var as = fxRand(sd + k) * 6.283;
        fxPut(bx + Math.cos(as) * 0.7 * M1 * us, by + (0.3 + 0.8 * us) * M1, bz + Math.sin(as) * 0.7 * M1 * us, (0.4 + 0.6 * us) * M1, 0xD8D8D8, 0.3 * (1 - us), false); }
    } else if (kind === 'chaff') {
      for (k = 0; k < 22; k++) { var uc = age / 2.4; if (uc >= 1) break;
        var ac = fxRand(sd + k) * 6.283, ec = fxRand(sd + 30 + k) * 1.4, rc = (0.4 + 2.6 * Math.sqrt(uc) * fxRand(sd + 50 + k)) * M1, tw = 0.5 + 0.5 * Math.sin(age * 31 + k * 2.3);
        fxPut(bx + Math.cos(ac) * Math.cos(ec) * rc, by + (0.6 + Math.sin(ec) * 2.2 * Math.sqrt(uc) - 1.1 * uc * uc) * M1, bz + Math.sin(ac) * Math.cos(ec) * rc, 0.09 * M1, 0xCFE9FF, tw * (1 - uc), true); }
    } else {                                    // smoke: a cloud that builds in about two seconds and thins out at the end
      var col0 = NADE_COL[n.w] || 0xB8BDB5, col = (((col0 >> 16 & 255) * 0.62 + 40) << 16) | (((col0 >> 8 & 255) * 0.62 + 40) << 8) | ((col0 & 255) * 0.62 + 38), build = Math.min(1, age / 2.2), fade = Math.min(1, (life - age) / 3.5);
      for (k = 0; k < 16; k++) {
        var a4 = fxRand(sd + k) * 6.283 + age * 0.05 * (k % 2 ? 1 : -1), r4 = (0.3 + 2.4 * fxRand(sd + 20 + k)) * M1 * build;
        var h4 = (0.5 + 2.3 * fxRand(sd + 40 + k) * build + 0.05 * age) * M1;
        fxPut(bx + Math.cos(a4) * r4, by + h4, bz + Math.sin(a4) * r4, (0.9 + 1.0 * build) * M1, col, 0.34 * build * fade, false);
      }
    }
  }
  // ---- coolant: a cold mist blown forward from the can hand while the player is in the spray action ----
  for (i = 0; i < marks.length; i++) {
    var p = live[i], m = marks[i];
    if (!p || !p.alive || (p.act !== 0x32 && p.act !== 0x30) || !m || !m.bones || !m.rig || !m.skin || !m.skin.visible) continue;
    var hb = m.bones[m.rig.skin.hand != null ? m.rig.skin.hand : 0]; if (!hb) continue;
    m.g.updateMatrixWorld(true);
    var hp = hb.getWorldPosition(new THREE.Vector3()), fa = m.dir.rotation.y, fx_ = Math.sin(fa), fz_ = Math.cos(fa);
    for (k = 0; k < 12; k++) {
      var ph = (T * 1.9 + k / 12) % 1, d5 = (0.15 + 2.0 * ph) * M1, sw = (fxRand(i * 17 + k) - 0.5) * 0.7 * ph * M1, lift5 = (fxRand(i * 29 + k) - 0.55) * 0.5 * ph * M1;
      fxPut(hp.x + fx_ * d5 + fz_ * sw, hp.y + lift5 - 0.25 * ph * M1, hp.z + fz_ * d5 - fx_ * sw, (0.08 + 0.42 * ph) * M1, 0xC9D6DA, 0.42 * (1 - ph), false);
    }
  }
  for (var j = fxUsed; j < fxSprites.length; j++) fxSprites[j].visible = false;
}

function updateNades() {
  if (!R || !R.nades || !R.nades.length || !scene) return;
  initNades();
  var used = 0;
  for (var i = 0; i < R.nades.length && used < NADE_POOL; i++) {
    var n = R.nades[i];
    var tf = n.f || 2.0;
    var age = T - n.t;
    if (age < 0 || age > tf + NADE_BURST_S) continue;
    var a = n.a, d = n.d;
    var flight = Math.min(age, tf);
    var ln = nadeArcs[used], pos = ln.geometry.attributes.position, np = [0, 0, 0];
    for (var k = 0; k <= NADE_SEG; k++) {
      nadePoint(n, flight * (k / NADE_SEG), np);
      pos.array[k * 3] = (np[0] - ctr[0]) * S;
      pos.array[k * 3 + 1] = np[1] * S;
      pos.array[k * 3 + 2] = (np[2] - ctr[2]) * S;
    }
    pos.needsUpdate = true;
    ln.geometry.setDrawRange(0, NADE_SEG + 1);
    var strength = n.est ? 0.55 : 0.9;
    var lp = R.players[n.by]; ln.material.color.set(follow >= 0 && n.by === follow ? NADE_OWN : lp && lp.team != null ? teamColor(lp.team) : '#E2A34C');
    ln.material.opacity = age <= tf ? strength
      : strength * (1 - (age - tf) / NADE_BURST_S);
    ln.visible = true;
    // the grenade itself, at the head of the arc, until it goes off
    var ball = nadeBalls[used];
    ball.visible = age <= tf;
    if (ball.visible) {
      ball.position.set(pos.array[NADE_SEG * 3], pos.array[NADE_SEG * 3 + 1], pos.array[NADE_SEG * 3 + 2]);
      // THE REAL GRENADE, not a green ball (user, 2026-10-04: "they look like
      // little green apples"): the thrown item's own model and texture, tumbling.
      // Drawn at NADE_SCALE times life size, because a 12 cm object is a speck
      // from the orbit camera.
      if (!ball.__ball) { ball.__ball = ball.geometry; ball.__flat = ball.material; }
      var ng = centredGeo(weaponGeo(n.wi)), nm = ng ? weaponMats(ng) : null;
      if (ng && nm) {
        if (ball.geometry !== ng) { ball.geometry = ng; ball.material = nm; }
        ball.scale.setScalar(MM * NADE_SCALE);
        ball.rotation.set(age * 9 + n.t, age * 5, 0);
      } else {
        if (ball.geometry !== ball.__ball) { ball.geometry = ball.__ball; ball.material = ball.__flat; }
        ball.scale.setScalar(1);
      }
    }
    var bm = nadeBursts[used];
    if (age >= tf) {
      var bu = (age - tf) / NADE_BURST_S;          // 0..1
      var dd = n.d, frag = n.wi === NADE_FRAG, M1 = S * 1000;      // M1 = one metre
      // THE BLAST AT ITS REAL SIZE (user, 2026-10-05). Measured over 131 frag
      // hits in the hosted replays: near-full damage inside 2 m, some damage out
      // to 5 m (98% of hits are within 5.3 m). So: a fireball that fills the 2 m
      // kill zone and fades, a ring on the ground at 2 m, and a shock ring that
      // runs out to the 5 m edge. Drawn solid in the world (hidden by walls)
      // rather than through them. Other throwables keep a plain coloured puff.
      var r = frag ? (0.5 + (NADE_KILL_M - 0.5) * Math.min(1, bu * 3.3)) * M1 : (0.6 + 2.2 * bu) * M1;
      bm.material.color.setHex(frag ? (bu < 0.15 ? 0xFFF3C4 : 0xFF9A3D) : (NADE_COL[n.w] || 0xFFC46B));
      bm.material.depthTest = frag;
      bm.scale.set(r, r, r);
      // the floor under the blast: where the flown path came to rest (the blast
      // record's own height sits on the victim's body, often below the floor)
      var fy = n.path ? n.path[n.steps * 3 + 1] : dd[1];
      bm.position.set((dd[0] - ctr[0]) * S, (fy + 300) * S, (dd[2] - ctr[2]) * S);
      bm.material.opacity = (frag ? 0.85 : 0.5) * (1 - bu) * (1 - bu);
      bm.visible = !(FX_ON && fxKind(n));
      var rg = nadeRingGet(used);
      if (frag) {
        rg.g.position.set((dd[0] - ctr[0]) * S, (fy + 60) * S, (dd[2] - ctr[2]) * S);
        var sr = NADE_EDGE_M * Math.min(1, bu * 2.2) * M1;
        rg.shock.scale.set(sr, sr, sr); rg.shock.material.opacity = 0.75 * (1 - bu);
        rg.kill.scale.set(NADE_KILL_M * M1, NADE_KILL_M * M1, NADE_KILL_M * M1); rg.kill.material.opacity = 0.9 * (1 - bu);
        // the kill-zone ring wears the THROWER'S team colour (user, 2026-10-05)
        // ... and GREEN when it is the selected player's own grenade (user, same day)
        var tp = R.players[n.by]; rg.kill.material.color.set(follow >= 0 && n.by === follow ? NADE_OWN : tp && tp.team != null ? teamColor(tp.team) : '#FF5A3D');
        rg.g.visible = true;
      } else rg.g.visible = false;
    } else {
      bm.visible = false;
      if (nadeRings[used]) nadeRings[used].g.visible = false;
    }
    used++;
  }
  for (var j = used; j < NADE_POOL; j++) {
    nadeArcs[j].visible = false;
    nadeBursts[j].visible = false;
    nadeBalls[j].visible = false;
    if (nadeRings[j]) nadeRings[j].g.visible = false;
  }
}

function stepTracers(dt) {
  for (var i = tracers.length - 1; i >= 0; i--) {
    var tr = tracers[i];
    tr.age += dt;
    if (tr.age >= tr.life) {
      scene.remove(tr.line);
      tr.line.geometry.dispose();
      tracers.splice(i, 1);
      continue;
    }
    tr.line.material.opacity = 0.95 * (1 - tr.age / tr.life);
  }
}

function clearTracers() {
  tracers.forEach(function (t) { scene.remove(t.line); t.line.geometry.dispose(); });
  tracers.length = 0;
}

// Every lethal-class HIT, not just the killing one: a short amber streak from
// the shooter to the recorded impact point plus a spark where it landed. The
// impact point is the damage record's own (it sits on the victim's body); when
// the file could not place it, the victim's position stands in. Unattributed
// hits get the spark only. ?hits=0 turns this off.
// ── sound ────────────────────────────────────────────────────────────────
// Sounds are NOT part of the site: they come from a separate sound folder the
// viewer is pointed at with ?snd=<url> (remembered in this browser; ?snd=off
// forgets it). That folder holds the audio files plus index.js, which sets
// window.__MGO2_SNDMAP__ = { "<weapon name as in meta.itemNames>": "file", ... }.
// Loaded with a <script> tag and played through <audio>, so the folder can sit
// on another origin with no CORS setup.
var SND = { base: null, map: null, on: true, live: 0, ctx: null, buf: {}, shot: {} };
(function () {
  var m = /[?&]snd=([^&#]+)/.exec(location.search), b = null;
  try {
    if (m) { b = decodeURIComponent(m[1]); localStorage.setItem('mgo2snd', b); }
    else b = localStorage.getItem('mgo2snd');
  } catch (e) { if (m) b = decodeURIComponent(m[1]); }
  if (!b || b === 'off' || b === '0') {
    try { localStorage.removeItem('mgo2snd'); } catch (e2) {}
    return;
  }
  if (b.slice(-1) !== '/') b += '/';
  SND.base = b;
  var s = document.createElement('script');
  s.src = b + 'index.js';
  s.onload = function () {
    SND.map = window.__MGO2_SNDMAP__ || null;
    var btn = document.getElementById('b-snd');
    if (btn && SND.map) btn.style.display = '';
  };
  document.head.appendChild(s);
})();
function setSound(on) {
  SND.on = on;
  var btn = document.getElementById('b-snd');
  if (btn) btn.dataset.on = on ? '1' : '0';
}
// Play one sound at a world position. Loudness falls off with distance from
// the camera; in the overview modes the falloff is stretched by the zoom so a
// whole-map view is not silent.
function playSnd(name, x, y, z, when) {
  if (!SND.map || !SND.on || !name || SND.live >= 14) return;
  var f = SND.map[name];
  if (!f) return;
  var dx = (x - ctr[0]) * S - cam.position.x, dy = y * S - cam.position.y,
      dz = (z - ctr[2]) * S - cam.position.z;
  var d = Math.sqrt(dx * dx + dy * dy + dz * dz);
  var ref = camMode === 'free' ? 150 : Math.max(150, orbit.dist * 1.2);
  var vol = 0.6 / (1 + (d / ref) * (d / ref));
  if (vol < 0.02) return;
  var buf = sndBuffer(f);
  if (!buf) return;
  var ac = SND.ctx, src = ac.createBufferSource(), g = ac.createGain();
  src.buffer = buf;
  g.gain.value = vol;
  src.connect(g); g.connect(ac.destination);
  SND.live++;
  src.onended = function () { SND.live--; };
  src.start(Math.max(ac.currentTime, when || 0));
}
// Decoded sounds, played through Web Audio so a shot starts on the audio clock
// (an <audio> element per shot started tens of ms late, and unevenly). The
// pack's index.js carries each file as base64 in window.__MGO2_SNDDATA__, so
// nothing is fetched and no CORS is involved. The context can only start
// after a click or key press, which pressing Play is.
function sndBuffer(f) {
  if (!SND.ctx) {
    var AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    SND.ctx = new AC();
    SND.buf = {};
    var data = window.__MGO2_SNDDATA__ || {};
    Object.keys(data).forEach(function (k) {
      var bin = atob(data[k]), u8 = new Uint8Array(bin.length);
      for (var i = 0; i < bin.length; i++) u8[i] = bin.charCodeAt(i);
      SND.ctx.decodeAudioData(u8.buffer, function (b) { SND.buf[k] = b; }, function () {});
    });
  }
  if (SND.ctx.state === 'suspended') SND.ctx.resume();
  return SND.buf[f] || null;
}
// Time between shots of one weapon in automatic fire, measured from THIS
// replay: the median gap between consecutive shots under 0.25 s apart.
function shotCadence(item) {
  var c = R.__cad || (R.__cad = {});
  if (c[item] !== undefined) return c[item];
  var gaps = [], last = {};
  for (var k = 1; k < R.frames.length; k++) {
    var row = R.frames[k].p || [], prow = R.frames[k - 1].p || [], t = R.frames[k].t;
    for (var i = 0; i < row.length; i++) {
      var e = row[i], pe = prow[i];
      if (!e || e[12] !== item || !isShot(e, pe)) continue;
      if (last[i] != null && t - last[i] < 0.25) gaps.push(t - last[i]);
      last[i] = t;
    }
  }
  gaps.sort(function (x, y) { return x - y; });
  return (c[item] = gaps.length >= 5 ? gaps[gaps.length >> 1] : null);
}
// A SHOT is the firing action pulsing to 7 (cell 14) with a gun equipped: one
// rising edge per round. MEASURED over 5 replays: while the ammo count is
// readable, 2061 of 2064 single-round ammo drops coincide with a pulse, and
// 89% of pulses are followed by an ammo drop within 4 frames (so the pulse
// count runs up to ~10% high). The ammo cell itself CANNOT count shots: it is
// capped at 15, so the first half of a 30-round magazine never moves it --
// which is why the first sound version went silent for half of every burst.
// Guns for SHOT / ACCURACY purposes: every bullet weapon in the item pool,
// ids 2..45 minus the blades (16, 29, 36) and the coolant spray (12). This is
// deliberately NOT the FIREARM table, which only lists what is held like a
// rifle, and it includes id 2: the pool's string calls it ST KNIFE but it is
// the MK2 tranquilliser pistol (see ITEM_FIX), which fires and stuns at range.
var NOT_GUN = { 12: 1, 16: 1, 29: 1, 36: 1 };
function isGun(item) { return item >= 2 && item <= 45 && !NOT_GUN[item]; }
// CORRECTED 2026-10-08 (user: "accuracy seems broken", match 69195). The rising edge was looked for against the PREVIOUS
// FRAME's row, but frames are change-triggered: a player often has no entry in the frame before, and then the edge was
// missed. Newer recordings are far sparser, so they lost 7 shots in 8 (69195: 385 counted, 2,860 real) and several
// players read 100 % accurate; older ones lost 10 - 30 %. The edge is now taken against the player's OWN previous sample,
// marked once per replay on the sample itself. Hits per shot then come out alike on old and new files (9 - 11 %).
function markShots() {
  R.__shotMarked = true;
  var prev = {}, fr = R.frames || [];
  for (var k = 0; k < fr.length; k++) {
    var row = fr[k].p || [];
    for (var i = 0; i < row.length; i++) {
      var e = row[i];
      if (!e) continue;
      var pe = prev[i];
      // (alive only: the sample a player dies on can still carry the firing action; the weapon page never counted those)
      if (pe && e[4] && e[12] === pe[12] && e[14] === 7 && pe[14] !== 7 && isGun(e[12])) e.__shot = true;
      prev[i] = e;
    }
  }
}
function isShot(e, pe) {
  if (R && !R.__shotMarked) markShots();
  return !!(e && e.__shot);
}
// Per-player shots fired and bullets that landed, for accuracy. Hits are the
// lethal-class damage rows from a gun that the file attributes to the shooter
// (some hits carry no attacker, so accuracy reads slightly LOW; the ~10% pulse
// overcount pushes the same way).
// WHO LANDED EACH GUN HIT. The file does NOT say. A damage record carries the weapon and the DIRECTION the round came from,
// nothing more; only a kill names its killer. The parser credits a hit to whoever stands in that direction, and for one hit
// in eight it credits nobody. MEASURED 2026-10-08 on the one place the truth is known - 4,769 hits that kill, over 41
// matches, scored against the killer the game itself names (scratch loop/attr.js, attr_ds.js):
//     direction only (the parser)                          75.5 % right (85.9 % of those it answers)
//     + the candidate fired that weapon within 0.35 s      90.2 %
//     + a shooter who died in the last second still counts 95.3 %   (traded kills: he is dead when his round lands)
// So every gun hit is re-credited here with the combined rule; a hit that kills takes the game's own killer. It is still an
// inference: about one hit in twenty goes to the wrong player. 100 % is not available for a hit that does not kill.
// A gun kill with no hit record counts as one hit; a health drop with no record at all is credited the same way when an
// enemy was firing at the victim (59 of 369 drops in match 69195 have no record).
// -> { k: credited attacker per R.dmg row (-1 unknown), extra: [[t, attacker, victim]], stats }
function hitCredits() {
  if (R.__hitc) return R.__hitc;
  if (!R.__shotMarked) markShots();
  var dmg = R.dmg || [], k = dmg.map(function (h) { return h[1]; }), extra = [], fr = R.frames || [];
  var per = [], pulses = [], n = R.players.length, i, f;
  for (i = 0; i < n; i++) per.push([]);
  for (f = 0; f < fr.length; f++) {
    var row = fr[f].p || [];
    for (i = 0; i < row.length; i++) {
      if (!row[i] || !per[i]) continue;
      per[i].push([fr[f].t, row[i]]);
      if (row[i].__shot && row[i][4]) pulses.push([fr[f].t, i, row[i][12]]);
    }
  }
  // his sample at t: alive and no older than 4 s, or - if he has just died - his last living one within the second before
  function candCell(pi, t) {
    var a = per[pi], lo = 0, hi = a.length - 1;
    if (!a.length || a[0][0] > t) return null;
    while (lo < hi) { var mid = (lo + hi + 1) >> 1; if (a[mid][0] <= t) lo = mid; else hi = mid - 1; }
    if (t - a[lo][0] > 4) return null;
    if (a[lo][1][4]) return a[lo][1];
    for (var q = lo; q >= 0 && t - a[q][0] < 1.0; q--) if (a[q][1][4]) return a[q][1];
    return null;
  }
  var pfrom = 0;
  function firedAt(pi, t0, t1, w) {
    for (var q = pfrom; q < pulses.length && pulses[q][0] <= t1; q++) {
      if (pulses[q][0] >= t0 && pulses[q][1] === pi && (w == null || pulses[q][2] === w)) return true;
    }
    return false;
  }
  function credit(vi, t, tFrom, weapon, bearing, vcell) {
    var vt = R.players[vi] ? R.players[vi].team : null, best = -1, bs = Infinity;
    while (pfrom < pulses.length && pulses[pfrom][0] < tFrom - 0.4) pfrom++;
    for (var c = 0; c < n; c++) {
      if (c === vi || !R.players[c] || R.players[c].team === vt) continue;
      var cc = candCell(c, t);
      if (!cc) continue;
      var fired = firedAt(c, tFrom - 0.35, t + 0.12, weapon);
      if (bearing == null && !fired) continue;                    // nothing ties him to it
      var sc = fired ? 0 : 0.7;
      if (bearing != null && vcell) {
        var d = Math.abs(Math.atan2(cc[0] - vcell[0], cc[2] - vcell[2]) + Math.PI - bearing) % (2 * Math.PI);
        sc += d > Math.PI ? 2 * Math.PI - d : d;
      } else sc += 0.6;
      if (cc[9] != null && vcell) {
        var dx = cc[9] - cc[0], dz = cc[11] - cc[2], L = Math.sqrt(dx * dx + dz * dz) || 1, px = vcell[0] - cc[0], pz = vcell[2] - cc[2];
        sc += 0.15 * (px * dx + pz * dz < 0 ? 1 : Math.min(Math.abs(px * dz - pz * dx) / L, 6000) / 6000);
      } else sc += 0.15;
      if (sc < bs) { bs = sc; best = c; }
    }
    return best;
  }
  var kills = (R.events || []).filter(function (e) { return e.type === 'kill' && e.killer != null && !e.suicide && e.killer !== e.victim; });
  var st = { rows: 0, byKill: 0, byRule: 0, changed: 0, unknown: 0 };
  dmg.forEach(function (h, hi) {
    if (!isGun(h[4])) return;
    st.rows++;
    for (var q = 0; q < kills.length; q++) {
      if (kills[q].victim === h[2] && kills[q].weapon === h[4] && Math.abs(kills[q].t - h[0]) < 0.25) {
        if (k[hi] !== kills[q].killer) st.changed++;
        k[hi] = kills[q].killer; st.byKill++; return;
      }
    }
    var vc = candCell(h[2], h[0]) || insCell(R, h[2], h[0]);
    var b = credit(h[2], h[0], h[0], h[4], h[9] == null ? null : h[9], vc);
    if (b >= 0) { if (k[hi] !== b) st.changed++; k[hi] = b; st.byRule++; } else if (k[hi] < 0) st.unknown++;
  });
  kills.forEach(function (e) {
    if (!isGun(e.weapon)) return;
    for (var q = 0; q < dmg.length; q++) { if (dmg[q][2] === e.victim && Math.abs(dmg[q][0] - e.t) < 0.3) return; }
    extra.push([e.t, e.killer, e.victim, e.weapon]);
  });
  // health drops that have no damage record, stun or kill beside them
  var prevC = {}, nDrop = 0, nCred = 0, di = 0, evs = (R.events || []);
  pfrom = 0;
  for (var f2 = 0; f2 < fr.length; f2++) {
    var row2 = fr[f2].p || [], t2 = fr[f2].t;
    for (var j = 0; j < row2.length; j++) {
      var c2 = row2[j];
      if (!c2) continue;
      var pc = prevC[j];
      prevC[j] = [t2, c2];
      if (!pc || !pc[1][4] || pc[1][5] == null || c2[5] == null) continue;
      if (pc[1][5] - (c2[4] ? c2[5] : 0) <= 0.015) continue;
      var tA = pc[0] - 0.25, tB = t2 + 0.25, logged = false;
      while (di < dmg.length && dmg[di][0] < tA - 5) di++;
      for (var q3 = di; q3 < dmg.length && dmg[q3][0] <= tB; q3++) { if (dmg[q3][2] === j && dmg[q3][0] >= tA) { logged = true; break; } }
      if (logged) continue;
      if (evs.some(function (e) { return (e.type === 'stun' || e.type === 'kill') && e.victim === j && e.t >= tA && e.t <= tB; })) continue;
      if (extra.some(function (x) { return x[2] === j && x[0] >= tA && x[0] <= tB; })) continue;
      nDrop++;
      var b2 = credit(j, t2, pc[0], null, null, pc[1]);
      if (b2 >= 0) { var bc2 = candCell(b2, t2); extra.push([t2, b2, j, bc2 ? bc2[12] : null]); nCred++; }
    }
  }
  st.unloggedDrops = nDrop; st.unloggedCredited = nCred; st.extra = extra.length;
  return (R.__hitc = { k: k, extra: extra, stats: st });
}
function shotStats(lim) {
  if (lim == null) lim = Infinity;
  if (lim !== Infinity && R.__shotsLim && R.__shotsLim.lim === lim) return R.__shotsLim;
  if (lim === Infinity && R.__shots) return R.__shots;
  var n = R.players.length, shots = new Array(n).fill(0), hits = new Array(n).fill(0);
  for (var k = 1; k < R.frames.length; k++) {
    if (R.frames[k].t > lim) break;
    var row = R.frames[k].p || [], prow = R.frames[k - 1].p || [];
    for (var i = 0; i < n; i++) if (isShot(row[i], prow[i])) shots[i]++;
  }
  var hc_ = hitCredits();
  (R.dmg || []).forEach(function (h, hi) {
    var hk = hc_.k[hi];
    if (h[0] <= lim && hk >= 0 && hk !== h[2] && isGun(h[4]) && hits[hk] != null) hits[hk]++;
  });
  hc_.extra.forEach(function (x) { if (x[0] <= lim && hits[x[1]] != null) hits[x[1]]++; });
  // A tranquilliser round that lands is a hit too. Non-lethal hits are not in
  // R.dmg (lethal class only); the parser emits each one as a 'stun' event.
  (R.events || []).forEach(function (e) {
    if (e.type === 'stun' && e.t <= lim && e.killer != null && e.killer !== e.victim &&
        isGun(e.weapon) && hits[e.killer] != null) hits[e.killer]++;
  });
  if (lim !== Infinity) return (R.__shotsLim = { lim: lim, shots: shots, hits: hits });
  return (R.__shots = { shots: shots, hits: hits });
}
function spawnShots(a, b) {
  if (!SND.map || !SND.on || !R || b - a > 2) return;
  var k0 = Math.floor(frameIndexAt(a)) + 1, k1 = Math.floor(frameIndexAt(b));
  for (var k = Math.max(1, k0); k <= k1 && k < R.frames.length; k++) {
    var row = R.frames[k].p || [], prow = R.frames[k - 1].p || [];
    for (var i = 0; i < row.length; i++) {
      var e = row[i], pe = prow[i];
      if (!isShot(e, pe)) continue;
      // The replay only knows a shot to the nearest 1/30 s frame, so automatic
      // fire arrives as uneven 0.066 / 0.1 / 0.133 s gaps. Inside a burst the
      // shot is therefore placed one measured cadence after the previous one
      // on the audio clock; a shot that starts a burst plays immediately.
      var when = 0, st = SND.shot[i], ft = R.frames[k].t;
      if (SND.ctx) {
        var now = SND.ctx.currentTime, cad = shotCadence(e[12]);
        when = now;
        if (st && st.item === e[12] && cad && ft - st.t < cad * 1.8 && ft > st.t) {
          when = st.when + cad / (speed || 1);
          if (when < now || when > now + 0.2) when = now;
        }
      }
      SND.shot[i] = { item: e[12], t: ft, when: when };
      playSnd(itemName(e[12]), e[0], e[1], e[2], when);
    }
  }
}

var HITS_ON = !/[?&]hits=0/.test(location.search);
function spawnHits(a, b) {
  var dm = R && R.dmg;
  if (!dm || !HITS_ON || b - a > 2) return;        // a seek, not playback
  for (var i = 0; i < dm.length; i++) {
    var h = dm[i];
    if (h[0] <= a) continue;
    if (h[0] > b) break;
    var end;
    if (h[5] != null) end = new THREE.Vector3((h[5] - ctr[0]) * S, h[6] * S, (h[7] - ctr[2]) * S);
    else {
      var vp = posAt(h[2], h[0]);
      if (!vp) continue;
      end = new THREE.Vector3((vp[0] - ctr[0]) * S, vp[1] * S + 11, (vp[2] - ctr[2]) * S);
    }
    var pg = new THREE.BufferGeometry().setFromPoints([end]);
    var pt = new THREE.Points(pg, new THREE.PointsMaterial({
      color: 0xFFC14D, size: 7, sizeAttenuation: false, transparent: true, depthTest: false }));
    pt.renderOrder = 6;
    scene.add(pt);
    tracers.push({ line: pt, age: 0, life: 0.45 });
    var ap = h[1] >= 0 && h[1] !== h[2] ? posAt(h[1], h[0]) : null;
    if (!ap) continue;
    var lg = new THREE.BufferGeometry().setFromPoints([
      muzzleOf(h[1]) ||
      new THREE.Vector3((ap[0] - ctr[0]) * S, ap[1] * S + 13, (ap[2] - ctr[2]) * S), end]);
    var ln = new THREE.Line(lg, new THREE.LineBasicMaterial({
      color: 0xFFC14D, transparent: true, opacity: 0.6, depthTest: false }));
    ln.renderOrder = 5;
    scene.add(ln);
    tracers.push({ line: ln, age: 0, life: 0.3 });
  }
}

function spawnRings(a, b) {
  var ev = R.events || [];
  for (var i = 0; i < ev.length; i++) {
    var ty = ev[i].type;
    if (ty !== 'kill' && ty !== 'stun') continue;
    if (ev[i].t > a && ev[i].t <= b) {
      spawnTracer(ev[i]);
      if (ty === 'kill') {
        var p = live[ev[i].victim];
        if (p) rings.push({ x: p.x, y: p.y, z: p.z, age: 0 });
      }
      renderFeed(true);
    }
  }
}

function updateMarks() {
  for (var i = 0; i < marks.length; i++) {
    var m = marks[i], p = live[i];
    if (!p) { m.g.visible = false; continue; }
    m.g.visible = true;
    if (m.rig) useRig(m.rig);          // SKIN / ANIM are per sex; point at this player's
    var pi_ = i;
    // A prone player is reported 400 world units BELOW the floor -- that
    // offset IS how the format encodes prone. Now that the body is actually
    // drawn lying down, putting it back on the floor is correct; leaving the
    // offset in would bury it.
    var gy_ = p.y;
    if (SKIN && ANIM && pi_ != null && p.alive &&
        (proneAt(pi_, frameIndexAt(T)) || crouchAt(pi_, frameIndexAt(T)))) {
      gy_ += 400;
    }
    m.g.position.set((p.x - ctr[0]) * S, gy_ * S, (p.z - ctr[2]) * S);
    // MEASURED, not assumed. The replay's heading is atan2(dx, dz): over
    // 188 000 running samples across two maps, heading minus direction of
    // travel has a circular mean of 0.2-0.4 deg (R = 0.92). The .mdn body
    // faces +Z (the toe bones sit at rest Z +100 against ankles at 0), and
    // three.js maps local +Z to (sin t, cos t) for rotation.y = t -- so the
    // rotation IS the heading. The previous negation mirrored every body:
    // measured against travel, -h is 91.3 deg wrong on average and +h is 14.5
    // (median 6.9, the rest being real strafing and turning).
    if (p.h != null) m.dir.rotation.y = p.h;
    applyPose(m, p, i);
    clipBlend(m, p);
    limpFall(m, p);
    // A BODY IN THE AIR (the ragdoll rows' own height, see buildInsights): lifted to it, and tipped over while it is high.
    // The height is recorded; the tumble is not - the tip is only there so a thrown body does not fly flat as a board.
    if (p.alive && p.ragH != null && p.ragH > 260 && !/^blast\d/.test(m.lastClip || '')) {       // a recorded blast clip has its own rise
      var airK = Math.min(1, (p.ragH - 260) / 900);
      m.g.position.y += (p.ragH - 260) * S;
      m.dir.rotation.x = -airK * 0.9 * ((i & 1) ? 1 : -1);
    }
    var a = p.alive ? 1 : 0.22;
    // TEAM SNEAKING: the sneaking side is cloaked, so it is drawn faded -- and
    // SOLID for the five seconds after contact breaks its stealth (the rule is
    // the user's; the timer is the file's). Inside the drum can the body is
    // not drawn at all: the can is (see updateCarry).
    // ...but NOT while he is in the cardboard box: stealth only works while it is the equipped item, and the box takes its
    // place (user, 2026-10-09), so a sneaker in a box is plainly visible.
    if (p.alive && sneakCloaked(i, T) && !inBox(i, T)) a = 0.28;
    if (m.dir) m.dir.visible = !(p.alive && gearAt(i, T) === 10 && PROPS && PROPS.drum) && !(i === follow && camEyes());
    m.g.traverse(function (o) {
      var ms = o.material;
      if (!ms) return;
      if (!Array.isArray(ms)) ms = [ms];
      for (var mi_ = 0; mi_ < ms.length; mi_++) {
        if (ms[mi_].opacity === undefined) continue;
        ms[mi_].opacity = a;
        ms[mi_].transparent = a < 1;
      }
    });
    // the traverse above touched the rings too; restore their own opacities
    m.disc.material.opacity = p.alive ? 0.16 : 0.06;
    m.stTrk.material.opacity = p.alive ? 0.16 : 0.06;
    m.hpArc.material.opacity = p.alive ? 0.95 : 0.35;
    m.stArc.material.opacity = p.alive ? 0.90 : 0.30;
    m.stalk.material.opacity = p.alive ? 0.30 : 0.10;
    m.stalk.visible = NAMES_ON;
    m.disc.visible = m.hpArc.visible = m.stTrk.visible = m.stArc.visible = NAMES_ON;
    setArc(m.hpArc, p.alive ? p.hp : 0, hpColor(p.hp));
    m.disc.visible = NAMES_ON && p.hp != null;
    var hasSt = p.st != null;
    m.stTrk.visible = NAMES_ON && hasSt;
    setArc(m.stArc, hasSt && p.alive ? p.st : (hasSt ? 0 : null), 0x4EA8DE);
    if (!NAMES_ON) { m.hpArc.visible = m.stArc.visible = false; }   // setArc re-shows them
    // Never shrink below life size: the figure is modelled at true scale, so
    // 1.0 is correct up close and we only grow it to stay visible far out.
    var sc = (camMode === 'free' ? 1 : clamp(orbit.dist / 260, 1, 7)) * (i === follow ? 1.25 : 1);
    m.g.scale.setScalar(sc);
  }
  // Live proof that the pose system is running, and what it chose. Throttled
  // to ~2 Hz: this is a readout, not a per-frame cost worth paying.
  if (SKIN && ANIM && DIAG && (++diagTick % 30) === 0) {
    var used = {}, posed = 0;
    for (var q = 0; q < marks.length; q++) {
      var c = marks[q].lastClip;
      if (!c) continue;
      posed++;
      used[c] = (used[c] || 0) + 1;
    }
    var parts = [];
    for (var k in used) parts.push(k + ' x' + used[k]);
    var groups = {};
    for (q = 0; q < live.length; q++) {
      var mg = live[q] && live[q].mg;
      if (mg) groups[mg] = (groups[mg] || 0) + 1;
    }
    var gl = [];
    for (k in groups) gl.push(k + ' x' + groups[k]);
    var wp = {}, wpBox = 0;
    for (var wi = 0; wi < live.length; wi++) {
      var lp = live[wi];
      if (!lp || !lp.alive || lp.item == null) continue;
      var wc = (WEAPON_OF && WEAPON_OF[String(lp.item)]) || null;
      if (wc && WEAPONS && WEAPONS[wc]) wp[wc] = (wp[wc] || 0) + 1;
      else if (propKind(lp.item)) wpBox++;
    }
    var wl = [];
    for (var wk in wp) wl.push(wk + ' x' + wp[wk]);
    var aimErr = '';
    if (WAIM_ERR.length) {
      var se = WAIM_ERR.slice().sort(function (a, b) { return a - b; });
      aimErr = '  aim err med ' + se[se.length >> 1].toFixed(1) + ' deg (n' +
               se.length + ')';
      WAIM_ERR.length = 0;
    }
    animDiag(FPS.v + ' fps' + (HD.on && HD.mesh ? '  (HD scenery)' : '') + '\n' +
             'SKIN ON  posing ' + posed + '/' + marks.length + ' players' +
             (FORCE_CLIP ? '   [FORCED: ' + FORCE_CLIP + ']' : '') + '\n' +
             'weapons: ' + (WEAPONS ? Object.keys(WEAPONS).length + ' models' : 'not loaded') +
             '  drawn: ' + (wl.join(', ') || 'none') +
             (wpBox ? '  |  ' + wpBox + ' on the placeholder box' : '') + '\n' +
             'clips in use: ' + (parts.join(', ') || 'none') + '\n' +
             'motionGroup this frame: ' + (gl.join(', ') || 'not decoded') +
             // where the selected player stands, in the game's own coordinates:
             // the quickest way to point at a wrong door or prop in the HD map
             (follow >= 0 && live[follow]
               ? '\nselected player at  x ' + Math.round(live[follow].x) + '  y ' +
                 Math.round(live[follow].y) + '  z ' + Math.round(live[follow].z)
               : '') +
             (Object.keys(used).length === 1 && ANIM.clips.walk === undefined
               ? '\nno walk/run clip captured yet - all groups fall back'
               : ''));
  }
}

// A value of null hides the arc entirely — an undecoded field must not read
// as an empty bar, which would look like "this player has no health".
function setArc(mesh, v, col) {
  if (v == null) { mesh.visible = false; return; }
  var k = Math.round(clamp(v, 0, 1) * RING_SEG);
  mesh.visible = k > 0;
  mesh.geometry.setDrawRange(0, k * 6);
  if (col != null) mesh.material.color.setHex(col);
}

function hpColor(v) {
  if (v == null) return 0x9BD17A;
  return v > 0.6 ? 0x9BD17A : v > 0.25 ? 0xE8B33C : 0xE2574C;
}

// ---- POSE ----------------------------------------------------------------
// Driven only by fields with a measured basis:
//   STUNNED  motionState 27 -- 100% of its records have stamina exactly 0
//            (845 samples), or stamina reads 0 directly. Body goes limp.
//   DEAD     alive == false (stateFlags bit1, P=1.000 over 431,340 samples).
//   AIMING   action in {4,6,7} = between shots / firing [CONFIRMED codes].
//   POSTURE  [PROBABLE] from measured median speed per motionState:
//            ~4.5 m/s run, ~1.8 walk, ~0.44 crouch, ~0.29 prone-crawl.
// motionState -> pose. The names come from the decoder's own table, which is
// 1.000-pure per state over 8 files; the values here are that table, not
// inferred from speed.
//
//   6  downed, lying on the ground     26 dead            27 incapacitated
//   17 climb up   10/11 wall or ledge hold   19 climb down over a ledge
//   5  stationary aim / scoped         24 explosion knockback
//
// CORRECTION: an earlier build drove prone from stateFlags bit 0x80 after a
// player reported crawling a vent with that bit set 98.2% of the window. The
// bit is real but it is NOT prone -- P(0x80) is 1.000 for "climb up" and for
// "stationary aim / scoped", and 41.8% of all bit-set frames are plain
// "run forward", while "downed, lying on the ground" mostly has it CLEAR.
// That build drew running and climbing players flat on the ground. The bit
// marks an alternate locomotion family (which a vent crawl belongs to), not a
// posture.
//
// The format does NOT transmit voluntary crouch or prone at all -- there is no
// such state in the table -- so the viewer no longer pretends to show one. The
// previous MS_CROUCH = {24, 10, 11} was "explosion knockback" and two
// wall-hold states.
var MS_DOWN = { 6: 1, 26: 1, 27: 1 };
var MS_CLIMB = { 10: 1, 11: 1, 17: 1, 19: 1 };
var MS_AIM = { 5: 1 };
// THE ACTION BYTE, surveyed 2026-10-08 over 32 matches (scratch loop/acts.js). With a gun: 2 raise -> 4 aimed -> 7 / 6 firing
// (they alternate every sample; a hit lands during a third of them) -> 8 lower. 3 -> 5 is a second, rarer raise-and-aim pair
// that leads into the same 4 / 6 (738 episodes; it was not treated as aiming before). 0x0B reload (ammo goes UP in 16 %),
// 0x14 wind-up -> 0x15 held -> 0x16 throw for grenades, 0x17 -> 0x19 / 0x18 CQC, 0x1B with the shield, 0x30 -> 0x32 coolant spray.
// 0x25 (running, grenade or empty hands, 4.5 m/s) is NOT the roll (32 of 1,613 roll gaps) and is still unknown; 9 and 10 too.
// THE GUN IS UP FROM ACTION 2 (user, 2026-10-10: "the gun is shooting while it's being brought up"). The game's own sequence
// (doos's table: 2 = gun raised / ready, about 0.1 s; 4 = FIRST SHOT of a trigger pull; 6 = following shot; 7 = between shots;
// 5 = single-shot recover): the first shot comes a tenth of a second after the raise. The aim pose used to start only at 4,
// so the flash of that first shot fired out of a gun still swinging up. 2 and 3 now count as aiming for the POSE.
var FIRING = { 2: 1, 3: 1, 4: 1, 5: 1, 6: 1, 7: 1 };

function poseOf(p) {
  if (!p) return 'stand';
  if (!p.alive) return 'down';
  if (p.st != null && p.st <= 0.001) return 'down';
  if (p.ms != null && MS_DOWN[p.ms]) return 'down';
  if (p.ms != null && MS_CLIMB[p.ms]) return 'climb';
  return 'stand';
}

// Is player `pi` mid-roll at frame `fi`? Returns progress 0..1, or -1.
function rollProgress(pi, fi) {
  if (!R || !R.rollByPlayer) return -1;
  var sp = R.rollByPlayer[pi];
  if (!sp) return -1;
  for (var k = 0; k < sp.length; k++) {
    if (fi < sp[k][0] - 1) break;
    if (fi >= sp[k][0] - 1 && fi <= sp[k][1] + 1) {
      var span = sp[k][1] - sp[k][0];
      return span > 0 ? Math.min(1, Math.max(0, (fi - sp[k][0]) / span)) : 0;
    }
  }
  return -1;
}

// ON A LADDER (user, 2026-10-10, Urban Ultimatum Team Sneaking: a player "climbs the ladder and just disappears"). In that
// file the game sends NO position record at all while a player is on a ladder: his samples stop at the foot and resume
// at the top 4 - 10 s later, 4 - 12 m higher and about 1.5 m along. A missing player is hidden after 0.75 s, so he
// vanished for the whole climb. A ladder climb = a gap of 1.5 - 16 s between two living samples that are under 3 m apart
// on the ground and at least 1.5 m apart in height. -> r.ladderBy[player] = [[frame a, frame b, 1 up / 0 down]]
function buildLadders(r) {
  var out = r.ladderBy = [], fr = r.frames || [];
  for (var i = 0; i < (r.players || []).length; i++) {
    var a = out[i] = [], pk = -1, pe = null;
    for (var k = 0; k < fr.length; k++) {
      var e = fr[k].p && fr[k].p[i];
      if (!e || e[0] == null) continue;
      if (pe && pe[4] && e[4]) {
        var dt = fr[k].t - fr[pk].t, dy = e[1] - pe[1];
        if (dt >= 1.5 && dt <= 16 && Math.abs(dy) >= 1500 && Math.hypot(e[0] - pe[0], e[2] - pe[2]) < 3000) a.push([pk, k, dy > 0 ? 1 : 0]);
      }
      pk = k; pe = e;
    }
  }
}
function ladderSpan(pi, fi) {
  var sp = R && R.ladderBy && R.ladderBy[pi];
  if (!sp) return null;
  for (var k = 0; k < sp.length; k++) { if (fi < sp[k][0]) break; if (fi <= sp[k][1]) return sp[k]; }
  return null;
}
function inRollSpan(pi, fi) {
  var sp = R && R.rollByPlayer && R.rollByPlayer[pi];
  if (!sp) return false;
  for (var k = 0; k < sp.length; k++) { if (fi < sp[k][0] - 1) break; if (fi <= sp[k][1] + 1) return true; }
  return false;
}
// A SIDE ROLL (the game's roll records, state 3) and which way it goes: 'sideRollL' / 'sideRollR', or null for a forward roll.
// Left or right = which side of the way he faced at the start the roll carried him.
function sideRollAt(pi, fi) {
  var sp = R && R.rollByPlayer && R.rollByPlayer[pi];
  if (!sp) return null;
  for (var k = 0; k < sp.length; k++) {
    if (fi < sp[k][0] - 1) break;
    if (fi <= sp[k][1] + 1 && sp[k][2] === 3) {
      var a = R.frames[sp[k][0]].p[pi], b = R.frames[sp[k][1]].p[pi];
      if (!a || !b || a[3] == null) return 'sideRollL';
      var h = a[3] * headScale, dx = b[0] - a[0], dz = b[2] - a[2];
      return (Math.cos(h) * dx - Math.sin(h) * dz) >= 0 ? 'sideRollR' : 'sideRollL';
    }
  }
  return null;
}

// ---- THE FALL WHEN A PLAYER DIES ------------------------------------------
// MEASURED 2026-10-09 (replaydiff/deathmap.py, 216 deaths over 6 matches played back in the real game): the game plays NO
// death animation. It lets go of the skeleton: no clip fits the 2 s after a death (best fit 129 mm median, against 5-40 mm
// for real animation states), a body that was upright drops its hips ~80 cm and is still after ~1.5 s, and a body that was
// already on the ground hardly moves. Where the body SLIDES is already in the replay (the ragdoll records move it). What was
// wrong here is the pose: the lying clip was switched on in one frame. Now the body sags from the exact pose it died in
// into the lying one over LIMP_S, fast at first and then settling. It is a blend, not physics: the real ragdoll lands
// differently every time, even on two playbacks of the same replay.
// ---- EASING FROM ONE CLIP INTO THE NEXT -------------------------------------
// Every change of clip used to be a cut: the body snapped from its last running pose to the standing one inside one frame.
// MEASURED 2026-10-10 on skeletons recorded off the real game (rp69262 + rp69252, 105 000 frames; pose speed = how fast the
// joints move in the body's own frame): the game does NOT cut. Coming to a stop (37 cases) the pose keeps moving at about
// half its running rate for ~0.3 s and is still ~0.45 s after the feet stopped; setting off (59 cases) it is at full
// running rate within ~0.15 s. Whether that is a blend or short stop / start clips is not separated - either way the right
// picture is a quick ease out of a still pose and a slower one into it. So: BLEND_GO into a moving clip, BLEND_STILL into
// a still one, from the pose that was ON SCREEN (aim twist included) into whatever the new clip plays.
// A jump along the timeline never blends (the time step is not a playback step). `?blend=0` = the old cuts.
var BLEND_GO = 0.12, BLEND_STILL = 0.35, _bq0 = null, _bq1 = null;
var BLEND_ON = !/[?&]blend=0\b/.test(location.search);
function clipBlend(m, p) {
  if (!m) return;
  if (!BLEND_ON || !m.bones || !m.skin || !m.skin.visible || !p || !p.alive) { m.__bT = null; m.__bs = null; return; }
  var nb = m.bones.length, b, bn, clip = m.lastClip;
  if (!m.__bq || m.__bq.length !== nb * 4) {
    m.__bq = new Float32Array(nb * 4); m.__bp = new Float32Array(nb * 3);
    m.__fq = new Float32Array(nb * 4); m.__fp = new Float32Array(nb * 3); m.__bT = null; m.__bs = null;
  }
  // NO BLEND ACROSS A HEADING FLIP: the body turns over in one sample (see headingFlip); easing the limbs through it while the
  // root has already swung round smeared the pose for a third of a second.
  if (p.h != null && m.__bh != null && Math.abs(((p.h - m.__bh) % (2 * Math.PI) + 3 * Math.PI) % (2 * Math.PI) - Math.PI) > 2.0) { m.__bT = null; m.__bs = null; }
  m.__bh = p.h;
  var same = (m.__bT != null && T === m.__bT);                                   // paused: hold the blend where it is
  var cont = same || (m.__bT != null && T > m.__bT && T - m.__bT < 0.25);
  if (!cont) m.__bs = null;
  else if (!same && clip !== m.__bc) {
    m.__fq.set(m.__bq); m.__fp.set(m.__bp); m.__fy = m.__by; m.__bs = m.__bT;
    var c_ = ANIM && ANIM.clips && ANIM.clips[clip];
    m.__bd = (c_ && c_.speed > 0.2) ? BLEND_GO : BLEND_STILL;
    // BRINGING THE GUN UP IS QUICK (user, 2026-10-10: a player who crouched and fired for 0.8 s before dying "doesn't look
    // like he gets his gun up"): the 0.35 s ease into a still clip ate half of it. Into an aim / fire clip it is 0.12 s.
    if (/aim|fire|^wa_/i.test(clip || '')) m.__bd = Math.min(m.__bd, 0.08);
  }
  if (m.__bs != null) {
    var u = (T - m.__bs) / m.__bd;
    if (u >= 1 || u < 0) m.__bs = null;
    else {
      var k = u * u * (3 - 2 * u);
      if (!_bq0) { _bq0 = new THREE.Quaternion(); _bq1 = new THREE.Quaternion(); }
      for (b = 0; b < nb; b++) {
        bn = m.bones[b];
        _bq0.set(m.__fq[b * 4], m.__fq[b * 4 + 1], m.__fq[b * 4 + 2], m.__fq[b * 4 + 3]);
        _bq1.copy(bn.quaternion);
        bn.quaternion.copy(_bq0).slerp(_bq1, k);
        bn.position.set(m.__fp[b * 3] + (bn.position.x - m.__fp[b * 3]) * k,
                        m.__fp[b * 3 + 1] + (bn.position.y - m.__fp[b * 3 + 1]) * k,
                        m.__fp[b * 3 + 2] + (bn.position.z - m.__fp[b * 3 + 2]) * k);
      }
      if (m.__fy != null) m.skin.position.y = m.__fy + (m.skin.position.y - m.__fy) * k;
    }
  }
  if (!same) {
    for (b = 0; b < nb; b++) {
      bn = m.bones[b];
      m.__bq[b * 4] = bn.quaternion.x; m.__bq[b * 4 + 1] = bn.quaternion.y; m.__bq[b * 4 + 2] = bn.quaternion.z; m.__bq[b * 4 + 3] = bn.quaternion.w;
      m.__bp[b * 3] = bn.position.x; m.__bp[b * 3 + 1] = bn.position.y; m.__bp[b * 3 + 2] = bn.position.z;
    }
    m.__by = m.skin.position.y; m.__bT = T; m.__bc = clip;
  }
}

var LIMP_S = 1.5, LIMP_RAG = 0.3, _lq0 = null, _lq1 = null;
var RAG_ON = !/[?&]rag=0/.test(location.search);
var PHYS_ON = /[?&]phys=1/.test(location.search);
// When did this dead player die: the last kill event naming him at or before the playhead (0.5 s of slack: the alive flag and
// the kill record are not on the same tick). null = no kill on record (fell, drowned ...): the lying clip is used then.
// WHICH RECORDED FALL (2026-10-10, user: "theres gotta be some measurement of x position vs y momentum vs z whatever hits /
// kills you"). MEASURED on 1,548 deaths recorded off the real game (replaydiff/ragstudy.py): a body shot while standing still
// goes AWAY FROM THE KILLER - median 29 deg off that line, 0.70 m - whichever way it was facing; a running body goes along ITS
// OWN MOVEMENT instead (36 deg, against 63 deg from the killer's line) and slides further (1.1 m at 3+ m/s, 1.6 m at 4+).
// So the push wanted = 0.70 m away from the killer + his own velocity x 0.22 s; it is turned into the victim's own frame and
// the recorded fall that goes nearest that way, about that far, is played (the two nearest alternate so twins differ).
// No killer on record -> the fall along his movement, or a fixed pick when he was standing.
function ragPick(pi, td, rig) {
  var phys_ = PHYS_ON && rig.physMeta && rig.physMeta.length;
  var key = pi + ':' + td + ':' + (rig === RIGS.female ? 'f' : 'm') + (phys_ ? 'p' : ''), memo = R.__rp || (R.__rp = {});
  if (memo[key]) return memo[key];
  var meta = phys_ ? rig.physMeta : (rig.ragMeta || []), fallback = 'rag' + ((pi * 7 + Math.floor(td * 10)) % rig.rag);
  var cv = insCell(R, pi, td), c0 = insCell(R, pi, td - 0.4), wx = 0, wz = 0, k = null;
  (R.events || []).forEach(function (e) { if (e.type === 'kill' && e.victim === pi && Math.abs(e.t - td) < 0.01) k = e; });
  if (cv && c0 && c0 !== cv) { wx += (cv[0] - c0[0]) / 0.4 * 0.22; wz += (cv[2] - c0[2]) / 0.4 * 0.22; }
  var ck = (k && k.killer != null && k.killer !== pi) ? insCell(R, k.killer, td) : null;
  if (cv && ck) { var ax = cv[0] - ck[0], az = cv[2] - ck[2], al = Math.sqrt(ax * ax + az * az); if (al > 1) { wx += ax / al * 700; wz += az / al * 700; } }
  var wl = Math.sqrt(wx * wx + wz * wz);
  if (!cv || wl < 60 || !meta.length || cv[3] == null) return (memo[key] = fallback);
  var h = cv[3] * headScale, lx = wx * Math.cos(h) - wz * Math.sin(h), lz = wx * Math.sin(h) + wz * Math.cos(h);
  var want = Math.atan2(lx, lz) * 180 / Math.PI, sc = meta.map(function (c) {
    var d = Math.abs(((c.dir - want) % 360 + 540) % 360 - 180);
    return [d + Math.abs(c.slide - wl) * 0.03, c.n];
  }).sort(function (a, b) { return a[0] - b[0]; });
  var pick = (sc.length > 1 && sc[1][0] - sc[0][0] < 20 && ((pi + Math.floor(td * 10)) & 1)) ? sc[1][1] : sc[0][1];
  R.__rpWant = R.__rpWant || {}; R.__rpWant[key] = [want, wl];
  return (memo[key] = pick);
}
function deathTimeOf(pi, t) {
  if (!R || !R.events) return null;
  if (!R.__kv) {
    R.__kv = [];
    R.events.forEach(function (e) { if (e.type === 'kill' && e.victim != null) (R.__kv[e.victim] || (R.__kv[e.victim] = [])).push(e.t); });
    R.__kv.forEach(function (a) { if (a) a.sort(function (x, y) { return x - y; }); });
  }
  var a = R.__kv[pi], best = null;
  if (!a) return null;
  for (var i = 0; i < a.length && a[i] <= t + 0.5; i++) best = a[i];
  return best;
}
function limpFall(m, p) {
  if (!m || !m.bones || !p || !m.skin || !m.skin.visible) return;
  var nb = m.bones.length, b, bn;
  if (p.alive) {
    if (!m.__lq || m.__lq.length !== nb * 4) { m.__lq = new Float32Array(nb * 4); m.__lp = new Float32Array(nb * 3); }
    for (b = 0; b < nb; b++) {
      bn = m.bones[b];
      m.__lq[b * 4] = bn.quaternion.x; m.__lq[b * 4 + 1] = bn.quaternion.y; m.__lq[b * 4 + 2] = bn.quaternion.z; m.__lq[b * 4 + 3] = bn.quaternion.w;
      m.__lp[b * 3] = bn.position.x; m.__lp[b * 3 + 1] = bn.position.y; m.__lp[b * 3 + 2] = bn.position.z;
    }
    m.__lt = T; m.__ly = m.skin.position.y; m.__dt = null; m.__lc = m.lastClip;
    return;
  }
  if (m.__lt == null || !m.__lq || m.__lq.length !== nb * 4) return;
  if (m.__dt == null) {
    // the first dead frame after a living one: only a death that was just WATCHED falls (a jump along the timeline that
    // lands on a body shows it lying, as before)
    if (T < m.__lt || T - m.__lt > 0.6) { m.__lt = null; return; }
    m.__dt = m.__lt;
  }
  // a recorded fall starts from ITS player's last pose, so only a short ease out of ours is needed; the lying clip needs the whole sag
  var u = (T - m.__dt) / (/^(rag|phys)\d/.test(m.lastClip || '') ? LIMP_RAG : LIMP_S);
  if (u < 0) { m.__lt = null; m.__dt = null; return; }
  if (u >= 1) return;
  var k = 1 - Math.pow(1 - u, 2.2);          // how far into the lying pose: quick collapse, slow settle
  if (!_lq0) { _lq0 = new THREE.Quaternion(); _lq1 = new THREE.Quaternion(); }
  for (b = 0; b < nb; b++) {
    bn = m.bones[b];
    _lq0.set(m.__lq[b * 4], m.__lq[b * 4 + 1], m.__lq[b * 4 + 2], m.__lq[b * 4 + 3]);
    _lq1.copy(bn.quaternion);
    bn.quaternion.copy(_lq0).slerp(_lq1, k);
    bn.position.set(m.__lp[b * 3] + (bn.position.x - m.__lp[b * 3]) * k,
                    m.__lp[b * 3 + 1] + (bn.position.y - m.__lp[b * 3 + 1]) * k,
                    m.__lp[b * 3 + 2] + (bn.position.z - m.__lp[b * 3 + 2]) * k);
  }
  m.skin.position.y = m.__ly + (m.skin.position.y - m.__ly) * k;
}

function applyPose(m, p, pi) {
  var pose = poseOf(p);
  var aiming = p && p.alive && ((p.act != null && FIRING[p.act])
                                || (p.ms != null && MS_AIM[p.ms]));
  updateProp(m, p, aiming);
  var flat = (pose === 'down');
  // A captured "down"/"dead" clip already contains the body lying on the
  // ground, so tipping the whole group over as well would lay it out twice.
  // The tip-over stays only for the rigid meshes, which cannot depict it.
  var skinned = !!(m.skin && ANIM);
  if (skinned) {
    var name = clipNameFor(p);
    // DEATH = one of the recorded ragdoll falls, when the body was on its feet. Which one and how far in both come from the
    // kill event (victim + time), so the same death always falls the same way, also after a jump along the timeline.
    if (RAG_ON && !FORCE_CLIP && p && !p.alive && pi != null && m.rig && m.rig.rag &&
        !/^(sleep|sleepBack|prone|crawl|down|hitProne|chokeProne|destabProne|blownFwd|blownBack|sleepFall|sleepFallCrouch|boxHide)$/.test(m.__lc || '')) {
      var rgT_ = deathTimeOf(pi, T);
      if (rgT_ != null) {
        var rgN_ = ragPick(pi, rgT_, m.rig), rgC_ = ANIM.clips[rgN_];
        if (rgC_) {
          // THE BODY KEEPS THE HEADING IT DIED WITH. The ragdoll rows carry the tumbling body's own heading (160 -> 347 deg inside
          // one death, measured 2026-10-10), and turning the whole figure by it spun the recorded fall - which has its own
          // turn in it - round on the spot; it also made "falls away from the killer" impossible to aim.
          var rgH_ = insCell(R, pi, rgT_);
          if (rgH_ && rgH_[3] != null) m.dir.rotation.y = rgH_[3] * headScale;
          poseSkin(m, p, rgN_, Math.max(0, T - rgT_));
          if (m.skin && rgC_.lift != null) m.skin.position.y = rgC_.lift * MM;
          m.skin.visible = true;
          m.lastClip = rgN_;
          m.dir.rotation.x = 0;
          m.dir.position.y = 0;
          if (m.cone) m.cone.visible = false;
          return pose;
        }
      }
    }
    // a body that was already lying face down when it died stays face down (no flip onto its back)
    if (!FORCE_CLIP && p && !p.alive && name === 'sleepBack' && ANIM.clips.sleep &&
        /^(sleep|prone|crawl|down|hitProne|chokeProne|destabProne|blownFwd|sleepFall|sleepFallCrouch)$/.test(m.__lc || '')) name = 'sleep';
    // A roll overrides whatever motionGroup claims, and plays ONCE across its
    // window instead of looping, so the tumble matches the 0.68 s the game
    // actually spent rolling.
    // action 0x16 is [CONFIRMED] "throw" in the format table, and it is a
    // different action from anything motionGroup reports, so it gets the same
    // override treatment as a roll.
    var cq_ = (!FORCE_CLIP && p && p.alive && pi != null) ? cqcClip(p, pi) : null;
    // THE BOX ITSELF: the game's own model (cbox_a_sk, 80 x 90 x 100 cm, origin at its bottom centre; weapons.json mesh 'cbox',
    // user 2026-10-09: "there's a specific cbox item you can use"). Placed from the box clips' own hands: sitting, the box stands
    // on the ground around the body (its middle 20 cm ahead of the hips); running, the hands - and so the box - are 21 cm higher
    // and 7 cm further forward. A plain block of the same size stands in until the model has loaded.
    var bxOn_ = !!(cq_ && (cq_.n === 'boxRun' || cq_.n === 'boxHide'));
    if (bxOn_ && (!m.boxProp || (m.boxProp.userData.block && WEAPONS && WEAPONS.cbox))) {
      if (m.boxProp) m.dir.remove(m.boxProp);
      var bg_ = WEAPONS && WEAPONS.cbox;
      if (bg_) {
        m.boxProp = new THREE.Mesh(bg_, weaponMats(bg_) || new THREE.MeshLambertMaterial({ color: 0xb08d5b, side: THREE.DoubleSide }));
        m.boxProp.scale.setScalar(MM);
      } else {
        var bb_ = new THREE.BoxGeometry(8, 9, 10); bb_.translate(0, 4.5, 0);
        m.boxProp = new THREE.Mesh(bb_, new THREE.MeshLambertMaterial({ color: 0xb08d5b }));
        m.boxProp.userData.block = true;
      }
      m.dir.add(m.boxProp);
    }
    if (m.boxProp) {
      m.boxProp.visible = bxOn_;
    }
    if (cq_) {
      var cqc_ = ANIM.clips[cq_.n], cql_ = cqc_.lift;
      poseSkin(m, p, cq_.n, cq_.t);
      // the box hangs from the HANDS: in the sitting clip they are 63 cm above the box's floor, on its centre line (measured on
      // the clip), so the box goes wherever the posed hands are - no forward / sideways constant to get wrong.
      if (bxOn_ && m.boxProp && m.bones && SKIN.hand != null && SKIN.hand2 != null && m.bones[SKIN.hand] && m.bones[SKIN.hand2]) {
        m.dir.updateMatrixWorld(true);
        var bh1_ = m.bones[SKIN.hand].getWorldPosition(new THREE.Vector3()), bh2_ = m.bones[SKIN.hand2].getWorldPosition(new THREE.Vector3());
        bh1_.add(bh2_).multiplyScalar(0.5); m.dir.worldToLocal(bh1_);
        m.boxProp.position.set(bh1_.x, cq_.n === 'boxHide' ? 0 : Math.max(0, bh1_.y - 5.0), bh1_.z);     // running: 13 cm higher than the sitting grip implies, or the head shows over the rim (seen 2026-10-09)
      }
      // A thrown body goes from standing to lying inside one clip, so it has a
      // ground offset per frame rather than one for the clip.
      if (cqc_.liftF) {
        cql_ = cqc_.liftF[Math.max(0, Math.min(cqc_.liftF.length - 1, Math.round(cq_.t * (cqc_.fps || ANIM.fps))))];
      }
      if (m.skin && cql_ != null) m.skin.position.y = cql_ * MM;
      m.skin.visible = true;
      m.lastClip = cq_.n;
      m.dir.rotation.x = 0;
      m.dir.position.y = 0;
      if (m.cone) m.cone.visible = false;
      return pose;
    }
    // THE WIND-UP (action 0x14) and the HELD ARM (0x15) before a throw: the game's own clips for the upper body, on the
    // legs of whatever gait he is moving at (he is running in 6,609 of 7,082 wind-ups), the same layering the throw uses.
    if (!FORCE_CLIP && ANIM.clips.throwWind && p && p.alive && (p.act === 0x14 || p.act === 0x15) && pi != null) {
      var twA = p.act, twN = (twA === 0x15 && ANIM.clips.throwHold) ? 'throwHold' : 'throwWind', twC = ANIM.clips[twN];
      var twS = entryRunStartT(pi, frameIndexAt(T), function (e) { return e.length > 14 && e[14] === twA; });
      var twD = ANIM.clips.throwWind.frames / (ANIM.clips.throwWind.fps || ANIM.fps);
      var twT = twN === 'throwHold' ? 0 : (twA === 0x15 ? twD - 0.04 : Math.min(twS != null ? Math.max(0, T - twS) : 0, twD - 0.04));
      var twB = (p.spd != null && p.spd > 0.6) ? (ANIM.clips.runNade ? 'runNade' : (ANIM.clips.run ? 'run' : null)) : null;
      if (twB) {
        var twBc = ANIM.clips[twB], twBt = T + (m.phase || 0), twDi = distAt(pi, frameIndexAt(T));
        if (twBc.speed > 0.2 && twDi != null) twBt = twDi / twBc.speed + (m.phase || 0);
        poseLayered(m, p, twB, twBt, twN, twT);
      } else poseSkin(m, p, twN, twT);
      var twL = ANIM.clips[twB || twN].lift;
      if (m.skin && twL != null) m.skin.position.y = twL * MM;
      applyHeadLook(m, p);
      m.skin.visible = true;
      m.lastClip = twN;
      m.dir.rotation.x = 0;
      m.dir.position.y = 0;
      if (m.cone) m.cone.visible = false;
      return pose;
    }
    if (!FORCE_CLIP && ANIM.clips.throw && p && p.alive && p.act === 0x16) {
      var thT = clipLocalT(m, pi, 'throw',
        entryRunStartT(pi, frameIndexAt(T), function (e) {
          return e.length > 14 && e[14] === 0x16;
        }));
      if (thT != null) thT = Math.max(0, thT + throwRelease(ANIM.clips.throw) - THROW_RELEASE);
      poseSkin(m, p, 'throw', thT != null ? thT : T + (m.phase || 0));
      var thGait = throwOverGait(m, p, pi);
      var thLift = ANIM.clips[thGait || 'throw'].lift;
      if (m.skin && thLift != null) m.skin.position.y = thLift * MM;
      applyTorsoAim(m, p, pi);
      applyHeadLook(m, p);
      applyWeaponAim(m, p, pi);
      m.skin.visible = true;
      m.lastClip = 'throw';
      m.dir.rotation.x = 0;
      m.dir.position.y = 0;
      if (m.cone) m.cone.visible = false;
      return pose;
    }
    // CROUCH overrides motionGroup for the same reason prone does: the state
    // byte still reads "stand idle" while a player is crouched, so the group
    // never mentions it. What marks it is the -400 height drop, split from
    // prone in buildProne by the motionState the player holds while low.
    // Without this a crouching player was drawn standing at a height 400 below
    // the floor, which is exactly the reported feet-through-the-ground.
    if (!FORCE_CLIP && pi != null && p && p.alive && crouchAt(pi, frameIndexAt(T))) {
      var firing_ = p.act != null && FIRING[p.act];
      // settling into a crouched shot: the last half-step of the run must not keep the crouch-run clip under a firing gun
      var cmoving = p.spd != null ? p.spd > (firing_ ? 1.2 : 0.25) : false;
      var cn = cmoving
        ? (ANIM.clips.crouchRun ? 'crouchRun' : (ANIM.clips.crouch ? 'crouch' : null))
        : (firing_ && ANIM.clips.crouchAim ? 'crouchAim'
           : (ANIM.clips.crouch ? 'crouch'
              : (ANIM.clips.crouchAim ? 'crouchAim' : null)));
      // CROUCHED AND FIRING: steady legs. The captured crouch-aim take is a few seconds of real play, and half a second in the
      // body dips and comes back (measured: head 91 -> 73 -> 79) - a stutter in the middle of a firefight (user, 2026-10-10).
      // The weapon's own aim pose goes on top anyway, so underneath it the settled end of the plain crouch is held still.
      var chold_ = firing_ && !cmoving && ANIM.clips.crouch && !THROWN[p.item] && ANIM.clips[wpnClip('wa', p) || ''];
      if (chold_) cn = 'crouch';
      if (cn) {
        var cc = ANIM.clips[cn];
        var ct = T + (m.phase || 0);
        var ctl = clipLocalT(m, pi, cn,
          flagRunStartT(pi, frameIndexAt(T), R.crouch && R.crouch[pi]));
        if (ctl != null) ct = ctl;                       // one-shot: play it once
        else if (cc.speed > 0.2) {
          var cd = distAt(pi, frameIndexAt(T));
          if (cd != null) ct = cd / cc.speed + (m.phase || 0);
        }
        if (chold_) ct = (cc.frames - 1) / (cc.fps || ANIM.fps);
        // CROUCHING WITH A GRENADE (or other thrown item) IN HAND. The only crouch
        // clips were captured holding a rifle, so a crouch-run with a grenade
        // drew both arms out front gripping a gun that is not there (user
        // screenshot, 2026-10-04). Keep the crouch clip's legs and pelvis and
        // put the grenade clips' upper body on them: runNade when moving,
        // idleNade when still.
        var cup = THROWN[p.item] ? (cmoving ? 'runNade' : 'idleNade') : null;
        // CROUCHED AND FIRING: the captured crouch-aim holds the rifle at the chest (user, 2026-10-10: "doesn't look like he
        // gets his gun up"). The weapon's own aiming pose - the one a standing shooter already gets - goes on the crouched legs.
        // (moving too: the gun comes up over the last steps of the crouch-run, legs keep running under it)
        var cwaOn_ = false;
        if (!cup && firing_) { var cwa_ = wpnClip('wa', p); if (cwa_ && ANIM.clips[cwa_]) { cup = cwa_; cwaOn_ = true; } }
        if (cup && ANIM.clips[cup]) {
          var cut_ = T + (m.phase || 0), cuc = ANIM.clips[cup];
          if (cuc.speed > 0.2) {
            var cud = distAt(pi, frameIndexAt(T));
            if (cud != null) cut_ = cud / cuc.speed + (m.phase || 0);
          }
          if (chold_ || cwaOn_) cut_ = 0;                // a held aim: the weapon pose is one still frame, not a clip looping under it
          poseLayered(m, p, cn, ct, cup, cut_);
        } else {
          var crl_ = reloadClip(p, pi) || sprayClip(p);
          if (crl_) poseLayered(m, p, cn, ct, crl_.n, crl_.t); else poseSkin(m, p, cn, ct);
        }
        if (m.skin && cc.lift != null) m.skin.position.y = cc.lift * MM;
        applyTorsoAim(m, p, pi);
        applyHeadLook(m, p);
        applyWeaponAim(m, p, pi);
        m.skin.visible = true;
        m.lastClip = cn;
        m.dir.rotation.x = 0;
        m.dir.position.y = 0;
        if (m.cone) m.cone.visible = false;
        return pose;
      }
    }
    // Prone overrides motionGroup entirely: the state byte reads "stand idle"
    // throughout, which is why prone players stood up in the viewer.
    if (!FORCE_CLIP && pi != null && p && p.alive && proneAt(pi, frameIndexAt(T))) {
      var moving = p.spd != null ? p.spd > 0.25 : false;
      var pn = (moving && ANIM.clips.crawl) ? 'crawl'
             : (ANIM.clips.prone ? 'prone' : null);
      // Lying still and shooting: the weapon's own prone hold. Only once the
      // go-prone clip has played out, or the drop to the ground would be skipped.
      if (!moving && aiming && pn === 'prone') {
        var pst_ = flagRunStartT(pi, frameIndexAt(T), R.prone && R.prone[pi]);
        if (pst_ != null && T - pst_ > ANIM.clips.prone.frames / ANIM.fps) pn = wpnClip('wp', p) || pn;
      }
      if (pn) {
        var pc = ANIM.clips[pn];
        var pt = T + (m.phase || 0);
        var ptl = clipLocalT(m, pi, pn,
          flagRunStartT(pi, frameIndexAt(T), R.prone && R.prone[pi]));
        if (ptl != null) pt = ptl;
        else if (pc.speed > 0.2) {
          var pd = distAt(pi, frameIndexAt(T));
          if (pd != null) pt = pd / pc.speed + (m.phase || 0);
        }
        poseSkin(m, p, pn, pt);
        if (m.skin && pc.lift != null) m.skin.position.y = pc.lift * MM;
        applyTorsoAim(m, p, pi);
      applyHeadLook(m, p);
        applyWeaponAim(m, p, pi);
        m.skin.visible = true;
        m.lastClip = pn;
        m.dir.rotation.x = 0;
        m.dir.position.y = 0;
        if (m.cone) m.cone.visible = false;
        return pose;
      }
    }
    var rp = (pi != null && !FORCE_CLIP && ANIM.clips.roll)
               ? rollProgress(pi, frameIndexAt(T)) : -1;
    if (rp >= 0) {
      var rn = sideRollAt(pi, frameIndexAt(T));
      if (!rn || !ANIM.clips[rn]) rn = 'roll';
      var rc = ANIM.clips[rn];
      poseSkin(m, p, rn, rp * (rc.frames - 1) / (rc.fps || ANIM.fps));
      if (m.skin && rc.lift != null) m.skin.position.y = rc.lift * MM;
      applyTorsoAim(m, p, pi);
      applyHeadLook(m, p);
      applyWeaponAim(m, p, pi);
      m.skin.visible = true;
      m.lastClip = rn;
      m.dir.rotation.x = 0;
      m.dir.position.y = 0;
      if (m.cone) m.cone.visible = false;
      return pose;
    }
    if (name) {
      // A clip captured while moving is played by distance covered, so the
      // stride matches the ground and the feet stop skating. Everything else
      // (idle, aim, crouch, dead) has no distance to key to and stays on time.
      var cl_ = ANIM.clips[name];
      var tm_ = T + (m.phase || 0);
      var tml_ = clipLocalT(m, pi, name, groupRunStartT(pi, frameIndexAt(T)));
      if (tml_ != null) tm_ = tml_;                      // fall / ledge / climb / down
      else if (cl_ && cl_.speed > 0.2 && pi != null) {
        var dm_ = distAt(pi, frameIndexAt(T));
        if (dm_ != null) tm_ = dm_ / cl_.speed + (m.phase || 0);
      }
      var rl_ = FORCE_CLIP ? null : (reloadClip(p, pi) || sprayClip(p));
      if (rl_) poseLayered(m, p, name, tm_, rl_.n, rl_.t); else poseSkin(m, p, name, tm_);
      if (m.skin && cl_ && cl_.lift != null) m.skin.position.y = cl_.lift * MM;
      // bare hands have nothing to point: pitching the torso down an aim ray
      // folded an unarmed player in half over whoever he had just put down
      if (name !== 'idleUnarmed' && name !== 'knifeIdle') applyTorsoAim(m, p, pi);
      applyHeadLook(m, p);
      applyWeaponAim(m, p, pi);
      m.skin.visible = true;
      m.lastClip = name;
    }
    m.dir.rotation.x = 0;
    m.dir.position.y = 0;
    if (m.cone) m.cone.visible = false;
    return pose;
  }
  // whole figure tips over when down or prone
  m.dir.rotation.x = flat ? -Math.PI / 2 : 0;
  m.dir.position.y = flat ? 1.6 : 0;
  // climbing draws upright with legs together; no crouch state exists in the
  // format, so nothing compresses the legs any more.
  var sq = pose === 'climb' ? 0.86 : 1;
  if (m.fig) {
    m.fig.scale.y = FIG_H * sq;
    var want = FIG_GEO[figKeyFor(p)];
    if (want && m.fig.geometry !== want) m.fig.geometry = want;
  }
  m.legL.scale.y = m.legR.scale.y = sq;
  m.legL.position.y = m.legR.position.y = 3.75 * sq;
  var hipY = 7.5 * sq;
  m.torso.position.y = hipY + 3.0;
  m.head.position.y = hipY + 7.8;
  m.armL.position.y = m.armR.position.y = hipY + 3.1;
  // arms come up and forward when engaging
  m.armL.rotation.x = m.armR.rotation.x = aiming ? -Math.PI / 2 : 0;
  m.armL.position.z = m.armR.position.z = aiming ? 2.4 : 0.3;
  m.cone.visible = !!aiming && !flat;
  return pose;
}

function pushTrails() {
  var fi = frameIndexAt(T);
  for (var i = 0; i < trailBuf.length; i++) {
    var tb = trailBuf[i], p = live[i];
    trailLines[i].visible = showTrails && !!p;
    if (!showTrails || !p) continue;
    if (tb.last === fi) continue;
    if (tb.last >= 0 && Math.abs(fi - tb.last) > 3) { tb.n = 0; }  // scrubbed: reset
    tb.last = fi;
    if (tb.n >= TRAIL_N) {
      tb.buf.copyWithin(0, 3);
      tb.n = TRAIL_N - 1;
    }
    tb.buf[tb.n * 3] = (p.x - ctr[0]) * S;
    tb.buf[tb.n * 3 + 1] = p.y * S + 1.5;
    tb.buf[tb.n * 3 + 2] = (p.z - ctr[2]) * S;
    tb.n++;
    var attr = trailLines[i].geometry.getAttribute('position');
    attr.needsUpdate = true;
    trailLines[i].geometry.setDrawRange(0, tb.n);
  }
}

// ── controls ─────────────────────────────────────────────────────────────
function syncPlay() {
  $('b-play').innerHTML = playing
    ? '<svg viewBox="0 0 16 16"><path d="M3 2h4v12H3zM9 2h4v12H9z"/></svg>'
    : '<svg viewBox="0 0 16 16"><path d="M3 2l11 6-11 6z"/></svg>';
  $('b-play').dataset.on = playing ? '1' : '0';
}
function setCam(m) {
  // Free cam starts from wherever the camera already is, so the view does not jump.
  if (m === 'free' && camMode !== 'free' && cam) {
    var d = cam.getWorldDirection(new THREE.Vector3());
    free.x = cam.position.x; free.y = cam.position.y; free.z = cam.position.z;
    free.yaw = Math.atan2(d.x, d.z);
    free.pitch = clamp(Math.asin(clamp(d.y, -1, 1)), -1.5, 1.5);
  }
  free.keys = {};
  camMode = m;
  setTimeout(applyXray, 0);
  document.querySelectorAll('[data-cam]').forEach(function (b) {
    b.dataset.on = b.dataset.cam === m ? '1' : '0';
  });
  if (m === 'follow') {
    if (follow < 0 && R && R.players.length) selectPlayerSilent(0);
    orbit.dist = clamp(orbit.dist, 25, 70);
    orbit.el = Math.min(orbit.el, 0.42);
  } else if (m !== 'free') {
    frameCameraKeepAngle();
  }
}
function selectPlayerSilent(i) { follow = i; updateRoster(); try { updatePlayerCard(); } catch (e) {} }
window.__mgo2Seek = function (frac) { if (R) seek(clamp(frac, 0, 1) * R.duration); };

// ROUND / MATCH RESULT BANNER with the MVP. Who won is the rules record's own
// byte (meta.roundResults / matchResult); the round count is the game's
// rounds-won counter; the MVP is the top scorer by the game's own points.
// Shown for 8 s after a round, and from the end of the last round on.
// ROOF CUT for enclosed maps in Top view (user, 2026-10-05: Tomb of Tubes "looks kinda
// messed up from above because its an enclosed basement" -- the HD scenery has its
// ceilings, so Top view showed the roof). In Top view on a listed map everything
// above head height is sliced away: 2.6 m over the followed player, or over the
// middle player when nobody is followed. ?roofcut=1 forces it on any map, =0 off.
// FLOORS button (user, same day: roof cut "for sure, maybe a per floor toggle"):
//   Auto   slice the roof off just above the followed player (or the middle player)
//   All    no slicing
//   1..N   show ONE storey: a slab from just under that floor to just under the next
// The storeys are not stored anywhere; they are found from where players actually
// stood in this replay (foot heights clustered, a gap of 1.8 m starts a new storey).
// Default: Auto in Top view on the enclosed maps listed here, All everywhere else.
// Key Y or the button steps it; link: floor=auto | all | <n>.
var ROOF_CUT_STAGES = { n008a: 1, n013a: 1, n006a: 1 }, roofPlane = null, floorPlane = null, roofSig = '';
var ROOF_CUT_Q = (/[?&]roofcut=([01])/.exec(location.search) || [])[1];
var floorMode = null;            // null = default for the map / view, 'auto', 'all', or a storey index
function floorsOf(r) {
  if (r.__floors) return r.__floors;
  var ys = [], step = Math.max(1, Math.floor(r.frames.length / 1500));
  for (var k = 0; k < r.frames.length; k += step) {
    var row = r.frames[k].p || [];
    for (var i = 0; i < row.length; i++) if (row[i] && row[i][4] && row[i][1] != null) ys.push(row[i][1]);
  }
  // A storey is a height where players stand a lot: foot heights in 250 mm bins, a bin
  // (with its two neighbours) holding 3% of all samples and beating everything within
  // 2.25 m of it. Gaps do not work -- stairs and ramps fill them (first try, all five
  // hosted replays came out as one storey).
  var B = 250, h = {}, out = [];
  ys.forEach(function (y) { var bn = Math.round(y / B); h[bn] = (h[bn] || 0) + 1; });
  function g(bn) { return (h[bn - 1] || 0) + (h[bn] || 0) + (h[bn + 1] || 0); }
  Object.keys(h).map(Number).sort(function (x, y) { return x - y; }).forEach(function (bn) {
    var v = g(bn);
    if (v < ys.length * 0.03) return;
    for (var o = -9; o <= 9; o++) { var w = g(bn + o); if (w > v || (w === v && o < 0)) return; }
    out.push({ y: bn * B, share: v / ys.length });
  });
  return (r.__floors = out);
}
function floorDefault() {
  if (ROOF_CUT_Q === '0') return 'all';
  if (ROOF_CUT_Q === '1') return 'auto';
  return (camMode === 'top' && HD.on && HD.mesh && R && ROOF_CUT_STAGES[R.stage]) ? 'auto' : 'all';
}
function cycleFloor() {
  if (!R) return;
  var n = floorsOf(R).length, cur = floorMode == null ? floorDefault() : floorMode;
  var seq = ['auto', 'all'];
  if (n > 1) for (var i = 0; i < n; i++) seq.push(i);
  var at = seq.indexOf(cur);
  floorMode = seq[(at + 1) % seq.length];
  syncFloorBtn();
}
function syncFloorBtn() {
  var b = $('b-floor');
  if (!b) {
    var ob = $('b-xray') || $('b-obj');
    if (!ob || !ob.parentNode) return;
    b = document.createElement('button');
    b.className = 'tbtn wide'; b.id = 'b-floor';
    b.title = 'Slice the map by height (Y): Auto cuts the roof off above the player, 1..N shows one storey, All shows everything';
    ob.parentNode.insertBefore(b, ob);
    b.addEventListener('click', cycleFloor);
  }
  var cur = floorMode == null ? floorDefault() : floorMode;
  b.textContent = cur === 'auto' ? 'Roof off' : cur === 'all' ? 'Floors' : 'Floor ' + (cur + 1);
  b.dataset.on = cur === 'all' ? '0' : '1';
}
function updateRoofCut() {
  if (!renderer || !R) return;
  var cur = floorMode == null ? floorDefault() : floorMode, top = null, bot = null;
  var ry = (/[?&]roofy=(-?\d+)/.exec(location.search) || [])[1];
  if (cur === 'auto') {
    var y = null;
    if (follow >= 0 && live[follow]) y = live[follow].y;
    else {
      var ys = [];
      for (var i = 0; i < live.length; i++) if (live[i] && live[i].alive) ys.push(live[i].y);
      ys.sort(function (a, b) { return a - b; });
      if (ys.length) y = ys[ys.length >> 1];
    }
    if (ry != null) top = +ry; else if (y != null) top = y + 2600;
  } else if (cur !== 'all') {
    var fl = floorsOf(R), me = fl[cur], up = fl[cur + 1];
    if (me) {
      bot = cur === 0 ? null : me.y - 700;
      top = up ? up.y - 400 : me.y + 2600;
    }
  }
  var sig = cur + ':' + (top == null ? '' : Math.round(top / 50)) + ':' + (bot == null ? '' : Math.round(bot / 50));
  if (sig === roofSig) return;
  roofSig = sig;
  var planes = [];
  if (top != null) {
    if (!roofPlane) roofPlane = new THREE.Plane(new THREE.Vector3(0, -1, 0), 0);
    roofPlane.constant = top * S; planes.push(roofPlane);
  }
  if (bot != null) {
    if (!floorPlane) floorPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    floorPlane.constant = -bot * S; planes.push(floorPlane);
  }
  renderer.clippingPlanes = planes;
  syncFloorBtn();
}
function updateOverlays() {
  try { updateIntroCard(); } catch (e1) {}
  try { updateStreakBanner(); } catch (e2) {}
  try { updateActionFx(); } catch (e3) {}
}
function updateResultCard() {
  updateOverlays();
  var el = $('resultcard');
  if (!el) {
    el = document.createElement('div');
    el.id = 'resultcard';
    el.style.cssText = 'position:absolute;top:22%;left:50%;transform:translateX(-50%);z-index:16;pointer-events:none;' +
      'text-align:center;font-family:var(--f-disp);text-transform:uppercase;letter-spacing:.1em;' +
      'background:rgba(11,13,12,.82);border:1px solid var(--hair);padding:14px 34px;white-space:nowrap;display:none';
    $('stage').appendChild(el);
  }
  var rrs = (R && R.meta && R.meta.roundResults) || [], mres = R && R.meta && R.meta.matchResult, show = null, isMatch = false;
  if (mres && T >= mres.t) { show = mres; isMatch = true; }
  else for (var i = 0; i < rrs.length; i++) if (T >= rrs[i].t && T - rrs[i].t < 8) show = rrs[i];
  if (!show) { el.style.display = 'none'; el.__key = null; return; }
  var key = (isMatch ? 'm' : 'r') + show.t + ':' + Math.floor(T / 2);
  if (el.__key === key) { el.style.display = 'block'; return; }
  el.__key = key;
  var rw = gameStat(STAT.roundsWon, T), teams = [], won = {};
  R.players.forEach(function (p, pi) {
    if (p.team == null) return;
    if (teams.indexOf(p.team) < 0) teams.push(p.team);
    if (rw && rw[pi] > (won[p.team] || 0)) won[p.team] = rw[pi];
  });
  teams.sort();
  var head = show.winner != null
    ? '<span style="color:' + teamColor(show.winner) + '">' + esc(teamLabel(show.winner)) + '</span> ' + (isMatch ? 'wins the match' : 'wins the round')
    : (show.draw ? (isMatch ? 'Match drawn' : 'Round drawn') : 'No result');
  var score = teams.map(function (tm) { return '<span style="color:' + teamColor(tm) + '">' + (won[tm] || 0) + '</span>'; }).join(' : ');
  var pts = gamePoints(T), mvp = -1;
  if (pts) pts.forEach(function (v, pi) { if (v > 0 && (mvp < 0 || v > pts[mvp])) mvp = pi; });
  el.innerHTML = '<div style="font-size:22px;color:var(--ink)">' + head + '</div>' +
    '<div style="font-size:30px;margin:4px 0;color:var(--ink)">' + score + '</div>' +
    (mvp >= 0 ? '<div style="font-size:12px;color:var(--ink-dim)">' + (isMatch ? 'MVP' : 'Top scorer so far') + ' &nbsp;<b style="color:' +
      teamColor(R.players[mvp].team) + '">' + esc(R.players[mvp].name || '?') + '</b> &nbsp;' + pts[mvp] + ' pts</div>' : '');
  el.style.display = 'block';
}

// THE FOLLOWED PLAYER'S OWN HUD, bottom-left, in follow view: life and stamina
// bars and the weapon box with its count, laid out like the game's. All four
// are in every record of the file; the magazine count stops at 15 because the
// file's own field does.
function updateFollowHud() {
  var el = $('ghud');
  if (!el) {
    el = document.createElement('div');
    el.id = 'ghud';
    el.style.cssText = 'position:absolute;left:300px;bottom:150px;z-index:14;pointer-events:none;font-family:var(--f-disp);' +
      'text-transform:uppercase;letter-spacing:.06em;font-size:12px;color:var(--ink);background:rgba(11,13,12,.66);' +
      'border:1px solid var(--hair);padding:8px 10px;min-width:210px;display:none';
    $('stage').appendChild(el);
  }
  var p = (camMode === 'follow' && follow >= 0) ? live[follow] : null;
  if (!R || !p) { el.style.display = 'none'; return; }
  function bar(v, col, label) {
    var w = Math.round(clamp(v == null ? 0 : v, 0, 1) * 150);
    return '<div style="display:flex;align-items:center;gap:6px;margin:2px 0"><span style="width:44px;color:var(--ink-dim);font-size:10px">' + label +
      '</span><span style="display:inline-block;width:150px;height:8px;border:1px solid var(--hair)"><span style="display:block;height:100%;width:' +
      w + 'px;background:' + col + '"></span></span></div>';
  }
  var nm = itemName(p.item), pl = R.players[follow];
  el.innerHTML = '<div style="color:' + teamColor(pl.team) + ';margin-bottom:3px">' + esc(pl.name || '') + (p.alive ? '' : ' &nbsp;<span style="color:#E2574C">down</span>') + '</div>' +
    bar(p.alive ? p.hp : 0, '#8FD18A', 'Life') + bar(p.alive ? p.st : 0, '#4EA8DE', 'Psyche') +
    '<div style="margin-top:5px;display:flex;justify-content:space-between;border-top:1px solid var(--hair);padding-top:4px"><span>' +
    esc(nm || 'no item') + '</span><b>' + (p.ammo == null ? '' : (p.ammo >= 15 ? '15+' : p.ammo)) + '</b></div>';
  el.style.display = 'block';
}
function frameCameraKeepAngle() {
  orbit.tx = 0; orbit.tz = 0;
  orbit.ty = (M && M.floorY != null ? M.floorY : 0) * S + 8;
  orbit.dist = clamp(span * 0.78 * aspectPad(), 60, 8000);
}
function seek(t) {
  T = clamp(t, 0, R ? R.duration : 0);
  feedShown = -1;
  trailBuf.forEach(function (tb) { tb.n = 0; tb.last = -1; });
  rings.length = 0;
  if (typeof clearTracers === 'function') clearTracers();
}

function wireControls() {
  $('b-play').addEventListener('click', function () { playing = !playing; syncPlay(); });
  $('b-back').addEventListener('click', function () { seek(T - 10); });
  $('b-fwd').addEventListener('click', function () { seek(T + 10); });
  document.querySelectorAll('[data-spd]').forEach(function (b) {
    b.addEventListener('click', function () {
      speed = +b.dataset.spd;
      document.querySelectorAll('[data-spd]').forEach(function (o) {
        o.dataset.on = o === b ? '1' : '0';
      });
    });
  });
  document.querySelectorAll('[data-cam]').forEach(function (b) {
    b.addEventListener('click', function () {
      if (b.dataset.cam === 'follow' && camMode === 'follow' && !camSubj) { setFollowStyle(followStyle + 1); return; }
      setCam(b.dataset.cam);
    });
  });
  // "More" reveals the secondary view toggles on a phone. The button exists at
  // every width but only does anything where the CSS collapses them, so the
  // desktop layout is untouched.
  // The dock grows when the bar wraps to a second row on a narrow window. The
  // side panes sit above it by --dock, so keep that equal to its real height
  // or the bar's last row ends up under the bottom edge of the window.
  var dockEl = $('dock');
  if (dockEl && window.ResizeObserver) {
    new ResizeObserver(function () {
      document.documentElement.style.setProperty('--dock', dockEl.offsetHeight + 'px');
    }).observe(dockEl);
  }
  // Names on/off (key N): hides the name tags, their stalks, the ground rings
  // and the developer read-out, for clean screenshots.
  var namesBtn = $('b-names');
  if (namesBtn) namesBtn.addEventListener('click', function () { setNames(!NAMES_ON); });
  var hdBtn = $('b-hd');
  if (hdBtn) { hdBtn.dataset.on = HD.on ? '1' : '0'; hdBtn.addEventListener('click', function () { setHD(!HD.on); }); }
  var sndBtn = $('b-snd');
  if (sndBtn) sndBtn.addEventListener('click', function () { setSound(!SND.on); });
  var skinsBtn = $('b-skins');
  if (skinsBtn) {
    skinsBtn.dataset.on = TEX_ON ? '1' : '0';
    skinsBtn.addEventListener('click', function () { setSkins(!TEX_ON); });
  }
  var moreBtn = $('b-more');
  if (moreBtn) {
    moreBtn.addEventListener('click', function () {
      var on = document.body.dataset.more === '1';
      document.body.dataset.more = on ? '0' : '1';
      moreBtn.dataset.on = on ? '0' : '1';
      // the dock changes height, so the canvas has to be re-fitted
      if (typeof onResize === 'function') onResize();
      else window.dispatchEvent(new Event('resize'));
    });
  }
  $('b-trail').addEventListener('click', function () {
    showTrails = !showTrails;
    $('b-trail').dataset.on = showTrails ? '1' : '0';
  });
  (function () {
    var hb = $('b-heat') || $('b-obj');
    if (!hb || !hb.parentNode || $('b-photo')) return;
    var pb = document.createElement('button');
    pb.className = 'tbtn wide'; pb.id = 'b-photo'; pb.dataset.on = '0'; pb.textContent = 'Photo';
    pb.title = 'Hide the HUD for screenshots (P): panels off, then name tags off too, then back. Esc brings everything back.';
    hb.parentNode.insertBefore(pb, hb.nextSibling);
    pb.addEventListener('click', function () { setPhoto(photoMode + 1); });
  })();
  document.addEventListener('keydown', function (e) {
    if (e.ctrlKey || e.metaKey || e.altKey || (e.target && /INPUT|TEXTAREA|SELECT/.test(e.target.tagName))) return;
    if (e.key === 'y' || e.key === 'Y') cycleFloor();
    else if (e.key === 'r' || e.key === 'R') toggleClipReview();
    else if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && stepClipReview(e.key === 'ArrowDown' ? 1 : -1)) e.preventDefault();
    else if (e.key === 'p' || e.key === 'P') setPhoto(photoMode + 1);
    else if (e.key === 'c' || e.key === 'C') { if (camMode !== 'follow') setCam('follow'); else setFollowStyle(followStyle + 1); }
    else if (e.key === 'Escape' && photoMode) setPhoto(0);
  });
  (function () { var m = null; try { m = localStorage.getItem('mgo2dockmin'); } catch (e) {} setDockMin(m == null ? innerHeight < 520 : m === '1'); })();
  syncSnapBtn();
  document.addEventListener('keydown', function (e) {
    if ((e.key === 'b' || e.key === 'B') && !e.ctrlKey && !e.metaKey && !e.altKey &&
        !(e.target && /INPUT|TEXTAREA|SELECT/.test(e.target.tagName))) cycleSnap();
  });
  $('b-obj').addEventListener('click', function () {
    showObj = !showObj;
    $('b-obj').dataset.on = showObj ? '1' : '0';
    if (objGroup) objGroup.visible = showObj;
  });
  $('b-clip').addEventListener('click', function () {
    showClip = !showClip;
    $('b-clip').dataset.on = showClip ? '1' : '0';
    if (clipMesh) clipMesh.visible = showClip;
  });
  $('b-level').addEventListener('click', function () {
    levelMode = (levelMode + 1) % 4;
    applyRoofs();
  });
  $('b-score').addEventListener('click', function () {
    var el = $('scorepane'), on = el.dataset.open === '1';
    el.dataset.open = on ? '0' : '1';
    $('b-score').dataset.on = on ? '0' : '1';
    if (!on) renderScore();
  });
  if ($('s-body')) $('s-body').addEventListener('click', function (ev) {
    var tb = ev.target && ev.target.closest ? ev.target.closest('[data-tab]') : null;
    if (tb) { scoreTab = tb.dataset.tab; renderScore(); return; }
    var a = ev.target && ev.target.closest ? ev.target.closest('[data-seek]') : null;
    if (!a) return;
    ev.preventDefault();
    seek(Math.max(0, parseFloat(a.dataset.seek) - 3));
  });
  // the open scoreboard can cover the dock, so it carries its own way out
  if ($('s-close')) $('s-close').addEventListener('click', function () { $('b-score').click(); });
  if ($('b-aim')) $('b-aim').addEventListener('click', function () { setAim(!AIM_ON); });
  if ($('b-heat')) $('b-heat').addEventListener('click', function () { setHeat(HEAT.mode + 1); });
  if ($('s-mode')) $('s-mode').addEventListener('click', function () {
    scoreLive = !scoreLive;
    scoreLiveSec = Math.floor(T);
    renderScore();
  });
  if ($('b-alt')) $('b-alt').addEventListener('click', function () {
    HD.alt = !HD.alt;
    if (M) loadHD(M.stage);
  });
  if ($('b-cut')) {
    $('b-cut').dataset.on = CUT.on ? '1' : '0';
    $('b-cut').addEventListener('click', function () {
      CUT.on = !CUT.on;
      $('b-cut').dataset.on = CUT.on ? '1' : '0';
      try { localStorage.setItem('mgo2rv.cut', CUT.on ? '1' : '0'); } catch (e) {}
    });
  }
  $('b-xray').addEventListener('click', function () {
    xray = !xray;
    $('b-xray').dataset.on = xray ? '1' : '0';
    applyXray();
  });
  $('b-roof').addEventListener('click', function () {
    showRoofs = !showRoofs;
    $('b-roof').dataset.on = showRoofs ? '1' : '0';
    applyRoofs();
  });

  addEventListener('keydown', function (e) {
    if (e.target.tagName === 'SELECT') return;
    if (e.code === 'ShiftLeft' || e.code === 'ShiftRight') free.keys[e.code] = 1;
    // In free cam WASD/QE fly the camera, so their usual toggles (A, S) are
    // buttons-only while it is on.
    if (camMode === 'free' && FREE_KEYS[e.code] && !e.ctrlKey && !e.metaKey && !e.altKey) {
      e.preventDefault(); free.keys[e.code] = 1; return;
    }
    if (e.key === 'v' || e.key === 'V') { setCam(camMode === 'free' ? 'orbit' : 'free'); }
    else if (e.code === 'Space') { e.preventDefault(); playing = !playing; syncPlay(); }
    else if (e.code === 'ArrowLeft') { e.preventDefault(); seek(T - (e.shiftKey ? 30 : 5)); }
    else if (e.code === 'ArrowRight') { e.preventDefault(); seek(T + (e.shiftKey ? 30 : 5)); }
    else if (e.key === 't' || e.key === 'T') { setCam(camMode === 'top' ? 'orbit' : 'top'); }
    else if (e.key === 'f' || e.key === 'F') { setCam('follow'); }
    else if (e.key === 'a' || e.key === 'A') { setAutoFollow(!autoFollow); }
    else if (e.key === 'k' || e.key === 'K') { setSkins(!TEX_ON); }
    else if (e.key === 'n' || e.key === 'N') { setNames(!NAMES_ON); }
    else if (e.key === 'm' || e.key === 'M') { setSound(!SND.on); }
    else if (e.key === 'h' || e.key === 'H') { setHD(!HD.on); }
    else if (e.key === 'i' || e.key === 'I') { toggleDiag(); }
    
    else if (e.key === 'x' || e.key === 'X') { $('b-xray').click(); }
    else if (e.key === 's' || e.key === 'S') { $('b-score').click(); }
    
    else if (e.key === 'o' || e.key === 'O') { $('b-obj').click(); }
    else if (e.key === 'g' || e.key === 'G') { setAim(!AIM_ON); }
    else if (e.key === 'j' || e.key === 'J') { setHeat(HEAT.mode + 1); }
  });
  addEventListener('keyup', function (e) { delete free.keys[e.code]; });
  addEventListener('blur', function () { free.keys = {}; });

  // timeline scrub (pointer events cover mouse + touch + pen)
  var tl = $('tl'), scrub = false;
  function tlSeek(ev) {
    var r = tl.getBoundingClientRect(), pad = 10;
    var u = clamp((ev.clientX - r.left - pad) / (r.width - pad * 2), 0, 1);
    seek((tlView.a + u * (tlView.b - tlView.a)) * (R ? R.duration : 0));
  }
  // a mark under the pointer: nearest one within its radius (+3 px of forgiveness)
  var tlTipEl = null, tlTipTimer = 0;
  function tlPick(ev) {
    var r = tl.getBoundingClientRect(), mx = ev.clientX - r.left, my = ev.clientY - r.top, best = null, bd = 1e9;
    for (var i = 0; i < tlHits.length; i++) {
      var h = tlHits[i], d = Math.hypot(mx - h[0], my - h[1]);
      if (d <= h[2] + 3 && d < bd) { bd = d; best = h; }
    }
    return best;
  }
  function tlTip(h, ev) {
    if (!tlTipEl) {
      tlTipEl = document.createElement('div');
      tlTipEl.id = 'tltip';
      tlTipEl.style.cssText = 'position:fixed;z-index:60;pointer-events:none;display:none;transform:translate(-50%,-100%);' +
        'background:rgba(11,13,12,.94);border:1px solid var(--hair);padding:5px 9px;white-space:nowrap;' +
        'font:500 12px/1.3 "IBM Plex Mono",monospace;color:#D7DDD0;letter-spacing:.02em';
      document.body.appendChild(tlTipEl);
    }
    if (!h) { tlTipEl.style.display = 'none'; return; }
    var txt = '';
    try { txt = tlLabel(h); } catch (eL) { txt = ''; }
    if (!txt) { tlTipEl.style.display = 'none'; return; }
    tlTipEl.innerHTML = txt + ' <span style="opacity:.55">· ' + fmt(h[3]) + '</span>';
    var r = tl.getBoundingClientRect();
    tlTipEl.style.left = clamp(r.left + h[0], 110, innerWidth - 110) + 'px';
    tlTipEl.style.top = (r.top + h[1] - h[2] - 6) + 'px';
    tlTipEl.style.display = 'block';
  }
  // two fingers on the bar = pinch: spread to zoom in around the point between them, and
  // slide both to move along the match
  var tlPts = {}, tlPinch = null, tlLastTap = 0;
  function tlFrac(cx) { var r = tl.getBoundingClientRect(); return clamp((cx - r.left - 10) / (r.width - 20), 0, 1); }
  function tlPinchState() {
    var ids = Object.keys(tlPts);
    if (ids.length < 2) return null;
    var xa = tlPts[ids[0]], xb = tlPts[ids[1]];
    return { d: Math.max(12, Math.abs(xa - xb)), c: (xa + xb) / 2 };
  }
  tl.addEventListener('wheel', function (e) {
    if (!R) return;
    e.preventDefault();
    if (e.shiftKey || Math.abs(e.deltaX) > Math.abs(e.deltaY)) tlPanBy((e.shiftKey ? e.deltaY : e.deltaX) * 0.0012);
    else tlZoomAt(tlFrac(e.clientX), Math.exp(e.deltaY * 0.0016));
    tlTip(null);
  }, { passive: false });
  tl.addEventListener('dblclick', function () { tlView.a = 0; tlView.b = 1; });
  tl.addEventListener('lostpointercapture', function (e) { delete tlPts[e.pointerId]; if (Object.keys(tlPts).length < 2) tlPinch = null; });
  tl.addEventListener('pointerdown', function (e) {
    // a PRIMARY pointer going down means nothing else is touching the bar: drop any finger left
    // over from a pinch whose release never arrived (seen in the headless test: the next tap was
    // read as a second finger and the double-tap reset never fired)
    if (e.isPrimary) { tlPts = {}; tlPinch = null; }
    tlPts[e.pointerId] = e.clientX;
    try { tl.setPointerCapture(e.pointerId); } catch (eC) {}
    if (Object.keys(tlPts).length >= 2) {
      var ps = tlPinchState();
      tlPinch = { d: ps.d, c: ps.c, a: tlView.a, b: tlView.b };
      scrub = false; tlTip(null);
      return;
    }
    if (e.pointerType !== 'mouse') {           // double-tap = reset (touch has no dblclick here)
      var nowT = performance.now();
      if (nowT - tlLastTap < 400) { tlView.a = 0; tlView.b = 1; tlLastTap = 0; return; }
      tlLastTap = nowT;
    }
    var h = tlPick(e);
    if (h) {
      // a click ON a mark goes to that moment (a second early, so it is seen happening)
      // instead of to wherever on the bar the pointer happens to be
      seek(Math.max(0, h[3] - 1));
      tlTip(h, e);
      clearTimeout(tlTipTimer);
      if (e.pointerType !== 'mouse') tlTipTimer = setTimeout(function () { tlTip(null); }, 2600);
      return;
    }
    scrub = true; tl.setPointerCapture(e.pointerId); tlSeek(e);
  });
  tl.addEventListener('pointermove', function (e) {
    if (tlPts[e.pointerId] !== undefined) tlPts[e.pointerId] = e.clientX;
    if (tlPinch) {
      var ps = tlPinchState();
      if (ps) {
        var sp0 = tlPinch.b - tlPinch.a, minSp = Math.min(1, 6 / Math.max(1, R ? R.duration : 1));
        var ns = clamp(sp0 * tlPinch.d / ps.d, minSp, 1);
        // the moment that was under the fingers' midpoint stays under it
        var c0 = tlPinch.a + tlFrac(tlPinch.c) * sp0, na = clamp(c0 - tlFrac(ps.c) * ns, 0, 1 - ns);
        tlView.a = na; tlView.b = na + ns;
      }
      return;
    }
    if (scrub) { tlSeek(e); return; }
    if (e.pointerType !== 'mouse') return;
    var h = tlPick(e);
    tl.style.cursor = h ? 'pointer' : '';
    tlTip(h, e);
  });
  tl.addEventListener('pointerleave', function () { if (!scrub) tlTip(null); });
  tl.addEventListener('pointerup', function (e) { scrub = false; delete tlPts[e.pointerId]; if (Object.keys(tlPts).length < 2) tlPinch = null; });
  tl.addEventListener('pointercancel', function (e) { scrub = false; delete tlPts[e.pointerId]; if (Object.keys(tlPts).length < 2) tlPinch = null; });

  // camera drag: 1 pointer = orbit, 2 pointers = pinch zoom + pan
  var gl = $('gl'), pts = {}, lastDist = 0, lastMid = null;
  gl.style.touchAction = 'none';
  gl.addEventListener('pointerdown', function (e) {
    gl.setPointerCapture(e.pointerId);
    pts[e.pointerId] = { x: e.clientX, y: e.clientY,
                         ox: e.clientX, oy: e.clientY,
                         t0: performance.now(), moved: 0 };
    lastDist = 0; lastMid = null;
  });
  gl.addEventListener('pointermove', function (e) {
    var p = pts[e.pointerId];
    if (!p) return;
    var ids = Object.keys(pts);
    if (ids.length === 1 && camMode === 'free') {
      // grab-style look: the scene follows the finger
      free.yaw += (e.clientX - p.x) * 0.004;
      free.pitch = clamp(free.pitch + (e.clientY - p.y) * 0.004, -1.5, 1.5);
    } else if (ids.length === 1) {
      orbit.az -= (e.clientX - p.x) * 0.006;
      orbit.el = clamp(orbit.el + (e.clientY - p.y) * 0.005, -0.25, 1.5);
      if (camMode === 'top') setCam('orbit');
    }
    p.moved = Math.max(p.moved || 0, Math.hypot(e.clientX - p.ox, e.clientY - p.oy));
    p.x = e.clientX; p.y = e.clientY;
    if (ids.length === 2) {
      var a = pts[ids[0]], b = pts[ids[1]];
      var d = Math.hypot(a.x - b.x, a.y - b.y);
      var mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
      if (lastDist && camMode === 'free') {
        // touch flying: pinch = forward / back, two-finger drag = slide
        freeMove((d - lastDist) * 0.6,
                 lastMid ? -(mid.x - lastMid.x) * 0.3 : 0,
                 lastMid ? (mid.y - lastMid.y) * 0.3 : 0);
      } else if (lastDist) {
        orbit.dist = clamp(orbit.dist * (lastDist / d), 5, 6000);
        if (lastMid && camMode !== 'follow') {
          var k = orbit.dist * 0.0016;
          var ca = Math.cos(orbit.az), sa = Math.sin(orbit.az);
          var dx = (mid.x - lastMid.x) * k, dy = (mid.y - lastMid.y) * k;
          orbit.tx -= dx * ca - dy * sa * 0.4;
          orbit.tz += dx * sa + dy * ca * 0.4;
        }
      }
      lastDist = d; lastMid = mid;
    }
  });
  // A tap is a pointer that went down and came up in the same place: under
  // 12 px of drag inside 400 ms, with no second finger involved.
  function up(e) {
    var p = pts[e.pointerId];
    if (p && Object.keys(pts).length === 1 && (p.moved || 0) < 12 &&
        performance.now() - p.t0 < 400) {
      tapAt(e.clientX, e.clientY);
    }
    delete pts[e.pointerId]; lastDist = 0; lastMid = null;
  }
  gl.addEventListener('pointerup', up);
  gl.addEventListener('pointercancel', up);
  gl.addEventListener('wheel', function (e) {
    e.preventDefault();
    if (camMode === 'free') { freeMove(-Math.sign(e.deltaY) * freeSpeed() * 0.25, 0, 0); return; }
    orbit.dist = clamp(orbit.dist * (1 + Math.sign(e.deltaY) * 0.12), 5, 6000);
  }, { passive: false });

  $('pick').addEventListener('change', function () { loadReplay(this.value); });

  // ---- open a .dat from disk -------------------------------------------
  var fi = $('file');
  $('b-open').addEventListener('click', function () { fi.click(); });
  fi.addEventListener('change', function () {
    if (this.files && this.files[0]) openFile(this.files[0]);
    this.value = '';
  });

  // ---- open a SaveMGO survival match by link ----------------------------
  var bl = $('b-link');
  if (bl) bl.addEventListener('click', function () {
    var s = prompt('Paste a SaveMGO survival match link (or its number):', '');
    if (s == null) return;
    if (matchId(s) == null && !isReplayUrl(s)) { warn('that does not look like a survival match link'); return; }
    openMatch(s);
  });
  // pasting a match link anywhere on the page opens it too
  addEventListener('paste', function (e) {
    var tg = e.target && e.target.tagName;
    if (tg === 'INPUT' || tg === 'TEXTAREA') return;
    var s = e.clipboardData && e.clipboardData.getData('text');
    if (s && ((/mgo2pc\.com/i.test(s) && matchId(s) != null) || isReplayUrl(s))) { e.preventDefault(); openMatch(s); }
  });

  var dz = $('drop'), depth = 0;
  addEventListener('dragenter', function (e) {
    e.preventDefault(); depth++; dz.className = 'on';
  });
  addEventListener('dragover', function (e) { e.preventDefault(); });
  addEventListener('dragleave', function (e) {
    e.preventDefault(); if (--depth <= 0) { depth = 0; dz.className = ''; }
  });
  addEventListener('drop', function (e) {
    e.preventDefault(); depth = 0; dz.className = '';
    var f = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
    if (f) openFile(f);
  });
}

// Parse a user-supplied replay in the browser. Everything stays on this
// machine -- the file is never uploaded anywhere.
function openFile(f) {
  if (typeof RPDT === 'undefined') {
    warn('this build has no parser — reload the page');
    return;
  }
  var name = f.name;
  warn('reading ' + name + '…');
  var read = f.arrayBuffer ? f.arrayBuffer() : new Promise(function (res, rej) {
    var fr = new FileReader();
    fr.onload = function () { res(fr.result); };
    fr.onerror = function () { rej(new Error('could not read ' + name)); };
    fr.readAsArrayBuffer(f);
  });
  return read.then(function (buf) {
    // SaveMGO hands replays out zipped (<room>_<id>.dat.zip); take the .dat inside
    return unzipDat(buf).then(function (z) {
      if (z.name && !f.keepName) name = z.name;
      return z.buf;
    });
  }).then(function (buf) {
    warn('parsing ' + name + '…');
    return new Promise(function (res) { setTimeout(function () { res(buf); }, 20); });
  }).then(function (buf) {
    var rep = RPDT.parse(buf, name, { allEvents: true, modeHint: f.modeHint, mapHint: f.mapHint });
    // SaveMGO sometimes links a match to the recording of a DIFFERENT game (4 of 119 audited,
    // all ids 49390-50042: other map, other mode, none of the match's players). Say so.
    if (f.expectNames && f.expectNames.length >= 4) {
      var inRep = {}, nNamed = 0;
      rep.players.forEach(function (pl) { if (pl.name) { inRep[pl.name] = 1; nNamed++; } });
      // A room's regulars carry over, so 'nobody in common' is too strict (49390 shares 1 of 12,
      // 49879 shares 3). A real recording misses at most 1 listed player (10 of 119 audited).
      var nHit = f.expectNames.filter(function (nm) { return inRep[nm]; }).length;
      var mapDiffers = f.mapHint != null && rep.meta && !rep.meta.mapFromHint && rep.meta.mapId && rep.meta.mapId !== f.mapHint;
      if (nNamed >= 4 && (mapDiffers || nHit * 2 < f.expectNames.length)) rep.__wrongGame = true;
    }
    if (!rep.frames.length) throw new Error(name + ': no position data in this file');
    var key = name;
    REPLAYS[key] = rep;
    var sel = $('pick');
    if (!Array.prototype.some.call(sel.options, function (o) { return o.value === key; })) {
      var opt = document.createElement('option');
      opt.value = key;
      opt.textContent = name + '  · ' + fmt(rep.duration);
      sel.insertBefore(opt, sel.firstChild);
    }
    sel.value = key;
    loadReplay(key);
    return true;
  }).catch(function (err) {
    warn(err && err.message ? err.message : 'could not open ' + name);
    return false;
  });
}

// A zip's first .dat member, inflated; anything that is not a zip passes
// through untouched. Reads the central directory, so sizes are always known.
function unzipDat(buf) {
  var u = new Uint8Array(buf), dv = new DataView(buf);
  if (u.length < 22 || dv.getUint32(0, true) !== 0x04034b50) return Promise.resolve({ buf: buf });
  var e = u.length - 22, stop = Math.max(0, u.length - 22 - 65535);
  while (e >= stop && dv.getUint32(e, true) !== 0x06054b50) e--;
  if (e < stop) return Promise.reject(new Error('damaged zip'));
  var n = dv.getUint16(e + 10, true), p = dv.getUint32(e + 16, true), pick = null;
  for (var i = 0; i < n && p + 46 <= u.length; i++) {
    if (dv.getUint32(p, true) !== 0x02014b50) break;
    var nl = dv.getUint16(p + 28, true);
    var ent = { method: dv.getUint16(p + 10, true), csize: dv.getUint32(p + 20, true),
                local: dv.getUint32(p + 42, true),
                name: new TextDecoder().decode(u.subarray(p + 46, p + 46 + nl)) };
    if (/\.dat$/i.test(ent.name)) { pick = ent; break; }
    if (!pick && ent.csize) pick = ent;
    p += 46 + nl + dv.getUint16(p + 30, true) + dv.getUint16(p + 32, true);
  }
  if (!pick) return Promise.reject(new Error('no replay inside that zip'));
  var d = pick.local + 30 + dv.getUint16(pick.local + 26, true) + dv.getUint16(pick.local + 28, true);
  var raw = u.subarray(d, d + pick.csize), nm = pick.name.replace(/^.*[\\/]/, '');
  if (pick.method === 0) {
    return Promise.resolve({ buf: raw.buffer.slice(raw.byteOffset, raw.byteOffset + raw.byteLength), name: nm });
  }
  if (pick.method !== 8 || typeof DecompressionStream === 'undefined') {
    return Promise.reject(new Error('this browser cannot unzip the replay — unzip it and open the .dat'));
  }
  return new Response(new Blob([raw]).stream().pipeThrough(new DecompressionStream('deflate-raw')))
    .arrayBuffer().then(function (b) { return { buf: b, name: nm }; });
}

// ── open a SaveMGO survival match straight from its link ─────────────────
// SaveMGO API v2 (2026-10-06; v1 is being removed): mgo2pc.com/v2/survival/<id> ->
// GET /api/v2/survival/matches/<id> = match JSON with team names and `replayUrl`
// (the zipped .dat under /replays/). Fetched by this browser, parsed here. The
// site's own "View in browser" button opens this page with ?match=<id>.
var MATCH_API = 'https://mgo2pc.com/api/v2/';
var MATCH_API_V1 = 'https://mgo2pc.com/api/v1/';     // fallback only, while it still answers
var MATCH_RELAY = 'https://eovsstone.com/api/mgo2-match/';   // same record, via this site's Pages Function
// Replay files are moving to a CDN (TriggerHappy, 2026-10-06): cdn.mgo2.net/replays/<file>.
// The match JSON may still name the old mgo2pc.com/replays/ path (which has no cross-site
// permission for this page and answers a missing file with an HTML page), so every
// replay URL is tried as its CDN spelling first, then exactly as given.
var REPLAY_CDN = 'https://cdn.mgo2.net/replays/';
function replayUrls(u) {
  u = String(u == null ? '' : u).trim();
  var out = [], m = /\/replays\/([^\/?#]+\.dat(?:\.zip)?)$/i.exec(u);
  if (m) out.push(REPLAY_CDN + m[1]);
  if (u && out.indexOf(u) < 0) out.push(u);
  return out;
}
function isReplayUrl(s) { return /^https?:\/\/[^\s]+\/replays\/[^\/\s]+\.dat(?:\.zip)?$/i.test(String(s == null ? '' : s).trim()); }
function looksLikeReplay(b) {            // zip ('PK') or bare RPDT, never an HTML error page
  if (!b || b.byteLength < 64) return false;
  var u = new Uint8Array(b, 0, 4);
  return (u[0] === 0x50 && u[1] === 0x4b) || (u[0] === 0x52 && u[1] === 0x50 && u[2] === 0x44 && u[3] === 0x54);
}
function fetchReplay(urls, what) {       // first URL that answers with a real replay wins
  var i = 0;
  function next(lastErr) {
    if (i >= urls.length) throw lastErr || new Error('SaveMGO has no replay for ' + what);
    return fetch(urls[i++]).then(function (r) {
      if (!r.ok) throw new Error('SaveMGO has no replay for ' + what + ' (' + r.status + ')');
      return r.arrayBuffer();
    }).then(function (b) {
      if (!looksLikeReplay(b)) throw new Error('SaveMGO has no replay for ' + what);
      return b;
    }).catch(function (e) { return next(e); });
  }
  return next(null);
}
function matchId(s) {
  s = String(s == null ? '' : s).trim();
  var m = /(?:survivalmatch|download-replay|svmatchresult|survival\/matches|survival)\/(\d+)/i.exec(s) ||
          /^#?(\d{1,9})$/.exec(s) || (isReplayUrl(s) ? null : /_(\d+)\.dat(?:\.zip)?$/i.exec(s));
  return m ? +m[1] : null;
}
function openMatch(s) {
  // a direct replay link (cdn.mgo2.net/replays/... or the old mgo2pc.com/replays/...) opens as is
  if (isReplayUrl(s)) {
    var fname = String(s).trim().replace(/^.*\//, '');
    warn('fetching ' + fname + '…');
    return fetchReplay(replayUrls(s), fname).then(function (b) {
      return openFile({ name: fname, arrayBuffer: function () { return Promise.resolve(b); } });
    }).catch(function (err) {
      warn(err && err.message && !/fetch/i.test(err.message) ? err.message : 'could not fetch ' + fname);
      return false;
    });
  }
  var id = matchId(s);
  if (id == null) { warn('not a survival match link'); return Promise.resolve(false); }
  warn('fetching survival match ' + id + '…');
  // v2: one JSON carries the team names (picker label) and where the replay file is.
  // Measured 2026-10-07: the v2 API answers cross-site reads only for pages on mgo2pc.com,
  // so after the direct try the same record is read through this site's own relay
  // (functions/api/mgo2-match/[id].js), which just forwards that one GET.
  function matchRecord(r, via) {
    if (r.status === 404) throw new Error('SaveMGO has no survival match ' + id);
    if (!r.ok) throw new Error(via + ' ' + r.status);
    return r.json();
  }
  var viaV2 = fetch(MATCH_API + 'survival/matches/' + id).then(function (r) {
    return matchRecord(r, 'v2');
  }).catch(function (e) {
    if (e && /no survival match/.test(e.message)) throw e;
    return fetch(MATCH_RELAY + id).then(function (r) { return matchRecord(r, 'relay'); });
  }).then(function (j) {
    if (!j || !j.replayUrl) throw new Error('SaveMGO has no replay for match ' + id);
    var label = j.redTeam && j.blueTeam ? j.redTeam + ' vs ' + j.blueTeam + ' · ' : '';
    var jn = (j.redPlayers || []).concat(j.bluePlayers || []).map(function (pl) { return pl && pl.name; }).filter(Boolean);
    return fetchReplay(replayUrls(j.replayUrl), 'match ' + id).then(function (b) { return [b, label, j.mode, j.map, jn]; });
  });
  // v1 fallback (same zipped .dat) for as long as the old API is still up
  var viaV1 = function () {
    return fetch(MATCH_API_V1 + 'download-replay/' + id).then(function (r) {
      if (!r.ok) throw new Error('SaveMGO has no replay for match ' + id + ' (' + r.status + ')');
      return r.arrayBuffer();
    }).then(function (b) {
      // since v1 was removed this URL answers with the site's HTML page, status 200
      if (!looksLikeReplay(b)) throw new Error('SaveMGO has no replay for match ' + id);
      return [b, ''];
    });
  };
  return viaV2.catch(function (e2) {
    return viaV1().catch(function () { throw e2; });
  }).then(function (a) {
    if (a[0].byteLength < 64) throw new Error('SaveMGO has no replay for match ' + id);
    return openFile({ name: a[1] + 'match ' + id, keepName: true, modeHint: a[2], mapHint: a[3], expectNames: a[4],
                      arrayBuffer: function () { return Promise.resolve(a[0]); } });
  }).then(function (ok) {
    if (ok) {
      try {
        var u = new URL(location.href);
        u.searchParams.set('match', id);
        u.searchParams.delete('replay');
        history.replaceState(null, '', u);
      } catch (e) {}
    }
    return ok;
  }).catch(function (err) {
    warn(err && err.message && !/fetch/i.test(err.message) ? err.message
       : 'could not reach SaveMGO for match ' + id);
    return false;
  });
}

// ── mobile panel toggles ─────────────────────────────────────────────────
function wireMobile() {
  var bar = document.createElement('div');
  bar.id = 'mtabs';
  bar.innerHTML =
    '<button class="tbtn wide" data-panel="roster">Roster</button>' +
    '<button class="tbtn wide" data-panel="feedpane">Feed</button>';
  $('bar').insertBefore(bar, $('bar').firstChild);
  bar.addEventListener('click', function (e) {
    var b = e.target.closest('[data-panel]');
    if (!b) return;
    var el = $(b.dataset.panel), on = el.dataset.open === '1';
    document.querySelectorAll('.pane[data-open]').forEach(function (p) { p.dataset.open = '0'; });
    bar.querySelectorAll('[data-panel]').forEach(function (o) { o.dataset.on = '0'; });
    el.dataset.open = on ? '0' : '1';
    b.dataset.on = on ? '0' : '1';
  });
  $('roster').dataset.open = '0';
  $('feedpane').dataset.open = '0';
}

// ── loading ──────────────────────────────────────────────────────────────
var MAPS = {}, REPLAYS = {}, MAPURL = {}, REPURL = {}, MAPBOUNDS = {}, MAPNAME = {
  n001a: 'Forest Firefight', n002a: 'Ambush Alley', n003a: 'Urban Ultimatum',
  n004a: 'Groznyj Grad', n005a: 'Coppertown', n006a: 'Hazard House',
  n007a: 'Blood Bath', n008a: 'Tomb of Tubes', n009a: 'Ravaged Riverfront',
  n010a: 'Silo Sunset', n018a: 'Icebound Inferno', n012a: 'Midtown',
  n013a: 'Winter Warehouse', n014a: 'Virtuous Vista', n015a: 'Outer Outlet',
  n022a: 'Quaint Quarrel', n023a: 'Jade Junction',
  n020a: 'Eastern Europe', sm_dd: 'Desert Duel', sm_ll: 'Lethal Leviathan'
};

function boot(msg, pct) {
  var m = $('boot-msg'), f = $('boot-fill');
  if (m && msg) m.innerHTML = msg;
  if (f && pct != null) f.style.width = pct + '%';
}
function bootDone() {
  var b = $('boot');
  if (b) b.remove();
}
function warn(msg) {
  var w = $('warn');
  if (!msg) { w.className = ''; return; }
  w.textContent = msg;
  w.className = 'on';
}

// Two delivery modes. The single-file build embeds everything; the hosted
// build ships a manifest and fetches each replay and stage on demand, so a
// phone on cellular pulls ~1 MB instead of ~10 MB.
// Which stage do these tracks actually belong to?
// A replay's declared mapId is not always usable — one sample carries 265 —
// so when the declared stage is missing or its box does not contain the
// tracks, score every stage's bounding box and take the winner. Bounds ship in
// the manifest (a couple of KB), so this costs no mesh downloads.
var CUSTOM_STAGE = /^n09\da$/;
function noScenery(rep) {
  return !!(rep && !rep.stage && rep.stageGuess && rep.stageGuess !== '(unknown)');
}
function fitStage(rep, declared) {
  var pts = [], fr = rep.frames || [];
  var step = Math.max(1, Math.floor(fr.length / 400));
  for (var i = 0; i < fr.length; i += step) {
    var row = fr[i].p || [];
    for (var k = 0; k < row.length; k++) if (row[k]) pts.push(row[k]);
    if (pts.length > 2500) break;
  }
  if (!pts.length) return declared;
  // A map the file DOES name but the viewer has no scenery for (SaveMGO's map 22,
  // Eastern Europe) is drawn with no stage at all: its tracks sit inside five other
  // maps' boxes, and match 69003 came up as Ambush Alley.
  if (!declared && noScenery(rep)) return null;
  function frac(st) {
    var b = MAPBOUNDS[st];
    if (!b) return -1;
    var lo = b.min, hi = b.max, ok = 0;
    for (var q = 0; q < pts.length; q++) {
      var e = pts[q];
      if (e[0] >= lo[0] && e[0] <= hi[0] && e[2] >= lo[2] && e[2] <= hi[2]
          && e[1] >= lo[1] - 1500 && e[1] <= hi[1] + 1500) ok++;
    }
    return ok / pts.length;
  }
  var df = declared ? frac(declared) : -1;
  if (df >= 0.85) return declared;
  var best = null, bf = -1, ties = 0;
  for (var st in MAPBOUNDS) {
    // the remix / custom stages (n091a..n099a) are only ever shown for a replay that
    // names them; a real match is never matched to one by position
    if (CUSTOM_STAGE.test(st)) continue;
    var f = frac(st);
    if (f > bf) { bf = f; best = st; ties = 1; }
    else if (f === bf) ties++;
  }
  // several boxes holding the tracks equally well is not an identification
  if (bf >= 0.85 && bf > df && ties === 1) {
    warn('stage not declared correctly — tracks fit ' +
         (MAPNAME[best] || best) + ' (' + Math.round(bf * 100) + '% in bounds)');
    return best;
  }
  return declared;
}

// Per-hit damage: R.dmg = [[t, attacker (-1 unknown), victim, amount, weaponId,
// ix, iy, iz], ...], LETHAL-class hits only, amount in health-byte units (250 =
// one full health bar; measured 240-305 summed per death over 5 replays).
// Hosted replays carry it ready-made (inject_damage.py); a dropped .dat is
// parsed with allEvents, and its 'damage' rows are moved out of R.events here
// so the kill feed and timeline never see them.
function buildDamage(r) {
  if (!r || !r.events) return;
  var rows = [], keep = [], had = false;
  r.events.forEach(function (e) {
    if (e.type !== 'damage') { keep.push(e); return; }
    had = true;
    if (e.damageClass !== 'lethal' || e.victim == null) return;
    var im = e.impact || [];
    rows.push([e.t, e.killer == null ? -1 : e.killer, e.victim, e.amount, e.weapon,
               im[0], im[1], im[2], e.subtype, e.bearing == null ? null : e.bearing]);
  });
  if (had) { r.events = keep; if (!r.dmg) r.dmg = rows; }
}

function loadReplay(key) {
  if (!REPLAYS[key] && REPURL[key]) {
    warn('loading ' + key + '…');
    fetch(REPURL[key]).then(function (x) { return x.json(); })
      .then(function (rep) { REPLAYS[key] = rep; loadReplay(key); })
      .catch(function () { warn('could not load ' + key); });
    return;
  }
  var r0 = REPLAYS[key];
  if (!r0) return;
  var stf = r0.stage ? String(r0.stage).split('__')[0] : null;
  var stg = r0.stage ? String(r0.stage).split('__').pop() : null;
  var declared = (stf && MAPBOUNDS[stf]) ? stf : ((stg && MAPBOUNDS[stg]) ? stg : null);
  if (r0.__fit === undefined) r0.__fit = fitStage(r0, declared);
  var need = r0.__fit && MAPURL[r0.__fit] ? r0.__fit
           : ((stf && MAPURL[stf]) ? stf : ((stg && MAPURL[stg]) ? stg : null));
  if (need && !MAPS[need]) {
    warn('loading ' + (MAPNAME[need] || need) + ' geometry…');
    fetch(mapUrlV(MAPURL[need])).then(function (x) { return x.json(); })
      .then(function (m) { MAPS[need] = m; applyReplay(key); })
      .catch(function () { applyReplay(key); });
    return;
  }
  applyReplay(key);
}

function applyReplay(key) {
  var r = REPLAYS[key];
  if (!r) return;
  R = r;
  try {
    if (typeof location !== 'undefined' &&
        new URLSearchParams(location.search).get('rolls') === 'raw') {
      ROLL_SMOOTH = false;
    }
  } catch (e) { /* no URL: keep the default */ }
  smoothRolls(R);
  buildDistance(R);
  buildProne(R);
  try { buildCqc(R); } catch (errC) { R.cqcThrows = []; }
  try { buildScans(R); } catch (errS) { R.scans = []; }
  try { buildBox(R); } catch (errB) { R.boxRuns = []; }
  try { buildLadders(R); } catch (errL) { R.ladderBy = []; }
  buildExtras(R);          // before buildDamage: it reads the gas damage rows
  buildDamage(R);
  buildNades(R);
  try { buildInsights(R); } catch (e) { console.warn('insights off:', e); }
  try { window.__PRONESTATS = R.proneStats; } catch (e) {}
  T = 0; playing = false; follow = -1; feedShown = -1;
  // DEEP LINKS. ?t=<seconds> lands the playhead on a moment and ?follow=<name>
  // picks a player by name (or index), so a specific thing can be pointed at
  // instead of described. Applied after the reset above, or it would be undone.
  try {
    var qs_ = new URLSearchParams(location.search);
    var tq_ = parseFloat(qs_.get('t'));
    if (!isNaN(tq_) && tq_ >= 0) T = Math.min(tq_, R.duration || tq_);
    var fq_ = qs_.get('follow');
    if (fq_ != null && fq_ !== '') {
      var want_ = String(fq_).trim().toLowerCase();
      var names_ = R.players || [];
      for (var k_ = 0; k_ < names_.length; k_++) {
        var nm_ = names_[k_] && (names_[k_].name || names_[k_]);
        if (nm_ != null && String(nm_).trim().toLowerCase() === want_) { follow = k_; break; }
      }
      if (follow < 0 && /^\d+$/.test(want_)) follow = parseInt(want_, 10);
      if (follow >= 0) camMode = 'follow';
    }
    if (qs_.get('play') === '1') playing = true;
  } catch (e) { /* no URL parameters: leave the defaults */ }
  syncPlay();
  // heading: degrees from the real parser, radians from the mock, u16 packed
  // if it ever appears. meta wins; otherwise infer from the observed range.
  var hu = (R.meta && R.meta.headingUnits || '').toLowerCase();
  if (hu.indexOf('degree') >= 0) headScale = Math.PI / 180;
  else if (hu.indexOf('radian') >= 0) headScale = 1;
  else {
    var mx = 0;
    for (var fi = 0; fi < R.frames.length; fi += Math.max(1, R.frames.length >> 6)) {
      var row = R.frames[fi].p || [];
      for (var pi = 0; pi < row.length; pi++)
        if (row[pi] && row[pi][3] != null) mx = Math.max(mx, Math.abs(row[pi][3]));
    }
    headScale = mx > 400 ? Math.PI * 2 / 65536 : mx > 7 ? Math.PI / 180 : 1;
  }

  var raw = [];
  R.players.forEach(function (p) {
    if (p.team != null && raw.indexOf(p.team) < 0) raw.push(p.team);
  });
  raw.sort(function (a, b) { return a - b; });
  R.players.forEach(function (p) {
    p.teamRaw = p.team;
    p.team = p.team == null ? null : raw.indexOf(p.team);
  });

  var stage = R.stage || null;
  $('b-file').textContent = R.file || key;
  $('b-stage').textContent = R.stageGuess || (stage && MAPNAME[stage]) || stage || 'UNKNOWN STAGE';
  var fitk = R.__fit === undefined ? fitStage(R, stage ? String(stage).split('__')[0] : null) : R.__fit;
  R.__fit = fitk;
  var sk = stage ? String(stage).split('__')[0] : null;
  var sg = stage ? String(stage).split('__').pop() : null;
  var m = (fitk && MAPS[fitk]) || (sk && MAPS[sk]) || (sg && MAPS[sg])
        || (noScenery(R) ? null : MAPS[Object.keys(MAPS)[0]]) || null;
  if (m && fitk && MAPNAME[fitk] && (!stage || fitk !== sk)) {
    $('b-stage').textContent = MAPNAME[fitk];
  }
  labelPos = {};
  buildTimeIndex();
  buildMap(m);
  buildMarkers();
  try { buildHeat(); } catch (e) { console.warn('heat off:', e); }
  // an open scoreboard belongs to the replay it was opened on
  if ($('scorepane') && $('scorepane').dataset.open === '1') { try { renderScore(); } catch (e) {} }
  buildRoster();
  sample(0); score = scoreAt(0); updateRoster(); renderFeed(false);
  buildFollowStrip();
  $('clock').innerHTML = fmt(0) + '<small> / ' + fmt(R.duration) + '</small>';
  var notes = [];
  if (R.__wrongGame) notes.push('SaveMGO linked this match to the recording of a different game (its map or most of its listed players do not match this replay)');
  if (!m && noScenery(R)) notes.push('no scenery for ' + R.stageGuess + ' yet — showing the players only');
  else if (!stage || !MAPS[stage]) notes.push('stage not identified — showing ' + (m ? (m.name || m.stage) : 'no') + ' geometry');
  if (!(R.events || []).some(function (e) { return e.type === 'kill'; })) notes.push('no kill events decoded');
  if (R.unknown && R.unknown.notes && /MOCK|SYNTH/i.test(R.unknown.notes)) notes.unshift('synthetic demo data');
  var wl = R.unknown && R.unknown.warnings;
  if (wl && wl.length) notes.push(wl.length + ' parser caveat' + (wl.length > 1 ? 's' : ''));
  if (R.degenerate) notes.unshift('no position stream in this file');
  warn(notes.length ? notes.join(' · ') : '');
  var q = {};
  (location.hash || '').replace(/^#/, '').split('&').forEach(function (kv) {
    var i = kv.indexOf('=');
    if (i > 0) q[kv.slice(0, i)] = kv.slice(i + 1);
  });
  setCam(q.cam === 'top' || q.cam === 'follow' || q.cam === 'free' ? q.cam : 'orbit');
  if (q.p != null && R.players[+q.p]) selectPlayerSilent(+q.p);
  // camera in the link too: d = distance, az / el = angles in degrees (a close
  // look at one player's pose can then be linked, and checked by screenshot)
  if (q.d && isFinite(+q.d)) orbit.dist = +q.d;
  if (q.az && isFinite(+q.az)) orbit.az = +q.az * Math.PI / 180;
  if (q.el && isFinite(+q.el)) orbit.el = +q.el * Math.PI / 180;
  seek(q.t ? parseFloat(q.t) : 0);
  if (q.dock === 'min' || q.dock === 'full') setDockMin(q.dock === 'min');
  if (q.panel && $('mtabs')) { var pb_ = $('mtabs').querySelector('[data-panel="' + q.panel + '"]'); if (pb_) pb_.click(); }
  if (q.floor) { floorMode = q.floor === 'auto' || q.floor === 'all' ? q.floor : Math.max(0, (+q.floor || 1) - 1); roofSig = ''; }
  else floorMode = null;
  if (q.review === '1') setTimeout(function () { toggleClipReview(true); }, 1500);
  if (/^[123]$/.test(q.fv || '')) setFollowStyle(+q.fv);          // fv=1 shoulder, 2 eyes, 3 chase
  if (q.photo === '1' || q.photo === '2') setPhoto(+q.photo);     // photo=1 tags only, photo=2 clean
  // snap=obj | site1 | site2 | nade: start on a camera subject (see camSubj)
  if (q.snap) {
    var qn = q.snap === 'nade' ? (R.nades || []).filter(function (n) { return T >= n.t && T <= n.t + (n.f || 2); })[0] : null;
    camSubj = q.snap === 'obj' ? { kind: 'obj' } : /^site\d$/.test(q.snap) ? { kind: 'site', k: +q.snap.slice(4) - 1 } : qn ? { kind: 'nade', n: qn } : null;
    if (camSubj) { setCam('follow'); if (q.d && isFinite(+q.d)) orbit.dist = +q.d; if (q.el && isFinite(+q.el)) orbit.el = +q.el * Math.PI / 180; }
    syncSnapBtn();
  }
  if (q.play === '1') { playing = true; syncPlay(); }
  // score=1 opens the scoreboard (a link can then show the board at a moment)
  if (q.score === '1' && $('b-score') && $('b-score').dataset.on !== '1') $('b-score').click();
}

function start(data) {
  var v = $('b-ver');
  if (v) v.textContent = 'build ' + (data.build || '—');
  MAPS = data.maps || {};
  // Phones get the lighter mesh: same map, fewer faces. Chosen on screen
  // size and reported device memory, not on user agent.
  var lite = (Math.min(screen.width, screen.height) < 820)
          || (navigator.deviceMemory && navigator.deviceMemory <= 4);
  MAPURL = (lite && data.mapUrlsLite) ? data.mapUrlsLite : (data.mapUrls || {});
  MAPBOUNDS = data.mapBounds || {};
  REPLAYS = {};
  REPURL = {};
  var keys = [], labels = {};
  if (Array.isArray(data.replays)) {                 // hosted manifest
    data.replays.forEach(function (m) {
      keys.push(m.key);
      REPURL[m.key] = m.url;
      labels[m.key] = (m.file || m.key) + '  · ' + fmt(m.duration) +
        (m.stageGuess ? '  · ' + m.stageGuess : '');
    });
  } else {                                           // embedded payload
    REPLAYS = data.replays || {};
    keys = Object.keys(REPLAYS);
    keys.forEach(function (k) {
      labels[k] = (REPLAYS[k].file || k) + '  · ' + fmt(REPLAYS[k].duration);
    });
  }
  if (!keys.length) {
    boot('No replay data found.', 100);
    return;
  }
  var sel = $('pick');
  sel.innerHTML = keys.map(function (k) {
    return '<option value="' + esc(k) + '">' + esc(labels[k]) + '</option>';
  }).join('');
  initGL();
  loadFigure();
  hudCtx = $('hud').getContext('2d');
  sizeTimeline();
  wireControls();
  wireMobile();
  syncPlay();
  // ?replay=<substring> opens a specific file, case-insensitive, so a link can
  // point at a moment in a named replay rather than whichever loads first.
  var want0 = null;
  try { want0 = new URLSearchParams(location.search).get('replay'); } catch (e) {}
  var key0 = keys[0];
  if (want0) {
    var w0 = String(want0).trim().toLowerCase();
    for (var q0 = 0; q0 < keys.length; q0++) {
      if (String(keys[q0]).toLowerCase().indexOf(w0) >= 0) { key0 = keys[q0]; break; }
    }
    var sel0 = $('pick');
    if (sel0) sel0.value = key0;
  }
  // ?match=<SaveMGO survival match id or link> fetches that replay instead; a
  // match that will not load falls back to the first hosted one.
  var match0 = null;
  try { match0 = new URLSearchParams(location.search).get('match'); } catch (e) {}
  if (match0 && (matchId(match0) != null || isReplayUrl(match0))) {
    openMatch(match0).then(function (ok) {
      if (ok) return;
      var why = $('warn').textContent;
      loadReplay(key0);
      if (why) setTimeout(function () { warn(why); }, 2500);
    });
    return;
  }
  loadReplay(key0);
}

// ── entry: embedded payload, or fetch for local dev ──────────────────────
function inflate(b64) {
  var bin = atob(b64), n = bin.length, u = new Uint8Array(n);
  for (var i = 0; i < n; i++) u[i] = bin.charCodeAt(i);
  if (typeof DecompressionStream === 'undefined') {
    return Promise.reject(new Error('no DecompressionStream'));
  }
  var ds = new DecompressionStream('gzip');
  return new Response(new Blob([u]).stream().pipeThrough(ds)).json();
}

function main() {
  if (window.__MGO2_GZ__) {
    boot('Decompressing telemetry…', 40);
    inflate(window.__MGO2_GZ__).then(function (d) {
      boot('Building stage geometry…', 80);
      setTimeout(function () { start(d); bootDone(); requestAnimationFrame(tick); }, 30);
    }).catch(function (e) {
      boot('Could not read the embedded data.<br>' + esc(e.message), 100);
    });
    return;
  }
  if (window.__MGO2_DATA__) {
    start(window.__MGO2_DATA__); bootDone(); requestAnimationFrame(tick); return;
  }
  if (window.__MGO2_MANIFEST__) {
    boot('Loading…', 40);
    fetch(window.__MGO2_MANIFEST__).then(function (r) { return r.json(); })
      // ?explore=1 adds one empty "EXPLORE <map>" entry per stage to the picker
      // (replay/make_explore.py): no recording, just the map to fly around in.
      .then(function (ix) {
        if (!/[?&]explore=1/.test(location.search)) return ix;
        return fetch('data/explore.json?v=' + Date.now()).then(function (r) { return r.ok ? r.json() : null; })
          .then(function (ex) {
            if (ex && ex.replays) ix.replays = ex.replays.concat(ix.replays || []);
            return ix;
          }).catch(function () { return ix; });
      })
      .then(function (ix) { start(ix); bootDone(); requestAnimationFrame(tick); })
      .catch(function (e) { boot('Could not load the replay index.<br>' + esc(e.message), 100); });
    return;
  }
  // local dev: fetch loose JSON
  boot('Fetching local JSON…', 30);
  fetch('data/index.json').then(function (r) { return r.json(); }).then(function (ix) {
    var jobs = [];
    var out = { maps: {}, replays: {} };
    (ix.maps || []).forEach(function (f) {
      jobs.push(fetch('data/' + f).then(function (r) { return r.json(); })
        .then(function (m) { out.maps[m.stage] = m; }));
    });
    (ix.replays || []).forEach(function (f) {
      jobs.push(fetch('data/' + f).then(function (r) { return r.json(); })
        .then(function (m) { out.replays[f] = m; }));
    });
    return Promise.all(jobs).then(function () {
      start(out); bootDone(); requestAnimationFrame(tick);
    });
  }).catch(function (e) {
    boot('No data found.<br>Run <code>mock.py</code> or the real parser first.<br>' + esc(e.message), 100);
  });
}

window.__openFileForTest = openFile;
// Headless checks (animcat/viewer/cdp_shot.py) read and drive the viewer through this.
window.__mgo2T = function (js) { return eval(js); };

if (document.readyState === 'loading') addEventListener('DOMContentLoaded', main);
else main();

})();
