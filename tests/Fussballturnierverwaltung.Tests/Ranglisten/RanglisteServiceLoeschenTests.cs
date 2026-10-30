using FluentAssertions;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

/// <summary>Löschen einer Rangliste, auf die ein Verband als Vereinsrangliste verweist (#574): dieser Fremdschlüssel hat
/// kein ON DELETE SET NULL, das Repository löst den Verweis selbst.</summary>
public class RanglisteServiceLoeschenTests
{
    private readonly TurnierverwaltungDbContext _context = new(
        new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Loeschen_LoestVereinsranglisteDesVerbands()
    {
        _context.Ranglisten.AddRange(new Rangliste { Id = 1, Name = "Klub-Koeffizient" }, new Rangliste { Id = 2, Name = "FIFA" });
        _context.Verbaende.AddRange(
            new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA", RanglisteId = 2, VereinsRanglisteId = 1 },
            new Verband { Id = 2, Name = "CAF", Kurzname = "CAF", RanglisteId = 2, VereinsRanglisteId = 2 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var service = new RanglisteService(new RanglisteRepository(_context), new RanglisteValidator(), NullLogger<RanglisteService>.Instance);

        await service.LoeschenAsync(1);

        _context.ChangeTracker.Clear();
        var verbaende = await _context.Verbaende.OrderBy(v => v.Id).ToListAsync();
        verbaende.Select(v => (v.Kurzname, v.RanglisteId, v.VereinsRanglisteId))
            .Should().Equal(("UEFA", 2, (int?)null), ("CAF", 2, 2));
        (await _context.Ranglisten.AnyAsync(r => r.Id == 1)).Should().BeFalse();
    }
}
