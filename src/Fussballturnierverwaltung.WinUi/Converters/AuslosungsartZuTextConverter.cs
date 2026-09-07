using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Data;

namespace Fussballturnierverwaltung.WinUi.Converters;

/// <summary>Nur lokal in AuslosungPage registriert — kein globaler App.xaml-Eintrag, analog
/// TurnierKategorieZuTextConverter.</summary>
public class AuslosungsartZuTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Auslosungsart a ? AuslosungViewModel.AuslosungsartAnzeigeName(a) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
