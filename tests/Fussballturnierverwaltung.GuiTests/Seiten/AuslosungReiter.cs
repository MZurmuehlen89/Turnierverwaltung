using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Reiter „Auslosung“ der Turnierdurchführung: Auslosung anlegen, Töpfe befüllen und auslosen –
/// automatisch, im TV-Modus oder von Hand.</summary>
public sealed class AuslosungReiter(Window fenster, Hauptfenster hauptfenster)
{
    public const string ABGESCHLOSSEN = "Abgeschlossen";

    private static readonly TimeSpan _schrittZeit = TimeSpan.FromSeconds(5);

    // Obergrenze gegen eine Endlosschleife, falls die Ziehung nie fertig wird (8 Teams brauchen rund 35 Schritte)
    private const int MAX_ZIEHUNGSSCHRITTE = 200;

    /// <summary>Kopfzeile der gewählten Auslosung: „Name · Runde · Art · Status: …“.</summary>
    public string Kopfzeile => fenster.TextOf("Auslosung.Kopfzeile");

    /// <summary>Teile der Kopfzeile ohne die Trennpunkte: [Name, Runde, Art, "Status: …"].</summary>
    private string[] KopfTeile => Kopfzeile.Split('·', StringSplitOptions.TrimEntries);

    public string Runde => KopfTeile is [_, var runde, ..] ? runde : string.Empty;

    public string Status => KopfTeile is [.., var status] && status.StartsWith("Status: ", StringComparison.Ordinal)
        ? status["Status: ".Length..]
        : string.Empty;

    /// <summary>Legt für <paramref name="runde"/> eine Auslosung an und speichert sie.</summary>
    public void Anlegen(string runde, string? auslosungsart = null)
    {
        fenster.ActivateById("Befehlsleiste.Neu");
        fenster.FindById("Auslosung.Runde").AsComboBox().SelectItem(runde);
        if (auslosungsart is not null)
            fenster.FindById("Auslosung.Art").AsComboBox().SelectItem(auslosungsart);
        fenster.ActivateById("Befehlsleiste.Speichern");

        Wait.Until(() => Runde == runde && Status == "Vorbereitung", Wait.DefaultTimeout,
            $"Auslosung für '{runde}' gespeichert");
        hauptfenster.WarteBisRuhig();
    }

    public void RanglisteWaehlen(string rangliste)
    {
        fenster.FindById("Auslosung.Rangliste").AsComboBox().SelectItem(rangliste);
        hauptfenster.WarteBisRuhig();
    }

    public void ToepfeAutomatischBefuellen()
    {
        fenster.InvokeMenuItem("Befehlsleiste.Toepfe", "Befehlsleiste.ToepfeAutomatischBefuellen");
        Wait.Until(() => fenster.TextOf("Auslosung.Zuweisung").StartsWith('✓'), Wait.DefaultTimeout,
            "alle Teams den Töpfen zugewiesen");
    }

    public void Durchfuehren()
    {
        fenster.ActivateById("Befehlsleiste.AuslosungDurchfuehren");
        WarteBisAbgeschlossen();
    }

    // ── TV-Modus ──────────────────────────────────────────────────────────────

    /// <summary>„Mit TV-Modus manuell“: lost sofort aus und zeigt die Ziehung danach Schritt für Schritt.</summary>
    public void MitTvModusManuellDurchfuehren()
    {
        fenster.InvokeMenuItem("Befehlsleiste.TvModus", "Befehlsleiste.TvModusManuell");
        WarteBisAbgeschlossen();
    }

    /// <summary>Klickt „Weiter“, bis die TV-Ziehung ihr Ergebnis zeigt. Zwischen zwei Schritten ist „Weiter“ kurz
    /// gesperrt, darum wartet jeder Schritt, bis entweder das Ergebnis dasteht oder „Weiter“ wieder aktiv ist.
    /// Liefert die Zahl der Schritte.</summary>
    public int ZiehungBisZumEndeWeiterklicken()
    {
        for (int schritte = 0; schritte < MAX_ZIEHUNGSSCHRITTE; schritte++)
        {
            Wait.Until(() => ZiehungsErgebnis().Count > 0 || WeiterBereit() is not null, _schrittZeit,
                "Ergebnis der TV-Ziehung oder „Weiter“ aktiv");
            if (WeiterBereit() is null) return schritte;
            fenster.ActivateById("Befehlsleiste.ZiehungWeiter", _schrittZeit);
        }
        throw new InvalidOperationException($"TV-Ziehung nach {MAX_ZIEHUNGSSCHRITTE} Schritten nicht beendet.");
    }

    private AutomationElement? WeiterBereit() =>
        ZiehungsErgebnis().Count == 0 && fenster.TryFindById("Befehlsleiste.ZiehungWeiter") is { IsEnabled: true } weiter
            ? weiter
            : null;

    /// <summary>Ergebnis der TV-Ziehung: Gruppe → Teams, in Anzeigereihenfolge.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ZiehungsErgebnis()
    {
        var ergebnis = new Dictionary<string, List<string>>();
        List<string>? aktuelle = null;
        foreach (var element in fenster.FindAllDescendants(cf =>
                     cf.ByAutomationId("Ziehung.ErgebnisGruppe").Or(cf.ByAutomationId("Ziehung.ErgebnisTeam"))))
        {
            if (element.AutomationId == "Ziehung.ErgebnisGruppe")
                ergebnis[element.NameOrEmpty()] = aktuelle = [];
            else
                aktuelle?.Add(element.NameOrEmpty());
        }
        return ergebnis.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value);
    }

    // ── Ziehung von Hand ──────────────────────────────────────────────────────

    public string HandAnsage => fenster.TextOf("HandZiehung.Ansage");

    public string HandFortschritt => fenster.TextOf("HandZiehung.Fortschritt");

    /// <summary>Startet bei einer Auslosung der Art „Manuell“ die Ziehung von Hand.</summary>
    public void HandZiehungStarten()
    {
        fenster.ActivateById("Befehlsleiste.AuslosungDurchfuehren");
        Wait.Until(() => HandAnsage.Length > 0, Wait.DefaultTimeout, "Ansage der Ziehung von Hand");
    }

    /// <summary>Zieht von Hand, bis sich die Auslosung abschließen lässt: je Schritt das erste Team bzw. die erste
    /// Gruppe bzw. den ersten Platz, den die App annimmt (gesperrte Flächen ignoriert sie, die Ansage bleibt dann
    /// gleich). „8 von 8 Teams gezogen“ allein reicht nicht – beim letzten Team steht die Gruppe noch aus. Klickt
    /// über das Invoke-Muster der KlickFlaechen (#522).</summary>
    public int HandZiehungDurchspielen()
    {
        int schritte = 0;
        while (!fenster.FindById("Befehlsleiste.AuslosungAbschliessen").IsEnabled)
        {
            if (++schritte > MAX_ZIEHUNGSSCHRITTE)
                throw new InvalidOperationException($"Ziehung von Hand nach {MAX_ZIEHUNGSSCHRITTE} Schritten nicht beendet.");
            HandSchrittAusfuehren();
        }
        return schritte;
    }

    public void HandZiehungAbschliessen()
    {
        fenster.ActivateById("Befehlsleiste.AuslosungAbschliessen");
        WarteBisAbgeschlossen();
    }

    private void HandSchrittAusfuehren()
    {
        string ansage = HandAnsage;
        string fortschritt = HandFortschritt;
        string flaeche = ansage.StartsWith("Ziehe ein Team", StringComparison.Ordinal) ? "HandZiehung.Team"
            : ansage.Contains("Gruppe", StringComparison.Ordinal) ? "HandZiehung.Gruppe"
            : "HandZiehung.Platz";

        foreach (var kandidat in fenster.FindAllById(flaeche))
        {
            kandidat.Activate();
            if (Wait.Within(() => HandAnsage != ansage || HandFortschritt != fortschritt, TimeSpan.FromMilliseconds(500)))
                return;
        }
        throw new InvalidOperationException($"Kein Element '{flaeche}' angenommen bei der Ansage „{ansage}“.");
    }

    private void WarteBisAbgeschlossen()
    {
        Wait.Until(() => Status == ABGESCHLOSSEN, TimeSpan.FromSeconds(30), $"Auslosung '{ABGESCHLOSSEN}'");
        hauptfenster.WarteBisRuhig();
    }
}
