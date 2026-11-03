using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

public class LandesQuote : BasisEntitaet
{
    public int TurnierInstanzId { get; set; }
    public TurnierInstanz TurnierInstanz { get; set; } = null!;

    public int LandId { get; set; }
    public Land Land { get; set; } = null!;

    public int Anzahl { get; set; }

    // Kontingent (#572): Spielklasse und Startplatz innerhalb der Spielklasse in der Rangliste des Landes, z. B.
    // Bundesliga ab Platz 5 → Plätze 5–6. Beide null = reine Anzahl ohne Herkunft.
    public int? Spielklasse { get; set; }
    public int? AbPlatz { get; set; }

    // Kontingent „Pokalsieger“ (#583): ein Platz für den Pokalsieger aus der Rangliste des Landes, ohne Spielklasse.
    // AbPlatz ist dann „Nachrücken ab“ in der obersten Spielklasse: Plätze davor gelten als schon für einen höheren
    // Wettbewerb vergeben; ist der Pokalsieger dort oder schon im Turnier, rückt der erste freie Verein ab hier nach.
    public bool Pokalsieger { get; set; }
}
