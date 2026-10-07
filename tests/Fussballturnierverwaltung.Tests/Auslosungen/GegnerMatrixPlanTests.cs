using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class GegnerMatrixPlanTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private static readonly Dictionary<int, Mannschaft> _mannschaften =
        Enumerable.Range(1, 99).ToDictionary(id => id, id => new Mannschaft { Id = id, Name = $"Team {id}" });

    private static Lostopf Topf(int reihenfolge, params int[] mannschaftIds) => new()
    {
        Id          = reihenfolge,
        Name        = $"Topf {reihenfolge}",
        Reihenfolge = reihenfolge,
        Eintraege   = mannschaftIds
            .Select(id => new LostopfEintrag { Id = id, MannschaftId = id, Mannschaft = _mannschaften[id] })
            .ToList(),
    };

    private static AuslosungsErgebnis Spiel(int heimId, int auswaertsId) => new()
    {
        HeimMannschaftId      = heimId,
        HeimMannschaft        = _mannschaften[heimId],
        AuswaertsMannschaftId = auswaertsId,
        AuswaertsMannschaft   = _mannschaften[auswaertsId],
    };

    private static Auslosung Auslosung(IEnumerable<Lostopf> toepfe, params AuslosungsErgebnis[] ergebnisse) =>
        new() { Lostoepfe = toepfe.ToList(), Ergebnisse = ergebnisse.ToList() };

    private static GegnerPlanZeile Zeile(IReadOnlyList<GegnerPlanTopf> plan, int mannschaftId) =>
        plan.SelectMany(t => t.Zeilen).Single(z => z.Team.Mannschaft?.Id == mannschaftId);

    private static (int? Heim, int? Auswaerts) Ids(GegnerPlanPaar paar) =>
        (paar.Heim?.Mannschaft?.Id, paar.Auswaerts?.Mannschaft?.Id);

    // ── Aufbau ────────────────────────────────────────────────────────────────

    [Fact]
    public void Aufbauen_ToepfeNachReihenfolge_TeamsInDerReihenfolgeDerTopfListe()
    {
        var auslosung = Auslosung([Topf(2, 22, 21), Topf(1, 12, 11)]);

        var plan = GegnerMatrixPlan.Aufbauen(auslosung);

        plan.Select(t => t.Name).Should().Equal("Topf 1", "Topf 2");
        plan[0].Zeilen.Select(z => z.Team.Mannschaft!.Id).Should().Equal(11, 12);
        plan[1].Zeilen.Select(z => z.Team.Mannschaft!.Id).Should().Equal(21, 22);
        plan.SelectMany(t => t.Zeilen).Should().OnlyContain(z => z.GegnerJeTopf.Count == 2);
    }

    [Fact]
    public void Aufbauen_HeimUndAuswaertsgegner_StehenImTopfDesGegners()
    {
        // 11 spielt zu Hause gegen 21 (Topf 2) und auswärts bei 12 (eigener Topf)
        var auslosung = Auslosung([Topf(1, 11, 12), Topf(2, 21, 22)], Spiel(11, 21), Spiel(12, 11));

        var zeile = Zeile(GegnerMatrixPlan.Aufbauen(auslosung), 11);

        Ids(zeile.GegnerJeTopf[0]).Should().Be((null, 12));
        Ids(zeile.GegnerJeTopf[1]).Should().Be((21, null));
    }

    [Fact]
    public void Aufbauen_JedesSpiel_ErscheintGespiegeltBeimGegner()
    {
        var auslosung = Auslosung([Topf(1, 11, 12), Topf(2, 21, 22)], Spiel(11, 21), Spiel(22, 12));

        var plan = GegnerMatrixPlan.Aufbauen(auslosung);

        Ids(Zeile(plan, 11).GegnerJeTopf[1]).Should().Be((21, null));
        Ids(Zeile(plan, 21).GegnerJeTopf[0]).Should().Be((null, 11));
        Ids(Zeile(plan, 22).GegnerJeTopf[0]).Should().Be((12, null));
        Ids(Zeile(plan, 12).GegnerJeTopf[1]).Should().Be((null, 22));
    }

    [Fact]
    public void Aufbauen_OhneSpielGegenEinenTopf_BleibenBeideGegnerLeer()
    {
        var auslosung = Auslosung([Topf(1, 11), Topf(2, 21)]);

        var plan = GegnerMatrixPlan.Aufbauen(auslosung);

        plan.SelectMany(t => t.Zeilen).SelectMany(z => z.GegnerJeTopf)
            .Should().OnlyContain(p => p.Heim == null && p.Auswaerts == null);
    }

    [Fact]
    public void Aufbauen_Freilos_WirdNichtEingetragen()
    {
        var freilos = Spiel(11, 21);
        freilos.IstFreilos = true;
        var auslosung = Auslosung([Topf(1, 11), Topf(2, 21)], freilos);

        var plan = GegnerMatrixPlan.Aufbauen(auslosung);

        Ids(Zeile(plan, 11).GegnerJeTopf[1]).Should().Be((null, null));
    }

    [Fact]
    public void Aufbauen_GegnerOhneLostopf_WirdNichtEingetragen()
    {
        var auslosung = Auslosung([Topf(1, 11), Topf(2, 21)], Spiel(11, 77));

        var plan = GegnerMatrixPlan.Aufbauen(auslosung);

        Zeile(plan, 11).GegnerJeTopf.Should().OnlyContain(p => p.Heim == null && p.Auswaerts == null);
    }

    [Fact]
    public void Aufbauen_Platzhalter_WerdenUeberDenNamenZugeordnet()
    {
        var topf1 = new Lostopf
        {
            Id = 1, Name = "Topf 1", Reihenfolge = 1,
            Eintraege = [new LostopfEintrag { Id = 1, PlatzhalterName = "Sieger Play-off A" }],
        };
        var auslosung = Auslosung([topf1, Topf(2, 21)],
            new AuslosungsErgebnis
            {
                HeimPlatzhalterName   = "Sieger Play-off A",
                AuswaertsMannschaftId = 21,
                AuswaertsMannschaft   = _mannschaften[21],
            });

        var plan = GegnerMatrixPlan.Aufbauen(auslosung);

        var platzhalterZeile = plan[0].Zeilen.Single();
        platzhalterZeile.Team.Schluessel.Should().Be("P:Sieger Play-off A");
        Ids(platzhalterZeile.GegnerJeTopf[1]).Should().Be((21, null));
        Zeile(plan, 21).GegnerJeTopf[0].Auswaerts!.Schluessel.Should().Be("P:Sieger Play-off A");
        Zeile(plan, 21).Team.Schluessel.Should().Be("21");
    }
}
