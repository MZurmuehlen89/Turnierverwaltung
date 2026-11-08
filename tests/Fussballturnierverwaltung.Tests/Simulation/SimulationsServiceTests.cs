using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Die Massensimulationen des <see cref="SimulationsService"/> über die Datenbank (#629): welche Spiele sie
/// treffen, in welcher Reihenfolge und was sie speichern.</summary>
public class SimulationsServiceTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private static void GrunddatenAnlegen(TurnierverwaltungDbContext ctx, int mannschaften)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "Turnier" });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });
        for (int id = 1; id <= mannschaften; id++)
            ctx.Mannschaften.Add(new Mannschaft
            {
                Id = id, Name = $"Team {id}", Kurzname = $"T{id}", LandId = 1,
                OffensivRating = 70, MittelfeldRating = 70, DefensivRating = 70,
                Heimstaerke = 100, Auswaertsstaerke = 100
            });
    }

    /// <summary>K.-o.-Runde 1 mit Hin- und Rückspiel: Paarung i = Team 2i+1 gegen 2i+2, Hinspiel-Id 2i+1,
    /// Rückspiel-Id 2i+2.</summary>
    private static void KoRundeAnlegen(
        TurnierverwaltungDbContext ctx, int paarungen, int spieltagHin = 1, int spieltagRueck = 2,
        bool auswaertstorRegel = false)
    {
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1, Rundenart = Rundenart.KoRunde,
            RegelwerkId = 1, MitRueckspiel = true, AuswaertstorRegelAktiv = auswaertstorRegel
        });
        for (int i = 0; i < paarungen; i++)
        {
            int teamA = 2 * i + 1, teamB = 2 * i + 2;
            ctx.Spiele.Add(new Spiel
            {
                Id = teamA, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = teamA, Spieltag = spieltagHin,
                HeimMannschaftId = teamA, AuswaertsMannschaftId = teamB, Status = SpielStatus.Geplant
            });
            ctx.Spiele.Add(new Spiel
            {
                Id = teamB, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = teamB, Spieltag = spieltagRueck,
                HeimMannschaftId = teamB, AuswaertsMannschaftId = teamA, Status = SpielStatus.Geplant,
                IstRueckspiel = true, HinspielId = teamA
            });
        }
    }

    private static List<Spiel> SpieleMitErgebnis(TurnierverwaltungDbContext ctx) =>
        [.. ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse).OrderBy(s => s.Id)];

    // ── Ganzes Turnier ────────────────────────────────────────────────────────

    // Die Rückspiele stehen im Spielplan vor ihren Hinspielen (früherer Spieltag) — bis #629 sortierte nur die
    // Rundensimulation Hinspiele vor, die Turniersimulation brach am ersten Rückspiel ab.
    [Fact]
    public async Task TurnierSimulierenAsync_RueckspieleVorHinspielenImSpielplan_SimuliertAllesUndBesetztDasFinale()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        GrunddatenAnlegen(ctx, mannschaften: 4);
        KoRundeAnlegen(ctx, paarungen: 2, spieltagHin: 2, spieltagRueck: 1);
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2, Rundenart = Rundenart.Finale, RegelwerkId = 1
        });
        ctx.Spiele.Add(new Spiel
        {
            Id = 100, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 5, Spieltag = 1,
            HeimPlatzhalterName = "Sieger Halbfinale 1", AuswaertsPlatzhalterName = "Sieger Halbfinale 2",
            Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();

        var lauf = await SimulationsDienste.Service(ctx, new Zufallsquelle())
            .TurnierSimulierenAsync(1, SimulationsModus.SchnellSimulation, seed: 629);

        lauf.EndZeitpunkt.Should().NotBeNull();
        var spiele = SpieleMitErgebnis(ctx);
        spiele.Should().OnlyContain(s => s.Ergebnis != null);
        var finale = spiele.Single(s => s.Id == 100);
        finale.HeimMannschaftId.Should().Be(spiele.Single(s => s.Id == 2).Ergebnis!.SiegerMannschaftId);
        finale.AuswaertsMannschaftId.Should().Be(spiele.Single(s => s.Id == 4).Ergebnis!.SiegerMannschaftId);
    }

    // Die Tor-Ereignisse gehören zur Mannschaft, die das Tor erzielt hat — Spielverlauf und Statistik lesen sie.
    [Fact]
    public async Task RundeSimulierenAsync_ToreignisseStimmenJeMannschaftMitDenToren()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        GrunddatenAnlegen(ctx, mannschaften: 20);
        KoRundeAnlegen(ctx, paarungen: 10);
        ctx.SaveChanges();

        await SimulationsDienste.Service(ctx, new Zufallsquelle()).RundeSimulierenAsync(1, SimulationsModus.SchnellSimulation, seed: 629);

        var spiele = SpieleMitErgebnis(ctx);
        spiele.Sum(s => s.Ergebnis!.HeimToreRegulaer + s.Ergebnis.AuswaertsToreRegulaer).Should().BeGreaterThan(0);
        spiele.Should().AllSatisfy(s =>
        {
            int Tore(int? mannschaftId) => s.Ereignisse.Count(e => e.EreignisArt == EreignisArt.Tor && e.MannschaftId == mannschaftId);
            Tore(s.HeimMannschaftId).Should().Be(s.Ergebnis!.HeimToreRegulaer + (s.Ergebnis.HeimToreVerlaengerung ?? 0));
            Tore(s.AuswaertsMannschaftId).Should().Be(s.Ergebnis.AuswaertsToreRegulaer + (s.Ergebnis.AuswaertsToreVerlaengerung ?? 0));
        });
    }

    // ── Freie Auswahl ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SpieleSimulierenAsync_RueckspielVorHinspielAusgewaehlt_UndAbgeschlossenesSpiel_SimuliertDieOffenen()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        GrunddatenAnlegen(ctx, mannschaften: 4);
        KoRundeAnlegen(ctx, paarungen: 2);
        ctx.SaveChanges();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());
        await service.SpielSimulierenAsync(3, SimulationsModus.SchnellSimulation, new Random(1));

        int anzahl = await service.SpieleSimulierenAsync([4, 2, 1, 3], SimulationsModus.SchnellSimulation, seed: 629);

        anzahl.Should().Be(3, "das abgeschlossene Hinspiel 3 wird übersprungen");
        SpieleMitErgebnis(ctx).Should().OnlyContain(s => s.Ergebnis != null);
    }

    // ── Ein Spiel überschreiben ───────────────────────────────────────────────

    [Fact]
    public async Task SpielSimulierenAsync_AbgeschlossenesSpiel_WirftOhneUeberschreiben_ErsetztMitUeberschreibenDieEreignisse()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        GrunddatenAnlegen(ctx, mannschaften: 2);
        KoRundeAnlegen(ctx, paarungen: 1);
        ctx.SaveChanges();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());
        await service.SpielSimulierenAsync(1, SimulationsModus.SchnellSimulation, new Random(1));

        var nochmal = () => service.SpielSimulierenAsync(1, SimulationsModus.SchnellSimulation, new Random(2));
        await nochmal.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bereits abgeschlossen*");

        var neu = await service.SpielSimulierenAsync(1, SimulationsModus.SchnellSimulation, new Random(2), ueberschreiben: true);

        var spiel = SpieleMitErgebnis(ctx).Single(s => s.Id == 1);
        spiel.Ergebnis!.HeimToreRegulaer.Should().Be(neu.HeimToreRegulaer);
        spiel.Ereignisse.Should().HaveCount(neu.Ereignisse.Count, "die Ereignisse der ersten Simulation sind ersetzt");
    }

    // ── Auswärtstorregel im Rückspiel ─────────────────────────────────────────

    // Entscheiden die Auswärtstore, gibt es im Rückspiel kein Elfmeterschießen — nur bei auch dort Gleichstand.
    [Fact]
    public async Task RundeSimulierenAsync_Auswaertstorregel_ElfmeterschiessenNurBeiGleichenAuswaertstoren()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        GrunddatenAnlegen(ctx, mannschaften: 120);
        KoRundeAnlegen(ctx, paarungen: 60, auswaertstorRegel: true);
        ctx.SaveChanges();

        await SimulationsDienste.Service(ctx, new Zufallsquelle()).RundeSimulierenAsync(1, SimulationsModus.SchnellSimulation, seed: 629);

        var spiele = SpieleMitErgebnis(ctx);
        var arten = new List<EntscheidungArt>();
        foreach (var rueck in spiele.Where(s => s.IstRueckspiel))
        {
            var h = spiele.Single(s => s.Id == rueck.HinspielId).Ergebnis!;
            var r = rueck.Ergebnis!;
            bool gesamtGleich = h.HeimToreRegulaer + r.AuswaertsToreRegulaer == h.AuswaertsToreRegulaer + r.HeimToreRegulaer;
            bool auswaertsGleich = h.AuswaertsToreRegulaer == r.AuswaertsToreRegulaer;

            r.HeimElfmetertore.HasValue.Should().Be(gesamtGleich && auswaertsGleich);
            if (gesamtGleich && !auswaertsGleich)
                r.EntscheidungArt.Should().Be(EntscheidungArt.Auswaertstorregel);
            arten.Add(r.EntscheidungArt);
        }
        arten.Should().Contain([EntscheidungArt.Auswaertstorregel, EntscheidungArt.Elfmeterschiessen],
            "beide Zweige müssen vorkommen");
    }

    // ── Gruppe und Spieltag ───────────────────────────────────────────────────

    // Gruppe A = Teams 1–4, Gruppe B = Teams 5–8; je Gruppe zwei Spiele an Spieltag 1 und 2.
    private static void GruppenphaseAnlegen(TurnierverwaltungDbContext ctx)
    {
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1, Rundenart = Rundenart.Gruppenphase,
            RegelwerkId = 1
        });
        int id = 1;
        foreach (int erstes in new[] { 1, 5 })
            foreach (var (spieltag, heim, gast) in new[] { (1, 0, 1), (1, 2, 3), (2, 0, 2), (2, 1, 3) })
            {
                ctx.Spiele.Add(new Spiel
                {
                    Id = id, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = id, Spieltag = spieltag,
                    HeimMannschaftId = erstes + heim, AuswaertsMannschaftId = erstes + gast, Status = SpielStatus.Geplant
                });
                id++;
            }
    }

    [Fact]
    public async Task GruppenUndSpieltagsSimulation_TreffenNurIhreSpiele()
    {
        await using var ctx = SimulationsDienste.NeuerContext();
        GrunddatenAnlegen(ctx, mannschaften: 8);
        GruppenphaseAnlegen(ctx);
        ctx.SaveChanges();
        var service = SimulationsDienste.Service(ctx, new Zufallsquelle());
        int[] gruppeA = [1, 2, 3, 4], gruppeB = [5, 6, 7, 8];
        int[] Gespielt() => [.. SpieleMitErgebnis(ctx).Where(s => s.Ergebnis != null).Select(s => s.Id)];

        (await service.SpieltagInGruppeSimulierenAsync(1, 1, gruppeA, SimulationsModus.SchnellSimulation, seed: 1)).Should().Be(2);
        Gespielt().Should().Equal(1, 2);

        (await service.GruppeSimulierenAsync(1, gruppeB, SimulationsModus.SchnellSimulation, seed: 2)).Should().Be(4);
        Gespielt().Should().Equal(1, 2, 5, 6, 7, 8);

        (await service.SpieltageSimulierenAsync(1, 2, SimulationsModus.SchnellSimulation, seed: 3)).Should().Be(2);
        Gespielt().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }
}
