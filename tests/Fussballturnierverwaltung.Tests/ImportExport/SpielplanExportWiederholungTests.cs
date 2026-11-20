using FluentAssertions;
using Fussballturnierverwaltung.Application.ImportExport;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Fussballturnierverwaltung.Infrastructure.Repositories;
using Fussballturnierverwaltung.Tests.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.ImportExport;

/// <summary>Der Spielplan-Export (CSV und JSON) überträgt das Wiederholungsspiel (#547, K10); ein Spiel ohne
/// Wiederholungsspiel lässt die Felder leer.</summary>
public sealed class SpielplanExportWiederholungTests : IDisposable
{
    private readonly string _csv = Path.Combine(Path.GetTempPath(), $"spielplan-{Guid.NewGuid():N}.csv");
    private readonly string _json = Path.Combine(Path.GetTempPath(), $"turnier-{Guid.NewGuid():N}.json");
    private readonly TurnierverwaltungDbContext _ctx = SimulationsDienste.NeuerContext();

    public void Dispose()
    {
        File.Delete(_csv);
        File.Delete(_json);
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

    private void FinaleMitWiederholungAnlegen()
    {
        _ctx.Verbaende.Add(new Verband { Id = 1, Name = "Verband", Kurzname = "V" });
        _ctx.Laender.Add(new Land { Id = 1, Name = "Land", Kurzname = "L", FifaCode = "LAN", VerbandId = 1 });
        _ctx.TurnierInstanzen.Add(new TurnierInstanz { Id = 1, Name = "Turnier" });
        _ctx.TurnierRunden.Add(new TurnierRunde
        {
            Id = 1, TurnierInstanzId = 1, Name = "Finale", Reihenfolge = 1, Rundenart = Rundenart.Finale
        });
        _ctx.Mannschaften.AddRange(
            new Mannschaft { Id = 1, Name = "Team 1", Kurzname = "T1", LandId = 1 },
            new Mannschaft { Id = 2, Name = "Team 2", Kurzname = "T2", LandId = 1 });
        _ctx.Spiele.Add(new Spiel
        {
            Id = 1, TurnierInstanzId = 1, TurnierRundeId = 1, Spielnummer = 1,
            HeimMannschaftId = 1, AuswaertsMannschaftId = 2, Status = SpielStatus.Abgeschlossen
        });
        _ctx.SpielErgebnisse.Add(new SpielErgebnis
        {
            SpielId = 1, HeimToreRegulaer = 1, AuswaertsToreRegulaer = 1, EntscheidungArt = EntscheidungArt.Wiederholungsspiel,
            HeimToreWiederholung = 1, AuswaertsToreWiederholung = 1,
            HeimToreWiederholungVerlaengerung = 1, AuswaertsToreWiederholungVerlaengerung = 0,
            SiegerMannschaftId = 1
        });
        _ctx.SaveChanges();
    }

    [Fact]
    public async Task CsvExport_UebertraegtDasWiederholungsspiel()
    {
        FinaleMitWiederholungAnlegen();

        await Service().SpielplanExportierenCsvAsync(1, _csv);

        var zeilen = await File.ReadAllLinesAsync(_csv);
        string[] kopf = zeilen[0].Split(';');
        string[] daten = zeilen[1].Split(';');
        string Wert(string spalte) => daten[Array.IndexOf(kopf, spalte)];
        (Wert("HeimWdh"), Wert("AuswaertsWdh"), Wert("HeimWdhNV"), Wert("AuswaertsWdhNV")).Should().Be(("1", "1", "1", "0"));
        Wert("Entscheidung").Should().Be(nameof(EntscheidungArt.Wiederholungsspiel));
    }

    [Fact]
    public async Task JsonExport_UebertraegtDasWiederholungsspiel()
    {
        FinaleMitWiederholungAnlegen();

        await Service().TurnierExportierenJsonAsync(1, _json);

        string json = await File.ReadAllTextAsync(_json);
        json.Should().Contain("\"heimToreWdh\": 1").And.Contain("\"auswaertsToreWdhNV\": 0");
    }
}
