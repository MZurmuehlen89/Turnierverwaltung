using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Untertitel unter dem Teamnamen im Spielkopf (Issue #341), je nach Kontext der Begegnung. Zustandslos.</summary>
public static class SpielKopfUntertitel
{
    // Untertitel je nach Kontext der Begegnung (Nutzer-Vorgabe #341): Nationalmannschaft → Kontinental-
    // verband; Verein → Land, außer beide sind Vereine desselben Landes (dann nichtssagend → keiner;
    // dort gehört künftig die Liga hin, siehe #384).
    public static string? Fuer(Mannschaft? mannschaft, Mannschaft? gegner, Func<Mannschaft, string?> verbandKurzname)
    {
        if (mannschaft is null) return null;
        if (mannschaft.Mannschaftsart == Mannschaftsart.Nationalmannschaft)
            return verbandKurzname(mannschaft);
        bool nationalesVereinsduell = gegner is { Mannschaftsart: Mannschaftsart.Verein } && gegner.LandId == mannschaft.LandId;
        return nationalesVereinsduell ? null : mannschaft.Land?.Name;
    }
}
