using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Lädt alle Turniervorlagen als <see cref="VorlagenMuster"/> für den Vorschlagsassistenten (#167, Quelle A).
/// Reiner Lesezugriff über die vorhandenen Dienste, kein eigener Zustand.</summary>
public sealed class UebergangsVorlagenQuelle(TurnierInstanzService instanzService, UebergangsregelService uebergangsregelService)
{
    public async Task<IReadOnlyList<VorlagenMuster>> LadenAsync(CancellationToken ct = default)
    {
        var muster = new List<VorlagenMuster>();
        foreach (var kopf in (await instanzService.AlleAbrufenAsync(ct)).Where(i => i.Status == TurnierStatus.Vorlage))
        {
            var vorlage = await instanzService.FindenNachIdMitRundenAsync(kopf.Id, ct);
            if (vorlage is null) continue;
            muster.Add(VorlagenMuster.Aus(vorlage, await uebergangsregelService.AlleNachTurnierInstanzIdAsync(kopf.Id, ct)));
        }
        return muster;
    }
}
