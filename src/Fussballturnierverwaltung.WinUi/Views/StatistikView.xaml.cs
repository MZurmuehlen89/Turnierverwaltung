using Fussballturnierverwaltung.WinUi.Helpers;
using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class StatistikView : UserControl
{
    public StatistikView()
    {
        InitializeComponent();
    }

    // Klick auf eine Ranglistenzeile öffnet die Seite der Mannschaft (#324) — im Code-Behind, weil die Zeile eine
    // KlickFlaeche ohne Command ist (#428).
    private void RanglistenZeile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StatistikZeile { Mannschaft: { } mannschaft } } ||
            DataContext is not StatistikViewModel vm) return;
        Ablaufprotokoll.Bedienung($"Statistik: Ranglistenzeile {mannschaft.Name}");
        vm.MannschaftsStatistikAnzeigen(mannschaft.Id);
    }

    // Klicks in der Kurve des Tabellenverlaufs (#480).
    private void Diagramm_MannschaftGeklickt(object? sender, int mannschaftId)
    {
        if (DataContext is not StatistikViewModel vm) return;
        Ablaufprotokoll.Bedienung($"Statistik: Tabellenverlauf Linie {mannschaftId}");
        vm.TabellenVerlauf.MannschaftUmschalten(mannschaftId);
    }

    private void Diagramm_SpieltagGeklickt(object? sender, int spieltag)
    {
        if (DataContext is not StatistikViewModel vm) return;
        Ablaufprotokoll.Bedienung($"Statistik: Tabellenverlauf Spieltag {spieltag}");
        vm.TabellenVerlauf.SpieltagWaehlen(spieltag);
    }

    // Fokus aus der Box: sonst klappt die Vorschlagsliste beim nächsten Tastendruck wieder auf.
    private void MannschaftsAuswahl_Bestaetigt(object? sender, EventArgs e)
        => AuswertungsListe.Focus(FocusState.Programmatic);
}
