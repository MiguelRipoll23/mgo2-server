import { injectable } from "@needle-di/core";
import { createLogger } from "../../../core/tcp/utils/logger-util.ts";

const LAUNCHER_SERVER = Deno.env.get("LAUNCHER_SERVER") ?? "http://mgo2pc.com";
const LOCAL_FILES_DIR = "./static/files";
const UPSTREAM_FETCH_TIMEOUT_MS = 3000;
const log = createLogger("files");

function getContentType(filePath: string): string {
  if (filePath.endsWith(".zip")) return "application/zip";
  if (filePath.endsWith(".txt")) return "text/plain; charset=utf-8";
  return "application/octet-stream";
}

function buildResponseHeaders(
  filePath: string,
  headers: Headers | Record<string, string>,
): Headers {
  const source = headers instanceof Headers ? headers : new Headers(headers);
  const responseHeaders = new Headers();

  responseHeaders.set(
    "content-type",
    source.get("content-type") ?? getContentType(filePath),
  );

  const contentLength = source.get("content-length");
  if (contentLength !== null) {
    responseHeaders.set("content-length", contentLength);
  }

  const lastModified = source.get("last-modified");
  if (lastModified !== null) {
    responseHeaders.set("last-modified", lastModified);
  }

  const etag = source.get("etag");
  if (etag !== null) {
    responseHeaders.set("etag", etag);
  }

  responseHeaders.set(
    "accept-ranges",
    source.get("accept-ranges") ?? "bytes",
  );

  const server = source.get("server");
  if (server !== null) {
    responseHeaders.set("server", server);
  }

  const date = source.get("date");
  if (date !== null) {
    responseHeaders.set("date", date);
  }

  const connection = source.get("connection");
  if (connection !== null) {
    responseHeaders.set("connection", connection);
  }

  return responseHeaders;
}

async function saveLocalCopy(
  filePath: string,
  data: Uint8Array,
): Promise<void> {
  try {
    await Deno.mkdir(LOCAL_FILES_DIR, { recursive: true });
    await Deno.writeFile(`${LOCAL_FILES_DIR}/${filePath}`, data);
  } catch {
    // Non-fatal: continue serving even if we can't cache locally.
  }
}

async function loadLocalCopy(filePath: string): Promise<Uint8Array | null> {
  try {
    return await Deno.readFile(`${LOCAL_FILES_DIR}/${filePath}`);
  } catch {
    return null;
  }
}

@injectable()
export class FilesService {
  async getFile(
    filePath: string,
    method: "GET" | "HEAD" = "GET",
  ): Promise<Response> {
    try {
      const upstream = await fetch(`${LAUNCHER_SERVER}/files/${filePath}`, {
        method,
        headers: {
          "user-agent": "Mozilla/5.0 (PLAYSTATION 3; 3.55)",
        },
        signal: AbortSignal.timeout(UPSTREAM_FETCH_TIMEOUT_MS),
      });
      if (upstream.ok) {
        if (method === "HEAD") {
          return new Response(null, {
            status: 200,
            headers: buildResponseHeaders(filePath, upstream.headers),
          });
        }

        const data = new Uint8Array(await upstream.arrayBuffer());
        await saveLocalCopy(filePath, data);
        return new Response(data, {
          status: 200,
          headers: buildResponseHeaders(filePath, {
            "content-type": upstream.headers.get("content-type") ??
              getContentType(filePath),
            "content-length": String(data.length),
            ...(upstream.headers.get("last-modified")
              ? { "last-modified": upstream.headers.get("last-modified")! }
              : {}),
            ...(upstream.headers.get("etag")
              ? { etag: upstream.headers.get("etag")! }
              : {}),
            ...(upstream.headers.get("accept-ranges")
              ? { "accept-ranges": upstream.headers.get("accept-ranges")! }
              : {}),
            ...(upstream.headers.get("server")
              ? { server: upstream.headers.get("server")! }
              : {}),
            ...(upstream.headers.get("date")
              ? { date: upstream.headers.get("date")! }
              : {}),
            ...(upstream.headers.get("connection")
              ? { connection: upstream.headers.get("connection")! }
              : {}),
          }),
        });
      }
      log.warn(
        `Upstream file request failed with status ${upstream.status}; falling back to the local copy.`,
      );
    } catch (error) {
      log.warn(
        "Upstream file request failed; falling back to the local copy.",
        error,
      );
    }

    if (method === "HEAD") {
      try {
        const fileInfo = await Deno.stat(`${LOCAL_FILES_DIR}/${filePath}`);
        if (fileInfo.isFile) {
          return new Response(null, {
            status: 200,
            headers: buildResponseHeaders(filePath, {
              "content-length": String(fileInfo.size),
              "accept-ranges": "bytes",
              "server": "nginx/1.18.0",
              "connection": "keep-alive",
            }),
          });
        }
      } catch {
        // Local copy unavailable — handled below.
      }
    } else {
      const local = await loadLocalCopy(filePath);
      if (local !== null) {
        return new Response(local.slice().buffer, {
          status: 200,
          headers: buildResponseHeaders(filePath, {
            "content-type": getContentType(filePath),
            "content-length": String(local.length),
            "accept-ranges": "bytes",
            "server": "nginx/1.18.0",
            "connection": "keep-alive",
          }),
        });
      }
    }

    return new Response(null, { status: 404 });
  }
}
