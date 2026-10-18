# CLAUDE.md — Tests

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Testing

Tests use xUnit + FluentAssertions. Integration tests use `Microsoft.EntityFrameworkCore.InMemory`. Test files mirror the source namespace structure under `tests/Fussballturnierverwaltung.Tests/`. No mocking framework is used — generators and validators are instantiated directly.

Coverage areas: `KoRundeGeneratorTests`, `RoundRobinGeneratorTests`, `TopfLigaGeneratorTests`, `SchweizerSystemGeneratorTests`, `SimulationsEngineTests`, `TabellenServiceTests`, `TabellenRechnerTests`, `RangPrognoseTests`, `SpielErgebnisServiceTests`, `TurnierValidatorTests`, `RegelwerkValidatorTests`, `MannschaftValidatorTests`, `AuslosungsValidatorTests`, `AuslosungAlgorithmusTests`, `LosverfahrenTests`, `AuslosungServiceLostopfTests`, `QuotenBelegungTests`, `PaarungsRegelnTests`, `VorbelegungsRasterTests`, `AusgangsreihenfolgeTests`, `ManuellePaarungEingabeTests`, `ZiehungsReihenfolgeTests`, `GegnerMatrixPlanTests`, `AuslosungsfilterTests`, `MannschaftsIconTests`, `UebergangsAbdeckungTests`, `TurnierRundeEingabeTests`, `TabellenZonenTests`, `GesamtbaumLayoutTests`, `RundenbaumLayoutTests`, `RundenBezeichnungTests`, `KnotenErgebnisTests`, `SeedVorlagenAbgleichTests`, `TurnierLogoNachruestungTests`, `GruppenSpielplanTests`, `KoPaarungenTests`, `KoSpielLabelTests`, `SpieltagFokusTests`, `ErgebnisEntscheidungTests`, `PlatzhalterNamenTests`, `TabellenZwischenspeicherTests`, `SpielplanServiceTests`, `PlatzhalterAufloesungTests`, `QualifikantenAufloesungTests`, `PlatzhalterKaskadeServiceTests`, `FinaleAutomatikTests`, `KoQuellverknuepfungTests`.

Die Tests der Spielplan-Dienste bauen ihre Dienste über `Spielplan/SpielplanDienste` (ein Objektgraph wie in der App) und ihre Turniere über `Spielplan/SpielplanTestdaten` auf.

Test commands (build/run single class/method) are documented in the root `CLAUDE.md` under "Build & Run".
