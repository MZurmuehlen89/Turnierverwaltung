namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Baut die Zufallsfolge für den minutenweisen Spielablauf (#361) lesbar auf. Je Minute zieht der Ablauf: ob ein Tor
/// fällt, bei einem Tor wer trifft und die Torbeschreibung, dann erneut ob ein Tor fällt (ein weiteres Tor derselben
/// Minute), danach vier Karten (Gelb Heim, Rot Heim, Gelb Gast, Rot Gast). Nach der letzten regulären Minute einer
/// Halbzeit wird die Nachspielzeit gezogen. <see cref="NEIN"/> als Folgewert danach heißt: kein Tor, keine Karte,
/// längste Nachspielzeit. Steht ein Taktikwechsel an, den nur die Zeit fällig macht (#673), zieht der Ablauf zu
/// Beginn der Minute je betroffenem Team (erst Heim, dann Gast) einen Wurf, ob umgestellt wird; <see cref="NEIN"/>
/// heißt „noch nicht“ – spätestens sieben Minuten nach der Schwelle stellt das Team auch dann um.
/// </summary>
internal sealed class Ablauffolge
{
    public const double JA   = 0.0;
    public const double NEIN = 0.9999;

    private readonly List<double> _werte = [];

    public Ablauffolge KeinTor() => Hinzu(NEIN);

    /// <summary>Ein Tor mit Beschreibung „Tor per Kopfball (oben links)“; JA beim Trefferwurf heißt Heimteam.</summary>
    public Ablauffolge Tor(bool heim) => Hinzu(JA, heim ? JA : NEIN).Hinzu(SimulationsEngineTestdaten.TorstilOhneKonter);

    /// <summary>Golden Goal: ohne Torbeschreibung, danach ist das Spiel vorbei.</summary>
    public Ablauffolge GoldenGoal(bool heim) => Hinzu(JA, heim ? JA : NEIN);

    public Ablauffolge Karten(bool gelbHeim = false, bool rotHeim = false, bool gelbGast = false, bool rotGast = false)
        => Hinzu(gelbHeim ? JA : NEIN, rotHeim ? JA : NEIN, gelbGast ? JA : NEIN, rotGast ? JA : NEIN);

    /// <summary>Wurf, ob ein Team einen fälligen Taktikwechsel vollzieht (#673): <see cref="JA"/> sofort.</summary>
    public Ablauffolge Taktikwurf(double wert = NEIN) => Hinzu(wert);

    /// <summary>Ruhige Minuten, in denen ein Team – oder mit <paramref name="teams"/> = 2 beide – einen fälligen
    /// Taktikwechsel noch aufschiebt.</summary>
    public Ablauffolge RuhigMitTaktikwurf(int minuten, int teams = 1)
    {
        for (int i = 0; i < minuten; i++)
        {
            for (int team = 0; team < teams; team++)
                Taktikwurf();
            Ruhig(1);
        }
        return this;
    }

    /// <summary>Minuten ohne Tor und ohne Karte.</summary>
    public Ablauffolge Ruhig(int minuten)
    {
        for (int i = 0; i < minuten; i++)
            KeinTor().Karten();
        return this;
    }

    /// <summary>Eine Minute mit genau diesem Tor.</summary>
    public Ablauffolge MinuteMitTor(bool heim) => Tor(heim).KeinTor().Karten();

    /// <summary>Wert für die Nachspielzeit: <see cref="JA"/> = kürzeste, <see cref="NEIN"/> = längste.</summary>
    public Ablauffolge Nachspielzeit(double wert) => Hinzu(wert);

    public Ablauffolge Hinzu(params double[] werte)
    {
        _werte.AddRange(werte);
        return this;
    }

    public VorgegebenerZufall Zufall(double? danach = NEIN) => new(_werte, danach);

    /// <summary>Die Folge als Anfang eines Spiels, danach zieht <paramref name="danach"/> weiter (#673).</summary>
    public VorgegebenerAnfang Anfang(Random danach) => new(_werte, danach);
}
