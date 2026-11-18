using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Spielweise eines Teams (#667), übersetzt in Werte der Risikoachse. Die <see cref="Grundausrichtung"/> ist der
/// Startpunkt g, auf dem das Team in der gewohnten Ordnung spielt, und verschiebt die Umstellzeitpunkte. Die
/// <see cref="Flexibilitaet"/> f bestimmt, wie weit ein Team von g abweicht, wenn die Lage eine Stufe verlangt (Ziel
/// S + (1 − f) · g), und wie schnell es umstellt. Ein ausgeglichenes, normal flexibles Team (der Standardwert)
/// spielt genau wie vor #667. Die <see cref="Spielbedeutung"/> der Runde (#668) verschiebt den Startpunkt zusätzlich –
/// im Finale vorsichtiger, im Spiel um Platz 3 offener; bei „normal“ wie vor #668.
/// </summary>
public readonly record struct Teamprofil(
    Grundausrichtung Grundausrichtung, Flexibilitaet Flexibilitaet, Spielbedeutung Spielbedeutung = Spielbedeutung.Normal,
    Kondition Kondition = Kondition.Normal)
{
    public const int NORMALES_UMSTELLFENSTER = 8;

    private const double SCHRITT_JE_STUFE  = 0.5; // auf der Risikoachse je Stufe der Grundausrichtung
    private const int    MINUTEN_JE_STUFE  = 5;   // Verschiebung der Umstellzeitpunkte je Stufe

    public static Teamprofil Von(Mannschaft mannschaft, Spielbedeutung spielbedeutung = Spielbedeutung.Normal)
        => new(mannschaft.Grundausrichtung, mannschaft.Flexibilitaet, spielbedeutung, mannschaft.Kondition);

    /// <summary>Platz auf der Risikoachse in der gewohnten Ordnung: −1 (sehr defensiv) bis +1 (sehr offensiv), dazu die
    /// Verschiebung durch die Spielbedeutung.</summary>
    public double Startpunkt => (int)Grundausrichtung * SCHRITT_JE_STUFE + Verschiebung(Spielbedeutung);

    /// <summary>Verschiebung des Startpunkts durch die Spielbedeutung (#668, mit dem Nutzer abgestimmt): bewusst klein
    /// gehalten; offen stärker als vorsichtig, weil die Achse nach vorn mehr Abwehr kostet, als sie nach hinten an
    /// Angriff spart, und weil Spiele um Platz 3 real deutlich torreicher sind, Finals dagegen nicht durchgehend torarm.</summary>
    private static double Verschiebung(Spielbedeutung spielbedeutung) => spielbedeutung switch
    {
        Spielbedeutung.Hoch   => -0.25,
        Spielbedeutung.Gering => 0.5,
        _                     => 0
    };

    /// <summary>Minuten, um die das Team im Rückstand früher drückt und in Führung später mauert (negativ: umgekehrt).
    /// Eine konditionsschwache Mannschaft greift seltener an und mauert eher (#677): 5 Minuten weniger.</summary>
    public int Offensivdrang => (int)Grundausrichtung * MINUTEN_JE_STUFE - (Kondition == Kondition.Schwach ? MINUTEN_JE_STUFE : 0);

    /// <summary>Wie schnell das Team ermüdet (#677): schwach 1,15, normal 1, stark 0,85 – Faktor auf die Ermüdung.</summary>
    public double Ermuedungsfaktor => Kondition switch
    {
        Kondition.Schwach => 1.15,
        Kondition.Stark   => 0.85,
        _                 => 1.0
    };

    /// <summary>Minuten von der Schwelle bis zum sicheren Umstellen: starr 12, normal 8, flexibel 4.</summary>
    public int Umstellfenster => Flexibilitaet switch
    {
        Flexibilitaet.Starr    => 12,
        Flexibilitaet.Flexibel => 4,
        _                      => NORMALES_UMSTELLFENSTER
    };

    /// <summary>Anteil, zu dem das Team der verlangten Stufe folgt: starr 0, normal ½, flexibel 1.</summary>
    private double Anpassung => Flexibilitaet switch
    {
        Flexibilitaet.Starr    => 0,
        Flexibilitaet.Flexibel => 1,
        _                      => 0.5
    };

    /// <summary>Platz auf der Risikoachse, wenn das Team in die Stufe wechselt; die gewohnte Ordnung ist der
    /// Startpunkt.</summary>
    public double Ziel(Risikostufe stufe) => stufe == Risikostufe.Neutral
        ? Startpunkt
        : Math.Clamp(Spielsituation.Position(stufe) + (1 - Anpassung) * Startpunkt,
            Spielsituation.ACHSE_ANFANG, Spielsituation.ACHSE_ENDE);
}

/// <summary>Die Profile beider Teams eines Spiels.</summary>
public readonly record struct Teamprofile(Teamprofil Heim, Teamprofil Gast);
