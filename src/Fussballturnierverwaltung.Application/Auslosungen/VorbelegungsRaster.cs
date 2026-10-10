using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Eine Gruppe im Platzraster der Vorbelegung mit der Anzahl ihrer Plätze.</summary>
public record RasterGruppe(int GruppenNr, string Name, int PlatzAnzahl);

/// <summary>Das leere Platzraster, in das bei der Vorbelegung von Hand die Teilnehmer einer Runde gesetzt werden:
/// Gruppen mit Plätzen oder Paarungen mit Heim- und Auswärtsplatz. Zustandslos.</summary>
public static class VorbelegungsRaster
{
    /// <summary>Ob die Teilnehmer der Runde Gruppen zugeordnet werden (ein Team je Platz) statt paarweise
    /// gegeneinander gesetzt — Liga und Schweizer System teilen die flache Teilnehmerliste der Gruppenphase.</summary>
    public static bool HatGruppen(Rundenart rundenart) =>
        rundenart is Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem;

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

    /// <summary>Ob in einer Runde mit Gruppen nichts mehr fehlt: jeder Platz ist besetzt.</summary>
    public static bool GruppenVollstaendig(IReadOnlyCollection<bool> platzBesetzt) =>
        platzBesetzt.Count > 0 && platzBesetzt.All(besetzt => besetzt);

    /// <summary>Ob in einer Runde mit Paarungen nichts mehr fehlt: alle erwarteten Teams sind platziert (ohne
    /// bekannte Teilnehmerzahl mindestens eines) und keine Paarung hat nur ein Auswärtsteam — ein einzelnes Team
    /// (Freilos bei ungerader Anzahl) steht auf dem Heimplatz.</summary>
    public static bool PaarungenVollstaendig(
        IReadOnlyCollection<(bool HeimBesetzt, bool AuswaertsBesetzt)> paarungen, int teamsBenoetigt)
    {
        int zugewiesen = paarungen.Sum(p => (p.HeimBesetzt ? 1 : 0) + (p.AuswaertsBesetzt ? 1 : 0));
        return zugewiesen > 0
            && (teamsBenoetigt == 0 || zugewiesen == teamsBenoetigt)
            && paarungen.All(p => p.HeimBesetzt || !p.AuswaertsBesetzt);
    }
}
