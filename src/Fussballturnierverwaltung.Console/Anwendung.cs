using Fussballturnierverwaltung.Konsole.Menues;
using Spectre.Console;

namespace Fussballturnierverwaltung.Konsole;

public class Anwendung(HauptMenue hauptMenue)
{
    public async Task StartenAsync(CancellationToken ct = default)
    {
        await hauptMenue.AnzeigenAsync(ct);
        AnsiConsole.MarkupLine("[grey]Auf Wiedersehen![/]");
    }
}
