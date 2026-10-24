# CLAUDE.md — Tests

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Testing

No mocking framework is used — generators and validators are instantiated directly. Integration tests use the EF Core InMemory provider.

**Runner:** xunit.v3 on the Microsoft Testing Platform (MTP v2), fixed repo-wide in `global.json` (#497). Every test project in this repo must use MTP too — `dotnet test` cannot mix runners. MTP syntax differs from VSTest: the project is passed with `--project`, filters use `--filter-class`/`--filter-method` (xunit options) instead of `--filter`, coverage is `--coverage --coverage-output-format cobertura` (`Microsoft.Testing.Extensions.CodeCoverage`; coverlet does not run under MTP). Results land in `TestResults\` of the current directory (git-ignored).

Die Tests der Spielplan-Dienste bauen ihre Dienste über `Spielplan/SpielplanDienste` (ein Objektgraph wie in der App) und ihre Turniere über `Spielplan/SpielplanTestdaten` auf.

