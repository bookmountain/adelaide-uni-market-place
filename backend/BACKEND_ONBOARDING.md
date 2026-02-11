# Backend API Onboarding Guide (Freshman-Friendly)

Welcome 👋 — this guide helps you take over backend development quickly and safely.

## 1) What this backend is

The backend is an **ASP.NET Core 8 Web API** for Adelaide Uni Marketplace. It follows a clean architecture split:

- `Api`: HTTP controllers, SignalR hub, startup wiring.
- `Application`: use-cases (commands/queries), validation, mapping.
- `Domain`: core business entities and enums.
- `Infrastructure`: EF Core/PostgreSQL, seeding, SMTP, Cloudflare R2.
- `Contracts`: request/response DTOs shared between layers.

## 2) Request flow in simple words

When a client calls an endpoint:

1. A controller in `Api` receives HTTP input.
2. Controller sends a MediatR command/query into `Application`.
3. Handler in `Application` uses `IApplicationDbContext` and service interfaces.
4. `Infrastructure` provides concrete implementations (Postgres, SMTP, R2).
5. DTOs from `Contracts` are returned to API responses.

Think of `Application` as “the brain”, `Domain` as “the rules”, and `Infrastructure` as “the adapters”.

## 3) Key features currently implemented

- Auth: register, activate account via emailed token, login, resend activation.
- Marketplace: category list, item CRUD, image upload/delete (R2-backed).
- Orders: create order for active item, list your orders, fetch order details.
- Chat: SignalR realtime message send + chat history endpoint.
- Health + docs: `/healthz` and `/swagger` in development.

## 4) Local development checklist

From repo root:

```bash
cd backend
dotnet restore
dotnet build
```

Run full dependencies + API with Docker:

```bash
cd backend
docker compose up --build
```

## 5) Environment variables

Use `.env` with `docker-compose.yml`.

- Start by copying `backend/.env.example` to `backend/.env`.
- Fill SMTP, JWT, DB, and R2 values.
- Never commit real secrets.

## 6) First files to study (in order)

1. `backend/src/Api/Program.cs` → all startup wiring and middleware.
2. `backend/src/Api/Controllers/AuthController.cs` → auth entry points.
3. `backend/src/Application/Auth/Commands/...` → core auth behavior.
4. `backend/src/Api/Controllers/ItemsController.cs` + item handlers.
5. `backend/src/Infrastructure/Data/MarketplaceDbContext.cs` + entity configurations.
6. `backend/src/Infrastructure/Data/Seeding/DatabaseSeeder.cs` + `backend/db/seed.sql`.

## 7) Safe ways to extend the backend

When adding a new feature:

1. **Domain first**: add/extend entity/enums only if the business model changes.
2. **Application**: add command/query + validator + handler.
3. **Contracts**: add DTOs for request/response.
4. **Api**: add controller endpoint that forwards to MediatR.
5. **Infrastructure**: only if you need new persistence/external integration.
6. Update README/API docs and add tests.

## 8) Common gotchas to watch

- Keep handlers idempotent where possible.
- Use clear `InvalidOperationException` messages (current pattern in controllers).
- Ensure auth checks are explicit (`TryGetUserId` patterns in controllers/hubs).
- For uploads, validate MIME type and ownership before persisting.
- Keep migrations additive and avoid destructive seed changes.

## 9) Suggested first-week ownership plan

- Day 1: run stack, hit Swagger, call auth + items endpoints.
- Day 2: write integration tests for register/activate/login happy-path.
- Day 3: add tests for item create/update/delete + image upload validation.
- Day 4: tighten error handling + logs for SMTP and R2 failures.
- Day 5: propose CI workflow (`dotnet build` + tests) and open PR.

## 10) Definition of done for backend changes

- Build passes (`dotnet build`).
- Tests pass (or new tests added where needed).
- Swagger remains accurate.
- New config is documented in README and `.env.example`.
- Docker compose still starts successfully.

You’re set 🚀. If you keep changes small and layered, this codebase is very maintainable.
