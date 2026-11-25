using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Der Startwert eines Teams in der Tabelle einer Folgerunde (#355): die übernommenen Punkte und der
/// ungerundete Wert. Beide Werte brauchen Tabelle und Rangfolge – bei Punktgleichheit entscheidet der ungerundete Wert
/// vor allen Kriterien des Regelsets.</summary>
/// <param name="Punkte">Ganze Punkte, mit denen die Tabelle startet.</param>
/// <param name="Ungerundet">Der Wert vor dem Runden (bei halbierten Modi in halben Punkten).</param>
public readonly record struct Vortragswert(int Punkte, decimal Ungerundet)
{
    public static readonly Vortragswert Keiner = new(0, 0m);

    /// <summary>Wurde der Wert beim Halbieren gerundet (ungerade Punktzahl)?</summary>
    public bool IstGerundet => Ungerundet != Punkte;

    /// <summary>Wurde abgerundet – dann liegt der ganze Wert unter dem ungerundeten.</summary>
    public bool IstAbgerundet => Punkte < Ungerundet;

    /// <summary>Wurde aufgerundet – dann liegt der ganze Wert über dem ungerundeten.</summary>
    public bool IstAufgerundet => Punkte > Ungerundet;
}

/// <summary>Reine Funktion: Vortrag aus den Punkten der Quellrunde und dem Modus der Zielrunde (#355).</summary>
public static class PunktevortragBerechnung
{
    public static Vortragswert Berechnen(int quellPunkte, Punktevortrag modus) => modus switch
    {
        Punktevortrag.Voll                => new Vortragswert(quellPunkte, quellPunkte),
        Punktevortrag.HalbiertAbgerundet  => new Vortragswert(quellPunkte / 2, quellPunkte / 2m),
        Punktevortrag.HalbiertAufgerundet => new Vortragswert((quellPunkte + 1) / 2, quellPunkte / 2m),
        _                                 => Vortragswert.Keiner
    };
}
