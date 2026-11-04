using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using GuiTest.Framework;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Eine Landes-Quoten-Zeile, wie der Turnierkopf sie zeigt.</summary>
public sealed record QuotenZeile(string Land, string Spielklasse, bool SpielklasseWaehlbar, int AbPlatz, string Ergibt);

/// <summary>Aufklappbarer Bereich „Quoten“ im Turnierkopf (#572): die Landes-Quoten-Zeilen „Land | Spielklasse |
/// ab Platz | Anzahl | ergibt“ und der Hinweis auf die freien Plätze. Zeilen werden über ihre Position (ab 0)
/// angesprochen.</summary>
public sealed class QuotenBereich(Window fenster)
{
    private const string LAND = "Turnierkopf.LandesQuoteLand";
    private const string SPIELKLASSE = "Turnierkopf.LandesQuoteSpielklasse";

    public void Aufklappen()
    {
        var expander = fenster.FindById("Turnierkopf.Quoten").Patterns.ExpandCollapse.Pattern;
        if (expander.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
            expander.Expand();
        Wait.Until(() => expander.ExpandCollapseState.Value == ExpandCollapseState.Expanded, Wait.DefaultTimeout,
            "Quoten aufgeklappt");
    }

    public int Zeilen => fenster.FindAllById(LAND).Count;

    public void LandHinzufuegen()
    {
        int vorher = Zeilen;
        fenster.ActivateById("Turnierkopf.LandesQuoteHinzufuegen");
        Wait.Until(() => Zeilen == vorher + 1, Wait.DefaultTimeout, "neue Landes-Quoten-Zeile");
    }

    public void LandWaehlen(int zeile, string land) => Feld(LAND, zeile).AsComboBox().SelectItem(land);

    public void SpielklasseWaehlen(int zeile, string spielklasse) =>
        Feld(SPIELKLASSE, zeile).AsComboBox().SelectItem(spielklasse);

    /// <summary>Das Feld „ab Platz“ der Zeile, etwa für <see cref="Feldmarkierung"/>.</summary>
    public AutomationElement AbPlatzFeld(int zeile) => Feld("Turnierkopf.LandesQuoteAbPlatz", zeile);

    /// <summary>Klappt den Bereich zu (Gegenstück zu <see cref="Aufklappen"/>).</summary>
    public void Zuklappen()
    {
        var expander = fenster.FindById("Turnierkopf.Quoten").Patterns.ExpandCollapse.Pattern;
        if (expander.ExpandCollapseState.Value != ExpandCollapseState.Collapsed)
            expander.Collapse();
        Wait.Until(() => expander.ExpandCollapseState.Value == ExpandCollapseState.Collapsed, Wait.DefaultTimeout,
            "Quoten zugeklappt");
    }

    public void AnzahlSetzen(int zeile, int anzahl) =>
        Feld("Turnierkopf.LandesQuoteAnzahl", zeile).Patterns.RangeValue.Pattern.SetValue(anzahl);

    /// <summary>Ob sich die Anzahl der Zeile ändern lässt — beim Pokalsieger nicht (#583).</summary>
    public bool AnzahlWaehlbar(int zeile) => Feld("Turnierkopf.LandesQuoteAnzahl", zeile).IsEnabled;

    /// <summary>Die Zeile, wie sie gerade dasteht; Auswahllisten über ihren gewählten Eintrag gelesen.</summary>
    public QuotenZeile Zeile(int zeile)
    {
        var spielklasse = Feld(SPIELKLASSE, zeile);
        return new QuotenZeile(
            Gewaehlt(Feld(LAND, zeile)),
            Gewaehlt(spielklasse),
            spielklasse.IsEnabled,
            (int)Feld("Turnierkopf.LandesQuoteAbPlatz", zeile).Patterns.RangeValue.Pattern.Value.Value,
            Feld("Turnierkopf.LandesQuoteErgibt", zeile).NameOrEmpty());
    }

    private static string Gewaehlt(AutomationElement auswahl) =>
        auswahl.AsComboBox().SelectedItem?.DisplayedTexts().FirstOrDefault() ?? string.Empty;

    public string FreiePlaetze => fenster.TextOf("Turnierkopf.LandesFreiePlaetze");

    /// <summary>Die Einträge, die die Spielklassen-Auswahl der Zeile anbietet.</summary>
    public IReadOnlyList<string> SpielklassenAngebot(int zeile) =>
        Angebot(Feld(SPIELKLASSE, zeile).AsComboBox(), _ => true);

    /// <summary>Ob die Länder-Auswahl der Zeile <paramref name="land"/> anbietet.</summary>
    public bool LandAngeboten(int zeile, string land) =>
        Angebot(Feld(LAND, zeile).AsComboBox(), t => t == land).Count > 0;

    private static IReadOnlyList<string> Angebot(ComboBox auswahl, Func<string, bool> filter)
    {
        auswahl.Expand();
        try
        {
            return auswahl.ReadAllItems(i => i.DisplayedTexts().FirstOrDefault(filter));
        }
        finally
        {
            auswahl.Collapse();
        }
    }

    private AutomationElement Feld(string automationId, int zeile)
    {
        var felder = Wait.UntilFound(
            () => fenster.FindAllById(automationId) is { } gefunden && gefunden.Count > zeile ? gefunden : null,
            Wait.DefaultTimeout, $"Zeile {zeile} von '{automationId}'");
        return felder[zeile];
    }
}
