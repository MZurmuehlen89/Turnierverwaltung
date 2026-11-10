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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
            var vorlagen = await ctx.TurnierInstanzen.Where(i => i.Status == TurnierStatus.Vorlage).ToListAsync();
            anzahlVorlagen = vorlagen.Count;
            var altNachNeu = TurnierVorlagenSeedDaten.UmbenennungenNachAltemNamen
                .Where(u => u.Key != "FIFA WM (24 Teams, 1982–1994)")
                .ToDictionary(u => u.Value, u => u.Key);
            // Vorlagen, die erst nach #367 dazukamen (#375), hatten nie einen Klammerzusatz.
            foreach (var vorlage in vorlagen)
            {
                if (!altNachNeu.TryGetValue((vorlage.Name, vorlage.Variante), out string? alterName)) continue;
                vorlage.Name = alterName;
                vorlage.Variante = "";
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            var vorlagen = await ctx.TurnierInstanzen.Where(i => i.Status == TurnierStatus.Vorlage).ToListAsync();
            vorlagen.Should().HaveCount(anzahlVorlagen);
            vorlagen.Should().NotContain(v => v.Name.Contains('(') || v.Variante == "");
            vorlagen.Select(v => (v.Name, v.Variante)).Should().OnlyHaveUniqueItems();
            vorlagen.Where(v => v.Name == "FIFA WM").Select(v => v.Variante).Should().BeEquivalentTo(
                "48 Teams · ab 2026", "32 Teams · 1998–2022", "24 Teams · 1982", "24 Teams · 1986–1994",
                "16 Teams · 1974–1978", "16 Teams · 1958–1970", TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938);
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
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        var regelarten = await RegelartenDerRundeAsync(ctx, CL_VORLAGE, VARIANTE_36, "Ligaphase");
        regelarten.Should().ContainInOrder(
            RankingRegelArt.Punkte, RankingRegelArt.Tordifferenz, RankingRegelArt.ErzielteTore,
            RankingRegelArt.AuswaertsToreGesamt, RankingRegelArt.Siege, RankingRegelArt.AuswaertsSiege,
            RankingRegelArt.GegnerPunkte, RankingRegelArt.GegnerTordifferenz, RankingRegelArt.GegnerErzielteTore,
            RankingRegelArt.FairplayWertung, RankingRegelArt.Koeffizient, RankingRegelArt.Losentscheid);
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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
    public async Task InitialisierenAsync_NurUefaStandardRechnetDenDirektenVergleichNeu()
    {
        // #628: UEFA wiederholt die Mini-Tabelle für die übrigen gleichen Teams (EM-Reglement Art. 20.01d), FIFA nicht.
        await using var ctx = new TurnierverwaltungDbContext(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        (await ctx.RankingRegelSets.Where(s => s.DirektenVergleichWiederholen).Select(s => s.Name).ToListAsync())
            .Should().Equal("UEFA Standard");
    }

    [Fact]
    public async Task InitialisierenAsync_LaLigaUndSerieA_WertenNachDemRegelsetDerNationalenLiga()
    {
        // #628: direkter Vergleich nur über Punkte und Tordifferenz, ohne Neuberechnung (nicht „UEFA Standard“).
        await using var ctx = new TurnierverwaltungDbContext(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        var regelwerk = await ctx.Regelwerke.Include(r => r.RankingRegelSet!).ThenInclude(s => s.Eintraege)
            .ThenInclude(e => e.RankingRegel)
            .SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.LIGA_DIREKTER_VERGLEICH);
        regelwerk.RankingRegelSet!.Name.Should().Be(RegelwerkeSeedDaten.NATIONALE_LIGA_SET);
        regelwerk.RankingRegelSet.DirektenVergleichWiederholen.Should().BeFalse();
        regelwerk.RankingRegelSet.Eintraege.OrderBy(e => e.Reihenfolge).Select(e => e.RankingRegel.Regelart).Should().Equal(
            RankingRegelArt.Punkte, RankingRegelArt.DirekterVergleichPunkte, RankingRegelArt.DirekterVergleichTordifferenz,
            RankingRegelArt.Tordifferenz, RankingRegelArt.ErzielteTore, RankingRegelArt.FairplayWertung,
            RankingRegelArt.Losentscheid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialisierenAsync_SetztKoeffizientInUnveraendertesLigaphasenSetVorDenLosentscheid(bool vomNutzerGeaendert)
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            // Stand vor #354: Losentscheid an Stelle 11, keine Koeffizient-Regel.
            var set = await ctx.RankingRegelSets.Include(s => s.Eintraege).ThenInclude(e => e.RankingRegel)
                .SingleAsync(s => s.Name == RegelwerkeSeedDaten.UEFA_LIGAPHASE_SET);
            var koeffizient = set.Eintraege.Single(e => e.RankingRegel.Regelart == RankingRegelArt.Koeffizient);
            ctx.RankingRegelSetEintraege.Remove(koeffizient);
            if (vomNutzerGeaendert)
                ctx.RankingRegelSetEintraege.Remove(set.Eintraege.Single(e => e.RankingRegel.Regelart == RankingRegelArt.FairplayWertung));
            await ctx.SaveChangesAsync();
            set.Eintraege.Single(e => e.RankingRegel.Regelart == RankingRegelArt.Losentscheid).Reihenfolge = 11;
            await ctx.SaveChangesAsync();
            ctx.RankingRegeln.Remove(await ctx.RankingRegeln.SingleAsync(r => r.Regelart == RankingRegelArt.Koeffizient));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            var regelarten = await RegelartenDerRundeAsync(ctx, CL_VORLAGE, VARIANTE_36, "Ligaphase");
            if (vomNutzerGeaendert)
                regelarten.Should().NotContain(RankingRegelArt.Koeffizient, "eine geänderte Zusammenstellung bleibt erhalten");
            else
                regelarten.TakeLast(3).Should().Equal(
                    RankingRegelArt.FairplayWertung, RankingRegelArt.Koeffizient, RankingRegelArt.Losentscheid);
        }
    }

    // ── Copa América ohne Verlängerung (Issue #374) ────────────────────────

    private const string COPA_VORLAGE = "Copa América";
    private const string COPA_16 = "16 Teams · 2016/2024";
    private const string COPA_12 = "12 Teams · 1993–2019";

    [Theory]
    [InlineData(COPA_16)]
    [InlineData(COPA_12)]
    public async Task InitialisierenAsync_CopaAmericaGehtVorDemFinaleDirektInsElfmeterschiessen(string variante)
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        foreach (string runde in new[] { "Viertelfinale", "Halbfinale", "Spiel um Platz 3" })
            (await VerfahrenDerRundeAsync(ctx, COPA_VORLAGE, variante, runde))
                .Should().Equal([VerfahrenArt.Elfmeterschiessen], $"{runde} wird ohne Verlängerung gespielt");
        (await VerfahrenDerRundeAsync(ctx, COPA_VORLAGE, variante, "Finale"))
            .Should().Equal(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
    }

    [Fact]
    public async Task InitialisierenAsync_RuestetDirektesElfmeterschiessenDerCopaAmericaInBestehenderDatenbankNach()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        int golden;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            // Stand vor #374 simulieren: alle Copa-Runden auf dem FIFA-Regelwerk, alte Beschreibungen. Manuell
            // angepasst (muss unangetastet bleiben): das Halbfinale der 16er-Vorlage und die Beschreibung der 12er.
            int fifaId = (await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.FIFA_STANDARD_MODERN)).Id;
            int copaId = (await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.COPA_DIREKTES_ELFMETERSCHIESSEN)).Id;
            golden = (await ctx.Regelwerke.SingleAsync(r => r.Name == "UEFA EM 1996 (Golden Goal)")).Id;
            foreach (var runde in await ctx.TurnierRunden.Include(r => r.TurnierInstanz).Where(r => r.RegelwerkId == copaId).ToListAsync())
                runde.RegelwerkId = runde.TurnierInstanz.Variante == COPA_16 && runde.Name == "Halbfinale" ? golden : fifaId;
            foreach (var vorlage in await ctx.TurnierInstanzen.Where(i => i.Name == COPA_VORLAGE && i.Status == TurnierStatus.Vorlage).ToListAsync())
                vorlage.Beschreibung = vorlage.Variante == COPA_12
                    ? "Eigene Beschreibung"
                    : TurnierVorlagenSeedDaten.AlteBeschreibungen[(vorlage.Name, vorlage.Variante)];
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            ctx.Regelwerke.Remove(await ctx.Regelwerke.SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.COPA_DIREKTES_ELFMETERSCHIESSEN));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            (await VerfahrenDerRundeAsync(ctx, COPA_VORLAGE, COPA_16, "Viertelfinale")).Should().Equal(VerfahrenArt.Elfmeterschiessen);
            (await VerfahrenDerRundeAsync(ctx, COPA_VORLAGE, COPA_12, "Spiel um Platz 3")).Should().Equal(VerfahrenArt.Elfmeterschiessen);
            (await VerfahrenDerRundeAsync(ctx, COPA_VORLAGE, COPA_16, "Finale")).Should().Contain(VerfahrenArt.VerlaengerungKlassisch);
            (await VerfahrenDerRundeAsync(ctx, "FIFA WM", "32 Teams · 1998–2022", "Viertelfinale"))
                .Should().Contain(VerfahrenArt.VerlaengerungKlassisch, "andere Vorlagen behalten das FIFA-Regelwerk");

            var halbfinale = await ctx.TurnierRunden.SingleAsync(r =>
                r.Name == "Halbfinale" && r.TurnierInstanz.Name == COPA_VORLAGE && r.TurnierInstanz.Variante == COPA_16
                && r.TurnierInstanz.Status == TurnierStatus.Vorlage);
            halbfinale.RegelwerkId.Should().Be(golden, "ein manuell gewähltes Regelwerk bleibt erhalten");

            var beschreibungen = await ctx.TurnierInstanzen
                .Where(i => i.Name == COPA_VORLAGE && i.Status == TurnierStatus.Vorlage)
                .ToDictionaryAsync(i => i.Variante, i => i.Beschreibung);
            beschreibungen[COPA_16].Should().Contain("Verlängerung nur im Finale");
            beschreibungen[COPA_12].Should().Be("Eigene Beschreibung", "eine manuell geänderte Beschreibung bleibt erhalten");
        }
    }

    // ── EM-Formate vor 1996 (Issue #375) ───────────────────────────────────

    private const string EM_VORLAGE = "UEFA EM";
    private const string EM_4 = "4 Teams · 1960–1976";
    private const string EM_8_1980 = "8 Teams · 1980";
    private const string EM_8_ALT = "8 Teams · 1960–1992";

    [Fact]
    public void Erstellen_Em1980FuehrtGruppensiegerInsFinaleUndGruppenzweiteInsSpielUmPlatzDrei()
    {
        var em = TurnierVorlagenSeedDaten.Erstellen().Single(v => v.Name == EM_VORLAGE && v.Variante == EM_8_1980);

        em.Runden.Select(r => r.Rundenart).Should().Equal(Rundenart.Gruppenphase, Rundenart.SpielUmPlatzDrei, Rundenart.Finale);
        var regeln = em.Runden.Single(r => r.Rundenart == Rundenart.Gruppenphase).Uebergangsregeln;
        regeln.Where(u => u.ZielRunde.Rundenart == Rundenart.Finale)
            .Select(u => (u.GruppeIndex, u.PlatzierungVon, u.PlatzierungBis)).Should().BeEquivalentTo([(1, 1, 1), (2, 1, 1)]);
        regeln.Where(u => u.ZielRunde.Rundenart == Rundenart.SpielUmPlatzDrei)
            .Select(u => (u.GruppeIndex, u.PlatzierungVon, u.PlatzierungBis)).Should().BeEquivalentTo([(1, 2, 2), (2, 2, 2)]);

        var paarungen = em.Auslosungen.ToDictionary(
            a => a.TurnierRunde.Rundenart,
            a => a.Ergebnisse.Select(e => (e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName)).Single());
        paarungen[Rundenart.Finale].Should().Be(("1. Gruppe A", "1. Gruppe B"));
        paarungen[Rundenart.SpielUmPlatzDrei].Should().Be(("2. Gruppe A", "2. Gruppe B"));
    }

    [Fact]
    public async Task InitialisierenAsync_EmVorlagenVor1996BildenDieDreiHistorischenFormateAb()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

        (await RundenNamenAsync(ctx, EM_4)).Should().Equal("Halbfinale", "Spiel um Platz 3", "Finale");
        (await RundenNamenAsync(ctx, EM_8_1980)).Should().Equal("Gruppenphase", "Spiel um Platz 3", "Finale");
        (await RundenNamenAsync(ctx, TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992))
            .Should().Equal("Gruppenphase", "Halbfinale", "Finale");

        (await VerfahrenDerRundeAsync(ctx, EM_VORLAGE, EM_8_1980, "Spiel um Platz 3"))
            .Should().Equal([VerfahrenArt.Elfmeterschiessen], "1980 ohne Verlängerung");
        (await VerfahrenDerRundeAsync(ctx, EM_VORLAGE, EM_8_1980, "Finale"))
            .Should().Equal(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
        (await VerfahrenDerRundeAsync(ctx, EM_VORLAGE, TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992, "Halbfinale"))
            .Should().Equal(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
    }

    [Fact]
    public async Task InitialisierenAsync_TeiltAlteEmSammelvorlageInBestehenderDatenbankAuf()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
            await AlteEmSammelvorlageHerstellenAsync(ctx);
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            var varianten = await ctx.TurnierInstanzen
                .Where(i => i.Name == EM_VORLAGE && i.Status == TurnierStatus.Vorlage && (i.Variante.StartsWith("8 Teams") || i.Variante.StartsWith("4 Teams")))
                .Select(i => i.Variante).ToListAsync();
            varianten.Should().BeEquivalentTo(EM_4, EM_8_1980, TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992);

            var em = await ctx.TurnierInstanzen.Include(i => i.Runden).Include(i => i.Auslosungen)
                .SingleAsync(i => i.Name == EM_VORLAGE && i.Variante == TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992);
            em.Runden.OrderBy(r => r.Reihenfolge).Select(r => (r.Reihenfolge, r.Name))
                .Should().Equal((1, "Gruppenphase"), (2, "Halbfinale"), (3, "Finale"));
            em.Auslosungen.Select(a => a.Name).Should().BeEquivalentTo("Halbfinale", "Finale");
            em.Startdatum.Should().Be(new DateOnly(1984, 1, 1));
            em.Beschreibung.Should().Contain("kein Spiel um Platz 3");
            foreach (string runde in new[] { "Gruppenphase", "Halbfinale", "Finale" })
                (await VerfahrenDerRundeAsync(ctx, EM_VORLAGE, TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992, runde))
                    .Should().Equal(VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen);
            (await VerfahrenDerRundeAsync(ctx, "FIFA WM", TurnierVorlagenSeedDaten.WM_16_TEAMS_1934_BIS_1938, "Finale"))
                .Should().Contain(VerfahrenArt.Wiederholungsspiel, "andere Vorlagen behalten ihr Regelwerk");
        }
    }

    [Fact]
    public async Task InitialisierenAsync_LaesstUmgebauteEmSammelvorlageBisAufDenNamenUnangetastet()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();
            await AlteEmSammelvorlageHerstellenAsync(ctx);
            var platzDrei = await ctx.TurnierRunden.SingleAsync(r =>
                r.TurnierInstanz.Name == EM_VORLAGE && r.TurnierInstanz.Variante == EM_8_ALT && r.Rundenart == Rundenart.SpielUmPlatzDrei);
            platzDrei.Name = "Kleines Finale";
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TurnierverwaltungDbContext(optionen))
        {
            await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

            (await RundenNamenAsync(ctx, TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992))
                .Should().Equal("Gruppenphase", "Halbfinale", "Kleines Finale", "Finale");
        }
    }

    // Stand vor #375: eine Sammelvorlage "8 Teams · 1960–1992" mit Halbfinale UND Spiel um Platz 3 auf dem
    // Regelwerk mit Wiederholungsspiel + Los; die Vorlagen 1960–1976 und 1980 gibt es noch nicht.
    private static async Task AlteEmSammelvorlageHerstellenAsync(TurnierverwaltungDbContext ctx)
    {
        foreach (string variante in new[] { EM_4, EM_8_1980 })
        {
            var neu = await ctx.TurnierInstanzen.Include(i => i.Runden).ThenInclude(r => r.Uebergangsregeln)
                .Include(i => i.Auslosungen)
                .SingleAsync(i => i.Name == EM_VORLAGE && i.Variante == variante);
            ctx.Uebergangsregeln.RemoveRange(neu.Runden.SelectMany(r => r.Uebergangsregeln));
            ctx.Auslosungen.RemoveRange(neu.Auslosungen);
            ctx.TurnierInstanzen.Remove(neu);
        }

        int altesRegelwerkId = (await ctx.Regelwerke
            .SingleAsync(r => r.Name == TurnierVorlagenSeedDaten.HISTORISCH_WIEDERHOLUNGSSPIEL_LOS)).Id;
        var em = await ctx.TurnierInstanzen.Include(i => i.Runden)
            .SingleAsync(i => i.Name == EM_VORLAGE && i.Variante == TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992);
        em.Variante = EM_8_ALT;
        em.Startdatum = new DateOnly(1960, 1, 1);
        em.Beschreibung = TurnierVorlagenSeedDaten.AlteBeschreibungen[(EM_VORLAGE, TurnierVorlagenSeedDaten.EM_8_TEAMS_1984_BIS_1992)];
        em.Runden.Single(r => r.Name == "Finale").Reihenfolge = 4;
        var platzDrei = new Domain.Entitaeten.Turniere.TurnierRunde
        {
            Reihenfolge = 3, Name = "Spiel um Platz 3", Rundenart = Rundenart.SpielUmPlatzDrei,
            EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, PaarungFestVorgegeben = true,
        };
        em.Runden.Add(platzDrei);
        foreach (var runde in em.Runden) runde.RegelwerkId = altesRegelwerkId;
        ctx.Auslosungen.Add(new Domain.Entitaeten.Auslosungen.Auslosung
        {
            TurnierInstanz = em, TurnierRunde = platzDrei, Name = platzDrei.Name,
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen,
        });
        await ctx.SaveChangesAsync();
    }

    private static Task<List<string>> RundenNamenAsync(TurnierverwaltungDbContext ctx, string emVariante) =>
        ctx.TurnierRunden
            .Where(r => r.TurnierInstanz.Name == EM_VORLAGE && r.TurnierInstanz.Variante == emVariante
                     && r.TurnierInstanz.Status == TurnierStatus.Vorlage)
            .OrderBy(r => r.Reihenfolge)
            .Select(r => r.Name)
            .ToListAsync();

    private static async Task<List<VerfahrenArt>> VerfahrenDerRundeAsync(
        TurnierverwaltungDbContext ctx, string vorlage, string variante, string rundenName)
    {
        var runde = await ctx.TurnierRunden.SingleAsync(r =>
            r.Name == rundenName && r.TurnierInstanz.Name == vorlage && r.TurnierInstanz.Variante == variante
            && r.TurnierInstanz.Status == TurnierStatus.Vorlage);
        return await ctx.Regelwerke
            .Where(r => r.Id == runde.RegelwerkId)
            .SelectMany(r => r.Entscheidungsverfahren)
            .OrderBy(e => e.Reihenfolge)
            .Select(e => e.VerfahrenArt)
            .ToListAsync();
    }

    [Fact]
    public async Task InitialisierenAsync_FairplayWertungBevorzugtDasTeamMitWenigerKarten()
    {
        var optionen = new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var ctx = new TurnierverwaltungDbContext(optionen);
        await SeedTestDienst.Erzeugen(ctx).InitialisierenAsync();

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
