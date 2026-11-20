using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Eine Spielbericht-Karte (Spielkopf, Zeitstrahl, Zeitleiste), #247. Der DataContext liefert lose über
/// Property-Namen ProtokollKopf/ProtokollEreignisse/HatProtokoll/HatProtokollEreignisse.</summary>
public sealed partial class SpielberichtKarte : UserControl
{
    public SpielberichtKarte()
    {
        InitializeComponent();
    }
}
