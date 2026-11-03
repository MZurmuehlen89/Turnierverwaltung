using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

public class Rangliste : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Datum { get; set; }
    public string Quelle { get; set; } = string.Empty;

    public ICollection<RanglistenEintrag> Eintraege { get; set; } = new List<RanglistenEintrag>();

    // Ligapyramide (Issue #393): Abschnitte über Ranglistenplätze liefern Spielklasse und Liganamen. Leer bei
    // Ranglisten ohne Ligen, etwa Koeffizienten.
    public ICollection<RanglistenAbschnitt> Abschnitte { get; set; } = new List<RanglistenAbschnitt>();

    // Höchste Spielklasse, die noch als Profiliga zählt (DFB-Pokal: 2). „Heimrecht für Unterklassige“ greift dann
    // nur, wenn ein Team darunter auf ein Team bis zu dieser Klasse trifft. null = strikt Klasse gegen Klasse
    // (Copa del Rey). Eigenschaft der Pyramide eines Landes, nicht des Turniers.
    public int? ProfigrenzeSpielklasse { get; set; }

    // Nationaler Pokalsieger zum Stand der Rangliste (#583) — Grundlage des Kontingents „Pokalsieger“. Ein Verweis auf
    // die Mannschaft, nicht auf einen Eintrag: der Pokalsieger kann aus einer Spielklasse kommen, die die Rangliste
    // nicht enthält. Der Meister braucht kein Feld, er ist Platz 1.
    public int? PokalsiegerId { get; set; }
    public Mannschaft? Pokalsieger { get; set; }

    /// <summary>Der Abschnitt, in den der Platz <paramref name="rang"/> fällt — der mit dem kleinsten „bis Platz“, der
    /// den Platz noch einschließt; <c>null</c>, wenn der Platz hinter dem letzten Abschnitt liegt.</summary>
    public RanglistenAbschnitt? AbschnittFuer(int rang) =>
        Abschnitte.Where(a => a.BisPlatz >= rang).MinBy(a => a.BisPlatz);
}
