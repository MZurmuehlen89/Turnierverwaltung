using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Ansicht der Ziehung von Hand (#482), DataContext = <see cref="HandZiehungViewModel"/>. Die Klicks kommen im
/// Code-Behind an, weil die Flächen KlickFlaechen ohne Command sind (#428); was gewählt werden darf, entscheidet das
/// Element selbst (<see cref="HandZiehungWahl.Waehlen"/>).</summary>
public sealed partial class HandZiehungAnzeige : UserControl
{
    public HandZiehungAnzeige()
    {
        InitializeComponent();
    }

    private void Team_Tapped(object sender, TappedRoutedEventArgs e) =>
        Waehlen(((sender as FrameworkElement)?.DataContext as HandZiehungTeam)?.Wahl, e);

    // Ein Platz liegt in seiner Gruppe — ein gewählter Platz soll die Gruppe nicht mitwählen.
    private void Platz_Tapped(object sender, TappedRoutedEventArgs e) =>
        Waehlen(((sender as FrameworkElement)?.DataContext as HandZiehungPlatz)?.Wahl, e);

    private void Gruppe_Tapped(object sender, TappedRoutedEventArgs e) =>
        Waehlen(((sender as FrameworkElement)?.DataContext as HandZiehungGruppe)?.Wahl, e);

    private static void Waehlen(HandZiehungWahl? wahl, TappedRoutedEventArgs e)
    {
        if (wahl is not { IstWaehlbar: true }) return;
        e.Handled = true;
        wahl.Waehlen();
    }
}
