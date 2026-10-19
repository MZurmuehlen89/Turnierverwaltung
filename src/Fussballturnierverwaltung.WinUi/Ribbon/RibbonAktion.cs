using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Ribbon;

public sealed class RibbonAktion
{
    public required string Header { get; init; }
    public ICommand? Befehl { get; init; }

    /// <summary>Segoe Fluent Icons Glyph (z.B. "" für Add) für den AppBarButton.</summary>
    public string? IconGlyph { get; init; }

    /// <summary>Aktiv-/An-Zustand des Buttons selbst (nicht zu verwechseln mit <see
    /// cref="RibbonToggleAktion.IstAktiv"/>, das Flyout-Unterpunkte betrifft). Null (Standard) rendert einen
    /// normalen <c>AppBarButton</c> wie bisher; ein gesetzter Wert rendert stattdessen einen
    /// <c>AppBarToggleButton</c> mit sichtbar gedrücktem Zustand (analog zum Dark-Mode-Toggle in
    /// <c>MainWindow.xaml</c>), damit ein An/Aus-Button seinen Zustand nicht nur über den Label-Text zeigen
    /// muss (Issue #325).</summary>
    public bool? IstAktiv { get; init; }

    /// <summary>Alternative zu <see cref="Befehl"/>: der Button öffnet statt einer direkten Aktion ein
    /// Flyout mit an-/abschaltbaren Unterpunkten (Precedent: WPF-Original nutzte dafür eine an eine
    /// Collection gebundene Gallery, z.B. "Auslosungs-Regeln"). Generisch gehalten (kein Domänenbezug),
    /// damit beliebige ViewModels es wiederverwenden können — siehe MainWindow.xaml.cs,
    /// RibbonZusatzGruppenAktualisieren.</summary>
    public IReadOnlyList<RibbonToggleAktion>? ToggleUnterpunkte { get; init; }

    /// <summary>Zweite Alternative zu <see cref="Befehl"/>: der Button öffnet ein Flyout mit einer Liste
    /// direkt ausführbarer Aktionen (kein An/Aus-Zustand wie <see cref="ToggleUnterpunkte"/>, jeder Klick
    /// löst sofort den jeweiligen Befehl aus und schließt das Flyout) — Bündelung mehrerer eng verwandter
    /// Einzelbuttons unter einem gemeinsamen Oberbegriff (z.B. "Simulieren" statt sechs Einzelbuttons für
    /// Runde/Hinrunde/Rückrunde/Gruppe/Spieltag/Auswahl, Nutzerfund: Ribbon-Leiste bei #301 überfüllt).</summary>
    public IReadOnlyList<RibbonUnteraktion>? Unteraktionen { get; init; }
}

/// <summary>Ein Menüpunkt in einem <see cref="RibbonAktion.Unteraktionen"/>-Flyout — im Unterschied zu
/// <see cref="RibbonToggleAktion"/> keine Checkbox, sondern eine sofort ausgeführte Aktion.
/// <paramref name="SperrHinweis"/>: gesetzt = Menüpunkt ausgegraut, der Hinweis steht in Klammern dahinter (#479) —
/// ein Tooltip erschiene an einem gesperrten Menüpunkt nicht.</summary>
public sealed record RibbonUnteraktion(
    string Header, ICommand Befehl, string? IconGlyph = null, string? SperrHinweis = null);

/// <summary>Ein an-/abschaltbarer Menüpunkt in einem <see cref="RibbonAktion.ToggleUnterpunkte"/>-Flyout.
/// Bewusst ohne Binding-Mechanismus (kein <see cref="Microsoft.UI.Xaml.Data.Binding"/>-Objekt wie im
/// WPF-Original) — <paramref name="Setzen"/> wird beim Klick imperativ aufgerufen (analog zu <see
/// cref="RibbonAktion.Befehl"/>, das ebenfalls kein deklaratives Binding braucht).
/// <paramref name="SperrHinweis"/>: gesetzt = Umschalter ausgegraut, der Hinweis erklärt warum (#331).
/// <paramref name="ExklusivGruppe"/>: Umschalter derselben Gruppe schließen sich aus — Anhaken hakt die anderen
/// nur optisch ab, das Speichern der Nebenwirkung übernimmt <paramref name="Setzen"/>.</summary>
public sealed record RibbonToggleAktion(
    string Header, bool IstAktiv, Action<bool> Setzen, string? Beschreibung = null,
    string? SperrHinweis = null, string? ExklusivGruppe = null);
