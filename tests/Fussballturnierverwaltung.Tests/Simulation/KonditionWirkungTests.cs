using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Tests.Simulation.Realismus;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Messwerte der Stichprobe: ein starkes gegen ein schwaches Team gleicher Ratings, je zur Hälfte daheim.</summary>
public sealed class KonditionStichprobe
{
    public const int SEED = 677;
    public const int SPIELE = 30_000;

    /// <summary>Tore und gelbe Karten eines Teams nach Phase.</summary>
    public sealed class Zaehler
    {
        public int ToreErsteHaelfte { get; set; }
        public int ToreSchluss { get; set; }
        public int ToreVerlaengerung { get; set; }
        public int GelbZweiteHaelfte { get; set; }
        public int Siege { get; set; }
    }

    public Zaehler Stark { get; } = new();
    public Zaehler Schwach { get; } = new();

    public KonditionStichprobe()
    {
        if (Mutationslauf.Aktiv) return;

        var stark = Team(1, Kondition.Stark);
        var schwach = Team(2, Kondition.Schwach);
        var verfahren = new[] { new Domain.Entitaeten.Regelwerke.EntscheidungsVerfahren { VerfahrenArt = VerfahrenArt.VerlaengerungKlassisch, Reihenfolge = 1 } };
        var rng = new Random(SEED);
        for (int i = 0; i < SPIELE; i++)
        {
            bool starkDaheim = i % 2 == 0;
            var ergebnis = starkDaheim
                ? SimulationsEngine.Simulieren(stark, schwach, verfahren, ausfuehrlich: true, rng, Spielortvorteil.Neutral)
                : SimulationsEngine.Simulieren(schwach, stark, verfahren, ausfuehrlich: true, rng, Spielortvorteil.Neutral);
            Auswerten(ergebnis, starkDaheim);
        }
    }

    private static Mannschaft Team(int id, Kondition kondition) => new()
    {
        Id = id, Name = $"Team {id}", Kurzname = $"T{id}", OffensivRating = 75, MittelfeldRating = 75, DefensivRating = 75,
        Heimstaerke = 75, Auswaertsstaerke = 75, Kondition = kondition
    };

    private void Auswerten(SimulationsErgebnis e, bool starkDaheim)
    {
        foreach (var x in e.Ereignisse)
            Zaehlen(x.IstHeim == starkDaheim ? Stark : Schwach, x);

        int starkTore = starkDaheim ? e.HeimToreRegulaer : e.AuswaertsToreRegulaer;
        int schwachTore = starkDaheim ? e.AuswaertsToreRegulaer : e.HeimToreRegulaer;
        if (starkTore > schwachTore) Stark.Siege++;
        else if (schwachTore > starkTore) Schwach.Siege++;
    }

    private static void Zaehlen(Zaehler team, SimuliertesEreignis x)
    {
        if (x.Art == EreignisArt.GelbeKarte)
        {
            if (x.Minute is > 45 and <= 90) team.GelbZweiteHaelfte++;
            return;
        }

        if (x.Art != EreignisArt.Tor) return;
        if (x.Minute <= 45) team.ToreErsteHaelfte++;
        else if (x.Minute is >= 76 and <= 90) team.ToreSchluss++;
        else if (x.Minute > 90) team.ToreVerlaengerung++;
    }
}

/// <summary>#677, K3–K6: Wirkung der Kondition auf Tore und Karten. A und B in <see cref="Spielsituation"/> sind daran
/// kalibriert.</summary>
public class KonditionWirkungTests(KonditionStichprobe s) : IClassFixture<KonditionStichprobe>
{
    private const string SKIP_MUTATION = "Im Mutationslauf zu langsam (#677)";

    private static void Bericht(string zeile) => TestContext.Current.TestOutputHelper?.WriteLine(zeile);

    private static double Anteil(int stark, int schwach) => (double)stark / (stark + schwach);

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Schlussphase_StarkesTeamTrifftInDerSchlussphaseMehr_NichtInDerErstenHaelfte()
    {
        double erste = Anteil(s.Stark.ToreErsteHaelfte, s.Schwach.ToreErsteHaelfte);
        double schluss = Anteil(s.Stark.ToreSchluss, s.Schwach.ToreSchluss);
        Bericht($"Torenanteil stark: Minute 1–45 {erste:P1} (Band 48–52 %), Minute 76–90 {schluss:P1} (Band 55–62 %)");
        erste.Should().BeInRange(0.48, 0.52);
        schluss.Should().BeInRange(0.55, 0.62);
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Verlaengerung_StarkesTeamTrifftAmMeisten()
    {
        double anteil = Anteil(s.Stark.ToreVerlaengerung, s.Schwach.ToreVerlaengerung);
        Bericht($"Torenanteil stark in der Verlängerung: {anteil:P1} (Band 57–66 %), Tore {s.Stark.ToreVerlaengerung + s.Schwach.ToreVerlaengerung}");
        anteil.Should().BeInRange(0.57, 0.66);
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Gesamtwirkung_SiegquoteStarkMinusSchwach_Moderat()
    {
        double differenz = (double)(s.Stark.Siege - s.Schwach.Siege) / KonditionStichprobe.SPIELE;
        Bericht($"Siegquote stark − schwach: {differenz * 100:F1} Punkte (Band +2 … +7)");
        (differenz * 100).Should().BeInRange(2, 7);
    }

    [Fact(Skip = SKIP_MUTATION, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType = typeof(Mutationslauf))]
    public void Karten_SchwachesTeamSiehtInDerZweitenHaelfteMehrGelb()
    {
        double verhaeltnis = (double)s.Schwach.GelbZweiteHaelfte / s.Stark.GelbZweiteHaelfte;
        Bericht($"Gelbe Karten 2. Halbzeit schwach/stark: {verhaeltnis:F2} (mindestens 1,1)");
        verhaeltnis.Should().BeGreaterThanOrEqualTo(1.1);
    }
}
