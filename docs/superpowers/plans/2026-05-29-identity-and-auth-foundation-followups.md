# Plan 1 (Identity & Auth Foundation) — Fast-Follow Items

These came out of the final holistic code review. None blocked the merge; capture as small follow-up tickets.

## Security / correctness
- **Logout ownership check.** `POST /api/auth/logout` revokes whatever `refreshToken` is in the body without verifying it belongs to the authenticated caller. Low impact (the token is a 256-bit secret the caller must already possess; revoke-only), but `logout` should confirm the token's owner matches the caller's user id before revoking. (`LogoutCommandHandler`, `AuthController.Logout`)
- **Atomic refresh rotation.** Refresh validate-then-revoke is two separate awaits, so concurrent requests with the same refresh token could both validate before either revokes (replay window). Harden with an atomic validate-and-revoke in `RedisRefreshTokenStore` (Redis `GETDEL` or a small Lua script). Already noted in a code comment at the rotation site.

## Quality / efficiency
- **Redis service lifetimes.** `RedisRefreshTokenStore` and `RedisLoginRateLimiter` are registered `Scoped` but hold no scoped state (they depend only on the singleton `IConnectionMultiplexer` + `IOptions`). Flipping them (and the stateless `AppJwtTokenService` / `DefaultAnonHandleGenerator`) to `Singleton` avoids per-request allocation. Valid as-is; purely efficiency.
- **PATCH semantics for `AppearInDrawPool`.** `UpdateProfileRequest.AppearInDrawPool` is a non-nullable `bool`, so a PATCH omitting it silently sets `false`. If partial-update semantics are wanted, make it `bool?` and leave unchanged when null. Fine if the client always sends the full object.
- **`Retry-After` precision.** The 429 response returns the full `LoginFailureWindowMinutes * 60` rather than the offending Redis key's remaining TTL. Safe over-estimate; refine if precise backoff matters.

## Test coverage
- **`MaxAttempts` exhaustion.** `GetOrCreateAnonHandleCommandHandler` throws after 5 colliding candidates; this branch is unit-testable today with a `QueuedGenerator` returning 5 colliding values + a taken row. Cheap to add.
- **Redis impls + refresh-replay** are deferred to live-Redis integration tests (a later plan), as intended.
