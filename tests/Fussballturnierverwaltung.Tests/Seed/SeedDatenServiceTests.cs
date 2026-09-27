using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Seed;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Seed;

public class SeedDatenServiceTests
{
    private const string CL_VORLAGE = "UEFA Champions League (36 Teams, ab 2024/25)";

    [Fact]
    public async Task InitialisierenAsync_KorrigiertAlteSchweizerLigaphaseInVorlageAufTopfLiga()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            // Stand vor #328 simulieren: Ligaphase fälschlich als Schweizer System gespeichert,
            // plus eine bewusst manuell auf Liga umgestellte Runde, die unangetastet bleiben muss.
            var cl = await ctx.TurnierInstanzen.Include(i => i.Runden)
                .SingleAsync(i => i.Name == CL_VORLAGE && i.Status == TurnierStatus.Vorlage);
            cl.Runden.Single(r => r.Name == "Ligaphase").Rundenart = Rundenart.SchweizerSystem;
            cl.Runden.Single(r => r.Name == "Ligaphase").KonfigurationJson = "{\"anzahlSpieltage\":6}";
            var el = await ctx.TurnierInstanzen.Include(i => i.Runden)
                .SingleAsync(i => i.Name == "UEFA Europa League (36 Teams, ab 2024/25)" && i.Status == TurnierStatus.Vorlage);
            el.Runden.Single(r => r.Name == "Ligaphase").Rundenart = Rundenart.Liga;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var runden = await ctx.TurnierRunden
                .Where(r => r.Name == "Ligaphase" && r.TurnierInstanz.Status == TurnierStatus.Vorlage)
                .Select(r => new { Vorlage = r.TurnierInstanz.Name, r.Rundenart, r.KonfigurationJson })
                .ToListAsync();
            runden.Single(r => r.Vorlage == CL_VORLAGE).Rundenart.Should().Be(Rundenart.TopfLiga);
            runden.Single(r => r.Vorlage == CL_VORLAGE).KonfigurationJson.Should().BeNull();
            runden.Single(r => r.Vorlage == "UEFA Europa League (36 Teams, ab 2024/25)").Rundenart
                .Should().Be(Rundenart.Liga, "manuelle Anpassungen außerhalb des bekannten Altwerts bleiben erhalten");
        }
    }

    [Fact]
    public async Task InitialisierenAsync_JedeVorlageLaesstSichAusDemSeedPoolVollstaendigBesetzen()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        var mannschaften = await ctx.Mannschaften.Include(m => m.Land).ToListAsync();
        var vorlagen = await ctx.TurnierInstanzen.Where(i => i.Status == TurnierStatus.Vorlage).ToListAsync();

        // #351: DFB-Pokal (64) filterte auf GER-Vereine, der Seed hatte aber nur 36.
        foreach (var vorlage in vorlagen.Where(v => v.ZielMannschaftsanzahl > 0))
        {
            var pool = mannschaften.Count(m =>
                (vorlage.LandId is null || m.LandId == vorlage.LandId)
                && (vorlage.VerbandId is null || m.Land.VerbandId == vorlage.VerbandId)
                && vorlage.MannschaftsartFilter switch
                {
                    MannschaftsartFilter.Verein => m.Mannschaftsart == Mannschaftsart.Verein,
                    MannschaftsartFilter.Nationalmannschaft => m.Mannschaftsart == Mannschaftsart.Nationalmannschaft,
                    _ => true
                });
            pool.Should().BeGreaterThanOrEqualTo(vorlage.ZielMannschaftsanzahl!.Value, vorlage.Name);
        }
    }

    // #350: ältere Gruppenphasen-Vorlagen hatten keine GruppenAnzahl, die EM 24 keine Übergangsregeln für die
    // besten Dritten — jede Seed-Vorlage muss dieselben Prüfungen bestehen wie ein im Editor gespeichertes Turnier.
    [Fact]
    public void Erstellen_JedeVorlageBestehtRundenUndUebergangsregelValidierung()
    {
        foreach (var vorlage in TurnierVorlagenSeedDaten.Erstellen().Where(v => v.Runden.Count > 0))
        {
            var runden = vorlage.Runden.OrderBy(r => r.Reihenfolge).ToList();
            var eingaben = runden.Select(r => new TurnierRundeEingabe(
                r.Name, r.Rundenart, r.EingangsMannschaftsanzahl, r.AusgangsMannschaftsanzahl, r.RegelwerkId,
                r.MitRueckspiel, r.AuswaertstorRegelAktiv, r.KonfigurationJson, r.GruppenAnzahl,
                r.EingangUeberUebergangsregeln, r.PaarungFestVorgegeben, r.VorgegebeneAuslosungsart)).ToList();
            var regeln = runden.SelectMany(r => r.Uebergangsregeln).Select(u => new UebergangsregelPositionsEingabe(
                u.QuellRunde.Reihenfolge, u.ZielRunde.Reihenfolge, u.GruppeIndex, u.PlatzierungVon, u.PlatzierungBis,
                u.AnzahlBeste)).ToList();

            new TurnierValidator().RundenValidieren(eingaben).Fehler.Should().BeEmpty(vorlage.Name);
            new UebergangsregelValidator().Validieren(runden, regeln).Fehler.Should().BeEmpty(vorlage.Name);
        }
    }

    [Fact]
    public async Task InitialisierenAsync_ErgaenztFehlendeVorlagenStrukturUndBenenntWm24Um()
    {
        const string EM24 = "UEFA EM (24 Teams, 2016+)";
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            // Seed-Stand vor #350 simulieren: EM 24 ohne GruppenAnzahl/Übergangsregeln, CL-Achtelfinale ohne
            // EingangUeberUebergangsregeln, WM 24 unter altem Namen; neue WM-24-Vorlage (1982) noch nicht vorhanden.
            var em = await ctx.TurnierInstanzen.Include(i => i.Runden).SingleAsync(i => i.Name == EM24);
            ctx.Uebergangsregeln.RemoveRange(ctx.Uebergangsregeln.Where(u => em.Runden.Select(r => r.Id).Contains(u.QuellRundeId)));
            em.Runden.Single(r => r.Name == "Gruppenphase").GruppenAnzahl = null;
            em.Runden.Single(r => r.Name == "Achtelfinale").EingangUeberUebergangsregeln = false;
            var cl = await ctx.TurnierInstanzen.Include(i => i.Runden).SingleAsync(i => i.Name == CL_VORLAGE);
            cl.Runden.Single(r => r.Name == "Achtelfinale").EingangUeberUebergangsregeln = false;
            var wm24 = await ctx.TurnierInstanzen.SingleAsync(i => i.Name == "FIFA WM (24 Teams, 1982)");
            wm24.Name = "FIFA WM (24 Teams, 1982–1994)";
            wm24.Beschreibung = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.";
            wm24.Enddatum = new DateOnly(1994, 12, 31);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var em = await ctx.TurnierInstanzen.Include(i => i.Runden).ThenInclude(r => r.Uebergangsregeln)
                .SingleAsync(i => i.Name == EM24);
            var gruppenphase = em.Runden.Single(r => r.Name == "Gruppenphase");
            gruppenphase.GruppenAnzahl.Should().Be(6);
            gruppenphase.Uebergangsregeln.Should().HaveCount(7);
            gruppenphase.Uebergangsregeln.Single(u => u.AnzahlBeste is not null).AnzahlBeste.Should().Be(4);
            em.Runden.Single(r => r.Name == "Achtelfinale").EingangUeberUebergangsregeln.Should().BeTrue();

            (await ctx.TurnierRunden.SingleAsync(r => r.TurnierInstanz.Name == CL_VORLAGE && r.Name == "Achtelfinale"))
                .EingangUeberUebergangsregeln.Should().BeTrue();

            var wm24 = await ctx.TurnierInstanzen.Where(i => i.Name.StartsWith("FIFA WM (24 Teams")).ToListAsync();
            wm24.Select(i => i.Name).Should().BeEquivalentTo("FIFA WM (24 Teams, 1982)", "FIFA WM (24 Teams, 1986–1994)");
            wm24.Single(i => i.Name == "FIFA WM (24 Teams, 1982)").Enddatum.Should().Be(new DateOnly(1982, 12, 31));
        }
    }

    [Fact]
    public async Task InitialisierenAsync_VereinsKurznamenSindEindeutig()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        // Kurzname ist Dateiname des Wappens (Seed/Bildmaterial/Vereinswappen/<Kurzname>.png) — muss
        // über alle Länder eindeutig sein und darf kein reservierter Windows-Gerätename sein (#233).
        var kurznamen = await ctx.Mannschaften
            .Where(m => m.Mannschaftsart == Mannschaftsart.Verein)
            .Select(m => m.Kurzname)
            .ToListAsync();
        kurznamen.Should().OnlyHaveUniqueItems();
        kurznamen.Should().NotContain(k => System.Text.RegularExpressions.Regex.IsMatch(
            k, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }
}
