using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

/// <summary>Wandelt eine Spieltag-Auswahloption (0 = aktueller Stand) in den Anzeigetext um.</summary>
public class SpieltagOptionZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int spieltag and > 0 ? $"Spieltag {spieltag}" : "Aktuell";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
