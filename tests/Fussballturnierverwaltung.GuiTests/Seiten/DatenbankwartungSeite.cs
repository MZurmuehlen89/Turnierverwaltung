using FlaUI.Core.AutomationElements;
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

    public string Datenbank => Wait.UntilFound(
        () => _fenster.TextOf("Datenbankwartung.Datenbank") is { Length: > 0 } text ? text : null,
        Wait.DefaultTimeout,
        "Datenbankname im Status");

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
}
