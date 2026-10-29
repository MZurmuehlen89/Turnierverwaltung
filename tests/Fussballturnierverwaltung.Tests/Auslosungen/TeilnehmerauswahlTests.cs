using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Teilnehmerauswahl nach Kontingenten (#573).</summary>
public class TeilnehmerauswahlTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private const int DEUTSCHLAND = 1;
    private const int SPANIEN = 2;
    private const int BRASILIEN = 3;
    private const int UEFA = 10;
    private const int CONMEBOL = 20;

    // Deutsche Vereine 101–112 auf den Plätzen 1–12, spanische 201–204, brasilianische 301 („Alpha“) und 302 („Beta“).
    private static readonly IReadOnlyList<AuswahlKandidat> _pool =
    [
        .. Enumerable.Range(1, 12).Select(r => new AuswahlKandidat(100 + r, $"Verein D{r:00}", DEUTSCHLAND, UEFA)),
        .. Enumerable.Range(1, 4).Select(r => new AuswahlKandidat(200 + r, $"Verein E{r:00}", SPANIEN, UEFA)),
        new AuswahlKandidat(301, "Alpha", BRASILIEN, CONMEBOL),
        new AuswahlKandidat(302, "Beta", BRASILIEN, CONMEBOL),
    ];

    private static readonly Dictionary<int, int> _keinRang = [];

    // Wie die deutsche Pyramide im Kleinen: Bundesliga 1–4, 2. Bundesliga 5–8, zwei Regionalliga-Staffeln 9–10 und 11–12.
    private static Rangliste Pyramide(params int[] ohneRang) => new()
    {
        Abschnitte =
        [
            new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 4 },
            new RanglistenAbschnitt { Name = "2. Bundesliga", Spielklasse = 2, BisPlatz = 8 },
            new RanglistenAbschnitt { Name = "Regionalliga Nordost", Spielklasse = 3, BisPlatz = 10 },
            new RanglistenAbschnitt { Name = "Regionalliga West", Spielklasse = 3, BisPlatz = 12 },
        ],
        Eintraege = Enumerable.Range(1, 12).Except(ohneRang)
            .Select(r => new RanglistenEintrag { MannschaftId = 100 + r, Rang = r }).ToList(),
    };

    private static Rangliste Liste(params int[] mannschaftenInReihenfolge) => new()
    {
        Eintraege = mannschaftenInReihenfolge
            .Select((id, i) => new RanglistenEintrag { MannschaftId = id, Rang = i + 1 }).ToList(),
    };

    private static LandesKontingent Deutschland(int anzahl, int? spielklasse = null, int? abPlatz = null, Rangliste? rangliste = null) =>
        new(DEUTSCHLAND, "Deutschland", anzahl, spielklasse, abPlatz, rangliste ?? Pyramide());

    private static LandesKontingent Spanien(int anzahl) =>
        new(SPANIEN, "Spanien", anzahl, null, null, Liste(201, 202, 203, 204));

    private static Kontingentvorgabe Quoten(params LandesKontingent[] landes) => new(landes, []);

    private static Kontingentvorgabe Quoten(VerbandsKontingent verband, params LandesKontingent[] landes) => new(landes, [verband]);

    private static Teilnehmerauswahl Waehlen(
        Kontingentvorgabe quoten, int eingang,
        IReadOnlyList<AuswahlKandidat>? pool = null,
        IReadOnlySet<int>? eingelegt = null,
        IReadOnlyDictionary<int, int>? rang = null) =>
        Teilnehmerauswahl.Waehlen(quoten, pool ?? _pool, eingelegt ?? new HashSet<int>(), eingang, rang ?? _keinRang);

    // ── Landesquoten ──────────────────────────────────────────────────────────

    [Fact]
    public void Spielklasse_AbPlatz_NimmtGenauDiesePlaetze()
    {
        var auswahl = Waehlen(Quoten(Deutschland(2, spielklasse: 1, abPlatz: 3)), eingang: 2);

        auswahl.Mannschaften.Should().Equal(103, 104);
        auswahl.Fehlbestaende.Should().BeEmpty();
    }

    [Fact]
    public void Spielklasse_UeberParalleleStaffeln() =>
        Waehlen(Quoten(Deutschland(2, spielklasse: 3, abPlatz: 2)), eingang: 2).Mannschaften.Should().Equal(110, 111);

    [Fact]
    public void OhneSpielklasse_DieErstenDesLandes() =>
        Waehlen(Quoten(Spanien(2)), eingang: 2).Mannschaften.Should().Equal(201, 202);

    [Fact]
    public void OhneRangliste_Fehlbestand()
    {
        var auswahl = Waehlen(Quoten(new LandesKontingent(BRASILIEN, "Brasilien", 2, null, null, null)), eingang: 2);

        auswahl.Mannschaften.Should().BeEmpty();
        auswahl.Fehlbestaende.Should().Equal("Brasilien: 2 verlangt, keine Rangliste");
    }

    [Fact]
    public void BereichPasstNichtInDieRangliste_Fehlbestand() =>
        Waehlen(Quoten(Deutschland(2, spielklasse: 1, abPlatz: 4)), eingang: 2).Fehlbestaende
            .Should().Equal("Deutschland (Spielklasse 1 ab Platz 4): 2 verlangt, nicht in der Rangliste");

    [Fact]
    public void Luecke_BleibtOffenUndWirdNichtUeberFreiePlaetzeAufgefuellt()
    {
        var auswahl = Waehlen(Quoten(Deutschland(4, spielklasse: 1, rangliste: Pyramide(ohneRang: 2))), eingang: 4);

        auswahl.Mannschaften.Should().Equal(101, 103, 104);
        auswahl.Fehlbestaende.Should().Equal("Deutschland (Bundesliga, Plätze 1–4): 1 Platz nicht besetzbar");
    }

    [Fact]
    public void NichtZugelassen_BleibtOffen()
    {
        var auswahl = Waehlen(Quoten(Deutschland(4, spielklasse: 1)), eingang: 4, pool: [.. _pool.Where(k => k.MannschaftId != 103)]);

        auswahl.Mannschaften.Should().Equal(101, 102, 104);
        auswahl.Fehlbestaende.Should().Equal("Deutschland (Bundesliga, Plätze 1–4): 1 Platz nicht besetzbar");
    }

    [Fact]
    public void MehrerePlaetzeOffen_Mehrzahl() =>
        Waehlen(Quoten(Spanien(6)), eingang: 6).Fehlbestaende.Should().Equal("Spanien: 2 Plätze nicht besetzbar");

    [Fact]
    public void SchonEingelegt_ZaehltAufSeinKontingent() =>
        Waehlen(Quoten(Deutschland(4, spielklasse: 1)), eingang: 4, eingelegt: new HashSet<int> { 102 })
            .Mannschaften.Should().Equal(101, 103, 104);

    [Fact]
    public void SchonEingelegt_ZaehltAufLandesquoteOhneSpielklasse_AuchWennWeiterHinten() =>
        Waehlen(Quoten(Spanien(2)), eingang: 2, eingelegt: new HashSet<int> { 204 }).Mannschaften.Should().Equal(201);

    [Fact]
    public void ZweiKontingenteEinesLandes_UeberschneidenSichNicht() =>
        Waehlen(Quoten(Deutschland(2), Deutschland(4, spielklasse: 1)), eingang: 6).Mannschaften.Should().Equal(101, 102, 103, 104, 105, 106);

    // ── Verbandsquoten ────────────────────────────────────────────────────────

    [Fact]
    public void Verbandsquote_NurTeamsDesVerbands()
    {
        var uefa = new VerbandsKontingent(UEFA, "UEFA", 2, Liste(301, 201, 101, 102));

        Waehlen(Quoten(uefa), eingang: 2).Mannschaften.Should().Equal(201, 101);
    }

    [Fact]
    public void Verbandsquote_OhneRangliste_Fehlbestand() =>
        Waehlen(Quoten(new VerbandsKontingent(UEFA, "UEFA", 2, null)), eingang: 2)
            .Fehlbestaende.Should().Equal("UEFA: 2 verlangt, keine Rangliste");

    [Fact]
    public void UeberschneidungLandUndVerband_TeamZaehltFuerBeide()
    {
        var uefa = new VerbandsKontingent(UEFA, "UEFA", 3, Liste(201, 101, 102, 202));

        var auswahl = Waehlen(Quoten(uefa, Deutschland(2, spielklasse: 1)), eingang: 5, rang: new Dictionary<int, int> { [302] = 1 });

        // Deutschland 101, 102 zählen auch für die UEFA, die nur noch 201 braucht; freie Plätze: 5 − 3 = 2.
        auswahl.Mannschaften.Should().Equal(101, 102, 201, 302, 301);
    }

    // ── Freie Plätze ──────────────────────────────────────────────────────────

    [Fact]
    public void FreiePlaetze_NachRangDerAutomatik_DannNachName()
    {
        var auswahl = Waehlen(Quoten(Spanien(1)), eingang: 3, rang: new Dictionary<int, int> { [302] = 1 });

        auswahl.Mannschaften.Should().Equal(201, 302, 301);
    }

    [Fact]
    public void FreiePlaetze_EingelegteOhneQuoteBelegenSieZuerst() =>
        Waehlen(Quoten(Spanien(1)), eingang: 2, eingelegt: new HashSet<int> { 301 }).Mannschaften.Should().Equal(201);
}
