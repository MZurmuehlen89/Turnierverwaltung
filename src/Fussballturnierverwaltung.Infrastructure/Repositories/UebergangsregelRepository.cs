using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class UebergangsregelRepository(TurnierverwaltungDbContext context)
    : GenericRepository<Uebergangsregel>(context), IUebergangsregelRepository
{
    public async Task<IReadOnlyList<Uebergangsregel>> AlleNachQuellRundeIdAsync(int quellRundeId, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.ZielRunde)
            .Where(r => r.QuellRundeId == quellRundeId)
            .OrderBy(r => r.GruppeIndex)
                .ThenBy(r => r.PlatzierungVon)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Uebergangsregel>> AlleNachZielRundeIdAsync(int zielRundeId, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.QuellRunde)
            .Where(r => r.ZielRundeId == zielRundeId)
            .OrderBy(r => r.GruppeIndex)
                .ThenBy(r => r.PlatzierungVon)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Uebergangsregel>> AlleNachTurnierInstanzIdAsync(int turnierInstanzId, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.QuellRunde)
            .Include(r => r.ZielRunde)
            .Where(r => r.QuellRunde.TurnierInstanzId == turnierInstanzId)
            .ToListAsync(ct);

    public async Task<Uebergangsregel?> FindenNachIdMitRundenAsync(int id, CancellationToken ct = default)
        => await Context.Uebergangsregeln
            .Include(r => r.QuellRunde)
            .Include(r => r.ZielRunde)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <summary>Vollersatz per Schlüssel-Diff (Issue #294, Nachfolger des Übergangsregel-Teils von
    /// <c>TurnierVorlageRepository.RundenErsetzenAsync</c>) — Identität = Quell-/Zielrunde+Gruppe/Platzierung,
    /// nur neue Kombinationen anlegen, nur weggefallene entfernen, unveränderte bleiben unangetastet.</summary>
    public async Task<IReadOnlyList<Uebergangsregel>> AlleErsetzenAsync(
        IReadOnlyList<TurnierRunde> runden,
        IReadOnlyList<UebergangsregelPositionsEingabe> neueRegeln,
        CancellationToken ct = default)
    {
        var idNachReihenfolge = runden.ToDictionary(r => r.Reihenfolge, r => r.Id);
        var rundenIds = idNachReihenfolge.Values.ToHashSet();

        var alteRegeln = await Context.Uebergangsregeln
            .Where(r => rundenIds.Contains(r.QuellRundeId))
            .ToListAsync(ct);
        var alteRegelSchluessel = alteRegeln
            .ToDictionary(r => (r.QuellRundeId, r.ZielRundeId, r.GruppeIndex, r.PlatzierungVon, r.PlatzierungBis, r.AnzahlBeste));

        var neueRegelSchluessel = new HashSet<(int Quell, int Ziel, int? GruppeIndex, int Von, int Bis, int? AnzahlBeste)>();
        foreach (var e in neueRegeln)
        {
            (int Quell, int Ziel, int? GruppeIndex, int Von, int Bis, int? AnzahlBeste) schluessel =
                (idNachReihenfolge[e.QuellReihenfolge], idNachReihenfolge[e.ZielReihenfolge], e.GruppeIndex, e.PlatzierungVon, e.PlatzierungBis, e.AnzahlBeste);
            neueRegelSchluessel.Add(schluessel);
            if (!alteRegelSchluessel.ContainsKey(schluessel))
                Context.Uebergangsregeln.Add(new Uebergangsregel
                {
                    QuellRundeId   = schluessel.Quell,
                    ZielRundeId    = schluessel.Ziel,
                    GruppeIndex    = schluessel.GruppeIndex,
                    PlatzierungVon = schluessel.Von,
                    PlatzierungBis = schluessel.Bis,
                    AnzahlBeste    = schluessel.AnzahlBeste
                });
        }

        var entfernteRegeln = alteRegeln
            .Where(r => !neueRegelSchluessel.Contains((r.QuellRundeId, r.ZielRundeId, r.GruppeIndex, r.PlatzierungVon, r.PlatzierungBis, r.AnzahlBeste)))
            .ToList();
        Context.Uebergangsregeln.RemoveRange(entfernteRegeln);

        await Context.SaveChangesAsync(ct);

        return await Context.Uebergangsregeln
            .Where(r => rundenIds.Contains(r.QuellRundeId))
            .ToListAsync(ct);
    }
}
