import { injectable } from "@needle-di/core";

@injectable()
export class VersionService {
  buildCheckVerResponse(): Response {
    return new Response(new Uint8Array([0x00]), {
      status: 200,
      headers: {
        "content-type": "text/html",
        "content-length": "1",
        "server": "nginx/1.18.0",
        "date": "Tue, 17 Mar 2026 18:12:52 GMT",
        "last-modified": "Mon, 21 Feb 2022 06:54:43 GMT",
        "connection": "keep-alive",
        "etag": "\"62133733-1\"",
        "accept-ranges": "bytes",
      },
    });
  }
}
