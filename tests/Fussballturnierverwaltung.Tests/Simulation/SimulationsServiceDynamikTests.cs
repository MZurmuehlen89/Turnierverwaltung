using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Dynamische Teamstärke im <see cref="SimulationsService"/> (#359, K5, K6, K8): Der Stand folgt allein aus den
/// Ergebnissen, die in der Turnierreihenfolge vor dem Spiel liegen – wer ein Ergebnis löscht, rechnet danach ohne es.</summary>
public class SimulationsServiceDynamikTests
{
    private const int FESTER_SEED = 123;
    private const int STAERKE_SEED = 555;
    private const double K = 20;   // Liga

    /// <summary>Liga mit vier Teams, Spiel i = Team i gegen Team i+1 an Spieltag i (Id = Spielnummer = i).</summary>
    private static TurnierverwaltungDbContext Turnier(bool dynamisch)
    {
        var ctx = SimulationsDienste.NeuerContext();
        SimulationsServiceTests.GrunddatenAnlegen(ctx, mannschaften: 4);
        var instanz = ctx.TurnierInstanzen.Local.Single();
        instanz.Kategorie = TurnierKategorie.Liga;
        instanz.StaerkeSeed = STAERKE_SEED;
        instanz.DynamischeTeamstaerkeAktiv = dynamisch;
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, RegelwerkId = 1
        });
        for (int i = 1; i <= 3; i++)
            ctx.Spiele.Add(new Spiel
            {
                Id = i, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = i, Spieltag = i,
                HeimMannschaftId = i, AuswaertsMannschaftId = i + 1, Status = SpielStatus.Geplant
            });
        ctx.SaveChanges();
        return ctx;
    }

    private static SimulationsService Service(TurnierverwaltungDbContext ctx) =>
        SimulationsDienste.Service(ctx, new Zufallsquelle(FESTER_SEED));

    private static void ErgebnisAnlegen(TurnierverwaltungDbContext ctx, int spielId, int heim, int gast)
    {
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { SpielId = spielId, HeimToreRegulaer = heim, AuswaertsToreRegulaer = gast, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit });
        ctx.Spiele.Single(s => s.Id == spielId).Status = SpielStatus.Abgeschlossen;
        ctx.SaveChanges();
    }

    private static void ErgebnisLoeschen(TurnierverwaltungDbContext ctx, int spielId)
    {
        ctx.SpielErgebnisse.Remove(ctx.SpielErgebnisse.Single(e => e.SpielId == spielId));
        ctx.Spiele.Single(s => s.Id == spielId).Status = SpielStatus.Geplant;
        ctx.SaveChanges();
    }

    private static string Abdruck(int heim, int gast, IEnumerable<int> minuten) =>
        $"{heim}:{gast}/{string.Join(',', minuten.Order())}";

    private static string Gespeichert(TurnierverwaltungDbContext ctx, int spielId)
    {
        var spiel = ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse).Single(s => s.Id == spielId);
        return Abdruck(spiel.Ergebnis!.HeimToreRegulaer, spiel.Ergebnis.AuswaertsToreRegulaer, spiel.Ereignisse.Select(e => e.Minute));
    }

    private static double Start(TurnierverwaltungDbContext ctx, int mannschaftId) =>
        DynamischeTeamstaerke.Startwert(ctx.Mannschaften.AsNoTracking().Single(m => m.Id == mannschaftId));

    private static Austragung Termin(TurnierverwaltungDbContext ctx, int spielId) =>
        new(new Wertungsschluessel(1, spielId, spielId), spielId, spielId + 1, Start(ctx, spielId), Start(ctx, spielId + 1),
            Spielortvorteil.Heim, false);

    /// <summary>Was die Engine für das Spiel liefert, wenn die Wertungen davor <paramref name="davor"/> sind.</summary>
    private static (SimulationsErgebnis Ergebnis, string Abdruck) Erwartet(
        TurnierverwaltungDbContext ctx, int spielId, Random rng, IEnumerable<WertungsSpiel> davor, bool dynamisch)
    {
        var termin = Termin(ctx, spielId);
        var form = Turnierstaerke.Form(STAERKE_SEED, termin.HeimId, termin.GastId);
        if (dynamisch)
        {
            var abw = DynamischeTeamstaerke.Abweichungen(davor, K, termin.Schluessel);
            form = DynamischeTeamstaerke.Anwenden(form, abw.GetValueOrDefault(termin.HeimId), abw.GetValueOrDefault(termin.GastId));
        }
        var heim = ctx.Mannschaften.AsNoTracking().Single(m => m.Id == termin.HeimId);
        var gast = ctx.Mannschaften.AsNoTracking().Single(m => m.Id == termin.GastId);
        var e = SimulationsEngine.Simulieren(heim, gast, [], ausfuehrlich: true, rng, turnierform: form);
        return (e, Abdruck(e.HeimToreRegulaer, e.AuswaertsToreRegulaer, e.Ereignisse.Select(x => x.Minute)));
    }

    private static WertungsSpiel Wertung(TurnierverwaltungDbContext ctx, int spielId, int heimTore, int gastTore) =>
        DynamischeTeamstaerke.Wertungen(Termin(ctx, spielId), heimTore, gastTore, null).Single();

    // ── K6 Zustand davor ──────────────────────────────────────────────────

    [Fact]
    public async Task ErstesSpiel_HatNochKeineAbweichung()
    {
        await using var ctx = Turnier(dynamisch: true);

        await Service(ctx).SpielSimulierenAsync(1, new Random(1));

        Gespeichert(ctx, 1).Should().Be(Erwartet(ctx, 1, new Random(1), [], dynamisch: false).Abdruck);
    }

    // ── K6 Wirkung / K4 Reihenfolge ───────────────────────────────────────

    [Fact]
    public async Task FolgespielRechnetMitDemStandNachDemErstenErgebnis()
    {
        // Die Wirkung ist klein (wenige Elo-Punkte, Torerwartung um unter 1 %): ein einzelner Seed bleibt oft bitgleich. Jeder
        // Seed muss die Rechnung mit Stand treffen, und über viele Seeds muss sich die Abweichung zeigen (Verdrahtung).
        int abweichend = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            await using var ctx = Turnier(dynamisch: true);
            ErgebnisAnlegen(ctx, 1, heim: 9, gast: 0);   // Team 2 verlor hoch → seine Torerwartung sinkt in Spiel 2

            await Service(ctx).SpielSimulierenAsync(2, new Random(seed));

            var davor = new[] { Wertung(ctx, 1, 9, 0) };
            Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(seed), davor, dynamisch: true).Abdruck);
            if (Gespeichert(ctx, 2) != Erwartet(ctx, 2, new Random(seed), davor, dynamisch: false).Abdruck)
                abweichend++;
        }

        abweichend.Should().BeGreaterThan(0, "die Abweichung muss das Ergebnis tatsächlich verändern");
    }

    [Fact]
    public async Task SpaetereErgebnisse_WirkenNichtAufEinFruehesSpielZurueck()
    {
        await using var ctx = Turnier(dynamisch: true);
        ErgebnisAnlegen(ctx, 3, heim: 5, gast: 0);   // später in der Reihenfolge, zuerst erfasst

        await Service(ctx).SpielSimulierenAsync(2, new Random(2));

        Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(2), [], dynamisch: false).Abdruck);
    }

    // ── K5 Zurücksetzen und Korrektur ─────────────────────────────────────

    [Fact]
    public async Task ErgebnisLoeschen_NaechstesSpielRechnetWieOhneDieses()
    {
        await using var ctx = Turnier(dynamisch: true);
        ErgebnisAnlegen(ctx, 1, heim: 4, gast: 0);
        ErgebnisLoeschen(ctx, 1);

        await Service(ctx).SpielSimulierenAsync(2, new Random(2));

        Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(2), [], dynamisch: false).Abdruck);
    }

    [Fact]
    public async Task ErgebnisUeberschreiben_NaechstesSpielRechnetMitDemNeuenStand()
    {
        await using var ctx = Turnier(dynamisch: true);
        ErgebnisAnlegen(ctx, 1, heim: 4, gast: 0);
        ErgebnisLoeschen(ctx, 1);
        ErgebnisAnlegen(ctx, 1, heim: 0, gast: 3);

        await Service(ctx).SpielSimulierenAsync(2, new Random(2));

        Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(2), [Wertung(ctx, 1, 0, 3)], dynamisch: true).Abdruck);
    }

    [Fact]
    public async Task ErneutSimulieren_RechnetMitDemStandDavor_NichtMitDemEigenenAltenErgebnis()
    {
        await using var ctx = Turnier(dynamisch: true);
        ErgebnisAnlegen(ctx, 1, heim: 4, gast: 0);
        ErgebnisAnlegen(ctx, 2, heim: 0, gast: 0);

        await Service(ctx).SpielSimulierenAsync(2, new Random(2), ueberschreiben: true);

        Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(2), [Wertung(ctx, 1, 4, 0)], dynamisch: true).Abdruck);
    }

    // ── Block: was der Vorgang selbst simuliert, zählt für die folgenden Spiele ──

    [Fact]
    public async Task Block_RechnetJedesSpielMitDenErgebnissenDesVorgangsDavor()
    {
        await using var ctx = Turnier(dynamisch: true);

        await Service(ctx).SpieleSimulierenAsync([1, 2, 3], seed: 9);

        // Der Stärke-Seed bleibt bei 555 nur, solange es Ergebnisse gibt – hier gab es keine, der Vorgang zieht den festen.
        int seed = ctx.TurnierInstanzen.AsNoTracking().Single().StaerkeSeed!.Value;
        seed.Should().Be(FESTER_SEED);

        var rng = new Random(9);
        var wertungen = new List<WertungsSpiel>();
        foreach (int id in new[] { 1, 2, 3 })
        {
            var termin = Termin(ctx, id);
            var abw = DynamischeTeamstaerke.Abweichungen(wertungen, K, termin.Schluessel);
            var form = DynamischeTeamstaerke.Anwenden(Turnierstaerke.Form(seed, id, id + 1),
                abw.GetValueOrDefault(id), abw.GetValueOrDefault(id + 1));
            var e = SimulationsEngine.Simulieren(
                ctx.Mannschaften.AsNoTracking().Single(m => m.Id == id), ctx.Mannschaften.AsNoTracking().Single(m => m.Id == id + 1),
                [], ausfuehrlich: true, rng, turnierform: form);
            wertungen.AddRange(DynamischeTeamstaerke.Wertungen(termin, e.HeimToreRegulaer, e.AuswaertsToreRegulaer, null));

            Gespeichert(ctx, id).Should().Be(Abdruck(e.HeimToreRegulaer, e.AuswaertsToreRegulaer, e.Ereignisse.Select(x => x.Minute)));
        }
    }

    // ── K8 Schalter ───────────────────────────────────────────────────────

    [Fact]
    public async Task SchalterAus_IgnoriertDieErgebnisseDavor()
    {
        await using var ctx = Turnier(dynamisch: false);
        ErgebnisAnlegen(ctx, 1, heim: 4, gast: 0);

        await Service(ctx).SpielSimulierenAsync(2, new Random(2));

        Gespeichert(ctx, 2).Should().Be(Erwartet(ctx, 2, new Random(2), [Wertung(ctx, 1, 4, 0)], dynamisch: false).Abdruck);
    }

    [Fact]
    public async Task UmschaltenMitteImTurnier_AendertKeinGespeichertesErgebnis_NaechstesSpielRechnetNachDemNeuenStand()
    {
        await using var ctx = Turnier(dynamisch: false);
        ErgebnisAnlegen(ctx, 1, heim: 4, gast: 0);
        await Service(ctx).SpielSimulierenAsync(2, new Random(2));
        string spiel1 = Gespeichert(ctx, 1), spiel2 = Gespeichert(ctx, 2);

        ctx.TurnierInstanzen.Single().DynamischeTeamstaerkeAktiv = true;
        ctx.SaveChanges();
        await Service(ctx).SpielSimulierenAsync(3, new Random(3));

        Gespeichert(ctx, 1).Should().Be(spiel1);
        Gespeichert(ctx, 2).Should().Be(spiel2);
        var davor = new[] { Wertung(ctx, 1, 4, 0), SpielWertung(ctx, 2) };
        Gespeichert(ctx, 3).Should().Be(Erwartet(ctx, 3, new Random(3), davor, dynamisch: true).Abdruck);
    }

    private static WertungsSpiel SpielWertung(TurnierverwaltungDbContext ctx, int spielId)
    {
        var e = ctx.SpielErgebnisse.AsNoTracking().Single(x => x.SpielId == spielId);
        return Wertung(ctx, spielId, e.HeimToreGesamt, e.AuswaertsToreGesamt);
    }

    // ── K9 Stammdaten unberührt ───────────────────────────────────────────

    [Fact]
    public async Task Simulation_AendertDieRatingsDerMannschaftenNicht()
    {
        await using var ctx = Turnier(dynamisch: true);
        string Ratings() => string.Join(';', ctx.Mannschaften.AsNoTracking().OrderBy(m => m.Id)
            .Select(m => $"{m.OffensivRating}/{m.MittelfeldRating}/{m.DefensivRating}/{m.Heimstaerke}/{m.Auswaertsstaerke}"));
        var vorher = Ratings();

        await Service(ctx).SpieleSimulierenAsync([1, 2, 3], seed: 9);

        Ratings().Should().Be(vorher);
    }
}
