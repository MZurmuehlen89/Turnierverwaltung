using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Fussballturnierverwaltung.Konsole.Menues.Auslosungen;
using Fussballturnierverwaltung.Konsole.Menues.Statistiken;
using Fussballturnierverwaltung.Konsole.Menues.Turniere;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Bewegungsdaten;

public class BewegungsdatenMenue(
    TurnierInstanzMenue turnierInstanzMenue,
    UebergangsregelMenue uebergangsregelMenue,
    VerbandsQuoteMenue verbandsQuoteMenue,
    AuslosungMenue auslosungMenue,
    SpielplanMenue spielplanMenue,
    StatistikMenue statistikMenue)
{
    private enum Option
    {
        Turniere,
        Uebergangsregeln,
        VerbandsQuoten,
        Auslosungen,
        SpielplanSimulation,
        Statistiken,
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
                        Option.Uebergangsregeln    => "Übergangsregeln",
                        Option.VerbandsQuoten      => "Verband-Quoten",
                        Option.Auslosungen         => "Auslosungen",
                        Option.SpielplanSimulation => "Spielplan / Simulation",
                        Option.Statistiken         => "Statistiken",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.Turniere, Option.Uebergangsregeln, Option.VerbandsQuoten, Option.Auslosungen,
                                Option.SpielplanSimulation, Option.Statistiken, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try
            {
                switch (auswahl)
                {
                    case Option.Turniere:            await turnierInstanzMenue.AnzeigenAsync(ct);     break;
                    case Option.Uebergangsregeln:     await uebergangsregelMenue.AnzeigenAsync(ct);    break;
                    case Option.VerbandsQuoten:      await verbandsQuoteMenue.AnzeigenAsync(ct);       break;
                    case Option.Auslosungen:         await auslosungMenue.AnzeigenAsync(ct);           break;
                    case Option.SpielplanSimulation: await spielplanMenue.AnzeigenAsync(ct);           break;
                    case Option.Statistiken:         await statistikMenue.AnzeigenAsync(ct);           break;
                }
            }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }
}
