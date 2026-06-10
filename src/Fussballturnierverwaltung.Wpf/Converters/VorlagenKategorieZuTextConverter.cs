using Fussballturnierverwaltung.Domain.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Fussballturnierverwaltung.Wpf.Converters;

public class VorlagenKategorieZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is VorlagenKategorie k ? KategorieAnzeigeName(k) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    public static string KategorieAnzeigeName(VorlagenKategorie k) => k switch
    {
        VorlagenKategorie.Weltmeisterschaft       => "Weltmeisterschaft",
        VorlagenKategorie.Kontinentalmeisterschaft => "Kontinentalmeisterschaft",
        VorlagenKategorie.Vereinspokal             => "Vereinspokal",
        VorlagenKategorie.Liga                     => "Liga",
        VorlagenKategorie.KleinesTurnier           => "Kleines Turnier",
        VorlagenKategorie.Benutzerdefiniert        => "Benutzerdefiniert",
        _                                          => k.ToString()
    };
}
