#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
# NET8 migration: base images -> :8.0 and the build stages rewritten for the 2026-05 MONOREPO
# layout. The old single-project COPY ("Febris.UserNode.Portal.csproj", ".")
# could not restore the ..\..\shared\* project references, so these images have not been buildable
# since the monorepo migration. The build context must now be the REPO ROOT:
#   docker build -f enduser/FebrisEndUserPortal/Dockerfile .
# (run from the repo root; the csproj's DockerfileContext points there too.)

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# net8 images listen on 8080 by default (ASPNETCORE_HTTP_PORTS); pin 80 to preserve the existing
# EXPOSE/K8s port contract. Non-root hardening is a post-migration follow-up.
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80
EXPOSE 443
# Self-host clone-and-run: curl in the runtime layer so docker-compose / K8s healthchecks can
# probe the node's /health/live + /health/ready endpoints -- the aspnet base image ships no
# HTTP client for CMD probes.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "enduser/FebrisEndUserPortal/Febris.UserNode.Portal.csproj"
RUN dotnet build "enduser/FebrisEndUserPortal/Febris.UserNode.Portal.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "enduser/FebrisEndUserPortal/Febris.UserNode.Portal.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Febris.UserNode.Portal.dll"]
