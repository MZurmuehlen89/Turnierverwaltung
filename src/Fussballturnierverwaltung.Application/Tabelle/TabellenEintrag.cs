using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

public sealed class TabellenEintrag
{
    public required Mannschaft Mannschaft { get; init; }
    public required int Spiele { get; init; }
    public required int Siege { get; init; }
    public required int Unentschieden { get; init; }
    public required int Niederlagen { get; init; }
    public required int Tore { get; init; }
    public required int Gegentore { get; init; }
    public required int Punkte { get; init; }
    public int Tordifferenz => Tore - Gegentore;
    public int Rang { get; set; }
    public Qualifikationsstatus Qualifikationsstatus { get; set; } = Qualifikationsstatus.Offen;

    /// <summary>Der Rang kann sich durch kein Ergebnis der verbleibenden offenen Spiele mehr ändern
    /// (Issue #202) — stärker als <see cref="Qualifikationsstatus"/>, das nur die Zonen-Zugehörigkeit
    /// (qualifiziert/ausgeschieden) absichert, nicht den exakten Platz innerhalb der Zone.</summary>
    public bool SicherPositioniert { get; set; }
}
