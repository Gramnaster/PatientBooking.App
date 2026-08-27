---
name: docker
description: >
  Docker containerization for .NET 10 applications. Covers multi-stage builds,
  .NET container images, non-root user configuration, health checks, and
  .dockerignore.
  Load this skill when containerizing an application, optimizing image size,
  setting up Docker Compose for local development, or when the user mentions
  "Docker", "Dockerfile", "container", "docker-compose", "image", "multi-stage",
  "non-root", ".dockerignore", "container health check", or "dotnet publish container".
---

# Docker

## Core Principles

1. **Multi-stage builds always** — Separate build and runtime stages. Build in the SDK image, run in the ASP.NET runtime image.
2. **Non-root by default** — .NET container images support `USER app` by default since .NET 8. Never run as root in production.
3. **Layer caching matters** — Copy `.csproj` files and restore before copying source code. This caches NuGet dependencies across builds.
4. **Health checks in the container** — Use `HEALTHCHECK` in Dockerfile or configure in Docker Compose / orchestrator.

## Patterns

### Multi-Stage Dockerfile for Web API

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore (cached layer)
COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
COPY ["src/MyApp.Domain/MyApp.Domain.csproj", "src/MyApp.Domain/"]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

# Copy everything and build
COPY . .
RUN dotnet publish "src/MyApp.Api/MyApp.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user (default in .NET 8+ images)
USER app

COPY --from=build /app/publish .

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["dotnet", "MyApp.Api.dll", "--urls", "http://localhost:8080/health/live"]

ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

### .dockerignore

```
**/.git
**/.vs
**/bin
**/obj
**/node_modules
**/Dockerfile*
**/docker-compose*
**/tests
```

### Docker Compose for Local Development

Key .NET-specific concerns — pass connection strings via environment, use `depends_on` with health checks:

```yaml
services:
  api:
    build:
      context: .
      dockerfile: src/MyApp.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Host=postgres;Database=myapp;Username=postgres;Password=postgres
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      postgres:
        condition: service_healthy
  # Add postgres/redis services with healthcheck — standard boilerplate
```

### Dev Infra Compose — one command, DB of choice, foldered per project

Every project gets a `docker/` folder holding a dev-only compose file, separate from any
production deploy compose at the repo root. The API runs on the host (`dotnet run`/`dotnet
watch`); this stack only supplies what it talks to — SQL Server or Postgres (mutually
selectable via profile, not both mounted by default), Seq, smtp4dev, and RabbitMQ when the
project needs a broker. Anyone cloning the repo gets working infra from one command instead of
a string of ad-hoc `docker run`s.

```yaml
# docker/dev.compose.yaml
name: myapp-dev

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    profiles: ["sqlserver"]           # opt-in: docker compose ... --profile sqlserver up -d
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD:-YourStrong@Passw0rd}
    ports:
      - "${SQLSERVER_PORT:-1433}:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql

  postgres:
    image: postgres:17-alpine
    profiles: ["postgres"]            # pick one DB profile, not both
    environment:
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-postgres}
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data

  seq:                                 # no profile - cheap enough to always run
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: "Y"
      SEQ_FIRSTRUN_NOAUTHENTICATION: "True"   # or SEQ_FIRSTRUN_ADMINPASSWORD for a real login
    ports:
      - "${SEQ_UI_PORT:-5341}:80"
    volumes:
      - seq-data:/data

  smtp4dev:                           # no profile - same reasoning
    image: rnwood/smtp4dev
    ports:
      - "${SMTP_PORT:-2525}:25"
      - "${SMTP_UI_PORT:-5000}:80"

  rabbitmq:
    image: rabbitmq:4-management-alpine
    profiles: ["rabbitmq"]            # only for projects that actually use a broker
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:-guest}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-guest}
    ports:
      - "${RABBITMQ_PORT:-5672}:5672"
      - "${RABBITMQ_UI_PORT:-15672}:15672"
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq

volumes:
  sqlserver-data:
  postgres-data:
  seq-data:
  rabbitmq-data:
```

Ship `docker/.env.dev.example` alongside it (every var above defaulted, gitignore the real
`docker/.env.dev`) and a one-liner in the README:

```bash
cp docker/.env.dev.example docker/.env.dev
docker compose -f docker/dev.compose.yaml --env-file docker/.env.dev --profile sqlserver up -d
```

Profiles are the mechanism for "DB of choice" and "only bring up what this project needs" —
don't build a custom script for service selection when Compose already has one.

### Optimized Build with .slnx

For solutions with multiple projects, restore only the necessary projects.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and all project files
COPY *.slnx .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/**/*.csproj ./src/

# Restore project structure
RUN for file in src/**/*.csproj; do \
    mkdir -p $(dirname $file) && mv $file $(dirname $file)/; \
    done
RUN dotnet restore

COPY . .
RUN dotnet publish src/MyApp.Api -c Release -o /app/publish --no-restore
```

### Health Check Endpoint

```csharp
// In Program.cs — lightweight health endpoint for Docker
app.MapGet("/health/live", () => Results.Ok("healthy"))
    .ExcludeFromDescription();
```

## Anti-patterns

### Don't Use SDK Image for Runtime

```dockerfile
# BAD — SDK image is 900MB+, includes compilers
FROM mcr.microsoft.com/dotnet/sdk:10.0
COPY . .
RUN dotnet run

# GOOD — separate build and runtime, runtime image is ~200MB
FROM mcr.microsoft.com/dotnet/aspnet:10.0
```

### Don't Copy Everything Before Restore

```dockerfile
# BAD — any source change invalidates the NuGet cache
COPY . .
RUN dotnet restore

# GOOD — copy only project files first, then restore
COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"
COPY . .
```

### Don't Run as Root

```dockerfile
# BAD — running as root (security risk)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MyApp.Api.dll"]

# GOOD — use the built-in non-root user
FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Web API container | Multi-stage build with aspnet runtime image |
| Worker service | Multi-stage build with dotnet/runtime image |
| Local development | `docker/dev.compose.yaml` with service dependencies (see Dev Infra Compose above) |
| New project scaffolding | Set up `docker/dev.compose.yaml` + `.env.dev.example` from day one, not retrofitted later |
| CI builds | Multi-stage build (self-contained) |
| Image size optimization | Use Alpine variant + trimming for small images |
| Health monitoring | HEALTHCHECK instruction + /health endpoint |
| Secrets | Environment variables or mounted secrets, never in image |
