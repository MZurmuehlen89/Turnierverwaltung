using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Eine Gruppe im Platzraster der Vorbelegung mit der Anzahl ihrer Plätze.</summary>
public record RasterGruppe(int GruppenNr, string Name, int PlatzAnzahl);

/// <summary>Das leere Platzraster, in das bei der Vorbelegung von Hand die Teilnehmer einer Runde gesetzt werden:
/// Gruppen mit Plätzen oder Paarungen mit Heim- und Auswärtsplatz. Zustandslos.</summary>
public static class VorbelegungsRaster
{
    /// <summary>Die Gruppen einer Runde, deren Teilnehmer Gruppen zugeordnet werden. Eine Gruppenphase verteilt
    /// die Teilnehmer gleichmäßig auf ihre Gruppen (ohne bekannte Teilnehmerzahl vier Plätze je Gruppe); Liga und
    /// Schweizer System haben eine einzige Gruppe mit einem Platz je Teilnehmer.</summary>
    public static IReadOnlyList<RasterGruppe> Gruppen(Rundenart rundenart, int? gruppenAnzahl, int teamAnzahl)
    {
        if (rundenart != Rundenart.Gruppenphase)
            return [new RasterGruppe(1, "Teilnehmer", Math.Max(teamAnzahl, 1))];

        int anzahl = gruppenAnzahl ?? 1;
        int plaetzeJeGruppe = teamAnzahl > 0 && anzahl > 0 ? teamAnzahl / anzahl : 4;
        return Enumerable.Range(1, anzahl)
            .Select(nr => new RasterGruppe(nr, $"Gruppe {GruppenHerkunft.Buchstabe(nr)}", plaetzeJeGruppe))
            .ToList();
    }

    /// <summary>Anzahl der Paarungen einer K.-o.-Runde — bei ungerader Teilnehmerzahl bleibt in der letzten der
    /// Auswärtsplatz frei (Freilos), ohne bekannte Teilnehmerzahl eine Paarung.</summary>
    public static int PaarungsAnzahl(int teamAnzahl) =>
        teamAnzahl > 0 ? (int)Math.Ceiling(teamAnzahl / 2.0) : 1;
}
