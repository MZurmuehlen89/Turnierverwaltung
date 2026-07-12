using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Stammdaten;

public class RanglisteMenue(RanglisteService ranglisteService, MannschaftService mannschaftService)
{
    private enum Option { AlleAnzeigen, Details, NeuAnlegen, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📋 Ranglisten");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.Details      => "Details anzeigen",
                        Option.NeuAnlegen   => "Neue Rangliste anlegen",
                        Option.Loeschen     => "Rangliste löschen",
                        Option.Zurueck      => "← Zurück",
                        _                   => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.Details, Option.NeuAnlegen, Option.Loeschen, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { await AktionAusfuehrenAsync(auswahl, ct); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    private async Task AktionAusfuehrenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.AlleAnzeigen: await AlleAnzeigenAsync(ct); break;
            case Option.Details:      await DetailsAnzeigenAsync(ct); break;
            case Option.NeuAnlegen:   await NeuAnlegenAsync(ct); break;
            case Option.Loeschen:     await LoeschenAsync(ct); break;
        }
    }

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Ranglisten");
        var ranglisten = await ranglisteService.AlleAbrufenAsync(ct);

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn("Datum")
            .AddColumn("Quelle")
            .AddColumn(new TableColumn("Einträge").RightAligned());

        foreach (var r in ranglisten)
            tabelle.AddRow($"[grey]{r.Id}[/]", Markup.Escape(r.Name), r.Datum.ToString("yyyy-MM-dd"),
                Markup.Escape(r.Quelle), r.Eintraege.Count.ToString());

        AnsiConsole.MarkupLine($"[grey]{ranglisten.Count} Rangliste(n)[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task DetailsAnzeigenAsync(CancellationToken ct)
    {
        var rangliste = await AuswaehlenAsync("Rangliste:", ct);
        if (rangliste is null) return;

        var details = await ranglisteService.FindenNachIdAsync(rangliste.Id, ct);
        if (details is null) return;

        KonsolenHilfsmittel.Kopf($"Rangliste: {details.Name}");
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Name:[/]",   Markup.Escape(details.Name));
        grid.AddRow("[grey]Datum:[/]",  details.Datum.ToString("yyyy-MM-dd"));
        grid.AddRow("[grey]Quelle:[/]", Markup.Escape(details.Quelle));
        AnsiConsole.Write(new Panel(grid));

        if (details.Eintraege.Any())
        {
            var eintraegeTabelle = new Table().Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("Rang").RightAligned())
                .AddColumn("Mannschaft")
                .AddColumn(new TableColumn("Punkte").RightAligned());
            foreach (var e in details.Eintraege.OrderBy(e => e.Rang))
                eintraegeTabelle.AddRow(e.Rang.ToString(), Markup.Escape(e.Mannschaft.Name), e.Punkte?.ToString() ?? "[grey]–[/]");
            AnsiConsole.Write(eintraegeTabelle);
        }

        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neue Rangliste anlegen");

        var name   = KonsolenHilfsmittel.TextEingeben("Name");
        var datum  = AnsiConsole.Prompt(new TextPrompt<DateOnly>("Datum (yyyy-MM-dd):").DefaultValue(DateOnly.FromDateTime(DateTime.Today)));
        var quelle = KonsolenHilfsmittel.TextEingeben("Quelle (z.B. \"FIFA-Weltrangliste\" oder \"Manuell\")");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Einträge (Mannschaft + Rang):[/]");
        var alleTeams = await mannschaftService.AlleAbrufenAsync(ct);
        var eintraege = new List<RanglistenEintragEingabe>();

        while (true)
        {
            var verbleibend = alleTeams.Where(t => eintraege.All(e => e.MannschaftId != t.Id)).ToList();
            if (!verbleibend.Any() || !AnsiConsole.Confirm($"Eintrag hinzufügen? ({eintraege.Count} bereits)", defaultValue: eintraege.Count == 0))
                break;

            var team = AnsiConsole.Prompt(new SelectionPrompt<Mannschaft?>()
                .Title("Mannschaft:").UseConverter(t => t is null ? "← Abbrechen" : t.Name).EnableSearch()
                .AddChoices(verbleibend.Cast<Mannschaft?>().Append(null)));
            if (team is null) break;

            var rang   = AnsiConsole.Prompt(new TextPrompt<int>("Rang:").DefaultValue(eintraege.Count + 1).Validate(v => v >= 1));
            var punkte = AnsiConsole.Confirm("Punkte angeben?", defaultValue: false)
                ? AnsiConsole.Prompt(new TextPrompt<int>("Punkte:").Validate(v => v >= 0))
                : (int?)null;

            eintraege.Add(new RanglistenEintragEingabe(team.Id, rang, punkte));
            AnsiConsole.MarkupLine($"  → {rang}. {Markup.Escape(team.Name)}");
        }

        var eingabe = new RanglisteErstellenEingabe(name, datum, quelle, eintraege);
        var neu = await ranglisteService.ErstellenAsync(eingabe, ct);

        AnsiConsole.WriteLine();
        KonsolenHilfsmittel.ErfolgAnzeigen($"Rangliste '{neu.Name}' angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var rangliste = await AuswaehlenAsync("Rangliste löschen:", ct);
        if (rangliste is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(rangliste.Name)}'[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await ranglisteService.LoeschenAsync(rangliste.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{rangliste.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task<Rangliste?> AuswaehlenAsync(string titel, CancellationToken ct)
    {
        var ranglisten = await ranglisteService.AlleAbrufenAsync(ct);
        if (ranglisten.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Ranglisten vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<Rangliste?>()
            .Title(titel).UseConverter(r => r is null ? "← Zurück" : r.Name).EnableSearch()
            .AddChoices(ranglisten.Cast<Rangliste?>().Append(null)));
    }
}
