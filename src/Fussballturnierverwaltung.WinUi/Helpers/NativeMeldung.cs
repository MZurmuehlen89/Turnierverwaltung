using System.Runtime.InteropServices;

namespace Fussballturnierverwaltung.WinUi.Helpers;

/// <summary>Win32-MessageBox für Fehler, bevor ein Fenster mit XamlRoot existiert (ContentDialog braucht eins) —
/// z. B. wenn die Datenbank beim Start nicht erreichbar ist (Issue #370).</summary>
internal static partial class NativeMeldung
{
    private const uint MB_OK = 0x0;
    private const uint MB_ICONERROR = 0x10;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static void FehlerAnzeigen(string titel, string text) =>
        MessageBoxW(IntPtr.Zero, text, titel, MB_OK | MB_ICONERROR);
}
