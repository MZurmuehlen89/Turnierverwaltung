using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Issue #404: Monte-Carlo-Absicherung der Torformel gegen reale Kennzahlen. Die Ratings sind eine feste Kopie
/// der Seed-Daten (Saison 2025/26), damit spätere Seed-Pflege die Kalibrierung nicht unbemerkt verschiebt.
/// Die Toleranzbänder sind bewusst großzügig — sie sollen grobe Fehlkalibrierungen fangen, nicht Rauschen.
/// </summary>
public class SimulationsKalibrierungTests
{
    // #361: der ausführliche Pfad (Minute für Minute mit Spielzustand) muss dieselben Kennzahlen treffen – die App
    // simuliert seit #171 immer ausführlich. #667: auch mit der Spielweise, die der Seed aus den Ratings vorschlägt.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Bundesliga_KennzahlenLiegenImRealenBereich(bool ausfuehrlich, bool mitSpielweise)
    {
        var teams = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1, mitSpielweise);
        var rng = new Random(404);
        const int SAISONS = 30;

        int spiele = 0, tore = 0, heimsiege = 0, remis = 0;
        double meisterPunkte = 0, letzterPunkte = 0;

        for (int s = 0; s < SAISONS; s++)
        {
            var punkte = teams.ToDictionary(t => t.Id, _ => 0);
            int staerkeSeed = 404_000 + s; // #679: je Saison eine Turnierstärke je Team
            foreach (var heim in teams)
            foreach (var gast in teams)
            {
                if (heim == gast) continue;
                var erg = SimulationsEngine.Simulieren(
                    heim, gast, [], ausfuehrlich, rng, turnierform: Turnierstaerke.Form(staerkeSeed, heim.Id, gast.Id));
                spiele++;
                tore += erg.HeimToreRegulaer + erg.AuswaertsToreRegulaer;
                if (erg.HeimToreRegulaer > erg.AuswaertsToreRegulaer) { heimsiege++; punkte[heim.Id] += 3; }
                else if (erg.HeimToreRegulaer < erg.AuswaertsToreRegulaer) punkte[gast.Id] += 3;
                else { remis++; punkte[heim.Id]++; punkte[gast.Id]++; }
            }
            meisterPunkte += punkte.Values.Max();
            letzterPunkte += punkte.Values.Min();
        }

        // Reale Bundesliga (Mittel der letzten ~10 Saisons): ~3,1 Tore, ~44 % Heimsiege, ~24 % Remis,
        // Meister ~80 Punkte, Letzter ~25 Punkte.
        ((double)tore / spiele).Should().BeInRange(2.8, 3.4, "Tore je Spiel");
        ((double)heimsiege / spiele).Should().BeInRange(0.39, 0.48, "Heimsieg-Quote");
        ((double)remis / spiele).Should().BeInRange(0.19, 0.28, "Remis-Quote");
        (meisterPunkte / SAISONS).Should().BeInRange(72, 88, "Punkte des Meisters");
        (letzterPunkte / SAISONS).Should().BeInRange(16, 30, "Punkte des Letzten");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pokalrunde1_AussenseiterGewinnenRealistischSelten(bool ausfuehrlich)
    {
        // Real scheiden je Saison 1–4 der 18 Bundesligisten in Runde 1 aus.
        AusgeschiedeneBundesligistenJeSaison(ueberraschungsfaktor: 0, ausfuehrlich).Should().BeInRange(1.5, 4.0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pokalrunde1_MitPokalUeberraschungsfaktor_MehrAussenseitersiegeImRealenRahmen(bool ausfuehrlich)
    {
        // Issue #360: der Pokal-Faktor der Seed-Vorlagen hebt die Quote spürbar, bleibt aber im realen Bereich (1–4,
        // etwas Luft nach oben für die Zufallsschwankung).
        double ohne = AusgeschiedeneBundesligistenJeSaison(ueberraschungsfaktor: 0, ausfuehrlich);
        double mit  = AusgeschiedeneBundesligistenJeSaison(TurnierVorlagenSeedDaten.POKAL_UEBERRASCHUNGSFAKTOR, ausfuehrlich);

        mit.Should().BeGreaterThan(ohne + 0.3);
        mit.Should().BeInRange(2.5, 4.5);
    }

    private static double AusgeschiedeneBundesligistenJeSaison(int ueberraschungsfaktor, bool ausfuehrlich)
    {
        var bundesliga = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga, 1);
        var unterklassig = KalibrierungsTeams.Teams(KalibrierungsTeams.Unterklassig, 100);
        EntscheidungsVerfahren[] verfahren =
        [
            new() { VerfahrenArt = VerfahrenArt.VerlaengerungKlassisch, Reihenfolge = 1 },
            new() { VerfahrenArt = VerfahrenArt.Elfmeterschiessen,      Reihenfolge = 2 }
        ];
        var rng = new Random(404);
        const int DURCHLAEUFE = 40;

        int spiele = 0, aussenseiterSiege = 0;
        for (int d = 0; d < DURCHLAEUFE; d++)
        foreach (var favorit in bundesliga)
        foreach (var aussenseiter in unterklassig)
        {
            // Im DFB-Pokal hat der unterklassige Verein Heimrecht. #679: je Durchlauf eine Turnierstärke je Team.
            var erg = SimulationsEngine.Simulieren(
                aussenseiter, favorit, verfahren, ausfuehrlich, rng, Spielortvorteil.Heim, ueberraschungsfaktor,
                turnierform: Turnierstaerke.Form(404_100 + d, aussenseiter.Id, favorit.Id));
            spiele++;
            if (HeimGewinnt(erg)) aussenseiterSiege++;
        }

        return 18.0 * aussenseiterSiege / spiele;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Topfavorit_VerliertAuswaertsBeimSchwaechstenRegionalligistenSelten(bool ausfuehrlich)
    {
        var bayern = KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga[..1], 1)[0];
        var wuppertal = KalibrierungsTeams.Teams(KalibrierungsTeams.Unterklassig[^1..], 100)[0];
        var rng = new Random(404);
        const int SPIELE = 5000;

        int niederlagen = Enumerable.Range(0, SPIELE)
            .Select(_ => SimulationsEngine.Simulieren(wuppertal, bayern, [], ausfuehrlich, rng))
            .Count(e => e.HeimToreRegulaer > e.AuswaertsToreRegulaer);

        ((double)niederlagen / SPIELE).Should().BeInRange(0.002, 0.06,
            "Überraschungen bleiben möglich, aber selten");
    }

    // ── Spielbedeutung (#668, K3) ─────────────────────────────────────────

    [Fact]
    public void Spielbedeutung_GeringMehrToreHochWeniger_EffektBleibtKlein()
    {
        double hoch   = ToreJeSpielGleichStarker(Spielbedeutung.Hoch);
        double normal = ToreJeSpielGleichStarker(Spielbedeutung.Normal);
        double gering = ToreJeSpielGleichStarker(Spielbedeutung.Gering);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Tore je Spiel – hoch {hoch:F3}, normal {normal:F3}, gering {gering:F3}");

        // Abgestimmt: Finale gut 5 % weniger Tore, Spiel um Platz 3 knapp 20 % mehr – kein „Wunder-Modus“ (#672).
        (hoch / normal).Should().BeInRange(0.90, 0.98, "hohe Bedeutung: etwas vorsichtiger");
        (gering / normal).Should().BeInRange(1.10, 1.25, "geringe Bedeutung: offener");
    }

    private static double ToreJeSpielGleichStarker(Spielbedeutung spielbedeutung)
    {
        var (heim, gast) = (KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga[8..9], 1)[0], KalibrierungsTeams.Teams(KalibrierungsTeams.Bundesliga[8..9], 2)[0]);
        var rng = new Random(668);
        const int SPIELE = 40_000;

        int tore = 0;
        for (int i = 0; i < SPIELE; i++)
        {
            var erg = SimulationsEngine.Simulieren(
                heim, gast, [], ausfuehrlich: true, rng, Spielortvorteil.Neutral, spielbedeutung: spielbedeutung);
            tore += erg.HeimToreRegulaer + erg.AuswaertsToreRegulaer;
        }
        return (double)tore / SPIELE;
    }

    private static bool HeimGewinnt(SimulationsErgebnis erg)
    {
        int heim = erg.HeimToreRegulaer + (erg.HeimToreVerlaengerung ?? 0);
        int gast = erg.AuswaertsToreRegulaer + (erg.AuswaertsToreVerlaengerung ?? 0);
        if (heim != gast) return heim > gast;
        return (erg.HeimElfmetertore ?? 0) > (erg.AuswaertsElfmetertore ?? 0);
    }
}
