using Fussballturnierverwaltung.Application.Statistiken;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Statistiken;

public class StatistikMenue(
    StatistikService statistikService,
    TurnierInstanzService instanzService)
{
    private enum Option { TorStatistik, Formtabelle, Direktvergleich, HeimAuswaerts, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📊 Statistiken");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.TorStatistik    => "Tor-Statistik",
                        Option.Formtabelle     => "Formtabelle (letzte 5 Spiele)",
                        Option.Direktvergleich => "Direktvergleich",
                        Option.HeimAuswaerts   => "Heim / Auswärts Bilanz",
                        Option.Zurueck         => "← Zurück",
                        _                      => o.ToString()
                    })
                    .AddChoices(Option.TorStatistik, Option.Formtabelle,
                                Option.Direktvergleich, Option.HeimAuswaerts, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try
            {
                switch (auswahl)
                {
                    case Option.TorStatistik:    await TorStatistikAnzeigenAsync(ct);    break;
                    case Option.Formtabelle:     await FormtabelleAnzeigenAsync(ct);     break;
                    case Option.Direktvergleich: await DirektvergleichAnzeigenAsync(ct); break;
                    case Option.HeimAuswaerts:   await HeimAuswaertsAnzeigenAsync(ct);   break;
                }
            }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }

    // ── Tor-Statistik ─────────────────────────────────────────────────────

    private async Task TorStatistikAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Tor-Statistik");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var stats = await AnsiConsole.Status()
            .StartAsync("Wird berechnet...", async _ =>
                await statistikService.TorStatistikAsync(instanz.Id, ct));

        if (stats.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine abgeschlossenen Spiele für dieses Turnier vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(instanz.Name)} ({Markup.Escape(instanz.SaisonOderJahr)})[/]\n");

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]#[/]").RightAligned())
            .AddColumn("Mannschaft")
            .AddColumn(new TableColumn("[green]Tore[/]").RightAligned())
            .AddColumn(new TableColumn("[red]Kassiert[/]").RightAligned())
            .AddColumn(new TableColumn("Diff").RightAligned());

        for (int i = 0; i < stats.Count; i++)
        {
            var e = stats[i];
            var diff = e.Tordifferenz > 0 ? $"[green]+{e.Tordifferenz}[/]"
                     : e.Tordifferenz < 0 ? $"[red]{e.Tordifferenz}[/]"
                     : "[grey]±0[/]";
            tabelle.AddRow(
                $"[grey]{i + 1}[/]",
                Markup.Escape(e.Mannschaft.Name),
                $"[green]{e.ToreErzielt}[/]",
                $"[red]{e.ToreKassiert}[/]",
                diff);
        }

        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    // ── Formtabelle ───────────────────────────────────────────────────────

    private async Task FormtabelleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Formtabelle (letzte 5 Spiele)");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var form = await AnsiConsole.Status()
            .StartAsync("Wird berechnet...", async _ =>
                await statistikService.FormtabelleAsync(instanz.Id, 5, ct));

        if (form.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine abgeschlossenen Spiele für dieses Turnier vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(instanz.Name)} ({Markup.Escape(instanz.SaisonOderJahr)})[/]\n");

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]#[/]").RightAligned())
            .AddColumn("Mannschaft")
            .AddColumn("Form")
            .AddColumn(new TableColumn("[bold]Pkt.[/]").RightAligned());

        for (int i = 0; i < form.Count; i++)
        {
            var e = form[i];
            var formStr = e.LetzteErgebnisse.Count > 0
                ? string.Concat(e.LetzteErgebnisse.Select(c => c switch
                    {
                        'S' => "[green]S[/]",
                        'U' => "[grey]U[/]",
                        _   => "[red]N[/]"
                    }))
                : "[grey]–[/]";
            tabelle.AddRow(
                $"[grey]{i + 1}[/]",
                Markup.Escape(e.Mannschaft.Name),
                formStr,
                $"[bold]{e.Punkte}[/]");
        }

        AnsiConsole.Write(tabelle);
        AnsiConsole.MarkupLine("\n[grey]S = Sieg   U = Unentschieden   N = Niederlage   (ältestes Spiel links)[/]");
        KonsolenHilfsmittel.Pause();
    }

    // ── Direktvergleich ───────────────────────────────────────────────────

    private async Task DirektvergleichAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Direktvergleich");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var torStats = await statistikService.TorStatistikAsync(instanz.Id, ct);
        if (torStats.Count < 2)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Weniger als 2 Teams haben in diesem Turnier gespielt.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        var teams = torStats.Select(e => e.Mannschaft).OrderBy(m => m.Name).ToList();

        var teamA = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft?>()
                .Title("Team [bold]A[/]:")
                .UseConverter(m => m is null ? "← Zurück" : Markup.Escape(m.Name))
                .AddChoices(teams.Cast<Mannschaft?>().Append(null)));
        if (teamA is null) return;

        var teamB = AnsiConsole.Prompt(
            new SelectionPrompt<Mannschaft?>()
                .Title("Team [bold]B[/]:")
                .UseConverter(m => m is null ? "← Zurück" : Markup.Escape(m.Name))
                .AddChoices(teams.Where(m => m.Id != teamA.Id).Cast<Mannschaft?>().Append(null)));
        if (teamB is null) return;

        var ergebnis = await statistikService.DirektvergleichAsync(instanz.Id, teamA.Id, teamB.Id, ct);

        if (ergebnis is null)
        {
            KonsolenHilfsmittel.WarnungAnzeigen(
                $"Keine Direktbegegnungen zwischen {teamA.Name} und {teamB.Name} gefunden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(
            $"[bold]{Markup.Escape(ergebnis.MannschaftA.Name)}[/]  " +
            $"[bold green]{ergebnis.SiegeA}[/] – [grey]{ergebnis.Unentschieden}[/] – [bold green]{ergebnis.SiegeB}[/]  " +
            $"[bold]{Markup.Escape(ergebnis.MannschaftB.Name)}[/]\n" +
            $"Tore:  [bold]{ergebnis.ToreA}[/]  :  [bold]{ergebnis.ToreB}[/]  " +
            $"({ergebnis.Spiele.Count} Spiel{(ergebnis.Spiele.Count == 1 ? "" : "e")})")
            .Header("[blue]Direktvergleich[/]")
            .Border(BoxBorder.Rounded));

        var spieleTabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]Nr.[/]").RightAligned())
            .AddColumn("Heim")
            .AddColumn(new TableColumn("Ergebnis").Centered())
            .AddColumn("Auswärts");

        foreach (var spiel in ergebnis.Spiele)
        {
            var heimName = spiel.HeimMannschaftId == ergebnis.MannschaftA.Id
                ? ergebnis.MannschaftA.Name : ergebnis.MannschaftB.Name;
            var auswaertsName = spiel.AuswaertsMannschaftId == ergebnis.MannschaftA.Id
                ? ergebnis.MannschaftA.Name : ergebnis.MannschaftB.Name;

            int h = spiel.Ergebnis!.HeimToreGesamt;
            int a = spiel.Ergebnis!.AuswaertsToreGesamt;
            var ergebnisMarkup = h > a ? $"[bold]{h}[/] : {a}"
                               : h < a ? $"{h} : [bold]{a}[/]"
                               : $"[grey]{h} : {a}[/]";

            spieleTabelle.AddRow(
                $"[grey]{spiel.Spielnummer}[/]",
                Markup.Escape(heimName),
                ergebnisMarkup,
                Markup.Escape(auswaertsName));
        }

        AnsiConsole.Write(spieleTabelle);
        KonsolenHilfsmittel.Pause();
    }

    // ── Heim / Auswärts ───────────────────────────────────────────────────

    private async Task HeimAuswaertsAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Heim / Auswärts Bilanz");

        var instanz = await TurnierAuswaehlenAsync(ct);
        if (instanz is null) return;

        var stats = await AnsiConsole.Status()
            .StartAsync("Wird berechnet...", async _ =>
                await statistikService.HeimAuswaertsStatistikAsync(instanz.Id, ct));

        if (stats.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine abgeschlossenen Spiele für dieses Turnier vorhanden.");
            KonsolenHilfsmittel.Pause();
            return;
        }

        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(instanz.Name)} ({Markup.Escape(instanz.SaisonOderJahr)})[/]\n");

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn("Mannschaft")
            .AddColumn(new TableColumn("[grey]Sp[/]").RightAligned())
            .AddColumn(new TableColumn("[green]S[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]U[/]").RightAligned())
            .AddColumn(new TableColumn("[red]N[/]").RightAligned())
            .AddColumn(new TableColumn("Tore").RightAligned())
            .AddColumn(new TableColumn("[bold]Quote[/]").RightAligned())
            .AddColumn(new TableColumn("[grey dim]│[/]").Centered())
            .AddColumn(new TableColumn("[grey]Sp[/]").RightAligned())
            .AddColumn(new TableColumn("[green]S[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]U[/]").RightAligned())
            .AddColumn(new TableColumn("[red]N[/]").RightAligned())
            .AddColumn(new TableColumn("Tore").RightAligned())
            .AddColumn(new TableColumn("[bold]Quote[/]").RightAligned());

        foreach (var e in stats)
        {
            tabelle.AddRow(
                Markup.Escape(e.Mannschaft.Name),
                e.HeimSpiele.ToString(),
                $"[green]{e.HeimSiege}[/]",
                $"[grey]{e.HeimUnentschieden}[/]",
                $"[red]{e.HeimNiederlagen}[/]",
                $"{e.HeimTore}:{e.HeimGegentore}",
                $"[bold]{e.HeimSiegquote:F0}%[/]",
                "[grey dim]│[/]",
                e.AuswaertsSpiele.ToString(),
                $"[green]{e.AuswaertsSiege}[/]",
                $"[grey]{e.AuswaertsUnentschieden}[/]",
                $"[red]{e.AuswaertsNiederlagen}[/]",
                $"{e.AuswaertsTore}:{e.AuswaertsGegentore}",
                $"[bold]{e.AuswaertsSiegquote:F0}%[/]");
        }

        AnsiConsole.MarkupLine("[grey]Erste 7 Spalten = Heim   Letzte 6 Spalten = Auswärts[/]");
        AnsiConsole.MarkupLine("[grey]Sp=Spiele  S=Siege  U=Unentsch.  N=Niederlagen[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────

    private async Task<TurnierInstanz?> TurnierAuswaehlenAsync(CancellationToken ct)
    {
        var instanzen = await instanzService.AlleAbrufenAsync(ct);
        if (instanzen.Count == 0)
        {
            KonsolenHilfsmittel.WarnungAnzeigen("Keine Turniere vorhanden.");
            KonsolenHilfsmittel.Pause();
            return null;
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<TurnierInstanz?>()
                .Title("Turnier:")
                .UseConverter(ti => ti is null
                    ? "← Zurück"
                    : $"{Markup.Escape(ti.Name)} ({Markup.Escape(ti.SaisonOderJahr)})")
                .EnableSearch()
                .AddChoices(instanzen.Cast<TurnierInstanz?>().Append(null)));
    }
}
