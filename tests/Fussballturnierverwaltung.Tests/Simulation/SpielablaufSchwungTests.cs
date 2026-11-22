using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.Ablauffolge;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Schwung nach dem Ausgleich (#692, K2, K3). Beobachtet wird er am Wurf „wessen Chance“: Heimteam und Gastteam stehen
/// dort ohne Schwung bei einem Heimanteil von rund 0,66 (ein Wurf unter dem Anteil heißt Heimteam). Hat das Heimteam den
/// Schwung, steigt er über 0,775, hat ihn das Gastteam, fällt er unter 0,575. Der Probewurf 0,62 trifft also ohne
/// Schwung und mit Schwung des Heimteams das Heimteam, mit dem Schwung des Gasts das Gastteam; 0,70 ohne Schwung und mit
/// Schwung des Gasts das Gastteam, mit dem Schwung des Heimteams das Heimteam. Der Probewurf folgt als Treffer auf den
/// letzten Tor-Zug und macht die wählende Mannschaft am letzten Tor sichtbar.
/// </summary>
public class SpielablaufSchwungTests
{
    private const double PROBE_HEIM_OHNE_SCHWUNG = 0.62;
    private const double PROBE_GAST_OHNE_SCHWUNG = 0.70;

    /// <summary>Spielt die Tore (true = Heimteam) je in einer eigenen Minute und danach eine Probe-Chance, die ein Tor
    /// wird; liefert, wer sie hatte.</summary>
    private static bool ProbeTrifftHeim(bool[] tore, double wurf, params VerfahrenArt[] verfahren) =>
        Probe(new Ablauffolge(), tore, wurf, hinspielstand: default, verfahren);

    private static bool Probe(
        Ablauffolge folge, bool[] tore, double wurf, Hinspielstand hinspielstand, params VerfahrenArt[] verfahren)
    {
        foreach (bool heim in tore)
            folge.MinuteMitTor(heim);
        folge.Hinzu(JA, wurf, NEIN, JA).Hinzu(TorstilOhneKonter).Karten();

        var e = SimulationsEngine.Simulieren(
            HeimTeam(), GastTeam(), VerfahrenKette(verfahren), ausfuehrlich: true, folge.Zufall(), hinspielstand: hinspielstand);
        return e.Ereignisse.Last(x => x.Art == EreignisArt.Tor).IstHeim;
    }

    // ── K2: Zustände davor, Ausgleich, Ende ───────────────────────────────

    [Fact]
    public void BeiNullZuNull_KeinSchwung()
    {
        ProbeTrifftHeim([], PROBE_HEIM_OHNE_SCHWUNG).Should().BeTrue();
        ProbeTrifftHeim([], PROBE_GAST_OHNE_SCHWUNG).Should().BeFalse();
    }

    [Fact]
    public void BeiFuehrung_KeinSchwung_EinFuehrungstorSetztIhnNicht()
    {
        ProbeTrifftHeim([true], PROBE_GAST_OHNE_SCHWUNG).Should().BeFalse("das Führungstor gibt dem Heimteam keinen Schwung");
        ProbeTrifftHeim([false, false], PROBE_HEIM_OHNE_SCHWUNG).Should().BeTrue("das Führungstor gibt dem Gast keinen Schwung");
    }

    [Fact]
    public void GleichtDasGastteamAus_HatEsDenSchwung()
    {
        ProbeTrifftHeim([true, false], PROBE_HEIM_OHNE_SCHWUNG).Should().BeFalse();
    }

    [Fact]
    public void GleichtDasHeimteamAus_HatEsDenSchwung()
    {
        ProbeTrifftHeim([false, true], PROBE_GAST_OHNE_SCHWUNG).Should().BeTrue();
    }

    [Fact]
    public void DasNaechsteTorBeendetDenSchwung()
    {
        // Heimteam gleicht aus (Schwung), das Gast trifft zum 1:2 – der Stand entspricht dem ohne Ausgleich (0:1, 0:2, 1:2).
        ProbeTrifftHeim([false, true, false], PROBE_GAST_OHNE_SCHWUNG).Should().BeFalse();
        ProbeTrifftHeim([false, false, true], PROBE_GAST_OHNE_SCHWUNG).Should().BeFalse();
    }

    [Fact]
    public void GleichtDasNaechsteTeamErneutAus_HatEsDenSchwung()
    {
        // 1:0, 1:1 (Gast), 2:1 (beendet), 2:2 (Gast gleicht erneut aus)
        ProbeTrifftHeim([true, false, true, false], PROBE_HEIM_OHNE_SCHWUNG).Should().BeFalse();
    }

    // ── K3: Verlängerung, Rückspiel ───────────────────────────────────────

    [Fact]
    public void InDerVerlaengerung_WirktDerSchwungNicht()
    {
        // 1:0, 1:1 in den ersten beiden Minuten, danach ruhig bis zum Ende der regulären Spielzeit; die erste Chance der
        // Verlängerung ist die Probe – mit Schwung des Gasts ginge der Wurf 0,62 ans Gastteam.
        var folge = new Ablauffolge()
            .MinuteMitTor(true).MinuteMitTor(false).Ruhig(43).Nachspielzeit(NEIN).Ruhig(3)
            .Ruhig(45).Nachspielzeit(NEIN).Ruhig(6)
            .Hinzu(JA, PROBE_HEIM_OHNE_SCHWUNG, NEIN, JA).Hinzu(TorstilOhneKonter).Karten();

        var e = SimulationsEngine.Simulieren(
            HeimTeam(), GastTeam(), VerfahrenKette(VerfahrenArt.VerlaengerungKlassisch), ausfuehrlich: true, folge.Zufall());

        e.Ereignisse.Last(x => x.Art == EreignisArt.Tor).IstHeim.Should().BeTrue();
        e.Ereignisse.Last(x => x.Art == EreignisArt.Tor).Minute.Should().BeGreaterThan(90);
    }

    [Fact]
    public void ImRueckspiel_ZaehltDerGesamtstandAlsAusgleich()
    {
        // Hinspiel 0:1 aus Sicht des Rückspiel-Heimteams: Es führt 1:0 im Rückspiel und gleicht damit den Gesamtstand aus.
        var hin = Hinspielstand.AusHinspiel(hinspielHeimTore: 1, hinspielAuswaertsTore: 0, auswaertstorRegelAktiv: false);

        Probe(new Ablauffolge(), [true], PROBE_GAST_OHNE_SCHWUNG, hin).Should().BeTrue("Gesamtstand 1:1, das Heimteam hat ausgeglichen");
    }

    [Fact]
    public void ImRueckspiel_GleichstandNurImSpielIstKeinAusgleich()
    {
        // Hinspiel 1:0 für das Rückspiel-Heimteam; 1:0, 1:1 im Rückspiel gleicht das Spiel, nicht den Gesamtstand (2:1).
        var hin = Hinspielstand.AusHinspiel(hinspielHeimTore: 0, hinspielAuswaertsTore: 1, auswaertstorRegelAktiv: false);

        Probe(new Ablauffolge(), [true, false], PROBE_HEIM_OHNE_SCHWUNG, hin).Should().BeTrue();
    }
}
