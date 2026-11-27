using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class Siegerfeld : UserControl
{
    public Siegerfeld()
    {
        InitializeComponent();

        // Eigener Wert statt Erben: Steht das Steuerelement im XAML der TurnierBaumView, bindet es bis zum ersten
        // Setzen des DataContext gegen deren TurnierBaumViewModel – acht Pfadfehler je Seitenaufbau in der Debug-Ausgabe.
        DataContext = new SiegerfeldAnzeige(null, null);
    }
}
