using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Nur lokal in TurnierkopfPage registriert — kein globaler App.xaml-Eintrag, analog
/// VerfahrenArtZuTextConverter.</summary>
public class VorlagenKategorieZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is VorlagenKategorie k ? KategorieAnzeigeName(k) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    public static string KategorieAnzeigeName(VorlagenKategorie k) => k switch
    {
        VorlagenKategorie.Weltmeisterschaft        => "Weltmeisterschaft",
        VorlagenKategorie.Kontinentalmeisterschaft => "Kontinentalmeisterschaft",
        VorlagenKategorie.Vereinspokal              => "Vereinspokal",
        VorlagenKategorie.Liga                      => "Liga",
        VorlagenKategorie.KleinesTurnier            => "Kleines Turnier",
        VorlagenKategorie.Benutzerdefiniert         => "Benutzerdefiniert",
        _                                            => k.ToString()
    };
}
