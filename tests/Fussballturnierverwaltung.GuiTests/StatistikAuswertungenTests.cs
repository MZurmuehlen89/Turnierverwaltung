using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Die Auswertungen der Statistik passen zueinander und zum Spielplan (#534), an drei gespielten Spieltagen
/// der Vorlage „Bundesliga“ — eine Liga hat echtes Heimrecht, Heim und Auswärts sind also verfügbar. Geprüft werden
/// Invarianten, keine festen Werte: Heim + Auswärts ergibt Gesamt, die Form über höchstens fünf Spiele deckt hier
/// alle Spiele ab, „Tore und Zu-Null“ folgt aus den Ergebnissen, der Tabellenverlauf zeigt die Tabelle des Spielplans
/// und nach dem 1. Spieltag eine Reihenfolge nach den Punkten dieses Spieltags. Der Verlauf selbst ist eine Kurve
/// ohne UIA-Knoten; seine Geometrie gehört in Unit-Tests.</summary>
public sealed class StatistikAuswertungenTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string SAISON = "Saison";
    private const int TEAMS = 18;
    private const int SPIELTAGE = 3;
    private const int SPIELE_JE_SPIELTAG = TEAMS / 2;

    private static readonly string[] _bilanz = ["Sp", "S", "U", "N", "Pkt"];

    [Fact]
    public void Auswertungen_PassenZueinanderUndZumSpielplan()
    {
        var turnier = app.Hauptfenster.TurnierAusVorlageAnlegen("Bundesliga", "18 Teams");
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(SAISON);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(SAISON);
        for (int spieltag = 1; spieltag <= SPIELTAGE; spieltag++)
        {
            spielplan.SpieltagWaehlen(spieltag);
            spielplan.Simulieren(Spielmenge.Spieltag, SPIELE_JE_SPIELTAG);
        }
        var ersterSpieltag = spielplan.SpieltagWaehlen(1);
        var spiele = spielplan.SpieleJeSpieltag(SPIELTAGE);
        var spielplanTabelle = spielplan.Tabelle();

        var statistik = turnier.Statistik();
        var gesamt = NachMannschaft(statistik.Tabelle("Gesamt"));
        var heim = NachMannschaft(statistik.Tabelle("Heim"));
        var auswaerts = NachMannschaft(statistik.Tabelle("Auswärts"));
        var form = NachMannschaft(statistik.Tabelle("Form"));
        var halbzeit = statistik.Tabelle("Halbzeit");
        var tore = NachMannschaft(statistik.Tabelle("Tore und Zu-Null"));
        var serien = statistik.Tabelle("Serien");
        var fairplay = statistik.Tabelle("Fairplay");
        var verbaende = statistik.Tabelle("Verbände");
        statistik.AuswertungWaehlen("Tabellenverlauf");
        var verlaufAktuell = statistik.RundentabelleNach("aktueller Stand");
        var verlaufNachErstem = statistik.RundentabelleNach("nach dem 1. Spieltag");

        spiele.Should().HaveCount(SPIELTAGE * SPIELE_JE_SPIELTAG).And.OnlyContain(s => s.IstGespielt);
        var teams = spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).ToHashSet();
        TabellenPruefung.Stimmig([.. gesamt.Values], teams.ToDictionary(t => t, _ => SPIELTAGE));
        HeimUndAuswaertsErgebenGesamt(gesamt, heim, auswaerts);
        foreach (var (team, zeile) in gesamt)
            form[team].Zahl("Pkt").Should().Be(zeile.Zahl("Pkt"), "bei {0} Spielen zählt die Form alle – {1}", SPIELTAGE, team);
        ToreUndZuNullStimmig(tore, gesamt, spiele);
        HalbzeitUndRauchtests(halbzeit, serien, fairplay, verbaende, teams);
        verlaufAktuell.Should().Equal(spielplanTabelle, "der Tabellenverlauf zeigt aktuell die Tabelle des Spielplans");
        ReihenfolgeNachPunkten(verlaufNachErstem, ersterSpieltag);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static Dictionary<string, TabellenZeile> NachMannschaft(IReadOnlyList<TabellenZeile> tabelle) =>
        tabelle.ToDictionary(z => z.Mannschaft);

    /// <summary>Je Mannschaft Heim + Auswärts = Gesamt für Spiele, Siege, Unentschieden, Niederlagen, Punkte und Tore.
    /// Wer noch kein Heim- bzw. Auswärtsspiel hatte, fehlt in der Teiltabelle und zählt dort 0.</summary>
    private static void HeimUndAuswaertsErgebenGesamt(Dictionary<string, TabellenZeile> gesamt,
        Dictionary<string, TabellenZeile> heim, Dictionary<string, TabellenZeile> auswaerts)
    {
        heim.Keys.Concat(auswaerts.Keys).Should().OnlyContain(team => gesamt.ContainsKey(team));
        foreach (var (team, zeile) in gesamt)
        {
            foreach (string spalte in _bilanz)
                (Zahl(heim, team, spalte) + Zahl(auswaerts, team, spalte)).Should().Be(zeile.Zahl(spalte),
                    "Heim + Auswärts = Gesamt bei {0} von {1}", spalte, team);
            var (heimTore, heimGegen) = Tore(heim, team);
            var (auswTore, auswGegen) = Tore(auswaerts, team);
            (heimTore + auswTore, heimGegen + auswGegen).Should().Be(Tore(gesamt, team), "Tore von {0}", team);
        }
    }

    /// <summary>Tore und Gegentore wie in der Gesamttabelle, „Zu Null“ und „Ohne Tor“ aus den Ergebnissen, die Summe
    /// aller Tore wie im Spielplan.</summary>
    private static void ToreUndZuNullStimmig(Dictionary<string, TabellenZeile> tore,
        Dictionary<string, TabellenZeile> gesamt, IReadOnlyList<SpielZeile> spiele)
    {
        var ergebnisse = spiele.Select(s => (s.Heim, s.Auswaerts, Tore: Spielstand.Tore(s.Ergebnis)!.Value)).ToList();
        tore.Keys.Should().BeEquivalentTo(gesamt.Keys);
        foreach (var (team, zeile) in tore)
        {
            var gegentoreJeSpiel = ergebnisse.Where(e => e.Heim == team || e.Auswaerts == team)
                .Select(e => e.Heim == team ? (Eigene: e.Tore.Heim, Gegner: e.Tore.Auswaerts) : (Eigene: e.Tore.Auswaerts, Gegner: e.Tore.Heim))
                .ToList();
            (zeile.Zahl("Tore"), zeile.Zahl("Gegentore")).Should().Be(Tore(gesamt, team), "Tore von {0}", team);
            zeile.Zahl("Zu Null").Should().Be(gegentoreJeSpiel.Count(t => t.Gegner == 0), "Zu-Null-Spiele von {0}", team);
            zeile.Zahl("Ohne Tor").Should().Be(gegentoreJeSpiel.Count(t => t.Eigene == 0), "Spiele ohne Tor von {0}", team);
        }
        tore.Values.Sum(z => z.Zahl("Tore")).Should().Be(ergebnisse.Sum(e => e.Tore.Heim + e.Tore.Auswaerts),
            "alle Tore des Spielplans");
    }

    private static void HalbzeitUndRauchtests(IReadOnlyList<TabellenZeile> halbzeit, IReadOnlyList<TabellenZeile> serien,
        IReadOnlyList<TabellenZeile> fairplay, IReadOnlyList<TabellenZeile> verbaende, HashSet<string> teams)
    {
        halbzeit.Select(z => z.Mannschaft).Should().BeEquivalentTo(teams);
        halbzeit.Should().OnlyContain(z => z.Zahl("Sp") == SPIELTAGE && z.Zahl("S") + z.Zahl("U") + z.Zahl("N") == SPIELTAGE);
        serien.Select(z => z.Mannschaft).Should().BeEquivalentTo(teams);
        fairplay.Select(z => z.Mannschaft).Should().BeEquivalentTo(teams);
        fairplay.Should().OnlyContain(z => z.Zahl("Sp") == SPIELTAGE);
        verbaende.Should().NotBeEmpty().And.OnlyContain(z => z.Zahl("Teams") <= TEAMS
            && z.Zahl("S") + z.Zahl("U") + z.Zahl("N") == z.Zahl("Sp"));
    }

    /// <summary>Die Tabelle nach dem 1. Spieltag steht nach den Punkten dieses Spieltags (Sieger vor Unentschieden vor
    /// Verlierern); innerhalb gleicher Punkte entscheiden Tore, das prüft dieser Test nicht.</summary>
    private static void ReihenfolgeNachPunkten(IReadOnlyList<string> tabelle, IReadOnlyList<SpielZeile> ersterSpieltag)
    {
        var punkte = ersterSpieltag.SelectMany(s => new[]
        {
            (Team: s.Heim, Punkte: s.Sieger == s.Heim ? 3 : s.Sieger is null ? 1 : 0),
            (Team: s.Auswaerts, Punkte: s.Sieger == s.Auswaerts ? 3 : s.Sieger is null ? 1 : 0),
        }).ToDictionary(p => p.Team, p => p.Punkte);
        tabelle.Should().BeEquivalentTo(punkte.Keys);
        tabelle.Select(team => punkte[team]).Should().BeInAscendingOrder("nach dem 1. Spieltag: {0}", string.Join(", ", tabelle));
    }

    private static int Zahl(Dictionary<string, TabellenZeile> tabelle, string team, string spalte) =>
        tabelle.TryGetValue(team, out var zeile) ? zeile.Zahl(spalte) : 0;

    private static (int Tore, int Gegentore) Tore(Dictionary<string, TabellenZeile> tabelle, string team) =>
        tabelle.TryGetValue(team, out var zeile)
            ? Spielstand.Tore(zeile.Werte["Tore"]) ?? throw new FormatException($"Tore von {team}: {zeile.Werte["Tore"]}")
            : (0, 0);
}
