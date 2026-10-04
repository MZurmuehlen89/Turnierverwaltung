using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class SpielRepositoryTests
{
    private static TurnierverwaltungDbContext NeuerContext(string datenbank) =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(datenbank)
            .Options);

    /// <summary>Issue #414/#103: Die Runde wird in der Datenbank mit einer Anweisung am Change-Tracker vorbei
    /// gelöscht. Der InMemory-Provider kennt kein ExecuteDelete — das Löschen "von außen" stellt hier ein
    /// zweiter Kontext auf derselben Datenbank nach.</summary>
    [Fact]
    public async Task AusKontextNehmen_EntferntAussenGeloeschteSpieleSamtErgebnisUndAusSammlungen()
    {
        string datenbank = Guid.NewGuid().ToString();
        using (var anlegen = NeuerContext(datenbank))
        {
            anlegen.TurnierRunden.AddRange(
                new TurnierRunde { Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1, Rundenart = Rundenart.KoRunde },
                new TurnierRunde { Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2, Rundenart = Rundenart.Finale });
            anlegen.Spiele.AddRange(
                new Spiel
                {
                    Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 1,
                    Ergebnis = new SpielErgebnis { Id = 1, HeimToreRegulaer = 2, AuswaertsToreRegulaer = 1 }
                },
                new Spiel { Id = 2, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 2 },
                new Spiel { Id = 3, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 3 });
            await anlegen.SaveChangesAsync();
        }

        using var ctx = NeuerContext(datenbank);
        var repo = new SpielRepository(ctx);
        var halbfinale = await ctx.TurnierRunden
            .Include(r => r.Spiele).ThenInclude(s => s.Ergebnis)
            .SingleAsync(r => r.Id == 1);
        var finalSpiel = await ctx.Spiele.SingleAsync(s => s.Id == 3);
        finalSpiel.Spielnummer = 30; // ungespeicherte Änderung an einem unbeteiligten Spiel

        using (var aussen = NeuerContext(datenbank))
        {
            aussen.Spiele.RemoveRange(await aussen.Spiele.Where(s => s.TurnierRundeId == 1).ToListAsync());
            await aussen.SaveChangesAsync();
        }

        repo.AusKontextNehmen(new HashSet<int> { 1, 2 });

        ctx.ChangeTracker.Entries<Spiel>().Select(e => e.Entity.Id).Should().Equal(3);
        ctx.ChangeTracker.Entries<SpielErgebnis>().Should().BeEmpty();
        halbfinale.Spiele.Should().BeEmpty();
        (await ctx.Spiele.FindAsync(1)).Should().BeNull("das Spiel ist gelöscht und darf nicht mehr aus dem Kontext kommen");

        // Die unbeteiligte Änderung bleibt erhalten und lässt sich speichern.
        await ctx.SaveChangesAsync();
        (await repo.AlleNachRundeAsync(1)).Should().BeEmpty();
        using var pruefen = NeuerContext(datenbank);
        (await pruefen.Spiele.SingleAsync()).Spielnummer.Should().Be(30);
    }
}
