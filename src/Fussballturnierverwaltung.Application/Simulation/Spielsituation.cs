using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Taktische Ausrichtung eines Teams im Spielverlauf (#361), aufsteigend nach Risiko.</summary>
public enum Risikostufe
{
    /// <summary>Führendes Team verteidigt den Vorsprung – greift weniger an und überlässt dem Gegner das Spiel (seit
    /// #674; bis dahin weniger Tore auf beiden Seiten).</summary>
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

    /// <summary>Enden der Risikoachse: Mauern und Brechstange.</summary>
    public const int ACHSE_ANFANG = -2;
    public const int ACHSE_ENDE = 3;

    // Je Position der Achse, beginnend bei Mauern. Defensiv liegt vorläufig in der Mitte zwischen Mauern und Neutral.
    // Die Achse beschreibt die Haltung (Grundausrichtung, Spielbedeutung): defensiv = kompakt, weniger Tore auf beiden
    // Seiten. Der Rückzug in Führung (Stufen Defensiv und Mauern) wirkt seit #674 anders – siehe Rückzug.
    /// <summary>Wirkung auf den eigenen Angriff: mit dem Risiko nur leicht stärker.</summary>
    private static readonly double[] _eigenerAngriff   = [0.75, 0.875, 1.0, 1.10, 1.20, 1.30];
    /// <summary>Wirkung auf den Angriff des Gegners: die Abwehr wird überproportional schwächer (Konter, Lucky
    /// Punch); bei der Brechstange steht das Tor leer.</summary>
    private static readonly double[] _angriffDesGegners = [0.85, 0.925, 1.0, 1.30, 1.60, 2.00];

    /// <summary>Position der Stufe auf der Risikoachse: Mauern −2, Defensiv −1, Neutral 0 … Brechstange 3.</summary>
    public static double Position(Risikostufe stufe) => (int)stufe + ACHSE_ANFANG;

    /// <summary>Wirkung der Stufe auf den eigenen Angriff, für ein Team genau auf der Stufe (Rückzug eingerechnet).</summary>
    public static double EigenerAngriff(Risikostufe stufe) => EigenerAngriff(new Teamzustand(stufe, 0));

    /// <summary>Wirkung der Stufe auf den Angriff des Gegners, für ein Team genau auf der Stufe (Rückzug eingerechnet).</summary>
    public static double AngriffDesGegners(Risikostufe stufe) => AngriffDesGegners(new Teamzustand(stufe, 0));

    /// <summary>Wirkung eines Teams auf seinen eigenen Angriff: Platz auf der Achse, dazu der Rückzug seiner Stufe.</summary>
    public static double EigenerAngriff(Teamzustand team) => EigenerAngriff(team.Position) * Rueckzug(team.Stufe).Eigener;

    /// <summary>Wirkung eines Teams auf den Angriff des Gegners: Platz auf der Achse, dazu der Rückzug seiner Stufe.</summary>
    public static double AngriffDesGegners(Teamzustand team) => AngriffDesGegners(team.Position) * Rueckzug(team.Stufe).Gegner;

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

    // ── Rückzug ───────────────────────────────────────────────────────────
    // Die Stufen Defensiv und Mauern wählt die Risikoentscheidung, um eine Führung (in Unterzahl ein Unentschieden) zu
    // halten. Wer sich so zurückzieht, greift weniger an und überlässt dem Gegner das Spiel (#674). Bis dahin wirkten
    // die Stufen wie die Achse und senkten auch den Angriff des Gegners – das fror ein 1:0 ein, real wird eine knappe
    // Führung öfter ausgeglichen (1:0 + 0:1 12,1 %, Dixon-Coles ρ −0,14). Zielwerte für ein Team genau auf der Stufe;
    // kalibriert an SimulationsRealismusTests, Messreihe in #674. Die Haltung (Grundausrichtung, Spielbedeutung) bleibt
    // auf der Achse: ein defensiv ausgerichtetes Team kassiert weiter weniger (#667, #668). Bewusst auch beim Mauern in
    // Unterzahl: Wer sich zu zehnt hinten reinstellt, lädt ebenso Druck ein (Überzahl-Kennzahl im Band, Review #674).
    private const double DEFENSIV_EIGENER = 0.95;
    private const double DEFENSIV_GEGNER  = 1.05;
    private const double MAUERN_EIGENER   = 0.90;
    private const double MAUERN_GEGNER    = 1.10;

    // Einmal berechnet – der Rückzug wird je Minute und Chance gebraucht. Steht hinter den Achsenwerten, die er liest.
    private static readonly (double Eigener, double Gegner) _rueckzugDefensiv = RueckzugAufDerStufe(Risikostufe.Defensiv, DEFENSIV_EIGENER, DEFENSIV_GEGNER);
    private static readonly (double Eigener, double Gegner) _rueckzugMauern   = RueckzugAufDerStufe(Risikostufe.Mauern, MAUERN_EIGENER, MAUERN_GEGNER);

    /// <summary>Faktoren des Rückzugs auf den eigenen Angriff und den des Gegners – zusätzlich zur Achse, sodass ein
    /// Team genau auf der Stufe die Zielwerte erreicht; auf allen anderen Stufen 1.</summary>
    public static (double Eigener, double Gegner) Rueckzug(Risikostufe stufe) => stufe switch
    {
        Risikostufe.Defensiv => _rueckzugDefensiv,
        Risikostufe.Mauern   => _rueckzugMauern,
        _                    => (1, 1)
    };

    private static (double Eigener, double Gegner) RueckzugAufDerStufe(Risikostufe stufe, double eigener, double gegner)
        => (eigener / EigenerAngriff(Position(stufe)), gegner / AngriffDesGegners(Position(stufe)));

    // ── Platzverweise ─────────────────────────────────────────────────────

    public const double UNTERZAHL_EIGENER_ANGRIFF = 0.7;
    public const double UNTERZAHL_ANGRIFF_GEGNER  = 1.2;

    // ── Ermüdung ──────────────────────────────────────────────────────────

    /// <summary>Ermüdung nach gespielten Minuten (inklusive Nachspielzeit): 0 beim Anpfiff, 1 nach 90 Minuten,
    /// in der Verlängerung weiter steigend. In #361 für alle Mannschaften gleich.</summary>
    public static double Ermuedung(int gespielteMinuten) => gespielteMinuten / 90.0;

    /// <summary>Wirkung der Ermüdung auf die Kartenrate: steigt mit der Fehlerquote, im Mittel der regulären
    /// Spielzeit 1,0.</summary>
    public static double Kartenfaktor(double ermuedung) => 0.5 + ermuedung;

    /// <summary>Wie stark ein knapper Spielstand die Rotrate verschiebt (#682): Wer mit einem Tor hinten liegt, sieht
    /// häufiger Rot (Notbremse, Foul aus Verzweiflung), wer mit einem Tor führt, seltener. Real misst die Wirkung der
    /// Überzahl beides – die Karte selbst und dass sie eher das unterlegene Team trifft; die Unterzahl-Faktoren allein
    /// bleiben weit unter dem Realwert. Kalibriert an <c>SimulationsRealismusTests.Platzverweise_UeberzahlHilftSpuerbar</c>
    /// (Messreihe in #682).</summary>
    public const double ROT_SPIELSTAND_EINFLUSS = 0.6;

    /// <summary>Faktor auf die Rotrate eines Teams nach seinem Vorsprung. Nur bei einem Tor Abstand: Bei klarem Stand
    /// wird keine Notbremse mehr gezogen, und ein Rotfaktor dort würde Kantersiege weiter aufblähen (in #682 gemessen).
    /// Rückstand und Führung verschieben gleich weit, damit die Häufigkeit über das Spiel gleich bleibt.</summary>
    public static double Rotfaktor(int vorsprung) => vorsprung switch
    {
        -1 => 1 + ROT_SPIELSTAND_EINFLUSS,
        1  => 1 - ROT_SPIELSTAND_EINFLUSS,
        _  => 1
    };

    // ── Spielstand ────────────────────────────────────────────────────────
    // Faktor auf die Chancenrate beider Teams nach dem Spielstand (#674): Nach einem Ausgleich wird das Spiel ruhiger
    // (real 1:1 1,22-mal und 2:2 1,11-mal so häufig wie bei unabhängigen Toren), ein 0:0 bleibt dagegen kaum länger
    // stehen (1,04). Bei Ungleichstand leicht unter 1, damit die Torerwartung im Mittel bleibt (Kalibrierung #404).
    // Kalibriert an SimulationsRealismusTests (Ergebnisverteilung, Dixon-Coles), Messreihe in #674.
    public const double NACH_AUSGLEICH = 0.80;
    public const double UNGLEICHSTAND  = 0.96;

    /// <summary>Faktor auf die Chancenrate beider Teams nach dem Stand: 1 ohne Tor, <see cref="NACH_AUSGLEICH"/> bei
    /// Gleichstand nach Toren, sonst <see cref="UNGLEICHSTAND"/>. In der Verlängerung 1 – sie folgt immer auf
    /// Gleichstand und hat ihre eigene Kalibrierung (Zeitfaktor); kalibriert ist der Faktor nur an der regulären
    /// Spielzeit (Review #674).</summary>
    /// <param name="vorsprung">Maßgeblicher Vorsprung, im Rückspiel nach dem Gesamtstand (wie die Taktik, #671).</param>
    /// <param name="toreGefallen">Ob im Spiel – im Rückspiel samt Hinspiel – schon ein Tor gefallen ist.</param>
    /// <param name="verlaengerung">Ob die Minute zur Verlängerung gehört.</param>
    public static double Spielstandfaktor(int vorsprung, bool toreGefallen, bool verlaengerung)
    {
        if (verlaengerung) return 1;
        if (vorsprung != 0) return UNGLEICHSTAND;
        return toreGefallen ? NACH_AUSGLEICH : 1;
    }

    // ── Zeitprofil ────────────────────────────────────────────────────────

    // Faktor auf die Chancenrate je Viertelstunde der regulären Spielzeit und in der Nachspielzeit (#431, Schritt 4).
    // Real fallen in der ersten Viertelstunde die wenigsten Tore, gegen Ende jeder Halbzeit mehr (Länderspiele ab 2010:
    // 12,3 / 14,2 / 17,2 / 16,4 / 16,5 / 23,4 %, letzte Viertelstunde inklusive Nachspielzeit). Die Werte gleichen
    // aus, was die Risikoachse schon von sich aus an späten Toren erzeugt; kalibriert an
    // SimulationsRealismusTests.Torminuten_ZeitprofilDerTore (Messreihe in #431). Ersetzt den linearen Torfaktor.
    private static readonly double[] _zeitprofil = [0.815, 0.955, 1.01, 1.05, 1.03, 1.065];
    private const double NACHSPIELZEIT_ERSTE_HALBZEIT  = 1.0;
    private const double NACHSPIELZEIT_ZWEITE_HALBZEIT = 1.3;
    private const int ENDE_REGULAER = 90;

    /// <summary>Faktor auf die Chancenrate nach dem Zeitpunkt: in der regulären Spielzeit das Zeitprofil der
    /// Spielphase (im Mittel über die Spielzeit samt Nachspielzeit etwa 1, die Kalibrierung aus #404 bezieht sich auf
    /// die reguläre Spielzeit), in der Verlängerung die nachlassende Genauigkeit nach der Ermüdung (leicht weniger
    /// Tore, nie unter 70 %).</summary>
    /// <param name="minute">Spielminute; in der Nachspielzeit die letzte Minute der Halbzeit (45, 90, …).</param>
    public static double Zeitfaktor(int minute, bool nachspielzeit, double ermuedung)
    {
        if (minute > ENDE_REGULAER)
            return Math.Max(0.7, 1.05 - 0.9 * (ermuedung - 1));
        if (nachspielzeit)
            return minute <= ENDE_REGULAER / 2 ? NACHSPIELZEIT_ERSTE_HALBZEIT : NACHSPIELZEIT_ZWEITE_HALBZEIT;
        return _zeitprofil[(minute - 1) / 15];
    }

    // ── Menge und Güte der Chancen ────────────────────────────────────────
    // Die Risikoachse bestimmt, wie stark sich die Torerwartung verschiebt (EigenerAngriff, AngriffDesGegners); seit
    // #431 (Schritt 4) wirkt das getrennt auf Menge und Güte, das Produkt bleibt die Torerwartung. Wer mehr riskiert,
    // erspielt mehr, aber schlechtere Chancen (Flanken und Fernschüsse gegen eine tiefe Abwehr): die Chancenrate
    // steigt mit E², die Quote fällt mit 1/E. Wer mauert, hat wenige, aber gute Chancen (Konter). Gegen ein Team, das
    // aufmacht, steigen Menge und Güte je zur Hälfte (Konter in offene Räume, bei der Brechstange das leere Tor).

    /// <summary>Faktor auf die Quote der eigenen Chancen nach der eigenen Position auf der Risikoachse: 1/E.</summary>
    public static double EigeneGuete(double position) => 1 / EigenerAngriff(position);

    /// <summary>Faktor auf die Quote der eigenen Chancen eines Teams: 1/E, mit dem Rückzug seiner Stufe.</summary>
    public static double EigeneGuete(Teamzustand team) => 1 / EigenerAngriff(team);

    /// <summary>Faktor auf die Quote des Gegners nach der eigenen Position auf der Risikoachse: √G.</summary>
    public static double GueteDesGegners(double position) => Math.Sqrt(AngriffDesGegners(position));

    /// <summary>Faktor auf die Quote des Gegners eines Teams: √G, mit dem Rückzug seiner Stufe.</summary>
    public static double GueteDesGegners(Teamzustand team) => Math.Sqrt(AngriffDesGegners(team));

    // ── Zusammengefasst ───────────────────────────────────────────────────

    /// <summary>Faktor auf die Verwertungsquote des Angreifers (#431), getrennt nach Herkunft: die eigene Risikostufe
    /// und die des Verteidigers. Mit <see cref="Angriffsfaktor"/> multipliziert ergibt <see cref="Verwertung.Gesamt"/>
    /// die Wirkung der Risikoachse auf die Torerwartung.</summary>
    public static Verwertung Verwertungsfaktor(Teamzustand angreifer, Teamzustand verteidiger)
        => new(EigeneGuete(angreifer), GueteDesGegners(verteidiger));

    /// <param name="Eigene">Anteil der eigenen Risikostufe.</param>
    /// <param name="DurchGegner">Anteil des Verteidigers: über 1, wenn er aufmacht (bei der Brechstange das leere Tor).</param>
    public readonly record struct Verwertung(double Eigene, double DurchGegner)
    {
        public double Gesamt => Eigene * DurchGegner;
    }

    /// <summary>Faktor auf die Chancenrate des Angreifers in einer Minute (bis #431: auf die Torerwartung). Die Menge
    /// ist die Wirkung geteilt durch die Güte: E / (1/E) = E² und G / √G = √G.</summary>
    /// <param name="zeitfaktor">Wirkung des Zeitpunkts, aus <see cref="Zeitfaktor"/>.</param>
    public static double Angriffsfaktor(Teamzustand angreifer, Teamzustand verteidiger, double zeitfaktor)
    {
        double eigener = EigenerAngriff(angreifer);
        return eigener * eigener
               * Math.Sqrt(AngriffDesGegners(verteidiger))
               * Math.Pow(UNTERZAHL_EIGENER_ANGRIFF, angreifer.Platzverweise)
               * Math.Pow(UNTERZAHL_ANGRIFF_GEGNER, verteidiger.Platzverweise)
               * zeitfaktor;
    }

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
