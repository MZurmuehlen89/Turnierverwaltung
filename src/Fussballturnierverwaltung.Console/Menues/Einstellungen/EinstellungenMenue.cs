using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Einstellungen;

public class EinstellungenMenue(EinstellungenService einstellungenService)
{
    private enum Option
    {
        ExportPfadAendern,
        SimulationsModusAendern,
        SimulationsSeedFestlegen,
        ZurueckSetzen,
        Zurueck
    }

    public Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var einstellungen = einstellungenService.Laden();

            KonsolenHilfsmittel.Kopf("⚙ Einstellungen");
            ZeigeTabelleAn(einstellungen);
            AnsiConsole.WriteLine();

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.ExportPfadAendern        => "Export-Pfad festlegen",
                        Option.SimulationsModusAendern  => "Standard-Simulationsmodus ändern",
                        Option.SimulationsSeedFestlegen => "Standard-Simulationsseed festlegen",
                        Option.ZurueckSetzen            => "Einstellungen zurücksetzen",
                        Option.Zurueck                  => "← Zurück",
                        _                               => o.ToString()
                    })
                    .AddChoices(
                        Option.ExportPfadAendern,
                        Option.SimulationsModusAendern,
                        Option.SimulationsSeedFestlegen,
                        Option.ZurueckSetzen,
                        Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try { AktionAusfuehren(auswahl, einstellungen); }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }

        return Task.CompletedTask;
    }

    private void AktionAusfuehren(Option auswahl, BenutzerEinstellungen einstellungen)
    {
        switch (auswahl)
        {
            case Option.ExportPfadAendern:        ExportPfadAendern(einstellungen);        break;
            case Option.SimulationsModusAendern:  SimulationsModusAendern(einstellungen);  break;
            case Option.SimulationsSeedFestlegen: SimulationsSeedFestlegen(einstellungen); break;
            case Option.ZurueckSetzen:            ZurueckSetzen();                         break;
        }
    }

    // ── Aktionen ──────────────────────────────────────────────────────────

    private void ExportPfadAendern(BenutzerEinstellungen einstellungen)
    {
        KonsolenHilfsmittel.Kopf("Export-Pfad festlegen");

        var neuerPfad = AnsiConsole.Prompt(
            new TextPrompt<string>("Neuer Export-Pfad:")
                .DefaultValue(einstellungen.ExportPfad)
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(neuerPfad))
            neuerPfad = einstellungen.ExportPfad;

        einstellungen.ExportPfad = neuerPfad;
        einstellungenService.Speichern(einstellungen);
        KonsolenHilfsmittel.ErfolgAnzeigen($"Export-Pfad gesetzt: {Markup.Escape(neuerPfad)}");
        KonsolenHilfsmittel.Pause();
    }

    private void SimulationsModusAendern(BenutzerEinstellungen einstellungen)
    {
        KonsolenHilfsmittel.Kopf("Standard-Simulationsmodus ändern");

        var modus = AnsiConsole.Prompt(
            new SelectionPrompt<SimulationsModus>()
                .Title("Simulationsmodus:")
                .UseConverter(m => m switch
                {
                    SimulationsModus.SchnellSimulation     => "Schnellsimulation (nur Endergebnis)",
                    SimulationsModus.AusfuehrlicheSimulation => "Ausführliche Simulation (mit Spielereignissen)",
                    SimulationsModus.ManuellesErgebnis     => "Manuelles Ergebnis (kein Simulator)",
                    _                                      => m.ToString()
                })
                .AddChoices(
                    SimulationsModus.SchnellSimulation,
                    SimulationsModus.AusfuehrlicheSimulation,
                    SimulationsModus.ManuellesErgebnis));

        einstellungen.StandardSimulationsModus = modus;
        einstellungenService.Speichern(einstellungen);
        KonsolenHilfsmittel.ErfolgAnzeigen("Simulationsmodus gespeichert.");
        KonsolenHilfsmittel.Pause();
    }

    private void SimulationsSeedFestlegen(BenutzerEinstellungen einstellungen)
    {
        KonsolenHilfsmittel.Kopf("Standard-Simulationsseed festlegen");
        AnsiConsole.MarkupLine("[grey]Leer lassen = zufälliger Seed bei jeder Simulation.[/]");
        AnsiConsole.WriteLine();

        var eingabe = AnsiConsole.Prompt(
            new TextPrompt<string>("Seed (ganze Zahl oder leer):")
                .DefaultValue(einstellungen.StandardSimulationsSeed?.ToString() ?? "")
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(eingabe))
        {
            einstellungen.StandardSimulationsSeed = null;
            einstellungenService.Speichern(einstellungen);
            KonsolenHilfsmittel.ErfolgAnzeigen("Seed entfernt — jede Simulation wird zufällig.");
        }
        else if (int.TryParse(eingabe, out int seed))
        {
            einstellungen.StandardSimulationsSeed = seed;
            einstellungenService.Speichern(einstellungen);
            KonsolenHilfsmittel.ErfolgAnzeigen($"Seed gesetzt: {seed}");
        }
        else
        {
            KonsolenHilfsmittel.FehlerAnzeigen($"'{eingabe}' ist keine gültige ganze Zahl.");
        }

        KonsolenHilfsmittel.Pause();
    }

    private void ZurueckSetzen()
    {
        KonsolenHilfsmittel.Kopf("Einstellungen zurücksetzen");

        if (!KonsolenHilfsmittel.Bestaetigen("Alle Einstellungen auf Standardwerte zurücksetzen?"))
            return;

        einstellungenService.Speichern(new BenutzerEinstellungen());
        KonsolenHilfsmittel.ErfolgAnzeigen("Einstellungen wurden zurückgesetzt.");
        KonsolenHilfsmittel.Pause();
    }

    // ── Anzeige ──────────────────────────────────────────────────────────

    private static void ZeigeTabelleAn(BenutzerEinstellungen einstellungen)
    {
        var tabelle = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Einstellung[/]")
            .AddColumn("[bold]Wert[/]");

        tabelle.AddRow("Export-Pfad", Markup.Escape(einstellungen.ExportPfad));

        var modusText = einstellungen.StandardSimulationsModus switch
        {
            SimulationsModus.SchnellSimulation      => "Schnellsimulation",
            SimulationsModus.AusfuehrlicheSimulation => "Ausführliche Simulation",
            SimulationsModus.ManuellesErgebnis       => "Manuelles Ergebnis",
            _                                        => einstellungen.StandardSimulationsModus.ToString()
        };
        tabelle.AddRow("Standard-Simulationsmodus", modusText);

        var seedText = einstellungen.StandardSimulationsSeed.HasValue
            ? einstellungen.StandardSimulationsSeed.Value.ToString()
            : "[grey]zufällig[/]";
        tabelle.AddRow("Standard-Simulationsseed", seedText);

        AnsiConsole.Write(tabelle);
    }
}
