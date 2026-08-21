using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

public interface ITurnierVorlageRepository : IRepository<TurnierVorlage>
{
    Task<IReadOnlyList<TurnierVorlage>> AlleNachKategorieFindenAsync(VorlagenKategorie kategorie, CancellationToken ct = default);
    Task<TurnierVorlage?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default);

    /// <summary>Ersetzt alle Runden einer Vorlage sowie (Issue #166) ihre Vorlagen-Übergangsregeln in einem
    /// Zug — <paramref name="neueUebergangsregeln"/> referenziert Runden über die 1-basierte Reihenfolge
    /// innerhalb <paramref name="neueRunden"/>, da die neuen Runden erst nach dem Insert reale Ids haben.</summary>
    Task RundenErsetzenAsync(
        int vorlageId,
        IReadOnlyList<VorlagenRunde> neueRunden,
        IReadOnlyList<VorlagenUebergangsRegelEingabe> neueUebergangsregeln,
        CancellationToken ct = default);
}
