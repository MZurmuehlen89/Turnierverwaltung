using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Reiter „Auslosung“ der Turnierdurchführung: Auslosung anlegen, Töpfe befüllen und auslosen –
/// automatisch, im TV-Modus oder von Hand.</summary>
public sealed class AuslosungReiter(Window fenster, Hauptfenster hauptfenster)
{
    public const string ABGESCHLOSSEN = "Abgeschlossen";
    public const string VORBEREITUNG = "Vorbereitung";

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

        // Ladezeit statt Standard-Timeout: Auf dem CI-Runner brauchten Seitenliste und Ranglisten zwischen Speichern
        // und Detail schon über 10 s (#631, Fall 1). Nicht erst WarteBisRuhig: Das kehrt zurück, bevor der Vorgang
        // „Auslosung anlegen“ beginnt, solange Speichern noch auf den DbContext-Schutz wartet
        Wait.Until(() => Runde == runde && Status == VORBEREITUNG, Hauptfenster.Ladezeit,
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
        WarteBisAlleZugewiesen();
    }

    /// <summary>Wartet, bis alle Teams in den Töpfen liegen — etwa nach dem Anlegen einer Runde mit Standard-Rangliste,
    /// die die App selbst befüllt (#349).</summary>
    public void WarteBisAlleZugewiesen() =>
        Wait.Until(() => fenster.TextOf("Auslosung.Zuweisung").StartsWith('✓'), Wait.DefaultTimeout,
            "alle Teams den Töpfen zugewiesen");

    // ── Teilnehmerpool (#576) ─────────────────────────────────────────────────

    /// <summary>Öffnet den Reiter „Teilnehmerpool“; den gibt es nur in der ersten Runde eines Turniers mit Quoten.</summary>
    public void TeilnehmerpoolOeffnen()
    {
        fenster.ActivateById("Auslosung.ReiterTeilnehmerpool");
        Wait.UntilFound(() => fenster.TryFindById("Teilnehmerpool.Weitere"), Wait.DefaultTimeout, "Reiter Teilnehmerpool");
        hauptfenster.WarteBisRuhig();
    }

    public void LostoepfeOeffnen()
    {
        fenster.ActivateById("Auslosung.ReiterLostoepfe");
        Wait.UntilFound(() => fenster.TryFindById("Auslosung.Verfuegbar"), Wait.DefaultTimeout, "Reiter Lostöpfe");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Was dem Pool zu seinen Quoten fehlt, durch „ · “ getrennt; leer, wenn er sie erfüllt.</summary>
    public string PoolMaengel => fenster.TryFindById("Teilnehmerpool.Maengel")?.NameOrEmpty() ?? string.Empty;

    /// <summary>Wie oft die Seite gerade aufzählt, was dem Pool fehlt — seit #593 höchstens einmal, in beiden Reitern.</summary>
    public int PoolMeldungen => fenster.FindAllById("Teilnehmerpool.Maengel").Count;

    /// <summary>Hinweis, dass der Pool seine Quoten nicht erfüllt (seit #593 in beiden Reitern); leer, wenn keiner dasteht.</summary>
    public string PoolHinweis => fenster.TryFindById("Auslosung.PoolHinweis")?.NameOrEmpty() ?? string.Empty;

    /// <summary>Der Wert der Quote <paramref name="name"/> in der Quotenliste („Deutschland“, „Freie Plätze“, „Gesamt“), etwa „4 / 4“.</summary>
    public string Quote(string name) => QuoteFinden(name).TextOf("Teilnehmerpool.KachelWert");

    /// <summary>Klickt die Quote <paramref name="name"/> (Land oder Verband) an.</summary>
    public void QuoteAnklicken(string name)
    {
        QuoteFinden(name).Activate();
        hauptfenster.WarteBisRuhig();
    }

    private AutomationElement QuoteFinden(string name) =>
        Wait.UntilFound(
            () => fenster.FindAllById("Auslosung.Quote").FirstOrDefault(q => q.NameOrEmpty() == name),
            Wait.DefaultTimeout, $"Quote '{name}'");

    /// <summary>Das im Länderfilter des Pools gewählte Land.</summary>
    public string LandFilter =>
        fenster.FindById("Teilnehmerpool.LandFilter").AsComboBox().SelectedItem?.DisplayedTexts().FirstOrDefault() ?? string.Empty;

    /// <summary>Die ersten <paramref name="anzahl"/> Teams von „Weitere verfügbar“ — alle zu lesen ist bei mehreren hundert
    /// Einträgen nicht verlässlich (#569).</summary>
    public IReadOnlyList<string> WeitereAnfang(int anzahl) =>
        fenster.FindById("Teilnehmerpool.Weitere").ReadFirstItems(anzahl, e => e.TextOf("Teilnehmerpool.TeilnehmerName"));

    /// <summary>Die sichtbaren Teams des Pools. Die Liste ist nach Land gruppiert und virtualisiert — verlässlich nur, wenn
    /// ein Filter sie kurz hält.</summary>
    public IReadOnlyList<string> PoolTeams() =>
        [.. PoolEintraege().Select(e => e.TextOf("Teilnehmerpool.TeilnehmerName"))];

    /// <summary>Nimmt das erste sichtbare Team per Doppelklick aus dem Pool und liefert seinen Namen.</summary>
    public string ErstesAusPoolNehmen()
    {
        var eintrag = PoolEintraege().First();
        string team = eintrag.TextOf("Teilnehmerpool.TeilnehmerName");
        Doppelklicken(eintrag);
        Wait.Until(() => !PoolTeams().Contains(team), Wait.DefaultTimeout, $"'{team}' aus dem Pool genommen");
        hauptfenster.WarteBisRuhig();
        return team;
    }

    /// <summary>Nimmt das erste sichtbare Team von „Weitere verfügbar“ per Doppelklick in den Pool und liefert seinen Namen.</summary>
    public string ErstesInPoolAufnehmen()
    {
        var eintrag = fenster.FindById("Teilnehmerpool.Weitere").FindItem(_ => true)
            ?? throw new InvalidOperationException("„Weitere verfügbar“ ist leer.");
        string team = eintrag.TextOf("Teilnehmerpool.TeilnehmerName");
        Doppelklicken(eintrag);
        Wait.Until(() => PoolTeams().Contains(team), Wait.DefaultTimeout, $"'{team}' in den Pool aufgenommen");
        hauptfenster.WarteBisRuhig();
        return team;
    }

    private IReadOnlyList<AutomationElement> PoolEintraege() =>
        fenster.FindById("Teilnehmerpool.Pool").FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));

    // Der Doppelklick liest das gewählte Element der Liste, darum zuerst wählen (wie in der Vorbelegung, #531). Die
    // Listen des Pools stehen unten auf der Seite und liegen oft außerhalb des sichtbaren Bereichs.
    private static void Doppelklicken(AutomationElement eintrag)
    {
        eintrag.ScrollIntoView();
        eintrag.Patterns.SelectionItem.Pattern.Select();
        eintrag.DoubleClick();
    }

    /// <summary>Wechselt bei geändertem Pool zum Reiter <paramref name="reiter"/> der Turnierdurchführung und lehnt die
    /// Rückfrage „Teilnehmerpool verwerfen“ ab — der Wechsel unterbleibt.</summary>
    public void ReiterwechselAbbrechen(string reiter)
    {
        const string RUECKFRAGE = "Teilnehmerpool verwerfen";
        fenster.SelectTab(reiter);
        Dialog.WaitFor(fenster, RUECKFRAGE).Close();
        Wait.Until(() => Dialog.FindOpen(fenster).All(d => d.Title != RUECKFRAGE), Wait.DefaultTimeout,
            $"Dialog '{RUECKFRAGE}' geschlossen");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Ob „Automatisch befüllen“ im Menü „Töpfe“ gerade möglich ist — öffnet das Menü und schließt es wieder.</summary>
    public bool AutomatischBefuellenMoeglich()
    {
        const string EINTRAG = "Befehlsleiste.ToepfeAutomatischBefuellen";
        hauptfenster.WarteBisRuhig();
        var eintrag = Wait.UntilFound(
            () =>
            {
                var gefunden = fenster.TryFindById(EINTRAG);
                if (gefunden is null && !MenueOffen())
                    fenster.ActivateById("Befehlsleiste.Toepfe");
                return gefunden;
            },
            Wait.DefaultTimeout, "Menüpunkt „Automatisch befüllen“");
        bool moeglich = eintrag.IsEnabled;
        FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Wait.Until(() => !MenueOffen(), Wait.DefaultTimeout, "Menü „Töpfe“ geschlossen");
        return moeglich;
    }

    private bool MenueOffen() => fenster.FindFirstDescendant(cf => cf.ByClassName("MenuFlyout")) is not null;

    public void Durchfuehren()
    {
        fenster.ActivateById("Befehlsleiste.AuslosungDurchfuehren");
        WarteBisAbgeschlossen();
    }

    /// <summary>Setzt die abgeschlossene Auslosung zurück. Eine Rückfrage kommt nur, wenn Daten betroffen sind (#405),
    /// und wird dann bestätigt.</summary>
    public void Zuruecksetzen()
    {
        fenster.ActivateById("Befehlsleiste.AuslosungZuruecksetzen");
        Wait.Until(() =>
        {
            if (Dialog.FindOpen(fenster).FirstOrDefault(d => d.Title == "Auslosung zurücksetzen") is { } rueckfrage)
                rueckfrage.Confirm();
            return Status == VORBEREITUNG;
        }, Wait.DefaultTimeout, $"Auslosung zurück auf '{VORBEREITUNG}'");
        hauptfenster.WarteBisRuhig();
    }

    /// <summary>Schaltet eine Auslosungs-Regel über das Flyout der Befehlsleiste an oder aus. Die Umschalter sind
    /// Checkboxen, deren Änderung nur ein echter Klick meldet (das Toggle-Muster löst kein Click-Ereignis aus),
    /// darum hier mit der Maus. Die Änderung baut die Befehlsleiste neu auf, was das Flyout schließen kann; bleibt es
    /// offen, schließt es Escape.</summary>
    public void RegelSetzen(string regelart, bool aktiv)
    {
        string id = $"Befehlsleiste.Regel.{regelart}";
        // Ein Neuaufbau der Befehlsleiste (etwa nach dem Befüllen der Töpfe) schließt ein gerade geöffnetes Flyout;
        // darum erst auf Ruhe warten und dann öffnen, bis der Umschalter erscheint.
        hauptfenster.WarteBisRuhig();
        var umschalter = Wait.UntilFound(
            () =>
            {
                var gefunden = fenster.TryFindById(id);
                if (gefunden is null && !FlyoutOffen())
                    fenster.ActivateById("Befehlsleiste.AuslosungsRegeln");
                return gefunden;
            },
            Wait.DefaultTimeout,
            $"Umschalter '{id}'").AsCheckBox();
        if (umschalter.IsChecked != aktiv)
        {
            // Das Flyout ist höher als ein kleines Fenster (CI-Runner, seit #470 acht Regeln) — die unteren Regeln liegen
            // dann außerhalb des sichtbaren Bereichs
            umschalter.ScrollIntoView();
            umschalter.Click();
            Wait.Until(() => fenster.TryFindById(id) is not { } offen || offen.AsCheckBox().IsChecked == aktiv,
                Wait.DefaultTimeout, $"Regel '{regelart}' {(aktiv ? "an" : "aus")}");
        }
        if (fenster.TryFindById(id) is not null)
            FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Wait.Until(() => fenster.TryFindById(id) is null, Wait.DefaultTimeout, "Regel-Flyout geschlossen");
        hauptfenster.WarteBisRuhig();
    }

    private bool FlyoutOffen() => fenster.FindFirstDescendant(cf => cf.ByClassName("FlyoutPresenter")) is not null;

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

    /// <summary>Überspringt den Rest der TV-Ziehung und wartet, bis das Ergebnis dasteht.</summary>
    public void RestSofortAnzeigen()
    {
        fenster.ActivateById("Befehlsleiste.ZiehungRestAnzeigen");
        Wait.Until(() => ZiehungsErgebnis().Count > 0, _schrittZeit, "Ergebnis der TV-Ziehung");
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

    /// <summary>Zieht genau einen Schritt (Team, Gegner, Gruppe oder Platz).</summary>
    public void HandEinenSchrittZiehen() => HandSchrittAusfuehren();

    /// <summary>Nimmt den letzten Zug zurück und wartet, bis die Ansage <paramref name="erwarteteAnsage"/> lautet.</summary>
    public void HandZugZuruecknehmen(string erwarteteAnsage)
    {
        fenster.ActivateById("Befehlsleiste.HandZiehungZuruecknehmen");
        Wait.Until(() => HandAnsage == erwarteteAnsage, Wait.DefaultTimeout, $"Ansage „{erwarteteAnsage}“");
    }

    /// <summary>Bricht die Ziehung von Hand ab. Nach dem ersten Zug fragt die App nach; <paramref name="bestaetigen"/>
    /// beantwortet diese Rückfrage.</summary>
    public void HandZiehungAbbrechen(bool bestaetigen)
    {
        const string RUECKFRAGE = "Ziehung von Hand verwerfen";
        fenster.ActivateById("Befehlsleiste.HandZiehungAbbrechen");
        var dialog = Dialog.WaitFor(fenster, RUECKFRAGE);
        if (bestaetigen) dialog.Confirm();
        else dialog.Close();
        Wait.Until(() => Dialog.FindOpen(fenster).All(d => d.Title != RUECKFRAGE), Wait.DefaultTimeout,
            $"Dialog '{RUECKFRAGE}' geschlossen");
        hauptfenster.WarteBisRuhig();
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
        // Team, Gegner (K.-o., Schweizer System, #627) und Schlüsselzahl (Liga) zieht man aus den Lostöpfen.
        string flaeche = ansage.EndsWith("ziehe die Gruppe", StringComparison.Ordinal) ? "HandZiehung.Gruppe"
            : ansage.EndsWith("ziehe die Position", StringComparison.Ordinal) ? "HandZiehung.Platz"
            : "HandZiehung.Team";

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
