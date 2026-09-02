using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Ribbon;

public sealed class RibbonAktion
{
    public required string Header { get; init; }
    public ICommand? Befehl { get; init; }

    /// <summary>Segoe Fluent Icons Glyph (z.B. "" für Add) für den AppBarButton.</summary>
    public string? IconGlyph { get; init; }
}
