using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace GuiTest.Framework;

/// <summary>An open WinUI <c>ContentDialog</c>. UI Automation shows it as a window of class <c>Popup</c> inside the
/// main window; its buttons carry the fixed AutomationIds <c>PrimaryButton</c>, <c>SecondaryButton</c> and
/// <c>CloseButton</c>. Other popups (menus, tooltips) have none of them and are not dialogs.</summary>
public sealed class Dialog
{
    private static readonly string[] _buttonIds = ["PrimaryButton", "SecondaryButton", "CloseButton"];

    private Dialog(AutomationElement element) => Element = element;

    public AutomationElement Element { get; }
    public string Title => Element.NameOrEmpty();

    public static IReadOnlyList<Dialog> FindOpen(Window window) =>
        [.. window
            .FindAllDescendants(cf => cf.ByControlType(ControlType.Window).And(cf.ByClassName("Popup")))
            .Where(HasDialogButton)
            .Select(popup => new Dialog(popup))];

    public static Dialog WaitFor(Window window, string title, TimeSpan? timeout = null) =>
        Wait.UntilFound(
            () => FindOpen(window).FirstOrDefault(d => d.Title == title),
            timeout ?? Wait.DefaultTimeout,
            $"dialog '{title}'");

    public void Confirm() => Element.ActivateById("PrimaryButton");
    public void Close() => Element.ActivateById("CloseButton");

    private static bool HasDialogButton(AutomationElement popup) =>
        _buttonIds.Any(id => popup.TryFindById(id) is not null);
}
