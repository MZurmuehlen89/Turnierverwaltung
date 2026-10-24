using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Navigation;

public class NavigationsEintrag
{
    public required string Titel { get; init; }
    public required ICommand Befehl { get; init; }

    /// <summary>Kennung für automatisierte GUI-Tests (#521), Schema <c>Navigation.Ziel</c>.</summary>
    public required string AutomationId { get; init; }
}

public class NavigationsGruppe
{
    public required string Titel { get; init; }
    public required IReadOnlyList<NavigationsEintrag> Eintraege { get; init; }
}
