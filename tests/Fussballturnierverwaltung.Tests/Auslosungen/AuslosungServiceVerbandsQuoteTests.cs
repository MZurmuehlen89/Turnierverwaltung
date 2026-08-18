using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Regressionstests dafür, dass Verbandsquoten (Domänen-Annahme: gelten nur für die initiale
/// Turnierrunde, siehe project_verbandsquote_scope) in AuslosungService.AusfuehrenAsync nur für die erste
/// Runde einer TurnierInstanz geprüft werden, nicht für jede Runde mit eigener Auslosung.</summary>
public class AuslosungServiceVerbandsQuoteTests
{
    private static Mannschaft TeamAusVerband(int id, int verbandId) =>
        new() { Id = id, Name = $"Team {id}", LandId = id, Land = new Land { Id = id, VerbandId = verbandId } };

    private static (AuslosungService Service, FakeAuslosungRepository AuslosungRepo, Auslosung Auslosung)
        AufbauenMitVerletzterQuote(int turnierRundeId, params TurnierRunde[] alleRundenDerInstanz)
    {
        const int turnierInstanzId = 5;
        const int verbandId = 1;

        // Zwei Teams aus demselben Verband, Quote erlaubt aber nur 1 — Verletzung, sofern geprüft.
        var teamA = TeamAusVerband(1, verbandId);
        var teamB = TeamAusVerband(2, verbandId);
        var lostopf = new Lostopf
        {
            Id = 1, Reihenfolge = 1, Name = "Topf 1",
            Eintraege =
            [
                new LostopfEintrag { Id = 1, MannschaftId = teamA.Id, Mannschaft = teamA },
                new LostopfEintrag { Id = 2, MannschaftId = teamB.Id, Mannschaft = teamB },
            ]
        };
        var auslosung = new Auslosung
        {
            Id = 1, TurnierInstanzId = turnierInstanzId, TurnierRundeId = turnierRundeId,
            TurnierRunde = alleRundenDerInstanz.First(r => r.Id == turnierRundeId),
            Auslosungsart = Auslosungsart.Zufall, Status = AuslosungsStatus.Vorbereitung,
            Lostoepfe = [lostopf]
        };

        var auslosungRepo = new FakeAuslosungRepository();
        auslosungRepo.Speichern(auslosung);

        var verbandsQuoteRepo = new FakeVerbandsQuoteRepository();
        verbandsQuoteRepo.Speichern(new VerbandsQuote { TurnierInstanzId = turnierInstanzId, VerbandId = verbandId, Anzahl = 1 });

        var rundeRepo = new FakeTurnierRundeRepository();
        rundeRepo.Speichern(alleRundenDerInstanz);

        var service = new AuslosungService(
            auslosungRepo, verbandsQuoteRepo, rundeRepo, new AuslosungsValidator(),
            NullLogger<AuslosungService>.Instance);

        return (service, auslosungRepo, auslosung);
    }

    [Fact]
    public async Task AusfuehrenAsync_PrueftVerbandsQuote_FuerErsteRundeDerInstanz()
    {
        var runde = new TurnierRunde { Id = 10, TurnierInstanzId = 5, Reihenfolge = 1, Rundenart = Rundenart.Liga, RegelwerkId = 1 };
        var (service, _, auslosung) = AufbauenMitVerletzterQuote(runde.Id, runde);

        var handeln = () => service.AusfuehrenAsync(auslosung.Id);

        await handeln.Should().ThrowAsync<ValidationException>(
            "die Auslosung betrifft die erste Runde der Instanz — die Verbandsquote muss hier greifen");
    }

    [Fact]
    public async Task AusfuehrenAsync_PrueftVerbandsQuoteNicht_FuerFolgerundeMitEigenerAuslosung()
    {
        var ersteRunde = new TurnierRunde { Id = 9, TurnierInstanzId = 5, Reihenfolge = 1, Rundenart = Rundenart.Gruppenphase, RegelwerkId = 1 };
        var folgeRunde = new TurnierRunde { Id = 10, TurnierInstanzId = 5, Reihenfolge = 2, Rundenart = Rundenart.Liga, RegelwerkId = 1 };
        var (service, _, auslosung) = AufbauenMitVerletzterQuote(folgeRunde.Id, ersteRunde, folgeRunde);

        var ergebnisse = await service.AusfuehrenAsync(auslosung.Id);

        ergebnisse.Should().NotBeEmpty(
            "die Auslosung betrifft eine Folgerunde — die Verbandsquote der initialen Runde darf hier nicht greifen");
    }
}
