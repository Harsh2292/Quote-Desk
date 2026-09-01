# Builds and runs src/QuoteDesk.Api only — src/QuoteDesk.Web is a separate Static Web Apps deploy
# (task 09b), not part of this image. See .dockerignore for what never reaches the build context.
#
# Base image tags float on the major.minor band (10.0), matching every official Microsoft tutorial —
# not pinned to the exact local SDK (10.0.400, see global.json) because Docker Hub does not reliably
# publish a tag per SDK patch. Directory.Build.props' TreatWarningsAsErrors is embedded in the project
# files themselves, so an SDK patch bump that adds a new analyzer diagnostic would fail this build
# loudly rather than silently drifting — the same protection CLAUDE.md's `-warnaserror` gives locally.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so `dotnet restore` is cached across builds that only change application
# code — global.json/Directory.Build.props affect every project's build settings and must already be
# present. Only QuoteDesk.Api's own dependency graph (Api -> Agents -> Data -> Domain,
# Api -> Intake -> Data, per CLAUDE.md) is copied; the test projects are never part of this image.
COPY Directory.Build.props global.json QuoteDesk.sln ./
COPY src/QuoteDesk.Domain/QuoteDesk.Domain.csproj src/QuoteDesk.Domain/
COPY src/QuoteDesk.Data/QuoteDesk.Data.csproj src/QuoteDesk.Data/
COPY src/QuoteDesk.Intake/QuoteDesk.Intake.csproj src/QuoteDesk.Intake/
COPY src/QuoteDesk.Agents/QuoteDesk.Agents.csproj src/QuoteDesk.Agents/
COPY src/QuoteDesk.Api/QuoteDesk.Api.csproj src/QuoteDesk.Api/
RUN dotnet restore src/QuoteDesk.Api/QuoteDesk.Api.csproj

# The rest of the source. .dockerignore already keeps tests/, docs/, tasks/ and src/QuoteDesk.Web
# out of the build context entirely, so this layer only invalidates on an actual QuoteDesk.Api-graph
# source change.
COPY src/ src/
RUN dotnet publish src/QuoteDesk.Api/QuoteDesk.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Program.cs never calls UseHttpsRedirection — Container Apps' own ingress terminates TLS, so plain
# HTTP on 8080 (the .NET 8+ default, explicit here for clarity) is correct.
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

# Every .NET 8+ runtime image already ships this non-root user — this is the one line needed to
# actually run as it instead of the image's default root.
USER app

ENTRYPOINT ["dotnet", "QuoteDesk.Api.dll"]
