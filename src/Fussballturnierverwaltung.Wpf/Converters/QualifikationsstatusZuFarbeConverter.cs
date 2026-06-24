using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class QualifikationsstatusZuFarbeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Qualifikationsstatus status
            ? status switch
            {
                Qualifikationsstatus.SicherQualifiziert  => new SolidColorBrush(Color.FromRgb(22,  163, 74)),  // green-600
                Qualifikationsstatus.SicherAusgeschieden => new SolidColorBrush(Color.FromRgb(220, 38,  38)),  // red-600
                _                                         => Brushes.Transparent
            }
            : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
