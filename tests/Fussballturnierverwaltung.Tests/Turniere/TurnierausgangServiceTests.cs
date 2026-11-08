using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Turniere;

/// <summary>Der Weg über die Datenbank (#527): Runden und Spiele laden, bei einer Liga die Tabelle samt Sicherheit.</summary>
public class TurnierausgangServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private const int INSTANZ = 1;

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TurnierausgangService ServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var tabellen = new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx), new RanglisteRepository(ctx), new AuslosungRepository(ctx));
        return new TurnierausgangService(rundeRepo, spielRepo, tabellen);
    }

    private static void TurnierAnlegen(TurnierverwaltungDbContext ctx, params TurnierRunde[] runden)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1 });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = INSTANZ, Name = "Turnier", StandardRegelwerkId = 1 });
        foreach (var runde in runden)
        {
            runde.TurnierInstanzId = INSTANZ;
            ctx.TurnierRunden.Add(runde);
        }
    }

    private static void SpielAnlegen(TurnierverwaltungDbContext ctx, int id, int rundeId, int heim, int auswaerts,
        int? spieltag = null, (int Heim, int Auswaerts)? tore = null)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = INSTANZ, TurnierRundeId = rundeId, Spielnummer = id, Spieltag = spieltag,
            HeimMannschaftId = heim, AuswaertsMannschaftId = auswaerts,
            Status = tore is null ? SpielStatus.Geplant : SpielStatus.Abgeschlossen
        });
        if (tore is not { } t) return;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = id, SpielId = id, HeimToreRegulaer = t.Heim, AuswaertsToreRegulaer = t.Auswaerts,
            SiegerMannschaftId = t.Heim > t.Auswaerts ? heim : t.Auswaerts > t.Heim ? auswaerts : null
        });
    }

    // Liga mit drei Teams, je ein Spiel: A schlägt B am 1. Spieltag; am 2. spielt A gegen C (mit oder ohne Ergebnis),
    // am 3. B gegen C.
    private static async Task<Turnierausgang> LigaNachZweitemSpieltagAsync(bool zweitesSpielGespielt)
    {
        using var ctx = NeuerContext();
        TurnierAnlegen(ctx, new TurnierRunde { Id = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga, EingangsMannschaftsanzahl = 3 });
        SpielAnlegen(ctx, 1, 1, 1, 2, spieltag: 1, tore: (1, 0));
        SpielAnlegen(ctx, 2, 1, 1, 3, spieltag: 2, tore: zweitesSpielGespielt ? (2, 0) : null);
        SpielAnlegen(ctx, 3, 1, 2, 3, spieltag: 3);
        await ctx.SaveChangesAsync();

        return await ServiceFuer(ctx).ErmittelnAsync(INSTANZ);
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ermitteln_LigaMitUneinholbaremErsten_IstVorzeitigMeister()
    {
        var ausgang = await LigaNachZweitemSpieltagAsync(zweitesSpielGespielt: true);

        ausgang.Sieger!.Name.Should().Be("Team A");
        ausgang.Plaetze.Single().Grund.Should().Be(PlatzierungsGrund.Meister);
        (ausgang.OffeneSpieltage, ausgang.SpieltageGesamt).Should().Be((1, 3));
    }

    [Fact]
    public async Task Ermitteln_LigaMitEinholbaremErsten_NochKeinMeister()
    {
        // C kann mit zwei Siegen auf die Punkte von A kommen.
        var ausgang = await LigaNachZweitemSpieltagAsync(zweitesSpielGespielt: false);

        ausgang.Sieger.Should().BeNull();
        ausgang.Tabellenfuehrer!.Name.Should().Be("Team A");
        ausgang.OffeneSpieltage.Should().Be(2);
    }

    [Fact]
    public async Task Ermitteln_KoTurnier_LiestFinaleUndMannschaftenAusDerDatenbank()
    {
        using var ctx = NeuerContext();
        TurnierAnlegen(ctx, new TurnierRunde { Id = 1, Name = "Finale", Reihenfolge = 1, Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2 });
        SpielAnlegen(ctx, 1, 1, 1, 2, tore: (0, 1));
        await ctx.SaveChangesAsync();

        var ausgang = await ServiceFuer(ctx).ErmittelnAsync(INSTANZ);

        ausgang.Sieger!.Name.Should().Be("Team B");
        ausgang.Plaetze.Single(p => p.Platz == 2).Mannschaft.Name.Should().Be("Team A");
    }

    [Fact]
    public async Task Ermitteln_OhneRunden_KeinAusgang()
    {
        using var ctx = NeuerContext();
        TurnierAnlegen(ctx);
        await ctx.SaveChangesAsync();

        (await ServiceFuer(ctx).ErmittelnAsync(INSTANZ)).Should().BeSameAs(Turnierausgang.Keiner);
    }
}
