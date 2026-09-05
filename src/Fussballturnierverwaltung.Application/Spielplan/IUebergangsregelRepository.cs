using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface IUebergangsregelRepository : IRepository<Uebergangsregel>
{
    Task<IReadOnlyList<Uebergangsregel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Uebergangsregel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default);
    Task<IReadOnlyList<Uebergangsregel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default);
    Task<Uebergangsregel?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default);

    /// <summary>Vollersatz-Semantik (Issue #294, Nachfolger von <c>TurnierVorlageRepository.RundenErsetzenAsync</c>s
    /// Übergangsregel-Teil): synchronisiert per Schlüssel-Diff (Quell-/Zielrunde+Gruppe/Platzierung als
    /// Identität) gegen den vollständigen beabsichtigten Endzustand über alle <paramref name="runden"/> hinweg —
    /// nur neue Kombinationen werden angelegt, nur weggefallene entfernt.</summary>
    Task<IReadOnlyList<Uebergangsregel>> AlleErsetzenAsync(
        IReadOnlyList<TurnierRunde> runden,
        IReadOnlyList<UebergangsregelPositionsEingabe> neueRegeln,
        CancellationToken ct = default);
}
