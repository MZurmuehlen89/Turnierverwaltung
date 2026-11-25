using FluentAssertions;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Tabelle;

/// <summary>Issue #355 K3/K5: Der <see cref="TabellenService"/> lädt den Punktevortrag aus der Abschlusstabelle der einen
/// Quellrunde. Grunddurchgang (Runde 1, drei Teams): Team 1 hat 6, Team 2 hat 7 und Team 3 hat 1 Punkt; Meistergruppe
/// (Runde 2, Team 1 gegen Team 2, noch offen) startet mit halbierten, abgerundeten Punkten: 3 und 3 (Team 2 ungerundet 3,5).</summary>
public class PunktevortragServiceTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TabellenService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new TurnierRundeRepository(ctx), new SpielRepository(ctx), new RegelwerkRepository(ctx),
            new RanglisteRepository(ctx), new AuslosungRepository(ctx), new UebergangsregelRepository(ctx));

    private static void SpielAnlegen(TurnierverwaltungDbContext ctx, int id, int rundeId, int heim, int gast, (int Heim, int Gast)? ergebnis)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = id, TurnierInstanzId = 1, TurnierRundeId = rundeId, Spielnummer = id,
            HeimMannschaftId = heim, AuswaertsMannschaftId = gast,
            Status = ergebnis is null ? SpielStatus.Geplant : SpielStatus.Abgeschlossen
        });
        if (ergebnis is { } e)
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = id, SpielId = id, HeimToreRegulaer = e.Heim, AuswaertsToreRegulaer = e.Gast,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
            });
    }

    /// <summary>Grunddurchgang abgeschlossen, Meistergruppe mit dem angegebenen Modus; <paramref name="zweiteQuelle"/> speist
    /// die Meistergruppe zusätzlich aus einer dritten Runde.</summary>
    private static void TurnierAnlegen(TurnierverwaltungDbContext ctx, Punktevortrag modus, bool zweiteQuelle = false)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.PunkteRegeln.Add(new PunkteRegel { Id = 1, RegelwerkId = 1, PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 });
        ctx.TurnierRunden.AddRange(
            new TurnierRunde { Id = 1, TurnierInstanzId = 1, Name = "Grunddurchgang", Rundenart = Rundenart.Liga, RegelwerkId = 1, Reihenfolge = 1 },
            new TurnierRunde { Id = 2, TurnierInstanzId = 1, Name = "Meistergruppe", Rundenart = Rundenart.Liga, RegelwerkId = 1, Reihenfolge = 2, Punktevortrag = modus },
            new TurnierRunde { Id = 3, TurnierInstanzId = 1, Name = "Zweite Quelle", Rundenart = Rundenart.Liga, RegelwerkId = 1, Reihenfolge = 3 });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team 1", Kurzname = "T1", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team 2", Kurzname = "T2", LandId = 1 },
            new Mannschaft { Id = 3, Name = "Team 3", Kurzname = "T3", LandId = 1 });

        // Team 1: S(g3) S(g4) N(g1) = 6 · Team 2: S(g1) S(g2) U(g5) = 7 · Team 3: N N U = 1
        SpielAnlegen(ctx, 1, 1, 2, 1, (1, 0));
        SpielAnlegen(ctx, 2, 1, 2, 3, (1, 0));
        SpielAnlegen(ctx, 3, 1, 1, 3, (1, 0));
        SpielAnlegen(ctx, 4, 1, 1, 3, (1, 0));
        SpielAnlegen(ctx, 5, 1, 3, 2, (0, 0));
        SpielAnlegen(ctx, 6, 2, 1, 2, null);

        ctx.Uebergangsregeln.Add(new Uebergangsregel { Id = 1, QuellRundeId = 1, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 2 });
        if (zweiteQuelle)
            ctx.Uebergangsregeln.Add(new Uebergangsregel { Id = 2, QuellRundeId = 3, ZielRundeId = 2, PlatzierungVon = 1, PlatzierungBis = 2 });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Tabelle_DerFolgerunde_StartetMitDenHalbiertenPunktenDerQuellrunde()
    {
        await using var ctx = NeuerContext();
        TurnierAnlegen(ctx, Punktevortrag.HalbiertAbgerundet);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(2);

        tabelle.Should().OnlyContain(e => e.Punkte == 3 && e.Spiele == 0);
        // Team 2 wurde von 3,5 auf 3 abgerundet und steht bei Punktgleichheit vor Team 1.
        tabelle.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Tabelle_DerFolgerunde_VolleUebernahme()
    {
        await using var ctx = NeuerContext();
        TurnierAnlegen(ctx, Punktevortrag.Voll);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(2);

        tabelle.Single(e => e.Mannschaft.Id == 1).Punkte.Should().Be(6);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(7);
        tabelle.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Tabelle_DerFolgerundeOhneModus_StartetBeiNull()
    {
        await using var ctx = NeuerContext();
        TurnierAnlegen(ctx, Punktevortrag.Keine);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(2);

        tabelle.Should().OnlyContain(e => e.Punkte == 0);
    }

    [Fact]
    public async Task Tabelle_DerFolgerunde_MitZweiQuellrunden_NimmtNichtsMit()
    {
        await using var ctx = NeuerContext();
        TurnierAnlegen(ctx, Punktevortrag.Voll, zweiteQuelle: true);

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(2);

        tabelle.Should().OnlyContain(e => e.Punkte == 0);
    }

    [Fact]
    public async Task Tabelle_DerFolgerunde_ZeigtDenVortragAuchNachDemErstenSpieltag()
    {
        await using var ctx = NeuerContext();
        TurnierAnlegen(ctx, Punktevortrag.HalbiertAbgerundet);
        var spiel = await ctx.Spiele.SingleAsync(s => s.Id == 6);
        spiel.Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = 6, SpielId = 6, HeimToreRegulaer = 2, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit
        });
        await ctx.SaveChangesAsync();

        var tabelle = await ServiceFuer(ctx).TabelleBerechnenAsync(2);

        var team1 = tabelle.Single(e => e.Mannschaft.Id == 1);
        team1.Punkte.Should().Be(6);
        team1.Spiele.Should().Be(1);
        tabelle.Single(e => e.Mannschaft.Id == 2).Punkte.Should().Be(3);
        tabelle.OrderBy(e => e.Rang).Select(e => e.Mannschaft.Id).Should().Equal(1, 2);
    }
}
