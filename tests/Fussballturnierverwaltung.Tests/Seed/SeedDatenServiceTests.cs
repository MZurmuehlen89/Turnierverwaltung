using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
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
    private const string CL_VORLAGE = "UEFA Champions League";
    private const string VARIANTE_36 = "36 Teams · ab 2024/25";

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
                .SingleAsync(i => i.Name == CL_VORLAGE && i.Variante == VARIANTE_36 && i.Status == TurnierStatus.Vorlage);
            cl.Runden.Single(r => r.Name == "Ligaphase").Rundenart = Rundenart.SchweizerSystem;
            cl.Runden.Single(r => r.Name == "Ligaphase").KonfigurationJson = "{\"anzahlSpieltage\":6}";
            var el = await ctx.TurnierInstanzen.Include(i => i.Runden)
                .SingleAsync(i => i.Name == "UEFA Europa League" && i.Status == TurnierStatus.Vorlage);
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
            runden.Single(r => r.Vorlage == "UEFA Europa League").Rundenart
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

    // #358: die Tabellenzonen im Turnierbaum entstehen allein aus den ausgehenden Übergangsregeln — eine
    // Gruppenphase ohne Regeln bliebe farblos, obwohl ihre Folgerunde per Platzhalter gespeist wird.
    [Fact]
    public void Erstellen_JedeGruppenphaseHatUebergangsregelnFuerIhrenGesamtenAusgang()
    {
        var gruppenphasen = TurnierVorlagenSeedDaten.Erstellen()
            .SelectMany(v => v.Runden.Select(r => (Vorlage: v, Runde: r)))
            .Where(x => x.Runde.Rundenart == Rundenart.Gruppenphase)
            .ToList();

        gruppenphasen.Should().NotBeEmpty();
        foreach (var (vorlage, runde) in gruppenphasen)
            UebergangsregelService.AusgangAbleiten([.. runde.Uebergangsregeln])
                .Should().Be(runde.AusgangsMannschaftsanzahl, $"{vorlage.Name} ({vorlage.Variante}): {runde.Name}");
    }

    [Fact]
    public async Task InitialisierenAsync_ErgaenztFehlendeVorlagenStrukturUndBenenntWm24Um()
    {
        const string EM24 = "24 Teams · 2016+";
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            // Seed-Stand vor #350 simulieren: EM 24 ohne GruppenAnzahl/Übergangsregeln, CL-Achtelfinale ohne
            // EingangUeberUebergangsregeln, WM 24 unter altem Namen; neue WM-24-Vorlage (1982) noch nicht vorhanden.
            var em = await ctx.TurnierInstanzen.Include(i => i.Runden).SingleAsync(i => i.Name == "UEFA EM" && i.Variante == EM24);
            ctx.Uebergangsregeln.RemoveRange(ctx.Uebergangsregeln.Where(u => em.Runden.Select(r => r.Id).Contains(u.QuellRundeId)));
            em.Runden.Single(r => r.Name == "Gruppenphase").GruppenAnzahl = null;
            em.Runden.Single(r => r.Name == "Achtelfinale").EingangUeberUebergangsregeln = false;
            var cl = await ctx.TurnierInstanzen.Include(i => i.Runden).SingleAsync(i => i.Name == CL_VORLAGE && i.Variante == VARIANTE_36);
            cl.Runden.Single(r => r.Name == "Achtelfinale").EingangUeberUebergangsregeln = false;
            var wm24 = await ctx.TurnierInstanzen.SingleAsync(i => i.Name == "FIFA WM" && i.Variante == "24 Teams · 1982");
            wm24.Name = "FIFA WM (24 Teams, 1982–1994)";
            wm24.Variante = "";
            wm24.Beschreibung = "6 Dreiergruppen + Achtelfinale-Runde → K.-o.";
            wm24.Enddatum = new DateOnly(1994, 12, 31);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var em = await ctx.TurnierInstanzen.Include(i => i.Runden).ThenInclude(r => r.Uebergangsregeln)
                .SingleAsync(i => i.Name == "UEFA EM" && i.Variante == EM24);
            var gruppenphase = em.Runden.Single(r => r.Name == "Gruppenphase");
            gruppenphase.GruppenAnzahl.Should().Be(6);
            gruppenphase.Uebergangsregeln.Should().HaveCount(7);
            gruppenphase.Uebergangsregeln.Single(u => u.AnzahlBeste is not null).AnzahlBeste.Should().Be(4);
            em.Runden.Single(r => r.Name == "Achtelfinale").EingangUeberUebergangsregeln.Should().BeTrue();

            (await ctx.TurnierRunden.SingleAsync(r => r.TurnierInstanz.Name == CL_VORLAGE && r.TurnierInstanz.Variante == VARIANTE_36 && r.Name == "Achtelfinale"))
                .EingangUeberUebergangsregeln.Should().BeTrue();

            var wm24 = await ctx.TurnierInstanzen.Where(i => i.Name == "FIFA WM" && i.Variante.StartsWith("24 Teams")).ToListAsync();
            wm24.Select(i => i.Variante).Should().BeEquivalentTo("24 Teams · 1982", "24 Teams · 1986–1994");
            wm24.Single(i => i.Variante == "24 Teams · 1982").Enddatum.Should().Be(new DateOnly(1982, 12, 31));
        }
    }

    // #367: Bestands-Datenbanken tragen noch den Klammerzusatz im Namen und keine Variante — der Seed muss sie
    // umbenennen statt unter dem neuen Schlüssel (Name + Variante) Duplikate nachzurüsten.
    [Fact]
    public async Task InitialisierenAsync_VerschiebtKlammerzusatzAlterVorlagenInVarianteOhneDuplikate()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        int anzahlVorlagen;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();
            var vorlagen = await ctx.TurnierInstanzen.Where(i => i.Status == TurnierStatus.Vorlage).ToListAsync();
            anzahlVorlagen = vorlagen.Count;
            var altNachNeu = TurnierVorlagenSeedDaten.UmbenennungenNachAltemNamen
                .Where(u => u.Key != "FIFA WM (24 Teams, 1982–1994)")
                .ToDictionary(u => u.Value, u => u.Key);
            foreach (var vorlage in vorlagen)
            {
                vorlage.Name = altNachNeu[(vorlage.Name, vorlage.Variante)];
                vorlage.Variante = "";
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            var vorlagen = await ctx.TurnierInstanzen.Where(i => i.Status == TurnierStatus.Vorlage).ToListAsync();
            vorlagen.Should().HaveCount(anzahlVorlagen);
            vorlagen.Should().NotContain(v => v.Name.Contains('(') || v.Variante == "");
            vorlagen.Select(v => (v.Name, v.Variante)).Should().OnlyHaveUniqueItems();
            vorlagen.Where(v => v.Name == "FIFA WM").Select(v => v.Variante).Should().BeEquivalentTo(
                "48 Teams · ab 2026", "32 Teams · 1998–2022", "24 Teams · 1982", "24 Teams · 1986–1994", "16 Teams · 1934–1978");
        }
    }

    [Fact]
    public void Erstellen_NameUndVarianteSindEindeutigUndOhneKlammerzusatz()
    {
        var vorlagen = TurnierVorlagenSeedDaten.Erstellen();
        vorlagen.Select(v => (v.Name, v.Variante)).Should().OnlyHaveUniqueItems();
        vorlagen.Should().NotContain(v => v.Name.Contains('('));
        vorlagen.Should().NotContain(v => string.IsNullOrWhiteSpace(v.Variante));
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

    // ── Rangfolge der UEFA-Ligaphase (Issue #353) ──────────────────────────

    [Fact]
    public async Task InitialisierenAsync_LigaphaseDerUefaClubwettbewerbeNutztDieGegnerKriterien()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        var regelarten = await RegelartenDerRundeAsync(ctx, CL_VORLAGE, VARIANTE_36, "Ligaphase");
        regelarten.Should().ContainInOrder(
            RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz, RankingRegelArt.ErzielteTore,
            RankingRegelArt.AuswaertsToreGesamt, RankingRegelArt.Siege, RankingRegelArt.AuswaertsSiege,
            RankingRegelArt.GegnerPunkte, RankingRegelArt.GegnerTordifferenz, RankingRegelArt.GegnerErzielteTore);
        regelarten.Should().NotContain(RankingRegelArt.DirekterVergleichPunkte, "in der Ligaphase spielt nicht jeder gegen jeden");

        var klassisch = await RegelartenDerRundeAsync(ctx, CL_VORLAGE, "32 Teams · ohne Auswärtstorregel · 2021–2024", "Gruppenphase");
        klassisch.Should().Contain(RankingRegelArt.DirekterVergleichPunkte, "die Gruppenphase behält das Standard-Regelwerk");
    }

    [Fact]
    public async Task InitialisierenAsync_RuestetRegelwerkDerLigaphaseInBestehenderDatenbankNach()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        int fifaId;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            // Stand vor #353 simulieren: alle Vorlagen-Runden auf dem Standard-Regelwerk, die Ligaphase der
            // Europa League bewusst manuell auf ein anderes Regelwerk gestellt (muss unangetastet bleiben).
            int standardId = (await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.UEFA_CLUB_STANDARD)).Id;
            int ligaphaseId = (await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.UEFA_LIGAPHASE)).Id;
            fifaId = (await ctx.Regelwerke.SingleAsync(r => r.Name == "FIFA Standard Modern (3-Punkte)")).Id;
            foreach (var runde in await ctx.TurnierRunden.Include(r => r.TurnierInstanz).Where(r => r.RegelwerkId == ligaphaseId).ToListAsync())
                runde.RegelwerkId = runde.TurnierInstanz.Name == "UEFA Europa League" && runde.Name == "Ligaphase" ? fifaId : standardId;
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            ctx.Regelwerke.Remove(await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.UEFA_LIGAPHASE));
            ctx.RankingRegelSets.Remove(await ctx.RankingRegelSets.Include(s => s.Eintraege)
                .SingleAsync(s => s.Name == RegelwerkeSeedDaten.UEFA_LIGAPHASE_SET));
            await ctx.SaveChangesAsync();
            ctx.RankingRegeln.RemoveRange(await ctx.RankingRegeln
                .Where(r => r.Regelart == RankingRegelArt.GegnerPunkte
                         || r.Regelart == RankingRegelArt.GegnerTordifferenz
                         || r.Regelart == RankingRegelArt.GegnerErzielteTore)
                .ToListAsync());
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

            (await RegelartenDerRundeAsync(ctx, CL_VORLAGE, VARIANTE_36, "Ligaphase"))
                .Should().Contain(RankingRegelArt.GegnerPunkte);
            (await RegelartenDerRundeAsync(ctx, CL_VORLAGE, VARIANTE_36, "Achtelfinale"))
                .Should().Contain(RankingRegelArt.GegnerPunkte, "alle Runden der Vorlage ziehen mit");
            var elLigaphase = await ctx.TurnierRunden.SingleAsync(r =>
                r.Name == "Ligaphase" && r.TurnierInstanz.Name == "UEFA Europa League" && r.TurnierInstanz.Status == TurnierStatus.Vorlage);
            elLigaphase.RegelwerkId.Should().Be(fifaId, "ein manuell gewähltes Regelwerk bleibt erhalten");
            (await ctx.RankingRegelSets.CountAsync(s => s.Name == RegelwerkeSeedDaten.UEFA_LIGAPHASE_SET)).Should().Be(1);
        }
    }

    [Fact]
    public async Task InitialisierenAsync_FairplayWertungBevorzugtDasTeamMitWenigerKarten()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await new SeedDatenService(ctx, NullLogger<SeedDatenService>.Instance).InitialisierenAsync();

        // Der Fairplay-Wert im TabellenService ist negativ (Karten zählen als Abzug) - nur absteigend
        // sortiert steht das Team mit weniger Karten vorn.
        var fairplay = await ctx.RankingRegeln.SingleAsync(r => r.Regelart == RankingRegelArt.FairplayWertung);
        fairplay.Sortierrichtung.Should().Be(Sortierrichtung.Absteigend);
    }

    private static async Task<List<RankingRegelArt>> RegelartenDerRundeAsync(
        TurnierverwaltungDbContext ctx, string vorlage, string variante, string rundenName)
    {
        var runde = await ctx.TurnierRunden.SingleAsync(r =>
            r.Name == rundenName && r.TurnierInstanz.Name == vorlage && r.TurnierInstanz.Variante == variante
            && r.TurnierInstanz.Status == TurnierStatus.Vorlage);
        var regelwerk = await ctx.Regelwerke.SingleAsync(r => r.Id == runde.RegelwerkId);
        return await ctx.RankingRegelSetEintraege
            .Where(e => e.RankingRegelSetId == regelwerk.RankingRegelSetId)
            .OrderBy(e => e.Reihenfolge)
            .Select(e => e.RankingRegel.Regelart)
            .ToListAsync();
    }
}
