# Plan 4 (Moderation & Notifications) — Fast-Follow Items

From the final holistic review. The blocking issue (notification idempotency not concurrency-safe — non-unique `SourceCommentId` index) was FIXED in this branch (unique index + graceful `DbUpdateException` no-op + constraint test). The items below are minor and were left as follow-ups.

## Robustness
- **Rate-limit exception coupling.** `ThreadsController.FileReport` maps the rate-limit case to HTTP 429 by string-matching `"too many"` in the `InvalidOperationException` message; a copy edit to the message would silently turn 429 into 400. Introduce a dedicated `RateLimitExceededException` (or a result type) for robust mapping. Tested behavior today, low priority.
- **Redis rate-limiter expiry race.** `RedisReportRateLimiter` (and the pre-existing `RedisLoginRateLimiter` from Plan 1) do `INCR` then `EXPIRE` as two ops; a crash between them leaves a key with no TTL (permanent rate-limit for that user). Replace with an atomic Lua `INCR`+conditional-`EXPIRE` and apply the same fix to both limiters for consistency.

## Performance
- **`GetReportQueue` N+1.** The admin queue handler issues one target lookup + one user lookup per report in a loop. Fine for a modest open queue; batch-load by target/author ids if the queue grows.

## Carried from design (future, out of scope)
- Email/push delivery of notifications (APNs/FCM) — in-app only today.
- Auto-moderation / spam scoring; user bans/suspensions (warn-user is a logged no-op for now).
- Report deduplication (multiple users reporting the same target create multiple rows).
