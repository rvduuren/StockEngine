# Testing

Patterns for integration and review coverage in this repo.

## Shared repository CRUD parity

Master data repositories share `MariaDbRepositoryBase` / `IMasterDataRepository<T>`. Integration tests in `tests/**/Integration/MasterData/` must give **each concrete entity** (`Warehouse`, `Product`, `Zone`, `Bin`, and any new one) its own coverage for every shared method you rely on—not only the first entity you wired up.

Minimum bar when those methods exist on the base interface:

| Shared path | Assert |
|-------------|--------|
| `GetAllAsync` | Created entity appears in the list |
| `GetByIdAsync` | Returns the created row (add per entity if only one entity had it) |
| `UpdateAsync` | Returns `false` for a non-existent `Id` |

Entity-specific behavior (FK delete rules, name search, duplicate codes) still gets dedicated tests; this rule is only for **inherited** CRUD shared across types.

When you add a master-data entity or a new method on the base repository, add or extend tests so **entity parity** holds before the change is done.