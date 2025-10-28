# AGENTS.md

## Purpose

You are the AI maintainer for the **Adelaide University Marketplace** backend. The core platform is already running with:

- ASP.NET Core 8 Web API (clean architecture across `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`).
- Local email/password registration with activation workflow (activation links via SMTP).
- JWT authentication/authorization.
- Marketplace items with image management (images stored in Cloudflare R2 through the S3-compatible SDK).
- PostgreSQL, Redis, RabbitMQ, and Elasticsearch orchestrated via Docker Compose.

Your mission is to extend and harden the backend while keeping the system production-ready.

Work incrementally, favour small, reviewable changes, and keep documentation up to date.

---

## Current Repository Layout

```
/backend
  Dockerfile
  docker-compose.yml
  .env.example
  /db
    seed.sql
  /src
    /Api
    /Application
    /Domain
    /Infrastructure
    /Contracts
  /tests          (placeholder – add projects when tests are introduced)
/README.md
/AGENTS.md
```

---

## Running the stack

1. `cp backend/.env.example backend/.env` and populate with real values (SMTP, R2, JWT).
2. `cd backend`
3. `docker compose up --build`
4. Swagger UI is available at `http://localhost:8080/swagger`.

The API container runs EF Core migrations and executes `db/seed.sql` automatically the first time (it skips on subsequent starts once seed markers exist).

---

## Implemented features (baseline)

- **Auth**
  - Local email + password registration.
  - Activation emails (token expires after 24 hours).
  - Login requiring activated accounts; application-issued JWTs.
  - Resend activation endpoint.
- **Marketplace Domain**
  - Categories, Items, ListingImages entities with EF Core migrations.
  - Item CRUD with validation via FluentValidation.
  - Image upload/delete endpoints (multipart form-data) storing media in R2.
- **Infrastructure**
  - PostgreSQL (Npgsql), Redis, RabbitMQ, Elasticsearch clients configured.
  - Cloudflare R2 storage abstraction (S3-compatible).
  - Serilog logging, health checks, Swagger (with JWT auth support).
- **Tooling**
  - Docker Compose orchestrating Postgres, Redis, RabbitMQ, Elasticsearch, API.
  - Seed script wiping and repopulating canonical demo data.

---

## Guardrails

- Target framework: **.NET 8**.
- Use existing architecture (Api/Application/Domain/Infrastructure/Contracts).
- All secrets/config come from environment variables / `.env` (never commit secrets).
- Write idempotent migrations and avoid destructive seed logic.
- When extending endpoints, keep Swagger documentation accurate (response codes, example payloads where useful).
- Update `README.md` and this file when behaviour/setup changes.
- Prefer strong typing, explicit validation, and clear logging.

---

## High-level roadmap

### 1. Stability & Tests
- Add unit/integration tests for critical flows (registration, activation, item CRUD & image handling).
- Introduce GitHub Actions (or other CI) to run build + tests.
- Harden error handling & logging around external services (SMTP, R2).

### 2. Authentication Enhancements
- Optional: integrate institutional SSO/OIDC once credentials are available (keep local login as fallback).
- Implement password reset flow (email token, new password endpoint).

### 3. Marketplace Features
- Item search (Elasticsearch indexing + query endpoint).
- Category & item administration (CRUD restrictions, moderation).
- Item image reordering.
- Pagination & filtering on item listing endpoints.

### 4. Messaging & Payments
- Hook up RabbitMQ for future notifications.
- Stub Stripe checkout endpoints (create intent, handle webhook) in preparation for payments.

### 5. Observability & Ops
- Metrics/Tracing (OpenTelemetry, Application Insights or similar).
- Production-ready Docker images (multi-stage with health checks, non-root user).
- IaC skeleton (Terraform/Bicep) for deploying infrastructure.

Work on one theme at a time; keep PRs scoped.

---

## Definition of Done (for any change)

- Compiles (`dotnet build`) and passes tests (once available).
- Swagger reflects the new/changed endpoints.
- Environment variables documented in README if new ones are added.
- Seed logic updated if new canonical data is required (keep idempotent).
- Relevant new behaviour covered by unit/integration tests (where applicable).
- Docker Compose continues to start successfully (`docker compose up --build`).
- `README.md` and `AGENTS.md` updated if developer workflow changes.

---

## Useful commands

```bash
# Restore & build
cd backend
dotnet restore
dotnet build

# Apply migrations / update schema
ASPNETCORE_ENVIRONMENT=Development \
  dotnet ef database update \
    --project src/Infrastructure/Infrastructure.csproj \
    --startup-project src/Api/Api.csproj

# Run API locally without containers
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/Api/Api.csproj

# Regenerate Swagger JSON
dotnet tool install --global Swashbuckle.AspNetCore.Cli
dotnet swagger tofile --output swagger.json src/Api/bin/Debug/net8.0/Api.dll v1
```

---

## Developer checklist before opening a PR

- [ ] Code builds locally (`dotnet build`).
- [ ] Added/updated tests (if applicable) and they pass.
- [ ] Updated migrations/scripts where schema changed.
- [ ] Swagger annotations/response types accurate.
- [ ] Documentation (`README.md`, `AGENTS.md`, comments) updated.
- [ ] Environment variables defined in `.env.example` if new ones introduced.
- [ ] Docker Compose tested (`docker compose up --build`).

Keep changes small, descriptive, and easy to review. Happy shipping! 🚀
