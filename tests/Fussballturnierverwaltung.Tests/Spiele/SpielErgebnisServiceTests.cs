using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Validierung;
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

namespace Fussballturnierverwaltung.Tests.Spiele;

public class SpielErgebnisServiceTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private static TurnierverwaltungDbContext NeuerContext() =>
        new(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SpielplanService SpielplanServiceFuer(TurnierverwaltungDbContext ctx)
    {
        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        var mannschaftRepo = new MannschaftRepository(ctx);
        var baumRepo = new TurnierBaumRepository(ctx);
        var uebergangsregelRepo = new UebergangsregelRepository(ctx);
        var factory = new SpielplanGeneratorFactory([new KoRundeGenerator(), new RoundRobinGenerator()]);
        return new SpielplanService(
            rundeRepo, spielRepo, mannschaftRepo, baumRepo, uebergangsregelRepo,
            factory, new TurnierValidator(), new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)),
            new AuslosungRepository(ctx), NullLogger<SpielplanService>.Instance);
    }

    private static SpielErgebnisService ServiceFuer(TurnierverwaltungDbContext ctx) =>
        new(new SpielRepository(ctx),
            new SpielErgebnisValidator(),
            SpielplanServiceFuer(ctx),
            NullLogger<SpielErgebnisService>.Instance);

    private static void GrundSetupAnlegen(TurnierverwaltungDbContext ctx, bool auswaertstorRegel)
    {
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Achtelfinale",
            Rundenart = Rundenart.KoRunde,
            RegelwerkId = 1, Reihenfolge = 1,
            MitRueckspiel = true,
            AuswaertstorRegelAktiv = auswaertstorRegel
        });
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team B", Kurzname = "TB", LandId = 1 });
        ctx.SaveChanges();
    }

    private static void HinspielAnlegen(
        TurnierverwaltungDbContext ctx, int spielId, int rundeId,
        int heimId, int auswaertsId,
        int heimTore, int auswaertsTore)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = spielId, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = spielId, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = SpielStatus.Abgeschlossen, IstRueckspiel = false
        });
        ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            Id = spielId, SpielId = spielId,
            HeimToreRegulaer = heimTore, AuswaertsToreRegulaer = auswaertsTore,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit,
            SiegerMannschaftId = heimTore > auswaertsTore ? heimId
                               : auswaertsTore > heimTore ? auswaertsId
                               : null
        });
        ctx.SaveChanges();
    }

    private static void RueckspielAnlegen(
        TurnierverwaltungDbContext ctx, int spielId, int rundeId,
        int heimId, int auswaertsId, int hinspielId)
    {
        ctx.Spiele.Add(new Spiel
        {
            Id = spielId, TurnierInstanzId = 1, TurnierRundeId = rundeId,
            Spielnummer = spielId, HeimMannschaftId = heimId, AuswaertsMannschaftId = auswaertsId,
            Status = SpielStatus.Geplant, IstRueckspiel = true, HinspielId = hinspielId
        });
        ctx.SaveChanges();
    }

    private static async Task<SpielErgebnis?> ErgebnisFuer(TurnierverwaltungDbContext ctx, int spielId)
        => await ctx.SpielErgebnisse.FirstOrDefaultAsync(e => e.SpielId == spielId);

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MehrereErgebnisseLoeschenAsync_LoeschtErgebnisseUndEreignisseGebuendelt_LaesstAndereSpieleUnberuehrt()
    {
        // Issue #386: gebündeltes Löschen in einer Abfrage/einem Speichern statt Spiel für Spiel.
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 2, auswaertsTore: 1);
        HinspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, heimTore: 0, auswaertsTore: 0);
        HinspielAnlegen(ctx, spielId: 3, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 1, auswaertsTore: 3);
        ctx.SpielEreignisse.AddRange(
            new SpielEreignis { SpielId = 1, Minute = 10, EreignisArt = EreignisArt.Tor, MannschaftId = 1 },
            new SpielEreignis { SpielId = 3, Minute = 20, EreignisArt = EreignisArt.Tor, MannschaftId = 2 });
        ctx.SaveChanges();

        await ServiceFuer(ctx).MehrereErgebnisseLoeschenAsync([1, 2]);

        var spiele = await ctx.Spiele.AsNoTracking().Include(s => s.Ergebnis).Include(s => s.Ereignisse)
            .ToDictionaryAsync(s => s.Id);
        foreach (var id in new[] { 1, 2 })
        {
            spiele[id].Ergebnis.Should().BeNull();
            spiele[id].Ereignisse.Should().BeEmpty();
            spiele[id].Status.Should().Be(SpielStatus.Geplant);
        }
        spiele[3].Ergebnis.Should().NotBeNull("nicht ausgewählte Spiele behalten ihr Ergebnis");
        spiele[3].Ereignisse.Should().HaveCount(1);
        spiele[3].Status.Should().Be(SpielStatus.Abgeschlossen);
    }

    [Fact]
    public async Task EinfachesSpiel_HeimGewinnt_SiegerWirdGesetzt()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Geplant, IstRueckspiel = false
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(2, 1, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 1);
        erg.Should().NotBeNull();
        erg!.SiegerMannschaftId.Should().Be(1);
        erg.HeimToreRegulaer.Should().Be(2);
        erg.AuswaertsToreRegulaer.Should().Be(1);
    }

    [Fact]
    public async Task Rueckspiel_AggregateUngleich_GesamtsiegerWirdErmittelt()
    {
        // Hinspiel: Team A (Heim/id=1) 1:0 → Team A führt
        // Rückspiel: Team B (Heim/id=2) 2:0 → Aggregat: A=1, B=2 → Team B gewinnt
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: true);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 1, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(2, 0, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        // Aggregat: Team A = hinspiel.Heim(1) + rückspiel.Auswärts(0) = 1
        //           Team B = hinspiel.Auswärts(0) + rückspiel.Heim(2) = 2 → Team B gewinnt
        erg!.SiegerMannschaftId.Should().Be(2, "Team B hat das bessere Aggregat");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    [Fact]
    public async Task Rueckspiel_AggregateGleich_AuswaertsToreEntscheiden()
    {
        // Hinspiel: Team A (id=1) heim 0:1 → Team B hat 1 Auswärtstor
        // Rückspiel: Team B (id=2) heim 0:2 → Team A hat 2 Auswärtstore
        // Aggregat: A=0+2=2, B=1+0=1 → Nein, ungleich!
        //
        // Stattdessen: Hinspiel 0:1, Rückspiel 1:0 → Aggregat: A=0+0=0, B=1+1=2 → ungleich
        //
        // Gleich-Aggregat + unterschiedliche Auswärtstore:
        // Hinspiel: A heim 0:1 (B hat 1 Auswärtstor)
        // Rückspiel: B heim 1:2 (A hat 2 Auswärtstore) → Aggregat A=0+2=2, B=1+1=2 → gleich!
        // Auswärtstore: A=2, B=1 → Team A gewinnt via Auswaertstorregel
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: true);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 0, auswaertsTore: 1);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 2, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        // teamA_gesamt = hinspiel.Heim(0) + rückspiel.Auswärts(2) = 2
        // teamB_gesamt = hinspiel.Auswärts(1) + rückspiel.Heim(1) = 2 → gleich
        // teamA_weg = rückspiel.Auswärts(2) > teamB_weg = hinspiel.Auswärts(1) → Team A gewinnt
        erg!.SiegerMannschaftId.Should().Be(1, "Team A hat mehr Auswärtstore (2 vs 1)");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.Auswaertstorregel);
    }

    [Fact]
    public async Task Rueckspiel_OhneAuswaertstorRegel_GesamtGleich_KeinSiegerOhneEntscheidungsverfahren()
    {
        // Gleiche Konstellation, aber AuswaertstorRegel ist deaktiviert (Issue #346: früher entschied hier
        // fälschlich das Einzelspiel-Ergebnis des Rückspiels).
        // Gesamt: A = 0+2 = 2, B = 1+1 = 2 → ohne Auswärtstorregel Entscheidungsverfahren nötig
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);  // deaktiviert!
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 0, auswaertsTore: 1);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 2, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        erg!.SiegerMannschaftId.Should().BeNull("das Gesamtergebnis ist ausgeglichen, die Auswärtstorregel greift nicht");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    // ── Tests: Gesamtwertung Hin-/Rückspiel (Issue #346) ────────────────────

    [Fact]
    public async Task Rueckspiel_OhneAuswaertstorRegel_GesamtsiegerTrotzRueckspielNiederlage()
    {
        // Hinspiel: A (id=1) heim 3:0. Rückspiel: B (id=2) heim 1:0 → gesamt A 3, B 1 → A weiter,
        // obwohl A das Rückspiel verliert (früher: Validierungsfehler bzw. B als Sieger).
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 3, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 0, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 2);
        erg!.SiegerMannschaftId.Should().Be(1, "Team A gewinnt das Gesamtergebnis 3:1");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    [Fact]
    public async Task Rueckspiel_GesamtGleich_VerlaengerungstoreEntscheiden()
    {
        // Hinspiel: A heim 1:0. Rückspiel: B heim 1:0 → gesamt 1:1. Verlängerung 0:1 → A (Auswärts im
        // Rückspiel) gewinnt — obwohl das Rückspiel selbst (1:1 n.V.) nach Einzelspiel-Logik offen wäre.
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 1, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(
            1, 0, EntscheidungArt.Verlaengerung, HeimToreVerlaengerung: 0, AuswaertsToreVerlaengerung: 1));

        var erg = await ErgebnisFuer(ctx, 2);
        erg!.SiegerMannschaftId.Should().Be(1);
        erg.EntscheidungArt.Should().Be(EntscheidungArt.Verlaengerung);
    }

    [Fact]
    public async Task Rueckspiel_OhneHinspielErgebnis_Wirft()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);

        var service = ServiceFuer(ctx);
        var aufruf = () => service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 0, EntscheidungArt.RegulaereSpielzeit));

        await aufruf.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Hinspiel*");
    }

    [Fact]
    public async Task KoHinspiel_MitElfmeterschiessen_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Geplant
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        var aufruf = () => service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(
            1, 1, EntscheidungArt.Elfmeterschiessen, HeimElfmetertore: 5, AuswaertsElfmetertore: 4));

        await aufruf.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task KoHinspiel_ErgebnisGeaendert_SetztGespieltesRueckspielZurueck()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 3, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);
        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 0, EntscheidungArt.RegulaereSpielzeit));

        // Hinspiel nachträglich korrigiert → die Gesamtwertung des Rückspiels ist hinfällig.
        await service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(0, 0, EntscheidungArt.RegulaereSpielzeit));

        (await ErgebnisFuer(ctx, 2)).Should().BeNull();
        ctx.Spiele.First(s => s.Id == 2).Status.Should().NotBe(SpielStatus.Abgeschlossen);
        (await ErgebnisFuer(ctx, 1))!.HeimToreRegulaer.Should().Be(0);
    }

    [Fact]
    public async Task Rueckspiel_ErgebnisUeberschriebenMitAnderemSieger_KorrigiertFolgerunde()
    {
        // Manueller #346-Testfund: Rückspiel zuerst regulär für B entschieden, dann per Elfmeterschießen für A
        // umentschieden — das Viertelfinale behielt den alten Sieger B.
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 3, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 4, Name = "Team D", Kurzname = "TD", LandId = 1 });
        ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 2, TurnierInstanzId = 1, Name = "Viertelfinale", Rundenart = Rundenart.KoRunde,
            RegelwerkId = 1, Reihenfolge = 2
        });
        ctx.SaveChanges();
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 2, auswaertsTore: 1);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);
        HinspielAnlegen(ctx, spielId: 3, rundeId: 1, heimId: 3, auswaertsId: 4, heimTore: 1, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 4, rundeId: 1, heimId: 4, auswaertsId: 3, hinspielId: 3);

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(2, 0, EntscheidungArt.RegulaereSpielzeit)); // B gesamt 3:2
        await service.ErgebnisErfassenAsync(4, new ErgebnisErfassenEingabe(0, 0, EntscheidungArt.RegulaereSpielzeit)); // C gesamt 1:0

        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = 2, Name = "VF-Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        ctx.SaveChanges();
        ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
        {
            AuslosungId = auslosung.Id, PaarungsNummer = 1,
            HeimPlatzhalterName = "Sieger Achtelfinale 1", AuswaertsPlatzhalterName = "Sieger Achtelfinale 2"
        });
        ctx.SaveChanges();
        await SpielplanServiceFuer(ctx).GenerierenAusAuslosungAsync(
            2, [.. ctx.AuslosungsErgebnisse.Where(e => e.AuslosungId == auslosung.Id)]);
        ctx.Spiele.AsNoTracking().Single(s => s.TurnierRundeId == 2).HeimMannschaftId
            .Should().Be(2, "Vorbedingung: Team B hat Paarung 1 gewonnen");

        // Rückspiel 1:0 → gesamt 2:2, Elfmeterschießen 3:4 → Team A (Auswärts im Rückspiel) weiter
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(
            1, 0, EntscheidungArt.Elfmeterschiessen,
            HeimToreVerlaengerung: 0, AuswaertsToreVerlaengerung: 0,
            HeimElfmetertore: 3, AuswaertsElfmetertore: 4));

        (await ErgebnisFuer(ctx, 2))!.SiegerMannschaftId.Should().Be(1);
        var vf = ctx.Spiele.AsNoTracking().Single(s => s.TurnierRundeId == 2);
        vf.HeimMannschaftId.Should().Be(1, "der neue Gesamtsieger Team A rückt nach");
        vf.AuswaertsMannschaftId.Should().Be(3);
    }

    [Fact]
    public async Task KoHinspiel_ErgebnisGeloescht_LoeschtRueckspielErgebnisMit()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        HinspielAnlegen(ctx, spielId: 1, rundeId: 1, heimId: 1, auswaertsId: 2, heimTore: 3, auswaertsTore: 0);
        RueckspielAnlegen(ctx, spielId: 2, rundeId: 1, heimId: 2, auswaertsId: 1, hinspielId: 1);
        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(2, new ErgebnisErfassenEingabe(1, 0, EntscheidungArt.RegulaereSpielzeit));

        await service.ErgebnisLoeschenAsync(1);

        (await ErgebnisFuer(ctx, 1)).Should().BeNull();
        (await ErgebnisFuer(ctx, 2)).Should().BeNull();
    }

    [Fact]
    public async Task Unentschieden_KeinSieger()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Geplant, IstRueckspiel = false
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(1, 1, EntscheidungArt.RegulaereSpielzeit));

        var erg = await ErgebnisFuer(ctx, 1);
        erg!.SiegerMannschaftId.Should().BeNull("ein Unentschieden ergibt keinen Sieger");
        erg.EntscheidungArt.Should().Be(EntscheidungArt.RegulaereSpielzeit);
    }

    [Fact]
    public async Task MitHalbzeitstand_WirdPersistiert()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Geplant, IstRueckspiel = false
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        await service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(
            2, 1, EntscheidungArt.RegulaereSpielzeit,
            HalbzeitHeimTore: 1, HalbzeitAuswaertsTore: 0));

        var erg = await ErgebnisFuer(ctx, 1);
        erg!.HalbzeitHeimTore.Should().Be(1);
        erg.HalbzeitAuswaertsTore.Should().Be(0);
    }

    [Fact]
    public async Task MitUngueltigemHalbzeitstand_WirftValidationException()
    {
        await using var ctx = NeuerContext();
        GrundSetupAnlegen(ctx, auswaertstorRegel: false);
        ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1,
            Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2,
            Status = SpielStatus.Geplant, IstRueckspiel = false
        });
        ctx.SaveChanges();

        var service = ServiceFuer(ctx);
        // 2 Tore zur Halbzeit, aber nur 1 Tor im Endstand.
        var aufruf = () => service.ErgebnisErfassenAsync(1, new ErgebnisErfassenEingabe(
            1, 0, EntscheidungArt.RegulaereSpielzeit,
            HalbzeitHeimTore: 2, HalbzeitAuswaertsTore: 0));

        await aufruf.Should().ThrowAsync<ValidationException>();
    }

    // ── Tests: Kaskaden-Check nach Batch-Reset (Issue #125) ─────────────────

    private static void KaskadeSetupAnlegen(TurnierverwaltungDbContext ctx, out int hfRundeId, out int finaleRundeId)
    {
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        var hf = new TurnierRunde
        {
            Id = 10, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        var finale = new TurnierRunde
        {
            Id = 11, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 2,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(hf, finale);

        ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 10, Name = "Team A", Kurzname = "TA", LandId = 1 },
            new Mannschaft { Id = 11, Name = "Team B", Kurzname = "TB", LandId = 1 },
            new Mannschaft { Id = 12, Name = "Team C", Kurzname = "TC", LandId = 1 },
            new Mannschaft { Id = 13, Name = "Team D", Kurzname = "TD", LandId = 1 });

        ctx.Spiele.AddRange(
            new Spiel
            {
                Id = 20, TurnierInstanzId = 1, TurnierRundeId = hf.Id, Spielnummer = 1,
                HeimMannschaftId = 10, AuswaertsMannschaftId = 11, Status = SpielStatus.Abgeschlossen
            },
            new Spiel
            {
                Id = 21, TurnierInstanzId = 1, TurnierRundeId = hf.Id, Spielnummer = 2,
                HeimMannschaftId = 12, AuswaertsMannschaftId = 13, Status = SpielStatus.Abgeschlossen
            });
        ctx.SpielErgebnisse.AddRange(
            new SpielErgebnis
            {
                Id = 20, SpielId = 20, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 10
            },
            new SpielErgebnis
            {
                Id = 21, SpielId = 21, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 12
            });

        ctx.SaveChanges();
        hfRundeId = hf.Id;
        finaleRundeId = finale.Id;
    }

    [Fact]
    public async Task MehrereErgebnisseLoeschenAsync_LoestKaskadeAus_EntferntAutomatischGeneriertesFinale()
    {
        await using var ctx = NeuerContext();
        KaskadeSetupAnlegen(ctx, out _, out int finaleRundeId);

        // Vorbedingung: Finale wurde automatisch generiert, weil das Halbfinale entschieden ist
        await SpielplanServiceFuer(ctx).FinaleAutomatischGenerierenWennBereitAsync(1);
        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().HaveCount(1,
            "Vorbedingung: Finale wurde automatisch generiert");

        // Batch-Reset eines Halbfinalspiels — vor Issue #125 löste MehrereErgebnisseLoeschenAsync
        // (anders als das Einzelspiel-Löschen) keinen Kaskaden-Check aus.
        await ServiceFuer(ctx).MehrereErgebnisseLoeschenAsync([20]);

        ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).Should().BeEmpty(
            "Issue #125: MehrereErgebnisseLoeschenAsync muss denselben Kaskaden-Check auslösen wie das " +
            "Einzelspiel-Löschen — das noch nicht gespielte Finale muss entfernt werden");
    }

    [Fact]
    public async Task ErgebnisLoeschenAsync_LoestPlatzhalterKaskadeAus_RegeneriertUeberAuslosungAufgeloesteFolgerunde()
    {
        await using var ctx = NeuerContext();
        KaskadeSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);

        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Name = "Finale-Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        ctx.SaveChanges();
        ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
        {
            AuslosungId = auslosung.Id, PaarungsNummer = 1,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Sieger HF 2"
        });
        ctx.SaveChanges();

        var spielplanService = SpielplanServiceFuer(ctx);
        await spielplanService.GenerierenAusAuslosungAsync(
            finaleRundeId, [.. ctx.AuslosungsErgebnisse.Where(e => e.AuslosungId == auslosung.Id)]);
        await spielplanService.PlatzhalterAufloesenAsync(finaleRundeId);

        var finaleSpielVorher = ctx.Spiele.First(s => s.TurnierRundeId == finaleRundeId);
        finaleSpielVorher.HeimMannschaftId.Should().Be(10, "Vorbedingung: Sieger HF 1 = Team A");

        // Halbfinalspiel 1 (Team A vs. Team B) wird zurückgesetzt — über SpielErgebnisService, nicht
        // direkt über SpielplanService, um die vollständige Verdrahtung (Issue #88) zu prüfen.
        await ServiceFuer(ctx).ErgebnisLoeschenAsync(20);

        var finaleSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == finaleRundeId).ToList();
        finaleSpiele.Should().HaveCount(1);
        finaleSpiele[0].Id.Should().Be(finaleSpielVorher.Id,
            "das Spiel wird gezielt korrigiert statt die ganze Runde zu löschen und neu anzulegen");
        finaleSpiele[0].HeimMannschaftId.Should().BeNull(
            "Sieger HF 1 ist nach dem Zurücksetzen nicht mehr bekannt, das Finale muss wieder auf Platzhalter stehen");
        finaleSpiele[0].HeimPlatzhalterName.Should().Be("Sieger HF 1");
        finaleSpiele[0].AuswaertsMannschaftId.Should().Be(12,
            "Sieger HF 2 ist weiterhin bekannt (Team C) und darf durch die Korrektur der Heim-Seite nicht angefasst werden");
    }

    // ── Tests: Automatische Platzhalter-Auflösung nach Rundenabschluss (Issue #90) ─────────

    [Fact]
    public async Task ErgebnisErfassenAsync_RundeWirdKomplett_LoestPlatzhalterFolgerundeAutomatischAuf()
    {
        await using var ctx = NeuerContext();
        KaskadeSetupAnlegen(ctx, out int hfRundeId, out int finaleRundeId);

        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Name = "Finale-Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        ctx.SaveChanges();
        ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
        {
            AuslosungId = auslosung.Id, PaarungsNummer = 1,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Sieger HF 2"
        });
        ctx.SaveChanges();

        var spielplanService = SpielplanServiceFuer(ctx);
        await spielplanService.GenerierenAusAuslosungAsync(
            finaleRundeId, [.. ctx.AuslosungsErgebnisse.Where(e => e.AuslosungId == auslosung.Id)]);

        // Halbfinalspiel 2 wird zurückgesetzt, damit die Runde unten wirklich erst durch das erneute
        // Erfassen komplett wird (Vorbedingung: noch KEIN manueller PlatzhalterAufloesenAsync-Aufruf,
        // anders als im Nachbartest oben).
        await ServiceFuer(ctx).ErgebnisLoeschenAsync(21);

        var finaleVorher = ctx.Spiele.AsNoTracking().First(s => s.TurnierRundeId == finaleRundeId);
        // Issue #346-Folgefund: das bereits entschiedene HF 1 wird schon beim Generieren eingetragen.
        finaleVorher.HeimMannschaftId.Should().Be(10, "Vorbedingung: Sieger HF 1 steht schon bei der Generierung fest");
        finaleVorher.AuswaertsMannschaftId.Should().BeNull("Vorbedingung: HF 2 noch offen, nicht aufgelöst");

        // Kein manueller PlatzhalterAufloesenAsync-Aufruf hier — Issue #90 verlangt, dass das Speichern
        // dieses Ergebnisses (macht die Runde komplett) die Auflösung von selbst anstößt.
        await ServiceFuer(ctx).ErgebnisErfassenAsync(21, new ErgebnisErfassenEingabe(1, 0, EntscheidungArt.RegulaereSpielzeit));

        var finaleNachher = ctx.Spiele.AsNoTracking().First(s => s.TurnierRundeId == finaleRundeId);
        finaleNachher.HeimMannschaftId.Should().Be(10, "Sieger HF 1 = Team A, automatisch aufgelöst ohne manuellen Trigger");
        finaleNachher.AuswaertsMannschaftId.Should().Be(12, "Sieger HF 2 = Team C, automatisch aufgelöst ohne manuellen Trigger");
    }

    [Fact]
    public async Task ErgebnisErfassenAsync_LoestBereitsBeiTeilweiseKompletterRundeDieAufloesbareSeiteAuf()
    {
        // Issue #90 kombiniert mit #206: die Auflösung läuft absichtlich OHNE Vorprüfung "ist die ganze
        // Runde komplett" — eine frühere Fassung prüfte genau das und verpasste dadurch den Fall, dass
        // eine Runde mehrere unabhängige Teile hat (hier: zwei Halbfinalspiele) und einer davon längst
        // fertig ist, während der andere noch offen ist. Die auflösbare Seite ("Sieger HF 1") muss
        // sofort geschrieben werden, auch wenn Spiel 2 (und damit "Sieger HF 2") noch offen ist.
        await using var ctx = NeuerContext();
        KaskadeSetupAnlegen(ctx, out _, out int finaleRundeId);

        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = finaleRundeId, Name = "Finale-Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        ctx.SaveChanges();
        ctx.AuslosungsErgebnisse.Add(new AuslosungsErgebnis
        {
            AuslosungId = auslosung.Id, PaarungsNummer = 1,
            HeimPlatzhalterName = "Sieger HF 1", AuswaertsPlatzhalterName = "Sieger HF 2"
        });
        ctx.SaveChanges();

        var spielplanService = SpielplanServiceFuer(ctx);
        await spielplanService.GenerierenAusAuslosungAsync(
            finaleRundeId, [.. ctx.AuslosungsErgebnisse.Where(e => e.AuslosungId == auslosung.Id)]);

        // Halbfinalspiel 2 UND 1 zurücksetzen, dann nur Spiel 1 neu erfassen — Spiel 2 bleibt offen.
        await ServiceFuer(ctx).MehrereErgebnisseLoeschenAsync([20, 21]);
        await ServiceFuer(ctx).ErgebnisErfassenAsync(20, new ErgebnisErfassenEingabe(1, 0, EntscheidungArt.RegulaereSpielzeit));

        var finale = ctx.Spiele.AsNoTracking().First(s => s.TurnierRundeId == finaleRundeId);
        finale.HeimMannschaftId.Should().Be(10,
            "Sieger HF 1 = Team A steht schon fest und muss automatisch aufgelöst werden, auch wenn Spiel 2 noch offen ist");
        finale.AuswaertsMannschaftId.Should().BeNull("Sieger HF 2 ist mangels Ergebnis von Spiel 2 noch nicht bekannt");
        finale.AuswaertsPlatzhalterName.Should().Be("Sieger HF 2");
    }

    [Fact]
    public async Task ErgebnisLoeschenAsync_HaeltAutomatischGeneriertesFinaleKonsistent_WennKaskadeVorgelagerteRundeAendert()
    {
        // Regressionstest: Finale hat KEINE eigene Auslosung (klassischer #113/#125-Fall), hängt aber über
        // die #125-Heuristik vom Halbfinale ab, das seinerseits über Auslosung+Platzhalter vom Viertelfinale
        // abhängt (#88-Fall). Wird ein Viertelfinal-Ergebnis zurückgesetzt, muss die #125-Logik NACH der
        // #88-Kaskade erneut laufen — sonst bliebe das Finale auf der jetzt ungültigen, veralteten
        // Halbfinale-Paarung stehen (Issue #88, beim manuellen Test gefunden).
        await using var ctx = NeuerContext();
        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.Entscheidungsverfahren.Add(new EntscheidungsVerfahren
            { Id = 1, RegelwerkId = 1, Reihenfolge = 1, VerfahrenArt = VerfahrenArt.Elfmeterschiessen });

        // Eingang 8 → Kürzel "VF" (KuerzelZuEingangsteams), damit "Sieger VF n" im Halbfinale eindeutig
        // hierauf verweist.
        var viertelfinale = new TurnierRunde
        {
            Id = 30, TurnierInstanzId = 1, Name = "Viertelfinale", Reihenfolge = 1,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 8, AusgangsMannschaftsanzahl = 4,
            RegelwerkId = 1
        };
        var hf = new TurnierRunde
        {
            Id = 31, TurnierInstanzId = 1, Name = "Halbfinale", Reihenfolge = 2,
            Rundenart = Rundenart.KoRunde, EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2,
            RegelwerkId = 1
        };
        var finale = new TurnierRunde
        {
            Id = 32, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 3,
            Rundenart = Rundenart.Finale, EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 1,
            RegelwerkId = 1
        };
        ctx.TurnierRunden.AddRange(viertelfinale, hf, finale);
        ctx.Mannschaften.AddRange(Enumerable.Range(30, 8)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 }));

        // VF1: 30 schlägt 31. VF2: 32 schlägt 33. VF3: 34 schlägt 35. VF4: 36 schlägt 37.
        for (int i = 0; i < 4; i++)
        {
            int spielId = 30 + i, heim = 30 + i * 2, ausw = 31 + i * 2;
            ctx.Spiele.Add(new Spiel
            {
                Id = spielId, TurnierInstanzId = 1, TurnierRundeId = viertelfinale.Id, Spielnummer = spielId,
                HeimMannschaftId = heim, AuswaertsMannschaftId = ausw, Status = SpielStatus.Abgeschlossen
            });
            ctx.SpielErgebnisse.Add(new SpielErgebnis
            {
                Id = spielId, SpielId = spielId, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
                EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = heim
            });
        }
        await ctx.SaveChangesAsync();

        var spielplanService = SpielplanServiceFuer(ctx);
        var auslosung = new Auslosung
        {
            TurnierInstanzId = 1, TurnierRundeId = hf.Id, Name = "HF-Auslosung",
            Auslosungsart = Auslosungsart.Manuell, Status = AuslosungsStatus.Abgeschlossen
        };
        ctx.Auslosungen.Add(auslosung);
        await ctx.SaveChangesAsync();
        ctx.AuslosungsErgebnisse.AddRange(
            new AuslosungsErgebnis { AuslosungId = auslosung.Id, PaarungsNummer = 1, HeimPlatzhalterName = "Sieger VF 1", AuswaertsPlatzhalterName = "Sieger VF 2" },
            new AuslosungsErgebnis { AuslosungId = auslosung.Id, PaarungsNummer = 2, HeimPlatzhalterName = "Sieger VF 3", AuswaertsPlatzhalterName = "Sieger VF 4" });
        await ctx.SaveChangesAsync();
        await spielplanService.GenerierenAusAuslosungAsync(
            hf.Id, [.. ctx.AuslosungsErgebnisse.Where(e => e.AuslosungId == auslosung.Id)]);
        await spielplanService.PlatzhalterAufloesenAsync(hf.Id);

        var hfSpiele = ctx.Spiele.Where(s => s.TurnierRundeId == hf.Id).OrderBy(s => s.Spielnummer).ToList();
        hfSpiele.Should().HaveCount(2);
        var hf1Id = hfSpiele[0].Id; // Team30 vs Team32
        var hf2Id = hfSpiele[1].Id; // Team34 vs Team36

        // Beide Halbfinal-Spiele werden ausgetragen (Team30, Team34 gewinnen) — das Finale wird daraufhin
        // automatisch generiert (#113/#125), aber noch nicht gespielt.
        ctx.Spiele.First(s => s.Id == hf1Id).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 100, SpielId = hf1Id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 30 });
        ctx.Spiele.First(s => s.Id == hf2Id).Status = SpielStatus.Abgeschlossen;
        ctx.SpielErgebnisse.Add(new SpielErgebnis
            { Id = 101, SpielId = hf2Id, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0, EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = 34 });
        await ctx.SaveChangesAsync();
        await spielplanService.FinaleAutomatischGenerierenWennBereitAsync(1);

        var finaleVorher = ctx.Spiele.Where(s => s.TurnierRundeId == finale.Id).ToList();
        finaleVorher.Should().HaveCount(1, "Vorbedingung: Finale wurde automatisch aus Team30/Team34 generiert");
        var finaleTeilnehmerVorher = new[] { finaleVorher[0].HeimMannschaftId, finaleVorher[0].AuswaertsMannschaftId };
        finaleTeilnehmerVorher.Should().BeEquivalentTo(new int?[] { 30, 34 });

        // VF1 wird zurückgesetzt (Ergebnis gelöscht) — betrifft nur HF1 ("Sieger VF 1"), und darüber
        // indirekt auch das Finale.
        await ServiceFuer(ctx).ErgebnisLoeschenAsync(30);

        var hf1Danach = ctx.Spiele.First(s => s.Id == hf1Id);
        hf1Danach.HeimMannschaftId.Should().BeNull("Sieger VF 1 ist nicht mehr bekannt");
        hf1Danach.AuswaertsMannschaftId.Should().Be(32, "Sieger VF 2 bleibt unverändert Team32");
        ctx.SpielErgebnisse.Any(e => e.SpielId == hf1Id).Should().BeFalse("HF1-Ergebnis basierte auf der ungültigen Paarung");

        var hf2Danach = ctx.Spiele.First(s => s.Id == hf2Id);
        hf2Danach.HeimMannschaftId.Should().Be(34, "HF2 ist von VF1 unabhängig und muss unverändert bleiben");
        hf2Danach.AuswaertsMannschaftId.Should().Be(36);
        ctx.SpielErgebnisse.Any(e => e.SpielId == hf2Id).Should().BeTrue("das gültige HF2-Ergebnis darf nicht angefasst werden");

        var finaleDanach = ctx.Spiele.Where(s => s.TurnierRundeId == finale.Id).ToList();
        finaleDanach.Should().BeEmpty(
            "HF1 ist jetzt nicht mehr entschieden — die #125-Logik muss das NACH der #88-Kaskade erneut " +
            "erkennen und darf das Finale nicht auf der alten Team30/Team34-Paarung stehen lassen");
    }
}
