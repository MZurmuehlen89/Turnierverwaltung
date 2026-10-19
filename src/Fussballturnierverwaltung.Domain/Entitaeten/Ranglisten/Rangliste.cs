namespace Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

public class Rangliste : BasisEntitaet
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Datum { get; set; }
    public string Quelle { get; set; } = string.Empty;

    // Ranglisten-Quelle, aus der die Rangliste importiert wurde (Issue #349) — dient nur dem Aktualisieren per
    // Import; Runden verweisen direkt auf die Rangliste (#393). null = manuell gepflegt oder Quelle gelöscht.
    public int? RanglistenQuelleId { get; set; }
    public RanglistenQuelle? RanglistenQuelle { get; set; }

    public ICollection<RanglistenEintrag> Eintraege { get; set; } = new List<RanglistenEintrag>();

    // Ligapyramide (Issue #393): Abschnitte über Ranglistenplätze liefern Spielklasse und Liganamen. Leer bei
    // Ranglisten ohne Ligen, etwa importierten Koeffizienten.
    public ICollection<RanglistenAbschnitt> Abschnitte { get; set; } = new List<RanglistenAbschnitt>();

    // Höchste Spielklasse, die noch als Profiliga zählt (DFB-Pokal: 2). „Heimrecht für Unterklassige“ greift dann
    // nur, wenn ein Team darunter auf ein Team bis zu dieser Klasse trifft. null = strikt Klasse gegen Klasse
    // (Copa del Rey). Eigenschaft der Pyramide eines Landes, nicht des Turniers.
    public int? ProfigrenzeSpielklasse { get; set; }

    /// <summary>Der Abschnitt, in den der Platz <paramref name="rang"/> fällt — der mit dem kleinsten „bis Platz“, der
    /// den Platz noch einschließt; <c>null</c>, wenn der Platz hinter dem letzten Abschnitt liegt.</summary>
    public RanglistenAbschnitt? AbschnittFuer(int rang) =>
        Abschnitte.Where(a => a.BisPlatz >= rang).MinBy(a => a.BisPlatz);
}
