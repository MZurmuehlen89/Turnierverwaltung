namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Zeigt alle Spiele einer Mannschaft im Turnier — Ziel des Kontextmenüs „Alle Spiele des Teams anzeigen“
/// (<see cref="Views.MannschaftsKontextmenue"/>, #479).</summary>
public interface IMannschaftsSpieleAnzeige
{
    Task MannschaftsSpieleAnzeigenAsync(int mannschaftId);
}
