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

# Runtime stage.
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

# Ships the policy document the HTTP API serves, so every image carries its own
# static content and a deployment needs no checkout on the host.
COPY static/ static/

# exec keeps the process as PID 1 so the container stops on SIGTERM.
ENTRYPOINT ["sh", "-c", "exec dotnet \"$MGO2_SERVER_ASSEMBLY\".dll"]
