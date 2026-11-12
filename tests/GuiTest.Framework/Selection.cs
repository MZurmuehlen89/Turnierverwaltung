using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace GuiTest.Framework;

/// <summary>Choosing entries in combo boxes, tabs, menus and lists. Items bound to data objects often report the
/// object's <c>ToString()</c> as their UIA name (a type name), so entries are matched by the texts they display.</summary>
public static class Selection
{
    /// <summary>Selects the combo box entry that displays all <paramref name="texts"/> (exact match each), e.g. a
    /// template shown as "Mini-Turnier" and "8 Teams". A long drop-down virtualizes its entries: one far down the
    /// list is not among the realized items until it has been realized through the ItemContainer pattern — and it
    /// must be selected before any further item is realized, or the list recycles it (#572).</summary>
    public static void SelectItem(this ComboBox comboBox, params string[] texts)
    {
        bool Matches(AutomationElement item) => ItemMatcher.Matches(item.NameOrEmpty(), DisplayedTexts(item), texts);

        comboBox.Expand();
        try
        {
            var item = Wait.UntilFound(
                () => comboBox.Items.FirstOrDefault(Matches) ?? comboBox.FindItem(Matches),
                Wait.DefaultTimeout,
                $"combo box entry '{string.Join(" | ", texts)}'");
            item.Patterns.SelectionItem.Pattern.Select();
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
        Wait.UntilFound(
            () =>
            {
                var found = window.TryFindById(itemId);
                if (found is null && !IsMenuOpen(window))
                    window.ActivateById(openerId);
                return found;
            },
            Wait.DefaultTimeout,
            $"menu item '{itemId}' of '{openerId}'");
        window.ActivateById(itemId);
    }

    private static bool IsMenuOpen(Window window) =>
        window.FindFirstDescendant(cf => cf.ByClassName("MenuFlyout")) is not null;

    /// <summary>Reads every entry of a list, including those a virtualizing list has not created yet: the
    /// ItemContainer pattern enumerates every item, each one is realized and read right away — realizing later
    /// items lets the list recycle earlier ones, whose elements then become invalid. Entries for which
    /// <paramref name="read"/> returns null are skipped.</summary>
    public static IReadOnlyList<T> ReadAllItems<T>(this AutomationElement list, Func<AutomationElement, T?> read)
        where T : class =>
        [.. RealizedItems(list).Select(read).OfType<T>()];

    /// <summary>Reads the first <paramref name="count"/> entries of a list (fewer if it is shorter) and stops there —
    /// for long lists where only the top matters: every further entry costs a pause before it is realized.</summary>
    public static IReadOnlyList<T> ReadFirstItems<T>(this AutomationElement list, int count, Func<AutomationElement, T> read) =>
        [.. RealizedItems(list).Take(count).Select(read)];

    /// <summary>The first entry of a list for which <paramref name="matches"/> holds, realized if necessary; null if
    /// there is none. Stops at the match: realizing further items lets a virtualizing list recycle the found one,
    /// whose element then reports "not available" when it is used (#572).</summary>
    public static AutomationElement? FindItem(this AutomationElement list, Func<AutomationElement, bool> matches) =>
        RealizedItems(list).FirstOrDefault(matches);

    // Realizing list items in quick succession makes a virtualizing list hand out rows that still show an earlier item, in
    // whole or field by field. Measured on the CI runner with 306 rows (#569): without a pause 90–100 of 306 pairs
    // missing, 10 ms still 21, 30/60 ms none (as did reading each row again after 20/50 ms, which only lengthened the
    // gap to the next Realize). A row read right after Realize never changed when read again, so the error cannot be
    // caught by reading until stable — only the pause before the next Realize avoids it.
    private static readonly TimeSpan _pauseBeforeRealize = TimeSpan.FromMilliseconds(50);

    /// <summary>Every entry of a list, each realized just before it is handed out — lazily, so a caller that stops
    /// early realizes nothing beyond.</summary>
    private static IEnumerable<AutomationElement> RealizedItems(AutomationElement list)
    {
        if (!list.Patterns.ItemContainer.TryGetPattern(out var container))
        {
            foreach (var child in list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)))
                yield return child;
            yield break;
        }

        bool realizedBefore = false;
        for (var item = container.FindItemByProperty(null, null, null);
             item is not null;
             item = container.FindItemByProperty(item, null, null))
        {
            if (item.Patterns.VirtualizedItem.TryGetPattern(out var virtualized))
            {
                if (realizedBefore)
                    Thread.Sleep(_pauseBeforeRealize);
                virtualized.Realize();
                realizedBefore = true;
            }
            yield return item;
        }
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
