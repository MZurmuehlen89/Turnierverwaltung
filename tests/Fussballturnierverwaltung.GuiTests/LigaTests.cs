using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Rundenart „Liga“ mit Hin- und Rückrunde (#560), an der Vorlage „Bundesliga“: 18 Teams aus der
/// Ligapyramide, eine Saison simuliert. Geprüft werden Invarianten, keine festen Werte.</summary>
public sealed class LigaTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string VORLAGE = "Bundesliga";
    private const string VARIANTE = "18 Teams";
    private const string SAISON = "Saison";

    private const int TEAMS = 18;
    private const int SPIELTAGE = 2 * (TEAMS - 1);
    private const int SPIELE = TEAMS * (TEAMS - 1);

    [Fact]
    public void Saison_JederSpieltZweimalGegenJeden_MitWechselndemHeimrecht()
    {
        var turnier = app.Hauptfenster.TurnierAusVorlageAnlegen(VORLAGE, VARIANTE);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(SAISON);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();

        var spiele = turnier.RundeSimulieren(SAISON, SPIELE);
        var statistik = turnier.Statistik();
        statistik.AuswertungWaehlen("Gesamt");
        var tabelle = statistik.Tabelle();

        var teams = spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).ToHashSet();
        spiele.Should().HaveCount(SPIELE).And.OnlyContain(s => s.IstGespielt);
        teams.Should().HaveCount(TEAMS);
        JedesPaarHinUndZurueck(spiele);
        SpielplanInvarianten.JedesTeamEinmalJeSpieltag(spiele, teams, SPIELTAGE);
        TabellenPruefung.Stimmig(tabelle, teams.ToDictionary(t => t, _ => SPIELTAGE));
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    /// <summary>18 · 17 verschiedene Paare (Heim, Auswärts): Jedes Team hat gegen jedes andere genau ein Heim- und
    /// ein Auswärtsspiel.</summary>
    private static void JedesPaarHinUndZurueck(IReadOnlyList<SpielZeile> spiele) =>
        spiele.Select(s => (s.Heim, s.Auswaerts)).Should().OnlyHaveUniqueItems(
            "jedes Paar spielt einmal mit jedem Heimrecht – bei {0} Spielen also jeder gegen jeden hin und zurück", SPIELE);
}
