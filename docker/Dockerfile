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

ARG ASSEMBLY
ENV MGO2_SERVER_ASSEMBLY=$ASSEMBLY

WORKDIR /app
COPY --from=build /app ./

# Ships the policy document the HTTP API serves, so every image carries its own
# static content and a deployment needs no checkout on the host.
COPY static/ static/

# exec keeps the process as PID 1 so the container stops on SIGTERM.
ENTRYPOINT ["sh", "-c", "exec dotnet \"$MGO2_SERVER_ASSEMBLY\".dll"]
