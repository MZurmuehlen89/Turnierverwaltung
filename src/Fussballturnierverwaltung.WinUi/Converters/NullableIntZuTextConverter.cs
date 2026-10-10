using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>TwoWay-Bindung einer <c>TextBox</c> an eine <c>int?</c>-Property: WinUI 3s Standard-Konvertierung
/// (anders als WPFs <c>NullableConverter</c>) wandelt einen eingegebenen Text nicht zurück in <c>Nullable&lt;int&gt;</c>
/// — das Binding schlägt bei ConvertBack fehl-still (keine Exception, aber die Property wird nie gesetzt), was
/// z.B. bei <c>RundenFormularViewModel.GruppenAnzahl</c> dazu führte, dass eine Änderung nie <c>IsDirty</c> auslöste
/// (Nutzerfund: Speichern-Button blieb nach Gruppenanzahl-Änderung deaktiviert).</summary>
public class NullableIntZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is int i ? i.ToString() : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is string s && int.TryParse(s, out var i) ? i : null!;
}
