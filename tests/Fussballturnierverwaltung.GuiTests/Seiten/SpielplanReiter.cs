using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Reiter „Spielplan &amp; Baum“: Runde wählen, simulieren, Spiele und Fortschritt lesen.</summary>
public sealed class SpielplanReiter(Window fenster, Hauptfenster hauptfenster)
{
    private const string ALLE_SPIELTAGE = "Alle Spieltage";

    private static readonly TimeSpan _simulationsZeit = TimeSpan.FromSeconds(60);

    /// <summary>(gespielt, gesamt) über alle Runden, aus „Runde x von y · a / b Spiele gespielt“. Während die Seite nach
    /// einer Aktion neu lädt, ist die Anzeige kurz leer – gelesen wird, bis sie wieder dasteht.</summary>
    public (int Gespielt, int Gesamt) Fortschritt =>
        Wait.UntilHasValue(() => AktuellerFortschritt, Wait.DefaultTimeout, "Fortschritt lesbar");

    private (int Gespielt, int Gesamt)? AktuellerFortschritt =>
        Spielstand.Fortschritt(fenster.TextOf("Spielplan.Fortschritt"));

    public void RundeWaehlen(string runde)
    {
        fenster.FindById("Spielplan.Runden").FindByName(runde, FlaUI.Core.Definitions.ControlType.ListItem).Activate();
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Simuliert die gewählte Runde und wartet, bis ihre <paramref name="spieleInDerRunde"/> Spiele im
    /// Fortschritt angekommen sind.</summary>
    public void RundeSimulieren(int spieleInDerRunde)
    {
        int erwartet = Fortschritt.Gespielt + spieleInDerRunde;
        fenster.InvokeMenuItem("Befehlsleiste.Simulieren", "Befehlsleiste.SimulierenRunde");
        Wait.Until(() => AktuellerFortschritt?.Gespielt == erwartet, _simulationsZeit, $"{erwartet} Spiele gespielt");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Die Spiele der gewählten Runde, auch die von einer virtualisierenden Liste noch nicht erzeugten. Runden
    /// mit mehreren Spieltagen zeigen sonst nur einen Spieltag, darum zuerst „Alle Spieltage“.</summary>
    public IReadOnlyList<SpielZeile> Spiele()
    {
        if (fenster.TryFindById("Spielplan.Spieltag") is { } spieltag)
        {
            spieltag.AsComboBox().SelectItem(ALLE_SPIELTAGE);
            hauptfenster.WarteBisRuhig();
        }
        return SichtbareSpiele();
    }

    /// <summary>Die Spiele der gewählten Runde, Spieltag für Spieltag über die Auswahl „n. Spieltag“ gelesen. Für
    /// Runden mit vielen Spielen statt <see cref="Spiele"/>: Bei einigen hundert Zeilen recycelt die Liste ihre
    /// Zeilen, und eine gerade erst wiederverwendete Zeile zeigt kurz noch Teile des vorigen Spiels (#560: 306
    /// Bundesliga-Spiele, gelesen 27 Paare doppelt, die Datenbank korrekt). Ein Spieltag hat wenige Spiele.</summary>
    public IReadOnlyList<SpielZeile> SpieleJeSpieltag(int spieltage)
    {
        var auswahl = fenster.FindById("Spielplan.Spieltag").AsComboBox();
        var spiele = new List<SpielZeile>();
        for (int spieltag = 1; spieltag <= spieltage; spieltag++)
        {
            auswahl.SelectItem($"{spieltag}. Spieltag");
            hauptfenster.WarteBisRuhig();
            spiele.AddRange(SichtbareSpiele());
        }
        return spiele;
    }

    private IReadOnlyList<SpielZeile> SichtbareSpiele() =>
        fenster.FindById("Spielplan.Spiele").ReadAllItems(zeile =>
            zeile.TryFindById("Spiel.Heim") is null
                ? null
                : new SpielZeile(
                    zeile.TextOf("Spiel.Heim"),
                    zeile.TextOf("Spiel.Ergebnis"),
                    zeile.TextOf("Spiel.ErgebnisZusatz"),
                    zeile.TextOf("Spiel.Auswaerts"),
                    zeile.TextOf("Spiel.Status"),
                    zeile.TryFindById("Spiel.Kennung")?.NameOrEmpty() ?? string.Empty));

    /// <summary>Die Tabelle einer Runde ohne Gruppen (Liga, Schweizer System), wie sie neben dem Spielplan steht:
    /// die Mannschaften in Tabellenreihenfolge.</summary>
    public IReadOnlyList<string> Tabelle() =>
        [.. fenster.FindAllById("Tabelle.Mannschaft").Select(m => m.NameOrEmpty())];

    // ── Schweizer System ──────────────────────────────────────────────────────

    /// <summary>„Spieltag x von y“ in der Kopfzeile einer Runde „Schweizer System“.</summary>
    public string SchweizerFortschritt => fenster.TextOf("Spielplan.SchweizerFortschritt");

    public bool NaechsterSpieltagMoeglich => fenster.FindById("Spielplan.NaechsterSpieltag").IsEnabled;

    /// <summary>Erzeugt den nächsten Spieltag einer Runde „Schweizer System“ und wartet, bis der Fortschritt ihn
    /// zeigt.</summary>
    public void NaechstenSpieltagGenerieren()
    {
        string vorher = SchweizerFortschritt;
        fenster.ActivateById("Spielplan.NaechsterSpieltag");
        Wait.Until(() => SchweizerFortschritt != vorher, Wait.DefaultTimeout, "nächster Spieltag generiert");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Die Gruppentabellen der gewählten Runde, wie sie neben dem Spielplan stehen: Kopf („Gruppe 1“) →
    /// Mannschaften in Tabellenreihenfolge. Gelesen wird in Baumreihenfolge; Rangfolgen über Gruppen hinweg (beste
    /// Dritte) würden der letzten Gruppe zugeschlagen, darum nur für Runden ohne solche Rangfolge.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> GruppenTabellen()
    {
        var tabellen = new Dictionary<string, List<string>>();
        List<string>? aktuelle = null;
        foreach (var element in fenster.FindAllDescendants(cf =>
                     cf.ByAutomationId("Tabelle.Gruppe").Or(cf.ByAutomationId("Tabelle.Mannschaft"))))
        {
            if (element.AutomationId == "Tabelle.Gruppe")
                tabellen[element.NameOrEmpty()] = aktuelle = [];
            else
                aktuelle?.Add(element.NameOrEmpty());
        }
        return tabellen.ToDictionary(t => t.Key, t => (IReadOnlyList<string>)t.Value);
    }

    // ── Ergebnis von Hand ─────────────────────────────────────────────────────

    /// <summary>Trägt das Ergebnis des Spiels <paramref name="heim"/> – <paramref name="auswaerts"/> der gewählten
    /// Runde über das Ergebnisformular ein. <paramref name="nachVerlaengerung"/> ist der Stand nach Verlängerung
    /// einschließlich der regulären Tore; Verlängerung und Elfmeterschießen blendet das Formular erst ein, wenn sie
    /// fällig sind. Wartet, bis der Spielplan das Endergebnis zeigt.</summary>
    public void ErgebnisEintragen(string heim, string auswaerts, (int Heim, int Auswaerts) regulaer,
        (int Heim, int Auswaerts)? nachVerlaengerung = null, (int Heim, int Auswaerts)? elfmeter = null)
    {
        SpielWaehlen(heim, auswaerts);
        ToreSetzen("Tore", regulaer);
        if (nachVerlaengerung is { } verlaengerung) ToreSetzen("ToreVerlaengerung", verlaengerung);
        if (elfmeter is { } elfmeterTore) ToreSetzen("Elfmetertore", elfmeterTore);
        fenster.ActivateById("Befehlsleiste.Speichern");

        var (endeHeim, endeAuswaerts) = nachVerlaengerung ?? regulaer;
        // Während die Liste nach dem Speichern neu lädt, kann eine Zeile kurz doppelt erscheinen – darum genau ein
        // Treffer mit dem neuen Ergebnis abwarten, statt beim ersten Lesen zu scheitern.
        Wait.Until(() => Spiele().Where(s => s.Heim == heim && s.Auswaerts == auswaerts).Select(s => s.Ergebnis).ToList()
                is [var ergebnis] && ergebnis == $"{endeHeim}:{endeAuswaerts}", Wait.DefaultTimeout,
            $"Ergebnis {endeHeim}:{endeAuswaerts} für {heim} – {auswaerts} im Spielplan");
        hauptfenster.WarteBisRuhig();
    }

    private void SpielWaehlen(string heim, string auswaerts)
    {
        fenster.FindById("Spielplan.Spiele")
            .ReadAllItems(z => z.TryFindById("Spiel.Heim") is not null && z.TextOf("Spiel.Heim") == heim
                               && z.TextOf("Spiel.Auswaerts") == auswaerts ? z : null)
            .Single()
            .Activate();
        Wait.Until(() => fenster.TryFindById("Ergebnis.HeimTore") is not null, Wait.DefaultTimeout,
            $"Ergebnisformular für {heim} – {auswaerts}");
    }

    /// <summary>Setzt ein Torpaar des Formulars, die Felder „Ergebnis.Heim{feld}“ und „Ergebnis.Auswaerts{feld}“.
    /// FindById wartet, bis ein gerade erst eingeblendetes Feld (Verlängerung, Elfmeter) im Baum steht.</summary>
    private void ToreSetzen(string feld, (int Heim, int Auswaerts) tore)
    {
        fenster.FindById($"Ergebnis.Heim{feld}").Patterns.Value.Pattern.SetValue(tore.Heim.ToString());
        fenster.FindById($"Ergebnis.Auswaerts{feld}").Patterns.Value.Pattern.SetValue(tore.Auswaerts.ToString());
    }
}
