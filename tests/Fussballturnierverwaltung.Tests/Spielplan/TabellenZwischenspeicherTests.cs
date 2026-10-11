using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fussballturnierverwaltung.Tests.Spielplan;

/// <summary>Ob etwas aus dem Speicher kam, zeigt die Objektidentität: ein neuer Ladevorgang liefert eine neue Liste.</summary>
public class TabellenZwischenspeicherTests
{
    private const int LIGA = 1;
    private const int GRUPPENPHASE = 2;

    private static TabellenZwischenspeicher SpeicherMitDaten()
    {
        var ctx = new TurnierverwaltungDbContext(new DbContextOptionsBuilder<TurnierverwaltungDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        ctx.Regelwerke.Add(new Regelwerk { Id = 1, Name = "Standard" });
        ctx.TurnierRunden.AddRange(
            new TurnierRunde
            {
                Id = LIGA, TurnierInstanzId = 1, Name = "Liga", Reihenfolge = 1, Rundenart = Rundenart.Liga,
                EingangsMannschaftsanzahl = 2, AusgangsMannschaftsanzahl = 2, RegelwerkId = 1,
            },
            new TurnierRunde
            {
                Id = GRUPPENPHASE, TurnierInstanzId = 1, Name = "Gruppenphase", Reihenfolge = 2, Rundenart = Rundenart.Gruppenphase,
                EingangsMannschaftsanzahl = 4, AusgangsMannschaftsanzahl = 2, RegelwerkId = 1,
            });
        ctx.Mannschaften.AddRange(Enumerable.Range(1, 4)
            .Select(i => new Mannschaft { Id = i, Name = $"Team {i}", Kurzname = $"T{i}", LandId = 1 }));
        ctx.Spiele.AddRange(
            new Spiel { Id = 1, TurnierInstanzId = 1, TurnierRundeId = LIGA, Spielnummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2 },
            new Spiel { Id = 2, TurnierInstanzId = 1, TurnierRundeId = GRUPPENPHASE, Spielnummer = 2, GruppenNummer = 1, HeimMannschaftId = 1, AuswaertsMannschaftId = 2 },
            new Spiel { Id = 3, TurnierInstanzId = 1, TurnierRundeId = GRUPPENPHASE, Spielnummer = 3, GruppenNummer = 2, HeimMannschaftId = 3, AuswaertsMannschaftId = 4 },
            new Spiel { Id = 4, TurnierInstanzId = 1, TurnierRundeId = GRUPPENPHASE, Spielnummer = 4, HeimMannschaftId = 3, AuswaertsMannschaftId = 4 });
        ctx.SaveChanges();

        var rundeRepo = new TurnierRundeRepository(ctx);
        var spielRepo = new SpielRepository(ctx);
        return new TabellenZwischenspeicher(new TabellenService(rundeRepo, spielRepo, new RegelwerkRepository(ctx)), spielRepo);
    }

    [Fact]
    public async Task OhneDurchlauf_WirdJedesMalNeuGeladen()
    {
        var speicher = SpeicherMitDaten();

        var erste  = await speicher.RundenSpieleAsync(LIGA, default);
        var zweite = await speicher.RundenSpieleAsync(LIGA, default);

        zweite.Should().NotBeSameAs(erste);
    }

    [Fact]
    public async Task ImDurchlauf_WerdenSpieleUndTabellenJeRundeEinmalGeladen()
    {
        var speicher = SpeicherMitDaten();

        await speicher.DurchlaufAsync(async () =>
        {
            (await speicher.RundenSpieleAsync(LIGA, default)).Should().BeSameAs(await speicher.RundenSpieleAsync(LIGA, default));
            (await speicher.LigaTabelleAsync(LIGA, true, default)).Should().BeSameAs(await speicher.LigaTabelleAsync(LIGA, true, default));
            (await speicher.LigaTabelleAsync(LIGA, false, default)).Should().BeSameAs(await speicher.LigaTabelleAsync(LIGA, false, default));
            (await speicher.GruppenTabellenAsync(GRUPPENPHASE, default))
                .Should().BeSameAs(await speicher.GruppenTabellenAsync(GRUPPENPHASE, default));
            return 0;
        });
    }

    [Fact]
    public async Task ImDurchlauf_SichereUndAktuelleLigatabelleSindGetrennteEintraege()
    {
        var speicher = SpeicherMitDaten();

        await speicher.DurchlaufAsync(async () =>
        {
            var sicher  = await speicher.LigaTabelleAsync(LIGA, true, default);
            var aktuell = await speicher.LigaTabelleAsync(LIGA, false, default);
            aktuell.Should().NotBeSameAs(sicher);
            return 0;
        });
    }

    [Fact]
    public async Task Verwerfen_LaedtNurDieseRundeNeu()
    {
        var speicher = SpeicherMitDaten();

        await speicher.DurchlaufAsync(async () =>
        {
            var liga    = await speicher.RundenSpieleAsync(LIGA, default);
            var tabelle = await speicher.LigaTabelleAsync(LIGA, true, default);
            var gruppen = await speicher.RundenSpieleAsync(GRUPPENPHASE, default);

            speicher.Verwerfen(LIGA);

            (await speicher.RundenSpieleAsync(LIGA, default)).Should().NotBeSameAs(liga);
            (await speicher.LigaTabelleAsync(LIGA, true, default)).Should().NotBeSameAs(tabelle);
            (await speicher.RundenSpieleAsync(GRUPPENPHASE, default)).Should().BeSameAs(gruppen);
            return 0;
        });
    }

    [Fact]
    public async Task VerschachtelterDurchlauf_NutztDenAeusserenMit_UndBeendetIhnNicht()
    {
        var speicher = SpeicherMitDaten();

        await speicher.DurchlaufAsync(async () =>
        {
            var aussen = await speicher.RundenSpieleAsync(LIGA, default);
            var innen  = await speicher.DurchlaufAsync(() => speicher.RundenSpieleAsync(LIGA, default));

            innen.Should().BeSameAs(aussen);
            (await speicher.RundenSpieleAsync(LIGA, default)).Should().BeSameAs(aussen);
            return 0;
        });
    }

    [Fact]
    public async Task NachDemDurchlauf_IstNichtsMehrGemerkt_AuchNachEinemFehler()
    {
        var speicher = SpeicherMitDaten();
        IReadOnlyList<Spiel>? imDurchlauf = null;

        var durchlauf = () => speicher.DurchlaufAsync<int>(async () =>
        {
            imDurchlauf = await speicher.RundenSpieleAsync(LIGA, default);
            throw new InvalidOperationException("Abbruch");
        });
        await durchlauf.Should().ThrowAsync<InvalidOperationException>();

        var danach = await speicher.RundenSpieleAsync(LIGA, default);
        danach.Should().NotBeSameAs(imDurchlauf);
        (await speicher.RundenSpieleAsync(LIGA, default)).Should().NotBeSameAs(danach);
    }

    [Theory]
    [InlineData(null, true)]   // ohne Gruppe: irgendein Spiel der Runde
    [InlineData(1, false)]     // Spiel 3 gehört zu Gruppe 2
    [InlineData(2, true)]
    public async Task EnthaeltIgnoriertesSpiel_PrueftBeiEinerGruppeNurDerenSpiele(int? gruppenNummer, bool erwartet)
    {
        var speicher = SpeicherMitDaten();

        var ergebnis = await speicher.EnthaeltIgnoriertesSpielAsync(GRUPPENPHASE, new HashSet<int> { 3 }, default, gruppenNummer);

        ergebnis.Should().Be(erwartet);
    }

    [Fact]
    public async Task EnthaeltIgnoriertesSpiel_SpielOhneGruppennummer_ZaehltFuerJedeGruppe()
    {
        var speicher = SpeicherMitDaten();

        (await speicher.EnthaeltIgnoriertesSpielAsync(GRUPPENPHASE, new HashSet<int> { 4 }, default, gruppenNummer: 1))
            .Should().BeTrue();
    }

    [Fact]
    public async Task EnthaeltIgnoriertesSpiel_SpielEinerAnderenRundeOderNichtsIgnoriert_IstFalsch()
    {
        var speicher = SpeicherMitDaten();

        (await speicher.EnthaeltIgnoriertesSpielAsync(GRUPPENPHASE, new HashSet<int> { 1 }, default)).Should().BeFalse();
        (await speicher.EnthaeltIgnoriertesSpielAsync(GRUPPENPHASE, new HashSet<int>(), default)).Should().BeFalse();
        (await speicher.EnthaeltIgnoriertesSpielAsync(GRUPPENPHASE, null, default)).Should().BeFalse();
    }
}
