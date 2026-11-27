using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Navigation;

public partial class NavigationsEintrag : ObservableObject
{
    public required string Titel { get; init; }
    public required ICommand Befehl { get; init; }

    /// <summary>Kennung für automatisierte GUI-Tests (#521), Schema <c>Navigation.Ziel</c>.</summary>
    public required string AutomationId { get; init; }

    /// <summary>ViewModel-Typ der Seite, die dieser Eintrag öffnet (#269 K9) — daran erkennt das Menü, welcher
    /// Eintrag zur offenen Seite gehört, auch wenn die Seite aus dem Code geöffnet wurde.</summary>
    public required Type ZielTyp { get; init; }

    /// <summary>Gehört die offene Seite zu diesem Eintrag? Genau ein Eintrag zugleich (#269 K9).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    private bool _istAktiv;

    /// <summary>UIA-<c>ItemStatus</c> für GUI-Tests: <c>Aktiv</c> beim Eintrag der offenen Seite, sonst leer.</summary>
    public string Status => IstAktiv ? "Aktiv" : string.Empty;
}

public class NavigationsGruppe
{
    public required string Titel { get; init; }
    public required IReadOnlyList<NavigationsEintrag> Eintraege { get; init; }
}
