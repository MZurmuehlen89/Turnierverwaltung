using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class RundenPage : UserControl
{
    public RundenPage()
    {
        InitializeComponent();
    }

    /// <summary>MenuFlyout kennt kein ItemsSource — die Gruppenanzahl-Vorschläge werden deshalb bei jedem Öffnen
    /// aus dem aktuellen Eingang neu eingetragen (#476).</summary>
    private void GruppenAnzahlVorschlaege_Opening(object sender, object e)
    {
        var menue = (MenuFlyout)sender;
        menue.Items.Clear();
        if (DataContext is not RundenViewModel vm) return;
        foreach (var vorschlag in vm.Formular.GruppenAnzahlVorschlaege)
        {
            menue.Items.Add(new MenuFlyoutItem
            {
                Text = vorschlag.Anzeige,
                Command = vm.Formular.GruppenAnzahlVorschlagWaehlenCommand,
                CommandParameter = vorschlag,
            });
        }
    }
}

/// <summary>Hervorhebung der ausgewählten Eingehend-/Ausgehend-Regelzeile über den Button-Hintergrund statt über
/// einen eigenen Selektions-Control-State (siehe #284-Bugfix: ToggleButton.IsChecked kollidierte mit dem nativen
/// Klick-Toggle-Verhalten, mehrere Zeilen blieben gleichzeitig markiert). Lokal für diese Page.</summary>
public class BoolZuAuswahlHintergrundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true
            ? Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
