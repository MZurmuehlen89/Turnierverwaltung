using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Fussballturnierverwaltung.Konsole.Menues.Auslosungen;
using Fussballturnierverwaltung.Konsole.Menues.Turniere;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Bewegungsdaten;

public class BewegungsdatenMenue(
    TurnierInstanzMenue turnierInstanzMenue,
    AuslosungMenue auslosungMenue,
    SpielplanMenue spielplanMenue)
{
    private enum Option
    {
        Turniere,
        Auslosungen,
        SpielplanSimulation,
        Zurueck
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("⚽ Bewegungsdaten");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.Turniere            => "Turniere",
                        Option.Auslosungen         => "Auslosungen",
                        Option.SpielplanSimulation => "Spielplan / Simulation",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.Turniere, Option.Auslosungen,
                                Option.SpielplanSimulation, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try
            {
                switch (auswahl)
                {
                    case Option.Turniere:            await turnierInstanzMenue.AnzeigenAsync(ct); break;
                    case Option.Auslosungen:         await auslosungMenue.AnzeigenAsync(ct);      break;
                    case Option.SpielplanSimulation: await spielplanMenue.AnzeigenAsync(ct);      break;
                }
            }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }
}
