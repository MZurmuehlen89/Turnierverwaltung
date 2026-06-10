using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

[ValueConversion(typeof(VerfahrenArt), typeof(string))]
public class VerfahrenArtZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is VerfahrenArt art ? VerfahrenAnzeigeName(art) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static string VerfahrenAnzeigeName(VerfahrenArt art) => art switch
    {
        VerfahrenArt.VerlaengerungKlassisch => "Verlängerung (klassisch)",
        VerfahrenArt.GoldenGoal             => "Golden Goal",
        VerfahrenArt.SilverGoal             => "Silver Goal",
        VerfahrenArt.Elfmeterschiessen      => "Elfmeterschießen",
        VerfahrenArt.Wiederholungsspiel     => "Wiederholungsspiel",
        VerfahrenArt.Losentscheid           => "Losentscheid",
        VerfahrenArt.Muenzwurf              => "Münzwurf",
        _                                   => art.ToString()
    };
}
