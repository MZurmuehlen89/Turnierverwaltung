using Fussballturnierverwaltung.Application.Spiele;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Tore aus dem Hinspiel aus Sicht des Rückspiels (#671): Das Rückspiel-Heimteam war im Hinspiel der Gast. Daraus
/// ergibt sich der maßgebliche Vorsprung, nach dem sich die Risikostufe im Rückspiel richtet. <c>default</c> heißt
/// „kein Hinspiel“ – dann ist der Vorsprung der Spielstand des Spiels selbst.
/// </summary>
/// <param name="ToreHeim">Hinspiel-Tore des Rückspiel-Heimteams (seine Auswärtstore).</param>
/// <param name="ToreGast">Hinspiel-Tore des Rückspiel-Gasts.</param>
public readonly record struct Hinspielstand(int ToreHeim, int ToreGast, bool AuswaertstorRegelAktiv)
{
    /// <summary>Aus dem Hinspiel-Ergebnis, wie es gespeichert ist (Heim = späterer Rückspiel-Gast).</summary>
    public static Hinspielstand AusHinspiel(int hinspielHeimTore, int hinspielAuswaertsTore, bool auswaertstorRegelAktiv)
        => new(hinspielAuswaertsTore, hinspielHeimTore, auswaertstorRegelAktiv);

    /// <summary>
    /// Maßgeblicher Vorsprung des Rückspiel-Heimteams beim Rückspielstand <paramref name="heimTore"/> :
    /// <paramref name="gastTore"/>: Ist es weiter, die Zahl der Tore, die der Gast noch braucht, damit es das nicht
    /// mehr ist; liegt es zurück, negativ die Zahl der Tore, die es selbst braucht, um mindestens gleichzuziehen.
    /// Ohne Auswärtstorregel ist das die Tordifferenz des Gesamtstands, mit ihr kann es mehr sein. Für den Gast gilt
    /// derselbe Wert mit umgekehrtem Vorzeichen.
    /// </summary>
    public int VorsprungDesHeimteams(int heimTore, int gastTore)
    {
        var stand = this; // Lambdas in einer Struktur dürfen this nicht fassen
        int wertung = stand.Wertung(heimTore, gastTore);
        if (wertung > 0)
            return ToreBisZurWende(k => stand.Wertung(heimTore, gastTore + k) > 0);
        if (wertung < 0)
            return -ToreBisZurWende(k => stand.Wertung(heimTore + k, gastTore) < 0);
        return 0;
    }

    private int Wertung(int heimTore, int gastTore) => HinRueckspielWertung.Gesamtwertung(
        ToreGast, ToreHeim, heimTore, gastTore, AuswaertstorRegelAktiv);

    /// <summary>Kleinste Torzahl, ab der die Lage nicht mehr gilt. Endet immer: Mit jedem Tor kippt irgendwann
    /// der Gesamtstand.</summary>
    private static int ToreBisZurWende(Func<int, bool> giltNoch)
    {
        int tore = 1;
        while (giltNoch(tore))
            tore++;
        return tore;
    }
}
