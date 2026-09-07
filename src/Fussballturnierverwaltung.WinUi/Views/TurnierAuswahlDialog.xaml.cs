using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Popup-Turnierauswahl (#300-Nachbesserung) — Precedent: WPF <c>TurnierAuswahlDialog</c>. Nutzt
/// den bereits vorhandenen <see cref="TurnierTabKoordinator"/> direkt als DataContext (Suchbegriff/
/// InstanzenGefiltert/AusgewaehlteInstanz sind dort schon vorhanden), keine eigene ViewModel-Kopie nötig.</summary>
public sealed partial class TurnierAuswahlDialog : ContentDialog
{
    public TurnierAuswahlDialog(TurnierTabKoordinator tabKoordinator)
    {
        InitializeComponent();
        DataContext = tabKoordinator;
    }

    private void ListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => Hide();
}
