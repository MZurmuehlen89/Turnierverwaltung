using System.Runtime.InteropServices;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Startfenster bei einem Verbindungsfehler (#49): dieselben Felder wie auf der Seite „Datenbankwartung“.
/// „Verbinden“ prüft und speichert, „Beenden“ oder Schließen bricht ab. Das Ergebnis liefert <see cref="ErgebnisAsync"/>.</summary>
public sealed partial class VerbindungsFenster : Window
{
    private const int BREITE = 600;
    private const int HOEHE = 600;

    private readonly VerbindungsFormularViewModel _formular;
    private readonly TaskCompletionSource<bool> _ergebnis = new();

    public VerbindungsFenster(VerbindungsFormularViewModel formular, string ursache, ThemeModus theme)
    {
        InitializeComponent();
        Wurzel.RequestedTheme = theme == ThemeModus.Hell ? ElementTheme.Light : ElementTheme.Dark;
        _formular = formular;
        _formular.Fehlermeldung = ursache;
        Felder.DataContext = _formular;
        _formular.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VerbindungsFormularViewModel.Laeuft))
                VerbindenKnopf.IsEnabled = BeendenKnopf.IsEnabled = !_formular.Laeuft;
        };

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var skalierung = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(BREITE * skalierung), (int)(HOEHE * skalierung)));
        Closed += (_, _) => _ergebnis.TrySetResult(false);
    }

    /// <summary><c>true</c>: Verbindung gespeichert, der Start geht weiter; <c>false</c>: der Nutzer hat beendet.</summary>
    public Task<bool> ErgebnisAsync => _ergebnis.Task;

    private async void Verbinden_Click(object sender, RoutedEventArgs e)
    {
        if (!await _formular.UebernehmenAsync()) return;
        _ergebnis.TrySetResult(true);
        Close();
    }

    private void Beenden_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
