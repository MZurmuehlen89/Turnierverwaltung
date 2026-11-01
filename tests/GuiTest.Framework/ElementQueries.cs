using System.ComponentModel;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Exceptions;

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
    /// select or toggle, in this order. Falls back to a mouse click for elements without such a pattern. Waits until
    /// the element is enabled first: buttons bound to commands are briefly disabled while their CanExecute is being
    /// re-evaluated, and invoking a disabled element fails (ElementNotEnabledException, seen in #522).</summary>
    public static void Activate(this AutomationElement element)
    {
        Wait.Until(() => element.IsEnabled, Wait.DefaultTimeout, $"'{element.NameOrEmpty()}' enabled");
        PrimaryAction(element);
    }

    /// <summary>Scrolls the element into view so mouse actions find a clickable point — also when the list it lives in is
    /// itself scrolled out of a page: the ScrollItem pattern only scrolls the list, focusing the element lets the UI
    /// framework bring it into view through all scrollable ancestors.</summary>
    public static void ScrollIntoView(this AutomationElement element)
    {
        if (element.Patterns.ScrollItem.TryGetPattern(out var scrollItem))
            scrollItem.ScrollIntoView();
        if (element.TryGetClickablePoint(out _)) return;
        element.Focus();
        Wait.Until(() => element.TryGetClickablePoint(out _), Wait.DefaultTimeout, $"'{element.NameOrEmpty()}' scrolled into view");
    }

    /// <summary>Like <see cref="Activate"/>, but looks the element up afresh on every attempt. Applications replace
    /// elements at any time — a toolbar may rebuild all its buttons shortly after its actions changed (#522) — so an
    /// element found a moment ago can be gone when it is invoked. That surfaces as an unsupported property, a COM
    /// error or a Win32Exception; such an attempt is repeated with a fresh lookup. Prefer this for anything that
    /// has an AutomationId.</summary>
    public static void ActivateById(this AutomationElement root, string automationId, TimeSpan? timeout = null) =>
        Wait.Until(() => TryActivate(root.TryFindById(automationId)), timeout ?? Wait.DefaultTimeout,
            $"'{automationId}' activated");

    private static bool TryActivate(AutomationElement? element)
    {
        if (element is null) return false;
        try
        {
            if (!element.IsEnabled) return false;
            PrimaryAction(element);
            return true;
        }
        catch (Exception e) when (e is PropertyNotSupportedException or Win32Exception or ElementNotEnabledException)
        {
            // Replaced or disabled in the meantime — the next attempt looks it up again
            return false;
        }
    }

    private static void PrimaryAction(AutomationElement element)
    {
        var patterns = element.Patterns;
        if (patterns.Invoke.TryGetPattern(out var invoke)) invoke.Invoke();
        else if (patterns.SelectionItem.TryGetPattern(out var selection)) selection.Select();
        else if (patterns.Toggle.TryGetPattern(out var toggle)) toggle.Toggle();
        else element.Click();
    }

    /// <summary>The element's UIA name, empty if it has none. Prefer this over <c>Name</c>: an element showing an
    /// empty text (a WinUI <c>TextBlock</c> with <c>Text=""</c>) reports its name as not supported, and <c>Name</c>
    /// throws (verified #522).</summary>
    public static string NameOrEmpty(this AutomationElement element) =>
        element.Properties.Name.ValueOrDefault ?? string.Empty;

    /// <summary>The UIA ItemStatus property, which applications can use to publish state for tests and screen
    /// readers (e.g. busy/ready).</summary>
    public static string ItemStatus(this AutomationElement element) =>
        element.Properties.ItemStatus.ValueOrDefault ?? string.Empty;
}
