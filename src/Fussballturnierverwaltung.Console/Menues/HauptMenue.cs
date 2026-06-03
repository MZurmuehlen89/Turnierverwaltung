using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Fussballturnierverwaltung.Konsole.Menues.Stammdaten;
using Fussballturnierverwaltung.Konsole.Menues.Turniere;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues;

public class HauptMenue(
    MannschaftMenue mannschaftMenue,
    LandVerbandMenue landVerbandMenue,
    TurnierInstanzMenue turnierInstanzMenue,
    TurnierVorlageMenue turnierVorlageMenue,
    RegelwerkMenue regelwerkMenue,
    SpielplanMenue spielplanMenue)
{
    private enum Option
    {
        Mannschaften,
        LaenderUndVerbaende,
        Turniere,
        Turniervorlagen,
        Regelwerke,
        Auslosungen,
        Simulationen,
        ImportExport,
        Einstellungen,
        Beenden
    }

    public async Task AnzeigenAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!Console.IsOutputRedirected)
                AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("Turnierverwaltung")
                .Centered().Color(Color.SteelBlue1));
            AnsiConsole.Write(new Rule("[grey]⚽ Fußballturnierverwaltung v1[/]").Centered());
            AnsiConsole.WriteLine();

            var auswahl = AnsiConsole.Prompt(
                new SelectionPrompt<Option>()
                    .Title("[bold]Hauptmenü[/]")
                    .UseConverter(AuswahlBezeichnung)
                    .AddChoices(
                        Option.Mannschaften,
                        Option.LaenderUndVerbaende,
                        Option.Turniere,
                        Option.Turniervorlagen,
                        Option.Regelwerke,
                        Option.Auslosungen,
                        Option.Simulationen,
                        Option.ImportExport,
                        Option.Einstellungen,
                        Option.Beenden));

            if (auswahl == Option.Beenden) break;

            await VerarbeitenAsync(auswahl, ct);
        }
    }

    private async Task VerarbeitenAsync(Option auswahl, CancellationToken ct)
    {
        switch (auswahl)
        {
            case Option.Mannschaften:
                await mannschaftMenue.AnzeigenAsync(ct);
                break;
            case Option.LaenderUndVerbaende:
                await landVerbandMenue.AnzeigenAsync(ct);
                break;
            case Option.Turniere:
                await turnierInstanzMenue.AnzeigenAsync(ct);
                break;
            case Option.Turniervorlagen:
                await turnierVorlageMenue.AnzeigenAsync(ct);
                break;
            case Option.Regelwerke:
                await regelwerkMenue.AnzeigenAsync(ct);
                break;
            case Option.Simulationen:
                await spielplanMenue.AnzeigenAsync(ct);
                break;
            default:
                KonsolenHilfsmittel.NochNichtImplementiert();
                break;
        }
    }

    private static string AuswahlBezeichnung(Option o) => o switch
    {
        Option.Mannschaften      => "1. Mannschaften",
        Option.LaenderUndVerbaende => "2. Länder und Verbände",
        Option.Turniere          => "3. Turniere",
        Option.Turniervorlagen   => "4. Turniervorlagen",
        Option.Regelwerke        => "5. Regelwerke",
        Option.Auslosungen       => "6. Auslosungen",
        Option.Simulationen      => "7. Spielplan / Simulationen",
        Option.ImportExport      => "8. Import / Export",
        Option.Einstellungen     => "9. Einstellungen",
        Option.Beenden           => "0. Beenden",
        _                        => o.ToString()
    };
}
