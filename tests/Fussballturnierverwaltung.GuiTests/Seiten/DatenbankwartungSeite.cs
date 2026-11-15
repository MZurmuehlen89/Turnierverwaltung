using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Was die Datenbankwartung nach „Prüfen / ergänzen“ zeigt: die Protokollzeilen und eine etwaige
/// Fehlermeldung.</summary>
public sealed record WartungsErgebnis(IReadOnlyList<string> Protokoll, string Fehlermeldung);

/// <summary>Seitenobjekt „Datenbankwartung“ (#538): Migrationsstand ablesen und „Prüfen / ergänzen“ auslösen. Die Seite
/// entsteht bei jedem Aufruf neu (eigenes ViewModel je Navigation), ein Protokoll steht also erst nach der eigenen
/// Prüfung da – daran erkennt <see cref="PruefenUndErgaenzen"/> das Ende.</summary>
public sealed class DatenbankwartungSeite
{
    private const string LETZTE_PRUEFUNG = "Datenbankwartung.LetztePruefung";
    private const string FEHLERMELDUNG = "Datenbankwartung.Fehlermeldung";
    private const string PRUEFEN = "Befehlsleiste.PruefenErgaenzen";

    // Prüfen spielt ggf. Migrationen ein und gleicht den ganzen Seed ab
    private static readonly TimeSpan _pruefzeit = TimeSpan.FromSeconds(60);

    private readonly Window _fenster;

    public DatenbankwartungSeite(Window fenster)
    {
        _fenster = fenster;
        Wait.Until(() => _fenster.TryFindById(LETZTE_PRUEFUNG) is null, Wait.DefaultTimeout,
            "frische Datenbankwartung ohne Protokoll");
    }

    private static readonly string[] _verbindungsfelder =
        ["Verbindung.Server", "Verbindung.Port", "Verbindung.Datenbank", "Verbindung.Benutzer", "Verbindung.Kennwort"];

    /// <summary>Datenbank im Feld der Karte „Verbindung“ (#49).</summary>
    public string Datenbank => Wait.UntilFound(
        () => _fenster.FindById("Verbindung.Datenbank").Patterns.Value.Pattern.Value.ValueOrDefault is { Length: > 0 } text
            ? text : null,
        Wait.DefaultTimeout,
        "Datenbankname in der Verbindungskarte");

    /// <summary>Alle Verbindungsfelder gesperrt und der Hinweis auf die Vorgabe von außen sichtbar (#49).</summary>
    public bool VerbindungGesperrt =>
        _fenster.TryFindById("Verbindung.Vorgabe") is not null
        && _verbindungsfelder.All(id => !_fenster.FindById(id).IsEnabled);

    public bool VerbindungSpeichernMoeglich => _fenster.FindById("Befehlsleiste.VerbindungSpeichern").IsEnabled;

    /// <summary>Etwa „87 eingespielt, keine ausstehend“; nach einer Prüfung lädt die Seite den Stand neu.</summary>
    public string Migrationsstand => Wait.UntilFound(
        () => _fenster.TextOf("Datenbankwartung.Migrationen") is { Length: > 0 } text ? text : null,
        Wait.DefaultTimeout,
        "Migrationsstand im Status");

    /// <summary>Löst die Prüfung aus und liest das Ergebnis, sobald Protokoll oder Fehlermeldung dastehen und der
    /// Befehl wieder freigegeben ist (die Seite sperrt ihn, solange sie prüft).</summary>
    public WartungsErgebnis PruefenUndErgaenzen()
    {
        _fenster.ActivateById(PRUEFEN);
        Wait.Until(
            () => (_fenster.TryFindById(LETZTE_PRUEFUNG) is not null || _fenster.TryFindById(FEHLERMELDUNG) is not null)
                  && _fenster.FindById(PRUEFEN).IsEnabled,
            _pruefzeit,
            "Ergebnis von 'Prüfen / ergänzen'");
        return new WartungsErgebnis(
            [.. _fenster.FindAllById("Datenbankwartung.ProtokollZeile").Select(z => z.NameOrEmpty())],
            _fenster.TextOf(FEHLERMELDUNG));
    }

    // ── Sicherung (#43) ────────────────────────────────

    private const string SICHERN = "Befehlsleiste.Sichern";
    private const string WIEDERHERSTELLEN = "Befehlsleiste.Wiederherstellen";
    private const string SICHERUNG_FEHLER = "Sicherung.Fehlermeldung";
    public const string RUECKFRAGE_WIEDERHERSTELLEN = "Datenbank wiederherstellen?";

    // pg_dump einer frischen Testdatenbank dauert Sekunden, auf dem CI-Runner mehr
    private static readonly TimeSpan _sicherungszeit = TimeSpan.FromSeconds(60);

    /// <summary>Der Hinweis „Noch keine Sicherung“ steht da (nur sichtbar, wenn die Liste leer ist).</summary>
    public bool KeineSicherungAngezeigt => _fenster.TryFindById("Sicherung.KeineSicherung") is not null;

    /// <summary>Dateinamen der Sicherungen, neueste zuerst.</summary>
    public IReadOnlyList<string> Sicherungen =>
        [.. Sicherungsliste.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)).Select(z => z.NameOrEmpty())];

    public string SicherungsFehlermeldung => _fenster.TextOf(SICHERUNG_FEHLER);

    public bool WiederherstellenMoeglich => _fenster.FindById(WIEDERHERSTELLEN).IsEnabled;

    private AutomationElement Sicherungsliste => _fenster.FindById("Sicherung.Liste");

    /// <summary>Sichert und wartet, bis die Liste eine Sicherung mehr zeigt oder ein Fehler dasteht und „Sichern“
    /// wieder freigegeben ist (die Karte sperrt ihre Befehle, solange sie arbeitet).</summary>
    public void Sichern()
    {
        var vorher = Sicherungen.Count;
        _fenster.ActivateById(SICHERN);
        Wait.Until(
            () => (Sicherungen.Count > vorher || _fenster.TryFindById(SICHERUNG_FEHLER) is not null)
                  && _fenster.FindById(SICHERN).IsEnabled,
            _sicherungszeit,
            "Ergebnis von 'Sichern'");
    }

    public void SicherungAuswaehlen(string name) =>
        Wait.UntilFound(() => Sicherungsliste.FindItem(z => z.NameOrEmpty() == name), Wait.DefaultTimeout,
                $"Sicherung '{name}'")
            .Patterns.SelectionItem.Pattern.Select();

    /// <summary>Löst „Wiederherstellen“ aus und bricht die Rückfrage ab.</summary>
    public void WiederherstellenAbbrechen()
    {
        _fenster.ActivateById(WIEDERHERSTELLEN);
        Dialog.WaitFor(_fenster, RUECKFRAGE_WIEDERHERSTELLEN).Close();
        Wait.Until(() => Dialog.FindOpen(_fenster).Count == 0, Wait.DefaultTimeout, "Rückfrage geschlossen");
    }
}
