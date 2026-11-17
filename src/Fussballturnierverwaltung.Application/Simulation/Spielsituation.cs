using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Taktische Ausrichtung eines Teams im Spielverlauf (#361), aufsteigend nach Risiko.</summary>
public enum Risikostufe
{
    /// <summary>Führendes Team verteidigt den Vorsprung – weniger Tore auf beiden Seiten.</summary>
    Mauern,
    /// <summary>Zwischenschritt vor dem Mauern (#667): etwas tiefer stehen, weniger Risiko nach vorn.</summary>
    Defensiv,
    Neutral,
    Offensiv,
    AllesNachVorn,
    /// <summary>Schlussphase: der Torhüter geht bei Standards mit nach vorn.</summary>
    Brechstange
}

/// <summary>Zustand eines Teams, der seine Torerwartung und die des Gegners verändert. <see cref="Position"/> ist der
/// Platz auf der Risikoachse – seit #667 nicht immer genau die Stufe, sondern verschoben um die Grundausrichtung.</summary>
public readonly record struct Teamzustand(Risikostufe Stufe, int Platzverweise, double Position)
{
    /// <summary>Ein Team genau auf seiner Stufe.</summary>
    public Teamzustand(Risikostufe stufe, int platzverweise) : this(stufe, platzverweise, Spielsituation.Position(stufe)) { }

    public static Teamzustand Anpfiff(int platzverweise = 0, double startpunkt = 0)
        => new(Risikostufe.Neutral, platzverweise, startpunkt);

    /// <summary>Wechselt in die Stufe, auf dem Platz, den das Profil des Teams dafür vorsieht.</summary>
    public Teamzustand Umstellen(Risikostufe stufe, Teamprofil profil)
        => this with { Stufe = stufe, Position = profil.Ziel(stufe) };
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
/// Faktoren sind bewusst Konstanten; welche Stufe ein Team wählt, entscheidet <see cref="Risikoentscheidung"/>.
/// </summary>
public static class Spielsituation
{
    // ── Risikoachse ───────────────────────────────────────────────────────
    // Welche Stufe ein Team wählt, entscheidet seit #673 die Risikoentscheidung; hier steht nur ihre Wirkung. Seit
    // #667 liegen die Stufen auf einer Achse (Mauern −2 … Brechstange 3): Grundausrichtung und Flexibilität lassen ein
    // Team auch zwischen zwei Stufen spielen, die Faktoren werden dazwischen linear interpoliert.

    private const int ACHSE_ANFANG = -2;

    // Je Position der Achse, beginnend bei Mauern. Defensiv liegt vorläufig in der Mitte zwischen Mauern und Neutral.
    /// <summary>Wirkung auf den eigenen Angriff: mit dem Risiko nur leicht stärker.</summary>
    private static readonly double[] _eigenerAngriff   = [0.75, 0.875, 1.0, 1.10, 1.20, 1.30];
    /// <summary>Wirkung auf den Angriff des Gegners: die Abwehr wird überproportional schwächer (Konter, Lucky
    /// Punch); bei der Brechstange steht das Tor leer.</summary>
    private static readonly double[] _angriffDesGegners = [0.85, 0.925, 1.0, 1.30, 1.60, 2.00];

    /// <summary>Position der Stufe auf der Risikoachse: Mauern −2, Defensiv −1, Neutral 0 … Brechstange 3.</summary>
    public static double Position(Risikostufe stufe) => (int)stufe + ACHSE_ANFANG;

    public static double EigenerAngriff(Risikostufe stufe) => EigenerAngriff(Position(stufe));
    public static double AngriffDesGegners(Risikostufe stufe) => AngriffDesGegners(Position(stufe));

    /// <summary>Wirkung einer Position der Risikoachse auf den eigenen Angriff; jenseits der Enden wie das Ende.</summary>
    public static double EigenerAngriff(double position) => Interpoliert(_eigenerAngriff, position);

    /// <summary>Wirkung einer Position der Risikoachse auf den Angriff des Gegners; jenseits der Enden wie das Ende.</summary>
    public static double AngriffDesGegners(double position) => Interpoliert(_angriffDesGegners, position);

    private static double Interpoliert(double[] werte, double position)
    {
        double index = Math.Clamp(position - ACHSE_ANFANG, 0, werte.Length - 1);
        int unten = Math.Min((int)index, werte.Length - 2);
        return werte[unten] + (werte[unten + 1] - werte[unten]) * (index - unten);
    }

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
        => EigenerAngriff(angreifer.Position)
           * AngriffDesGegners(verteidiger.Position)
           * Math.Pow(UNTERZAHL_EIGENER_ANGRIFF, angreifer.Platzverweise)
           * Math.Pow(UNTERZAHL_ANGRIFF_GEGNER, verteidiger.Platzverweise)
           * Torfaktor(ermuedung);

    /// <summary>Text des Ereignisses <c>Taktikwechsel</c> beim Wechsel in diese Stufe. Gemauert wird seit #673 auch
    /// bei Gleichstand (in Unterzahl).</summary>
    public static string Taktiktext(Risikostufe stufe, int vorsprung) => stufe switch
    {
        Risikostufe.Defensiv when vorsprung == 0 => "Zieht sich etwas zurück und sichert das Unentschieden",
        Risikostufe.Defensiv      => "Zieht sich etwas zurück und sichert die Führung",
        Risikostufe.Mauern when vorsprung == 0 => "Zieht sich zurück und verteidigt das Unentschieden",
        Risikostufe.Mauern        => "Zieht sich zurück und verteidigt die Führung",
        Risikostufe.Offensiv      => "Stellt auf Offensive um",
        Risikostufe.AllesNachVorn => "Wirft alles nach vorn",
        Risikostufe.Brechstange   => "Torwart geht mit nach vorn",
        _                         => "Kehrt zur gewohnten Ordnung zurück"
    };
}
