using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Teilnehmerpool einer ersten Runde mit Quoten (#576): Vorschlag und Stand.</summary>
public class TeilnehmerpoolTests
{
    // ── Testdaten ─────────────────────────────────────────────────────────────

    private const int DEUTSCHLAND = 1;
    private const int SPANIEN = 2;
    private const int BRASILIEN = 3;
    private const int UEFA = 10;
    private const int CONMEBOL = 20;

    // Deutsche Vereine 101–106, spanische 201–203, brasilianische 301–302.
    private static readonly IReadOnlyList<AuswahlKandidat> _kandidaten =
    [
        .. Enumerable.Range(1, 6).Select(r => new AuswahlKandidat(100 + r, $"Verein D{r}", DEUTSCHLAND, UEFA)),
        .. Enumerable.Range(1, 3).Select(r => new AuswahlKandidat(200 + r, $"Verein E{r}", SPANIEN, UEFA)),
        new AuswahlKandidat(301, "Alpha", BRASILIEN, CONMEBOL),
        new AuswahlKandidat(302, "Beta", BRASILIEN, CONMEBOL),
    ];

    private static Rangliste Liste(params int[] mannschaften) => new()
    {
        Eintraege = mannschaften.Select((id, i) => new RanglistenEintrag { MannschaftId = id, Rang = i + 1 }).ToList(),
    };

    // Deutschland 3 (Plätze 101, 102, 103), Spanien 1 (201), Eingang 6 → 2 freie Plätze.
    private static readonly Kontingentvorgabe _quoten = new(
        [
            new LandesKontingent(DEUTSCHLAND, "Deutschland", 3, null, null, Liste(101, 102, 103, 104, 105, 106)),
            new LandesKontingent(SPANIEN, "Spanien", 1, null, null, Liste(201, 202, 203)),
        ],
        []);

    // Rangliste der Automatik für die freien Plätze: 301 vor 104 vor 202.
    private static readonly Dictionary<int, int> _rang = new() { [301] = 1, [104] = 2, [202] = 3, [101] = 4 };

    private static Teilnehmerpool Pool(Kontingentvorgabe? quoten = null, int eingang = 6, IReadOnlyList<int>? vorab = null) =>
        new(quoten ?? _quoten, _kandidaten, eingang, _rang, vorab ?? []);

    // ── Vorschlag ─────────────────────────────────────────────────────────────

    [Fact]
    public void Vorschlagen_LeereToepfe_QuotenUndDahinterFreiePlaetzeNachRang()
    {
        Pool().Vorschlagen([]).Should().BeEquivalentTo([101, 102, 103, 201, 301, 104]);
    }

    [Fact]
    public void Vorschlagen_EingelegteBleibenDrinUndZaehlenAufIhreQuote()
    {
        // 106 liegt schon im Topf und zählt auf die deutsche Quote — für Deutschland kommen nur noch 101 und 102.
        Pool().Vorschlagen([106]).Should().BeEquivalentTo([106, 101, 102, 201, 301, 104]);
    }

    [Fact]
    public void Vorschlagen_VorabFuerTopf1KommtImmerMit()
    {
        // 302 (Titelverteidiger) belegt einen freien Platz, 104 fällt dafür heraus.
        Pool(vorab: [302]).Vorschlagen([]).Should().BeEquivalentTo([302, 101, 102, 103, 201, 301]);
    }

    [Fact]
    public void Vorschlagen_NichtZugelasseneEingelegteUndVorabZaehlenNicht()
    {
        Pool(vorab: [999]).Vorschlagen([888]).Should().NotContain([888, 999]).And.HaveCount(6);
    }

    // ── Stand ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stand_ErfuellterPool_OhneMaengel()
    {
        var pool = Pool();

        var stand = pool.Stand(pool.Vorschlagen([]).ToList());

        stand.Erfuellt.Should().BeTrue();
        stand.Landesquoten.Should().Equal(
            new QuotenStand("Deutschland", 4, 3, DEUTSCHLAND), new QuotenStand("Spanien", 1, 1, SPANIEN),
            new QuotenStand("Freie Plätze", 2, 2));
        stand.Gesamt.Should().Be(new QuotenStand("Gesamt", 6, 6));
        stand.Verbandsquoten.Should().BeEmpty("das Turnier hat keine Verbandsquoten");
    }

    [Fact]
    public void Stand_FehlendeQuote_NenntSieUndVerschweigtDieGesamtzahl()
    {
        // Spanien fehlt, dafür ein Brasilianer zu viel auf den freien Plätzen — die Gesamtzahl stimmt sogar.
        var stand = Pool().Stand([101, 102, 103, 301, 302, 104]);

        stand.Erfuellt.Should().BeFalse();
        stand.Maengel.Should().Equal("Spanien: 1 Mannschaft fehlt", "Freie Plätze: 1 Mannschaft zu viel");
    }

    [Fact]
    public void Stand_QuotenErfuelltAberZuWenige_NenntDieGesamtzahl()
    {
        Pool().Stand([101, 102, 103, 201]).Maengel.Should().Equal("Gesamt: 2 Mannschaften fehlen");
    }

    [Fact]
    public void Stand_ZuViele_NenntDieFreienPlaetze()
    {
        Pool().Stand([101, 102, 103, 201, 301, 302, 104]).Maengel.Should().Equal("Freie Plätze: 1 Mannschaft zu viel");
    }

    [Fact]
    public void Stand_NichtZugelasseneZaehlenNicht()
    {
        Pool().Stand([101, 102, 103, 201, 301, 104, 999]).Erfuellt.Should().BeTrue();
    }

    [Fact]
    public void Stand_Verbandsquoten_MitEigenenFreienPlaetzen()
    {
        var quoten = new Kontingentvorgabe([], [new VerbandsKontingent(UEFA, "UEFA", 3, null), new VerbandsKontingent(CONMEBOL, "CONMEBOL", 1, null)]);

        var stand = Pool(quoten, eingang: 5).Stand([101, 102, 201, 301, 302]);

        stand.Verbandsquoten.Should().Equal(
            new QuotenStand("UEFA", 3, 3, UEFA), new QuotenStand("CONMEBOL", 2, 1, CONMEBOL), new QuotenStand("Freie Plätze", 1, 1));
        stand.Erfuellt.Should().BeTrue();
    }

    [Fact]
    public void RangVon_OhneEintrag_Null()
    {
        Pool().RangVon(301).Should().Be(1);
        Pool().RangVon(105).Should().BeNull();
    }

    // ── Mangeltexte je Quote (Tooltip der Quotenliste, #593) ──────────────

    [Theory]
    [InlineData(2, 3, "Spanien: 1 Mannschaft fehlt")]
    [InlineData(0, 3, "Spanien: 3 Mannschaften fehlen")]
    [InlineData(3, 3, null)]
    [InlineData(4, 3, null)]
    public void Mangel_Quote_NenntWasFehlt(int aktuell, int anzahl, string? erwartet) =>
        Teilnehmerpool.Mangel(new QuotenStand("Spanien", aktuell, anzahl, 1)).Should().Be(erwartet);

    [Fact]
    public void Mangel_FreiePlaetze_NenntDenUeberhang()
    {
        Teilnehmerpool.Mangel(new QuotenStand("Freie Plätze", 3, 1)).Should().Be("Freie Plätze: 2 Mannschaften zu viel");
        Teilnehmerpool.Mangel(new QuotenStand("Freie Plätze", 0, 1)).Should().BeNull("freie Plätze dürfen leer bleiben");
    }

    [Theory]
    [InlineData(34, 36, "Gesamt: 2 Mannschaften fehlen")]
    [InlineData(37, 36, "Gesamt: 1 Mannschaft zu viel")]
    [InlineData(36, 36, null)]
    [InlineData(5, 0, null)]
    public void GesamtMangel_NenntDieAbweichungVonDerMannschaftszahl(int aktuell, int anzahl, string? erwartet) =>
        Teilnehmerpool.GesamtMangel(new QuotenStand("Gesamt", aktuell, anzahl)).Should().Be(erwartet);
}
