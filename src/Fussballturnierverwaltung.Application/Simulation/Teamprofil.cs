using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Spielweise eines Teams (#667), übersetzt in Werte der Risikoachse. Die <see cref="Grundausrichtung"/> ist der
/// Startpunkt g, auf dem das Team in der gewohnten Ordnung spielt, und verschiebt die Umstellzeitpunkte. Die
/// <see cref="Flexibilitaet"/> f bestimmt, wie weit ein Team von g abweicht, wenn die Lage eine Stufe verlangt (Ziel
/// S + (1 − f) · g), und wie schnell es umstellt. Ein ausgeglichenes, normal flexibles Team (der Standardwert)
/// spielt genau wie vor #667.
/// </summary>
public readonly record struct Teamprofil(Grundausrichtung Grundausrichtung, Flexibilitaet Flexibilitaet)
{
    public const int NORMALES_UMSTELLFENSTER = 8;

    private const double SCHRITT_JE_STUFE  = 0.5; // auf der Risikoachse je Stufe der Grundausrichtung
    private const int    MINUTEN_JE_STUFE  = 5;   // Verschiebung der Umstellzeitpunkte je Stufe

    public static Teamprofil Von(Mannschaft mannschaft) => new(mannschaft.Grundausrichtung, mannschaft.Flexibilitaet);

    /// <summary>Platz auf der Risikoachse in der gewohnten Ordnung: −1 (sehr defensiv) bis +1 (sehr offensiv).</summary>
    public double Startpunkt => (int)Grundausrichtung * SCHRITT_JE_STUFE;

    /// <summary>Minuten, um die das Team im Rückstand früher drückt und in Führung später mauert (negativ: umgekehrt).</summary>
    public int Offensivdrang => (int)Grundausrichtung * MINUTEN_JE_STUFE;

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
