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

# Migration job stage. Runs the bundle once per deployment and exits, before any
# server opens a connection; the services wait for it with the
# service_completed_successfully condition.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrate

# Npgsql probes GSSAPI when a connection asks for Kerberos-integrated
# security; the native library ships in libgssapi-krb5-2, without which the
# runtime prints "Error: libgssapi_krb5.so.2: cannot open shared object file"
# on the first database connection. The bundle opens one like any other client.
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

# Npgsql probes GSSAPI when a connection asks for Kerberos-integrated
# security; the native library ships in libgssapi-krb5-2, without which the
# runtime prints "Error: libgssapi_krb5.so.2: cannot open shared object file"
# on the first database connection.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
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

# exec keeps the process as PID 1 so the container stops on SIGTERM.
ENTRYPOINT ["sh", "-c", "exec dotnet \"$MGO2_SERVER_ASSEMBLY\".dll"]
