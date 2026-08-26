using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Ranglisten.Eingaben;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
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
        var eingabe = await EingabeAbfragenAsync(ct: ct);
        if (eingabe is null) return;

        var neu = await quelleService.ErstellenAsync(eingabe, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Quelle '{neu.Name}' angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task BearbeitenAsync(CancellationToken ct)
    {
        var quelle = await AuswaehlenAsync("Quelle bearbeiten:", ct);
        if (quelle is null) return;

        KonsolenHilfsmittel.Kopf($"Quelle bearbeiten: {quelle.Name}");
        var eingabe = await EingabeAbfragenAsync(quelle, ct);
        if (eingabe is null) return;

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

    /// <summary>Auswahloption einer Spalten-Auswahl (Issue #225) — Index null steht für
    /// "— keine —" (nur bei der optionalen Punkte-Spalte verwendet).</summary>
    private record SpaltenOption(int? Index, string Beschreibung);

    private async Task<RanglistenQuelleErstellenEingabe?> EingabeAbfragenAsync(RanglistenQuelle? vorhanden = null, CancellationToken ct = default)
    {
        var name = KonsolenHilfsmittel.TextEingeben("Name", vorhanden?.Name ?? "");

        IReadOnlyList<RanglistenTabellenVorschau> tabellen;
        string url;
        while (true)
        {
            url = KonsolenHilfsmittel.TextEingeben("URL", vorhanden?.Url ?? "");
            try
            {
                tabellen = await AnsiConsole.Status().StartAsync("Webseite wird geprüft…",
                    _ => quelleService.WebseitePruefenAsync(url, ct));

                if (tabellen.Count == 0)
                {
                    KonsolenHilfsmittel.FehlerAnzeigen("Auf der Seite wurden keine Tabellen gefunden.");
                    if (!AnsiConsole.Confirm("Andere URL versuchen?")) return null;
                    continue;
                }

                break;
            }
            catch (Exception ex)
            {
                KonsolenHilfsmittel.FehlerAnzeigen($"Seite konnte nicht geladen werden: {ex.Message}");
                if (!AnsiConsole.Confirm("Erneut versuchen?")) return null;
            }
        }

        var gewaehlteTabelle = tabellen.Count == 1
            ? tabellen[0]
            : AnsiConsole.Prompt(new SelectionPrompt<RanglistenTabellenVorschau>()
                .Title("Welche Tabelle enthält die Rangliste?")
                .UseConverter(t => $"Tabelle {t.Index + 1}: {t.ZeilenAnzahl} Zeilen × {t.SpaltenAnzahl} Spalten"
                    + (t.VorschauZeilen.Count > 0 ? $" — {string.Join(" | ", t.VorschauZeilen[0])}" : ""))
                .AddChoices(tabellen));

        VorschauAnzeigen(gewaehlteTabelle);

        var spaltenOptionen = SpaltenOptionenErmitteln(gewaehlteTabelle);

        var mannschaftSpalte = AnsiConsole.Prompt(new SelectionPrompt<SpaltenOption>()
            .Title("Welche Spalte enthält den Team-/Landnamen?")
            .UseConverter(s => s.Beschreibung)
            .AddChoices(spaltenOptionen)).Index!.Value;

        var punkteOptionen = new[] { new SpaltenOption(null, "— keine —") }
            .Concat(spaltenOptionen.Where(s => s.Index != mannschaftSpalte));
        var punkteSpalte = AnsiConsole.Prompt(new SelectionPrompt<SpaltenOption>()
            .Title("Welche Spalte enthält die Punkte? (optional)")
            .UseConverter(s => s.Beschreibung)
            .AddChoices(punkteOptionen)).Index;

        var kopfzeileUeberspringen = AnsiConsole.Confirm("Erste Zeile ist Kopfzeile (überspringen)?",
            defaultValue: gewaehlteTabelle.ErkannteKopfzeile);

        return new RanglistenQuelleErstellenEingabe(
            name, url, gewaehlteTabelle.Index, null, mannschaftSpalte, punkteSpalte, kopfzeileUeberspringen);
    }

    private static void VorschauAnzeigen(RanglistenTabellenVorschau vorschau)
    {
        if (vorschau.VorschauZeilen.Count == 0) return;

        var tabelle = new Table().Border(TableBorder.Rounded);
        for (int i = 0; i < vorschau.SpaltenAnzahl; i++)
            tabelle.AddColumn(vorschau.ErkannteKopfzeile && i < vorschau.VorschauZeilen[0].Count
                ? Markup.Escape(vorschau.VorschauZeilen[0][i])
                : $"Spalte {i + 1}");

        var datenZeilen = vorschau.ErkannteKopfzeile ? vorschau.VorschauZeilen.Skip(1) : vorschau.VorschauZeilen;
        foreach (var zeile in datenZeilen)
            tabelle.AddRow([.. Enumerable.Range(0, vorschau.SpaltenAnzahl)
                .Select(i => Markup.Escape(i < zeile.Count ? zeile[i] : ""))]);

        AnsiConsole.Write(tabelle);
    }

    private static List<SpaltenOption> SpaltenOptionenErmitteln(RanglistenTabellenVorschau vorschau)
    {
        var datenZeilen = (vorschau.ErkannteKopfzeile ? vorschau.VorschauZeilen.Skip(1) : vorschau.VorschauZeilen).ToList();
        var optionen = new List<SpaltenOption>();

        for (int spalte = 0; spalte < vorschau.SpaltenAnzahl; spalte++)
        {
            var beispiele = datenZeilen
                .Select(zeile => spalte < zeile.Count ? zeile[spalte] : "")
                .Where(wert => !string.IsNullOrWhiteSpace(wert))
                .Take(3)
                .ToList();

            var kopf = vorschau.ErkannteKopfzeile && spalte < vorschau.VorschauZeilen[0].Count
                ? vorschau.VorschauZeilen[0][spalte]
                : $"Spalte {spalte + 1}";

            optionen.Add(new SpaltenOption(spalte, beispiele.Count > 0 ? $"{kopf} ({string.Join(", ", beispiele)})" : kopf));
        }

        return optionen;
    }
}
