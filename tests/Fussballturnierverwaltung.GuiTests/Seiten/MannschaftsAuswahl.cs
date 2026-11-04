using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using GuiTest.Framework;
using Keyboard = FlaUI.Core.Input.Keyboard;

namespace Fussballturnierverwaltung.GuiTests.Seiten;

/// <summary>Suchbox für eine Mannschaft (<c>MannschaftsAuswahlBox</c>, #479), zuerst bedient in #535. Die Box nimmt nur
/// Eingaben an, die in ihr selbst getippt werden – ein per Value-Muster gesetzter Text zählt nicht als Eingabe. Darum
/// wie ein Nutzer: hineinklicken, den Namen tippen und den Vorschlag anklicken (Enter nähme den ersten Treffer, und das
/// muss bei ähnlichen Namen nicht der gesuchte sein).</summary>
public sealed class MannschaftsAuswahl(Window fenster, string automationId)
{
    // Vorlagenteil der AutoSuggestBox; liegt in einem Popup direkt unter dem Fenster, nicht unter der Box
    private const string VORSCHLAGSLISTE = "SuggestionsList";

    /// <summary>Was die Box anzeigt – nach einer Wahl der Name der gewählten Mannschaft.</summary>
    public string Text => Eingabefeld().Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;

    public void Waehlen(string mannschaft)
    {
        Eingabefeld().Click();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(mannschaft);

        var vorschlag = Wait.UntilFound(
            () => fenster.FindFirstDescendant(cf => cf.ByAutomationId(VORSCHLAGSLISTE))
                ?.FindItem(e => e.NameOrEmpty() == mannschaft),
            Wait.DefaultTimeout,
            $"Vorschlag '{mannschaft}' in '{automationId}'");
        vorschlag.ScrollIntoView();
        vorschlag.Click();
    }

    private AutomationElement Eingabefeld() =>
        fenster.FindById(automationId).FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
        ?? throw new InvalidOperationException($"'{automationId}' hat kein Eingabefeld.");
}
