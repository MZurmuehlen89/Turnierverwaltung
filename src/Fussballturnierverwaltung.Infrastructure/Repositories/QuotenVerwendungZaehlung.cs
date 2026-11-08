using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

/// <summary>Zählt Turniere und Vorlagen getrennt, deren Quoten einen Verband bzw. ein Land nennen (#559).</summary>
internal static class QuotenVerwendungZaehlung
{
    public static async Task<QuotenVerwendung> ZaehlenAsync(IQueryable<TurnierInstanz> mitQuote, CancellationToken ct) => new(
        await mitQuote.CountAsync(t => t.Status != TurnierStatus.Vorlage, ct),
        await mitQuote.CountAsync(t => t.Status == TurnierStatus.Vorlage, ct));
}
