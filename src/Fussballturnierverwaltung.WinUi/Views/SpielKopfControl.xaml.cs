using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Issue #341: Spielkopf nach kicker.de-Vorbild — DataContext ist ein
/// <see cref="ViewModels.SpielKopf"/>.</summary>
public sealed partial class SpielKopfControl : UserControl
{
    public SpielKopfControl()
    {
        InitializeComponent();
    }
}
