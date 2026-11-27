namespace Fussballturnierverwaltung.WinUi.Navigation;

/// <summary>
/// Ein ViewModel, dessen Seite (bzw. aktiver Reiter) ein Hintergrundbild im Inhaltsbereich zeigt (#269).
/// Die Seite sagt selbst, welches Bild zu ihr gehört; <c>MainViewModel</c> reicht es ans Fenster durch.
/// Ändert sich der Wert (Reiterwechsel), meldet das ViewModel <c>PropertyChanged(Bereichsbild)</c>.
/// </summary>
public interface IHatBereichsbild
{
    /// <summary>Dateiname ohne Endung unter <c>Assets/Bereiche/</c> (z. B. <c>bereich-mannschaften</c>).</summary>
    string? Bereichsbild { get; }
}
