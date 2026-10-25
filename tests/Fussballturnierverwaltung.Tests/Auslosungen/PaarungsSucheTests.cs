using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Hält die Paarungssuche der Topfpaar-Auslosung („Nach Töpfen“, „Nach Setzliste“, <c>PaarungsSuche</c>) je
/// Startwert exakt fest (#502): Ein Umbau darf weder das Ergebnis noch die Reihenfolge ändern, in der der Zufall
/// gezogen wird. Die Regeln selbst prüfen <see cref="LosverfahrenTests"/> und <see cref="ManuelleZiehungTests"/>.</summary>
public class PaarungsSucheTests
{
    private static readonly IReadOnlyDictionary<int, string> _ohneGruppen = new Dictionary<int, string>();

    private static RegelKontext Regeln(bool keinGleichesLand = false, bool freilosMoeglich = false) =>
        new(keinGleichesLand, KeineGleicheGruppe: false, HeimrechtNachLos: false, HeimrechtNachSetzliste: false,
            freilosMoeglich, Konfoederationsverteilung: false, MitRueckspiel: false);

    private static string Kurzform(IEnumerable<AuslosungsErgebnis> ergebnisse) =>
        string.Join(" ", ergebnisse.Select(e =>
            $"{e.HeimMannschaftId}-{(e.IstFreilos ? "frei" : e.AuswaertsMannschaftId?.ToString())}"));

    private static string Ausloesen(Auslosungsart art, IEnumerable<Lostopf> toepfe, RegelKontext regeln, int startwert) =>
        Kurzform(Losverfahren.Ausloesen(
            NeueAuslosung(Rundenart.KoRunde, art, toepfe), regeln, _ohneGruppen, new Random(startwert)));

    // 7 + 4 Teams wie in einer Pokalrunde: 4 Topfpaare, 3 Teams Überhang, eines davon bekommt das Freilos. Die Länder
    // wiederholen sich (Land = Id mod 3), damit „Kein gleiches Land“ die Suche zum Zurücknehmen zwingt.
    private static List<Lostopf> Pokaltoepfe() =>
    [
        Topf(1, [.. Enumerable.Range(1, 7).Select(id => Eintrag(Team(id, landId: id % 3)))]),
        Topf(2, [.. Enumerable.Range(8, 4).Select(id => Eintrag(Team(id, landId: id % 3)))])
    ];

    [Theory]
    [InlineData(false, 1, "1-9 2-11 5-8 7-10 6-3 4-frei")]
    [InlineData(false, 2, "2-11 3-9 5-10 6-8 1-7 4-frei")]
    [InlineData(true,  1, "1-9 2-10 3-8 7-11 6-5 4-frei")]
    [InlineData(true,  2, "2-9 5-10 6-8 7-11 1-3 4-frei")]
    public void NachToepfen_UeberhangUndFreilos_ZiehungJeStartwertFest(bool keinGleichesLand, int startwert, string erwartet) =>
        Ausloesen(Auslosungsart.NachToepfen, Pokaltoepfe(), Regeln(keinGleichesLand, freilosMoeglich: true), startwert)
            .Should().Be(erwartet);

    [Theory]
    [InlineData(1, "1-6 2-7 3-4 5-8")]
    [InlineData(2, "1-8 2-5 3-6 7-4")]
    public void NachSetzliste_ZiehungJeStartwertFest(int startwert, string erwartet)
    {
        var toepfe = new[]
        {
            Topf(1, [.. Enumerable.Range(1, 8).Select(id => Eintrag(Team(id, landId: id % 2), setzPosition: id <= 3 ? id : null))])
        };

        Ausloesen(Auslosungsart.NachSetzliste, toepfe, Regeln(keinGleichesLand: true), startwert).Should().Be(erwartet);
    }

    [Fact]
    public void NachToepfen_NichtErfuellbar_FehlerNenntDieEinschraenkung()
    {
        // Alle vier Teams aus einem Land: mit „Kein gleiches Land“ gibt es kein einziges erlaubtes Paar.
        var toepfe = new[]
        {
            Topf(1, Eintrag(Team(1, landId: 1)), Eintrag(Team(2, landId: 1))),
            Topf(2, Eintrag(Team(3, landId: 1)), Eintrag(Team(4, landId: 1)))
        };

        var ausloesen = () => Ausloesen(Auslosungsart.NachToepfen, toepfe, Regeln(keinGleichesLand: true), startwert: 1);

        ausloesen.Should().Throw<InvalidOperationException>().WithMessage(
            "Keine gültige Auslosung gefunden. Die Einschränkungen ('Kein gleiches Land') können mit den gewählten " +
            "Mannschaften nicht erfüllt werden.");
    }
}
