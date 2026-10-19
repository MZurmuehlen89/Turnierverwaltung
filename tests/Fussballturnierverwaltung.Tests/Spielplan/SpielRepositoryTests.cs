using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
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

    // ── Mannschaftsansicht (#479) ────────────────────────────────────────────

    /// <summary>Zwei Turniere: im ersten spielt Team 1 in der Gruppenphase (Runde 1), im Halbfinale (Runde 2, die
    /// Runde ist bewusst zuerst angelegt) und im Finale gegen einen offenen Gegner; Team 3 hat kein Spiel. Im
    /// zweiten Turnier spielt Team 1 ebenfalls.</summary>
    private static async Task<string> TurniereMitMannschaftenAnlegenAsync()
    {
        string datenbank = Guid.NewGuid().ToString();
        using var anlegen = NeuerContext(datenbank);
        var land = new Land { Id = 1, Name = "Land" };
        anlegen.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Zebras", Land = land },
            new Mannschaft { Id = 2, Name = "Adler", Land = land },
            new Mannschaft { Id = 3, Name = "Bären", Land = land },
            new Mannschaft { Id = 4, Name = "Füchse", Land = land });
        anlegen.TurnierRunden.AddRange(
            new TurnierRunde { Id = 2, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2, Rundenart = Rundenart.KoRunde },
            new TurnierRunde { Id = 1, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 1, Rundenart = Rundenart.Gruppenphase },
            new TurnierRunde { Id = 3, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 3, Rundenart = Rundenart.Finale },
            new TurnierRunde { Id = 4, TurnierInstanzId = 2, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga });
        anlegen.Spiele.AddRange(
            new Spiel { Id = 10, TurnierInstanzId = 1, TurnierRundeId = 2, Spielnummer = 5, HeimMannschaftId = 1, AuswaertsMannschaftId = 2 },
            new Spiel { Id = 11, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 2, Spieltag = 2, HeimMannschaftId = 2, AuswaertsMannschaftId = 1 },
            new Spiel { Id = 12, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 1, Spieltag = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 4 },
            new Spiel { Id = 13, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 3, Spieltag = 3, HeimMannschaftId = 2, AuswaertsMannschaftId = 4 },
            new Spiel { Id = 14, TurnierInstanzId = 1, TurnierRundeId = 3, Spielnummer = 6, AuswaertsMannschaftId = 1, HeimPlatzhalterName = "Sieger Spiel 4" },
            new Spiel { Id = 15, TurnierInstanzId = 2, TurnierRundeId = 4, Spielnummer = 1, Spieltag = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 3 });
        await anlegen.SaveChangesAsync();
        return datenbank;
    }

    [Fact]
    public async Task AlleNachMannschaftAsync_SpieleDerMannschaftImTurnier_InTurnierreihenfolgeSamtRunde()
    {
        string datenbank = await TurniereMitMannschaftenAnlegenAsync();
        using var ctx = NeuerContext(datenbank);

        var spiele = await new SpielRepository(ctx).AlleNachMannschaftAsync(turnierInstanzId: 1, mannschaftId: 1);

        spiele.Select(s => s.Id).Should().Equal(12, 11, 10, 14);
        spiele.Should().OnlyContain(s => s.TurnierRunde != null, "die Ansicht zeigt zu jedem Spiel seine Runde");
        spiele.Last().HeimPlatzhalterName.Should().Be("Sieger Spiel 4", "der Gegner darf noch offen sein");
    }

    [Fact]
    public async Task MannschaftenNachInstanzAsync_NurMannschaftenMitSpielImTurnier_NachName()
    {
        string datenbank = await TurniereMitMannschaftenAnlegenAsync();
        using var ctx = NeuerContext(datenbank);
        var repo = new SpielRepository(ctx);

        (await repo.MannschaftenNachInstanzAsync(1)).Select(m => m.Name).Should().Equal("Adler", "Füchse", "Zebras");
        (await repo.MannschaftenNachInstanzAsync(2)).Select(m => m.Name).Should().Equal("Bären", "Zebras");
    }
}
