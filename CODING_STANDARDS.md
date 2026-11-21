# Coding standards (index)

Reviewers start here; implementation detail lives in the linked docs. Do not duplicate rules in this file.

| Topic | Source |
|-------|--------|
| Architecture, layers, migrations, engine façade | [`PROJECT_OVERVIEW.md`](PROJECT_OVERVIEW.md) |
| Domain vocabulary and ADRs | [`docs/agents/domain.md`](docs/agents/domain.md) → `GLOSSARY.md`, `docs/adr/` |
| Master-data integration tests | [`docs/agents/testing.md`](docs/agents/testing.md) |
| Workflow integration tests, data-integrity invariant | [`docs/agents/workflow-testing.md`](docs/agents/workflow-testing.md) |
| Issues and acceptance | [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md) |

Mechanical rules (build, analyzers, project references) are enforced by `dotnet build` and the solution layout—not restated here.