using Fussballturnierverwaltung.Konsole.Hilfsmittel;
using Fussballturnierverwaltung.Konsole.Menues.Bewegungsdaten;
using Fussballturnierverwaltung.Konsole.Menues.ImportExport;
using Fussballturnierverwaltung.Konsole.Menues.Stammdaten;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole.Menues;

public class HauptMenue(
    StammdatenMenue stammdatenMenue,
    BewegungsdatenMenue bewegungsdatenMenue,
    ImportExportMenue importExportMenue)
{
    private enum Option
    {
        Stammdaten,
        Bewegungsdaten,
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
                    .UseConverter(o => o switch
                    {
                        Option.Stammdaten      => "1. Stammdaten",
                        Option.Bewegungsdaten  => "2. Bewegungsdaten",
                        Option.ImportExport    => "3. Import / Export",
                        Option.Einstellungen   => "4. Einstellungen",
                        Option.Beenden         => "0. Beenden",
                        _                      => o.ToString()
                    })
                    .AddChoices(
                        Option.Stammdaten,
                        Option.Bewegungsdaten,
                        Option.ImportExport,
                        Option.Einstellungen,
                        Option.Beenden));

            if (auswahl == Option.Beenden) break;

            switch (auswahl)
            {
                case Option.Stammdaten:
                    await stammdatenMenue.AnzeigenAsync(ct);
                    break;
                case Option.Bewegungsdaten:
                    await bewegungsdatenMenue.AnzeigenAsync(ct);
                    break;
                case Option.ImportExport:
                    await importExportMenue.AnzeigenAsync(ct);
                    break;
                default:
                    KonsolenHilfsmittel.NochNichtImplementiert();
                    break;
            }
        }
    }
}
