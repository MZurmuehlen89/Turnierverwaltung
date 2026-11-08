using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

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

    /// <summary>Buchholz-Zahl (Schweizer System): Summe der aktuellen Punkte aller bisherigen Gegner —
    /// Standard-Tiebreaker bei Punktgleichheit, höherer Wert deutet auf eine stärkere Gegnerschaft hin.</summary>
    public int Buchholz { get; set; }

    /// <summary>Der Rang kann sich durch kein Ergebnis der verbleibenden offenen Spiele mehr ändern
    /// (Issue #202). Ob nur die Zone feststeht, zeigt <see cref="PunktFarbe"/>.</summary>
    public bool SicherPositioniert { get; set; }

    /// <summary>Bester und schlechtester Rang, der mit den verbleibenden offenen Spielen noch möglich ist
    /// (Issue #358) — <c>null</c>, solange das nicht bestimmbar ist (offene Platzhalter, historischer
    /// Tabellenstand, Teilansichten).</summary>
    public int? BesterRang { get; set; }
    public int? SchlechtesterRang { get; set; }

    /// <summary>Farbe der Tabellenzone des aktuellen Rangs (Issue #358), <c>null</c> = keine Zone.</summary>
    public ZonenFarbe? ZonenFarbe { get; set; }

    /// <summary>Farbe des Sicher-Punkts (Issue #358): gesetzt, sobald feststeht, in welche Zielrunde das Team
    /// kommt (deren Zonenfarbe) bzw. dass es ausscheidet (<see cref="Tabelle.ZonenFarbe.Rot"/>).</summary>
    public ZonenFarbe? PunktFarbe { get; set; }
}
