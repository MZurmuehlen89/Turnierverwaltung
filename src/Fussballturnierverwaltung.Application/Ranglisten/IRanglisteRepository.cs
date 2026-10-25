using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten;

public interface IRanglisteRepository : IRepository<Rangliste>
{
    Task<IReadOnlyList<Rangliste>> AlleFindenMitEintraegenAsync(CancellationToken ct = default);
    Task<Rangliste?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default);

    /// <summary>Platz je Mannschaft in der Rangliste <paramref name="ranglisteId"/> (Issue #354); leer, wenn es sie
    /// nicht gibt.</summary>
    Task<IReadOnlyDictionary<int, int>> PlaetzeAsync(int ranglisteId, CancellationToken ct = default);

    /// <summary>Die Rangliste samt Einträgen und Abschnitten, ohne Mannschaften und ohne Änderungsverfolgung
    /// (Issue #393); <c>null</c>, wenn es sie nicht gibt.</summary>
    Task<Rangliste?> MitAbschnittenAsync(int ranglisteId, CancellationToken ct = default);

    /// <summary>Alle Ranglisten mit Abschnitten (Ligapyramiden) samt Einträgen, ohne Mannschaften und ohne
    /// Änderungsverfolgung (Issue #384).</summary>
    Task<IReadOnlyList<Rangliste>> LigapyramidenAsync(CancellationToken ct = default);

    Task SpeichernAenderungenAsync(CancellationToken ct = default);
}
