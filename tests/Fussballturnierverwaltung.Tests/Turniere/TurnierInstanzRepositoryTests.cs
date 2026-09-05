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

    // ── Runden-Diff (Issue #294, Nachfolger von TurnierVorlageRepositoryTests) ──────────────────

    private static async Task<int> InstanzAnlegenAsync(TurnierverwaltungDbContext ctx)
    {
        var instanz = new TurnierInstanz { Name = "Test-Turnier", SaisonOderJahr = "2026", Status = TurnierStatus.Geplant };
        ctx.TurnierInstanzen.Add(instanz);
        await ctx.SaveChangesAsync();
        return instanz.Id;
    }

    private static List<TurnierRunde> ZweiRunden(int instanzId) =>
    [
        new() { TurnierInstanzId = instanzId, Name = "Gruppenphase", Reihenfolge = 1, Rundenart = Rundenart.Gruppenphase, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4, GruppenAnzahl = 2 },
        new() { TurnierInstanzId = instanzId, Name = "Halbfinale", Reihenfolge = 2, Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2 }
    ];

    [Fact]
    public async Task RundenErsetzenAsync_SpeichertGruppenAnzahlDerGruppenphaseRunde()
    {
        using var ctx = NeuerContext();
        var instanzId = await InstanzAnlegenAsync(ctx);
        var repo = new TurnierInstanzRepository(ctx);

        await repo.RundenErsetzenAsync(instanzId, ZweiRunden(instanzId));

        var geladen = await repo.FindenNachIdMitRundenAsync(instanzId);
        geladen!.Runden.Single(r => r.Reihenfolge == 1).GruppenAnzahl.Should().Be(2);
    }

    [Fact]
    public async Task RundenErsetzenAsync_ZweiterAufrufMitWenigerRundenBehaeltIdDerErstenRundeUndEntferntUeberzaehlige()
    {
        using var ctx = NeuerContext();
        var instanzId = await InstanzAnlegenAsync(ctx);
        var repo = new TurnierInstanzRepository(ctx);
        var ersteRunden = ZweiRunden(instanzId);
        await repo.RundenErsetzenAsync(instanzId, ersteRunden);
        var ersteRundeId = ersteRunden[0].Id;

        var einzelneRunde = new List<TurnierRunde>
        {
            new() { TurnierInstanzId = instanzId, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 8 }
        };
        var ergebnis = await repo.RundenErsetzenAsync(instanzId, einzelneRunde);

        ergebnis.Should().ContainSingle();
        ergebnis[0].Id.Should().Be(ersteRundeId);
        (await ctx.TurnierRunden.Where(r => r.TurnierInstanzId == instanzId).ToListAsync()).Should().ContainSingle();
    }
}
