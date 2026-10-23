# Warehouse.StockEngine

Transactional, idempotent stock engine for WMS. MariaDB (InnoDB) + Dapper + MySqlConnector.

## Operations

| Operation | Effect |
|-----------|--------|
| **Inbound** | Increase quantity at a bin |
| **Outbound** | Decrease quantity (rejects negative stock) |
| **Move** | Transfer between bins in a warehouse (2 history rows per line) |
| **Count** | Set absolute quantity (stores `counted_quantity` + optional line `ReasonCode` in history) |

Client-supplied `TransactionId` (UUID) is the idempotency key:

- same `TransactionId` + same request hash → success, `WasAlreadyProcessed = true`
- same `TransactionId` + different hash → `IdempotencyConflictException`

All work (claim transaction, inventory update, history insert, `completed_at`) runs in a single DB transaction.

Optional audit fields on every command: `Source`, `UserId`, `DeviceId`, `ReasonCode` (stored on `inventory_transaction`).

## Tables

1. `inventory` — current balances, PK `(warehouse, zone, bin, sku)`  
2. `inventory_history` — append-only ledger (`history_id` BIGINT AUTO_INCREMENT)  
3. `inventory_transaction` — idempotency claims + audit (`transaction_id`, `request_hash`, `status`, `source`, `user_id`, `device_id`, `reason_code`, `completed_at`)

Location identity is hierarchical: `warehouse` (INT), `zone` (INT), `bin` (INT), `sku` (BIGINT).  
`document_no` / `document_line_id` on history are nullable. One `TransactionId` may produce multiple history rows.

### Future improvement

Optimistic concurrency on `inventory` (e.g. row version) is not implemented yet; counts currently lock with `SELECT … FOR UPDATE` only.

## Solution layout

```
.
├── README.md
├── Warehouse.StockEngine.slnx
├── global.json
├── src
│   ├── Warehouse.StockEngine.Core
│   └── Warehouse.StockEngine.Infrastructure.MariaDb
└── tests
    └── Warehouse.StockEngine.Tests
```

## Requirements

- .NET SDK 10 (`global.json` pins `10.0.401`)
- Docker (Testcontainers MariaDB for integration / load tests)

## Quick start

```bash
dotnet restore
dotnet build
dotnet test
```

Load test (NBomber):

```bash
dotnet test --filter FullyQualifiedName~StockEngineLoadTests
```

## Usage

```csharp
var connections = new MariaDbConnectionFactory(connectionString);
await new MariaDbDatabaseMigrator(connections).MigrateAsync();

var engine = new MariaDbStockEngineFactory(connections).Create();

var result = await engine.InboundAsync(new InboundStockCommand(
    TransactionId: Guid.NewGuid(),
    DocumentNo: "ASN-100",
    Lines:
    [
        new InboundStockLine("1", Warehouse: 1, Zone: 10, Bin: 100, Sku: 10001, Quantity: 10m)
    ],
    Source: "WMS",
    UserId: "operator-1"));
```
