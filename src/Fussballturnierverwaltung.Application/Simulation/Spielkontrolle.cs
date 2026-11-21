namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Verborgener, zeitlich zusammenhängender Zustand eines Spiels (#674): Phasen, in denen ein Team das Spiel bestimmt,
/// ohne dass es sich in Toren zeigen muss. Der Stand z schwankt Minute für Minute (z' = ρ·z + ε, abklingend) und
/// verschiebt die Chancenrate – Heim ×e^z, Gast ×e^−z, normiert, sodass sie im eingeschwungenen Zustand im Mittel
/// gleich bleibt. Beim Anpfiff steht z auf 0; in den ersten Minuten liegt der Faktor deshalb im Mittel knapp unter 1
/// (Minute 1 ≈ 0,96, über 90 Minuten knapp 1 % weniger Chancen) – das gleicht ABLAUF_KORREKTUR im Spielablauf mit aus,
/// beides ist zusammen an der Realismus-Stichprobe kalibriert (Review #674).
/// <para>Bewusst <b>ohne</b> Bezug zu Toren (kein Momentum): Eine Kopplung „ein Tor stärkt den Torschützen“ wurde in #674
/// gemessen und verschlechtert die Realismus-Kennzahlen (zu viele Kantersiege, 1:0 bleibt öfter stehen). Ebenso ohne
/// Bezug zu Stärke, Form oder Spielstand – Tagesform (#679) verschiebt die Stärke eines ganzen Spiels, Nervenstärke
/// (#669) nur das Elfmeterschießen; dieselbe schwache Phase wird so nicht doppelt bestraft.</para>
/// <para>Zieht aus einem eigenen Zufallsstrom, damit die Würfe des Spielablaufs je Minute unverändert bleiben; dessen
/// Startwert stammt aus dem Zufall des Spiels, ein Spiel mit festem Seed bleibt also reproduzierbar.</para>
/// </summary>
public sealed class Spielkontrolle(Random zufall)
{
    /// <summary>Anteil des Stands, der je Minute erhalten bleibt (Halbwertszeit gut 20 Minuten).</summary>
    public const double ERHALT_JE_MINUTE = 0.97;

    /// <summary>Streuung des Stands im eingeschwungenen Zustand – kalibriert in #674 (Messreihe).</summary>
    public const double STREUUNG = 0.3;

    private static readonly double _schritt = STREUUNG * Math.Sqrt(1 - ERHALT_JE_MINUTE * ERHALT_JE_MINUTE);
    private static readonly double _normierung = Math.Exp(-STREUUNG * STREUUNG / 2);

    /// <summary>Positiv: das Heimteam bestimmt das Spiel. Beim Anpfiff 0.</summary>
    public double Stand { get; private set; }

    public double HeimFaktor => Faktor(Stand);
    public double GastFaktor => Faktor(-Stand);

    /// <summary>Eine Minute weiter.</summary>
    public void Fortschreiben() => Stand = Naechster(Stand, Normalverteilung.Ziehen(zufall));

    /// <summary>Stand nach einer Minute bei standardnormalem <paramref name="rauschen"/>; die Schrittweite hält die
    /// Streuung im eingeschwungenen Zustand bei <see cref="STREUUNG"/>.</summary>
    public static double Naechster(double stand, double rauschen) => ERHALT_JE_MINUTE * stand + _schritt * rauschen;

    /// <summary>Faktor auf die Chancenrate des Teams, zu dessen Gunsten <paramref name="stand"/> zeigt; im
    /// eingeschwungenen Zustand im Mittel 1 (E[e^z] = e^(σ²/2)).</summary>
    public static double Faktor(double stand) => Math.Exp(stand) * _normierung;
}
