using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Issue #404: Monte-Carlo-Absicherung der Torformel gegen reale Kennzahlen. Die Ratings sind eine feste Kopie
/// der Seed-Daten (Saison 2025/26), damit spätere Seed-Pflege die Kalibrierung nicht unbemerkt verschiebt.
/// Die Toleranzbänder sind bewusst großzügig — sie sollen grobe Fehlkalibrierungen fangen, nicht Rauschen.
/// </summary>
public class SimulationsKalibrierungTests
{
    // (Off, Mid, Def, Heim, Ausw)
    private static readonly (int, int, int, int, int)[] _bundesliga =
    [
        (90, 88, 86, 95, 82), (87, 86, 84, 88, 80), (84, 82, 78, 88, 76), (83, 82, 80, 84, 76),
        (79, 78, 77, 82, 73), (78, 77, 76, 80, 72), (75, 75, 73, 77, 69), (74, 74, 74, 78, 69),
        (73, 73, 73, 79, 68), (73, 73, 72, 76, 67), (74, 73, 72, 76, 68), (73, 73, 71, 76, 67),
        (72, 72, 71, 75, 67), (70, 70, 70, 73, 65), (68, 68, 67, 73, 62), (67, 67, 66, 70, 61),
        (73, 72, 71, 77, 67), (70, 69, 68, 75, 63)
    ];

    // 3. Liga + Regionalliga-Pokalteilnehmer
    private static readonly (int, int, int, int, int)[] _unterklassig =
    [
        (64, 63, 63, 67, 57), (62, 62, 61, 64, 55), (62, 61, 61, 64, 55), (62, 61, 61, 66, 56),
        (62, 61, 60, 66, 55), (62, 61, 61, 67, 55), (61, 61, 60, 66, 55), (61, 60, 60, 64, 55),
        (61, 60, 59, 66, 55), (61, 60, 60, 65, 55), (61, 60, 60, 66, 54), (60, 60, 60, 65, 54),
        (60, 59, 59, 64, 54), (60, 60, 59, 63, 54), (60, 59, 59, 64, 53), (59, 59, 58, 62, 53),
        (56, 56, 56, 60, 51), (56, 56, 55, 60, 50), (56, 55, 55, 60, 50), (55, 55, 54, 59, 50),
        (55, 55, 54, 60, 49), (55, 55, 54, 59, 49), (54, 54, 54, 59, 49), (54, 54, 53, 58, 49),
        (54, 53, 53, 58, 48), (53, 53, 53, 58, 48), (53, 53, 52, 57, 48), (53, 52, 52, 57, 47)
    ];

    private static List<Mannschaft> Teams((int Off, int Mid, int Def, int Heim, int Ausw)[] ratings, int idBasis) =>
        ratings.Select((r, i) => new Mannschaft
        {
            Id = idBasis + i, Name = $"Team {idBasis + i}", Kurzname = $"T{idBasis + i}",
            OffensivRating = r.Off, MittelfeldRating = r.Mid, DefensivRating = r.Def,
            Heimstaerke = r.Heim, Auswaertsstaerke = r.Ausw
        }).ToList();

    [Fact]
    public void Bundesliga_KennzahlenLiegenImRealenBereich()
    {
        var teams = Teams(_bundesliga, 1);
        var rng = new Random(404);
        const int SAISONS = 30;

        int spiele = 0, tore = 0, heimsiege = 0, remis = 0;
        double meisterPunkte = 0, letzterPunkte = 0;

        for (int s = 0; s < SAISONS; s++)
        {
            var punkte = teams.ToDictionary(t => t.Id, _ => 0);
            foreach (var heim in teams)
            foreach (var gast in teams)
            {
                if (heim == gast) continue;
                var erg = SimulationsEngine.Simulieren(heim, gast, [], false, rng);
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

    [Fact]
    public void Pokalrunde1_AussenseiterGewinnenRealistischSelten()
    {
        var bundesliga = Teams(_bundesliga, 1);
        var unterklassig = Teams(_unterklassig, 100);
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
            // Im DFB-Pokal hat der unterklassige Verein Heimrecht.
            var erg = SimulationsEngine.Simulieren(aussenseiter, favorit, verfahren, false, rng);
            spiele++;
            if (HeimGewinnt(erg)) aussenseiterSiege++;
        }

        // Real scheiden je Saison 1–4 der 18 Bundesligisten in Runde 1 aus.
        double ausgeschiedeneJeSaison = 18.0 * aussenseiterSiege / spiele;
        ausgeschiedeneJeSaison.Should().BeInRange(1.5, 4.0);
    }

    [Fact]
    public void Topfavorit_VerliertAuswaertsBeimSchwaechstenRegionalligistenSelten()
    {
        var bayern = Teams(_bundesliga[..1], 1)[0];
        var wuppertal = Teams(_unterklassig[^1..], 100)[0];
        var rng = new Random(404);
        const int SPIELE = 5000;

        int niederlagen = Enumerable.Range(0, SPIELE)
            .Select(_ => SimulationsEngine.Simulieren(wuppertal, bayern, [], false, rng))
            .Count(e => e.HeimToreRegulaer > e.AuswaertsToreRegulaer);

        ((double)niederlagen / SPIELE).Should().BeInRange(0.002, 0.06,
            "Überraschungen bleiben möglich, aber selten");
    }

    private static bool HeimGewinnt(SimulationsErgebnis erg)
    {
        int heim = erg.HeimToreRegulaer + (erg.HeimToreVerlaengerung ?? 0);
        int gast = erg.AuswaertsToreRegulaer + (erg.AuswaertsToreVerlaengerung ?? 0);
        if (heim != gast) return heim > gast;
        return (erg.HeimElfmetertore ?? 0) > (erg.AuswaertsElfmetertore ?? 0);
    }
}
