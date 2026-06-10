using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class SpielStatusZuFarbeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SpielStatus status
            ? status switch
            {
                SpielStatus.Abgeschlossen => new SolidColorBrush(Color.FromRgb(22, 163, 74)),   // green-600
                SpielStatus.Laufend       => new SolidColorBrush(Color.FromRgb(234, 179, 8)),   // yellow-500
                SpielStatus.Verschoben    => new SolidColorBrush(Color.FromRgb(249, 115, 22)),  // orange-500
                SpielStatus.Abgebrochen   => new SolidColorBrush(Color.FromRgb(220, 38, 38)),   // red-600
                _                         => new SolidColorBrush(Color.FromRgb(148, 163, 184))  // slate-400
            }
            : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
