using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Ribbon;

public sealed class RibbonAktion
{
    public required string Header { get; init; }
    public ICommand? Befehl { get; init; }

    /// <summary>Segoe Fluent Icons Glyph (z.B. "" für Add) für den AppBarButton.</summary>
    public string? IconGlyph { get; init; }

    /// <summary>Alternative zu <see cref="Befehl"/>: der Button öffnet statt einer direkten Aktion ein
    /// Flyout mit an-/abschaltbaren Unterpunkten (Precedent: WPF-Original nutzte dafür eine an eine
    /// Collection gebundene Gallery, z.B. "Auslosungs-Regeln"). Generisch gehalten (kein Domänenbezug),
    /// damit beliebige ViewModels es wiederverwenden können — siehe MainWindow.xaml.cs,
    /// RibbonZusatzGruppenAktualisieren.</summary>
    public IReadOnlyList<RibbonToggleAktion>? ToggleUnterpunkte { get; init; }
}

/// <summary>Ein an-/abschaltbarer Menüpunkt in einem <see cref="RibbonAktion.ToggleUnterpunkte"/>-Flyout.
/// Bewusst ohne Binding-Mechanismus (kein <see cref="Microsoft.UI.Xaml.Data.Binding"/>-Objekt wie im
/// WPF-Original) — <paramref name="Setzen"/> wird beim Klick imperativ aufgerufen (analog zu <see
/// cref="RibbonAktion.Befehl"/>, das ebenfalls kein deklaratives Binding braucht).</summary>
public sealed record RibbonToggleAktion(string Header, bool IstAktiv, Action<bool> Setzen, string? Beschreibung = null);
