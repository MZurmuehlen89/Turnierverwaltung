using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class ZiehungsReihenfolgeTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private static Lostopf Topf(int reihenfolge, params int[] mannschaftIds) => new()
    {
        Id          = reihenfolge,
        Name        = $"Topf {reihenfolge}",
        Reihenfolge = reihenfolge,
        Eintraege   = mannschaftIds.Select(id => new LostopfEintrag { Id = id, MannschaftId = id }).ToList(),
    };

    private static AuslosungsErgebnis GruppenPlatz(int gruppe, int mannschaftId) =>
        new() { PaarungsNummer = gruppe, HeimMannschaftId = mannschaftId };

    private static AuslosungsErgebnis Paarung(int nummer, int heimId, int auswaertsId) =>
        new() { PaarungsNummer = nummer, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId };

    /// <summary>Vier Gruppen, zwei Töpfe mit je acht Teams: aus jedem Topf kommen zwei Teams in jede Gruppe.
    /// Mannschafts-Id = Topf * 100 + Gruppe * 10 + laufende Nummer in der Gruppe.</summary>
    private static (List<Lostopf> Toepfe, List<AuslosungsErgebnis> Ergebnisse) GruppenAuslosung()
    {
        var ids = (int topf) => Enumerable.Range(1, 4).SelectMany(g => new[] { topf * 100 + g * 10 + 1, topf * 100 + g * 10 + 2 });
        var toepfe = new List<Lostopf> { Topf(2, ids(2).ToArray()), Topf(1, ids(1).ToArray()) };
        var ergebnisse = toepfe
            .SelectMany(t => t.Eintraege)
            .OrderBy(e => e.MannschaftId)
            .Select(e => GruppenPlatz(e.MannschaftId!.Value / 10 % 10, e.MannschaftId.Value))
            .ToList();
        return (toepfe, ergebnisse);
    }

    // ── Gruppenphase ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void Gruppenphase_EinTopfWirdVollstaendigGeleert_BevorDerNaechsteBeginnt(int seed)
    {
        var (toepfe, ergebnisse) = GruppenAuslosung();

        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(seed));

        schritte.Should().HaveCount(16).And.OnlyContain(s => s.IstGruppenmodus && s.IstHeim == null && !s.IstFreilos);
        schritte.Select(s => s.TopfId).Should().Equal(Enumerable.Repeat(1, 8).Concat(Enumerable.Repeat(2, 8)));
        schritte.Select(s => s.Eintrag.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void Gruppenphase_InnerhalbEinesTopfs_BekommtJedeGruppeEinTeamBevorEineIhrZweitesErhaelt(int seed)
    {
        var (toepfe, ergebnisse) = GruppenAuslosung();

        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(seed));

        foreach (var durchgang in schritte.Chunk(4))
            durchgang.Select(s => s.ZielNummer).Should().BeEquivalentTo([1, 2, 3, 4]);
    }

    [Fact]
    public void Gruppenphase_PositionIstDieGespeicherteReihenfolgeInDerGruppe()
    {
        var toepfe = new List<Lostopf> { Topf(1, 1, 2), Topf(2, 3, 4), Topf(3, 5, 6) };
        // Gruppe 1: 5, 1, 3 — Gruppe 2: 4, 6, 2 (bewusst nicht in Topf-Reihenfolge gespeichert)
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            GruppenPlatz(1, 5), GruppenPlatz(2, 4), GruppenPlatz(1, 1),
            GruppenPlatz(2, 6), GruppenPlatz(1, 3), GruppenPlatz(2, 2),
        };

        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(3));

        schritte.ToDictionary(s => s.Eintrag.MannschaftId!.Value, s => (s.ZielNummer, s.Position))
            .Should().BeEquivalentTo(new Dictionary<int, (int, int?)>
            {
                [5] = (1, 1), [1] = (1, 2), [3] = (1, 3),
                [4] = (2, 1), [6] = (2, 2), [2] = (2, 3),
            });
    }

    [Fact]
    public void Gruppenphase_PlatzhalterEintrag_WirdUeberDenNamenZugeordnet()
    {
        var topf = new Lostopf
        {
            Id = 1, Name = "Topf 1", Reihenfolge = 1,
            Eintraege = [new LostopfEintrag { Id = 1, PlatzhalterName = "Sieger Play-off A" }],
        };
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            new() { PaarungsNummer = 2, HeimPlatzhalterName = "Sieger Play-off A" },
        };

        var schritte = ZiehungsReihenfolge.Aufbauen([topf], ergebnisse, new Random(1));

        schritte.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ZiehungsSchritt(topf.Eintraege.Single(), 1, "Topf 1", ZielNummer: 2,
                IstGruppenmodus: true, IstHeim: null, Position: 1, IstFreilos: false));
    }

    // ── Paarungen (K.-o.-Runde) ───────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void Paarungen_WerdenPaarungFuerPaarungGezogen_ErstHeimDannAuswaerts(int seed)
    {
        // Gesetzte (Topf 1) haben Heimrecht, außer in Paarung 3
        var toepfe = new List<Lostopf> { Topf(1, 11, 12, 13), Topf(2, 21, 22, 23) };
        var ergebnisse = new List<AuslosungsErgebnis> { Paarung(2, 12, 23), Paarung(3, 21, 13), Paarung(1, 11, 22) };

        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(seed));

        schritte.Should().OnlyContain(s => !s.IstGruppenmodus && s.Position == null && !s.IstFreilos);
        schritte.Select(s => (s.ZielNummer, s.Eintrag.MannschaftId, s.IstHeim)).Should().Equal(
            (1, 11, true), (1, 22, false),
            (2, 12, true), (2, 23, false),
            (3, 21, true), (3, 13, false));
    }

    [Fact]
    public void Paarungen_Freilos_IstEinEinzelnerSchrittOhneGegner()
    {
        var toepfe = new List<Lostopf> { Topf(1, 1, 2, 3) };
        var ergebnisse = new List<AuslosungsErgebnis>
        {
            Paarung(1, 1, 2),
            new() { PaarungsNummer = 2, HeimMannschaftId = 3, IstFreilos = true },
        };

        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(5));

        schritte.Should().HaveCount(3);
        schritte[2].Should().Match<ZiehungsSchritt>(s =>
            s.ZielNummer == 2 && s.Eintrag.MannschaftId == 3 && s.IstFreilos && s.IstHeim == true && !s.IstGruppenmodus);
    }

    // ── Zwischenstand ─────────────────────────────────────────────────────────

    /// <summary>Die Indizes, nach denen ein Zwischenstand fällig ist, mit ihrem Titel.</summary>
    private static List<(int Index, string Titel)> Zwischenstaende(List<ZiehungsSchritt> schritte) =>
        Enumerable.Range(0, schritte.Count)
            .Select(i => (Index: i, Titel: ZiehungsReihenfolge.ZwischenstandNach(schritte, i)))
            .Where(x => x.Titel is not null)
            .Select(x => (x.Index, x.Titel!))
            .ToList();

    /// <summary>K.-o.-Runde mit <paramref name="paarungen"/> Paarungen, davon die ersten <paramref name="freilose"/>
    /// als Freilos.</summary>
    private static List<ZiehungsSchritt> PaarungsSchritte(int paarungen, int freilose = 0)
    {
        var nummern = Enumerable.Range(1, paarungen).ToList();
        var toepfe = new List<Lostopf>
        {
            Topf(1, nummern.ToArray()),
            Topf(2, nummern.Skip(freilose).Select(n => 100 + n).ToArray()),
        };
        var ergebnisse = nummern
            .Select(n => n <= freilose
                ? new AuslosungsErgebnis { PaarungsNummer = n, HeimMannschaftId = n, IstFreilos = true }
                : Paarung(n, n, 100 + n))
            .ToList();
        return ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(3));
    }

    [Fact]
    public void Zwischenstand_Gruppenphase_NachJedemGeleertenTopfAusserDemLetzten()
    {
        var (toepfe, ergebnisse) = GruppenAuslosung();
        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(1));

        Zwischenstaende(schritte).Should().Equal((7, "Topf 1 komplett"));
    }

    [Fact]
    public void Zwischenstand_Paarungen_NachJeAchtFertigenPaarungen()
    {
        var schritte = PaarungsSchritte(paarungen: 17);

        Zwischenstaende(schritte).Should().Equal(
            (15, "8 von 17 Paarungen gezogen"),
            (31, "16 von 17 Paarungen gezogen"));
    }

    [Fact]
    public void Zwischenstand_Paarungen_NichtNachDerLetztenPaarung()
    {
        var schritte = PaarungsSchritte(paarungen: 16);

        Zwischenstaende(schritte).Should().Equal((15, "8 von 16 Paarungen gezogen"));
    }

    [Fact]
    public void Zwischenstand_Paarungen_FreilosZaehltAlsFertigePaarung()
    {
        // Sieben Freilose (je ein Schritt), danach vollständige Paarungen (je zwei Schritte)
        var schritte = PaarungsSchritte(paarungen: 10, freilose: 7);

        Zwischenstaende(schritte).Should().Equal((8, "8 von 10 Paarungen gezogen"));
    }

    [Fact]
    public void Zwischenstand_EinzigerSchritt_IstNieFaellig()
    {
        var schritte = PaarungsSchritte(paarungen: 1, freilose: 1);

        Zwischenstaende(schritte).Should().BeEmpty();
    }

    // ── Allgemein ─────────────────────────────────────────────────────────────

    [Fact]
    public void EintragOhneErgebnis_WirdNichtGezogen()
    {
        var toepfe = new List<Lostopf> { Topf(1, 1, 2, 99) };
        var ergebnisse = new List<AuslosungsErgebnis> { Paarung(1, 1, 2) };

        var schritte = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(1));

        schritte.Select(s => s.Eintrag.MannschaftId).Should().Equal(1, 2);
    }

    [Fact]
    public void OhneErgebnisse_GibtEsKeineSchritte()
    {
        ZiehungsReihenfolge.Aufbauen([Topf(1, 1, 2)], [], new Random(1)).Should().BeEmpty();
    }

    [Fact]
    public void GleicherSeed_LiefertDieselbeReihenfolge()
    {
        var (toepfe, ergebnisse) = GruppenAuslosung();

        var erste  = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(123));
        var zweite = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(123));

        zweite.Should().Equal(erste);
    }

    [Fact]
    public void VerschiedeneSeeds_AendernNurDieReihenfolgeNichtDieZuordnung()
    {
        var (toepfe, ergebnisse) = GruppenAuslosung();

        var erste  = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(1));
        var zweite = ZiehungsReihenfolge.Aufbauen(toepfe, ergebnisse, new Random(2));

        zweite.Should().BeEquivalentTo(erste).And.NotEqual(erste);
    }
}
