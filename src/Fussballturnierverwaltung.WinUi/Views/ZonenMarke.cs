using Fussballturnierverwaltung.Application.Tabelle;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Farbfläche einer Tabellenzone (Issue #358) — Balken, Sicher-Punkt und Legendenpunkt. Ohne
/// <see cref="Farbe"/> bleibt die Fläche leer, behält aber ihre Größe (kein Versatz zwischen Zeilen mit und
/// ohne Markierung). Die Brushes stehen als <c>ZonenFarbe…Brush</c> in den ThemeDictionaries der App.xaml und
/// werden nach dem Theme dieses Elements gewählt: ein Converter hätte kein Element und bekäme über
/// <c>Application.Current.Resources</c> immer die Hell-Variante (Dunkelmodus sitzt per RequestedTheme am
/// Fenster-Wurzelelement, siehe #340).</summary>
public sealed class ZonenMarke : Grid
{
    public static readonly DependencyProperty FarbeProperty = DependencyProperty.Register(
        nameof(Farbe), typeof(object), typeof(ZonenMarke),
        new PropertyMetadata(null, (d, _) => ((ZonenMarke)d).FarbeAnwenden()));

    /// <summary><see cref="ZonenFarbe"/> oder <c>null</c> — als <c>object</c>, damit ein Binding auf eine
    /// nullable Enum-Eigenschaft ohne Converter auskommt.</summary>
    public object? Farbe
    {
        get => GetValue(FarbeProperty);
        set => SetValue(FarbeProperty, value);
    }

    public ZonenMarke()
    {
        Loaded += (_, _) => FarbeAnwenden();
        ActualThemeChanged += (_, _) => FarbeAnwenden();
    }

    private void FarbeAnwenden()
    {
        if (Farbe is not ZonenFarbe farbe)
        {
            Background = null;
            return;
        }

        Background = ThemeFarben.Brush(this, $"ZonenFarbe{farbe}Brush");
    }
}
