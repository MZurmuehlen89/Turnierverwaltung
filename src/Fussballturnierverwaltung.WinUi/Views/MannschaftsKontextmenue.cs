using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Kontextmenü an einer Mannschaft im Turnier (#479): ein Element mit
/// <c>views:MannschaftsKontextmenue.MannschaftId="{Binding …}"</c> bekommt per Rechtsklick „Alle Spiele des Teams
/// anzeigen“ (Wert: die Mannschafts-Id als <c>int</c>). Ausgeführt wird es vom nächsten
/// <see cref="IMannschaftsSpieleAnzeige"/> im DataContext eines übergeordneten Elements — die Gruppentabelle und der
/// Turnierbaum haben selbst einen anderen DataContext als der Spielplan. Ohne Mannschaft (offener Platzhalter, <c>null</c>) gibt es kein Menü. Weitere Einträge (#324) kommen
/// hier hinzu.</summary>
public static class MannschaftsKontextmenue
{
    public const string ALLE_SPIELE_TEXT = "Alle Spiele des Teams anzeigen";

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
        var alleSpiele = new MenuFlyoutItem { Text = ALLE_SPIELE_TEXT, Icon = new SymbolIcon(Symbol.List) };
        alleSpiele.Click += (_, _) =>
        {
            if (GetMannschaftId(element) is not int mannschaftId || AnzeigeFinden(element) is not { } anzeige) return;
            Ablaufprotokoll.Bedienung(ALLE_SPIELE_TEXT);
            _ = anzeige.MannschaftsSpieleAnzeigenAsync(mannschaftId);
        };

        var menue = new MenuFlyout();
        menue.Items.Add(alleSpiele);
        return menue;
    }

    private static IMannschaftsSpieleAnzeige? AnzeigeFinden(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
            if (element is FrameworkElement { DataContext: IMannschaftsSpieleAnzeige anzeige })
                return anzeige;
        return null;
    }
}
