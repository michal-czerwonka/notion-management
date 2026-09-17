## task-xp.md

Consider whether Task XP should eventually split `delivery-receipt`, `task-state`, `xp-event`, and `xp-total` into separate Cosmos DB containers. Separate containers could improve operational clarity and allow independent retention, indexing, and scaling policies, especially because receipts may be short-lived while XP history is permanent.

The current single-container design deliberately keeps all documents in one partition so receipt, state, event, and total can be updated atomically. A multi-container design would trade that guarantee for eventual consistency and require idempotent processing, retries, reconciliation, and clear handling of temporarily inconsistent projections. Revisit this when retention, scale, or ownership requirements make the trade-off worthwhile.
