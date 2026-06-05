using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class RegelwerkMenue(RegelwerkService regelwerkService)
{
    private enum Option { AlleAnzeigen, Details, NeuAnlegen, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📋 Regelwerke");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.Details      => "Details anzeigen",
                        Option.NeuAnlegen   => "Neues Regelwerk anlegen",
                        Option.Loeschen     => "Regelwerk löschen",
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
        KonsolenHilfsmittel.Kopf("Alle Regelwerke");
        var regelwerke = await regelwerkService.AlleAbrufenAsync(ct);

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn(new TableColumn("Punkte S/U/N").Centered())
            .AddColumn("Entscheidungsverfahren")
            .AddColumn("RankingSet");

        foreach (var r in regelwerke)
        {
            var punkte = r.PunkteRegeln.FirstOrDefault();
            var punkteStr = punkte != null ? $"{punkte.PunkteFuerSieg}/{punkte.PunkteFuerUnentschieden}/{punkte.PunkteFuerNiederlage}" : "[grey]–[/]";
            var verfahrenStr = string.Join(" → ", r.Entscheidungsverfahren.OrderBy(e => e.Reihenfolge).Select(e => VerfahrenKurz(e.VerfahrenArt)));
            tabelle.AddRow($"[grey]{r.Id}[/]", Markup.Escape(r.Name), punkteStr,
                string.IsNullOrEmpty(verfahrenStr) ? "[grey]–[/]" : verfahrenStr,
                r.RankingRegelSet != null ? Markup.Escape(r.RankingRegelSet.Name) : "[grey]–[/]");
        }

        AnsiConsole.MarkupLine($"[grey]{regelwerke.Count} Regelwerk(e)[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task DetailsAnzeigenAsync(CancellationToken ct)
    {
        var regelwerke = await regelwerkService.AlleAbrufenAsync(ct);
        if (regelwerke.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Regelwerke vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<Regelwerk>()
            .Title("Regelwerk:").UseConverter(r => r.Name).EnableSearch().AddChoices(regelwerke));

        var details = await regelwerkService.FindenNachIdAsync(auswahl.Id, ct);
        if (details is null) return;

        KonsolenHilfsmittel.Kopf($"Regelwerk: {details.Name}");
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Name:[/]",         Markup.Escape(details.Name));
        grid.AddRow("[grey]Beschreibung:[/]",  Markup.Escape(details.Beschreibung));
        if (details.GueltigVon.HasValue) grid.AddRow("[grey]Gültig von:[/]", details.GueltigVon.Value.Year.ToString());
        if (details.GueltigBis.HasValue) grid.AddRow("[grey]Gültig bis:[/]", details.GueltigBis.Value.Year.ToString());
        AnsiConsole.Write(new Panel(grid));

        if (details.PunkteRegeln.Any())
        {
            var p = details.PunkteRegeln.First();
            AnsiConsole.MarkupLine($"[bold]Punkte:[/] Sieg=[green]{p.PunkteFuerSieg}[/]  Unentschieden=[yellow]{p.PunkteFuerUnentschieden}[/]  Niederlage=[red]{p.PunkteFuerNiederlage}[/]");
        }

        if (details.Entscheidungsverfahren.Any())
        {
            AnsiConsole.MarkupLine("[bold]Entscheidungsverfahren (K.-o.):[/]");
            foreach (var v in details.Entscheidungsverfahren.OrderBy(e => e.Reihenfolge))
                AnsiConsole.MarkupLine($"  {v.Reihenfolge}. {v.VerfahrenArt}");
        }

        if (details.RankingRegelSet?.Eintraege.Any() == true)
        {
            AnsiConsole.MarkupLine($"[bold]Ranking-Regelset:[/] {Markup.Escape(details.RankingRegelSet.Name)}");
            foreach (var e in details.RankingRegelSet.Eintraege.OrderBy(e => e.Reihenfolge))
                AnsiConsole.MarkupLine($"  {e.Reihenfolge}. {Markup.Escape(e.RankingRegel.Name)} [grey]({e.RankingRegel.Sortierrichtung})[/]");
        }

        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neues Regelwerk anlegen");

        var name        = KonsolenHilfsmittel.TextEingeben("Name");
        var beschreibung = KonsolenHilfsmittel.TextEingeben("Beschreibung");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Punkte-System:[/]");
        var sieg    = AnsiConsole.Prompt(new TextPrompt<int>("Punkte für Sieg:").DefaultValue(3).Validate(v => v >= 0 && v <= 10));
        var unentsch = AnsiConsole.Prompt(new TextPrompt<int>("Punkte für Unentschieden:").DefaultValue(1).Validate(v => v >= 0 && v <= 10));
        var niederlage = AnsiConsole.Prompt(new TextPrompt<int>("Punkte für Niederlage:").DefaultValue(0).Validate(v => v >= 0 && v <= 10));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Entscheidungsverfahren für K.-o.-Runden (in Reihenfolge):[/]");
        var verfahrenListe = new List<VerfahrenArt>();
        var verfahrenOptionen = Enum.GetValues<VerfahrenArt>().ToList();

        while (true)
        {
            var verbleibend = verfahrenOptionen.Except(verfahrenListe).ToList();
            if (!verbleibend.Any() || !AnsiConsole.Confirm($"Verfahren hinzufügen? ({verfahrenListe.Count} bereits)", defaultValue: verfahrenListe.Count == 0))
                break;

            var verfahren = AnsiConsole.Prompt(new SelectionPrompt<VerfahrenArt>()
                .Title($"Verfahren {verfahrenListe.Count + 1}:")
                .UseConverter(v => v.ToString()).AddChoices(verbleibend));
            verfahrenListe.Add(verfahren);
            AnsiConsole.MarkupLine($"  → {string.Join(" → ", verfahrenListe)}");
        }

        // Optionales RankingRegelSet
        int? rankingSetId = null;
        var rankingSets = await regelwerkService.AlleRankingRegelSetsAbrufenAsync(ct);
        if (rankingSets.Any() && AnsiConsole.Confirm("Ranking-Regelset verknüpfen?", defaultValue: false))
        {
            var set = AnsiConsole.Prompt(new SelectionPrompt<RankingRegelSet>()
                .Title("Ranking-Regelset:").UseConverter(s => s.Name).AddChoices(rankingSets));
            rankingSetId = set.Id;
        }

        var eingabe = new RegelwerkErstellenEingabe(name, beschreibung, null, null, rankingSetId,
            sieg, unentsch, niederlage, verfahrenListe);
        var neu = await regelwerkService.ErstellenAsync(eingabe, ct);

        AnsiConsole.WriteLine();
        KonsolenHilfsmittel.ErfolgAnzeigen($"Regelwerk '{neu.Name}' angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var regelwerke = await regelwerkService.AlleAbrufenAsync(ct);
        if (regelwerke.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Regelwerke vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<Regelwerk>()
            .Title("Regelwerk löschen:").UseConverter(r => r.Name).EnableSearch().AddChoices(regelwerke));

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(auswahl.Name)}'[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await regelwerkService.LoeschenAsync(auswahl.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{auswahl.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    private static string VerfahrenKurz(VerfahrenArt v) => v switch
    {
        VerfahrenArt.VerlaengerungKlassisch => "Verl.",
        VerfahrenArt.GoldenGoal             => "GG",
        VerfahrenArt.SilverGoal             => "SG",
        VerfahrenArt.Elfmeterschiessen      => "Elf.",
        VerfahrenArt.Wiederholungsspiel     => "Wdh.",
        VerfahrenArt.Losentscheid           => "Los",
        VerfahrenArt.Muenzwurf              => "Münze",
        _                                    => v.ToString()
    };
}
