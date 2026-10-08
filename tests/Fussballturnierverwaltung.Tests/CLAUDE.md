# CLAUDE.md — Tests

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Testing

Tests use xUnit + FluentAssertions. Integration tests use `Microsoft.EntityFrameworkCore.InMemory`. Test files mirror the source namespace structure under `tests/Fussballturnierverwaltung.Tests/`. No mocking framework is used — generators and validators are instantiated directly.

Coverage areas: `KoRundeGeneratorTests`, `RoundRobinGeneratorTests`, `TopfLigaGeneratorTests`, `SchweizerSystemGeneratorTests`, `SimulationsEngineTests`, `TabellenServiceTests`, `SpielErgebnisServiceTests`, `TurnierValidatorTests`, `RegelwerkValidatorTests`, `MannschaftValidatorTests`, `AuslosungsValidatorTests`, `AuslosungAlgorithmusTests`, `LosverfahrenTests`, `AuslosungServiceLostopfTests`.

Test commands (build/run single class/method) are documented in the root `CLAUDE.md` under "Build & Run".
