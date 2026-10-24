using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace GuiTest.Framework;

/// <summary>Choosing entries in combo boxes, tabs, menus and lists. Items bound to data objects often report the
/// object's <c>ToString()</c> as their UIA name (a type name), so entries are matched by the texts they display.</summary>
public static class Selection
{
    /// <summary>Selects the combo box entry that displays all <paramref name="texts"/> (exact match each), e.g. a
    /// template shown as "Mini-Turnier" and "8 Teams".</summary>
    public static void SelectItem(this ComboBox comboBox, params string[] texts)
    {
        comboBox.Expand();
        try
        {
            var item = Wait.UntilFound(
                () => comboBox.Items.FirstOrDefault(i => ItemMatcher.Matches(i.NameOrEmpty(), DisplayedTexts(i), texts)),
                Wait.DefaultTimeout,
                $"combo box entry '{string.Join(" | ", texts)}'");
            item.Select();
        }
        finally
        {
            comboBox.Collapse();
        }
    }

    /// <summary>Selects a tab (e.g. a WinUI <c>PivotItem</c>) by its header.</summary>
    public static void SelectTab(this AutomationElement root, string header) =>
        root.FindByName(header, ControlType.TabItem).Activate();

    /// <summary>Opens the flyout of <paramref name="openerId"/> (a button with a menu) and invokes the menu item
    /// <paramref name="itemId"/>. Menu flyouts live in a popup directly below the window, so the item is searched
    /// from the window, not from the button. A flyout that is still closing swallows the next opening, so this
    /// waits until no menu is open and opens again as long as the item does not show up.</summary>
    public static void InvokeMenuItem(this Window window, string openerId, string itemId)
    {
        Wait.Until(() => !IsMenuOpen(window), Wait.DefaultTimeout, "previous menu flyout closed");
        var item = Wait.UntilFound(
            () =>
            {
                var found = window.TryFindById(itemId);
                if (found is null && !IsMenuOpen(window))
                    window.FindById(openerId).Activate();
                return found;
            },
            Wait.DefaultTimeout,
            $"menu item '{itemId}' of '{openerId}'");
        item.Activate();
    }

    private static bool IsMenuOpen(Window window) =>
        window.FindFirstDescendant(cf => cf.ByClassName("MenuFlyout")) is not null;

    /// <summary>Reads every entry of a list, including those a virtualizing list has not created yet: the
    /// ItemContainer pattern enumerates every item, each one is realized and read right away — realizing later
    /// items lets the list recycle earlier ones, whose elements then become invalid. Entries for which
    /// <paramref name="read"/> returns null are skipped.</summary>
    public static IReadOnlyList<T> ReadAllItems<T>(this AutomationElement list, Func<AutomationElement, T?> read)
        where T : class
    {
        if (!list.Patterns.ItemContainer.TryGetPattern(out var container))
            return [.. list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)).Select(read).OfType<T>()];

        var results = new List<T>();
        for (var item = container.FindItemByProperty(null, null, null);
             item is not null;
             item = container.FindItemByProperty(item, null, null))
        {
            if (item.Patterns.VirtualizedItem.TryGetPattern(out var virtualized))
                virtualized.Realize();
            if (read(item) is { } result)
                results.Add(result);
        }
        return results;
    }

    /// <summary>The texts an element displays, in tree order.</summary>
    public static IReadOnlyList<string> DisplayedTexts(this AutomationElement element) =>
        [.. element.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(t => t.NameOrEmpty())];

    /// <summary>The name of the first descendant with <paramref name="automationId"/>, empty if there is none.</summary>
    public static string TextOf(this AutomationElement element, string automationId) =>
        element.TryFindById(automationId)?.NameOrEmpty() ?? string.Empty;

    /// <summary>All descendants with <paramref name="automationId"/>, in tree order — e.g. the rows of a table.</summary>
    public static IReadOnlyList<AutomationElement> FindAllById(this AutomationElement root, string automationId) =>
        root.FindAllDescendants(cf => cf.ByAutomationId(automationId));
}

/// <summary>Decides whether a list or combo box entry is the wanted one: either its UIA name equals the single
/// wanted text, or every wanted text is displayed by the entry (exact match, so "8 Teams" does not hit "48 Teams").</summary>
public static class ItemMatcher
{
    public static bool Matches(string itemName, IReadOnlyCollection<string> displayedTexts, IReadOnlyList<string> wanted)
    {
        if (wanted.Count == 0) return false;
        if (wanted.Count == 1 && itemName == wanted[0]) return true;
        var displayed = displayedTexts.Select(t => t.Trim()).ToHashSet(StringComparer.Ordinal);
        return wanted.All(displayed.Contains);
    }
}
