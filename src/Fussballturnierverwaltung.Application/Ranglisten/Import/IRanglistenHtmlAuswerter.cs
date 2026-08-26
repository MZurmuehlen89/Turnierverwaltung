using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

public interface IRanglistenHtmlAuswerter
{
    IReadOnlyList<RanglistenRohZeile> Auswerten(string html, RanglistenQuelle quelle);

    /// <summary>Ermittelt alle Tabellen einer Seite mit Kurzvorschau (Issue #225) — Grundlage für
    /// die interaktive Tabellen-/Spaltenauswahl in WPF- und Console-Dialog.</summary>
    IReadOnlyList<RanglistenTabellenVorschau> TabellenErmitteln(string html);
}
