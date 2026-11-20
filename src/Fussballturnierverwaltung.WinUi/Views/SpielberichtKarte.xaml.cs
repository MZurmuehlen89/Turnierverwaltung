using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Eine Spielbericht-Karte (Spielkopf, Zeitstrahl, Zeitleiste), #247. Der DataContext ist eine
/// <see cref="ViewModels.SpielBeinAnsicht"/> (Kopf, Ereignisse, HatEreignisse).</summary>
public sealed partial class SpielberichtKarte : UserControl
{
    public SpielberichtKarte()
    {
        InitializeComponent();
    }
}
