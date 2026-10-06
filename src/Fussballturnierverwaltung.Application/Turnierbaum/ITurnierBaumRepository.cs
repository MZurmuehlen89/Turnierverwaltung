using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;

namespace Fussballturnierverwaltung.Application.Turnierbaum;

public interface ITurnierBaumRepository : IRepository<TurnierBaum>
{
    Task<TurnierBaum?> FindenNachInstanzAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<TurnierBaum?> FindenNachInstanzMitKnotenAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>Der Baum mit seinen Knoten, aber ohne deren Spiele — genügt, um den Aufbau zu vergleichen.</summary>
    Task<TurnierBaum?> FindenNachInstanzMitAufbauAsync(int turnierInstanzId, CancellationToken ct = default);

    /// <summary>Persistiert alle Knoten und gibt sie mit gesetzten IDs zurück.</summary>
    Task KnotenSpeichernAsync(IEnumerable<TurnierBaumKnoten> knoten, CancellationToken ct = default);

    /// <summary>Aktualisiert Eltern-/Kind-Verknüpfungen und WurzelKnotenId in einem Schritt.</summary>
    Task VerknuepfungenAktualisierenAsync(TurnierBaum baum, IEnumerable<TurnierBaumKnoten> knoten, CancellationToken ct = default);

    /// <summary>Löscht den Baum vollständig inkl. aller Knoten (zirkuläre Referenzen werden vorher aufgelöst).</summary>
    Task LoeschenMitKnotenAsync(int turnierBaumId, CancellationToken ct = default);

    /// <summary>Setzt SpielId aller Baumknoten für eine Runde auf null (vor dem Löschen der Spiele).</summary>
    Task SpielIdsNullSetzenAsync(int turnierRundeId, CancellationToken ct = default);
}
