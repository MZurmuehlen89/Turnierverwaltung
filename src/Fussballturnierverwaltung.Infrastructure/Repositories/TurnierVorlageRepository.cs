using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class TurnierVorlageRepository(TurnierverwaltungDbContext context)
    : GenericRepository<TurnierVorlage>(context), ITurnierVorlageRepository
{
    public override async Task<IReadOnlyList<TurnierVorlage>> AlleFindenAsync(CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .OrderBy(v => v.VorlagenKategorie)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TurnierVorlage>> AlleNachKategorieFindenAsync(VorlagenKategorie kategorie, CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .Where(v => v.VorlagenKategorie == kategorie)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

    public async Task<TurnierVorlage?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.TurnierVorlagen
            .Include(v => v.VorlagenRunden.OrderBy(r => r.Reihenfolge))
                .ThenInclude(r => r.Regelwerk)
            .Include(v => v.VorlagenRunden)
                .ThenInclude(r => r.AusgehendeUebergangsregeln)
                    .ThenInclude(regel => regel.ZielVorlagenRunde)
            .Include(v => v.VorlagenRunden)
                .ThenInclude(r => r.Paarungen)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task RundenErsetzenAsync(
        int vorlageId,
        IReadOnlyList<VorlagenRunde> neueRunden,
        IReadOnlyList<VorlagenUebergangsRegelEingabe> neueUebergangsregeln,
        IReadOnlyList<VorlagenPaarungEingabe>? neuePaarungen = null,
        CancellationToken ct = default)
    {
        var alteRegeln = await Context.VorlagenUebergangsregeln
            .Where(r => r.QuellVorlagenRunde.TurnierVorlageId == vorlageId)
            .ToListAsync(ct);
        Context.VorlagenUebergangsregeln.RemoveRange(alteRegeln);

        // Kein separates Entfernen der alten VorlagenPaarungen nötig: sie referenzieren ausschließlich ihre
        // eigene VorlagenRunde (Cascade), anders als die Übergangsregeln oben mit ihrer zweiten Restrict-FK
        // auf die Zielrunde — das serverseitige ON DELETE CASCADE der Runden-Löschung unten reicht aus.
        var alteRunden = await Context.VorlagenRunden
            .Where(r => r.TurnierVorlageId == vorlageId)
            .ToListAsync(ct);
        Context.VorlagenRunden.RemoveRange(alteRunden);
        await Context.SaveChangesAsync(ct);

        if (neueRunden.Count > 0)
        {
            Context.VorlagenRunden.AddRange(neueRunden);
            await Context.SaveChangesAsync(ct);
        }

        if (neueUebergangsregeln.Count > 0)
        {
            var idNachReihenfolge = neueRunden.ToDictionary(r => r.Reihenfolge, r => r.Id);
            var regeln = neueUebergangsregeln.Select(e => new VorlagenUebergangsRegel
            {
                QuellVorlagenRundeId = idNachReihenfolge[e.QuellReihenfolge],
                ZielVorlagenRundeId  = idNachReihenfolge[e.ZielReihenfolge],
                GruppeIndex          = e.GruppeIndex,
                PlatzierungVon       = e.PlatzierungVon,
                PlatzierungBis       = e.PlatzierungBis,
                AnzahlBeste          = e.AnzahlBeste
            }).ToList();
            Context.VorlagenUebergangsregeln.AddRange(regeln);
            await Context.SaveChangesAsync(ct);
        }

        if (neuePaarungen is { Count: > 0 })
        {
            var idNachReihenfolge = neueRunden.ToDictionary(r => r.Reihenfolge, r => r.Id);
            var paarungen = neuePaarungen.Select(e => new VorlagenPaarung
            {
                VorlagenRundeId       = idNachReihenfolge[e.RundenReihenfolge],
                PaarungsNummer        = e.PaarungsNummer,
                SlotNr                = e.SlotNr,
                PlatzhalterBezeichnung = e.PlatzhalterBezeichnung
            }).ToList();
            Context.VorlagenPaarungen.AddRange(paarungen);
            await Context.SaveChangesAsync(ct);
        }
    }
}
