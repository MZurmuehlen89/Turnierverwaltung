using System.Windows.Data;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.Ribbon;

public sealed class RibbonAktion
{
    public required string Header { get; init; }

    /// <summary>Primäraktion beim Klick auf den Button-Kopf. Bei reinen Dropdown-Aktionen (nur
    /// <see cref="ItemsQuelleBinding"/> gesetzt, kein <see cref="Unteraktionen"/>) optional/null.</summary>
    public ICommand? Befehl { get; init; }

    public string? IconGlyph { get; init; }

    /// <summary>Feste, eigenständig ausführbare Unteraktionen im Dropdown (z.B. "mit TV-Modus
    /// ausführen"), jede mit eigenem Befehl — kein Umschalt-/Checkbox-Zustand. Ist zusätzlich
    /// <see cref="Befehl"/> gesetzt, wird ein SplitButton erzeugt (Klick = Befehl, Pfeil = Dropdown),
    /// sonst ein reiner DropDownButton.</summary>
    public IReadOnlyList<RibbonAktion>? Unteraktionen { get; init; }

    /// <summary>Bindung auf eine dynamische Collection (z.B. Auslosungs-Regeln), deren Elemente per
    /// implizitem DataTemplate (Window.Resources) im Dropdown dargestellt werden. Erzeugt einen reinen
    /// DropDownButton (kein Befehl).</summary>
    public Binding? ItemsQuelleBinding { get; init; }
}
