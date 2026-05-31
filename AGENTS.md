# AGENTS.md

## Purpose

You are the AI maintainer for the **Adelaide University Marketplace** platform. The primary client is a mobile-first React Native iOS app (Expo) located under `frontend/`, backed by an ASP.NET Core 8 Web API. The core platform is already running with:

-   ASP.NET Core 8 Web API (clean architecture across `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts`).
-   Local email/password registration with activation workflow (activation links via SMTP).
-   JWT authentication/authorization.
-   Marketplace items with image management (images stored in Cloudflare R2 through the S3-compatible SDK).
-   PostgreSQL, Redis, RabbitMQ, and Elasticsearch orchestrated via Docker Compose.

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
  /tests
    /Application.UnitTests
/frontend         (Expo React Native client – iOS-first starter)
/README.md
/AGENTS.md
```

---

## Running the stack

### Backend API (ASP.NET Core)

1. `cp backend/.env.example backend/.env` and populate with real values (SMTP, R2, JWT).
2. `cd backend`
3. `docker compose up --build`
4. Swagger UI is available at `http://localhost:8080/swagger`.

The API container runs EF Core migrations and executes `db/seed.sql` automatically the first time (it skips on subsequent starts once seed markers exist).

### Mobile client (React Native iOS-first)

1. `cd frontend`
2. `npm install`
3. `npm run ios` (launches Metro + iOS simulator) — or `npm run start` and use Expo Go.
4. Update packages with `npm outdated` / `npm update` only after confirming Expo SDK compatibility.

---

## Mobile client – Expo React Native

-   `cd frontend`
-   `npm install`
-   `npm run ios` (or `npm run start` and use the Expo Go app)

The committed starter under `frontend/` implements the primary UX screens (login, home, product details, chat, listing form, seller dashboard/settings) with React Navigation bottom tabs and shared theme tokens inspired by the React Native Reusables design system. Keep Expo SDK versions in sync with the `package.json` when upgrading.

---

## Implemented features (baseline)

-   **Auth**
    -   Local email + password registration.
    -   Activation emails (token expires after 24 hours).
    -   Login requiring activated accounts; application-issued JWTs.
    -   Resend activation endpoint.
    -   Short-lived access tokens (default 15 min) + long-lived refresh tokens (default 14 days) stored in Redis.
    -   `POST /api/auth/refresh` – rotate refresh token; `POST /api/auth/logout` / `logout-all` – revoke.
    -   Redis-backed login rate limiting per email and per IP.
    -   `PATCH /api/users/me` – update profile; `GET /api/users/me/anon-handle` – lazy anon handle.
    -   New `Auth__` config keys (all optional with defaults): `AccessTokenMinutes` (15), `RefreshTokenDays` (14), `LoginMaxFailuresPerEmail` (5), `LoginMaxFailuresPerIp` (10), `LoginFailureWindowMinutes` (15).
-   **Marketplace Domain**
    -   Categories, Items, ListingImages entities with EF Core migrations.
    -   Item CRUD with validation via FluentValidation.
    -   Image upload/delete endpoints (multipart form-data) storing media in R2.
-   **Threads**
    -   Community feed with posts, 2-level comments, and like toggles.
    -   Per-post anonymous identity (`isAnonymous` flag set at creation, immutable); anonymous content served under a stable per-user handle, never leaking real identity.
    -   Seven seeded categories: `housemate`, `share-memberships`, `textbooks`, `rides`, `lost-and-found`, `events`, `general`.
    -   Feed sorted by `hot`, `new`, or `top`; cursor-paginated; **Elasticsearch-backed** (read path plan shipped).
    -   Admin endpoints (`POST/PATCH /api/threads/categories`) require the `Admin` role (`IsAdmin = true`).
    -   Controllers: `ThreadsController`, `ThreadCategoriesController` (auto-discovered via `AddControllers`).
    -   Endpoints:
        -   `GET  /api/threads/categories` – list active categories (public)
        -   `POST /api/threads/categories` – [Admin] create category
        -   `PATCH /api/threads/categories/{id}` – [Admin] update category
        -   `GET  /api/threads/feed?category=&sort=hot|new|top&cursor=&pageSize=&q=` – `q` performs full-text search on title/body via Elasticsearch; first page is Redis-cached (5-min TTL)
        -   `GET  /api/threads/posts/{id}` – post detail
        -   `GET  /api/threads/posts/{id}/comments` – 2-level comment tree
        -   `POST /api/threads/posts` – create post (multipart; `isAnonymous`, `images[]`)
        -   `PATCH /api/threads/posts/{id}` – author edits title/body
        -   `DELETE /api/threads/posts/{id}` – author or admin soft-delete
        -   `POST /api/threads/posts/{id}/like` – toggle like
        -   `POST /api/threads/posts/{id}/comments` – add comment (`parentCommentId` optional, max 1 level)
        -   `POST /api/threads/comments/{id}/like` – toggle like on a comment

    #### Read path (threads search)

    Thread writes are propagated to Elasticsearch through a transactional-outbox pipeline:

    - **Transactional outbox:** write handlers append an `outbox_events` row in the same EF Core transaction as the domain change (no dual-write).
    - **OutboxPublisher:** `BackgroundService` that polls unpublished rows and publishes typed events to RabbitMQ via MassTransit.
    - **ThreadIndexingConsumer:** rebuilds each post's document from Postgres (anonymity applied at index time — anonymous posts never carry real identity in the index), then upserts/deletes it in the `threads` Elasticsearch index. Idempotency is enforced via a Redis-backed processed-key set.
    - **Redis hot-feed cache:** the consumer invalidates the cache on every index change; `GET /api/threads/feed` repopulates a 5-min TTL entry on first request.
    - The `threads` ES index is auto-created on startup. Required config: `Elastic__Uri` and `RabbitMq__Host` (both already documented).
-   **Moderation & Notifications**
    -   Users can report posts and comments (rate-limited to 10 reports per rolling hour per user via Redis).
    -   Admins review a report queue and resolve with `dismiss`, `remove-content` (soft-delete + Elasticsearch drop), or `warn-user`; every resolution is written to `moderation_audits`.
    -   The admin review queue (`GET /api/threads/reports`) is the single deliberate "anon-break": moderators see the real author of anonymous content, behind the `Admin` role and a dedicated `ModerationAuthor` DTO — public read paths are unaffected.
    -   Reply notifications are created asynchronously by `ThreadNotificationConsumer` (reacts to `ThreadCommentCreated`): top-level comment → `PostReplied` to post author; reply → `CommentReplied` to parent comment author. Self-replies and duplicate deliveries are suppressed (DB-existence idempotency).
    -   Anonymous repliers appear in notifications only by their stable handle (`ActorAnonHandleSnapshot`); real user identity is never stored.
    -   Controllers: `ModerationController`, `NotificationsController` (auto-discovered via `AddControllers`).
    -   Endpoints:
        -   `POST /api/threads/posts/{id}/report` – file a report on a post (rate-limited 10/hr)
        -   `POST /api/threads/comments/{id}/report` – file a report on a comment (rate-limited 10/hr)
        -   `GET  /api/threads/reports?status=open` – [Admin] review queue (reveals real author of anon content)
        -   `POST /api/threads/reports/{id}/resolve` – [Admin] dismiss | remove-content | warn-user (audited)
        -   `GET  /api/notifications` – reply notifications for the authenticated user (paginated)
        -   `GET  /api/notifications/unread-count` – unread badge count
        -   `POST /api/notifications/{id}/read` – mark one notification read
        -   `POST /api/notifications/read-all` – mark all notifications read
-   **Infrastructure**
    -   PostgreSQL (Npgsql), Redis, RabbitMQ, Elasticsearch clients configured.
    -   Cloudflare R2 storage abstraction (S3-compatible).
    -   Serilog logging, health checks, Swagger (with JWT auth support).
-   **Tooling**
    -   Docker Compose orchestrating Postgres, Redis, RabbitMQ, Elasticsearch, API.
    -   Seed script wiping and repopulating canonical demo data.

---

## Guardrails

-   Target framework: **.NET 8**.
-   Use existing architecture (Api/Application/Domain/Infrastructure/Contracts).
-   All secrets/config come from environment variables / `.env` (never commit secrets).
-   Write idempotent migrations and avoid destructive seed logic.
-   When extending endpoints, keep Swagger documentation accurate (response codes, example payloads where useful).
-   Update `README.md` and this file when behaviour/setup changes.
-   Prefer strong typing, explicit validation, and clear logging.

---

## High-level roadmap

### 1. Stability & Tests

-   Add unit/integration tests for critical flows (registration, activation, item CRUD & image handling).
-   Introduce GitHub Actions (or other CI) to run build + tests.
-   Harden error handling & logging around external services (SMTP, R2).

### 2. Authentication Enhancements

-   Optional: integrate institutional SSO/OIDC once credentials are available (keep local login as fallback).
-   Implement password reset flow (email token, new password endpoint).

### 3. Marketplace Features

-   Item search (Elasticsearch indexing + query endpoint).
-   Category & item administration (CRUD restrictions, moderation).
-   Item image reordering.
-   Pagination & filtering on item listing endpoints.

### 4. Messaging & Payments

-   Hook up RabbitMQ for future notifications.
-   Stub Stripe checkout endpoints (create intent, handle webhook) in preparation for payments.

### 5. Observability & Ops

-   Metrics/Tracing (OpenTelemetry, Application Insights or similar).
-   Production-ready Docker images (multi-stage with health checks, non-root user).
-   IaC skeleton (Terraform/Bicep) for deploying infrastructure.

Work on one theme at a time; keep PRs scoped.

---

## Definition of Done (for any change)

-   Compiles (`dotnet build`) and passes tests (once available).
-   Swagger reflects the new/changed endpoints.
-   Environment variables documented in README if new ones are added.
-   Seed logic updated if new canonical data is required (keep idempotent).
-   Relevant new behaviour covered by unit/integration tests (where applicable).
-   Docker Compose continues to start successfully (`docker compose up --build`).
-   `README.md` and `AGENTS.md` updated if developer workflow changes.

---

## Useful commands

```bash
# Restore & build
cd backend
dotnet restore
dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false

# Run backend unit tests
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false

# Apply migrations / update schema
ASPNETCORE_ENVIRONMENT=Development \
  dotnet ef database update \
    --project src/Infrastructure/Infrastructure.csproj \
    --startup-project src/Infrastructure/Infrastructure.csproj

# Run API locally without containers
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/Api/Api.csproj

# Regenerate Swagger JSON
dotnet tool install --global Swashbuckle.AspNetCore.Cli
dotnet swagger tofile --output swagger.json src/Api/bin/Debug/net8.0/Api.dll v1
```

---

## Developer checklist before opening a PR

-   [ ] Code builds locally (`dotnet build`).
-   [ ] Added/updated tests (if applicable) and they pass.
-   [ ] Updated migrations/scripts where schema changed.
-   [ ] Swagger annotations/response types accurate.
-   [ ] Documentation (`README.md`, `AGENTS.md`, comments) updated.
-   [ ] Environment variables defined in `.env.example` if new ones introduced.
-   [ ] Docker Compose tested (`docker compose up --build`).

Keep changes small, descriptive, and easy to review. Happy shipping! 🚀

# FRONTEND

🎨 🔧 Global Design System Prompt

“Design a mobile-first UI for a React Native app using the React Native Reusables (RNR) design system (https://reactnativereusables.com/).
CLI: npx @react-native-reusables/cli@latest init
Primary color: #836BFF (violet-indigo).
Style: modern, bright, trustworthy, and academic.
Use white backgrounds, soft gray (#F6F6F8) surfaces, and consistent corner radius (12–16px).
Typography: modern sans-serif, slightly rounded (e.g., Inter, Poppins).
Components: Appbar, Button, Card, Input, Avatar, List, Tabs, Chip, Snackbar.
Visual tone: youthful and calm with subtle elevation and shadows.
Focus on clear hierarchy, accessible contrast, and balanced spacing (16px grid).”

⸻

🔐 1️⃣ Auth Flow (Login / Register)

“Design a login and sign-up screen for a React Native app using the React Native Reusables system.
Use a soft white background, and the primary violet (#836BFF) for buttons and highlights.
Layout:
• App logo (simple university emblem or ‘AUM’ mark)
• Headline: ‘Sign in to Adelaide Uni Marketplace’
• Email + password fields using RNR Input components
• PrimaryButton (violet) labeled ‘Continue’
• Secondary text: ‘New here? Create an account’
Add subtle illustration or gradient accent with #836BFF → #B5A8FF.
Keep overall tone clean, minimal, and academic.”

⸻

🏠 2️⃣ Home / Category Screen

“Design a Home screen for a student marketplace app styled with React Native Reusables.
as UI framework:
• Appbar with title ‘Marketplace’
• Search Input at top (‘Search items, books, electronics…’)
• Horizontal scroll of Chip components (Books, Furniture, Tech, Clothing, etc.)
• Product cards: photo, title, price, seller avatar.
• Bottom Tabs: Home (violet active), Chat, Sell, Profile.
Background: #F9F9FB, Cards white with subtle shadow.
Highlight color: #836BFF for active states.”

⸻

📦 3️⃣ Product Detail Screen

“Design a Product Detail screen in the React Native Reusables style with primary color #836BFF.
Top: image carousel.
Below: Card with product title, price (bold), seller avatar & name, description section.
Two buttons pinned at bottom:
• PrimaryButton (violet): ‘Buy Now’
• OutlineButton: ‘Chat with Seller’
Layout uses 16px spacing, white background, soft shadows.”

⸻

💬 4️⃣ Chat Screen

“Design a chat interface in the style of React Native Reusables components.
Message bubbles with rounded radius (16).
Sent message background: #836BFF, white text.
Received message background: #F1F0FF.
Include bottom Input bar with icons for image, emoji, and send (violet accent).
Top Appbar with back arrow, avatar, and name of seller.
Clean, minimal, iOS-style spacing.”

⸻

🛒 5️⃣ Listing Form

“Design a New Listing form using React Native Reusables Input and Button components.
Fields: Title, Category dropdown, Price, Description, Photo upload grid.
Include violet (#836BFF) PrimaryButton (‘Post Item’) and light OutlineButton (‘Save Draft’).
Use scrollable layout with Cards grouping inputs.
Show upload progress bars in violet accent.
Keep soft gray background and rounded Cards.”

⸻

👤 6️⃣ Seller Dashboard

“Design a Seller Dashboard screen using Cards and Lists from React Native Reusables.
Sections:
• Stats summary (Active listings, Items sold, Chats).
• Quick Actions (Add Item, Manage Listings, View Orders).
• Recent messages preview list.
Color scheme: white background, violet accent (#836BFF), gray icons, subtle dividers.
Dashboard Cards use consistent spacing, slight elevation, and bold numbers in violet.”

⸻

⚙️ 7️⃣ Settings Screen

“Design a Settings screen styled with React Native Reusables and primary color #836BFF.
Use ListItem rows with icons and chevrons: Profile, Notifications, Payment, Help, Logout.
Include toggle switches (accent = violet).
Use light background, white Cards, and modern typography.
Top Appbar with title ‘Settings’ and subtle divider.”

⸻

✨ Optional — Full Flow Prompt

If you want Figma Make to auto-generate the whole mobile flow in one go:

“Design a full mobile UI flow for a React Native app using React Native Reusables components and primary color #836BFF.
Screens: 1. Login / Register 2. Home / Categories 3. Product Detail 4. Chat 5. Listing Form 6. Seller Dashboard
Style: minimalist, academic, and modern.
Use white backgrounds, soft violet accents (#836BFF), rounded corners, subtle shadows, and clean typography.
Keep spacing consistent (16–20px grid). Output in iPhone 13 frame (390×844).”
