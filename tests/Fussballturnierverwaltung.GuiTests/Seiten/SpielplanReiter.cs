using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Ebenen der Menüs „Simulieren“ und „Zurücksetzen“ im Spielplan. Gruppe und Spieltag folgen der Auswahl in der
/// Liste (gewähltes Spiel bzw. Spieltag-Auswahl), Auswahl den markierten Spielen; „Auswahl“ gibt es nur zum Simulieren.</summary>
public enum Spielmenge { Runde, Hinrunde, Rueckrunde, Gruppe, Spieltag, Auswahl }

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

    /// <summary>Der Turnierbaum neben dem Spielplan — sichtbar, solange eine K.-o.-Runde gewählt ist.</summary>
    public TurnierbaumAnsicht Turnierbaum() => new(fenster, hauptfenster);

    public void RundeWaehlen(string runde)
    {
        fenster.FindById("Spielplan.Runden").FindByName(runde, FlaUI.Core.Definitions.ControlType.ListItem).Activate();
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Simuliert die gewählte Runde und wartet, bis ihre <paramref name="spieleInDerRunde"/> Spiele im
    /// Fortschritt angekommen sind.</summary>
    public void RundeSimulieren(int spieleInDerRunde) => Simulieren(Spielmenge.Runde, spieleInDerRunde);

    /// <summary>Simuliert die offenen Spiele der <paramref name="menge"/> und wartet, bis <paramref name="neu"/>
    /// weitere Spiele im Fortschritt angekommen sind.</summary>
    public void Simulieren(Spielmenge menge, int neu)
    {
        int erwartet = Fortschritt.Gespielt + neu;
        fenster.InvokeMenuItem("Befehlsleiste.Simulieren", $"Befehlsleiste.Simulieren{menge}");
        Wait.Until(() => AktuellerFortschritt?.Gespielt == erwartet, _simulationsZeit, $"{erwartet} Spiele gespielt");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Setzt die Ergebnisse der <paramref name="menge"/> nach Rückfrage zurück und wartet, bis
    /// <paramref name="geloescht"/> Spiele weniger im Fortschritt stehen.</summary>
    public void Zuruecksetzen(Spielmenge menge, int geloescht)
    {
        int erwartet = Fortschritt.Gespielt - geloescht;
        fenster.InvokeMenuItem("Befehlsleiste.Zuruecksetzen", $"Befehlsleiste.Zuruecksetzen{menge}");
        Dialog.WaitFor(fenster, "Ergebnisse zurücksetzen").Confirm();
        Wait.Until(() => AktuellerFortschritt?.Gespielt == erwartet, _simulationsZeit, $"{erwartet} Spiele gespielt");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Schaltet die Platzhalter-Vorschau (#208) um: Platzhalter zeigen dann in Klammern die Mannschaft, die
    /// nach dem aktuellen Stand der Vorrunde dort stünde.</summary>
    public void PlatzhalterVorschauUmschalten()
    {
        fenster.ActivateById("Befehlsleiste.PlatzhalterVorschau");
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

    /// <summary>Wählt in der Spieltag-Auswahl „n. Spieltag“ und liefert dessen Spiele; der Spieltag bleibt gewählt
    /// (Ebene <see cref="Spielmenge.Spieltag"/>).</summary>
    public IReadOnlyList<SpielZeile> SpieltagWaehlen(int spieltag)
    {
        fenster.FindById("Spielplan.Spieltag").AsComboBox().SelectItem($"{spieltag}. Spieltag");
        hauptfenster.WarteBisRuhig();
        return SichtbareSpiele();
    }

    /// <summary>Markiert genau diese Spiele der Liste (wie Strg + Klick) — Ebene <see cref="Spielmenge.Auswahl"/>.</summary>
    public void SpieleMarkieren(params (string Heim, string Auswaerts)[] spiele)
    {
        var liste = fenster.FindById("Spielplan.Spiele");
        for (int i = 0; i < spiele.Length; i++)
        {
            var (heim, auswaerts) = spiele[i];
            var zeile = liste.FindItem(z => z.TryFindById("Spiel.Heim") is not null && z.TextOf("Spiel.Heim") == heim
                                            && z.TextOf("Spiel.Auswaerts") == auswaerts)
                ?? throw new InvalidOperationException($"{heim} – {auswaerts} steht nicht im Spielplan.");
            var auswahl = zeile.Patterns.SelectionItem.Pattern;
            if (i == 0) auswahl.Select();
            else auswahl.AddToSelection();
        }
        Wait.Until(() => liste.Patterns.Selection.Pattern.Selection.Value.Length == spiele.Length, Wait.DefaultTimeout,
            $"{spiele.Length} Spiele markiert");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Die Spiele der gewählten Runde, Spieltag für Spieltag über die Auswahl „n. Spieltag“ gelesen.</summary>
    public IReadOnlyList<SpielZeile> SpieleJeSpieltag(int spieltage)
    {
        var spiele = new List<SpielZeile>();
        for (int spieltag = 1; spieltag <= spieltage; spieltag++)
            spiele.AddRange(SpieltagWaehlen(spieltag));
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
        var leiste = Leiste();
        leiste.ToreSetzen(Ergebnisleiste.TORE, regulaer);
        if (nachVerlaengerung is { } verlaengerung) leiste.ToreSetzen(Ergebnisleiste.VERLAENGERUNG, verlaengerung);
        if (elfmeter is { } elfmeterTore) leiste.ToreSetzen(Ergebnisleiste.ELFMETER, elfmeterTore);
        fenster.ActivateById("Befehlsleiste.Speichern");

        var (endeHeim, endeAuswaerts) = nachVerlaengerung ?? regulaer;
        // Während die Liste nach dem Speichern neu lädt, kann eine Zeile kurz doppelt erscheinen – darum genau ein
        // Treffer mit dem neuen Ergebnis abwarten, statt beim ersten Lesen zu scheitern.
        Wait.Until(() => Spiele().Where(s => s.Heim == heim && s.Auswaerts == auswaerts).Select(s => s.Ergebnis).ToList()
                is [var ergebnis] && ergebnis == $"{endeHeim}:{endeAuswaerts}", Wait.DefaultTimeout,
            $"Ergebnis {endeHeim}:{endeAuswaerts} für {heim} – {auswaerts} im Spielplan");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Die Ergebnis-Leiste unter der Spielliste (#391).</summary>
    public Ergebnisleiste Leiste() => new(fenster, hauptfenster);

    /// <summary>Wählt das Spiel <paramref name="heim"/> – <paramref name="auswaerts"/> und wartet, bis die Leiste es
    /// zeigt: mit Eingabefeldern, bei einem Spiel mit Platzhaltern (<paramref name="platzhalter"/>) mit dem Hinweis,
    /// dass die Paarung noch nicht feststeht.</summary>
    public void SpielWaehlen(string heim, string auswaerts, bool platzhalter = false)
    {
        fenster.FindById("Spielplan.Spiele")
            .ReadAllItems(z => z.TryFindById("Spiel.Heim") is not null && z.TextOf("Spiel.Heim") == heim
                               && z.TextOf("Spiel.Auswaerts") == auswaerts ? z : null)
            .Single()
            .Activate();
        var leiste = Leiste();
        Wait.Until(() => platzhalter ? leiste.ZeigtPlatzhalterHinweis : leiste.ZeigtEingabe, Wait.DefaultTimeout,
            $"Ergebnisleiste für {heim} – {auswaerts}");
        hauptfenster.WarteBisRuhig();
    }
}
