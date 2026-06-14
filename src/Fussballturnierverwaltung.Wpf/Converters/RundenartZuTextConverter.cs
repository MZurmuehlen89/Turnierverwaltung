using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class RundenartZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Rundenart r ? RundenartAnzeigeName(r) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public static string RundenartAnzeigeName(Rundenart r) => r switch
    {
        Rundenart.Liga                       => "Liga",
        Rundenart.Gruppenphase               => "Gruppenphase",
        Rundenart.TopfLiga                   => "Topf-Liga",
        Rundenart.KoRunde                    => "K.o.-Runde",
        Rundenart.SchweizerSystem            => "Schweizer System",
        Rundenart.Sonderrunde                => "Sonderrunde",
        Rundenart.Finale                     => "Finale",
        Rundenart.SpielUmPlatzDrei           => "Spiel um Platz 3",
        _                                    => r.ToString()
    };
}
