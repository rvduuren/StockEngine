# Workflow integration testing

Patterns for stock-engine and workflow integration tests (receipts, future workflows). Master-data repository tests live in [`testing.md`](testing.md).

## Data integrity invariant

Per `(warehouse, zone, bin, sku)` (product):

**`Sum(history quantity_delta) == inventory.quantity`**

Workflow and engine tests should keep this true for every scenario they mutate inventory. When a test only asserts a façade read model (e.g. staging quantity via `GetQuantityAsync`), the ledger may still be wrong—prefer asserting outcomes that imply a consistent ledger, or run a targeted history/inventory check.

### Post-run audits (Checks A / B / C)

NBomber load tests run SQL audits after the scenario in `tests/Warehouse.StockEngine.Tests/Performance/StockEngineLoadTests.cs`:

| Check | What it verifies |
|-------|------------------|
| **A** | `SUM(quantity_delta)` from `inventory_history` per lot matches `inventory.quantity` |
| **B** | Completed `inventory_transaction` rows have `completed_at`; history rows reference existing transactions |
| **C** | No negative `inventory.quantity` |

Reuse that SQL shape in new endurance or workflow stress tests; integration tests normally assert the invariant implicitly via balance + history (see below).

## Where tests live

```
tests/Warehouse.StockEngine.Tests/Integration/
├── MasterData/          # IMasterDataRepository — see testing.md
└── StockWorkflow/       # IReceiptWorkflow and future workflow ports
```

Engine operation tests (`InboundTests`, `MoveTests`, etc.) sit directly under `Integration/`.

## Workflow integration expectations

Call the **workflow port under test** (`IReceiptWorkflow`, etc.), not ad-hoc SQL against workflow tables unless asserting persistence details.

| Path | Minimum assert |
|------|----------------|
| Successful **confirm receive** / engine call | Staging or target balance via `GetQuantityAsync` (or workflow read model) **and** `inventory_history` carries the expected `document_no` and `document_line_id` for the receipt line |
| **Idempotent replay** (same client confirm id or engine `TransactionId`) | Unchanged balance **and** no duplicate engine side effects (e.g. single history row count for that `transaction_id`) |
| **Failure / rollback** | Workflow audit rows and line received qty unchanged when the engine step fails mid-transaction |

### Reference: engine-level history assert

`MoveTests.Move_TransfersQuantity_AndWritesTwoHistoryRowsPerLine` queries `inventory_history` by `transaction_id`—same pattern for workflow confirms that call inbound on the engine.

## Review bar

Before closing a workflow issue, map spec scenarios to named integration tests and note any spec that requires ledger traceability or replay footprint; those need explicit `inventory_history` assertions, not quantity alone.