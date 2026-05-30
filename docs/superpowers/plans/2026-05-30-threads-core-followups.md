# Plan 2 (Threads Core) — Fast-Follow Items

From the final holistic review. None blocked the merge; capture as small follow-up tickets.

## Performance
- **Feed materializes the full candidate set.** `GetThreadFeedQueryHandler` does `await query.ToListAsync()` then `.OrderByDescending(LastActivityAt).Take(500)` in memory — a SQLite `ORDER BY DateTimeOffset` test workaround that also runs in production. On Postgres this should push `OrderByDescending(...).Take(CandidateWindow)` to SQL to avoid loading the whole non-deleted post set. Largely moot once **Plan 3** replaces this handler with the Elasticsearch read model, but worth a DB-side bound in the interim if the feed ships before Plan 3.

## Correctness / consistency
- **Comments endpoint lacks a soft-deleted-post guard.** `GetThreadCommentsQueryHandler` filters by `PostId` only; hitting `GET /api/threads/posts/{deletedId}/comments` directly returns 200 with the tree instead of 404/empty (`GetThreadPost` already 404s for deleted posts, so normal navigation never reaches it; anonymity still holds). Add a `ThreadPosts.AnyAsync(p => p.Id == postId && !p.IsDeleted)` short-circuit returning empty.
- **`Excerpt` truncates by char index** (`body[..200]`) and can split a surrogate pair/grapheme. Cosmetic (feed summary only).

## Test coverage (cheap, non-blocking)
- Assert the feed `"hot"` sort ordering (only `new`/`top` are asserted today; `HotScore` is pure/deterministic — one ordering test locks it in).
- Cover `CreateThreadPostCommandValidator` 8-image limit and `CreateThreadCategory` duplicate-slug rejection at the validator/handler boundary.

## Carried from design (intended later plans)
- Reports, admin moderation queue, anon-break-for-admin, moderation audit → **Plan 4**.
- Notifications → **Plan 4**.
- Outbox → RabbitMQ → Elasticsearch indexer + Redis feed cache (replaces the provisional feed) → **Plan 3**.
