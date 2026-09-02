using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

public class VerfahrenArtZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is VerfahrenArt art ? VerfahrenAnzeigeName(art) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
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
