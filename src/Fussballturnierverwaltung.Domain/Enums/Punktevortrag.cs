namespace Fussballturnierverwaltung.Domain.Enums;

/// <summary>Wie viele Punkte aus der Quellrunde in die Tabelle einer Folgerunde mitgenommen werden (#355):
/// Meister-/Qualifikationsgruppe in Österreich und Belgien. Übernommen werden nur Punkte – Spiele, Siege/Unentschieden/
/// Niederlagen und Tore zählen allein die Folgerunde.</summary>
public enum Punktevortrag
{
    /// <summary>Die Runde startet bei null.</summary>
    Keine = 0,
    /// <summary>Alle Punkte der Quellrunde.</summary>
    Voll = 1,
    /// <summary>Punkte halbiert, ein halber Punkt wird abgerundet (Österreich).</summary>
    HalbiertAbgerundet = 2,
    /// <summary>Punkte halbiert, ein halber Punkt wird aufgerundet (Belgien).</summary>
    HalbiertAufgerundet = 3
}
