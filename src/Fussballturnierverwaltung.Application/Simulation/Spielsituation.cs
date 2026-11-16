using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Taktische Ausrichtung eines Teams im Spielverlauf (#361), aufsteigend nach Risiko.</summary>
public enum Risikostufe
{
    /// <summary>Führendes Team verteidigt den Vorsprung – weniger Tore auf beiden Seiten.</summary>
    Mauern,
    Neutral,
    Offensiv,
    AllesNachVorn,
    /// <summary>Schlussphase: der Torhüter geht bei Standards mit nach vorn.</summary>
    Brechstange
}

/// <summary>Zustand eines Teams, der seine Torerwartung und die des Gegners verändert.</summary>
public readonly record struct Teamzustand(Risikostufe Stufe, int Platzverweise)
{
    public static Teamzustand Anpfiff(int platzverweise = 0) => new(Risikostufe.Neutral, platzverweise);
}

/// <summary>Platzverweise je Team, mit denen ein Spielabschnitt beginnt – eine Verlängerung nach dem Rückspiel
/// übernimmt die des Rückspiels (#361, K2).</summary>
public readonly record struct Platzverweise(int Heim, int Gast)
{
    public static Platzverweise AusEreignissen(IEnumerable<SimuliertesEreignis> ereignisse)
    {
        var rote = ereignisse.Where(e => e.Art == EreignisArt.RoteKarte).ToList();
        return new(rote.Count(e => e.IstHeim), rote.Count(e => !e.IstHeim));
    }
}

/// <summary>
/// Reine Berechnung der Spielsituation (#361): wie Risikostufe, Platzverweise und Ermüdung die Torerwartung einer
/// Minute verändern. Kein Zufall, kein Zustand – der minutenweise <see cref="Spielablauf"/> fragt hier nach. Die
/// Faktoren sind bewusst Konstanten; eine Grundausrichtung je Mannschaft (#667) oder die Spielbedeutung je Runde
/// (#668) setzen an dieser Stelle an.
/// </summary>
public static class Spielsituation
{
    // ── Risikostufe ───────────────────────────────────────────────────────

    // Verbleibende Minuten bis zum Ende des Abschnitts (regulär: 90). Nachspielzeit zählt als 0.
    private const int OFFENSIV_AB_REST        = 30; // ab der 60. Minute
    private const int MAUERN_AB_REST          = 20; // ab der 70. Minute
    private const int ALLES_NACH_VORN_AB_REST = 10; // ab der 80. Minute
    private const int BRECHSTANGE_AB_REST     = 2;  // ab der 88. Minute – real nur in den letzten Augenblicken

    /// <summary>Risikostufe eines Teams aus seinem Vorsprung (negativ = Rückstand) und der verbleibenden Zeit.
    /// Zurückliegend steigt sie monoton mit Rückstand und Spielzeit; führend wird ab der 70. Minute gemauert.</summary>
    public static Risikostufe RisikostufeErmitteln(int vorsprung, int verbleibendeMinuten)
    {
        if (vorsprung > 0)
            return verbleibendeMinuten <= MAUERN_AB_REST ? Risikostufe.Mauern : Risikostufe.Neutral;
        if (vorsprung == 0 || verbleibendeMinuten > OFFENSIV_AB_REST)
            return Risikostufe.Neutral;
        if (verbleibendeMinuten <= BRECHSTANGE_AB_REST)
            return Risikostufe.Brechstange;
        if (verbleibendeMinuten <= ALLES_NACH_VORN_AB_REST || vorsprung <= -2)
            return Risikostufe.AllesNachVorn;
        return Risikostufe.Offensiv;
    }

    /// <summary>Wirkung der eigenen Risikostufe auf den eigenen Angriff: mit dem Risiko nur leicht stärker.</summary>
    public static double EigenerAngriff(Risikostufe stufe) => stufe switch
    {
        Risikostufe.Mauern        => 0.75,
        Risikostufe.Offensiv      => 1.10,
        Risikostufe.AllesNachVorn => 1.20,
        Risikostufe.Brechstange   => 1.30,
        _                         => 1.0
    };

    /// <summary>Wirkung der eigenen Risikostufe auf den Angriff des Gegners: die Abwehr wird überproportional
    /// schwächer (Konter, Lucky Punch); bei der Brechstange steht das Tor leer.</summary>
    public static double AngriffDesGegners(Risikostufe stufe) => stufe switch
    {
        Risikostufe.Mauern        => 0.85,
        Risikostufe.Offensiv      => 1.30,
        Risikostufe.AllesNachVorn => 1.60,
        Risikostufe.Brechstange   => 2.00,
        _                         => 1.0
    };

    // ── Platzverweise ─────────────────────────────────────────────────────

    public const double UNTERZAHL_EIGENER_ANGRIFF = 0.7;
    public const double UNTERZAHL_ANGRIFF_GEGNER  = 1.2;

    // ── Ermüdung ──────────────────────────────────────────────────────────

    /// <summary>Ermüdung nach gespielten Minuten (inklusive Nachspielzeit): 0 beim Anpfiff, 1 nach 90 Minuten,
    /// in der Verlängerung weiter steigend. In #361 für alle Mannschaften gleich.</summary>
    public static double Ermuedung(int gespielteMinuten) => gespielteMinuten / 90.0;

    /// <summary>Wirkung der Ermüdung auf die Torerwartung: in der regulären Spielzeit überwiegen die Fehler (mehr
    /// Tore gegen Ende, im Mittel 1,0), in der Verlängerung die nachlassende Genauigkeit (leicht weniger Tore).</summary>
    public static double Torfaktor(double ermuedung) => ermuedung <= 1
        ? 0.95 + 0.1 * ermuedung
        : Math.Max(0.7, 1.05 - 0.9 * (ermuedung - 1));

    /// <summary>Wirkung der Ermüdung auf die Kartenrate: steigt mit der Fehlerquote, im Mittel der regulären
    /// Spielzeit 1,0.</summary>
    public static double Kartenfaktor(double ermuedung) => 0.5 + ermuedung;

    // ── Zusammengefasst ───────────────────────────────────────────────────

    /// <summary>Faktor auf die Grund-Torerwartung des Angreifers in einer Minute.</summary>
    public static double Angriffsfaktor(Teamzustand angreifer, Teamzustand verteidiger, double ermuedung)
        => EigenerAngriff(angreifer.Stufe)
           * AngriffDesGegners(verteidiger.Stufe)
           * Math.Pow(UNTERZAHL_EIGENER_ANGRIFF, angreifer.Platzverweise)
           * Math.Pow(UNTERZAHL_ANGRIFF_GEGNER, verteidiger.Platzverweise)
           * Torfaktor(ermuedung);

    /// <summary>Text des Ereignisses <c>Taktikwechsel</c> beim Wechsel in diese Stufe.</summary>
    public static string Taktiktext(Risikostufe stufe) => stufe switch
    {
        Risikostufe.Mauern        => "Zieht sich zurück und verteidigt die Führung",
        Risikostufe.Offensiv      => "Stellt auf Offensive um",
        Risikostufe.AllesNachVorn => "Wirft alles nach vorn",
        Risikostufe.Brechstange   => "Torwart geht mit nach vorn",
        _                         => "Kehrt zur gewohnten Ordnung zurück"
    };
}
