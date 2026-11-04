using FlaUI.Core.AutomationElements;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Die Markierung eines fehlerhaften Felds (#595), wie die Tests sie lesen: Der Fehlertext steht im
/// UIA-HelpText des Felds (Rahmen und Tooltip sieht nur das Auge). Die …Abwarten-Methoden warten bis zum erwarteten
/// Zustand und liefern dann den tatsächlichen.</summary>
public static class Feldmarkierung
{
    /// <summary>Der Fehlertext am Feld, sobald er <paramref name="erwartet"/> ist; leer = unmarkiert.</summary>
    public static string FehlerAbwarten(AutomationElement feld, string erwartet)
    {
        Wait.Within(() => Fehler(feld) == erwartet, Wait.DefaultTimeout);
        return Fehler(feld);
    }

    /// <summary>Ob das Feld den Tastaturfokus hat, sobald es ihn bekommt.</summary>
    public static bool FokusAbwarten(AutomationElement feld)
    {
        Wait.Within(() => HatFokus(feld), Wait.DefaultTimeout);
        return HatFokus(feld);
    }

    private static string Fehler(AutomationElement feld) => feld.Properties.HelpText.ValueOrDefault ?? string.Empty;

    // Bei zusammengesetzten Feldern (NumberBox) hält ein inneres Element den Fokus, nicht das Feld selbst – darum vom
    // fokussierten Element aus nach oben suchen.
    private static bool HatFokus(AutomationElement feld)
    {
        for (var element = feld.Automation.FocusedElement(); element is not null; element = element.Parent)
            if (element.Equals(feld)) return true;
        return false;
    }
}
