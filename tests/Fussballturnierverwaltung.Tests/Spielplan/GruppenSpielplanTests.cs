using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

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
}
