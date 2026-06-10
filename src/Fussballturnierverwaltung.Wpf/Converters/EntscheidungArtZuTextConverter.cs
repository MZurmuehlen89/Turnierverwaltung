using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class EntscheidungArtZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EntscheidungArt art ? AnzeigeName(art) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static string AnzeigeName(EntscheidungArt art) => art switch
    {
        EntscheidungArt.RegulaereSpielzeit => "Reguläre Spielzeit",
        EntscheidungArt.Verlaengerung      => "Verlängerung",
        EntscheidungArt.GoldenGoal         => "Golden Goal",
        EntscheidungArt.SilverGoal         => "Silver Goal",
        EntscheidungArt.Elfmeterschiessen  => "Elfmeterschießen",
        EntscheidungArt.Auswaertstorregel  => "Auswärtstorregel",
        EntscheidungArt.Wiederholungsspiel => "Wiederholungsspiel",
        EntscheidungArt.Losentscheid       => "Losentscheid",
        EntscheidungArt.Muenzwurf          => "Münzwurf",
        _                                  => art.ToString()
    };
}
