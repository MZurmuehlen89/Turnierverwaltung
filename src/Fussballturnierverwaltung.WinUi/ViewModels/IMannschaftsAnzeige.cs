namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ziel des Kontextmenüs an einer Mannschaft im Turnier (<see cref="Views.MannschaftsKontextmenue"/>): alle
/// Spiele der Mannschaft im Spielplan (#479) bzw. ihre Statistik (#324).</summary>
public interface IMannschaftsAnzeige
{
    Task MannschaftsSpieleAnzeigenAsync(int mannschaftId);

    void MannschaftsStatistikAnzeigen(int mannschaftId);
}
