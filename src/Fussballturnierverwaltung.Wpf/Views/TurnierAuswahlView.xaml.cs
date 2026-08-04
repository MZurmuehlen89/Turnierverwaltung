using Fussballturnierverwaltung.Wpf.Navigation;
using System.Windows;
using System.Windows.Controls;

namespace Fussballturnierverwaltung.Wpf.Views;

public partial class TurnierAuswahlView : UserControl
{
    // Verhindert, dass das programmatische Text-Update, das eine Auswahl auslöst
    // (IsEditable-ComboBox spiegelt SelectedItem in Text), selbst wieder als Sucheingabe
    // interpretiert und der Filter dadurch fälschlich auf den gewählten Namen eingeengt wird.
    private bool _selektionAktiv;

    public TurnierAuswahlView()
    {
        InitializeComponent();
    }

    private void InstanzCombo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selektionAktiv)
        {
            _selektionAktiv = false;
            return;
        }

        if (DataContext is ITurnierAuswahlHost vm)
        {
            vm.InstanzSuchbegriff = InstanzCombo.Text;
            InstanzCombo.IsDropDownOpen = true;
        }
    }

    private void InstanzCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selektionAktiv = true;
        if (DataContext is ITurnierAuswahlHost vm)
            vm.InstanzSuchbegriff = null;
    }

    private void AuswahlDialogButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ITurnierAuswahlHost vm) return;

        var dialog = new TurnierAuswahlDialog(vm.Instanzen) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Ausgewaehlt is not null)
            vm.AusgewaehlteInstanz = dialog.Ausgewaehlt;
    }
}
