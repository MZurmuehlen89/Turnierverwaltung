using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Hält die Ziehung der Topf-Zuordnung (UCL-Ligaphase, <c>LigaphasenAuslosung</c>) je Startwert exakt fest
/// (#502): Ein Umbau darf weder das Ergebnis noch die Reihenfolge ändern, in der der Zufall gezogen wird. Die Regeln
/// selbst prüft <see cref="AuslosungAlgorithmusTests"/>.</summary>
public class LigaphasenAuslosungTests
{
    private static readonly IReadOnlyDictionary<int, string> _ohneGruppen = new Dictionary<int, string>();

    private static RegelKontext Regeln(bool keinGleichesLand) =>
        new(keinGleichesLand, KeineGleicheGruppe: false, HeimrechtNachLos: false, HeimrechtNachSetzliste: false,
            FreilosMoeglich: false, Konfoederationsverteilung: false, MitRueckspiel: false);

    private static List<AuslosungsErgebnis> Ausloesen(
        IEnumerable<Lostopf> toepfe, bool keinGleichesLand, int startwert, int gegnerJeTopf = 2) =>
        Losverfahren.Ausloesen(
            NeueAuslosung(Rundenart.TopfLiga, Auslosungsart.TopfZuordnung, toepfe, gegnerJeTopf: gegnerJeTopf),
            new LosVorbereitung(Regeln(keinGleichesLand), _ohneGruppen, new Dictionary<int, int>()), new Random(startwert));

    private static string Kurzform(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        string.Join(" ", ergebnisse.Select(e => $"{e.HeimMannschaftId}-{e.AuswaertsMannschaftId}"));

    // Fingerabdruck der ganzen Ziehung — 144 Paarungen ausgeschrieben wären unlesbar.
    private static string Fingerabdruck(string kurzform) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kurzform)))[..16];

    // 4 Töpfe à 9; mit Ländern wie in AuslosungAlgorithmusTests: 16 Teams aus 4 Ländern, der Rest mit eigenem Land.
    private static List<Lostopf> UclToepfe(bool mitLaendern) =>
        [.. Enumerable.Range(0, 4).Select(t => Topf(t + 1,
            [.. Enumerable.Range(t * 9 + 1, 9).Select(id => Eintrag(Team(id, mitLaendern ? LandVon(id) : null)))]))];

    private static int LandVon(int id) => id % 9 < 4 ? 100 + id % 9 : id;

    [Fact]
    public void ZweiToepfeZuDrei_ZiehungJeStartwertFest()
    {
        var ziehung = Ausloesen([TopfMitTeams(1, ersteId: 1, anzahl: 3), TopfMitTeams(2, ersteId: 4, anzahl: 3)],
            keinGleichesLand: false, startwert: 7);

        Kurzform(ziehung).Should().Be("1-2 3-1 1-4 6-1 2-3 3-5 4-3 2-6 5-2 4-5 6-4 5-6");
    }

    [Theory]
    [InlineData(false, 1, "8-5 7-8 8-16 10-8 8-26 19-8 8-31 30-8 ", "A7DCEAD06E915CB9")]
    [InlineData(false, 2, "4-8 7-4 4-15 11-4 4-27 19-4 4-36 34-4 ", "47D3B18EC2910286")]
    [InlineData(true,  1, "4-7 1-4 4-16 10-4 4-25 27-4 4-32 31-4 ", "FE2EEA349ABCA0FD")]
    [InlineData(true,  2, "8-5 3-8 8-18 12-8 8-27 19-8 8-35 33-8 ", "399AD1D1E0724874")]
    public void UclLigaphase_ZiehungJeStartwertFest(bool keinGleichesLand, int startwert, string anfang, string fingerabdruck)
    {
        var kurzform = Kurzform(Ausloesen(UclToepfe(keinGleichesLand), keinGleichesLand, startwert));

        kurzform.Should().StartWith(anfang);
        Fingerabdruck(kurzform).Should().Be(fingerabdruck);
    }

    [Fact]
    public void ToepfeZuZwei_NichtAuslosbar_FehlerNenntDieEinschraenkungen()
    {
        // Je Topf braucht jedes Team einen Heim- und einen Auswärtsgegner, nie zweimal denselben — mit nur einem
        // anderen Team im Topf geht das nicht.
        var ausloesen = () => Ausloesen([TopfMitTeams(1, ersteId: 1, anzahl: 2), TopfMitTeams(2, ersteId: 3, anzahl: 2)],
            keinGleichesLand: false, startwert: 1);

        ausloesen.Should().Throw<InvalidOperationException>().WithMessage(
            "Keine gültige Topf-Zuordnung gefunden: Die Einschränkungen (kein gleiches Land, höchstens 2 Gegner aus " +
            "demselben Land, konfliktfreie Spieltage) sind mit dieser Topf-Besetzung nicht erfüllbar.");
    }

    // ── UECL-Ligaphase: 6 Töpfe à 6, ein Gegner je Topf (#469) ───────────────

    // Mit Ländern: je Topf ein Team aus jedem der Länder 100–102, der Rest mit eigenem Land.
    private static List<Lostopf> UeclToepfe(bool mitLaendern) =>
        [.. Enumerable.Range(0, 6).Select(t => Topf(t + 1,
            [.. Enumerable.Range(t * 6 + 1, 6).Select(id => Eintrag(Team(id, mitLaendern ? UeclLandVon(id) : null)))]))];

    private static int UeclLandVon(int id) => id % 6 < 3 ? 100 + id % 6 : id;

    [Theory]
    [InlineData(false, 1, "A9D6A08A8E03E081")]
    [InlineData(true,  1, "EC9214A6860A6F0B")]
    public void UeclLigaphase_ZiehungJeStartwertFest(bool keinGleichesLand, int startwert, string fingerabdruck)
    {
        var kurzform = Kurzform(Ausloesen(UeclToepfe(keinGleichesLand), keinGleichesLand, startwert, gegnerJeTopf: 1));

        Fingerabdruck(kurzform).Should().Be(fingerabdruck);
    }

    [Fact]
    public void EinGegnerJeTopf_UngeradeTopfanzahl_WirftFehler()
    {
        var ausloesen = () => Ausloesen(
            [TopfMitTeams(1, ersteId: 1, anzahl: 4), TopfMitTeams(2, ersteId: 5, anzahl: 4), TopfMitTeams(3, ersteId: 9, anzahl: 4)],
            keinGleichesLand: false, startwert: 1, gegnerJeTopf: 1);

        ausloesen.Should().Throw<ArgumentException>().WithMessage("*gerade Anzahl Töpfe*");
    }
}
