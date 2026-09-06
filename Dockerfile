FROM denoland/deno:latest

# Install PostgreSQL 18 from the PGDG apt repo (latest; not in Debian main yet)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/pgdg.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/pgdg.gpg] https://apt.postgresql.org/pub/repos/apt trixie-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends postgresql-18 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Cache dependencies first for better layer reuse.
COPY deno.json deno.lock ./
COPY src ./src
COPY static ./static
COPY drizzle ./drizzle
COPY drizzle.config.ts ./drizzle.config.ts
COPY docker-entrypoint.sh ./docker-entrypoint.sh

RUN deno cache --node-modules-dir=auto src/main.ts

# Expose the ports used by the server.
EXPOSE 80/tcp
EXPOSE 53/udp
EXPOSE 5731/tcp
EXPOSE 5732/tcp
EXPOSE 5733/tcp

ENTRYPOINT ["sh", "docker-entrypoint.sh"]
