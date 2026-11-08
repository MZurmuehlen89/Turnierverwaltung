using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class GruppenSpielplanTests
{
    private static IReadOnlyList<GruppenSlot> FesteGruppe(params int[] mannschaftIds) =>
        [.. mannschaftIds.Select(id => new GruppenSlot(id, null))];

    /// <summary>Die Spiele, wie die Generierung sie aus dem Plan anlegt — ein Platzhalter-Platz ohne Mannschaft.</summary>
    private static List<Spiel> SpieleAus(IReadOnlyList<GeplantesGruppenSpiel> plan) =>
        [.. plan.Select(g => new Spiel
        {
            HeimMannschaftId         = g.Heim.MannschaftId,
            HeimPlatzhalterName      = g.Heim.MannschaftId is null ? g.Heim.PlatzhalterName : null,
            AuswaertsMannschaftId    = g.Auswaerts.MannschaftId,
            AuswaertsPlatzhalterName = g.Auswaerts.MannschaftId is null ? g.Auswaerts.PlatzhalterName : null,
            IstRueckspiel            = g.IstRueckspiel,
        })];

    // ── AusAuslosung ────────────────────────────────────────────────────────

    [Fact]
    public void AusAuslosung_BildetJePaarungsNummerEineGruppe_InnerhalbNachIdSortiert()
    {
        var ergebnisse = new[]
        {
            new AuslosungsErgebnis { Id = 4, PaarungsNummer = 2, HeimMannschaftId = 40 },
            new AuslosungsErgebnis { Id = 2, PaarungsNummer = 1, HeimPlatzhalterName = "Sieger Playoff 1" },
            new AuslosungsErgebnis { Id = 1, PaarungsNummer = 1, HeimMannschaftId = 10 },
            new AuslosungsErgebnis { Id = 3, PaarungsNummer = 2, HeimMannschaftId = 30 },
        };

        var gruppen = GruppenSpielplan.AusAuslosung(ergebnisse);

        gruppen.Should().HaveCount(2);
        gruppen[0].Should().Equal(new GruppenSlot(10, null), new GruppenSlot(null, "Sieger Playoff 1"));
        gruppen[1].Should().Equal(new GruppenSlot(30, null), new GruppenSlot(40, null));
    }

    // ── Planen ──────────────────────────────────────────────────────────────

    [Fact]
    public void Planen_ViererGruppe_JederSpieltJeSpieltagEinmal_UndGegenJedenEinmal()
    {
        var plan = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3, 4)], mitRueckspiel: false);

        plan.Should().HaveCount(6);
        plan.Select(s => s.Spieltag).Should().Equal(1, 1, 2, 2, 3, 3);
        foreach (var spieltag in plan.GroupBy(s => s.Spieltag))
            spieltag.SelectMany(s => new[] { s.Heim.MannschaftId, s.Auswaerts.MannschaftId })
                .Should().BeEquivalentTo(new int?[] { 1, 2, 3, 4 });
        plan.Select(s => (Math.Min(s.Heim.MannschaftId!.Value, s.Auswaerts.MannschaftId!.Value),
                          Math.Max(s.Heim.MannschaftId.Value, s.Auswaerts.MannschaftId.Value)))
            .Should().OnlyHaveUniqueItems();
        plan.Should().OnlyContain(s => !s.IstRueckspiel && s.HinspielIndex == null && s.GruppenNummer == 1);
    }

    [Fact]
    public void Planen_DreierGruppe_EinTeamSetztJeSpieltagAus()
    {
        var plan = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3)], mitRueckspiel: false);

        plan.Select(s => s.Spieltag).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Planen_GruppeMitWenigerAlsZweiPlaetzen_BekommtKeinSpiel()
    {
        var plan = GruppenSpielplan.Planen([FesteGruppe(1), FesteGruppe(2, 3)], mitRueckspiel: false);

        plan.Should().ContainSingle().Which.GruppenNummer.Should().Be(2);
    }

    [Fact]
    public void Planen_MitRueckspiel_SpiegeltJedesHinspielUndVerweistDarauf()
    {
        var plan = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3, 4)], mitRueckspiel: true);

        plan.Should().HaveCount(12);
        plan.Take(6).Should().OnlyContain(s => !s.IstRueckspiel);

        var rueckspiele = plan.Skip(6).ToList();
        rueckspiele.Select(s => s.Spieltag).Should().Equal(4, 4, 5, 5, 6, 6);
        foreach (var rueckspiel in rueckspiele)
        {
            rueckspiel.IstRueckspiel.Should().BeTrue();
            var hinspiel = plan[rueckspiel.HinspielIndex!.Value];
            hinspiel.IstRueckspiel.Should().BeFalse();
            (rueckspiel.Heim, rueckspiel.Auswaerts).Should().Be((hinspiel.Auswaerts, hinspiel.Heim));
            rueckspiel.Spieltag.Should().Be(hinspiel.Spieltag + 3);
        }
    }

    [Fact]
    public void Planen_MehrereGruppen_OrdnetNachSpieltagDannGruppe()
    {
        var plan = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3, 4), FesteGruppe(5, 6, 7, 8)], mitRueckspiel: false);

        plan.Select(s => (s.Spieltag, s.GruppenNummer)).Should().Equal(
            (1, 1), (1, 1), (1, 2), (1, 2),
            (2, 1), (2, 1), (2, 2), (2, 2),
            (3, 1), (3, 1), (3, 2), (3, 2));
    }

    [Fact]
    public void Planen_PlatzhalterPlatz_WirdWieEineMannschaftEingeplant()
    {
        var gruppe = new[] { new GruppenSlot(1, null), new GruppenSlot(null, "Sieger Playoff 1") };

        var plan = GruppenSpielplan.Planen([gruppe], mitRueckspiel: false);

        plan.Should().ContainSingle();
        new[] { plan[0].Heim, plan[0].Auswaerts }.Should().BeEquivalentTo(gruppe);
    }

    // ── SpieleErzeugen ──────────────────────────────────────────────────────

    private static readonly TurnierRunde _runde = new() { Id = 7, TurnierInstanzId = 3, Name = "Gruppenphase" };

    [Fact]
    public void SpieleErzeugen_NummeriertAbStartNr_UndUebernimmtPlanUndPlaetze()
    {
        var plan = GruppenSpielplan.Planen([[new GruppenSlot(1, null), new GruppenSlot(null, null), new GruppenSlot(null, "Sieger A")]], mitRueckspiel: false);

        var (hinspiele, rueckspiele) = GruppenSpielplan.SpieleErzeugen(_runde, plan, startNr: 21);

        rueckspiele.Should().BeEmpty();
        hinspiele.Select(s => s.Spielnummer).Should().Equal(21, 22, 23);
        hinspiele.Select(s => (s.Spieltag, s.GruppenNummer)).Should().Equal(plan.Select(g => ((int?)g.Spieltag, (int?)g.GruppenNummer)));
        hinspiele.Should().OnlyContain(s => s.TurnierRundeId == 7 && s.TurnierInstanzId == 3 && s.Status == SpielStatus.Geplant);
        hinspiele.SelectMany(s => new[] { s.HeimPlatzhalterName, s.AuswaertsPlatzhalterName }).OfType<string>()
            .Should().BeEquivalentTo("?", "?", "Sieger A", "Sieger A");
        GruppenSpielplan.PasstZu(plan, hinspiele).Should().BeTrue();
    }

    [Fact]
    public void SpieleErzeugen_ModusNurBeiFestenTeamsSchnellSimulation()
    {
        var fest      = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3)], mitRueckspiel: false);
        var gemischt  = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3), [new GruppenSlot(4, null), new GruppenSlot(null, "Sieger A")]], mitRueckspiel: false);

        GruppenSpielplan.SpieleErzeugen(_runde, fest, startNr: 1).Hinspiele
            .Should().OnlyContain(s => s.SimulationsModus == SimulationsModus.SchnellSimulation);
        GruppenSpielplan.SpieleErzeugen(_runde, gemischt, startNr: 1).Hinspiele
            .Should().OnlyContain(s => s.SimulationsModus == SimulationsModus.ManuellesErgebnis);
    }

    [Fact]
    public void SpieleErzeugen_MitRueckspiel_JedesRueckspielMitSeinemGespiegeltenHinspiel()
    {
        var plan = GruppenSpielplan.Planen([FesteGruppe(1, 2, 3, 4), FesteGruppe(5, 6, 7, 8)], mitRueckspiel: true);

        var (hinspiele, rueckspiele) = GruppenSpielplan.SpieleErzeugen(_runde, plan, startNr: 1);

        hinspiele.Should().HaveCount(12).And.OnlyContain(s => !s.IstRueckspiel);
        rueckspiele.Should().HaveCount(12);
        hinspiele.Concat(rueckspiele.Select(r => r.Rueckspiel)).Select(s => s.Spielnummer)
            .Should().BeEquivalentTo(Enumerable.Range(1, 24));
        foreach (var (hin, rueck) in rueckspiele)
        {
            rueck.IstRueckspiel.Should().BeTrue();
            hinspiele.Should().Contain(hin);
            (rueck.HeimMannschaftId, rueck.AuswaertsMannschaftId).Should().Be((hin.AuswaertsMannschaftId, hin.HeimMannschaftId));
            rueck.GruppenNummer.Should().Be(hin.GruppenNummer);
        }
    }

    // ── PasstZu ─────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<GruppenSlot> _gemischteGruppe =
        [new(1, null), new(2, null), new(3, null), new(null, "Sieger Playoff 1")];

    [Fact]
    public void PasstZu_SpielplanAusDemPlan_Passt()
    {
        var plan = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: true);

        GruppenSpielplan.PasstZu(plan, SpieleAus(plan)).Should().BeTrue();
    }

    [Fact]
    public void PasstZu_AndereSpielanzahl_PasstNicht()
    {
        var plan = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);

        GruppenSpielplan.PasstZu(plan, SpieleAus(plan).Take(5).ToList()).Should().BeFalse();
    }

    [Fact]
    public void PasstZu_FestesTeamAnAndererStelle_PasstNicht()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);
        var spiele = SpieleAus(plan);
        var mitFestemHeim = spiele.First(s => s.HeimMannschaftId.HasValue);
        mitFestemHeim.HeimMannschaftId = 99;

        GruppenSpielplan.PasstZu(plan, spiele).Should().BeFalse();
    }

    [Fact]
    public void PasstZu_AufgeloesterPlatzhalter_PasstMitJederMannschaft()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);
        var spiele = SpieleAus(plan);
        foreach (var spiel in spiele.Where(s => s.HeimPlatzhalterName is not null))
            (spiel.HeimMannschaftId, spiel.HeimPlatzhalterName) = (77, null);
        foreach (var spiel in spiele.Where(s => s.AuswaertsPlatzhalterName is not null))
            (spiel.AuswaertsMannschaftId, spiel.AuswaertsPlatzhalterName) = (77, null);

        GruppenSpielplan.PasstZu(plan, spiele).Should().BeTrue();
    }

    [Fact]
    public void PasstZu_OffenerPlatzhalterMitAnderemNamen_PasstNicht()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);
        var spiele = SpieleAus(plan);
        foreach (var spiel in spiele.Where(s => s.HeimPlatzhalterName is not null))
            spiel.HeimPlatzhalterName = "Sieger Playoff 2";
        foreach (var spiel in spiele.Where(s => s.AuswaertsPlatzhalterName is not null))
            spiel.AuswaertsPlatzhalterName = "Sieger Playoff 2";

        GruppenSpielplan.PasstZu(plan, spiele).Should().BeFalse();
    }

    [Fact]
    public void PasstZu_OffenerPlatzhalter_VergleichtOhneGrossKleinschreibung()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);
        var spiele = SpieleAus(plan);
        foreach (var spiel in spiele.Where(s => s.HeimPlatzhalterName is not null))
            spiel.HeimPlatzhalterName = spiel.HeimPlatzhalterName!.ToUpperInvariant();

        GruppenSpielplan.PasstZu(plan, spiele).Should().BeTrue();
    }

    [Fact]
    public void PasstZu_RueckspielAnAndererStelle_PasstNicht()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: true);
        var spiele = SpieleAus(plan);
        spiele[0].IstRueckspiel = true;

        GruppenSpielplan.PasstZu(plan, spiele).Should().BeFalse();
    }

    [Fact]
    public void PasstZu_AufgeloesterPlatzMitWechselnderMannschaft_PasstNicht()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);
        var spiele = SpieleAus(plan);
        int naechsteId = 70;
        foreach (var spiel in spiele.Where(s => s.HeimPlatzhalterName is not null))
            (spiel.HeimMannschaftId, spiel.HeimPlatzhalterName) = (naechsteId++, null);
        foreach (var spiel in spiele.Where(s => s.AuswaertsPlatzhalterName is not null))
            (spiel.AuswaertsMannschaftId, spiel.AuswaertsPlatzhalterName) = (naechsteId++, null);

        GruppenSpielplan.PasstZu(plan, spiele).Should().BeFalse("ein Platz steht für genau eine Mannschaft");
    }

    // ── Gespeicherte Pläne vor #568 ─────────────────────────────────────────

    private static readonly IReadOnlyList<GruppenSlot> _platzhalterGruppe =
        [new(null, "Sieger 1"), new(null, "Sieger 2"), new(null, "Sieger 3"), new(null, "Sieger 4")];

    /// <summary>Spiele einer Vierergruppe, wie sie vor #568 generiert wurden: dort hatte in jeder Partie ohne den
    /// festen Platz 0 das andere Team Heimrecht. Alle Platzhalter sind aufgelöst (Platz i → Mannschaft 10 + i).</summary>
    private static List<Spiel> AufgeloesteSpieleVor568(IReadOnlyList<GeplantesGruppenSpiel> plan)
    {
        int Mannschaft(GruppenSlot platz) => 11 + _platzhalterGruppe.ToList().IndexOf(platz);
        var festerPlatz = _platzhalterGruppe[0];

        return [.. plan.Select(g =>
        {
            var (heim, gast) = g.Heim == festerPlatz || g.Auswaerts == festerPlatz ? (g.Heim, g.Auswaerts) : (g.Auswaerts, g.Heim);
            return new Spiel { HeimMannschaftId = Mannschaft(heim), AuswaertsMannschaftId = Mannschaft(gast), IstRueckspiel = g.IstRueckspiel };
        })];
    }

    [Fact]
    public void GespeichertenPlanFinden_AktuellerPlan_WirdGefunden()
    {
        var plan = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: true);

        GruppenSpielplan.GespeichertenPlanFinden([_gemischteGruppe], mitRueckspiel: true, SpieleAus(plan))
            .Should().BeEquivalentTo(plan, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GespeichertenPlanFinden_PlanVor568MitAufgeloestenPlaetzen_WirdMitSeinemHeimrechtGefunden(bool mitRueckspiel)
    {
        var aktuell = GruppenSpielplan.Planen([_platzhalterGruppe], mitRueckspiel);
        var spiele  = AufgeloesteSpieleVor568(aktuell);

        var gefunden = GruppenSpielplan.GespeichertenPlanFinden([_platzhalterGruppe], mitRueckspiel, spiele);

        GruppenSpielplan.PasstZu(aktuell, spiele).Should().BeFalse("das Heimrecht hat sich mit #568 geändert");
        gefunden.Should().NotBeNull();
        gefunden!.Select(g => 11 + _platzhalterGruppe.ToList().IndexOf(g.Heim))
            .Should().Equal(spiele.Select(s => s.HeimMannschaftId!.Value));
    }

    [Fact]
    public void GespeichertenPlanFinden_FremderPlan_WirdNichtGefunden()
    {
        var plan   = GruppenSpielplan.Planen([_gemischteGruppe], mitRueckspiel: false);
        var spiele = SpieleAus(plan);
        spiele[0].HeimMannschaftId = 99;

        GruppenSpielplan.GespeichertenPlanFinden([_gemischteGruppe], mitRueckspiel: false, spiele).Should().BeNull();
    }

    // ── Gleicher Berger-Plan wie der RoundRobinGenerator (#504) ─────────────

    /// <summary>Gruppen mit festen Teams generiert der <see cref="RoundRobinGenerator"/>, Gruppen mit Platzhaltern
    /// <see cref="GruppenSpielplan"/> — beide müssen dieselben Paarungen mit demselben Heimrecht an denselben
    /// Spieltagen liefern.</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    public void Planen_EineGruppe_GleicheSpielfolgeWieRoundRobinGenerator(int teamAnzahl, bool mitRueckspiel)
    {
        var ids   = Enumerable.Range(1, teamAnzahl).ToArray();
        var runde = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase",
            Rundenart = Rundenart.Gruppenphase, MitRueckspiel = mitRueckspiel,
        };
        var teams = ids.Select(id => new Mannschaft { Id = id, Name = $"Team {id}" }).ToList();

        var generiert = new RoundRobinGenerator().Generieren(new SpielplanKontext(runde, teams, 1)).Spiele
            .OrderBy(s => s.Spielnummer)
            .Select(s => (s.Spieltag, s.HeimMannschaftId, s.AuswaertsMannschaftId));
        var geplant = GruppenSpielplan.Planen([FesteGruppe(ids)], mitRueckspiel)
            .Select(g => ((int?)g.Spieltag, g.Heim.MannschaftId, g.Auswaerts.MannschaftId));

        RoundRobinGeneratorTests.Kurzform(geplant).Should().Be(RoundRobinGeneratorTests.Kurzform(generiert));
    }
}
