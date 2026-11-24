using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Tests.Simulation;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Spielplan;

/// <summary>#624: K.-o.-Runde, deren wirksames Regelwerk kein Entscheidungsverfahren hat – Konfigurationsprüfung (K3–K5)
/// und Sicherung beim Speichern eines Ergebnisses (K6).
/// Halbfinale (Einzelspiele 1 und 2, Regelwerk 2 ohne Verfahren) → Finale mit Platzhaltern „Sieger Halbfinale 1/2“.</summary>
public class KoOhneEntscheidungsverfahrenTests
{
    private static TurnierverwaltungDbContext Turnier(int? rundenRegelwerk, int? standardRegelwerk)
    {
        var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: 4);
        ctx.Regelwerke.Add(new Regelwerk { Id = 2, Name = "Ohne Verfahren" });
        ctx.TurnierInstanzen.Local.Single().StandardRegelwerkId = standardRegelwerk;
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1, Rundenart = Rundenart.KoRunde,
            RegelwerkId = rundenRegelwerk, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2
        });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2, Rundenart = Rundenart.Finale, RegelwerkId = 1,
            EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static void HalbfinaleUndFinaleAnlegen(TurnierverwaltungDbContext ctx)
    {
        ctx.Spiele.Add(new Spiel { Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 1, Spieltag = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Geplant });
        ctx.Spiele.Add(new Spiel { Id = 2, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 2, Spieltag = 1,
            HeimMannschaftId = 3, AuswaertsMannschaftId = 4, Status = SpielStatus.Geplant });
        ctx.Spiele.Add(new Spiel { Id = 100, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 3, Spieltag = 1,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Sieger Halbfinale 2",
            Status = SpielStatus.Geplant });
        ctx.SaveChanges();
    }

    private static Spiel Laden(TurnierverwaltungDbContext ctx, int id) =>
        ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Single(s => s.Id == id);

    [Fact]
    public async Task Simulation_Remis_OhneVerfahren_WirdAbgelehnt_FinaleBleibtOffen()
    {
        for (int seed = 0; seed < 60; seed++)
        {
            using var ctx = Turnier(rundenRegelwerk: 2, standardRegelwerk: null);
            HalbfinaleUndFinaleAnlegen(ctx);
            try
            {
                await SimulationsDienste.Service(ctx, new Zufallsquelle()).SpielSimulierenAsync(1, new Random(seed));
            }
            catch (ValidationException)
            {
                Laden(ctx, 1).Ergebnis.Should().BeNull();
                Laden(ctx, 100).HeimMannschaftId.Should().BeNull();
                return;
            }
        }
        Assert.Fail("kein Remis in 60 Seeds");
    }

    [Fact]
    public async Task Simulation_StandardregelwerkMitVerfahren_RundeOhne_EntscheidetPerStandard()
    {
        // Runde ohne eigenes Regelwerk → Standard des Turniers (Regelwerk 1, Elfmeterschießen).
        for (int seed = 0; seed < 60; seed++)
        {
            using var ctx = Turnier(rundenRegelwerk: null, standardRegelwerk: 1);
            HalbfinaleUndFinaleAnlegen(ctx);
            var e = await SimulationsDienste.Service(ctx, new Zufallsquelle()).SpielSimulierenAsync(1, new Random(seed));
            if (e.HeimToreRegulaer != e.AuswaertsToreRegulaer) continue;

            Laden(ctx, 1).Ergebnis!.EntscheidungArt.Should().Be(EntscheidungArt.Elfmeterschiessen);
            return;
        }
        Assert.Fail("kein Remis in 60 Seeds");
    }

    [Fact]
    public async Task ManuelleEingabe_Remis_OhneVerfahren_WirdAbgelehnt()
    {
        using var ctx = Turnier(rundenRegelwerk: 2, standardRegelwerk: null);
        HalbfinaleUndFinaleAnlegen(ctx);
        var dienste = new SpielplanDienste(ctx);
        var service = new SpielErgebnisService(
            new Infrastructure.Repositories.SpielRepository(ctx), new SpielErgebnisValidator(), dienste.Aufloesung,
            dienste.Kaskade, Microsoft.Extensions.Logging.Abstractions.NullLogger<SpielErgebnisService>.Instance);

        var akt = () => service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(1, 1, EntscheidungArt.RegulaereSpielzeit));

        await akt.Should().ThrowAsync<ValidationException>();
        Laden(ctx, 1).Ergebnis.Should().BeNull();
    }

    [Fact]
    public void Formular_Remis_OhneVerfahren_ArtRegulaer_KeinFehler()
    {
        var rahmen = new ErgebnisRahmen(IstKoRunde: true, IstKoHinspiel: false, null, false, []);
        var tore = new TorEingabe(1, 1, null, null);

        ErgebnisEntscheidung.VerfahrenNoetig(rahmen, tore).Should().BeTrue();
        ErgebnisEntscheidung.Art(rahmen, tore).Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    // ── Konfigurationsprüfung (K3–K5) ───────────────────────────────────────

    private static KoRegelwerkPruefung Pruefung(TurnierverwaltungDbContext ctx) =>
        new(new Infrastructure.Repositories.RegelwerkRepository(ctx), new Infrastructure.Repositories.TurnierRundeRepository(ctx));

    private static TurnierRundeEingabe Eingabe(Rundenart art, int? regelwerkId) =>
        new("Halbfinale", art, 4, 2, regelwerkId);

    [Theory]
    [InlineData(Rundenart.KoRunde, 2, null)]    // eigenes Regelwerk ohne Verfahren
    [InlineData(Rundenart.Finale, null, 2)]     // Standard ohne Verfahren
    [InlineData(Rundenart.SpielUmPlatzDrei, null, null)] // gar keins
    public async Task Runden_KoOhneWirksamesVerfahren_Wirft(Rundenart art, int? regelwerkId, int? standardId)
    {
        using var ctx = Turnier(rundenRegelwerk: null, standardRegelwerk: null);
        var akt = () => Pruefung(ctx).RundenPruefenAsync([Eingabe(art, regelwerkId)], standardId);
        await akt.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(Rundenart.KoRunde, 1, null)]    // eigenes Regelwerk mit Verfahren
    [InlineData(Rundenart.KoRunde, null, 1)]    // Standard mit Verfahren
    [InlineData(Rundenart.Liga, 2, null)]       // Liga braucht kein Verfahren
    [InlineData(Rundenart.Gruppenphase, null, null)]
    public async Task Runden_Geeignet_WirftNicht(Rundenart art, int? regelwerkId, int? standardId)
    {
        using var ctx = Turnier(rundenRegelwerk: null, standardRegelwerk: null);
        await Pruefung(ctx).RundenPruefenAsync([Eingabe(art, regelwerkId)], standardId);
    }

    [Fact]
    public async Task Runde_RegelwerkOhneVerfahrenZuweisen_Wirft_MitVerfahren_Nicht()
    {
        using var ctx = Turnier(rundenRegelwerk: 1, standardRegelwerk: null);
        var runde = ctx.TurnierRunden.Single(r => r.Id == 1);

        var akt = () => Pruefung(ctx).RundePruefenAsync(runde, 2);

        await akt.Should().ThrowAsync<ValidationException>();
        await Pruefung(ctx).RundePruefenAsync(runde, 1);
    }

    [Fact]
    public async Task Standard_OhneVerfahren_WirftNurMitKoRundeOhneEigenesRegelwerk()
    {
        using (var ctx = Turnier(rundenRegelwerk: null, standardRegelwerk: 1))
            await new Func<Task>(() => Pruefung(ctx).StandardPruefenAsync(1, 2)).Should().ThrowAsync<ValidationException>();

        using var mitEigenem = Turnier(rundenRegelwerk: 1, standardRegelwerk: 1);
        await Pruefung(mitEigenem).StandardPruefenAsync(1, 2); // beide K.-o.-Runden haben ein eigenes Regelwerk
    }

    [Fact]
    public async Task Regelwerk_LetztesVerfahrenEntfernen_WirftMitTurnierUndRunde_UngenutztNicht()
    {
        using var ctx = Turnier(rundenRegelwerk: 1, standardRegelwerk: null);

        var akt = () => Pruefung(ctx).RegelwerkAenderungPruefenAsync(1, verfahrenAnzahl: 0);

        var ex = (await akt.Should().ThrowAsync<ValidationException>()).Which;
        ex.Ergebnis.FehlerZusammenfassung.Should().Contain("Halbfinale").And.Contain(ctx.TurnierInstanzen.Local.Single().Name);
        await Pruefung(ctx).RegelwerkAenderungPruefenAsync(2, verfahrenAnzahl: 0);   // von keiner K.-o.-Runde genutzt
        await Pruefung(ctx).RegelwerkAenderungPruefenAsync(1, verfahrenAnzahl: 1);   // behält ein Verfahren
    }

    [Fact]
    public async Task Generierung_PrueftKeinRegelwerkMehr()
    {
        // K7: die Sonderprüfung im SpielplanService entfiel, die Konfiguration verhindert den Fall vorher.
        using var ctx = Turnier(rundenRegelwerk: 2, standardRegelwerk: null);
        var ergebnis = await new SpielplanDienste(ctx).Spielplan.GenerierenAsync(1, [1, 2, 3, 4]);
        ergebnis.Spiele.Should().HaveCount(2);
    }
}
