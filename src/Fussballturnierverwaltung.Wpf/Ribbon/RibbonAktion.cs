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

    /// <summary>Dateiname einer Vektor-Icon-Ressource unter Resources/Icons/ (IconExperience-Icon-Pack,
    /// z.B. "trophy.xaml") — hat Vorrang vor <see cref="IconGlyph"/>, falls beide gesetzt sind. Jede
    /// Datei ist ein eigenständiges Viewbox/Canvas/Path-Fragment ohne x:Class, wird als loses XAML
    /// (Build Action "Page") kompiliert und zur Laufzeit per Application.LoadComponent geladen.</summary>
    public string? IconVektorDatei { get; init; }

    /// <summary>Feste, eigenständig ausführbare Unteraktionen im Dropdown (z.B. "mit TV-Modus
    /// ausführen"), jede mit eigenem Befehl — kein Umschalt-/Checkbox-Zustand. Ist zusätzlich
    /// <see cref="Befehl"/> gesetzt, wird ein SplitButton erzeugt (Klick = Befehl, Pfeil = Dropdown),
    /// sonst ein reiner DropDownButton.</summary>
    public IReadOnlyList<RibbonAktion>? Unteraktionen { get; init; }

    /// <summary>Bindung auf eine dynamische Collection (z.B. Auslosungs-Regeln), deren Elemente per
    /// implizitem DataTemplate (Window.Resources) im Dropdown dargestellt werden. Erzeugt einen reinen
    /// DropDownButton (kein Befehl).</summary>
    public Binding? ItemsQuelleBinding { get; init; }

    /// <summary>TwoWay-Bindung auf eine bool-Eigenschaft für einen Umschalter statt eines reinen
    /// Klick-Befehls (z.B. "Platzhalter-Vorschau", Issue #208). Erzeugt einen Fluent.ToggleButton mit
    /// Binding auf IsChecked, kein <see cref="Befehl"/> und keine <see cref="Unteraktionen"/> nötig.</summary>
    public Binding? IstAktivBinding { get; init; }

    /// <summary>Optionale Bindung auf IsEnabled für einen per <see cref="IstAktivBinding"/> erzeugten
    /// Umschalter (z.B. "Platzhalter-Vorschau" bleibt deaktiviert, solange die aktuelle Runde keine
    /// Platzhalter-Spiele hat — Nutzer-Feedback 2026-08-21). Ohne Befehl gibt es sonst kein CanExecute,
    /// über das sich das automatisch ergäbe.</summary>
    public Binding? IstAktivierbarBinding { get; init; }
}
