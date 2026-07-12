using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Fussballturnierverwaltung.Konsole.Menues.Turniere;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues.Stammdaten;

public class StammdatenMenue(
    MannschaftMenue mannschaftMenue,
    LandVerbandMenue landVerbandMenue,
    RegelwerkMenue regelwerkMenue,
    TurnierVorlageMenue turnierVorlageMenue,
    RanglisteMenue ranglisteMenue)
{
    private enum Option
    {
        Mannschaften,
        LaenderUndVerbaende,
        Regelwerke,
        Turniervorlagen,
        Ranglisten,
        Zurueck
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            KonsolenHilfsmittel.Kopf("📋 Stammdaten");
            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .UseConverter(o => o switch
                    {
                        Option.Mannschaften        => "Mannschaften",
                        Option.LaenderUndVerbaende => "Länder und Verbände",
                        Option.Regelwerke          => "Regelwerke",
                        Option.Turniervorlagen     => "Turniervorlagen",
                        Option.Ranglisten          => "Ranglisten",
                        Option.Zurueck             => "← Zurück",
                        _                          => o.ToString()
                    })
                    .AddChoices(Option.Mannschaften, Option.LaenderUndVerbaende,
                                Option.Regelwerke, Option.Turniervorlagen, Option.Ranglisten, Option.Zurueck));

            if (auswahl == Option.Zurueck) break;

            try
            {
                switch (auswahl)
                {
                    case Option.Mannschaften:        await mannschaftMenue.AnzeigenAsync(ct);      break;
                    case Option.LaenderUndVerbaende: await landVerbandMenue.AnzeigenAsync(ct);     break;
                    case Option.Regelwerke:          await regelwerkMenue.AnzeigenAsync(ct);       break;
                    case Option.Turniervorlagen:     await turnierVorlageMenue.AnzeigenAsync(ct);  break;
                    case Option.Ranglisten:          await ranglisteMenue.AnzeigenAsync(ct);       break;
                }
            }
            catch (Exception ex) { KonsolenHilfsmittel.FehlerAnzeigen(ex.Message); KonsolenHilfsmittel.Pause(); }
        }
    }
}
