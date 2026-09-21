# Build stage. The whole source tree is copied so one Dockerfile can build any
# of the standalone servers; the project to publish and the assembly to run are
# supplied as build arguments.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG PROJECT
ARG ASSEMBLY

WORKDIR /source

COPY Directory.Build.props ./
COPY src/ src/

RUN dotnet restore "$PROJECT"

RUN dotnet publish "$PROJECT" \
    --configuration Release \
    --no-restore \
    --output /app

# Migration bundle stage. The schema is applied by a migration bundle instead of
# by the servers, so the bundle is built here and no server image ever carries
# the ability to change the schema. It needs the SDK, the tool manifest and the
# Entity Framework tools, none of which reach a runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS bundle

WORKDIR /source

COPY Directory.Build.props ./
COPY .config/ .config/
COPY src/ src/

RUN dotnet restore src/Shared/Mgo2Server.Shared.csproj

RUN dotnet tool restore

RUN dotnet ef migrations bundle \
    --project src/Shared/Mgo2Server.Shared.csproj \
    --configuration Release \
    --output /bundle/efbundle

# .NET runtime stage. Provides the runtime for the migration bundle; the postgres
# and migrate stages copy from here rather than installing the runtime themselves.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS dotnet-runtime

# Custom PostgreSQL stage. Combines the official postgres:18 image with the
# migration bundle so schema changes are applied inside the postgres container
# before it starts accepting connections. No separate migration job is needed.
FROM postgres:18 AS postgres

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=dotnet-runtime /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet

WORKDIR /bundle
COPY --from=bundle /bundle/efbundle ./
COPY docker/entrypoint-postgres.sh /usr/local/bin/entrypoint-postgres.sh
COPY docker/healthcheck-postgres.sh /usr/local/bin/healthcheck-postgres.sh

# The custom entrypoint replaces the stock one: it runs the migration bundle
# against a temporary instance before starting the real one as PID 1.
ENTRYPOINT ["/usr/local/bin/entrypoint-postgres.sh"]
CMD ["postgres"]

# Migration job stage. Runs the bundle once per deployment and exits, before any
# server opens a connection; the services wait for it with the
# service_completed_successfully condition. Kept for backward compatibility or
# for cases where a separate migration job is preferred.
FROM dotnet-runtime AS migrate

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /bundle
COPY --from=bundle /bundle/efbundle ./

# The connection reaches the bundle from the environment rather than from the
# artifact, so a published bundle carries no credential. Both variable names are
# accepted, in the order the servers resolve them.
ENTRYPOINT ["sh", "-c", "exec ./efbundle --connection \"${DATABASE_CONNECTION_STRING:-$DATABASE_URL}\""]

# Runtime stage. This is the last stage, so a build that names no target gets a
# server image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# libgssapi-krb5-2 is for Npgsql, which probes GSSAPI when a connection asks for
# Kerberos-integrated security; without the native library the runtime prints
# "Error: libgssapi_krb5.so.2: cannot open shared object file" on the first
# database connection. tini is the container's init, and the entrypoint below
# explains why that is not optional.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 tini \
    && rm -rf /var/lib/apt/lists/*

ARG ASSEMBLY
ENV MGO2_SERVER_ASSEMBLY=$ASSEMBLY

# The ASP.NET base image sets ASPNETCORE_HTTP_PORTS=8080, which the host turns
# into a server URL. Kestrel then overrides that URL with the endpoint the HTTP
# API configures from HTTP_PORT and logs "Overriding address(es) 'http://*:8080'"
# on every start. Clearing the inherited value leaves the app in sole control of
# the binding; the other roles are console apps and never read it.
ENV ASPNETCORE_HTTP_PORTS=

WORKDIR /app
COPY --from=build /app ./

# The HTTP API mirrors the launcher files into its Static directory, so that
# cache has to be writable by the non-root user the k3s deployment runs the
# container as. Only the HTTP image carries a Static directory; the other roles
# have none.
RUN [ -d /app/Static ] && chown -R 1000:1000 /app/Static || true

# The servers read their configuration from appsettings.json. The committed
# example has the deployment defaults; a deployment mounts its own
# appsettings.json over this one, and a runtime environment variable of the same
# name overrides any file value, so secrets never have to be baked into an image.
COPY appsettings.example.json ./appsettings.json

# tini is the container's first process, so that no server is. The init of a PID
# namespace carries SIGNAL_UNKILLABLE, and the kernel discards any signal whose
# disposition is the default for it — the SIGABRT the runtime raises against
# itself when a server dies of an unhandled exception, included. So a server
# that dies of one does not die: the runtime calls abort, the signal is
# swallowed, and its fallback spins on the main thread for as long as the
# container lives. Nothing restarts the pod, because the container never exits,
# and nothing stops it either: the shutdown signal reaches a process whose only
# live thread is already inside that fallback, so the pod sits in Terminating
# for the whole of its grace period with a core pinned. `kubectl delete --force`
# does not clear it; a cgroup kill, which is what `crictl stop` performs, does.
# On a node with four cores and fifteen servers, a database that is away once
# costs most of the node until each one is removed by hand (the runtime's own
# report of the first half is dotnet/runtime#118049). Under an init the server is
# an ordinary process: the fatal signal ends it, the platform restarts the
# container, and tini forwards SIGTERM to it, so a rollout's graceful drain is
# unchanged.
#
# exec is still right here: it makes the server tini's own child rather than the
# shell's, so the signal tini forwards is the server's to handle.
ENTRYPOINT ["/usr/bin/tini", "--", "sh", "-c", "exec dotnet \"$MGO2_SERVER_ASSEMBLY\".dll"]
