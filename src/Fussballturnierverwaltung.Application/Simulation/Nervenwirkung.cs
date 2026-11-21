using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Wirkung der Nervenstärke auf die Trefferquote im Elfmeterschießen (#669). Reine Funktion; „Normal“ gibt die
/// Basisquote unverändert zurück und zieht keinen Zufall. Öffentlich, weil <c>Elfmeterschiessen</c> intern ist und
/// <c>Application</c> den Tests keine internen Typen öffnet.</summary>
public static class Nervenwirkung
{
    /// <summary>Verschiebung der Quote je Stufe (stark +, schwach −), wenn der Schuss nicht unter Druck steht.</summary>
    public const double VERSCHIEBUNG = 0.02;

    /// <summary>Verschiebung unter Druck (Muss-Treffer, Sudden Death).</summary>
    public const double VERSCHIEBUNG_UNTER_DRUCK = 0.03;

    private const double MINDEST_QUOTE = 0.5;
    private const double HOECHST_QUOTE = 0.92;

    /// <summary>Unter Druck steht ein regulärer Schuss, wenn ein Fehlschuss das Schießen sofort verloren macht: der
    /// Rückstand des Schützen ist größer als seine verbleibenden Schüsse nach diesem. Im Sudden Death steht jeder Schuss
    /// unter Druck – das entscheidet der Aufrufer.</summary>
    public static bool UnterDruck(int rueckstand, int verbleibendeSchuesse) => rueckstand > verbleibendeSchuesse;

    public static double Trefferquote(double basis, Nervenstaerke stufe, bool unterDruck)
    {
        if (stufe == Nervenstaerke.Normal)
            return basis;

        double verschiebung = unterDruck ? VERSCHIEBUNG_UNTER_DRUCK : VERSCHIEBUNG;
        return Math.Clamp(basis + (int)stufe * verschiebung, MINDEST_QUOTE, HOECHST_QUOTE);
    }
}
