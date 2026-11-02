using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>FIFA Klub-WM aus der Vorlage (#581): Die Vorlage verlangt je Kontinentalverband eine Mindestzahl Vereine
/// (#574). Der Teilnehmerpool füllt sie aus den Vereinsranglisten der Verbände – für Ozeanien erst, seit es dort Vereine
/// gibt (vorher war die Vorlage gesperrt). Die Gruppenphase hat keine Standard-Rangliste; nach einer gewählten
/// Rangliste befüllt die Automatik die Töpfe, und die Auslosung lässt sich durchführen. Die Verteilung je Verband kommt
/// aus der Datenbank.</summary>
public sealed class KlubWmTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string RUNDE = "Gruppenphase";
    private const string RANGLISTE = "UEFA-Klub-Koeffizient (Kassies)";

    private static readonly Dictionary<string, int> _quoten = new()
    {
        ["UEFA"] = 12, ["CONMEBOL"] = 6, ["CONCACAF"] = 5, ["AFC"] = 4, ["CAF"] = 4, ["OFC"] = 1,
    };

    [Fact]
    public async Task KlubWm_PoolErfuelltJedeVerbandsquote_AutomatikBefuelltUndLost()
    {
        string name = app.Hauptfenster.TurnierAusVorlageErstellen("FIFA Klub-WM", "32 Teams · ab 2025");
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen(name);

        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(RUNDE);
        auslosung.TeilnehmerpoolOeffnen();
        auslosung.Kachel("Gesamt").Should().Be("32 / 32", "der Pool ist aus den Vereinsranglisten der Verbände vorbelegt");
        auslosung.PoolMaengel.Should().BeEmpty("jeder Verband hat genug Vereine in seiner Vereinsrangliste");

        auslosung.LostoepfeOeffnen();
        auslosung.RanglisteWaehlen(RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        var verbandJeVerein = await VerbandJeTopfVereinAsync(name);
        verbandJeVerein.GroupBy(v => v.Value).ToDictionary(g => g.Key, g => g.Count())
            .Should().BeEquivalentTo(_quoten, "die Töpfe enthalten genau den Pool");
        verbandJeVerein.Should().ContainKey("Auckland City", "der beste Verein der OFC-Rangliste");

        auslosung.Durchfuehren();
        auslosung.Status.Should().Be(AuslosungReiter.ABGESCHLOSSEN);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    /// <summary>Verband je Verein in den Lostöpfen der Gruppenphase.</summary>
    private async Task<Dictionary<string, string>> VerbandJeTopfVereinAsync(string turnier) =>
        (await datenbank.SpalteLesenAsync(
            """
            SELECT m.Name + '|' + v.Kurzname
            FROM LostopfEintraege e
            JOIN Lostoepfe l ON l.Id = e.LostopfId
            JOIN Auslosungen a ON a.Id = l.AuslosungId
            JOIN TurnierRunden r ON r.Id = a.TurnierRundeId
            JOIN TurnierInstanzen t ON t.Id = r.TurnierInstanzId
            JOIN Mannschaften m ON m.Id = e.MannschaftId
            JOIN Laender la ON la.Id = m.LandId
            JOIN Verbaende v ON v.Id = la.VerbandId
            WHERE t.Name = @turnier AND r.Name = @runde
            """,
            ("@turnier", turnier), ("@runde", RUNDE)))
        .Select(zeile => zeile.Split('|'))
        .ToDictionary(teile => teile[0], teile => teile[1]);
}
