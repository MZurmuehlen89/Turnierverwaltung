using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace GuiTest.Framework;

/// <summary>The Windows common file dialog (open or save) shown by an application, e.g. by a WinUI
/// <c>FileOpenPicker</c>/<c>FileSavePicker</c>. It is a top-level window of class <c>#32770</c> owned by the
/// application, so it is looked up as a child of the main window and, failing that, on the desktop by process id.
/// Its controls are addressed by their fixed Win32 control ids instead of captions, which depend on the system
/// language: <c>1001</c> is the file name edit of the save dialog, <c>1148</c> that of the open dialog, <c>1</c> the
/// confirming button.</summary>
public sealed class FileDialog
{
    private const string DIALOG_CLASS = "#32770";
    private static readonly string[] _fileNameIds = ["1001", "1148"];

    private readonly Window _owner;

    private FileDialog(Window owner, AutomationElement element)
    {
        _owner = owner;
        Element = element;
    }

    public AutomationElement Element { get; }

    public static FileDialog WaitFor(Window owner, TimeSpan? timeout = null) =>
        new(owner, Wait.UntilFound(() => Find(owner), timeout ?? Wait.DefaultTimeout, "file dialog"));

    /// <summary>Enters <paramref name="path"/> (absolute) as file name, confirms and waits until the dialog is gone.
    /// If the file name field is missing, the exception lists the dialog's elements – the structure differs between
    /// Windows versions, and the list shows what to address instead.</summary>
    public void Choose(string path)
    {
        AutomationElement fileName;
        try
        {
            fileName = Wait.UntilFound(FileNameEdit, Wait.DefaultTimeout, "file name field");
        }
        catch (WaitTimeoutException e)
        {
            throw new InvalidOperationException(Describe(), e);
        }
        fileName.Patterns.Value.Pattern.SetValue(path);
        Element.FindFirstChild(cf => cf.ByAutomationId("1").And(cf.ByControlType(ControlType.Button)))
            .Patterns.Invoke.Pattern.Invoke();
        Wait.Until(() => Find(_owner) is null, Wait.DefaultTimeout, "file dialog closed");
    }

    private AutomationElement? FileNameEdit() =>
        _fileNameIds
            .Select(id => Element.FindFirstDescendant(cf => cf.ByAutomationId(id).And(cf.ByControlType(ControlType.Edit))))
            .FirstOrDefault(e => e is not null);

    /// <summary>The dialog's elements as "ControlType 'AutomationId' 'Name'" lines, for diagnosing a failed lookup.</summary>
    public string Describe() =>
        "File dialog elements:" + Environment.NewLine + string.Join(Environment.NewLine,
            Element.FindAllDescendants().Select(e =>
                $"{e.Properties.ControlType.ValueOrDefault} '{e.Properties.AutomationId.ValueOrDefault}' '{e.NameOrEmpty()}'"));

    private static AutomationElement? Find(Window owner) =>
        owner.FindFirstChild(cf => cf.ByClassName(DIALOG_CLASS))
        ?? owner.Automation.GetDesktop().FindFirstChild(cf =>
            cf.ByClassName(DIALOG_CLASS).And(cf.ByProcessId(owner.Properties.ProcessId.Value)));
}
