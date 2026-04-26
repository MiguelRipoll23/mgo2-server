FROM denoland/deno:latest

# Install PostgreSQL 17 (available in Debian trixie main, no PGDG repo needed)
RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-17 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Cache dependencies first for better layer reuse.
COPY deno.json deno.lock ./
COPY src ./src
COPY static ./static
COPY drizzle ./drizzle
COPY docker-entrypoint.sh ./docker-entrypoint.sh

RUN deno cache --node-modules-dir=auto src/main.ts

# Expose the ports used by the server.
EXPOSE 80/tcp
EXPOSE 53/udp
EXPOSE 3478/udp
EXPOSE 5731/tcp
EXPOSE 5732/tcp
EXPOSE 5733/tcp

ENTRYPOINT ["sh", "docker-entrypoint.sh"]
