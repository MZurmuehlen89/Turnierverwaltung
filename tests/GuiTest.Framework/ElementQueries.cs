using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace GuiTest.Framework;

/// <summary>Finding and operating elements. Lookups wait (see <see cref="Wait"/>) instead of failing on the
/// first miss, because UI Automation trees are built asynchronously.</summary>
public static class ElementQueries
{
    public static AutomationElement FindById(this AutomationElement root, string automationId, TimeSpan? timeout = null) =>
        Wait.UntilFound(
            () => root.TryFindById(automationId),
            timeout ?? Wait.DefaultTimeout,
            $"element with AutomationId '{automationId}'");

    public static AutomationElement? TryFindById(this AutomationElement root, string automationId) =>
        root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    /// <summary>Finds by visible name. The control type is required because names collide easily (a section
    /// heading and a button may both read "Turniere").</summary>
    public static AutomationElement FindByName(
        this AutomationElement root, string name, ControlType controlType, TimeSpan? timeout = null) =>
        Wait.UntilFound(
            () => root.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(controlType))),
            timeout ?? Wait.DefaultTimeout,
            $"{controlType} named '{name}'");

    /// <summary>Triggers the element's primary action through its UIA pattern (no mouse movement): invoke,
    /// select or toggle, in this order. Falls back to a mouse click for elements without such a pattern.</summary>
    public static void Activate(this AutomationElement element)
    {
        var patterns = element.Patterns;
        if (patterns.Invoke.TryGetPattern(out var invoke)) invoke.Invoke();
        else if (patterns.SelectionItem.TryGetPattern(out var selection)) selection.Select();
        else if (patterns.Toggle.TryGetPattern(out var toggle)) toggle.Toggle();
        else element.Click();
    }

    /// <summary>The UIA ItemStatus property, which applications can use to publish state for tests and screen
    /// readers (e.g. busy/ready).</summary>
    public static string ItemStatus(this AutomationElement element) =>
        element.Properties.ItemStatus.ValueOrDefault ?? string.Empty;
}
