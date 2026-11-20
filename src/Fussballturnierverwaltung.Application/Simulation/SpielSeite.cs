using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Eine Seite eines Spiels in der Anzeige (#509): die Mannschaft – bei Platzhaltern ohne Mannschaft
/// <c>null</c> – und der Name, unter dem sie erscheint. Das Bild (Wappen, bei Nationalteams die Flagge) folgt
/// aus <see cref="MannschaftsIcon"/>.</summary>
public sealed record SpielSeite(Mannschaft? Mannschaft, string Name)
{
    public byte[]? Wappen => MannschaftsIcon.Fuer(Mannschaft);

    public static SpielSeite Von(Mannschaft mannschaft) => new(mannschaft, mannschaft.Name);
}
