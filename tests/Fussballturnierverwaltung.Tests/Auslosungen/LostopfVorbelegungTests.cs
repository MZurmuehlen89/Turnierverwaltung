using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class LostopfVorbelegungTests
{
    // Mannschafts-Id = Ranglistenplatz, damit die Erwartungen direkt lesbar bleiben.
    private static Dictionary<int, int> RangGleichId(IEnumerable<int> ids) => ids.ToDictionary(id => id, id => id);

    [Fact]
    public void Verteilen_UefaLigaphase_Je9TeamsNachRangAufVierToepfe()
    {
        var ids = Enumerable.Range(1, 36).Reverse().ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4);

        toepfe.Should().HaveCount(4);
        toepfe[0].Should().Equal(Enumerable.Range(1, 9));
        toepfe[1].Should().Equal(Enumerable.Range(10, 9));
        toepfe[3].Should().Equal(Enumerable.Range(28, 9));
    }

    [Fact]
    public void Verteilen_TitelverteidigerAusserhalbTop8_VerdraengtDenNeuntenAusTopf1()
    {
        var ids = Enumerable.Range(1, 36).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4, vorabInTopf1: [20]);

        toepfe[0].Should().Equal(20, 1, 2, 3, 4, 5, 6, 7, 8);
        toepfe[1].Should().StartWith(9).And.NotContain(20);
    }

    [Fact]
    public void Verteilen_TitelverteidigerInnerhalbTop9_Topf1BleibtDieTop9()
    {
        var ids = Enumerable.Range(1, 36).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4, vorabInTopf1: [5]);

        toepfe[0].Should().BeEquivalentTo(Enumerable.Range(1, 9));
    }

    [Fact]
    public void Verteilen_TitelverteidigerNichtUnterDenTeams_WirdIgnoriert()
    {
        var ids = Enumerable.Range(1, 8).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 2, vorabInTopf1: [99]);

        toepfe[0].Should().Equal(1, 2, 3, 4);
        toepfe[1].Should().Equal(5, 6, 7, 8);
    }

    [Fact]
    public void Verteilen_TeamsOhneRanglisteneintrag_LandenHintenInUebergebenerReihenfolge()
    {
        var ids = new List<int> { 50, 3, 40, 1, 2 };
        var rang = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 };

        var toepfe = LostopfVorbelegung.Verteilen(ids, rang, 1);

        toepfe[0].Should().Equal(1, 2, 3, 50, 40);
    }

    /// <summary>#565: Die Gruppensieger sind in der Rangliste schlechter platziert als einige Zweite – trotzdem
    /// kommen alle Sieger in Topf 1, innerhalb gleicher Plätze nach Rang.</summary>
    [Fact]
    public void Verteilen_MitGruppenplatz_SiegerVorZweitenUnabhaengigVomRang()
    {
        var ids = Enumerable.Range(1, 8).ToList();
        var gruppenplatz = new Dictionary<int, int> { [8] = 1, [6] = 1, [5] = 1, [3] = 1, [1] = 2, [2] = 2, [4] = 2, [7] = 2 };

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 2, gruppenplatzProMannschaft: gruppenplatz);

        toepfe[0].Should().Equal(3, 5, 6, 8);
        toepfe[1].Should().Equal(1, 2, 4, 7);
    }

    [Fact]
    public void Verteilen_TeamsOhneGruppenplatz_FolgenNachDenPlatzierten()
    {
        var ids = Enumerable.Range(1, 4).ToList();
        var gruppenplatz = new Dictionary<int, int> { [3] = 1, [4] = 2 };

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 1, gruppenplatzProMannschaft: gruppenplatz);

        toepfe[0].Should().Equal(3, 4, 1, 2);
    }

    [Fact]
    public void Verteilen_NichtGlattTeilbar_VerteiltFairStattLetztenTopfLeerZuLassen()
    {
        var ids = Enumerable.Range(1, 9).ToList();

        var toepfe = LostopfVorbelegung.Verteilen(ids, RangGleichId(ids), 4);

        toepfe.Select(t => t.Count).Should().Equal(3, 2, 2, 2);
    }

    // ── Nach Profigrenze (#470) ─────────────────────────────────────────────

    // Ids 1–3 Bundesliga, 4–5 2. Bundesliga, 6–9 3. Liga und tiefer, 10 ohne Klasse; Profigrenze 2 wie im DFB-Pokal.
    private static readonly Spielklassen _dfbKlassen = new(
        new Dictionary<int, int> { [1] = 1, [2] = 1, [3] = 1, [4] = 2, [5] = 2, [6] = 3, [7] = 3, [8] = 4, [9] = 4 },
        Profigrenze: 2);

    [Fact]
    public void NachProfigrenzeVerteilen_ProfisInTopf1_AmateureUndTeamsOhneKlasseInTopf2_UngleichGross()
    {
        var ids = new List<int> { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        var toepfe = LostopfVorbelegung.NachProfigrenzeVerteilen(ids, RangGleichId(ids), _dfbKlassen);

        toepfe[0].Should().Equal(1, 2, 3, 4, 5);
        toepfe[1].Should().Equal(6, 7, 8, 9, 10);
    }

    [Fact]
    public void NachProfigrenzeVerteilen_KeineAmateureMehr_AlleInTopf1()
    {
        var ids = new List<int> { 1, 2, 3, 4 };

        var toepfe = LostopfVorbelegung.NachProfigrenzeVerteilen(ids, RangGleichId(ids), _dfbKlassen);

        toepfe[0].Should().HaveCount(4);
        toepfe[1].Should().BeEmpty();
    }

    [Theory]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.KoRunde, AuslosungsRegelArt.HeimrechtFuerUnterklassige, 2, 2, true)]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.KoRunde, AuslosungsRegelArt.HeimrechtFuerUnterenTopf, 2, 2, false)]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.KoRunde, AuslosungsRegelArt.HeimrechtFuerUnterklassige, null, 2, false)]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.KoRunde, AuslosungsRegelArt.HeimrechtFuerUnterklassige, 2, 4, false)]
    [InlineData(Auslosungsart.Zufall, Rundenart.KoRunde, AuslosungsRegelArt.HeimrechtFuerUnterklassige, 2, 2, false)]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.Gruppenphase, AuslosungsRegelArt.HeimrechtFuerUnterklassige, 2, 2, false)]
    public void NachProfigrenzeTeilen_NurKoRundeNachToepfenMitZweiToepfenUnterklassigenRegelUndProfigrenze(
        Auslosungsart art, Rundenart rundenart, AuslosungsRegelArt regel, int? profigrenze, int topfAnzahl, bool erwartet)
    {
        var auslosung = NeueAuslosung(rundenart, art,
            [.. Enumerable.Range(1, topfAnzahl).Select(i => TopfMitTeams(i, ersteId: 10 * i, anzahl: 2))]);
        auslosung.Regeln.Add(new AuslosungsRegel { Regelart = regel, Beschreibung = regel.ToString(), IstAktiv = true });

        LostopfVorbelegung.NachProfigrenzeTeilen(auslosung, profigrenze).Should().Be(erwartet);
    }
}
