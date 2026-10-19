using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten;

public interface IRanglisteRepository : IRepository<Rangliste>
{
    Task<IReadOnlyList<Rangliste>> AlleFindenMitEintraegenAsync(CancellationToken ct = default);
    Task<Rangliste?> FindenNachIdMitEintraegenAsync(int id, CancellationToken ct = default);

    /// <summary>Plätze der neuesten Rangliste aus der Quelle <paramref name="quelleId"/> je Mannschaft — Auswahl
    /// wie <see cref="Auslosungen.LostopfVorbelegung.NeuesteRanglisteZurQuelle"/>; leer, wenn aus der Quelle noch
    /// nichts importiert wurde (Issue #354).</summary>
    Task<IReadOnlyDictionary<int, int>> PlaetzeDerNeuestenZurQuelleAsync(int quelleId, CancellationToken ct = default);

    /// <summary>Die neueste Rangliste aus der Quelle <paramref name="quelleId"/> samt Einträgen und Abschnitten, ohne
    /// Mannschaften und ohne Änderungsverfolgung (Issue #393); <c>null</c>, wenn es aus der Quelle noch keine gibt.</summary>
    Task<Rangliste?> NeuesteZurQuelleMitAbschnittenAsync(int quelleId, CancellationToken ct = default);

    Task SpeichernAenderungenAsync(CancellationToken ct = default);
}
