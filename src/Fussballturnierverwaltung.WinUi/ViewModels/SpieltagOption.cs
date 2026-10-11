namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eintrag der Spieltag-Auswahl (Issue #363); <see cref="Nummer"/> <c>null</c> = "Alle Spieltage".</summary>
public sealed class SpieltagOption(int? nummer, string anzeige)
{
    public int? Nummer { get; } = nummer;
    public string Anzeige { get; } = anzeige;
}
