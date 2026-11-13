using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Ergebnis-Leiste unter der Spielliste (#391): Stände in der Reihenfolge des Spielverlaufs
/// (90 Minuten → nach Verlängerung → Elfmeterschießen bzw. Los), Silver-Goal-Haken und Live-Prüfung. Abschnitte, die
/// nicht fällig sind, stehen nicht im UIA-Baum.</summary>
public sealed class Ergebnisleiste(Window fenster, Hauptfenster hauptfenster)
{
    public const string TORE = "Tore";
    public const string VERLAENGERUNG = "ToreVerlaengerung";
    public const string ELFMETER = "Elfmetertore";

    /// <summary>Ohne gewähltes Spiel zeigt die Leiste nur den Hinweis zum Auswählen.</summary>
    public bool ZeigtAuswahlHinweis => fenster.TryFindById("Ergebnis.Hinweis") is not null;

    public bool ZeigtEingabe => fenster.TryFindById("Ergebnis.HeimTore") is not null;

    public bool ZeigtPlatzhalterHinweis => fenster.TryFindById("Ergebnis.Platzhalter") is not null;

    /// <summary>Ob die Zeile des Abschnitts <paramref name="feld"/> (z. B. <see cref="VERLAENGERUNG"/>) zu sehen ist.</summary>
    public bool Zeigt(string feld) => fenster.TryFindById($"Ergebnis.Heim{feld}") is not null;

    public bool ZeigtSilverGoalHaken => fenster.TryFindById("Ergebnis.SilverGoal") is not null;

    public bool ZeigtLos => fenster.TryFindById("Ergebnis.LosHeim") is not null;

    /// <summary>Die Fehlermeldung der Live-Prüfung, leer ohne Fehler.</summary>
    public string Fehler => fenster.TryFindById("Ergebnis.Fehler")?.NameOrEmpty() ?? string.Empty;

    public bool SpeichernMoeglich => fenster.FindById("Befehlsleiste.Speichern").IsEnabled;

    /// <summary>Setzt ein Torpaar, die Felder „Ergebnis.Heim{feld}“ und „Ergebnis.Auswaerts{feld}“. FindById wartet,
    /// bis ein gerade erst eingeblendetes Feld (Verlängerung, Elfmeter) im Baum steht.</summary>
    public void ToreSetzen(string feld, (int Heim, int Auswaerts) tore)
    {
        fenster.FindById($"Ergebnis.Heim{feld}").Patterns.Value.Pattern.SetValue(tore.Heim.ToString());
        fenster.FindById($"Ergebnis.Auswaerts{feld}").Patterns.Value.Pattern.SetValue(tore.Auswaerts.ToString());
    }

    /// <summary>Wartet, bis die Zeile des Abschnitts <paramref name="feld"/> erscheint bzw. verschwindet.</summary>
    public void WarteAufZeile(string feld, bool sichtbar) =>
        Wait.Until(() => Zeigt(feld) == sichtbar, Wait.DefaultTimeout, $"Zeile {feld} {(sichtbar ? "sichtbar" : "ausgeblendet")}");

    public bool SilverGoalAngehakt => fenster.FindById("Ergebnis.SilverGoal").AsCheckBox().IsChecked == true;

    public void SilverGoalHaken(bool gesetzt) =>
        fenster.FindById("Ergebnis.SilverGoal").AsCheckBox().IsChecked = gesetzt;

    public void LosFielAuf(bool heim) =>
        fenster.FindById(heim ? "Ergebnis.LosHeim" : "Ergebnis.LosAuswaerts").AsRadioButton().IsChecked = true;

    /// <summary>Speichert über die Befehlsleiste und wartet, bis die Seite wieder ruhig ist.</summary>
    public void Speichern()
    {
        Wait.Until(() => SpeichernMoeglich, Wait.DefaultTimeout, "Speichern möglich");
        fenster.ActivateById("Befehlsleiste.Speichern");
        hauptfenster.WarteBisRuhig();
    }
}
