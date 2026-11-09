using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Rundenart „Schweizer System“ (#561): ein von Hand aufgebautes Turnier mit einer Runde aus 8 Teams und
/// 3 Spieltagen, Spieltag für Spieltag simuliert und generiert. Eigene Klasse und damit eigene App-Sitzung, siehe
/// <see cref="HandTurnier"/>. Geprüft werden Invarianten, keine festen Werte.</summary>
public sealed class SchweizerSystemTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string RUNDE = "Schweizer System";
    private const int TEAMS = 8;
    private const int SPIELTAGE = 3;
    private const int SPIELE_JE_SPIELTAG = TEAMS / 2;

    [Fact]
    public void Spieltage_FolgenDerTabelle_OhneWiederholtePaarung()
    {
        var turnier = TurnierAufbauenUndAuslosen();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(RUNDE);

        // Tabelle vor dem Generieren von Spieltag 2 bzw. 3 = Stand nach dem Vortag
        var tabelleVorSpieltag = new Dictionary<int, IReadOnlyList<string>>();
        for (int spieltag = 1; spieltag <= SPIELTAGE; spieltag++)
        {
            if (spieltag > 1)
            {
                tabelleVorSpieltag[spieltag] = spielplan.Tabelle();
                spielplan.NaechstenSpieltagGenerieren();
            }
            spielplan.SchweizerFortschritt.Should().Be($"Spieltag {spieltag} von {SPIELTAGE}");
            spielplan.RundeSimulieren(SPIELE_JE_SPIELTAG);
        }
        var spiele = spielplan.SpieleJeSpieltag(SPIELTAGE);

        spielplan.Fortschritt.Should().Be((SPIELTAGE * SPIELE_JE_SPIELTAG, SPIELTAGE * SPIELE_JE_SPIELTAG));
        spielplan.NaechsterSpieltagMoeglich.Should().BeFalse("alle {0} Spieltage sind generiert", SPIELTAGE);
        var teams = spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).ToHashSet();
        teams.Should().HaveCount(TEAMS);
        SpielplanInvarianten.JedesTeamEinmalJeSpieltag(spiele, teams, SPIELTAGE);
        KeinePaarungWiederholt(spiele);
        foreach (var (spieltag, tabelle) in tabelleVorSpieltag)
            TabellenfuehrerTrifftBestplatziertenNeuenGegner(spiele, spieltag, tabelle, teams);
    }

    // #627: die Ziehung von Hand paart wie die automatische Auslosung (Team, dann Gegner) – die Paarungen sind die
    // Spiele des ersten Spieltags.
    [Fact]
    public void HandZiehung_PaartTeamUndGegner_ErgibtSpieltagEins()
    {
        var (turnier, auslosung) = TurnierAufbauen("Manuell");

        auslosung.HandZiehungStarten();
        auslosung.HandEinenSchrittZiehen();
        auslosung.HandAnsage.Should().Contain("ziehe den Gegner", "das Schweizer System zieht Paarungen");
        auslosung.HandZiehungDurchspielen();
        auslosung.HandFortschritt.Should().Be($"{TEAMS} von {TEAMS} Teams gezogen");
        auslosung.HandZiehungAbschliessen();

        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(RUNDE);
        spielplan.SchweizerFortschritt.Should().Be($"Spieltag 1 von {SPIELTAGE}");
        var spiele = spielplan.SpieleJeSpieltag(1);
        spiele.Should().HaveCount(SPIELE_JE_SPIELTAG);
        var teams = spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).ToHashSet();
        teams.Should().HaveCount(TEAMS);
        SpielplanInvarianten.JedesTeamEinmalJeSpieltag(spiele, teams, 1);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private TurnierdurchfuehrungSeite TurnierAufbauenUndAuslosen()
    {
        var (turnier, auslosung) = TurnierAufbauen();
        auslosung.Durchfuehren();
        return turnier;
    }

    private (TurnierdurchfuehrungSeite Turnier, AuslosungReiter Auslosung) TurnierAufbauen(string? auslosungsart = null)
    {
        string name = HandTurnier.EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, HandTurnier.REGELWERK);
        var runden = turniere.Runden();
        runden.Anlegen(RUNDE, "Schweizer System", TEAMS, 1);
        runden.SpieltageSetzen(SPIELTAGE);
        runden.Speichern();

        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(RUNDE, auslosungsart);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        return (turnier, auslosung);
    }

    // ── Invarianten ───────────────────────────────────────────────────────────

    private static void KeinePaarungWiederholt(IReadOnlyList<SpielZeile> spiele) =>
        spiele.Select(s => string.CompareOrdinal(s.Heim, s.Auswaerts) < 0 ? (s.Heim, s.Auswaerts) : (s.Auswaerts, s.Heim))
            .Should().OnlyHaveUniqueItems("im Schweizer System trifft kein Paar zweimal aufeinander");

    /// <summary>Die Folgerunde entsteht aus der Tabelle: Der Generator paart den Tabellenführer mit dem
    /// bestplatzierten Team, gegen das er noch nicht gespielt hat – sofern sich der Rest dann noch paaren lässt. Bei
    /// 8 Teams bis Spieltag 3 geht das immer: Je Team gibt es höchstens zwei frühere Gegner, unter den übrigen sechs
    /// bleiben jedem also mindestens drei mögliche Partner, und das reicht für eine vollständige Paarung (Satz von
    /// Dirac). Bewusst nicht die ganze
    /// Paarung nachgebaut: das kopierte die Generatorlogik.</summary>
    private static void TabellenfuehrerTrifftBestplatziertenNeuenGegner(
        IReadOnlyList<SpielZeile> spiele, int spieltag, IReadOnlyList<string> tabelle, IReadOnlySet<string> teams)
    {
        tabelle.Should().BeEquivalentTo(teams, "vor Spieltag {0} stehen alle Teams in der Tabelle", spieltag);
        string fuehrender = tabelle[0];
        var fruehereGegner = spiele.Where(s => Spieltag(s) < spieltag).SelectMany(s => Gegner(s, fuehrender)).ToHashSet();
        string erwartet = tabelle.Skip(1).First(t => !fruehereGegner.Contains(t));

        spiele.Where(s => Spieltag(s) == spieltag).SelectMany(s => Gegner(s, fuehrender)).Should().ContainSingle()
            .Which.Should().Be(erwartet, "der Tabellenführer {0} trifft an Spieltag {1} auf den bestplatzierten neuen Gegner",
                fuehrender, spieltag);
    }

    private static int Spieltag(SpielZeile spiel) => int.Parse(spiel.Kennung["ST ".Length..]);

    private static IEnumerable<string> Gegner(SpielZeile spiel, string team) =>
        spiel.Heim == team ? [spiel.Auswaerts] : spiel.Auswaerts == team ? [spiel.Heim] : [];
}
