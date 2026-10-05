namespace Fussballturnierverwaltung.Domain.Enums;

// Als int gespeichert — Werte nie entfernen oder umsortieren, sonst verrutscht die Bedeutung gespeicherter Zeilen.
public enum AuslosungsRegelArt
{
    KeinGleichesLand,
    KeineGleicheGruppe,
    [Obsolete("Entfallen mit #331 — die Auslosungsart 'Gruppensieger vs. Zweite' deckt das vollständig ab.")]
    GruppensiegerGegenGruppenzweite,
    [Obsolete("Entfallen mit #331 — 'Nach Töpfen'/'Nach Setzliste' paaren bereits Gesetzte gegen Ungesetzte.")]
    GesetzteGegenUngesetzte,
    HeimrechtNachLos,
    HeimrechtNachSetzliste,
    FreilosMoeglich,
    [Obsolete("Entfallen mit #331 — war nie konfigurierbar.")]
    ManuelleEinschraenkung,
    // Issue #356: Teams eines Kontinentalverbands gleichmäßig auf die Gruppen verteilen (FIFA-WM seit 1994).
    Konfoederationsverteilung
}
