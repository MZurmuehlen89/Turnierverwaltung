namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Turnierstärke (#679): Je Team und Turnier(durchlauf) wird einmal eine moderate Abweichung von seiner Stärke gezogen
/// und für das ganze Turnier beibehalten – ein starkes Turnier (Griechenland 2004) entsteht so zusammenhängend statt
/// durch Zufall in jedem Spiel neu. Die Abweichung ist <b>verborgen</b>: Sie steht nirgends, ändert keine Stammdaten
/// und kommt nur als Faktor auf die Torerwartung ins Spiel.
/// <para>Rein und zustandslos: der Wert folgt allein aus <c>StaerkeSeed</c> des Turniers und der Mannschafts-Id, ist
/// also reproduzierbar und braucht keinen Zufallsstrom des Spiels (dessen Folge bleibt unberührt). Elo (#359) darf sie
/// nicht einlesen; Nervenstärke (#669, nur Elfmeterschießen) und Spielkontrolle (#674) bleiben unverändert.</para>
/// </summary>
public static class Turnierstaerke
{
    /// <summary>Streuung der Abweichung – gemessen in #679 (K.-o.-Turnier, Bänder der Realismus-Tests halten).</summary>
    public const double STREUUNG = 0.075;

    /// <summary>Abweichung (Faktor im Exponenten) der Mannschaft in diesem Turnier, ~ N(0, <see cref="STREUUNG"/>²). Die
    /// Mischung aus Seed und Id ist fest (kein <c>HashCode.Combine</c>, der je Prozess verschieden ist).</summary>
    public static double Wert(int staerkeSeed, int mannschaftId)
    {
        var zufall = new Random(unchecked((staerkeSeed * 16777619) ^ (mannschaftId * 486187739)));
        // Der erste Wert eines Random hängt bei benachbarten Seeds stark zusammen – verwerfen.
        zufall.NextDouble();
        return Normalverteilung.Ziehen(zufall) * STREUUNG;
    }

    /// <summary>Die Turnierform eines Spiels aus Sicht von Heim und Gast.</summary>
    public static Turnierform Form(int staerkeSeed, int heimMannschaftId, int auswaertsMannschaftId) =>
        new(Wert(staerkeSeed, heimMannschaftId), Wert(staerkeSeed, auswaertsMannschaftId));
}

/// <summary>Die Turnierstärken beider Teams eines Spiels (<see cref="Turnierstaerke.Wert"/>). Die Torerwartung eines Teams
/// wächst mit der eigenen und sinkt mit der gegnerischen Abweichung; −σ² hält den Mittelwert der Faktoren bei 1.</summary>
public readonly record struct Turnierform(double Heim, double Gast)
{
    private const double MITTELWERT_AUSGLEICH = Turnierstaerke.STREUUNG * Turnierstaerke.STREUUNG;

    /// <summary>Wandert das Heimrecht (Wiederholungsspiel), wandert die Form mit dem Team.</summary>
    public Turnierform Tauschen() => new(Gast, Heim);

    /// <summary>Faktor auf die Torerwartung des Heimteams.</summary>
    public double HeimFaktor => Faktor(Heim, Gast);

    /// <summary>Faktor auf die Torerwartung des Gastteams.</summary>
    public double GastFaktor => Faktor(Gast, Heim);

    private static double Faktor(double angreifer, double verteidiger) =>
        Math.Exp(angreifer - verteidiger - MITTELWERT_AUSGLEICH);
}
