using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.Wpf.Navigation;

/// <summary>
/// Von TurnierTabKoordinator implementiert — löst die vormals hart auf
/// TurnierEditorViewModel verdrahteten Typ-Checks in TurnierAuswahlView.xaml.cs, seit
/// Issue #172 gibt es zwei Composite-VMs (Turnierdefinition/-durchführung) mit je einem
/// eigenen Koordinator statt eines einzigen Turnier-Bereichs.
/// </summary>
public interface ITurnierAuswahlHost
{
    ObservableCollection<TurnierInstanz> Instanzen { get; }
    ObservableCollection<TurnierInstanz> InstanzenGefiltert { get; }
    TurnierInstanz? AusgewaehlteInstanz { get; set; }
    string? InstanzSuchbegriff { get; set; }
}
