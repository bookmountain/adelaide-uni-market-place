# Adelaide University Marketplace – Backend

Backend scaffold for the Adelaide University Marketplace. The service is an ASP.NET Core 8 Web API following a clean architecture layout (`Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`). A mobile-first React Native (Expo) client lives in `frontend/` (initialised via `npx @react-native-reusables/cli@latest init`).

## Frontend (React Native iOS starter)

The mobile client targets iOS first using Expo + React Native with TypeScript and the React Native Reusables design system.

### Prerequisites

- Node.js 18+
- `npm` 9+ (or `pnpm`/`yarn` if you prefer)
- Xcode with iOS Simulator (for local iOS testing)
- Expo CLI (`npm install --global expo-cli`) optional but recommended

### Local development

```bash
cd frontend
npm install
npm run ios     # launches Metro + iOS simulator
# or: npm run start (then scan the QR code with Expo Go)
```

> If `pod install` fails with `unknown keyword: :privacy_file_aggregation_enabled`, update CocoaPods to ≥ 1.13.0 or rely on the guard already included in `ios/Podfile`.

Project layout:

```
frontend/
  App.tsx
  app.json
  package.json
  src/
    navigation/
    screens/
    components/
    theme/
```

Top-level screens implemented: Login, Home, Product Detail, Chat, Listing Form, Seller Dashboard (with Settings). Navigation uses React Navigation with a bottom tab layout tuned for Expo.

## Prerequisites

-   [.NET SDK 8.0](https://dotnet.microsoft.com/en-us/download). The repo pins SDK `8.0.407` via `global.json`.

## Getting Started

```bash
cd backend
dotnet restore
dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Infrastructure/Infrastructure.csproj
dotnet run --project src/Api/Api.csproj
```

### Running the full stack with Docker Compose

To run PostgreSQL, Redis, RabbitMQ, Elasticsearch, and the Web API together in containers:

```bash
cd backend
docker compose up --build
```

Services started by the compose file:

- `postgres` – PostgreSQL 16 with a persisted volume (`postgres_data`)
- `redis` – Redis 7 for cache/pub-sub (`redis_data`)
- `rabbitmq` – RabbitMQ 3 with the management UI on `http://localhost:15672`
- `elasticsearch` – Single-node Elasticsearch 8 (`elastic_data`)
- `api` – ASP.NET Core Web API exposed on `http://localhost:8080`

The API container waits for PostgreSQL, applies migrations, and runs `db/seed.sql` on startup. Override email/R2 credentials by editing `docker-compose.yml` or supplying an `.env` file before running `docker compose up`.

Rebuild after code changes with `docker compose up --build`. Stop everything with `docker compose down`. Add `-v` to remove the persisted volumes if you want a completely fresh start.


**Docker Compose (.env):**

1. Copy `backend/.env.example` to `backend/.env`.
2. Edit the new `.env` with real SMTP/R2 secrets.
3. Run `docker compose up --build` as shown above.

The API listens on `https://localhost:7123` (Kestrel default) and exposes:

-   `/swagger` – interactive OpenAPI documentation
-   `/healthz` – liveness probe returning `{ "status": "ok" }`
-   `/api/categories` – public category listing
-   `/api/items` – authenticated CRUD for marketplace items
-   `/api/orders` – create an order (in-person delivery) and list buyer orders
-   `/api/auth/refresh` – rotate a refresh token and obtain a new access token
-   `/api/auth/logout` – revoke the current refresh token
-   `/api/auth/logout-all` – revoke all refresh tokens for the authenticated user
-   `/api/users/me` – update the authenticated user's profile (`PATCH`)
-   `/api/users/me/anon-handle` – get (or generate) the authenticated user's anonymous handle (`GET`)
-   `/api/threads/...` – community threads feed (see **Threads** section below)

> The API seeds a default category set on startup. If you need deterministic IDs, run the SQL in `db/seed.sql` instead.

## Required Configuration

Set the following environment variables (or override in `backend/src/Api/appsettings.Development.json` for local runs):

| Variable                                | Description                                               |
| --------------------------------------- | --------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`                | Hosting environment (set to `Development` for local runs) |
| `Auth__AppJwtIssuer`                    | Issuer/audience for application JWTs                      |
| `Auth__AppJwtSigningKey`                | Symmetric signing key used for app-issued JWTs            |
| `Auth__AllowedEmailDomain`              | Email domain required for sign-in                         |
| `Auth__ActivationBaseUrl`              | Base URL used in activation emails                        |
| `Auth__AccessTokenMinutes`              | Access token lifetime in minutes (default: `15`)          |
| `Auth__RefreshTokenDays`                | Refresh token lifetime in days (default: `14`)            |
| `Auth__LoginMaxFailuresPerEmail`        | Max failed logins per email before lockout (default: `5`) |
| `Auth__LoginMaxFailuresPerIp`           | Max failed logins per IP before lockout (default: `10`)   |
| `Auth__LoginFailureWindowMinutes`       | Rolling window for login failure counters (default: `15`) |
| `Postgres__ConnectionString`            | Connection string for the marketplace PostgreSQL database |
| `Redis__ConnectionString`               | Connection string for Redis cache/pub-sub                 |
| `RabbitMq__Host`                        | Connection URI for RabbitMQ                               |
| `Elastic__Uri`                          | Endpoint for Elasticsearch/OpenSearch                     |
| `Stripe__SecretKey`                     | Stripe API secret key                                     |
| `R2__AccountId`                         | Cloudflare R2 account identifier                          |
| `R2__AccessKeyId`                       | Cloudflare R2 access key id                               |
| `R2__SecretAccessKey`                   | Cloudflare R2 secret access key                           |
| `R2__Bucket`                            | Cloudflare R2 bucket used for media assets                |

Example (PowerShell):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Auth__AppJwtIssuer = "http://localhost"
$env:Auth__AppJwtSigningKey = "local-development-signing-key-change-me"
$env:Auth__AllowedEmailDomain = "adelaide.edu.au"
$env:Auth__ActivationBaseUrl = "https://localhost:7123/api/auth/activate"
$env:Postgres__ConnectionString = "Host=localhost;Database=marketplace;Username=postgres;Password=postgres"
$env:Redis__ConnectionString = "localhost:6379"
$env:RabbitMq__Host = "amqp://guest:guest@localhost:5672"
$env:Elastic__Uri = "http://localhost:9200"
$env:Stripe__SecretKey = "sk_test_placeholder"
$env:R2__AccountId = "dev-account-id"
$env:R2__AccessKeyId = "dev-access-key"
$env:R2__SecretAccessKey = "dev-secret-key"
$env:R2__Bucket = "marketplace-media"
dotnet run --project src/Api/Api.csproj
```

Example (bash):

```bash
export ASPNETCORE_ENVIRONMENT=Development
export Auth__AppJwtIssuer=http://localhost
export Auth__AppJwtSigningKey=local-development-signing-key-change-me
export Auth__AllowedEmailDomain=adelaide.edu.au
export Auth__ActivationBaseUrl=https://localhost:7123/api/auth/activate
export Postgres__ConnectionString="Host=localhost;Database=marketplace;Username=postgres;Password=postgres"
export Redis__ConnectionString=localhost:6379
export RabbitMq__Host=amqp://guest:guest@localhost:5672
export Elastic__Uri=http://localhost:9200
export Stripe__SecretKey=sk_test_placeholder
export R2__AccountId=dev-account-id
export R2__AccessKeyId=dev-access-key
export R2__SecretAccessKey=dev-secret-key
export R2__Bucket=marketplace-media
dotnet run --project src/Api/Api.csproj
```

## Registration & Login

### Register

Call `POST /api/auth/register` with JSON payload:

```json
{
    "email": "student@adelaide.edu.au",
    "password": "ChangeMe123!",
    "displayName": "Local Student",
    "avatarUrl": "https://example.com/avatar.png",
    "department": "Computer Science",
    "degree": "Bachelor of IT",
    "sex": "Other",
    "nationality": "Australian",
    "age": 21
}
```

The response returns the persisted profile. Email addresses must end with `@adelaide.edu.au` and passwords require at least 8 characters. An activation email (logged to the console in development) is generated with a link of the form `https://localhost:7123/api/auth/activate?token=<guid>`.

### Login

Call `POST /api/auth/login` with:

```json
{
    "email": "student@adelaide.edu.au",
    "password": "ChangeMe123!"
}
```

On success you receive `{ token, refreshToken, user }`. The `token` is a short-lived JWT (default 15 minutes). Store `refreshToken` and call `POST /api/auth/refresh` with `{ refreshToken }` before the access token expires to rotate to a new pair. Use `Authorization: Bearer <token>` for all authenticated endpoints.

### Testing via Swagger

1. Start the API (`dotnet run --project src/Api/Api.csproj`).
2. Browse to `https://localhost:7123/swagger`.
3. Register a new account via `POST /api/auth/register` (check the console output for the activation link and open `GET /api/auth/activate?token=<value>` to activate) or reuse an existing one via `POST /api/auth/login`.
4. Click **Authorize**, choose the `Bearer` scheme, and paste `Bearer <token>`.
5. Exercise the secured `/api/items` endpoints. When creating items, use the `POST /api/items` form-data endpoint to supply metadata and one or more image files in a single request.

### Seeded Account

On first launch the database seeder creates a ready-to-use account:

- Email: `student@adelaide.edu.au`
- Password: `ChangeMe123!`

This account is pre-activated and owns two sample listings with Unsplash imagery so you can exercise the item endpoints immediately.

### Generating BCrypt Hashes (optional)

If you ever need to pre-seed users, you can generate password hashes via:

```bash
tmpdir=$(mktemp -d)
dotnet new console -n PwHash -o "$tmpdir" >/dev/null
dotnet add "$tmpdir/PwHash" package BCrypt.Net-Next >/dev/null
cat <<'CS' > "$tmpdir/PwHash/Program.cs"
using System;
class Program
{
    static void Main() => Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"));
}
CS
dotnet run --project "$tmpdir/PwHash"
```

Replace `ChangeMe123!` with your password, copy the output hash, and delete the temporary directory when finished.

- `POST /api/auth/register` – create a marketplace account (inactive until activated).
- `GET /api/auth/activate?token=<value>` – confirm registration using the emailed token.
- `POST /api/auth/login` – obtain `{ token, refreshToken, user }` for an active account; access token is short-lived (15 min default).
- `POST /api/auth/refresh` – exchange a valid refresh token for a new `{ token, refreshToken }` pair.
- `POST /api/auth/logout` – revoke the supplied refresh token.
- `POST /api/auth/logout-all` – revoke all refresh tokens for the authenticated user.
- `GET /api/categories` – list all categories.
- `GET /api/items` – list items (Bearer token required).
- `POST /api/items` – create item for the authenticated user.
- `PUT /api/items/{id}` – update existing item.
- `DELETE /api/items/{id}` – remove an item.
- `PATCH /api/users/me` – update the authenticated user's profile fields.
- `GET /api/users/me/anon-handle` – get (or lazily generate) the authenticated user's anonymous handle.

## Threads

A community feed where students can post, comment, and react — with optional per-post anonymity.

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/threads/categories` | List active categories (public) |
| `POST` | `/api/threads/categories` | [Admin] Create a category |
| `PATCH` | `/api/threads/categories/{id}` | [Admin] Update a category |
| `GET` | `/api/threads/feed` | Paginated feed (`?category=&sort=hot\|new\|top&cursor=&pageSize=`) |
| `GET` | `/api/threads/posts/{id}` | Post detail |
| `GET` | `/api/threads/posts/{id}/comments` | 2-level comment tree |
| `POST` | `/api/threads/posts` | Create a post (multipart; `isAnonymous`, `images[]`) |
| `PATCH` | `/api/threads/posts/{id}` | Author edits title/body |
| `DELETE` | `/api/threads/posts/{id}` | Author or admin soft-delete |
| `POST` | `/api/threads/posts/{id}/like` | Toggle like on a post |
| `POST` | `/api/threads/posts/{id}/comments` | Add a comment (`parentCommentId` optional; max 1 level deep) |
| `POST` | `/api/threads/comments/{id}/like` | Toggle like on a comment |

### Identity & anonymity

Per-post identity is chosen at creation (`isAnonymous: true/false`) and is **immutable** after posting. Anonymous posts and comments are served under a stable per-user handle (generated once and stored; see `GET /api/users/me/anon-handle`) and never expose the user's real identity through the API.

### Feed backing store

The feed is currently Postgres-backed (cursor-sorted by hot/new/top). It will move to Elasticsearch in a future Read Path plan.

### Seeded categories

Seven categories are seeded on startup: `housemate`, `share-memberships`, `textbooks`, `rides`, `lost-and-found`, `events`, `general`.

### Admin role

Admin endpoints (`POST /api/threads/categories`, `PATCH /api/threads/categories/{id}`) require the `Admin` role, which is granted to users with `IsAdmin = true` in the database.

## Solution Layout

```
backend/
  Marketplace.sln
  src/
    Api/
      Controllers/
      Auth/
    Application/
      Auth/
      Categories/
      Items/
      Common/
    Domain/
      Entities/
      Shared/
    Infrastructure/
      Configuration/
      Data/
        Configurations/
        Migrations/
        Seeding/
    Contracts/
      DTO/
  db/
    seed.sql
```
