using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Anzeigenamen für <see cref="TurnierStatus"/> (Issue #297) — nur lokal in TurnierkopfPage
/// registriert, kein globaler App.xaml-Eintrag, analog TurnierKategorieZuTextConverter.</summary>
public class TurnierStatusZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is TurnierStatus s ? StatusAnzeigeName(s) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    public static string StatusAnzeigeName(TurnierStatus s) => s switch
    {
        TurnierStatus.Geplant     => "Geplant",
        TurnierStatus.Auslosung   => "Auslosung",
        TurnierStatus.Laufend     => "Laufend",
        TurnierStatus.Abgeschlossen => "Abgeschlossen",
        TurnierStatus.Abgebrochen => "Abgebrochen",
        TurnierStatus.Vorlage     => "Vorlage",
        _                         => s.ToString()
    };
}
