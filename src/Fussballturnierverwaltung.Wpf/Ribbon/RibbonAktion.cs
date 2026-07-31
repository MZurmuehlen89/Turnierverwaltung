using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.Ribbon;

public sealed class RibbonAktion
{
    public required string Header { get; init; }
    public required ICommand Befehl { get; init; }
    public string? IconGlyph { get; init; }
}
