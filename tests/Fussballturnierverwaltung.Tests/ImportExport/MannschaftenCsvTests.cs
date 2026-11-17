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
}
