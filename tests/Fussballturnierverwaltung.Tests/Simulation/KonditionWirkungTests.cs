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

    public int StarkToreErsteHaelfte, SchwachToreErsteHaelfte, StarkToreSchluss, SchwachToreSchluss;
    public int StarkToreVerlaengerung, SchwachToreVerlaengerung, StarkSiege, SchwachSiege;
    public int StarkGelbZweiteHaelfte, SchwachGelbZweiteHaelfte;

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
        {
            bool istStark = x.IstHeim == starkDaheim;
            switch (x.Art)
            {
                case EreignisArt.Tor when x.Minute <= 45:
                    if (istStark) StarkToreErsteHaelfte++; else SchwachToreErsteHaelfte++;
                    break;
                case EreignisArt.Tor when x.Minute is >= 76 and <= 90:
                    if (istStark) StarkToreSchluss++; else SchwachToreSchluss++;
                    break;
                case EreignisArt.Tor when x.Minute > 90:
                    if (istStark) StarkToreVerlaengerung++; else SchwachToreVerlaengerung++;
                    break;
                case EreignisArt.GelbeKarte when x.Minute is > 45 and <= 90:
                    if (istStark) StarkGelbZweiteHaelfte++; else SchwachGelbZweiteHaelfte++;
                    break;
            }
        }

        int starkTore = starkDaheim ? e.HeimToreRegulaer : e.AuswaertsToreRegulaer;
        int schwachTore = starkDaheim ? e.AuswaertsToreRegulaer : e.HeimToreRegulaer;
        if (starkTore > schwachTore) StarkSiege++;
        else if (schwachTore > starkTore) SchwachSiege++;
    }
}

/// <summary>#677, K3–K6: Wirkung der Kondition auf Tore und Karten. A und B in <see cref="Spielsituation"/> sind daran
/// kalibriert.</summary>
public class KonditionWirkungTests(KonditionStichprobe s) : IClassFixture<KonditionStichprobe>
{
    private static void Bericht(string zeile) => TestContext.Current.TestOutputHelper?.WriteLine(zeile);

    private static double Anteil(int stark, int schwach) => (double)stark / (stark + schwach);

    [Fact]
    public void Schlussphase_StarkesTeamTrifftInDerSchlussphaseMehr_NichtInDerErstenHaelfte()
    {
        double erste = Anteil(s.StarkToreErsteHaelfte, s.SchwachToreErsteHaelfte);
        double schluss = Anteil(s.StarkToreSchluss, s.SchwachToreSchluss);
        Bericht($"Torenanteil stark: Minute 1–45 {erste:P1} (Band 48–52 %), Minute 76–90 {schluss:P1} (Band 55–62 %)");
        erste.Should().BeInRange(0.48, 0.52);
        schluss.Should().BeInRange(0.55, 0.62);
    }

    [Fact]
    public void Verlaengerung_StarkesTeamTrifftAmMeisten()
    {
        double anteil = Anteil(s.StarkToreVerlaengerung, s.SchwachToreVerlaengerung);
        Bericht($"Torenanteil stark in der Verlängerung: {anteil:P1} (Band 57–66 %), Tore {s.StarkToreVerlaengerung + s.SchwachToreVerlaengerung}");
        anteil.Should().BeInRange(0.57, 0.66);
    }

    [Fact]
    public void Gesamtwirkung_SiegquoteStarkMinusSchwach_Moderat()
    {
        double differenz = (double)(s.StarkSiege - s.SchwachSiege) / KonditionStichprobe.SPIELE;
        Bericht($"Siegquote stark − schwach: {differenz * 100:F1} Punkte (Band +2 … +7)");
        (differenz * 100).Should().BeInRange(2, 7);
    }

    [Fact]
    public void Karten_SchwachesTeamSiehtInDerZweitenHaelfteMehrGelb()
    {
        double verhaeltnis = (double)s.SchwachGelbZweiteHaelfte / s.StarkGelbZweiteHaelfte;
        Bericht($"Gelbe Karten 2. Halbzeit schwach/stark: {verhaeltnis:F2} (mindestens 1,1)");
        verhaeltnis.Should().BeGreaterThanOrEqualTo(1.1);
    }
}
