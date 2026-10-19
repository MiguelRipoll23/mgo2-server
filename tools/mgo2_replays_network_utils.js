"use strict";

/** Network half of the Survival replay downloader: one page, one file at a time. */

const fs = require("fs");
const { Readable } = require("stream");
const { pipeline } = require("stream/promises");

const API = "https://mgo2pc.com/api/v2/survival/matches";
const USER_AGENT = "mgo2-replays-downloader/1.0 (mgo2-server tools)";

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

/**
 * Fetch one 20-match page, retrying transient API errors.
 *
 * @param {number} page page number, 1-based
 * @param {(line: string) => void} log progress sink
 * @param {number} attempts total attempts
 * @returns {Promise<object>} the page body
 */
async function fetchPage(page, log, attempts = 5) {
  let lastError;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const response = await fetch(`${API}?page=${page}`, {
        headers: { "User-Agent": USER_AGENT, Accept: "application/json" },
        signal: AbortSignal.timeout(180_000),
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status} ${response.statusText}`);
      }
      const body = await response.json();
      if (!Array.isArray(body.matches)) {
        throw new Error("the page has no matches array");
      }
      return body;
    } catch (error) {
      lastError = error;
      if (attempt < attempts) {
        const delay = 2 ** (attempt - 1) * 1000;
        log(`page ${page}: ${error.message}; retrying in ${delay / 1000}s`);
        await sleep(delay);
      }
    }
  }
  throw new Error(`page ${page} failed after ${attempts} attempts: ${lastError.message}`);
}

/** Stream one replay to a `.part` file and rename it only when complete. */
async function downloadOnce(url, destination) {
  const temporary = `${destination}.part`;
  const response = await fetch(url, {
    headers: { "User-Agent": USER_AGENT },
    redirect: "follow",
    signal: AbortSignal.timeout(10 * 60 * 1000),
  });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`);
  }
  if (!response.body) {
    throw new Error("the response has no body");
  }

  await pipeline(Readable.fromWeb(response.body), fs.createWriteStream(temporary));
  const size = (await fs.promises.stat(temporary)).size;
  await fs.promises.rename(temporary, destination);
  return size;
}

/**
 * Download one replay, retrying with backoff and discarding a broken part file.
 *
 * @param {string} url replay URL
 * @param {string} destination final file
 * @param {(line: string) => void} log progress sink
 * @param {number} attempts total attempts
 * @returns {Promise<number>} bytes written
 */
async function downloadReplay(url, destination, log, attempts = 4) {
  let lastError;
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      return await downloadOnce(url, destination);
    } catch (error) {
      lastError = error;
      try {
        await fs.promises.rm(`${destination}.part`, { force: true });
      } catch {
        // The next attempt overwrites it; failure to clean up is not the error.
      }
      if (attempt < attempts) {
        const delay = 2 ** (attempt - 1) * 1000;
        log(`retry ${attempt}/${attempts - 1} in ${delay / 1000}s: ${error.message}`);
        await sleep(delay);
      }
    }
  }
  throw lastError;
}

module.exports = { API, USER_AGENT, fetchPage, downloadReplay, sleep };
