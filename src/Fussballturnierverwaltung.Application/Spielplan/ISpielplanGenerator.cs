using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spielplan;

public interface ISpielplanGenerator
{
    /// <summary>Welche Rundenart dieser Generator bedient.</summary>
    IReadOnlyList<Rundenart> UnterstützteRundenarten { get; }

    SpielplanErgebnis Generieren(SpielplanKontext kontext);
}
