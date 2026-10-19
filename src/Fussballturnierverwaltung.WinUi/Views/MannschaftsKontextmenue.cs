using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Kontextmenü an einer Mannschaft im Turnier: ein Element mit
/// <c>views:MannschaftsKontextmenue.MannschaftId="{Binding …}"</c> bekommt per Rechtsklick „Alle Spiele des Teams
/// anzeigen“ (#479) und „Statistik anzeigen“ (#324) (Wert: die Mannschafts-Id als <c>int</c>). Ausgeführt wird es vom
/// nächsten <see cref="IMannschaftsAnzeige"/> im DataContext eines übergeordneten Elements — die Gruppentabelle und der
/// Turnierbaum haben selbst einen anderen DataContext als der Spielplan. Ohne Mannschaft (offener Platzhalter,
/// <c>null</c>) gibt es kein Menü. Spielzeilen und Baumkarten haben stattdessen das Spielmenü
/// (<see cref="SpielKontextmenue"/>, #62) mit denselben Einträgen für beide Mannschaften.</summary>
public static class MannschaftsKontextmenue
{
    public const string ALLE_SPIELE_TEXT = "Alle Spiele des Teams anzeigen";
    public const string STATISTIK_TEXT = "Statistik anzeigen";

    /// <summary>Symbol der Statistik-Einträge (Segoe Fluent Icons „AreaChart“).</summary>
    public static IconElement StatistikIcon() => new FontIcon { Glyph = "" };

    public static readonly DependencyProperty MannschaftIdProperty = DependencyProperty.RegisterAttached(
        "MannschaftId", typeof(object), typeof(MannschaftsKontextmenue), new PropertyMetadata(null, MannschaftIdGeaendert));

    public static object? GetMannschaftId(DependencyObject element) => element.GetValue(MannschaftIdProperty);

    public static void SetMannschaftId(DependencyObject element, object? wert) => element.SetValue(MannschaftIdProperty, wert);

    private static void MannschaftIdGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        element.ContextFlyout = e.NewValue is int ? MenueErstellen(element) : null;
    }

    private static MenuFlyout MenueErstellen(UIElement element)
    {
        var menue = new MenuFlyout();
        menue.Items.Add(Eintrag(element, ALLE_SPIELE_TEXT, new SymbolIcon(Symbol.List),
            (anzeige, mannschaftId) => _ = anzeige.MannschaftsSpieleAnzeigenAsync(mannschaftId)));
        menue.Items.Add(Eintrag(element, STATISTIK_TEXT, StatistikIcon(),
            (anzeige, mannschaftId) => anzeige.MannschaftsStatistikAnzeigen(mannschaftId)));
        return menue;
    }

    private static MenuFlyoutItem Eintrag(
        UIElement element, string text, IconElement icon, Action<IMannschaftsAnzeige, int> aktion)
    {
        var eintrag = new MenuFlyoutItem { Text = text, Icon = icon };
        eintrag.Click += (_, _) =>
        {
            if (GetMannschaftId(element) is not int mannschaftId ||
                ZielFinden<IMannschaftsAnzeige>(element) is not { } anzeige) return;
            Ablaufprotokoll.Bedienung(text);
            aktion(anzeige, mannschaftId);
        };
        return eintrag;
    }

    /// <summary>Das nächste <typeparamref name="T"/> im DataContext von <paramref name="element"/> oder einem
    /// übergeordneten Element — auch für die Spielmenüs des Turnierbaums (<see cref="SpielKontextmenue"/>, #62).</summary>
    public static T? ZielFinden<T>(DependencyObject? element) where T : class
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
            if (element is FrameworkElement { DataContext: T ziel })
                return ziel;
        return null;
    }
}
