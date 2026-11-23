namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Was ein Team in einer Minute über seine Risikostufe entscheiden lässt (#673).</summary>
/// <param name="Vorsprung">Maßgeblicher Vorsprung (negativ = Rückstand); im Rückspiel nach dem Gesamtstand (#671).</param>
/// <param name="Verbleibend">Minuten bis zum Ende des Abschnitts; in der Nachspielzeit negativ.</param>
/// <param name="Staerkeanteil">Anteil des Teams an der Torerwartung beider Teams – 0,5 heißt gleich stark.</param>
/// <param name="Unterzahl">Eigene minus gegnerische Platzverweise; negativ heißt Überzahl.</param>
public readonly record struct Spiellage(
    int Vorsprung, int Verbleibend, double Staerkeanteil, int Unterzahl, Teamprofil Profil = default);

/// <summary>Stufe, die in einer Lage fällig ist, und die Restzeit, ab der sie es ist.</summary>
public readonly record struct FaelligeStufe(Risikostufe Stufe, int AbVerbleibend);

/// <summary>
/// Entscheidungsmodell der Risikostufe (#673): Aus der Lage ergibt sich ein Stufenplan – je Stufe die Restzeit, ab
/// der sie frühestens fällig ist. Je größer der Rückstand, desto früher wird umgestellt; ab drei Toren wird ein
/// Rückstand mit schwindender Zeit aussichtslos. In Führung geht es erst auf Defensiv, dann aufs Mauern (#667). Ein
/// Favorit im Rückstand drückt früher, ein Außenseiter in Führung mauert früher, Unterzahl verschiebt Richtung Mauern;
/// ebenso verschiebt die Grundausrichtung (#667) die Schwellen. Wann ein Team nach Erreichen der Schwelle tatsächlich umstellt,
/// entscheidet der Zufall mit steigender Wahrscheinlichkeit (<see cref="Umstellwahrscheinlichkeit"/>). Rein und ohne
/// Zufall; die Spielbedeutung (#668) setzt hier an. Die Tabellenlage (#176) ändert den Plan so: Wer einen Sieg braucht, spielt
/// bei Gleichstand nach dem Plan für ein Tor Rückstand, wem ein Remis reicht, nach dem Führungsplan; wer ohne sportliches
/// Ziel spielt, schickt den Torwart nie mit nach vorn.
/// </summary>
public static class Risikoentscheidung
{
    // Restzeit-Schwellen bei gleich starken Teams in Gleichzahl (Tabelle im Issue #673, mit dem Nutzer abgestimmt).
    private const int OFFENSIV_AB          = 30; // 60. Minute bei einem Tor Rückstand
    private const int ALLES_NACH_VORN_AB   = 10; // 80. Minute bei einem Tor Rückstand
    private const int FRUEHER_JE_TOR       = 15; // jedes weitere Tor Rückstand: eine Viertelstunde früher
    private const int BRECHSTANGE_AB       = 2;  // 88. Minute, nur bei genau einem Tor Rückstand
    private const int AUSSICHTSLOS_JE_TOR  = 10; // ab drei Toren: unter 10·(Rückstand − 2) Minuten Restzeit
    private const int DEFENSIV_AB          = 30; // 60. Minute (#667: erst defensiv …)
    private const int MAUERN_AB            = 20; // 70. Minute (… dann mauern)
    private const int UNTERZAHL_MAUERN_AB  = 15; // bei Gleichstand je Mann weniger
    // Verschiebungen in Minuten
    private const double KRAEFTE_MINUTEN   = 80; // je vollem Stärkeanteil über/unter 0,5
    private const int UNTERZAHL_OFFENSIV   = 10; // je Mann weniger später offensiv
    private const int UNTERZAHL_MAUERN     = 15; // je Mann weniger früher mauern

    /// <summary>Schwelle der gewohnten Ordnung vor der ersten Stufe des Plans: Die Zeit allein führt nie dorthin
    /// zurück (etwa zu Beginn der Verlängerung oder nach einem Platzverweis des Gegners) – das tut nur ein Tor.</summary>
    public const int OHNE_SCHWELLE = int.MaxValue;

    // ── Stufenplan ────────────────────────────────────────────────────────

    /// <summary>Stufen in der Reihenfolge, in der sie im Spiel aufeinander folgen, je mit ihrer Schwelle. Eine
    /// spätere Stufe gilt, sobald sie fällig ist, auch wenn eine Verschiebung eine frühere erst danach fällig
    /// macht.</summary>
    public static IReadOnlyList<FaelligeStufe> Stufenplan(Spiellage lage)
    {
        if (lage.Vorsprung > 0)
            return FuehrungsPlan(DEFENSIV_AB, MAUERN_AB, Drang(lage) - UNTERZAHL_MAUERN * lage.Unterzahl);
        if (lage.Vorsprung == 0 && lage.Profil.Bedarf == Tabellenbedarf.RemisReicht)
            return FuehrungsPlan(DEFENSIV_AB, MAUERN_AB, Drang(lage) - UNTERZAHL_MAUERN * lage.Unterzahl);
        if (lage.Vorsprung == 0 && lage.Profil.Bedarf == Tabellenbedarf.SiegNoetig)
            return RueckstandsPlan(1, Drang(lage) - UNTERZAHL_OFFENSIV * lage.Unterzahl, ohneBrechstange: false);
        if (lage.Vorsprung == 0)
            return lage.Unterzahl > 0
                ? [new(Risikostufe.Mauern, UNTERZAHL_MAUERN_AB * lage.Unterzahl - lage.Profil.Offensivdrang)]
                : [];
        return RueckstandsPlan(
            -lage.Vorsprung, Drang(lage) - UNTERZAHL_OFFENSIV * lage.Unterzahl,
            ohneBrechstange: lage.Profil.Bedarf == Tabellenbedarf.OhneZiel);
    }

    /// <summary>Erst defensiv, dann mauern (#667) – beides um <paramref name="spaeter"/> Minuten verschoben.</summary>
    private static List<FaelligeStufe> FuehrungsPlan(int defensivAb, int mauernAb, double spaeter) =>
    [
        new(Risikostufe.Defensiv, Runden(defensivAb - spaeter)),
        new(Risikostufe.Mauern, Runden(mauernAb - spaeter))
    ];

    private static List<FaelligeStufe> RueckstandsPlan(int rueckstand, double frueher, bool ohneBrechstange)
    {
        int weitereTore = rueckstand - 1;
        var plan = new List<FaelligeStufe>
        {
            new(Risikostufe.Offensiv, Runden(OFFENSIV_AB + FRUEHER_JE_TOR * weitereTore + frueher)),
            new(Risikostufe.AllesNachVorn, Runden(ALLES_NACH_VORN_AB + FRUEHER_JE_TOR * weitereTore + frueher))
        };
        if (rueckstand == 1 && !ohneBrechstange)
            plan.Add(new(Risikostufe.Brechstange, BRECHSTANGE_AB));
        else if (rueckstand >= 3)
            plan.Add(new(Risikostufe.Neutral, AUSSICHTSLOS_JE_TOR * (rueckstand - 2) - 1));
        return plan;
    }

    /// <summary>Minuten, um die ein Team früher drückt und später mauert (negativ: umgekehrt): stärker als der Gegner
    /// oder offensiver ausgerichtet (#667).</summary>
    private static double Drang(Spiellage lage)
        => (lage.Staerkeanteil - 0.5) * KRAEFTE_MINUTEN + lage.Profil.Offensivdrang;

    private static int Runden(double minuten) => (int)Math.Round(minuten, MidpointRounding.AwayFromZero);

    // ── Entscheidung ──────────────────────────────────────────────────────

    /// <summary>Die späteste Stufe des Plans, deren Schwelle erreicht ist; vor der ersten die gewohnte Ordnung.</summary>
    public static FaelligeStufe Faellig(Spiellage lage)
    {
        var faellig = new FaelligeStufe(Risikostufe.Neutral, OHNE_SCHWELLE);
        foreach (var stufe in Stufenplan(lage))
            if (lage.Verbleibend <= stufe.AbVerbleibend)
                faellig = stufe;
        return faellig;
    }

    /// <summary>Wahrscheinlichkeit, in dieser Minute ohne Tor auf eine fällige Stufe umzustellen: in der Minute der
    /// Schwelle 1/Fenster, danach je Minute ein Fenster-Teil mehr – beim normalen Fenster von acht Minuten ist also
    /// spätestens sieben Minuten nach der Schwelle umgestellt; ohne Schwelle nie. Das Fenster hängt seit #667 an der
    /// Flexibilität.</summary>
    public static double Umstellwahrscheinlichkeit(
        FaelligeStufe faellig, int verbleibend, int umstellfenster = Teamprofil.NORMALES_UMSTELLFENSTER)
        => faellig.AbVerbleibend == OHNE_SCHWELLE
            ? 0
            : Math.Clamp(((double)faellig.AbVerbleibend - verbleibend + 1) / umstellfenster, 0, 1);
}
