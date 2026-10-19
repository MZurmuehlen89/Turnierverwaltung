namespace Fussballturnierverwaltung.Domain.Enums;

// Wie die Position einer Mannschaft innerhalb ihrer Gruppe entsteht (Issue #475). Die Position bestimmt den
// Spielplan der Gruppe (Berger-Schema, reihenfolgeabhängig). Gesetzte Teams (Nach Setzliste) stehen immer auf
// Position 1. Als int gespeichert — Werte nie entfernen oder umsortieren.
public enum Gruppenposition
{
    // Topf 1 → Position 1, Topf 2 → Position 2 … (Copa América 2024)
    NachTopf,
    // Topf 1 auf Position 1, die übrigen Positionen gelost (FIFA-WM 2022)
    Topf1Fest,
    // Alle Positionen gelost (UEFA EURO 2024, Champions-League-Gruppenphase)
    AlleGelost
}
