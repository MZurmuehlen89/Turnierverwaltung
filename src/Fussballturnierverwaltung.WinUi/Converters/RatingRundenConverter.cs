using Microsoft.UI.Xaml.Data;
using System.Globalization;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Formatiert ein decimal-Rating auf eine Nachkommastelle. Nötig, weil die Binding-
/// Markup-Extension in WinUI 3 (anders als WPF) kein StringFormat unterstützt.</summary>
public class RatingRundenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is decimal rating ? rating.ToString("F1", CultureInfo.CurrentCulture) : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
