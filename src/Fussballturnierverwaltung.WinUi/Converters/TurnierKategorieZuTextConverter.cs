using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Nur lokal in TurnierkopfPage registriert — kein globaler App.xaml-Eintrag, analog
/// VerfahrenArtZuTextConverter.</summary>
public class TurnierKategorieZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is TurnierKategorie k ? KategorieAnzeigeName(k) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    public static string KategorieAnzeigeName(TurnierKategorie k) => k switch
    {
        TurnierKategorie.Weltmeisterschaft        => "Weltmeisterschaft",
        TurnierKategorie.Kontinentalmeisterschaft => "Kontinentalmeisterschaft",
        TurnierKategorie.Vereinspokal              => "Vereinspokal",
        TurnierKategorie.Liga                      => "Liga",
        TurnierKategorie.KleinesTurnier            => "Kleines Turnier",
        TurnierKategorie.Benutzerdefiniert         => "Benutzerdefiniert",
        _                                            => k.ToString()
    };
}
