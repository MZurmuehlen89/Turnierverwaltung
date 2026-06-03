using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Turniere;

public class TurnierVorlageMenue(TurnierVorlageService vorlageService)
{
    private enum Option { AlleAnzeigen, NeuAnlegen, Loeschen, Zurueck }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("🗂 Turniervorlagen");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.AlleAnzeigen => "Alle anzeigen",
                        Option.NeuAnlegen   => "Neue Vorlage anlegen",
                        Option.Loeschen     => "Vorlage löschen",
                        Option.Zurueck      => "← Zurück",
                        _                   => o.ToString()
                    })
                    .AddChoices(Option.AlleAnzeigen, Option.NeuAnlegen, Option.Loeschen, Option.Zurueck));

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
            case Option.NeuAnlegen:   await NeuAnlegenAsync(ct); break;
            case Option.Loeschen:     await LoeschenAsync(ct); break;
        }
    }

    private async Task AlleAnzeigenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Alle Turniervorlagen");
        var vorlagen = await vorlageService.AlleAbrufenAsync(ct);

        foreach (var kategorie in Enum.GetValues<VorlagenKategorie>())
        {
            var gruppe = vorlagen.Where(v => v.VorlagenKategorie == kategorie).ToList();
            if (gruppe.Count == 0) continue;

            AnsiConsole.Write(new Rule($"[bold]{kategorie}[/]").LeftJustified());
            var tabelle = new Table().Border(TableBorder.Simple)
                .AddColumn(new TableColumn("[grey]ID[/]").RightAligned())
                .AddColumn("Name")
                .AddColumn(new TableColumn("Teams").RightAligned())
                .AddColumn(new TableColumn("Historisch").Centered())
                .AddColumn("Gültigkeit");

            foreach (var v in gruppe)
            {
                var gueltigkeit = (v.GueltigVon?.Year.ToString() ?? "–") + " – " + (v.GueltigBis?.Year.ToString() ?? "heute");
                tabelle.AddRow($"[grey]{v.Id}[/]", Markup.Escape(v.Name),
                    v.ErforderlicheMannschaftsanzahl == 0 ? "[grey]frei[/]" : v.ErforderlicheMannschaftsanzahl.ToString(),
                    v.IstHistorisch ? "[grey]ja[/]" : "[green]nein[/]",
                    gueltigkeit);
            }
            AnsiConsole.Write(tabelle);
        }

        AnsiConsole.MarkupLine($"[grey]Gesamt: {vorlagen.Count} Vorlage(n)[/]");
        KonsolenHilfsmittel.Pause();
    }

    private async Task NeuAnlegenAsync(CancellationToken ct)
    {
        KonsolenHilfsmittel.Kopf("Neue Turniervorlage anlegen");

        var name         = KonsolenHilfsmittel.TextEingeben("Name");
        var beschreibung = KonsolenHilfsmittel.TextEingeben("Beschreibung");
        var anzahl       = AnsiConsole.Prompt(new TextPrompt<int>("Erforderliche Mannschaftsanzahl (0 = frei):").DefaultValue(16).Validate(n => n >= 0));
        var kategorie    = AnsiConsole.Prompt(new SelectionPrompt<VorlagenKategorie>().Title("Kategorie:").AddChoices(Enum.GetValues<VorlagenKategorie>()));
        var historisch   = AnsiConsole.Confirm("Historische Vorlage?", defaultValue: false);

        DateOnly? gueltigVon = null, gueltigBis = null;
        if (AnsiConsole.Confirm("Gültigkeitszeitraum angeben?", defaultValue: false))
        {
            var von = AnsiConsole.Prompt(new TextPrompt<int>("Gültig ab (Jahr):").Validate(y => y > 1860 && y <= 2100));
            gueltigVon = new DateOnly(von, 1, 1);
            if (AnsiConsole.Confirm("Gültig bis (Endjahr) angeben?", defaultValue: false))
            {
                var bis = AnsiConsole.Prompt(new TextPrompt<int>("Gültig bis (Jahr):").Validate(y => y >= von && y <= 2100));
                gueltigBis = new DateOnly(bis, 12, 31);
            }
        }

        var eingabe = new TurnierVorlageErstellenEingabe(name, beschreibung, anzahl, kategorie, historisch, gueltigVon, gueltigBis);
        var neu = await vorlageService.ErstellenAsync(eingabe, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Vorlage '{neu.Name}' angelegt (ID {neu.Id}).");
        KonsolenHilfsmittel.Pause();
    }

    private async Task LoeschenAsync(CancellationToken ct)
    {
        var vorlagen = await vorlageService.AlleAbrufenAsync(ct);
        if (vorlagen.Count == 0) { KonsolenHilfsmittel.WarnungAnzeigen("Keine Vorlagen vorhanden."); KonsolenHilfsmittel.Pause(); return; }

        var auswahl = AnsiConsole.Prompt(new SelectionPrompt<TurnierVorlage>()
            .Title("Vorlage löschen:").UseConverter(v => v.Name).EnableSearch().AddChoices(vorlagen));

        if (!KonsolenHilfsmittel.Bestaetigen($"[red]'{Markup.Escape(auswahl.Name)}'[/] wirklich löschen?"))
        { KonsolenHilfsmittel.WarnungAnzeigen("Abgebrochen."); KonsolenHilfsmittel.Pause(); return; }

        await vorlageService.LoeschenAsync(auswahl.Id, ct);
        KonsolenHilfsmittel.ErfolgAnzeigen($"'{auswahl.Name}' gelöscht.");
        KonsolenHilfsmittel.Pause();
    }
}
