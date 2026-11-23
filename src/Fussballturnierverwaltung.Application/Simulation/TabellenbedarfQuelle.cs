using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Holt für einen Simulationsvorgang, was der Tabellenbedarf (#176) über die Spiele hinaus braucht – je Runde mit Tabelle
/// ihre Zonen (Übergangsregeln und Tabellenbereiche) und ihr Punktesystem – und baut daraus den <see cref="TabellenbedarfVerlauf"/>.
/// Das Schweizer System bleibt außen vor: seine späteren Spieltage sind noch nicht angesetzt, die offenen Spiele also unbekannt.</summary>
public class TabellenbedarfQuelle(
    ITurnierRundeRepository rundeRepository, IUebergangsregelRepository uebergangsregelRepository, IRegelwerkRepository regelwerkRepository)
{
    private static readonly PunkteRegel DREI_EINS_NULL = new() { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

    /// <summary>Ob die Runde einen Bedarf kennen kann: eine Tabelle mit festem Spielplan.</summary>
    public static bool KenntBedarf(Rundenart rundenart) =>
        TabellenZonen.HatTabelle(rundenart) && rundenart != Rundenart.SchweizerSystem;

    public async Task<TabellenbedarfVerlauf> AnlegenAsync(IReadOnlyList<Spiel> spiele, CancellationToken ct = default)
    {
        var grundlagen = new Dictionary<int, TabellenbedarfGrundlage>();
        foreach (var runde in spiele.Select(s => s.TurnierRunde).Where(r => r is not null && KenntBedarf(r.Rundenart))
                     .DistinctBy(r => r.Id))
        {
            var zonen = TabellenZonen.Ermitteln(
                await uebergangsregelRepository.AlleNachQuellRundeIdAsync(runde.Id, ct),
                await rundeRepository.TabellenBereicheAsync(runde.Id, ct));
            if (zonen.Count > 0)
                grundlagen[runde.Id] = new TabellenbedarfGrundlage(zonen, await PunkteRegelAsync(runde, ct));
        }
        return new TabellenbedarfVerlauf(spiele, grundlagen);
    }

    /// <summary>Wie die Tabelle der Runde: das Regelwerk der Runde, sonst das Standard-Regelwerk des Turniers (AD-026), dessen
    /// erste Punkteregel – ohne eine gilt 3/1/0.</summary>
    private async Task<PunkteRegel> PunkteRegelAsync(TurnierRunde runde, CancellationToken ct)
    {
        int? regelwerkId = runde.RegelwerkId ?? await rundeRepository.StandardRegelwerkIdDerInstanzAsync(runde.TurnierInstanzId, ct);
        var regelwerk = regelwerkId is int id ? await regelwerkRepository.FindenNachIdMitAllenRegelnAsync(id, ct) : null;
        return regelwerk?.PunkteRegeln.FirstOrDefault() ?? DREI_EINS_NULL;
    }
}
