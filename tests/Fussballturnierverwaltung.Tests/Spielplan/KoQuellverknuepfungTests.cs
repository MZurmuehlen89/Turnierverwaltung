using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static Fussballturnierverwaltung.Tests.Spielplan.SpielplanTestdaten;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class KoQuellverknuepfungTests
{
    // ── Tests: K.o.-Quellverknüpfung (Issue #241) ──────────────────────────

    /// <summary>Regressionstest Issue #368: ohne Quellverknüpfung fiel der Turnierbaum auf die sequentielle
    /// Alt-Zuordnung zurück und verband z.B. die CL-Playoff-Runde falsch mit dem Achtelfinale.</summary>
    [Theory]
    [InlineData("Playoff-Runde")]
    [InlineData("1. Runde")]
    public async Task AlleRundenVerknuepfen_SetztQuellIdsBeiRundennamenMitLeerzeichenOderBindestrich(string rundenName)
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfName: rundenName);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = $"Sieger {rundenName} 2", AuswaertsPlatzhalterName = $"Verlierer {rundenName} 1",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Quellverknuepfung.AlleRundenVerknuepfenAsync(1);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimQuellSpielId.Should().Be(2);
        finale.HeimQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Sieger);
        finale.AuswaertsQuellSpielId.Should().Be(1);
        finale.AuswaertsQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Verlierer);
    }

    [Fact]
    public async Task AlleRundenVerknuepfen_SetztQuellIdsFuerSiegerUndVerlierer()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);
        HalbfinalSpielAnlegen(ctx, 1, hfRundeId, heimId: 1, auswaertsId: 2, siegerMannschaftId: null);
        HalbfinalSpielAnlegen(ctx, 2, hfRundeId, heimId: 3, auswaertsId: 4, siegerMannschaftId: null);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Verlierer HF 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        // Verknüpfung ist rein strukturell (Text-basiert) und braucht kein Ergebnis der HF-Spiele.
        await dienste.Quellverknuepfung.AlleRundenVerknuepfenAsync(1);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimQuellSpielId.Should().Be(1);
        finale.HeimQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Sieger);
        finale.AuswaertsQuellSpielId.Should().Be(2);
        finale.AuswaertsQuellErgebnis.Should().Be(SpielQuellErgebnisTyp.Verlierer);
    }

    // ── Tests: K.o.-Runden mit Hin-/Rückspiel und Platzhaltern (Issue #346) ─

    [Fact]
    public async Task AlleRundenVerknuepfen_HinUndRueckspiel_VerweistAufRueckspielDerPaarung()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId, hfMitRueckspiel: true);
        HalbfinaleMitRueckspielenAnlegen(ctx, hfRundeId);
        ctx.Spiele.Add(new Spiel
        {
            Id = 20, TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Spielnummer = 20,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Verlierer Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        var dienste = new SpielplanDienste(ctx);

        await dienste.Quellverknuepfung.AlleRundenVerknuepfenAsync(1);
        await dienste.Aufloesung.RundeAufloesenAsync(finaleRundeId);

        var finale = ctx.Spiele.First(s => s.Id == 20);
        finale.HeimQuellSpielId.Should().Be(2, "das Rückspiel der 1. Paarung trägt den Gesamtsieger");
        finale.AuswaertsQuellSpielId.Should().Be(4);
        finale.HeimMannschaftId.Should().Be(1);
        finale.AuswaertsMannschaftId.Should().Be(3, "Verlierer der 2. Paarung = Team C");
    }
}
