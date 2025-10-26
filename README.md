# Adelaide University Marketplace – Backend

Backend scaffold for the Adelaide University Marketplace. The service is an ASP.NET Core 8 Web API following a clean architecture layout (`Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`).

## Prerequisites

-   [.NET SDK 8.0](https://dotnet.microsoft.com/en-us/download) or later

## Getting Started

```bash
cd backend
dotnet restore
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj
dotnet run --project src/Api/Api.csproj
```

The API listens on `https://localhost:7123` (Kestrel default) and exposes:

-   `/swagger` – interactive OpenAPI documentation
-   `/healthz` – liveness probe returning `{ "status": "ok" }`
-   `/api/categories` – public category listing
-   `/api/items` – authenticated CRUD for marketplace items

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

On success you receive the same `AuthResponse` payload. Use `Authorization: Bearer <jwt>` for all `/api/items` operations.

### Testing via Swagger

1. Start the API (`dotnet run --project src/Api/Api.csproj`).
2. Browse to `https://localhost:7123/swagger`.
3. Register a new account via `POST /api/auth/register` (check the console output for the activation link and open `GET /api/auth/activate?token=<value>` to activate) or reuse an existing one via `POST /api/auth/login`.
4. Click **Authorize**, choose the `Bearer` scheme, and paste `Bearer <token>`.
5. Exercise the secured `/api/items` endpoints.

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
- `POST /api/auth/login` – obtain JWT for an active account.
- `GET /api/categories` – list all categories.
- `GET /api/items` – list items (Bearer token required).
- `POST /api/items` – create item for the authenticated user.
- `PUT /api/items/{id}` – update existing item.
- `DELETE /api/items/{id}` – remove an item.

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
