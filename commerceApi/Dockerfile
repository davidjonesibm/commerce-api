# syntax=docker/dockerfile:1
# ^^^^^^^^^^^^^^^^^^^^^^^^^^^^
# IMPORTANT: This must be the VERY FIRST line. It tells Docker to use the
# BuildKit frontend, which enables advanced features like cache mounts,
# secret mounts, and heredocs. Without this, some instructions below won't work.

# =============================================================================
# MULTI-STAGE BUILD — Commerce API
# =============================================================================
#
# WHAT IS A MULTI-STAGE BUILD?
#   A Dockerfile can have multiple FROM instructions, each starting a new
#   "stage". Only the FINAL stage becomes the published image. Earlier stages
#   are used for building and are discarded. This means:
#     - The SDK (build tools, compilers) is NOT in the production image
#     - The final image is MUCH smaller and has fewer security vulnerabilities
#     - Source code never ships to production
#
# OUR STAGES:
#   1. "build"   — Uses the full .NET SDK to restore, compile, and publish
#   2. "runtime" — Uses the slim ASP.NET runtime to run the compiled output
#
# =============================================================================


# =============================================================================
# STAGE 1: BUILD
# =============================================================================
# The SDK image (~900 MB) includes compilers, NuGet, MSBuild — everything
# needed to build .NET projects. We use this to compile our code, then throw
# this stage away.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# WORKDIR sets the working directory inside the container. All subsequent
# commands (COPY, RUN) execute relative to this path. If the directory doesn't
# exist, Docker creates it automatically.
WORKDIR /src

# ---------------------------------------------------------------------------
# LAYER CACHING TRICK: Copy dependency manifests FIRST, then restore.
# ---------------------------------------------------------------------------
# WHY? Docker caches each instruction as a "layer". If the input files haven't
# changed, Docker reuses the cached layer instead of re-executing the command.
#
# By copying ONLY the .csproj file first and running `dotnet restore`, we
# create a cached layer of all NuGet packages. On subsequent builds, if only
# our C# source code changed (but not the .csproj), Docker skips the restore
# step entirely — saving significant build time.
#
# If we did `COPY . .` first, ANY source file change would invalidate the
# cache and force a full restore every time.
COPY commerceApi/commerceApi.csproj commerceApi/

# RESTORE: Download all NuGet packages listed in the .csproj file.
# --mount=type=cache persists the NuGet package cache across builds so
# packages don't need to be re-downloaded every time.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore commerceApi/commerceApi.csproj

# NOW copy the rest of the source code. This layer is invalidated whenever
# any source file changes, but the restore layer above remains cached.
COPY commerceApi/ commerceApi/

# PUBLISH: Compile the application in Release mode and output to /app/publish.
# -c Release         — Optimized build (no debug symbols, optimized IL)
# -o /app/publish    — Output directory for the compiled files
# --no-restore       — Skip restore since we already did it above
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish commerceApi/commerceApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =============================================================================
# STAGE 2: RUNTIME
# =============================================================================
# The ASP.NET runtime image (~220 MB) contains only what's needed to RUN
# .NET apps — no compiler, no SDK, no build tools. This makes the final
# image smaller, faster to pull, and with a reduced attack surface.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# ---------------------------------------------------------------------------
# SECURITY: Run as a non-root user
# ---------------------------------------------------------------------------
# WHY? If an attacker exploits a vulnerability in the app, running as root
# gives them full control of the container (and potentially the host).
# Running as a non-root user limits the blast radius of any compromise.
#
# .NET 8+ images include a built-in non-root user called "app" (UID 1654).
# We switch to it with the USER instruction below.
# ---------------------------------------------------------------------------

# WORKDIR in the runtime stage — this is where our compiled app lives.
WORKDIR /app

# COPY the compiled output from the build stage into the runtime stage.
# --from=build references the first stage by its name (AS build).
# --chown=app:app ensures the files are owned by the non-root user.
# Only the compiled DLLs and runtime assets are copied — no source code!
COPY --from=build --chown=app:app /app/publish .

# Switch to the non-root "app" user. All subsequent commands (and the
# running container) will execute as this user.
USER app

# EXPOSE documents which port the container listens on. This is informational
# only — it doesn't actually open the port. The port is opened by the app
# or by the `-p` flag in `docker run` / `ports:` in compose.
#
# .NET 8+ defaults to port 8080 when running as non-root (previously 80).
# This is configured via the ASPNETCORE_HTTP_PORTS environment variable
# which defaults to 8080 in the official images.
EXPOSE 8080

# ENVIRONMENT: Set the ASP.NET Core environment.
# "Development" enables detailed error pages, Swagger UI, and verbose logging.
# In production, you would set this to "Production".
ENV ASPNETCORE_ENVIRONMENT=Development

# ENTRYPOINT vs CMD:
# - ENTRYPOINT: The executable that always runs — cannot be overridden easily.
# - CMD: Default arguments — can be overridden at runtime.
#
# Using ENTRYPOINT with the exec form (JSON array) ensures the .NET process
# receives OS signals (SIGTERM for graceful shutdown) directly, rather than
# being wrapped in a shell.
ENTRYPOINT ["dotnet", "commerceApi.dll"]
