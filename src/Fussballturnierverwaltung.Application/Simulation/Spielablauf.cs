using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Ausführliche Simulation Minute für Minute (#361). Ein Exemplar hält den Zustand eines Spiels: Spielstand des
/// laufenden Abschnitts, Risikostufe und Platzverweise je Team, gespielte Minuten (Ermüdung). Je Minute wird die
/// Chancenrate aus diesem Zustand neu berechnet (<see cref="Spielsituation"/>, dazu Spielstand und Spielkontrolle seit
/// #674), jede Chance hat eine Art
/// (<see cref="Chancenmodell"/>) und wird mit deren Quote zum Tor (#431); Chancen fallen nacheinander, eine zweite
/// Chance derselben Minute rechnet mit dem neuen Spielstand. Karten werden ebenfalls je Minute gezogen und
/// wirken ab der Folgeminute. Platzverweise und Ermüdung gelten bis zum Ende des Spiels, also auch in der
/// Verlängerung.
/// </summary>
internal sealed class Spielablauf
{
    // Die Nachspielzeit verlängert die Spielzeit um gut 6 % und die Spielsituation verschiebt die Torerwartung; die
    // Korrektur hält den ausführlichen Pfad bei den Kennzahlen aus #404 (SimulationsKalibrierungTests). Seit #674 von
    // 0,872 auf 0,882: Spielstand und Rückzug senken die Tore im Mittel um gut 1 %, der Wert hält 3,03 Tore je Spiel.
    // Er ist an die Realismus-Stichprobe gebunden – Messreihe über 10 Seeds in #674 (1:0 + 0:1 und ≥ 7 Tore liegen
    // beide am Bandrand, jede Verschiebung kippt eine der beiden).
    private const double ABLAUF_KORREKTUR = 0.882;

    // Real 1,88 gelbe und 0,073 rote Karten je Team und Spiel (Bundesliga 2015/16–2024/25, #678); bis #682 stand hier
    // der Je-Spiel-Wert ~0,15 als Je-Team-Wert. Die Korrektur gleicht Nachspielzeit und Kartenfaktor (im Mittel über
    // gut 96 Minuten etwas über 1) aus; der Rotfaktor nach Spielstand ändert die Häufigkeit nicht.
    private const double GELBE_KARTEN_JE_90 = 1.88;
    private const double ROTE_KARTEN_JE_90  = 0.075;
    private const double KARTEN_KORREKTUR   = 0.9;
    // Ab dem fünften Platzverweis würde ein Spiel abgebrochen – so weit lässt die Simulation es nicht kommen.
    private const int MAX_PLATZVERWEISE = 4;
    // Nur eine Sicherung gegen Endlosschleifen; mehr als zwei Chancen in einer Minute kommen praktisch nicht vor.
    private const int MAX_CHANCEN_JE_MINUTE = 8;

    private static readonly string[] _grosschanceVergeben =
        ["Großchance vergeben – vom Torhüter pariert", "Großchance vergeben – Pfosten", "Großchance vergeben – knapp vorbei"];
    private static readonly string[] _elfmeterVergeben = ["Elfmeter gehalten", "Elfmeter verschossen"];

    private const int ENDE_REGULAER           = 90;
    private const int ENDE_VERLAENGERUNG      = 120;
    private const int ENDE_ERSTE_VERL_HAELFTE = 105;

    private readonly Random _rng;
    private readonly double _heimChancenJeMinute;
    private readonly double _gastChancenJeMinute;
    // Verwertung der Teams aus den Ratings (#431, E2).
    private readonly double _heimVerwertung;
    private readonly double _gastVerwertung;
    // Kräfteverhältnis für die Risikoentscheidung (#673): Anteil des Heimteams an der Torerwartung beider Teams.
    private readonly double _heimAnteil;
    private readonly Hinspielstand _hinspielstand;
    // Grundausrichtung und Flexibilität beider Teams (#667).
    private readonly Teamprofile _profile;
    private Spielkontrolle? _kontrolle;
    private readonly List<SimuliertesEreignis> _ereignisse = [];
    private Teamzustand _heim;
    private Teamzustand _gast;
    private int _heimTore;
    private int _gastTore;
    private int _gespielteMinuten;

    /// <param name="platzverweise">Platzverweise, mit denen das Spiel weitergeht (Verlängerung nach dem Rückspiel).</param>
    /// <param name="gespielteMinuten">Bereits gespielte Minuten – bestimmt die Ermüdung.</param>
    /// <param name="hinspielstand">Im K.-o.-Rückspiel das Hinspiel – die Risikostufe richtet sich dann nach dem
    /// Gesamtstand (#671).</param>
    /// <param name="profile">Spielweise beider Teams (#667): Startpunkt, Umstellzeitpunkte und -tempo.</param>
#pragma warning disable S107 // vier Angaben zum Spiel, drei zum Stand, mit dem es beginnt; nur die Paarung ruft auf
    public Spielablauf(
        Chancenprofil heim, Chancenprofil gast, Random rng,
        Platzverweise platzverweise = default, int gespielteMinuten = 0, Hinspielstand hinspielstand = default,
        Teamprofile profile = default)
#pragma warning restore S107
    {
        _rng              = rng;
        _heimChancenJeMinute = heim.ChancenJe90 / ENDE_REGULAER * ABLAUF_KORREKTUR;
        _gastChancenJeMinute = gast.ChancenJe90 / ENDE_REGULAER * ABLAUF_KORREKTUR;
        _heimVerwertung   = heim.Verwertung;
        _gastVerwertung   = gast.Verwertung;
        _heimAnteil       = heim.Torerwartung + gast.Torerwartung > 0
            ? heim.Torerwartung / (heim.Torerwartung + gast.Torerwartung)
            : 0.5;
        _heim             = Teamzustand.Anpfiff(platzverweise.Heim, profile.Heim.Startpunkt);
        _gast             = Teamzustand.Anpfiff(platzverweise.Gast, profile.Gast.Startpunkt);
        _gespielteMinuten = gespielteMinuten;
        _hinspielstand    = hinspielstand;
        _profile          = profile;
    }

    /// <summary>Tore und Ereignisse eines Abschnitts (reguläre Spielzeit, Verlängerung, …).</summary>
    public readonly record struct Abschnitt(int HeimTore, int GastTore, IReadOnlyList<SimuliertesEreignis> Ereignisse);

    // ── Abschnitte ────────────────────────────────────────────────────────

    public Abschnitt RegulaereSpielzeit() => AbschnittSpielen(() =>
    {
        Halbzeit(1, 45, minNachspiel: 1, maxNachspiel: 3, ENDE_REGULAER, goldenGoal: false);
        Halbzeit(46, 90, minNachspiel: 2, maxNachspiel: 6, ENDE_REGULAER, goldenGoal: false);
    });

    public Abschnitt Verlaengerung() => AbschnittSpielen(() =>
    {
        Halbzeit(91, 105, minNachspiel: 0, maxNachspiel: 2, ENDE_VERLAENGERUNG, goldenGoal: false);
        Halbzeit(106, 120, minNachspiel: 0, maxNachspiel: 2, ENDE_VERLAENGERUNG, goldenGoal: false);
    });

    /// <summary>Verlängerung bis zum ersten Tor; liefert höchstens ein Tor.</summary>
    public Abschnitt GoldenGoal() => AbschnittSpielen(() =>
    {
        if (!Halbzeit(91, 105, minNachspiel: 0, maxNachspiel: 2, ENDE_VERLAENGERUNG, goldenGoal: true))
            Halbzeit(106, 120, minNachspiel: 0, maxNachspiel: 2, ENDE_VERLAENGERUNG, goldenGoal: true);
    });

    /// <summary>Erste Hälfte der Verlängerung beim Silver Goal – führt danach ein Team, endet das Spiel.</summary>
    public Abschnitt ErsteVerlaengerungshaelfte() => AbschnittSpielen(() =>
        Halbzeit(91, 105, minNachspiel: 0, maxNachspiel: 2, ENDE_ERSTE_VERL_HAELFTE, goldenGoal: false));

    public Abschnitt ZweiteVerlaengerungshaelfte() => AbschnittSpielen(() =>
        Halbzeit(106, 120, minNachspiel: 0, maxNachspiel: 2, ENDE_VERLAENGERUNG, goldenGoal: false));

    /// <summary>Spielt einen Abschnitt und gibt dessen Tore und Ereignisse zurück. Der Spielstand für die
    /// Risikostufe läuft über alle Abschnitte weiter – eine Verlängerung folgt nur auf Gleichstand. Nach einem
    /// Rückspiel, in dem nur das Gesamtergebnis ausgeglichen war, beginnt ein eigener Ablauf bei 0:0.</summary>
    private Abschnitt AbschnittSpielen(Action spielen)
    {
        int heimVorher = _heimTore, gastVorher = _gastTore, ereignisseVorher = _ereignisse.Count;
        spielen();
        return new(_heimTore - heimVorher, _gastTore - gastVorher, _ereignisse[ereignisseVorher..]);
    }

    /// <summary>Spielt die Minuten <paramref name="von"/> bis <paramref name="bis"/> und die danach ausgelosten
    /// Minuten der Nachspielzeit. Liefert true, sobald ein Golden Goal fällt.</summary>
#pragma warning disable S107 // sechs Angaben einer Halbzeit; ein eigener Typ dafür hätte nur hier einen Nutzer
    private bool Halbzeit(int von, int bis, int minNachspiel, int maxNachspiel, int abschnittsende, bool goldenGoal)
#pragma warning restore S107
    {
        for (int minute = von; minute <= bis; minute++)
            if (MinuteSpielen(minute, null, abschnittsende - minute, goldenGoal))
                return true;

        // Am Ende des Abschnitts läuft die Restzeit in der Nachspielzeit negativ weiter – ein fälliger Wechsel wird dort
        // immer wahrscheinlicher. Vor einer weiteren Hälfte bleibt sie stehen; sonst stiege sie mit deren erster Minute
        // wieder, und eine gerade fällige Stufe wäre es nicht mehr (Befund aus dem KI-Review, #673).
        int nachspielzeit = _rng.Next(minNachspiel, maxNachspiel + 1);
        for (int nachspielminute = 1; nachspielminute <= nachspielzeit; nachspielminute++)
        {
            int verbleibend = abschnittsende - bis - (bis == abschnittsende ? nachspielminute : 0);
            if (MinuteSpielen(bis, nachspielminute, verbleibend, goldenGoal))
                return true;
        }

        return false;
    }

    // ── Eine Minute ───────────────────────────────────────────────────────

    private bool MinuteSpielen(int minute, int? nachspielminute, int verbleibend, bool goldenGoal)
    {
        _gespielteMinuten++;
        Kontrolle.Fortschreiben();
        double ermuedung = Spielsituation.Ermuedung(_gespielteMinuten);
        double zeitfaktor = Spielsituation.Zeitfaktor(minute, nachspielminute != null, ermuedung);
        TaktikAnpassen(minute, nachspielminute, verbleibend, nachTor: false);

        for (int chance = 0; chance < MAX_CHANCEN_JE_MINUTE && ChanceEntsteht(zeitfaktor, minute > ENDE_REGULAER, out bool heimAngriff); chance++)
        {
            if (ChanceSpielen(minute, nachspielminute, heimAngriff, goldenGoal))
            {
                if (goldenGoal) return true;
                TaktikAnpassen(minute, nachspielminute, verbleibend, nachTor: true);
            }
        }

        KartenZiehen(minute, nachspielminute, ermuedung);
        return false;
    }

    /// <summary>Spielkontrolle (#674) mit eigenem Zufallsstrom, damit die Würfe je Minute unverändert bleiben. Den
    /// Startwert zieht sie in der ersten gespielten Minute aus dem Zufall des Spiels – reproduzierbar mit dem Seed; ein
    /// Ablauf, der nur ins Elfmeterschießen führt, zieht ihn nie. Abgeleitet, keine eigene Zufallsquelle (#519).</summary>
    private Spielkontrolle Kontrolle => _kontrolle ??= new Spielkontrolle(
        _rng is IZufallsabzweigung abzweigung ? abzweigung.Abzweigen() : new Random(_rng.Next(int.MaxValue)));

    /// <summary>Erst, ob überhaupt eine Chance entsteht (gemeinsame Chancenrate beider Teams), dann wessen (Anteil an
    /// der Chancenrate). Die Wahrscheinlichkeit r / (1 + r) macht die erwartete Zahl Chancen der Schleife je Minute
    /// genau gleich r. Spielstand und Spielkontrolle (#674) wirken auf die Menge, nicht auf die Güte der Chancen; der
    /// Spielstand zählt wie bei der Taktik im Rückspiel samt Hinspiel.</summary>
    private bool ChanceEntsteht(double zeitfaktor, bool verlaengerung, out bool heimAngriff)
    {
        bool toreGefallen = _heimTore + _gastTore + _hinspielstand.ToreHeim + _hinspielstand.ToreGast > 0;
        double spielstand = Spielsituation.Spielstandfaktor(
            _hinspielstand.VorsprungDesHeimteams(_heimTore, _gastTore), toreGefallen, verlaengerung);
        double heim = _heimChancenJeMinute * Spielsituation.Angriffsfaktor(_heim, _gast, zeitfaktor) * spielstand * Kontrolle.HeimFaktor;
        double gast = _gastChancenJeMinute * Spielsituation.Angriffsfaktor(_gast, _heim, zeitfaktor) * spielstand * Kontrolle.GastFaktor;
        double gesamt = heim + gast;
        heimAngriff = false;
        if (_rng.NextDouble() >= gesamt / (1 + gesamt))
            return false;

        heimAngriff = _rng.NextDouble() < heim / gesamt;
        return true;
    }

    /// <summary>Art der Chance, dann ob sie ein Tor wird. Liefert true bei einem Tor; eine vergebene Großchance oder
    /// ein vergebener Elfmeter wird ein Ereignis, eine vergebene normale Chance nicht. Steht der Torhüter des
    /// Verteidigers mit vorn, geht ins leere Tor, was nur dessen offene Abwehr zum Tor macht – der Teil der Quote, den
    /// <see cref="Spielsituation.Verwertung.DurchGegner"/> hinzufügt (bis #431 ein fester Anteil in der
    /// <see cref="Torbeschreibung"/>). Ein Elfmeter hängt nicht von der Abwehr ab.</summary>
    private bool ChanceSpielen(int minute, int? nachspielminute, bool heimAngriff, bool goldenGoal)
    {
        var art = Chancenmodell.Art(_rng.NextDouble());
        var (angreifer, verteidiger) = heimAngriff ? (_heim, _gast) : (_gast, _heim);
        double teamVerwertung = heimAngriff ? _heimVerwertung : _gastVerwertung;
        var faktor = Spielsituation.Verwertungsfaktor(angreifer, verteidiger);

        double wurf = _rng.NextDouble();
        if (wurf < Chancenmodell.Quote(art, teamVerwertung * faktor.Gesamt))
        {
            bool insLeereTor = verteidiger.Stufe == Risikostufe.Brechstange
                               && wurf >= Chancenmodell.Quote(art, teamVerwertung * faktor.Eigene);
            TorErzielen(minute, nachspielminute, heimAngriff, goldenGoal, art, insLeereTor);
            return true;
        }

        if (art != Chancenart.Normal)
            ChanceVergeben(minute, nachspielminute, heimAngriff, art);
        return false;
    }

    private void ChanceVergeben(int minute, int? nachspielminute, bool heimAngriff, Chancenart art)
    {
        string[] texte = art == Chancenart.Elfmeter ? _elfmeterVergeben : _grosschanceVergeben;
        _ereignisse.Add(new(minute, EreignisArt.Grosschance, heimAngriff, texte[_rng.Next(texte.Length)], nachspielminute, art));
    }

#pragma warning disable S107 // Zeitpunkt, Team und die Angaben zur Chance, aus der das Tor fällt
    private void TorErzielen(int minute, int? nachspielminute, bool heimTrifft, bool goldenGoal, Chancenart art, bool insLeereTor)
#pragma warning restore S107
    {
        if (heimTrifft) _heimTore++;
        else _gastTore++;

        if (goldenGoal)
        {
            _ereignisse.Add(new(minute, EreignisArt.GoldenGoal, heimTrifft, "Golden Goal", nachspielminute, art));
            return;
        }

        string text = Torbeschreibung.Zufaellig(_rng, insLeereTor, art);
        _ereignisse.Add(new(minute, EreignisArt.Tor, heimTrifft, text, nachspielminute, art));
    }

    /// <summary>Passt die Risikostufe beider Teams an Spielstand und Zeit an (#673); jeder Wechsel ist ein Ereignis.
    /// Im K.-o.-Rückspiel zählt der Gesamtstand samt Auswärtstorregel (#671). Nach einem Tor stellen beide Teams
    /// sofort um; einen Wechsel, den nur die Zeit fällig macht, vollzieht ein Team mit steigender Wahrscheinlichkeit
    /// – gewürfelt wird nur, wenn einer ansteht.</summary>
    private void TaktikAnpassen(int minute, int? nachspielminute, int verbleibend, bool nachTor)
    {
        int heimVorsprung = _hinspielstand.VorsprungDesHeimteams(_heimTore, _gastTore);
        var heimLage = new Spiellage(
            heimVorsprung, verbleibend, _heimAnteil, _heim.Platzverweise - _gast.Platzverweise, _profile.Heim);
        var gastLage = new Spiellage(
            -heimVorsprung, verbleibend, 1 - _heimAnteil, _gast.Platzverweise - _heim.Platzverweise, _profile.Gast);
        _heim = TaktikDesTeams(_heim, heimLage, istHeim: true, minute, nachspielminute, nachTor);
        _gast = TaktikDesTeams(_gast, gastLage, istHeim: false, minute, nachspielminute, nachTor);
    }

#pragma warning disable S107 // Team, Lage und Zeitpunkt; die Zeitangaben einzeln, wie sie das Ereignis braucht
    private Teamzustand TaktikDesTeams(
        Teamzustand team, Spiellage lage, bool istHeim, int minute, int? nachspielminute, bool nachTor)
#pragma warning restore S107
    {
        var faellig = Risikoentscheidung.Faellig(lage);
        if (faellig.Stufe == team.Stufe)
            return team;
        if (!nachTor && !ZeitMachtWechselFaellig(faellig, lage))
            return team;

        _ereignisse.Add(new(minute, EreignisArt.Taktikwechsel, istHeim, Spielsituation.Taktiktext(faellig.Stufe, lage.Vorsprung), nachspielminute));
        return team.Umstellen(faellig.Stufe, lage.Profil);
    }

    /// <summary>Würfelt, ob ein Team einen Wechsel vollzieht, den nur die Zeit fällig macht; ohne Schwelle (zurück zur
    /// gewohnten Ordnung) wird nicht gewürfelt.</summary>
    private bool ZeitMachtWechselFaellig(FaelligeStufe faellig, Spiellage lage)
    {
        double wahrscheinlichkeit = Risikoentscheidung.Umstellwahrscheinlichkeit(faellig, lage.Verbleibend, lage.Profil.Umstellfenster);
        return wahrscheinlichkeit > 0 && _rng.NextDouble() < wahrscheinlichkeit;
    }

    /// <summary>Gelbe und rote Karten je Team; mit der Ermüdung steigt die Fehlerquote. Ein Platzverweis wirkt ab
    /// der Folgeminute, weil die Tore dieser Minute schon gefallen sind.</summary>
    private void KartenZiehen(int minute, int? nachspielminute, double ermuedung)
    {
        double faktor = Spielsituation.Kartenfaktor(ermuedung) * KARTEN_KORREKTUR;
        // Wie bei der Taktik zählt der Gesamtstand inklusive Hinspiel: Wer im Rückspiel aggregiert knapp hinten liegt,
        // gerät ebenso unter Druck (die Kalibrierung #682 läuft ohne Hinspiel, dort ist es der reine Spielstand).
        int heimVorsprung = _hinspielstand.VorsprungDesHeimteams(_heimTore, _gastTore);
        _heim = KartenDesTeams(_heim, istHeim: true, minute, nachspielminute, faktor, heimVorsprung);
        _gast = KartenDesTeams(_gast, istHeim: false, minute, nachspielminute, faktor, -heimVorsprung);
    }

    private Teamzustand KartenDesTeams(Teamzustand team, bool istHeim, int minute, int? nachspielminute, double faktor, int vorsprung)
    {
        if (_rng.NextDouble() < GELBE_KARTEN_JE_90 / ENDE_REGULAER * faktor)
            _ereignisse.Add(new(minute, EreignisArt.GelbeKarte, istHeim, "Gelbe Karte", nachspielminute));

        if (_rng.NextDouble() >= ROTE_KARTEN_JE_90 / ENDE_REGULAER * faktor * Spielsituation.Rotfaktor(vorsprung) || team.Platzverweise >= MAX_PLATZVERWEISE)
            return team;

        _ereignisse.Add(new(minute, EreignisArt.RoteKarte, istHeim, "Rote Karte", nachspielminute));
        return team with { Platzverweise = team.Platzverweise + 1 };
    }
}
