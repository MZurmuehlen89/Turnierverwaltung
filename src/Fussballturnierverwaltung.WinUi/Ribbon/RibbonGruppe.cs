namespace Fussballturnierverwaltung.WinUi.Ribbon;

public sealed class RibbonGruppe
{
    public required string Header { get; init; }
    public required IReadOnlyList<RibbonAktion> Aktionen { get; init; }
}
