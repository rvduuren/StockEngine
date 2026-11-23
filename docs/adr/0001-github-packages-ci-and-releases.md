# GitHub Packages releases, tag-only publish, and CI test scope

StockEngine ships two NuGet packages—`Warehouse.StockEngine.Core` and `Warehouse.StockEngine.MariaDb`—to the **rvduuren GitHub Packages** feed only. We do **not** publish to nuget.org. Package **version is not computed in the repo** (no MinVer or similar); on a matching release, CI reads the **git tag** (`v*` SemVer, `v` stripped) and assigns that version to **both** packages in lockstep so consumers always get a matching Core + MariaDb pair. **Pushes and PRs to `main` run build and test but do not publish**; pack and push happen only on **`v*` tags** (see issue #24 for workflow implementation). Default CI runs the full test suite **except** NBomber load tests (`FullyQualifiedName!~StockEngineLoadTests` in [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)); load tests stay a deliberate local or tag-pipeline step. CI uses **ubuntu-latest** with **Docker** available so MariaDB **Testcontainers** integration tests can start MariaDB 11.4.

## Considered Options

| Option | Outcome |
|--------|---------|
| nuget.org + GitHub Packages | Rejected: single private/org feed under rvduuren control |
| Publish on every green `main` | Rejected: releases are explicit semver tags, not each merge |
| MinVer / git height in csproj | Rejected: version comes from the release tag in CI only |
| NBomber in default CI | Rejected: slow, resource-heavy; excluded via test filter |

## Consequences

- Agents and contributors must run load tests locally (Docker required) before relying on NBomber coverage; CI green does not imply load-test green.
- Release automation depends on correct `v*` tags and validated SemVer in the publish workflow—not on in-repo version properties.