"use strict";

/** Progress and manifest half of the Survival replay downloader. */

const fs = require("fs");
const fsp = fs.promises;
const path = require("path");
const { API } = require("./mgo2_replays_network_utils.js");

function isoNow() {
  return new Date().toISOString();
}

/** Name a replay keeps from its URL, or a match-id fallback when it has none. */
function fileNameOf(match) {
  if (!match.replayUrl) {
    return null;
  }
  try {
    const name = path.basename(new URL(match.replayUrl).pathname);
    return name && name !== "/" ? name : `replay_${match.id}.dat.zip`;
  } catch {
    return `replay_${match.id}.dat.zip`;
  }
}

/** Match ids the manifest already records, ignoring failed entries and torn lines. */
async function loadManifest(file) {
  const completed = new Set();
  try {
    const text = await fsp.readFile(file, "utf8");
    for (const line of text.split("\n")) {
      if (!line.trim()) {
        continue;
      }
      try {
        const entry = JSON.parse(line);
        if (entry.id !== undefined && entry.status !== "failed") {
          completed.add(Number(entry.id));
        }
      } catch {
        // A line cut off by a crash was never a completed match.
      }
    }
  } catch (error) {
    if (error.code !== "ENOENT") {
      throw error;
    }
  }
  return completed;
}

async function appendManifest(file, entry) {
  await fsp.appendFile(file, `${JSON.stringify(entry)}\n`);
}

function freshProgress() {
  return {
    version: 1,
    createdAt: isoNow(),
    updatedAt: isoNow(),
    api: { url: API, total: null, pageSize: null, pages: null },
    cursor: { page: 1, index: 0, matches: null },
    counts: { downloaded: 0, existing: 0, skipped: 0, failed: 0, bytes: 0 },
    last: null,
    failures: [],
  };
}

async function loadProgress(file) {
  try {
    const progress = JSON.parse(await fsp.readFile(file, "utf8"));
    if (progress.version !== 1 || !progress.cursor || !progress.counts) {
      throw new Error(`unrecognised progress file ${file}`);
    }
    return progress;
  } catch (error) {
    if (error.code === "ENOENT") {
      return freshProgress();
    }
    throw error;
  }
}

async function saveProgress(file, progress) {
  progress.updatedAt = isoNow();
  const temporary = `${file}.tmp`;
  await fsp.writeFile(temporary, `${JSON.stringify(progress, null, 2)}\n`);
  await fsp.rename(temporary, file);
}

function pageMatches(body) {
  return body.matches.map((match) => ({
    id: match.id,
    map: match.map,
    mode: match.mode,
    endedAt: match.endedAt,
    replayUrl: match.replayUrl,
  }));
}

function updateApi(progress, body) {
  if (typeof body.total === "number") {
    progress.api.total = body.total;
  }
  if (typeof body.pageSize === "number" && body.pageSize > 0) {
    progress.api.pageSize = body.pageSize;
  }
  if (progress.api.total && progress.api.pageSize) {
    progress.api.pages = Math.ceil(progress.api.total / progress.api.pageSize);
  }
}

function recordFailure(progress, match, error, attempts) {
  progress.counts.failed++;
  const failure = {
    id: match.id,
    url: match.replayUrl,
    error: error.message,
    attempts,
    at: isoNow(),
  };
  progress.failures = progress.failures.filter((entry) => entry.id !== match.id);
  progress.failures.push(failure);
  progress.last = { ...failure, status: "failed" };
  return failure;
}

module.exports = {
  appendManifest,
  fileNameOf,
  isoNow,
  loadManifest,
  loadProgress,
  pageMatches,
  recordFailure,
  saveProgress,
  updateApi,
};
