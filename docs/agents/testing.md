# Testing (master data)

Patterns for master-data integration and review coverage. Workflow ports and ledger invariants: [`workflow-testing.md`](workflow-testing.md).

Packaging, CI scope, and release policy: [ADR-0001](../adr/0001-github-packages-ci-and-releases.md).

## CI vs local `dotnet test`

| | CI ([`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)) | Local (full suite) |
|---|-----|--------|
| **Triggers** | PRs and pushes to `main` | Anytime |
| **Configuration** | Release | Usually Debug or Release |
| **Test scope** | All tests **except** NBomber load (`FullyQualifiedName!~StockEngineLoadTests`) | Unfiltered `dotnet test` includes `StockEngineLoadTests` |
| **Docker** | Required (GitHub-hosted runner provides Docker for Testcontainers) | Required for integration and load tests |
| **NuGet publish** | **No** — merge gate is test-only | N/A |

A green CI run does **not** mean load tests passed; run them locally when changing concurrency, performance, or ledger audits:

```bash
dotnet test --filter FullyQualifiedName~StockEngineLoadTests
```

**Tag-only publish:** pushing to `main` or merging a PR does **not** push NuGet packages. Releases are **`v*` git tags** that drive pack/push to **GitHub Packages** (never nuget.org), with lockstep versions for Core and MariaDb—see ADR-0001 and issue #24.

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