using FluentAssertions;
using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Fussballturnierverwaltung.Tests.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.ImportExport;

/// <summary>CSV-Export und -Import der Mannschaften behalten die Spielweise (#667, nachgetragen): Export und Reimport
/// stellen sie wieder her, eine Datei ohne diese Spalten (ältere Exporte) lässt sie bei vorhandenen Mannschaften
/// unverändert.</summary>
public sealed class MannschaftenCsvTests : IDisposable
{
    private readonly string _datei = Path.Combine(Path.GetTempPath(), $"mannschaften-{Guid.NewGuid():N}.csv");
    private readonly TurnierverwaltungDbContext _ctx = SimulationsDienste.NeuerContext();

    public void Dispose()
    {
        File.Delete(_datei);
        _ctx.Dispose();
    }

    private ImportExportService Service()
    {
        var mannschaftRepo = new MannschaftRepository(_ctx);
        return new ImportExportService(
            mannschaftRepo, new LandRepository(_ctx), new SpielRepository(_ctx), new TurnierInstanzRepository(_ctx),
            new MannschaftService(mannschaftRepo, new MannschaftValidator(), NullLogger<MannschaftService>.Instance),
            new ImportValidator(), NullLogger<ImportExportService>.Instance);
    }

    private Mannschaft TeamAnlegen(Grundausrichtung grundausrichtung, Flexibilitaet flexibilitaet)
    {
        var verband = new Verband { Name = "Testverband", Kurzname = "TV" };
        var land = new Land { Name = "Testland", Kurzname = "TL", FifaCode = "TST", Verband = verband };
        var team = new Mannschaft
        {
            Name = "Testelf", Kurzname = "TE", Land = land,
            OffensivRating = 80, MittelfeldRating = 78, DefensivRating = 76, Heimstaerke = 75, Auswaertsstaerke = 65,
            Grundausrichtung = grundausrichtung, Flexibilitaet = flexibilitaet
        };
        _ctx.Mannschaften.Add(team);
        _ctx.SaveChanges();
        return team;
    }

    private async Task<Mannschaft> Neugeladen(int id)
    {
        _ctx.ChangeTracker.Clear();
        return await _ctx.Mannschaften.SingleAsync(m => m.Id == id);
    }

    [Fact]
    public async Task ExportUndReimport_StelltDieSpielweiseWiederHer()
    {
        var team = TeamAnlegen(Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr);
        await Service().MannschaftenExportierenCsvAsync(_datei);
        team.Grundausrichtung = Grundausrichtung.Defensiv;
        team.Flexibilitaet = Flexibilitaet.Flexibel;
        _ctx.SaveChanges();

        var ergebnis = await Service().MannschaftenImportierenAsync(_datei);

        ergebnis.Aktualisiert.Should().Be(1);
        var neu = await Neugeladen(team.Id);
        (neu.Grundausrichtung, neu.Flexibilitaet).Should().Be((Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr));
    }

    // ── Kondition (#677, K9) ──────────────────────────────────────────────

    [Fact]
    public async Task ExportUndReimport_StelltDieKonditionWiederHer_UndExportHatDieSpalte()
    {
        var team = TeamAnlegen(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal);
        team.Kondition = Kondition.Stark;
        _ctx.SaveChanges();
        await Service().MannschaftenExportierenCsvAsync(_datei);
        File.ReadAllLines(_datei)[0].Should().EndWith(";Kondition;Nervenstaerke");
        team.Kondition = Kondition.Schwach;
        _ctx.SaveChanges();

        await Service().MannschaftenImportierenAsync(_datei);

        (await Neugeladen(team.Id)).Kondition.Should().Be(Kondition.Stark);
    }

    [Fact]
    public async Task ImportOhneKonditionSpalte_LaesstBestehendeUnveraendert_NeueBekommenNormal()
    {
        var team = TeamAnlegen(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal);
        team.Kondition = Kondition.Schwach;
        _ctx.SaveChanges();
        File.WriteAllLines(_datei,
        [
            "Name;Kurzname;Mannschaftsart;LandFifaCode;OffensivRating;MittelfeldRating;DefensivRating;Heimstaerke;Auswaertsstaerke",
            "Testelf;TE;Verein;TST;81;78;76;75;65",
            "Neuelf;NE;Verein;TST;70;70;70;75;65"
        ]);

        await Service().MannschaftenImportierenAsync(_datei);

        (await Neugeladen(team.Id)).Kondition.Should().Be(Kondition.Schwach);
        _ctx.ChangeTracker.Clear();
        (await _ctx.Mannschaften.SingleAsync(m => m.Name == "Neuelf")).Kondition.Should().Be(Kondition.Normal);
    }

    // ── Nervenstärke (#669, K7) ───────────────────────────────────────────

    [Fact]
    public async Task ExportUndReimport_StelltDieNervenstaerkeWiederHer_UndExportHatDieSpalte()
    {
        var team = TeamAnlegen(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal);
        team.Nervenstaerke = Nervenstaerke.Stark;
        _ctx.SaveChanges();
        await Service().MannschaftenExportierenCsvAsync(_datei);
        File.ReadAllLines(_datei)[0].Should().EndWith(";Nervenstaerke");
        team.Nervenstaerke = Nervenstaerke.Schwach;
        _ctx.SaveChanges();

        await Service().MannschaftenImportierenAsync(_datei);

        (await Neugeladen(team.Id)).Nervenstaerke.Should().Be(Nervenstaerke.Stark);
    }

    [Fact]
    public async Task ImportOhneNervenstaerkeSpalte_LaesstBestehendeUnveraendert_NeueBekommenNormal()
    {
        var team = TeamAnlegen(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal);
        team.Nervenstaerke = Nervenstaerke.Schwach;
        _ctx.SaveChanges();
        File.WriteAllLines(_datei,
        [
            "Name;Kurzname;Mannschaftsart;LandFifaCode;OffensivRating;MittelfeldRating;DefensivRating;Heimstaerke;Auswaertsstaerke",
            "Testelf;TE;Verein;TST;81;78;76;75;65",
            "Neuelf;NE;Verein;TST;70;70;70;75;65"
        ]);

        await Service().MannschaftenImportierenAsync(_datei);

        (await Neugeladen(team.Id)).Nervenstaerke.Should().Be(Nervenstaerke.Schwach);
        _ctx.ChangeTracker.Clear();
        (await _ctx.Mannschaften.SingleAsync(m => m.Name == "Neuelf")).Nervenstaerke.Should().Be(Nervenstaerke.Normal);
    }

    [Fact]
    public async Task ImportOhneSpielweiseSpalten_LaesstSieUnveraendert()
    {
        var team = TeamAnlegen(Grundausrichtung.Defensiv, Flexibilitaet.Flexibel);
        File.WriteAllLines(_datei,
        [
            "Name;Kurzname;Mannschaftsart;LandFifaCode;OffensivRating;MittelfeldRating;DefensivRating;Heimstaerke;Auswaertsstaerke",
            "Testelf;TE;Verein;TST;81;78;76;75;65"
        ]);

        await Service().MannschaftenImportierenAsync(_datei);

        var neu = await Neugeladen(team.Id);
        neu.OffensivRating.Should().Be(81, "der Import hat die Mannschaft aktualisiert");
        (neu.Grundausrichtung, neu.Flexibilitaet).Should().Be((Grundausrichtung.Defensiv, Flexibilitaet.Flexibel));
    }

    // ── Wappen (#681) ─────────────────────────────────────────────────────

    private static readonly byte[] _wappen = [1, 2, 3, 4];

    [Fact]
    public async Task ImportAktualisiertVorhandene_LaesstIhrWappenUnveraendert()
    {
        var team = TeamAnlegen(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal);
        team.Wappen = _wappen;
        _ctx.SaveChanges();
        File.WriteAllLines(_datei,
        [
            "Name;Kurzname;Mannschaftsart;LandFifaCode;OffensivRating;MittelfeldRating;DefensivRating;Heimstaerke;Auswaertsstaerke",
            "Testelf;TE;Verein;TST;81;78;76;75;65"
        ]);

        await Service().MannschaftenImportierenAsync(_datei);

        var neu = await Neugeladen(team.Id);
        neu.OffensivRating.Should().Be(81, "der Import hat die Mannschaft aktualisiert");
        neu.Wappen.Should().Equal(_wappen);
    }

    /// <summary>Die Mannschaftspflege übergibt <c>null</c> als „Wappen entfernen“ – das darf nie zu „unverändert“ werden.</summary>
    [Fact]
    public async Task AktualisierenOhneWappen_EntferntDasWappen()
    {
        var team = TeamAnlegen(Grundausrichtung.Ausgeglichen, Flexibilitaet.Normal);
        team.Wappen = _wappen;
        _ctx.SaveChanges();
        var mannschaftRepo = new MannschaftRepository(_ctx);
        var service = new MannschaftService(mannschaftRepo, new MannschaftValidator(), NullLogger<MannschaftService>.Instance);

        await service.AktualisierenAsync(team.Id, new Application.Stammdaten.Eingaben.MannschaftAktualisierenEingabe(
            "Testelf", "TE", team.LandId, 80, 78, 76, 75, 65, Wappen: null));

        (await Neugeladen(team.Id)).Wappen.Should().BeNull();
    }
}
