using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Nur lokal in Views registriert, die Rundenart anzeigen (aktuell nur RundenPage) — kein
/// globaler App.xaml-Eintrag, analog zu VerfahrenArtZuTextConverter.</summary>
public class RundenartZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Rundenart r ? RundenartAnzeigeName(r) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    public static string RundenartAnzeigeName(Rundenart r) => r switch
    {
        Rundenart.Liga             => "Liga",
        Rundenart.Gruppenphase     => "Gruppenphase",
        Rundenart.TopfLiga         => "Topf-Liga (Swiss-Modell)",
        Rundenart.KoRunde          => "K.o.-Runde",
        Rundenart.SchweizerSystem  => "Schweizer System",
        Rundenart.Sonderrunde      => "Sonderrunde",
        Rundenart.Finale           => "Finale",
        Rundenart.SpielUmPlatzDrei => "Spiel um Platz 3",
        _                          => r.ToString()
    };
}
