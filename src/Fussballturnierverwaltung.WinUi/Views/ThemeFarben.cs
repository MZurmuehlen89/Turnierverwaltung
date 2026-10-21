using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Brush aus den ThemeDictionaries der App.xaml nach dem Theme eines Elements. Ein Converter oder
/// <c>Application.Current.Resources[…]</c> liefert immer die Hell-Variante, weil der Dunkelmodus per RequestedTheme am
/// Fenster-Wurzelelement sitzt (#340) — deshalb über <see cref="FrameworkElement.ActualTheme"/>.</summary>
public static class ThemeFarben
{
    public static Brush? Brush(FrameworkElement element, string schluessel)
    {
        string theme = element.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        return Microsoft.UI.Xaml.Application.Current.Resources.ThemeDictionaries.TryGetValue(theme, out var eintraege)
               && eintraege is ResourceDictionary themeFarben
               && themeFarben.TryGetValue(schluessel, out var brush)
            ? brush as Brush
            : null;
    }
}
