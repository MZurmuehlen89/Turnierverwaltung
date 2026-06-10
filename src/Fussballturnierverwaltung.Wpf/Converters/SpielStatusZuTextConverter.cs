using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class SpielStatusZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SpielStatus status ? AnzeigeName(status) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static string AnzeigeName(SpielStatus status) => status switch
    {
        SpielStatus.Geplant       => "Geplant",
        SpielStatus.Laufend       => "Laufend",
        SpielStatus.Abgeschlossen => "Fertig",
        SpielStatus.Verschoben    => "Verschoben",
        SpielStatus.Abgebrochen   => "Abgebrochen",
        _                         => status.ToString()
    };
}
