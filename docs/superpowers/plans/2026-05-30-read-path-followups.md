# Plan 3 (Read Path) — Fast-Follow Items

From the final holistic review. The one blocking issue (idempotency marked before work → dropped index updates on transient failure) was FIXED in this branch, along with the `Outbox`→`EfOutbox` rename. The items below are minor and were left as follow-ups.

## Robustness
- **Event-ordering comment.** Out-of-order processing of a post's events is self-healing because the indexer rebuilds from current Postgres state and `DeleteAsync` is terminal (a "post changed" event for an already-soft-deleted post yields `BuildAsync == null` → delete). Add a one-line comment in `ThreadIndexingService` documenting that ordering safety relies on "rebuild reflects current DB state + delete is terminal," so a future refactor doesn't start trusting event payloads for content.
- **Index bootstrapper lazy fallback.** `ThreadSearchIndexBootstrapper` catches index-creation failure and logs "the indexer will create the index lazily" — but a lazily auto-created index gets dynamic mappings, not the intended `keyword`/`text`/`double` mapping (e.g. `CategorySlug` would be `text`+`keyword`, `HotRank` sort could differ). Low-risk in practice (ES is up by indexing time). Either tighten the comment or have the upsert path ensure the index exists with the explicit mapping.

## Reliability hardening (carried)
- **DLQ / poison-message handling.** MassTransit's default retry/error queue applies, but consider an explicit retry policy + dead-letter for `ThreadIndexingConsumer` so a persistently-failing event is observable rather than looping.
- **Outbox growth.** `outbox_events` rows are never pruned. Add a periodic cleanup of `PublishedAt < now - N days` (a small background sweep) before this runs long in production.

## Cosmetic
- ES feed pagination uses `from`/`size` (offset). Fine for the feed's depth; switch to `search_after` if deep paging is ever needed (noted in the plan's out-of-scope).

## Carried from design (Plan 4)
- Reports, moderation queue, anon-break-for-admin, moderation audit.
- In-app notifications — the indexer/consumer is the natural place to also emit notification rows.
