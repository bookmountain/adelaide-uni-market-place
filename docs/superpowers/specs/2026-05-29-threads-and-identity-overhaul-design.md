# Threads + Identity Overhaul — Design Spec

**Date:** 2026-05-29
**Status:** Draft, awaiting user review
**Scope:** First subsystem of the Adelaide Uni Marketplace overhaul into a Dcard-style trust-gated student platform.

## 1. Background & Motivation

The platform today is an Adelaide-uni-gated marketplace (items, images, chat, orders, reviews) on ASP.NET Core 8 + Postgres + Redis + RabbitMQ + Elasticsearch + Cloudflare R2, with an Expo React Native iOS-first client. Email registration is gated to `@adelaide.edu.au`, activation flow exists, JWT auth in place.

The product is being repositioned: marketplace stays the anchor (real identity, sell physical items), and a Reddit-style **Threads** surface is added to absorb every other student-life need — housemate hunting, splitting paid memberships (Spotify, Netflix), textbook swaps, rides, course chatter, lost-and-found, events. Threads are organized into admin-curated categories; each post is created either with the author's real identity or under their stable anonymous handle. A future Draw-Card subsystem and push-notification system are out of scope for this spec but are accounted for in the data model (`appear_in_draw_pool`, `notifications` table).

This spec covers the Threads subsystem and the folded-in identity/profile overhaul. Marketplace polish, Draw-Card, and push notifications are queued as follow-up specs.

## 2. Goals & Non-Goals

**Goals**
- Reddit-style threads inside admin-curated categories
- Per-post identity choice: real name OR stable per-user anonymous handle
- Likes-only voting (no downvotes) with Hot / New / Top sorting
- Nested comments capped at one reply level (max depth 2)
- Image attachments via existing R2 abstraction
- In-app notifications (no push)
- Moderation via user reports + admin review queue
- Identity work: bio, anon-handle generation, draw-pool opt-in toggle, refresh-token auth flow
- Trust model enforced at the API: anon posts never leak `author_user_id` to clients
- Exercise the existing infrastructure: Postgres, Redis, RabbitMQ, Elasticsearch

**Non-Goals (each becomes its own future spec)**
- Draw-Card subsystem
- Push notifications (APNs/FCM)
- Marketplace polish (search, filters, pagination)
- Group chat
- User-creatable subthreads (admin-curated only for now)
- Reputation/karma scoring

## 3. Architecture

Single bounded context inside the existing clean-architecture monolith — no service split. Postgres is source of truth, Elasticsearch is the denormalized read view for feeds and search, Redis is the auth-state + read cache, RabbitMQ decouples writes from the indexer.

```
Mobile ─POST /api/threads/posts─▶ ThreadsController
                                    │
                                    ▼
                          Application command handler
                                    │
                                    ▼
                          EF Core transaction:
                          ├─ thread_posts INSERT
                          └─ outbox_events INSERT
                                    │
                          ┌─────────┴──────────┐
                          ▼                    ▼
                   OutboxPublisher      (response to client)
                   (BackgroundService)
                          │
                          ▼
                     RabbitMQ
                     aum.threads topic
                          │
                          ▼
                   ThreadsIndexer (BackgroundService)
                   ├─ Idempotency check (Redis SET)
                   ├─ Upsert ES document + hot_rank
                   ├─ DEL threads:feed:*:hot caches
                   └─ Mark eventId processed

Mobile ─GET /api/threads/feed─▶ ThreadsController
                                    │
                                    ▼
                          Read cache: threads:feed:{slug}:hot
                                    │   (miss)
                                    ▼
                          ES query (filtered + sorted + search_after cursor)
                                    │
                                    ▼
                          Response
```

**Layer responsibilities**

| Layer | Responsibility |
|---|---|
| Postgres | Source of truth for all writes; transactional integrity; outbox for events |
| Elasticsearch | Denormalized post documents for feed reads and free-text search; hot-rank field |
| RabbitMQ | Event bus from outbox publisher to indexer (and to any future consumers) |
| Redis | Auth state (refresh tokens, login rate-limit), category list cache, top-N feed cache, anon-handle lookup, indexer idempotency set |
| Cloudflare R2 | Post image storage, reusing the existing abstraction used by Items |

**Indexer hosting**: in-process `BackgroundService` in the API for MVP. Lifts to its own service later with zero domain-code change because the contract is the RabbitMQ topic.

## 4. Data Model

### Postgres — new tables

```
thread_categories
  id              uuid PK
  slug            text UNIQUE
  name            text
  description     text
  icon_key        text
  sort_order      int
  is_active       bool
  created_at, updated_at

thread_posts
  id                  uuid PK
  category_id         uuid FK -> thread_categories
  author_user_id      uuid FK -> users
  is_anonymous        bool                       -- immutable after publish
  title               text
  body                text
  like_count          int DEFAULT 0
  comment_count       int DEFAULT 0
  last_activity_at    timestamptz
  is_pinned           bool DEFAULT false
  is_locked           bool DEFAULT false
  is_deleted          bool DEFAULT false         -- soft delete
  created_at, updated_at

thread_post_images
  id           uuid PK
  post_id      uuid FK -> thread_posts
  r2_key       text
  ordinal      int

thread_comments
  id                  uuid PK
  post_id             uuid FK -> thread_posts
  parent_comment_id   uuid FK -> thread_comments NULL    -- max one level
  author_user_id      uuid FK -> users
  is_anonymous        bool                                -- immutable after publish
  body                text
  like_count          int DEFAULT 0
  is_deleted          bool DEFAULT false
  created_at, updated_at

thread_likes
  user_id      uuid FK -> users
  target_type  text CHECK ('post','comment')
  target_id    uuid
  created_at
  PK (user_id, target_type, target_id)

thread_reports
  id                    uuid PK
  reporter_user_id      uuid FK -> users
  target_type           text CHECK ('post','comment')
  target_id             uuid
  reason                text CHECK ('spam','harassment','nsfw','scam','other')
  notes                 text
  status                text CHECK ('open','reviewed','dismissed') DEFAULT 'open'
  reviewed_by_user_id   uuid FK -> users NULL
  reviewed_at           timestamptz NULL
  created_at

notifications
  id                          uuid PK
  recipient_user_id           uuid FK -> users
  type                        text CHECK ('post_replied','comment_replied')
  source_post_id              uuid FK -> thread_posts
  source_comment_id           uuid FK -> thread_comments NULL
  actor_user_id               uuid FK -> users NULL          -- present for real-name actors
  actor_anon_handle_snapshot  text NULL                       -- present for anon actors
  is_read                     bool DEFAULT false
  created_at

outbox_events
  id              uuid PK
  aggregate_id    uuid
  event_type      text
  payload_json    jsonb
  occurred_at     timestamptz
  published_at    timestamptz NULL

moderation_audit
  id              uuid PK
  admin_user_id   uuid FK -> users
  target_type     text
  target_id       uuid
  action          text
  reason          text
  created_at
```

### Postgres — `users` table extensions

```
ALTER TABLE users ADD COLUMN bio                  text
ALTER TABLE users ADD COLUMN anon_handle          text UNIQUE     -- nullable; lazily created
ALTER TABLE users ADD COLUMN appear_in_draw_pool  bool DEFAULT false
ALTER TABLE users ADD COLUMN is_admin             bool DEFAULT false
```

### Indexes

- `thread_posts (category_id, last_activity_at DESC)` — feed fallback when ES is unavailable
- `thread_posts (author_user_id, created_at DESC)` — "my posts"
- `thread_comments (post_id, created_at)` — comment rendering
- `thread_comments (parent_comment_id)` — nested reply lookup
- `thread_likes (user_id, created_at DESC)` — "my likes"
- `thread_reports (status, created_at)` — admin queue
- `notifications (recipient_user_id, is_read, created_at DESC)` — notification list
- `outbox_events (published_at NULLS FIRST, id)` — publisher polling

### Elasticsearch — `threads` index

```json
{
  "post_id": "uuid",
  "category_slug": "housemate",
  "author_handle": "Sarah Chen" | "quiet-koala-4821",
  "is_anonymous": false,
  "title": "...",
  "body": "...",
  "image_count": 2,
  "like_count": 14,
  "comment_count": 6,
  "hot_rank": 0.847,
  "created_at": "2026-05-29T...",
  "last_activity_at": "2026-05-29T...",
  "is_deleted": false
}
```

Comments are not indexed in ES — they load from Postgres on post detail.

### Anonymous handle generation

`adjective-noun-NNNN` (e.g. `quiet-koala-4821`). Created lazily on the user's first anonymous post, persisted to `users.anon_handle`, never regenerated. Collision retry up to 5 attempts before failing the write.

## 5. API Surface

All routes JWT-authenticated unless noted.

**Categories**
```
GET    /api/threads/categories                   public-read; cached
POST   /api/threads/categories          [admin]
PATCH  /api/threads/categories/{id}     [admin]
```

**Posts**
```
GET    /api/threads/feed?category=&sort=hot|new|top&q=&cursor=
GET    /api/threads/posts/{id}
POST   /api/threads/posts                        multipart form-data
                                                 body: { categoryId, title, body, isAnonymous, images[] }
PATCH  /api/threads/posts/{id}                   owner-only; title + body only
                                                 (isAnonymous is IMMUTABLE)
DELETE /api/threads/posts/{id}                   owner OR admin -> soft delete
POST   /api/threads/posts/{id}/like              idempotent toggle
POST   /api/threads/posts/{id}/report
```

**Comments**
```
GET    /api/threads/posts/{id}/comments?cursor=
POST   /api/threads/posts/{id}/comments          body: { body, isAnonymous, parentCommentId? }
                                                 server rejects if parentCommentId already has a parent
PATCH  /api/threads/comments/{id}                owner; body only
DELETE /api/threads/comments/{id}                owner OR admin
POST   /api/threads/comments/{id}/like
POST   /api/threads/comments/{id}/report
```

**Profile & identity**
```
PATCH  /api/users/me                             body: { bio?, appearInDrawPool? }
GET    /api/users/me/anon-handle                 returns handle; creates if missing
```

**Auth (new)**
```
POST   /api/auth/refresh                         body: { refreshToken } -> new { accessToken, refreshToken }
POST   /api/auth/logout                          revokes current refresh token
POST   /api/auth/logout-all                      revokes all refresh tokens for the user
```

**Moderation**
```
GET    /api/threads/reports?status=open          [admin]
POST   /api/threads/reports/{id}/resolve         [admin] body: { action: 'dismiss'|'remove-content'|'warn-user' }
```

**Notifications**
```
GET    /api/notifications?cursor=
POST   /api/notifications/{id}/read
POST   /api/notifications/read-all
```

### Author resolution rule (trust model)

The API layer is the single enforcement point for anonymity. For any response containing post or comment data:

- If `is_anonymous = true`: DTO includes `authorHandle` (resolves to `users.anon_handle`) and **no** other author fields (`authorUserId`, `authorDisplayName`, `authorAvatarUrl`, `authorEmail` all absent).
- If `is_anonymous = false`: DTO includes `authorUserId`, `authorDisplayName`, `authorAvatarUrl`.

Mobile clients cannot link anon posts to real users because the linking data is never serialized. A contract test (Section 10) asserts this invariant across every read endpoint.

### Rate limits (Redis sliding window)

```
POST /threads/posts          10 / hour / user
POST /threads/comments       60 / hour / user
POST /threads/*/like         300 / hour / user
POST /threads/*/report       10 / hour / user
POST /auth/login             10 / 15 min / IP, 5 / 15 min / email
```

429 response includes `Retry-After` header.

## 6. Redis Keys & Cache Strategy

```
auth:login-fail:ip:{ip}              counter, TTL 15min, block at 10
auth:login-fail:email:{email}        counter, TTL 15min, block at 5
auth:refresh:{jti}                   { userId, expiresAt }, TTL 14d
auth:refresh-by-user:{userId}        SET of jtis (logout-all)
auth:activation:{token}              { userId, purpose }, TTL 24h
auth:password-reset:{token}          { userId }, TTL 1h
threads:categories                   JSON list, TTL 1h, invalidated on admin write
threads:feed:{slug}:hot              JSON top-50 summaries, TTL 5min, invalidated by indexer
threads:feed:global:hot              JSON top-50 summaries, TTL 5min, invalidated by indexer
users:anon-handle:{userId}           string, no TTL, write-once
ratelimit:{route}:{userId}           sliding window, TTL = window
indexer:processed:{date}             SET of eventIds, TTL 48h (idempotency)
```

**Invalidation rules**
- Categories list invalidated synchronously inside the admin write transaction.
- Feed caches invalidated by the **indexer** after ES is updated. Centralizes downstream sync; brief ≤1 sec staleness window is acceptable for hot feeds.
- Anon handle is write-once, never invalidated.

**Activation / password-reset tokens** move from a Postgres column on `users` to Redis-only. Acceptable risk because tokens are 24h-transient by design and a user can always click "Resend". If Redis loses its dump file between restart and a user clicking their email, they request a new token.

**Auth refresh flow** — current stateless JWT becomes:
- Access JWT: 15-min lifetime, returned on login/refresh
- Refresh token: opaque 32-byte random, 14d TTL in Redis, rotated on every refresh
- `logout` deletes one refresh entry; `logout-all` iterates the per-user SET

**Failure modes** — if Redis is down:
- Auth rate-limit becomes permissive (logged warning, not blocking — prefer not locking users out on an ops failure)
- Refresh flow fails closed (users re-login when 15-min access token expires)
- Feed cache misses fall through directly to ES
- Health probes `/healthz/redis`, `/healthz/elastic`, `/healthz/rabbitmq` expose status

## 7. Events, Outbox, Indexer

### Event contracts

```
exchange: aum.threads (topic)
```

Envelope (all events share this shape; `data` varies by routing key):
```json
{
  "eventId": "uuid",
  "occurredAt": "iso8601",
  "aggregateId": "uuid",        // post id or comment id
  "actorUserId": "uuid",
  "data": { ... }
}
```

Per-event `data` shapes:

| Routing key | `data` payload |
|---|---|
| `thread.post.created` | `{ postId, categoryId, categorySlug, authorUserId, isAnonymous, title, body, imageCount }` |
| `thread.post.updated` | `{ postId, title, body }` |
| `thread.post.deleted` | `{ postId }` |
| `thread.post.liked` | `{ postId, userId, newCount, delta }` (delta = +1 or -1) |
| `thread.comment.created` | `{ commentId, postId, parentCommentId, authorUserId, isAnonymous, body }` |
| `thread.comment.deleted` | `{ commentId, postId }` |
| `thread.comment.liked` | `{ commentId, postId, userId, newCount, delta }` |

Comment events carry `postId` so the indexer can update the parent post's `comment_count` and `last_activity_at` in the ES document without an extra Postgres read.

### Transactional outbox

API writes the domain change and an `outbox_events` row in the same EF Core transaction. An `OutboxPublisher` `BackgroundService` polls `WHERE published_at IS NULL` (small batches, short interval), publishes to RabbitMQ, sets `published_at`. Guarantees events are eventually published exactly the same set of times the writes succeeded — no dual-write race.

### ThreadsIndexer

Subscribes to `aum.threads` with a durable queue. Per event:
1. Idempotency check (skip if `eventId` already in `indexer:processed:{date}` Redis SET)
2. Upsert / delete the ES document; recompute `hot_rank` using `(like_count + 2*comment_count) / (hours_since_post + 2)^1.8`
3. Invalidate `threads:feed:{slug}:hot` and `threads:feed:global:hot`
4. Mark `eventId` processed in Redis (48h TTL)

On exception: NACK with requeue. After 3 redeliveries, dead-letter to `aum.threads.dlq` for manual review.

### Notifications

On `thread.comment.created`:
- If the parent is a post and the post author is not the commenter, write a `post_replied` notification to the post author
- If the parent is a comment, write a `comment_replied` notification to the parent comment's author

`actor_anon_handle_snapshot` is captured at write time (defensive against future anon-handle changes, even though MVP doesn't support rotation). Real-name actors store `actor_user_id` and resolve on read.

## 8. Moderation

- Reports land in `thread_reports` with `status=open`, rate-limited to 10/hour/user
- Admin (`users.is_admin=true`, seeded for the operator) views the queue and resolves with `dismiss`, `remove-content` (soft-deletes target, emits delete event so ES + caches drop it), or `warn-user` (logged only in MVP)
- **Anon-break for moderation**: admin endpoints return `author_user_id` alongside anonymous content for review. Every admin action writes to `moderation_audit` for accountability. Policy documented in onboarding and TOS.

## 9. Error Handling

| Code | When |
|---|---|
| 400 | FluentValidation failures — `ProblemDetails` response (existing pattern) |
| 401 | Missing / invalid JWT |
| 403 | Non-owner edit, non-admin moderation, posting to a locked category |
| 404 | Missing resource (includes soft-deleted unless caller is owner/admin) |
| 409 | Concurrency conflict (rare; like-toggle is idempotent) |
| 429 | Rate-limit hit, `Retry-After` header |
| 502 | Upstream service failure (R2 upload, etc.) — partial state cleaned up |
| 503 | Health check failure surfaced by `/healthz/*` |

External-service errors logged via Serilog at `Warning` with `eventId` for correlation. R2 failure on post-with-images = post not saved, client gets 502. Indexer failures do not block writes — the post lands in Postgres, the outbox event waits, the indexer retries with backoff.

## 10. Testing Strategy

**Unit tests** (extend existing `Application.UnitTests`)
- All command/query handlers: authorization rules, validation, anon-handle generation (including collision retry), hot-rank math, author-resolution rule
- Negative test: anon post responses must not contain `authorUserId`, `authorEmail`, `authorDisplayName`

**Integration tests** (new project `Threads.IntegrationTests`, `Testcontainers.dotnet`)
- Full happy path: API request → Postgres write → outbox → RabbitMQ → indexer → ES → Redis invalidation, asserting feed reads reflect writes within a 1-second window
- Auth: refresh-token rotation, logout-all revocation across multiple sessions, login rate-limit thresholds (extends the tests added in `dc524b5`)
- Moderation: anon-break for admin review writes a `moderation_audit` row

**Contract test — anon-leak guard**
A parametric test that hits every read endpoint with both anon and real-name fixture data and asserts every anon response object has no `authorUserId`, `authorEmail`, or `authorDisplayName` field. A future contributor adding a leaky field fails CI before merge.

## 11. Migration & Rollout

One PR ladder, no downtime:

1. EF Core migration: new tables, ALTER users, seed initial categories + admin user in `db/seed.sql` (idempotent)
2. Wire `OutboxPublisher` and `ThreadsIndexer` hosted services
3. Ship API endpoints behind feature flag `ThreadsFeature__Enabled` (env var, defaults off until mobile client catches up)
4. Mobile client: bottom-nav Threads tab, post composer with anon toggle, feed list, post detail, comment threads, notifications badge
5. Activation/password-reset token migration — Redis writes go first, Postgres columns deprecated in a follow-up release once stable

`docker-compose` already orchestrates Postgres, Redis, RabbitMQ, Elasticsearch — no infra additions needed.

## 12. Out of Scope (Follow-up Specs)

- Draw-Card subsystem (data model already accounts for `appear_in_draw_pool`)
- Push notifications via APNs / FCM
- Marketplace polish (search, filters, pagination, image reordering)
- Group chat
- User-creatable subthreads
- Reputation / karma scoring
- Cross-system search (one ES index per domain for now)

## 13. Open Visual Question

Bottom-nav layout on mobile — where Threads slots in alongside the existing Home / Chat / Sell / Profile tabs, and where the future Draw-Card button sits. This will be resolved during implementation planning using the visual companion.
