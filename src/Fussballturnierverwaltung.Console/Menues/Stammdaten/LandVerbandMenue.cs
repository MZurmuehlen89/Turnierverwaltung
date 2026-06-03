using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Stammdaten;

public class LandVerbandMenue(LandService landService, VerbandService verbandService)
{
    private enum Option { LaenderAnzeigen, VerbaendeAnzeigen, LandDetails, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🌍 Länder und Verbände");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.LaenderAnzeigen  => "Alle Länder anzeigen",
                        Option.VerbaendeAnzeigen => "Alle Verbände anzeigen",
                        Option.LandDetails      => "Land-Details",
                        Option.Zurueck          => "← Zurück",
                        _                       => o.ToString()
                    })
                    .AddChoices(Option.LaenderAnzeigen, Option.VerbaendeAnzeigen,
                                Option.LandDetails, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            await AktionAusfuehrenAsync(auswahl, ct);
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.LaenderAnzeigen:
                await LaenderAnzeigenAsync(ct);
                break;
            case Option.VerbaendeAnzeigen:
                await VerbaendeAnzeigenAsync(ct);
                break;
            case Option.LandDetails:
                await LandDetailsAnzeigenAsync(ct);
                break;
        }
    }

    private async Task LaenderAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Länder");
        var laender = await landService.AlleAbrufenMitVerbandAsync(ct);

        // Gruppiert nach Kontinent
        foreach (var kontinent in Enum.GetValues<Kontinent>())
        {
            var gruppe = laender.Where(l => l.Kontinent == kontinent).ToList();
            if (gruppe.Count == 0) continue;

            var tabelle = new Table()
                .Border(TableBorder.Simple)
                .AddColumn(new TableColumn("[grey]Code[/]").Width(6))
                .AddColumn("Name")
                .AddColumn("Verband");

            foreach (var l in gruppe)
                tabelle.AddRow(l.FifaCode, Markup.Escape(l.Name), Markup.Escape(l.Verband?.Kurzname ?? "?"));

            AnsiConsole.Write(new Rule($"[bold]{KontinentBezeichnung(kontinent)}[/] ({gruppe.Count})").LeftJustified());
            AnsiConsole.Write(tabelle);
        }

        AnsiConsole.MarkupLine($"[grey]Gesamt: {laender.Count} Länder[/]");
        KonsolenHilfsmittel.Pause();
    }

    private async Task VerbaendeAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Verbände");
        var verbaende = await verbandService.AlleAbrufenAsync(ct);

        var tabelle = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn("Kürzel");

        foreach (var v in verbaende)
            tabelle.AddRow($"[grey]{v.Id}[/]", Markup.Escape(v.Name), Markup.Escape(v.Kurzname));

        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task LandDetailsAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Land-Details");
        var laender = await landService.AlleAbrufenMitVerbandAsync(ct);
        if (laender.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Länder vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var land = AnsiConsole.Prompt(
            new SelectionPrompt<Land>()
                .Title("Land auswählen:")
                .UseConverter(l => $"{l.Name} ({l.FifaCode})")
                .EnableSearch()
                .PageSize(15)
                .AddChoices(laender));

        AnsiConsole.WriteLine();

        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Name:[/]",      Markup.Escape(land.Name));
        grid.AddRow("[grey]Kürzel:[/]",    Markup.Escape(land.Kurzname));
        grid.AddRow("[grey]FIFA-Code:[/]", land.FifaCode);
        grid.AddRow("[grey]Kontinent:[/]", KontinentBezeichnung(land.Kontinent));
        grid.AddRow("[grey]Verband:[/]",   Markup.Escape(land.Verband?.Name ?? "?"));

        AnsiConsole.Write(new Panel(grid).Header(Markup.Escape(land.Name)));

        KonsolenHilfsmittel.Pause();
    }

    private static string KontinentBezeichnung(Kontinent k) => k switch
    {
        Kontinent.Europa              => "Europa",
        Kontinent.Suedamerika         => "Südamerika",
        Kontinent.NordUndMittelamerika => "Nord- und Mittelamerika",
        Kontinent.Afrika              => "Afrika",
        Kontinent.Asien               => "Asien",
        Kontinent.Ozeanien            => "Ozeanien",
        _                             => k.ToString()
    };
}
