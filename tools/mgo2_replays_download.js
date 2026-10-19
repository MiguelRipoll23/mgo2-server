#!/usr/bin/env node
"use strict";

/**
 * Download every completed Survival match replay SaveMGO lists, one at a time.
 *
 * The list is `https://mgo2pc.com/api/v2/survival/matches`, newest first, 20
 * matches a page. Each match's `replayUrl` is the zipped `.dat` the replay
 * console reads. Files are kept in `~/mgo2-replays/replays/`, and everything
 * needed to resume is kept beside them:
 *
 *   ~/mgo2-replays/progress.json   cursor, current page, counters, failures
 *   ~/mgo2-replays/manifest.jsonl  one line per match handled
 *
 * A page is fetched only when the cursor has no page in hand. The cursor is
 * saved after every match, and a file is written to `.part` and renamed only
 * when complete, so an interrupted run resumes at the match it was on. Run the
 * script again after a stop; it skips anything the manifest already records.
 *
 * Downloads are sequential on purpose: one page request, then one replay
 * request at a time. The API answers a page in about 15 seconds, so do not run
 * several copies against it at once.
 *
 * USAGE
 *   node tools/mgo2_replays_download.js
 *   node tools/mgo2_replays_download.js --dir /tmp/replays --limit 2
 *   node tools/mgo2_replays_download.js --start-page 100
 *   node tools/mgo2_replays_download.js --retry-failures
 *
 * OPTIONS
 *   --dir <path>       output directory (default ~/mgo2-replays)
 *   --limit <n>        stop after n matches handled this run
 *   --pages <n>        stop after n pages
 *   --start-page <n>   begin at this page instead of the saved cursor
 *   --retry-failures   retry the failures recorded in progress.json, then exit
 */

const fs = require("fs");
const fsp = fs.promises;
const os = require("os");
const path = require("path");

const { fetchPage, downloadReplay } = require("./mgo2_replays_network_utils.js");
const {
  appendManifest,
  fileNameOf,
  isoNow,
  loadManifest,
  loadProgress,
  pageMatches,
  recordFailure,
  saveProgress,
  updateApi,
} = require("./mgo2_replays_state_utils.js");

const DOWNLOAD_RETRIES = 4;

function usage(message) {
  if (message) {
    console.error(message);
  }
  console.error(
    "usage: node tools/mgo2_replays_download.js [--dir <path>] [--limit <n>] " +
    "[--pages <n>] [--start-page <n>] [--retry-failures]"
  );
  process.exit(2);
}

function parseArguments(argv) {
  const options = {
    dir: path.join(os.homedir(), "mgo2-replays"),
    limit: 0,
    pages: 0,
    startPage: 0,
    retryFailures: false,
  };

  for (let index = 0; index < argv.length; index++) {
    const argument = argv[index];
    const value = () => {
      const next = argv[++index];
      if (next === undefined || next.startsWith("-")) {
        usage(`${argument} needs a value`);
      }
      return next;
    };

    if (argument === "--dir") {
      options.dir = path.resolve(value());
    } else if (argument === "--limit") {
      options.limit = positiveInteger(argument, value());
    } else if (argument === "--pages") {
      options.pages = positiveInteger(argument, value());
    } else if (argument === "--start-page") {
      options.startPage = positiveInteger(argument, value());
    } else if (argument === "--retry-failures") {
      options.retryFailures = true;
    } else if (argument === "--help" || argument === "-h") {
      usage();
    } else {
      usage(`unknown option: ${argument}`);
    }
  }

  return options;
}

function positiveInteger(option, raw) {
  const value = Number.parseInt(raw, 10);
  if (!Number.isFinite(value) || value < 1) {
    usage(`${option} needs a positive integer, got ${raw}`);
  }
  return value;
}

function log(message) {
  console.log(`${new Date().toISOString().slice(11, 19)} ${message}`);
}

async function handleMatch(match, state) {
  const { progress, replaysDir, manifest, completed } = state;
  const file = fileNameOf(match);

  if (completed.has(Number(match.id))) {
    log(`match ${match.id}: already in the manifest, skipping`);
    return;
  }

  if (!file) {
    log(`match ${match.id}: no replayUrl, recording the gap`);
    await appendManifest(manifest, {
      at: isoNow(), id: match.id, status: "skipped", reason: "no replayUrl",
      map: match.map, mode: match.mode, endedAt: match.endedAt,
    });
    completed.add(Number(match.id));
    progress.counts.skipped++;
    progress.last = { id: match.id, status: "skipped", reason: "no replayUrl" };
    return;
  }

  const destination = path.join(replaysDir, file);
  let size = null;
  try {
    size = (await fsp.stat(destination)).size;
    if (size === 0) {
      await fsp.rm(destination, { force: true });
      size = null;
    }
  } catch (error) {
    if (error.code !== "ENOENT") {
      throw error;
    }
  }

  if (size !== null) {
    log(`match ${match.id}: ${file} already on disk (${size.toLocaleString("en-US")} bytes)`);
    await appendManifest(manifest, {
      at: isoNow(), id: match.id, status: "existing", file, bytes: size,
      url: match.replayUrl, map: match.map, mode: match.mode, endedAt: match.endedAt,
    });
    completed.add(Number(match.id));
    progress.counts.existing++;
    progress.counts.bytes += size;
    progress.last = { id: match.id, status: "existing", file, bytes: size };
    return;
  }

  log(`match ${match.id}: downloading ${file}`);
  try {
    size = await downloadReplay(match.replayUrl, destination, log, DOWNLOAD_RETRIES);
  } catch (error) {
    const failure = recordFailure(progress, match, error, DOWNLOAD_RETRIES);
    await appendManifest(manifest, { at: isoNow(), ...failure, status: "failed", map: match.map });
    log(`match ${match.id}: FAILED (${error.message})`);
    return;
  }

  await appendManifest(manifest, {
    at: isoNow(), id: match.id, status: "downloaded", file, bytes: size,
    url: match.replayUrl, map: match.map, mode: match.mode, endedAt: match.endedAt,
  });
  completed.add(Number(match.id));
  progress.counts.downloaded++;
  progress.counts.bytes += size;
  progress.failures = progress.failures.filter((entry) => entry.id !== match.id);
  progress.last = { id: match.id, status: "downloaded", file, bytes: size };
  log(`match ${match.id}: ${file} ${(size / 1024 / 1024).toFixed(1)} MiB`);
}

async function retryFailures(state) {
  const { progress, replaysDir } = state;
  if (progress.failures.length === 0) {
    log("no recorded failures");
    return;
  }

  for (const failure of progress.failures.slice()) {
    const file = fileNameOf({ id: failure.id, replayUrl: failure.url });
    if (!file) {
      continue;
    }
    try {
      const size = await downloadReplay(
        failure.url, path.join(replaysDir, file), log, DOWNLOAD_RETRIES
      );
      await appendManifest(state.manifest, {
        at: isoNow(), id: failure.id, status: "downloaded", file, bytes: size,
        url: failure.url, retried: true,
      });
      state.completed.add(Number(failure.id));
      progress.failures = progress.failures.filter((item) => item.id !== failure.id);
      progress.counts.downloaded++;
      progress.counts.bytes += size;
      log(`match ${failure.id}: retry succeeded (${file})`);
    } catch (error) {
      failure.error = error.message;
      failure.at = isoNow();
      await appendManifest(state.manifest, {
        at: isoNow(), id: failure.id, status: "failed", error: error.message,
        url: failure.url, retried: true,
      });
      log(`match ${failure.id}: retry failed (${error.message})`);
    }
    await saveProgress(state.progressFile, progress);
  }
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const progressFile = path.join(options.dir, "progress.json");
  const manifest = path.join(options.dir, "manifest.jsonl");
  const replaysDir = path.join(options.dir, "replays");

  await fsp.mkdir(replaysDir, { recursive: true });
  const progress = await loadProgress(progressFile);
  const completed = await loadManifest(manifest);

  if (options.startPage > 0) {
    progress.cursor = { page: options.startPage, index: 0, matches: null };
  }

  const state = { progress, progressFile, manifest, replaysDir, completed };
  log(`output ${options.dir}`);
  log(`manifest has ${completed.size} match(es)`);

  if (options.retryFailures) {
    await retryFailures(state);
    await saveProgress(progressFile, progress);
    return;
  }

  let handled = 0;
  let pagesThisRun = progress.cursor.matches ? 1 : 0;

  while (true) {
    if (options.limit > 0 && handled >= options.limit) {
      log(`reached --limit ${options.limit}`);
      break;
    }
    if (options.pages > 0 && pagesThisRun >= options.pages) {
      log(`reached --pages ${options.pages}`);
      break;
    }

    const cursor = progress.cursor;
    if (progress.api.pages && cursor.page > progress.api.pages) {
      log(`done: page ${progress.api.pages} of ${progress.api.pages}`);
      break;
    }

    if (!cursor.matches) {
      log(`fetching page ${cursor.page}`);
      const body = await fetchPage(cursor.page, log);
      updateApi(progress, body);
      cursor.matches = pageMatches(body);
      cursor.index = 0;
      pagesThisRun++;
      await saveProgress(progressFile, progress);
      log(`page ${cursor.page}: ${cursor.matches.length} matches, ${progress.api.total ?? "?"} total`);
    }

    while (cursor.index < cursor.matches.length) {
      if (options.limit > 0 && handled >= options.limit) {
        break;
      }
      const match = cursor.matches[cursor.index];
      await handleMatch(match, state);
      cursor.index++;
      handled++;
      await saveProgress(progressFile, progress);
    }

    if (cursor.index < cursor.matches.length) {
      break;
    }

    if (progress.api.pages && cursor.page >= progress.api.pages) {
      log(`done: page ${cursor.page} of ${progress.api.pages}`);
      cursor.page = progress.api.pages + 1;
      cursor.index = 0;
      cursor.matches = null;
      await saveProgress(progressFile, progress);
      break;
    }

    cursor.page++;
    cursor.index = 0;
    cursor.matches = null;
    await saveProgress(progressFile, progress);
  }

  log(
    `run complete: ${progress.counts.downloaded} downloaded, ${progress.counts.existing} existing, ` +
    `${progress.counts.skipped} skipped, ${progress.counts.failed} failed, ` +
    `${(progress.counts.bytes / 1024 / 1024 / 1024).toFixed(2)} GiB`
  );
  log(`resume with: node tools/mgo2_replays_download.js --dir "${options.dir}"`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack : String(error));
  process.exit(1);
});
