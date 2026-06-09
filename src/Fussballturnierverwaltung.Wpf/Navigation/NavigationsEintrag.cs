using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.Navigation;

public class NavigationsEintrag
{
    public required string Titel { get; init; }
    public required ICommand Befehl { get; init; }
}

public class NavigationsGruppe
{
    public required string Titel { get; init; }
    public required IReadOnlyList<NavigationsEintrag> Eintraege { get; init; }
}
