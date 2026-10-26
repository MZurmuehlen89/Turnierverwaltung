using FluentAssertions;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Turniere;

public class TurnierKlonServiceTests
{
    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TurnierKlonService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new TurnierInstanzRepository(ctx), NullLogger<TurnierKlonService>.Instance);

    private static async Task<int> QuelleAnlegenAsync(TurnierverwaltungDbContext ctx)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "UEFA", Kurzname = "UEFA" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Deutschland", Kurzname = "GER", FifaCode = "GER", VerbandId = 1 });

        var quelle = new TurnierInstanz
        {
            Id = 1, Name = "EM 2024", Beschreibung = "Testbeschreibung", Variante = "4 Teams", SaisonOderJahr = "2024",
            Kategorie = TurnierKategorie.Kontinentalmeisterschaft, ZielMannschaftsanzahl = 4,
            Status = TurnierStatus.Abgeschlossen, StandardRegelwerkId = 1, TitelverteidigerId = 42
        };
        var halbfinale = new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2, RegelwerkId = 1,
            VorgegebeneAuslosungsart = Auslosungsart.NachToepfen,
            VorgegebeneGruppenposition = Gruppenposition.AlleGelost,
            StandardRanglisteId = 7, TitelverteidigerInTopf1 = true, GegnerJeTopf = 1, NeutralerSpielort = true,
            TabellenBereiche = [new TabellenBereich { PlatzVon = 2, PlatzBis = 2, Art = TabellenBereichArt.Relegation, Bezeichnung = "Relegation" }]
        };
        var finale = new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1, RegelwerkId = 1
        };
        ctx.TurnierInstanzen.Add(quelle);
        ctx.TurnierRunden.AddRange(halbfinale, finale);
        await ctx.SaveChangesAsync();

        ctx.Uebergangsregeln.Add(new Uebergangsregel
            { QuellRundeId = halbfinale.Id, ZielRundeId = finale.Id, GruppeIndex = 1, PlatzierungVon = 1, PlatzierungBis = 1 });
        ctx.VerbandsQuoten.Add(new VerbandsQuote { TurnierInstanzId = 1, VerbandId = 1, Anzahl = 2 });
        ctx.LandesQuoten.Add(new LandesQuote { TurnierInstanzId = 1, LandId = 1, Anzahl = 1 });
        ctx.Spiele.Add(new Spiel
        {
            TurnierInstanzId = 1, TurnierRundeId = finale.Id, Spielnummer = 1, Status = SpielStatus.Abgeschlossen
        });
        await ctx.SaveChangesAsync();

        return quelle.Id;
    }

    [Fact]
    public async Task KlonenAsync_KopiertKopfFelderMitNeuemNameSaisonUndStatus()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Vorlage);

        klon.Id.Should().NotBe(quellId);
        klon.Name.Should().Be("EM 2028");
        klon.SaisonOderJahr.Should().Be("2028");
        klon.Status.Should().Be(TurnierStatus.Vorlage);
        klon.Beschreibung.Should().Be("Testbeschreibung");
        klon.Variante.Should().Be("4 Teams");
        klon.Kategorie.Should().Be(TurnierKategorie.Kontinentalmeisterschaft);
        klon.ZielMannschaftsanzahl.Should().Be(4);
        klon.StandardRegelwerkId.Should().Be(1);
    }

    [Fact]
    public async Task KlonenAsync_AlsVorlageMitVorhandenemNameUndVariante_WirftValidationException()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);
        await service.KlonenAsync(quellId, "EM", "2028", TurnierStatus.Vorlage);

        var akt = () => service.KlonenAsync(quellId, "EM", "2032", TurnierStatus.Vorlage);

        await akt.Should().ThrowAsync<Application.Validierung.ValidationException>();
        (await service.KlonenAsync(quellId, "EM", "2032", TurnierStatus.Geplant)).Name.Should().Be("EM");
    }

    [Fact]
    public async Task KlonenAsync_KopiertRundenMitNeuenIds()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Vorlage);

        klon.Runden.Should().HaveCount(2);
        klon.Runden.Should().OnlyContain(r => r.TurnierInstanzId == klon.Id);
        klon.Runden.Select(r => r.Id).Should().NotIntersectWith(new[] { 1, 2 });
    }

    [Fact]
    public async Task KlonenAsync_KopiertTabellenbereicheJeRunde()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        var bereich = klon.Runden.Single(r => r.Name == "Halbfinale").TabellenBereiche.Should().ContainSingle().Subject;
        (bereich.PlatzVon, bereich.Art, bereich.Bezeichnung).Should().Be((2, TabellenBereichArt.Relegation, "Relegation"));
        ctx.TabellenBereiche.Count().Should().Be(2, "das Original behält seinen Bereich");
    }

    [Fact]
    public async Task KlonenAsync_UebernimmtVorgegebeneAuslosungsartJeRunde()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        klon.Runden.Single(r => r.Name == "Halbfinale").VorgegebeneAuslosungsart.Should().Be(Auslosungsart.NachToepfen);
        klon.Runden.Single(r => r.Name == "Finale").VorgegebeneAuslosungsart.Should().BeNull();
    }

    [Fact]
    public async Task KlonenAsync_UebernimmtVorgegebeneGruppenpositionJeRunde()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        klon.Runden.Single(r => r.Name == "Halbfinale").VorgegebeneGruppenposition.Should().Be(Gruppenposition.AlleGelost);
        klon.Runden.Single(r => r.Name == "Finale").VorgegebeneGruppenposition.Should().Be(Gruppenposition.NachTopf);
    }

    [Fact]
    public async Task KlonenAsync_UebernimmtStandardRanglisteJeRunde()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        var halbfinale = klon.Runden.Single(r => r.Name == "Halbfinale");
        halbfinale.StandardRanglisteId.Should().Be(7);
        halbfinale.TitelverteidigerInTopf1.Should().BeTrue();
        halbfinale.GegnerJeTopf.Should().Be(1);
        halbfinale.NeutralerSpielort.Should().BeTrue();
        klon.Runden.Single(r => r.Name == "Finale").StandardRanglisteId.Should().BeNull();
        klon.Runden.Single(r => r.Name == "Finale").NeutralerSpielort.Should().BeFalse();
    }

    [Fact]
    public async Task KlonenAsync_UebernimmtTitelverteidigerNicht()
    {
        // Der Titelverteidiger gehört zur Saison: für die geklonte Folgesaison ist es der Sieger der Quelle,
        // nicht deren Titelverteidiger (#349).
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        klon.TitelverteidigerId.Should().BeNull();
    }

    [Fact]
    public async Task KlonenAsync_KopiertUebergangsregelnGemapptAufNeueRundenIds()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Vorlage);

        var halbfinale = klon.Runden.Single(r => r.Name == "Halbfinale");
        var finale = klon.Runden.Single(r => r.Name == "Finale");
        halbfinale.Uebergangsregeln.Should().ContainSingle();
        var regel = halbfinale.Uebergangsregeln.Single();
        regel.ZielRundeId.Should().Be(finale.Id);
        regel.GruppeIndex.Should().Be(1);
    }

    [Fact]
    public async Task KlonenAsync_KopiertVerbandsquoten()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Vorlage);

        klon.VerbandsQuoten.Should().ContainSingle();
        klon.VerbandsQuoten.Single().VerbandId.Should().Be(1);
        klon.VerbandsQuoten.Single().Anzahl.Should().Be(2);

        klon.LandesQuoten.Should().ContainSingle();
        klon.LandesQuoten.Single().LandId.Should().Be(1);
        klon.LandesQuoten.Single().Anzahl.Should().Be(1);
    }

    [Fact]
    public async Task KlonenAsync_KopiertNieSpiele()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Vorlage);

        (await ctx.Spiele.Where(s => s.TurnierInstanzId == klon.Id).ToListAsync()).Should().BeEmpty();
    }

    // ── Feste Vorbelegung (Issue #372) ─────────────────────────────────────────

    private static async Task FesteVorbelegungAnlegenAsync(
        TurnierverwaltungDbContext ctx, int rundeId, int? heimMannschaftId = null)
    {
        var runde = await ctx.TurnierRunden.SingleAsync(r => r.Id == rundeId);
        runde.PaarungFestVorgegeben = true;
        ctx.Auslosungen.Add(new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = rundeId, Name = "Vorbelegung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen,
            Ergebnisse =
            {
                new AuslosungsErgebnis
                {
                    PaarungsNummer = 1, HeimMannschaftId = heimMannschaftId,
                    HeimPlatzhalterName = heimMannschaftId is null ? "1. Gruppe B" : null,
                    AuswaertsPlatzhalterName = "3. Gruppe A/D/E/F"
                },
                new AuslosungsErgebnis
                    { PaarungsNummer = 2, HeimPlatzhalterName = "1. Gruppe A", AuswaertsPlatzhalterName = "2. Gruppe C" },
            }
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task KlonenAsync_KopiertFesteVorbelegungMitPlatzhalternAufNeueRunde()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        await FesteVorbelegungAnlegenAsync(ctx, rundeId: 1);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        var auslosungen = await ctx.Auslosungen.Include(a => a.Ergebnisse)
            .Where(a => a.TurnierInstanzId == klon.Id).ToListAsync();
        var kopie = auslosungen.Should().ContainSingle().Subject;
        kopie.TurnierRundeId.Should().Be(klon.Runden.Single(r => r.Name == "Halbfinale").Id);
        kopie.Auslosungsart.Should().Be(Auslosungsart.Manuell);
        kopie.Status.Should().Be(AuslosungsStatus.Abgeschlossen);
        kopie.Ergebnisse.OrderBy(e => e.PaarungsNummer)
            .Select(e => (e.HeimPlatzhalterName, e.AuswaertsPlatzhalterName))
            .Should().Equal(("1. Gruppe B", "3. Gruppe A/D/E/F"), ("1. Gruppe A", "2. Gruppe C"));
    }

    [Fact]
    public async Task KlonenAsync_KopiertKeineVorbelegungMitEchtenMannschaften()
    {
        using var ctx = NeuerContext();
        var quellId = await QuelleAnlegenAsync(ctx);
        ctx.Mannschaften.Add(new Mannschaft { Id = 1, Name = "Deutschland", Kurzname = "GER" });
        await ctx.SaveChangesAsync();
        await FesteVorbelegungAnlegenAsync(ctx, rundeId: 1, heimMannschaftId: 1);
        var service = ServiceFuer(ctx);

        var klon = await service.KlonenAsync(quellId, "EM 2028", "2028", TurnierStatus.Geplant);

        (await ctx.Auslosungen.Where(a => a.TurnierInstanzId == klon.Id).ToListAsync()).Should().BeEmpty();
    }
}
