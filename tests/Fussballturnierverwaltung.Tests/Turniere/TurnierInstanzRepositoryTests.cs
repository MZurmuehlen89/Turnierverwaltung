using FluentAssertions;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class TurnierInstanzRepositoryTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Issue #186: Betrachten einer K.o.-Quellrunde laedt ihre ausgehenden Uebergangsregeln inkl.
    // Include(ZielRunde) — die Zielrunden werden dadurch im DbContext getrackt. Wird eine solche
    // Zielrunde anschliessend aus der Rundenliste entfernt und gespeichert, warf RundenErsetzenAsync
    // vor dem Fix eine rohe EF-Exception ("association ... severed, but the relationship is ...
    // required"), weil die Zielrunden-Beziehung bewusst OnDelete(Restrict) ist (anders als die
    // Quellrunden-Beziehung, die bereits cascadet) und EF verwaiste Uebergangsregeln nicht automatisch
    // mitloescht.
    [Fact]
    public async Task RundenErsetzenAsync_EntfernteRundeAlsZielrundeGetrackt_LoeschtUebergangsregelnMit()
    {
        await using var ctx = NeuerContext();
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.TurnierInstanzen.Add(new TurnierInstanz
            { Id = 1, Name = "Test", SaisonOderJahr = "2026", Status = TurnierStatus.Geplant });
        var halbfinale = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        var finale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        var spielUmPlatz3 = new TurnierRunde
        {
            Id = 3, TurnierInstanzId = 1, Name = "Spiel um Platz 3", Reihenfolge = 3,
            Rundenart = Rundenart.SpielUmPlatzDrei, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(halbfinale, finale, spielUmPlatz3);
        await ctx.SaveChangesAsync();

        var uebergangsregelRepo = new UebergangsregelRepository(ctx);
        await uebergangsregelRepo.HinzufuegenAsync(new Uebergangsregel
            { QuellRundeId = halbfinale.Id, ZielRundeId = finale.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 });
        var geloeschteRegel = new Uebergangsregel
            { QuellRundeId = halbfinale.Id, ZielRundeId = spielUmPlatz3.Id, GruppeIndex = 1, PlatzierungVon = 2, PlatzierungBis = 2 };
        await uebergangsregelRepo.HinzufuegenAsync(geloeschteRegel);

        // Simuliert das Betrachten der Halbfinale-Runde im Runden-Editor (Ausgehende Übergangsregeln, #186):
        // trackt Finale UND Spiel um Platz 3 als ZielRunde-Navigation im selben DbContext.
        await uebergangsregelRepo.AlleNachQuellRundeIdAsync(halbfinale.Id);

        var instanzRepo = new TurnierInstanzRepository(ctx);
        var neueHalbfinale = new TurnierRunde
        {
            TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        var neueFinale = new TurnierRunde
        {
            TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };

        // Nutzer entfernt "Spiel um Platz 3" aus der Rundenliste und speichert.
        var act = async () => await instanzRepo.RundenErsetzenAsync(1, [neueHalbfinale, neueFinale]);
        await act.Should().NotThrowAsync();

        (await uebergangsregelRepo.FindenNachIdAsync(geloeschteRegel.Id)).Should().BeNull();
    }
}
