using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Ausführliche Simulation Minute für Minute (#361). Ein Exemplar hält den Zustand eines Spiels: Spielstand des
/// laufenden Abschnitts, Risikostufe und Platzverweise je Team, gespielte Minuten (Ermüdung). Je Minute wird die
/// Torerwartung aus diesem Zustand neu berechnet (<see cref="Spielsituation"/>); Tore fallen nacheinander, ein
/// zweites Tor derselben Minute rechnet mit dem neuen Spielstand. Karten werden ebenfalls je Minute gezogen und
/// wirken ab der Folgeminute. Platzverweise und Ermüdung gelten bis zum Ende des Spiels, also auch in der
/// Verlängerung.
/// </summary>
internal sealed class Spielablauf
{
    // Die Nachspielzeit verlängert die Spielzeit um gut 6 % und die Spielsituation verschiebt die Torerwartung; die
    // Korrektur hält den ausführlichen Pfad bei den Kennzahlen aus #404 (SimulationsKalibrierungTests).
    private const double ABLAUF_KORREKTUR = 0.86;

    // Real ~2,5 gelbe und ~0,15 rote Karten je Team und Spiel; die Korrektur gleicht Nachspielzeit und
    // Kartenfaktor (im Mittel über gut 96 Minuten etwas über 1) aus.
    private const double GELBE_KARTEN_JE_90 = 2.5;
    private const double ROTE_KARTEN_JE_90  = 0.15;
    private const double KARTEN_KORREKTUR   = 0.9;
    // Ab dem fünften Platzverweis würde ein Spiel abgebrochen – so weit lässt die Simulation es nicht kommen.
    private const int MAX_PLATZVERWEISE = 4;
    // Nur eine Sicherung gegen Endlosschleifen; mehr als zwei Tore in einer Minute kommen praktisch nicht vor.
    private const int MAX_TORE_JE_MINUTE = 5;

    private const int ENDE_REGULAER           = 90;
    private const int ENDE_VERLAENGERUNG      = 120;
    private const int ENDE_ERSTE_VERL_HAELFTE = 105;

    private readonly Random _rng;
    private readonly double _heimJeMinute;
    private readonly double _gastJeMinute;
    private readonly List<SimuliertesEreignis> _ereignisse = [];
    private Teamzustand _heim;
    private Teamzustand _gast;
    private int _heimTore;
    private int _gastTore;
    private int _gespielteMinuten;

    /// <param name="platzverweise">Platzverweise, mit denen das Spiel weitergeht (Verlängerung nach dem Rückspiel).</param>
    /// <param name="gespielteMinuten">Bereits gespielte Minuten – bestimmt die Ermüdung.</param>
    public Spielablauf(
        double heimTorerwartung, double gastTorerwartung, Random rng,
        Platzverweise platzverweise = default, int gespielteMinuten = 0)
    {
        _rng              = rng;
        _heimJeMinute     = heimTorerwartung / ENDE_REGULAER * ABLAUF_KORREKTUR;
        _gastJeMinute     = gastTorerwartung / ENDE_REGULAER * ABLAUF_KORREKTUR;
        _heim             = Teamzustand.Anpfiff(platzverweise.Heim);
        _gast             = Teamzustand.Anpfiff(platzverweise.Gast);
        _gespielteMinuten = gespielteMinuten;
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

        int nachspielzeit = _rng.Next(minNachspiel, maxNachspiel + 1);
        for (int nachspielminute = 1; nachspielminute <= nachspielzeit; nachspielminute++)
            if (MinuteSpielen(bis, nachspielminute, abschnittsende - bis, goldenGoal))
                return true;

        return false;
    }

    // ── Eine Minute ───────────────────────────────────────────────────────

    private bool MinuteSpielen(int minute, int? nachspielminute, int verbleibend, bool goldenGoal)
    {
        _gespielteMinuten++;
        double ermuedung = Spielsituation.Ermuedung(_gespielteMinuten);
        TaktikAnpassen(minute, nachspielminute, verbleibend);

        for (int tor = 0; tor < MAX_TORE_JE_MINUTE && TorFaellt(ermuedung, out bool heimTrifft); tor++)
        {
            TorErzielen(minute, nachspielminute, heimTrifft, goldenGoal);
            if (goldenGoal) return true;
            TaktikAnpassen(minute, nachspielminute, verbleibend);
        }

        KartenZiehen(minute, nachspielminute, ermuedung);
        return false;
    }

    /// <summary>Erst, ob überhaupt ein Tor fällt (gemeinsame Torerwartung beider Teams), dann wer trifft (Anteil an
    /// der Torerwartung). Bis #361 wurde je Team einzeln gewürfelt, und beim Golden Goal entschied ein 50:50-Los,
    /// wenn beide trafen – das benachteiligte das stärkere Team.</summary>
    private bool TorFaellt(double ermuedung, out bool heimTrifft)
    {
        double heim = _heimJeMinute * Spielsituation.Angriffsfaktor(_heim, _gast, ermuedung);
        double gast = _gastJeMinute * Spielsituation.Angriffsfaktor(_gast, _heim, ermuedung);
        double gesamt = heim + gast;
        heimTrifft = false;
        if (_rng.NextDouble() >= 1 - Math.Exp(-gesamt))
            return false;

        heimTrifft = _rng.NextDouble() < heim / gesamt;
        return true;
    }

    private void TorErzielen(int minute, int? nachspielminute, bool heimTrifft, bool goldenGoal)
    {
        if (heimTrifft) _heimTore++;
        else _gastTore++;

        if (goldenGoal)
        {
            _ereignisse.Add(new(minute, EreignisArt.GoldenGoal, heimTrifft, "Golden Goal", nachspielminute));
            return;
        }

        var gegner = heimTrifft ? _gast : _heim;
        string text = Torbeschreibung.Zufaellig(_rng, gegner.Stufe == Risikostufe.Brechstange);
        _ereignisse.Add(new(minute, EreignisArt.Tor, heimTrifft, text, nachspielminute));
    }

    /// <summary>Passt die Risikostufe beider Teams an Spielstand und Zeit an; jeder Wechsel ist ein Ereignis.</summary>
    private void TaktikAnpassen(int minute, int? nachspielminute, int verbleibend)
    {
        _heim = TaktikDesTeams(_heim, _heimTore - _gastTore, istHeim: true, minute, nachspielminute, verbleibend);
        _gast = TaktikDesTeams(_gast, _gastTore - _heimTore, istHeim: false, minute, nachspielminute, verbleibend);
    }

#pragma warning disable S107 // Team, Stand und Zeitpunkt; die Zeitangaben einzeln, wie sie das Ereignis braucht
    private Teamzustand TaktikDesTeams(
        Teamzustand team, int vorsprung, bool istHeim, int minute, int? nachspielminute, int verbleibend)
#pragma warning restore S107
    {
        var stufe = Spielsituation.RisikostufeErmitteln(vorsprung, verbleibend);
        if (stufe == team.Stufe)
            return team;

        _ereignisse.Add(new(minute, EreignisArt.Taktikwechsel, istHeim, Spielsituation.Taktiktext(stufe), nachspielminute));
        return team with { Stufe = stufe };
    }

    /// <summary>Gelbe und rote Karten je Team; mit der Ermüdung steigt die Fehlerquote. Ein Platzverweis wirkt ab
    /// der Folgeminute, weil die Tore dieser Minute schon gefallen sind.</summary>
    private void KartenZiehen(int minute, int? nachspielminute, double ermuedung)
    {
        double faktor = Spielsituation.Kartenfaktor(ermuedung) * KARTEN_KORREKTUR;
        _heim = KartenDesTeams(_heim, istHeim: true, minute, nachspielminute, faktor);
        _gast = KartenDesTeams(_gast, istHeim: false, minute, nachspielminute, faktor);
    }

    private Teamzustand KartenDesTeams(Teamzustand team, bool istHeim, int minute, int? nachspielminute, double faktor)
    {
        if (_rng.NextDouble() < GELBE_KARTEN_JE_90 / ENDE_REGULAER * faktor)
            _ereignisse.Add(new(minute, EreignisArt.GelbeKarte, istHeim, "Gelbe Karte", nachspielminute));

        if (_rng.NextDouble() >= ROTE_KARTEN_JE_90 / ENDE_REGULAER * faktor || team.Platzverweise >= MAX_PLATZVERWEISE)
            return team;

        _ereignisse.Add(new(minute, EreignisArt.RoteKarte, istHeim, "Rote Karte", nachspielminute));
        return team with { Platzverweise = team.Platzverweise + 1 };
    }
}
