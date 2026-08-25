using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Stammdaten;

public class RanglistenQuelleMenue(RanglistenQuelleService quelleService)
{
    private enum Option { AlleAnzeigen, NeuAnlegen, Bearbeiten, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🌐 Ranglisten-Quellen");

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.NeuAnlegen   => "Neue Quelle anlegen",
                        Option.Bearbeiten   => "Quelle bearbeiten",
                        Option.Loeschen     => "Quelle löschen",
                        Option.Zurueck      => "← Zurück",
                        _                   => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.NeuAnlegen, Option.Bearbeiten, Option.Loeschen, Option.Zurueck));

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
            case Option.NeuAnlegen:   await NeuAnlegenAsync(ct);   break;
            case Option.Bearbeiten:   await BearbeitenAsync(ct);  break;
            case Option.Loeschen:     await LoeschenAsync(ct);    break;
        }
    }

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Ranglisten-Quellen");
        var quellen = await quelleService.AlleAbrufenAsync(ct);

        var tabelle = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
            .AddColumn("Name")
            .AddColumn("URL")
            .AddColumn(new TableColumn("Tabelle").Centered())
            .AddColumn(new TableColumn("Mannschaft-Spalte").Centered())
            .AddColumn(new TableColumn("Punkte-Spalte").Centered());

        foreach (var q in quellen)
            tabelle.AddRow($"[grey]{q.Id}[/]", Markup.Escape(q.Name), Markup.Escape(q.Url),
                Markup.Escape(q.TabellenSelektor ?? q.TabellenIndex.ToString()),
                q.MannschaftSpalte.ToString(), q.PunkteSpalte?.ToString() ?? "[grey]–[/]");

        AnsiConsole.MarkupLine($"[grey]{quellen.Count} Quelle(n)[/]");
        AnsiConsole.Write(tabelle);
        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neue Ranglisten-Quelle anlegen");
        var eingabe = EingabeAbfragen();
        var neu = await quelleService.ErstellenAsync(eingabe, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Quelle '{neu.Name}' angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task BearbeitenAsync(CancellationToken ct)
    {
        var quelle = await AuswaehlenAsync("Quelle bearbeiten:", ct);
        if (quelle is null) return;

        KonsolenHilfsmittel.Kopf($"Quelle bearbeiten: {quelle.Name}");
        var eingabe = EingabeAbfragen(quelle);
        await quelleService.AktualisierenAsync(quelle.Id, new RanglistenQuelleAktualisierenEingabe(
            eingabe.Name, eingabe.Url, eingabe.TabellenIndex, eingabe.TabellenSelektor,
            eingabe.MannschaftSpalte, eingabe.PunkteSpalte, eingabe.KopfzeileUeberspringen), ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Quelle '{eingabe.Name}' aktualisiert.");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var quelle = await AuswaehlenAsync("Quelle löschen:", ct);
        if (quelle is null) return;

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(quelle.Name)}'[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await quelleService.LoeschenAsync(quelle.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{quelle.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
    }

    public async Task<RanglistenQuelle?> AuswaehlenAsync(string titel, CancellationToken ct)
    {
        var quellen = await quelleService.AlleAbrufenAsync(ct);
        if (quellen.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Ranglisten-Quellen vorhanden."); KonsolenHilfsmittel.Pause(); return null; }

        return AnsiConsole.Prompt(new SelectionPrompt<RanglistenQuelle?>()
            .Title(titel).UseConverter(q => q is null ? "← Zurück" : q.Name).EnableSearch()
            .AddChoices(quellen.Cast<RanglistenQuelle?>().Append(null)));
    }

    private static RanglistenQuelleErstellenEingabe EingabeAbfragen(RanglistenQuelle? vorhanden = null)
    {
        var name = KonsolenHilfsmittel.TextEingeben("Name", vorhanden?.Name ?? "");
        var url  = KonsolenHilfsmittel.TextEingeben("URL", vorhanden?.Url ?? "");

        var tabellenSelektor = AnsiConsole.Prompt(
            new TextPrompt<string>("CSS-Selektor der Tabelle (leer = per Index wählen):")
                .AllowEmpty()
                .DefaultValue(vorhanden?.TabellenSelektor ?? ""));

        var tabellenIndex = string.IsNullOrWhiteSpace(tabellenSelektor)
            ? AnsiConsole.Prompt(new TextPrompt<int>("Tabellenindex (0-basiert):").DefaultValue(vorhanden?.TabellenIndex ?? 0).Validate(v => v >= 0))
            : vorhanden?.TabellenIndex ?? 0;

        var mannschaftSpalte = AnsiConsole.Prompt(new TextPrompt<int>("Spalte mit Team-/Landname (0-basiert):")
            .DefaultValue(vorhanden?.MannschaftSpalte ?? 1).Validate(v => v >= 0));

        var punkteAngeben = AnsiConsole.Confirm("Punkte-Spalte vorhanden?", defaultValue: vorhanden?.PunkteSpalte is not null);
        var punkteSpalte = punkteAngeben
            ? AnsiConsole.Prompt(new TextPrompt<int>("Spalte mit Punkten (0-basiert):")
                .DefaultValue(vorhanden?.PunkteSpalte ?? 2).Validate(v => v >= 0 && v != mannschaftSpalte))
            : (int?)null;

        var kopfzeileUeberspringen = AnsiConsole.Confirm("Erste Zeile ist Kopfzeile (überspringen)?",
            defaultValue: vorhanden?.KopfzeileUeberspringen ?? true);

        return new RanglistenQuelleErstellenEingabe(
            name, url, tabellenIndex,
            string.IsNullOrWhiteSpace(tabellenSelektor) ? null : tabellenSelektor,
            mannschaftSpalte, punkteSpalte, kopfzeileUeberspringen);
    }
}
