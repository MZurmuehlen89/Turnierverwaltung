using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Hintergrund eines Menüeintrags (#269 K9) für <c>x:Bind</c>-Funktionsbindung: hell hinterlegt beim
/// Eintrag der offenen Seite, sonst durchsichtig. Kein <c>IValueConverter</c>, weil <c>MainWindow</c> als
/// <c>Window</c> kein <c>FrameworkElement</c> ist und <c>x:Bind</c> in dessen DataTemplates keine
/// StaticResource-Converter auflösen kann. Hover und Gedrückt setzt weiter die Schaltflächenvorlage.</summary>
public static class MenueHintergrund
{
    private static readonly SolidColorBrush _aktiv = new(Windows.UI.Color.FromArgb(0x29, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush _normal = new(Microsoft.UI.Colors.Transparent);

    public static Brush Fuer(bool istAktiv) => istAktiv ? _aktiv : _normal;
}
