#!/usr/bin/env node
"use strict";

/**
 * Parse an MGO2 RPDT replay with the replay console's own decoder.
 *
 * This replaces the hand-written Python walk of the record stream. The record
 * framing, roster identity fit, position cells, kill feed and damage records are
 * all read by `docs/analysis/replay-console/rpdt.js`, the copy of the decoder the
 * SaveMGO replay console itself loads, so there is one reading of the format in
 * the repository rather than two.
 *
 * USAGE
 *   node tools/mgo2_replay_parser.js <replay.dat|.dat.zip>        # summary + events
 *   node tools/mgo2_replay_parser.js <replay.dat|.dat.zip> --show=kill,damage
 *   node tools/mgo2_replay_parser.js <replay.dat|.dat.zip> --show=pos  # every position frame
 *   node tools/mgo2_replay_parser.js <replay.dat|.dat.zip> --events=50 # cap the listing
 *   node tools/mgo2_replay_parser.js <replay.dat|.dat.zip> --json      # parsed JSON, no frames
 *
 * Event kinds: KILL, HIT (damage), STAM (stamina restore), STUN, THROW, OBJ,
 * and any other event `rpdt.js` names. POS prints one line per position frame.
 *
 * The Python parser inferred an attacker from the same tick's aim record
 * because the stream was read by hand. `rpdt.js` names the killer from the
 * match's own 0x1254 op-0x09 kill feed, so KILL rows carry the real killer and
 * `--show=fire` is no longer a separate event, as there is no fire record in
 * the decoder's event list.
 */

const fs = require("fs");
const path = require("path");
const zlib = require("zlib");
const { RPDT } = require("../docs/analysis/replay-console/rpdt.js");

const EVENT_KIND = {
  kill: "KILL",
  damage: "HIT",
  staminaRestore: "STAM",
  stun: "STUN",
  throw: "THROW",
  objective: "OBJ",
};

function usage(message) {
  if (message) {
    console.error(message);
  }
  console.error("usage: node tools/mgo2_replay_parser.js <replay.dat> [--show=kills,damage,...] [--events=N] [--json]");
  process.exit(2);
}

function parseArguments(argv) {
  const files = [];
  let show = ["all"];
  let maxEvents = 0;
  let json = false;

  for (const argument of argv) {
    if (argument.startsWith("--show=")) {
      show = argument.slice("--show=".length).split(",").map((value) => value.trim()).filter(Boolean);
    } else if (argument.startsWith("--events=")) {
      maxEvents = Number.parseInt(argument.slice("--events=".length), 10);
      if (!Number.isFinite(maxEvents) || maxEvents < 0) {
        usage(`invalid --events value: ${argument}`);
      }
    } else if (argument === "--json") {
      json = true;
    } else if (argument.startsWith("-")) {
      usage(`unknown option: ${argument}`);
    } else {
      files.push(argument);
    }
  }

  if (files.length === 0) {
    usage("no replay file given");
  }
  return { files, show, maxEvents, json };
}

/** Pull the single `.dat` out of the zip the replay API serves. */
function unzipFirstEntry(archive, file) {
  if (archive.length < 30 || archive.readUInt32LE(0) !== 0x04034b50) {
    throw new Error(`${file}: not an RPDT replay or a replay zip`);
  }
  const method = archive.readUInt16LE(8);
  const compressedSize = archive.readUInt32LE(18);
  const nameLength = archive.readUInt16LE(26);
  const extraLength = archive.readUInt16LE(28);
  const start = 30 + nameLength + extraLength;
  if (compressedSize === 0 || start + compressedSize > archive.length) {
    throw new Error(`${file}: unsupported zip layout (streamed sizes)`);
  }
  const compressed = archive.subarray(start, start + compressedSize);
  if (method === 0) {
    return compressed;
  }
  if (method === 8) {
    return zlib.inflateRawSync(compressed);
  }
  throw new Error(`${file}: unsupported zip compression method ${method}`);
}

function readReplay(file) {
  const stored = fs.readFileSync(file);
  const buffer = stored.toString("latin1", 0, 4) === "RPDT" ? stored : unzipFirstEntry(stored, file);
  if (buffer.length < 4 || buffer.toString("latin1", 0, 4) !== "RPDT") {
    throw new Error(`${file}: not an RPDT replay`);
  }
  const arrayBuffer = buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength);
  return {
    storedBytes: stored.length,
    replay: RPDT.parse(arrayBuffer, path.basename(file), { allEvents: true }),
  };
}

function wants(show, kind) {
  return show.includes("all") || show.includes(kind);
}

function playerLabel(replay, index) {
  if (index === null || index === undefined) {
    return "?";
  }
  const player = replay.players[index];
  if (!player) {
    return `player ${index}`;
  }
  return `${player.name || `player ${index}`}${player.teamGroup === null ? "" : ` [${player.teamGroup === 0 ? "red" : "blue"}]`}`;
}

function eventDetail(replay, event) {
  const parts = [];
  if (event.type === "kill") {
    parts.push(event.suicide ? `victim ${playerLabel(replay, event.victim)} (suicide)` : `${playerLabel(replay, event.killer)} -> ${playerLabel(replay, event.victim)}`);
    if (event.weaponName) {
      parts.push(event.weaponName);
    }
    if (event.cause && event.cause !== "combat") {
      parts.push(event.cause);
    }
  } else if (event.type === "damage") {
    const who = event.killer === null || event.killer === undefined ? "?" : playerLabel(replay, event.killer);
    parts.push(`${who} -> ${playerLabel(replay, event.victim)}`);
    if (event.amount !== null && event.amount !== undefined) {
      parts.push(`${event.amount} dmg`);
    }
    if (event.weaponName) {
      parts.push(event.weaponName);
    }
  } else if (event.type === "throw") {
    parts.push(playerLabel(replay, event.killer));
    if (event.weaponName) {
      parts.push(event.weaponName);
    }
  } else if (event.type === "objective") {
    parts.push(`item ${event.item}`);
    if (event.carrier !== null && event.carrier !== undefined) {
      parts.push(`carried by ${playerLabel(replay, event.carrier)}`);
    }
  } else {
    for (const key of ["killer", "victim", "carrier", "player"]) {
      if (event[key] !== null && event[key] !== undefined) {
        parts.push(`${key} ${playerLabel(replay, event[key])}`);
      }
    }
    if (event.weaponName) {
      parts.push(event.weaponName);
    }
  }
  return parts.filter(Boolean).join("  ");
}

function headerLines(file, storedBytes, replay) {
  const meta = replay.meta;
  const decoded = storedBytes === meta.fileBytes
    ? ""
    : `, ${meta.fileBytes.toLocaleString("en-US")} bytes decoded`;
  return [
    `=== ${file} (${storedBytes.toLocaleString("en-US")} bytes${decoded}) ===`,
    `magic            : RPDT`,
    `map id           : ${meta.mapId}${meta.mapFromHint ? " (from match record)" : ""}${replay.stage ? `  stage ${replay.stage}` : ""}`,
    `mode             : ${meta.mode === null ? "unresolved" : `${meta.mode} ${meta.modeName || ""}`.trim()}`,
    `duration         : ${replay.duration === null ? "unknown" : `${replay.duration.toFixed(3)} s`}`,
    `tick rate        : ${replay.tickRate === null ? "unknown" : `${replay.tickRate} Hz`}`,
    `sessions         : ${meta.sessions}   records ${meta.records.toLocaleString("en-US")}   frames ${replay.frames.length.toLocaleString("en-US")}`,
    `kills / damage   : ${meta.kills} / ${meta.damageEvents}${meta.killsAttributed !== meta.kills ? ` (${meta.killsAttributed} attributed)` : ""}`,
    `warnings         : ${replay.unknown.warnings.length}`,
    "",
    "  players:",
    ...replay.players.map((player) => {
      const side = player.teamGroup === null ? "-" : player.teamGroup === 0 ? "red" : "blue";
      return `    [${String(player.index).padStart(2, " ")}] ${(player.name || "-").padEnd(16)} side=${side.padEnd(4)} kills=${player.kills} deaths=${player.deaths} samples=${player.positionSamples}`;
    }),
    "",
  ];
}

function positionLines(replay, remaining) {
  const lines = [];
  for (const frame of replay.frames) {
    if (remaining !== 0 && lines.length >= remaining) {
      break;
    }
    const positions = replay.players.map((player, index) => {
      const sample = frame.p[index];
      if (!sample || sample[0] === null) {
        return `${player.name || `player ${index}`}=?`;
      }
      return `${player.name || `player ${index}`}=(${sample[0]},${sample[1]},${sample[2]}) yaw=${Number(sample[3]).toFixed(1)}`;
    });
    lines.push(`  ${String(frame.t).padStart(8)}s  POS  ${positions.join("  ")}`);
  }
  return lines;
}

function eventLines(replay, show, maxEvents) {
  const lines = [];
  for (const event of replay.events) {
    const kind = EVENT_KIND[event.type] || event.type.toUpperCase();
    if (!wants(show, event.type) && !(event.type === "kill" && wants(show, "kill")) &&
        !(event.type === "damage" && (wants(show, "hit") || wants(show, "damage"))) &&
        !(event.type === "staminaRestore" && wants(show, "stam"))) {
      continue;
    }
    if (maxEvents !== 0 && lines.length >= maxEvents) {
      break;
    }
    lines.push(`  ${String(event.t).padStart(8)}s  ${kind.padEnd(5)} ${eventDetail(replay, event)}`);
  }
  return lines;
}

function render(file, storedBytes, replay, options) {
  const lines = headerLines(file, storedBytes, replay);
  const showPositions = options.show.includes("pos") || options.show.includes("position");
  let remaining = options.maxEvents;

  if (showPositions && replay.frames.length > 0) {
    const positions = positionLines(replay, remaining);
    lines.push("== position frames ==", ...positions, "");
    if (remaining !== 0) {
      remaining = Math.max(0, remaining - positions.length);
    }
  }

  const events = eventLines(replay, options.show, remaining);
  lines.push("== events ==", ...(events.length > 0 ? events : ["  (none)"]), "");
  lines.push(
    `  ${replay.meta.records.toLocaleString("en-US")} records; ${replay.meta.kills} kills, ` +
    `${replay.meta.damageEvents} damage, ${replay.meta.damageAttributed} attributed, ` +
    `${replay.frames.length.toLocaleString("en-US")} frames`
  );
  return lines.join("\n");
}

function main() {
  const options = parseArguments(process.argv.slice(2));
  for (const file of options.files) {
    const { storedBytes, replay } = readReplay(file);
    if (options.json) {
      const { frames, ...rest } = replay;
      console.log(JSON.stringify({ ...rest, frameCount: frames.length }, null, 2));
    } else {
      console.log(render(file, storedBytes, replay, options));
      console.log();
    }
  }
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
