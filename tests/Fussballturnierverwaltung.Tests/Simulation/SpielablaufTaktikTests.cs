using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Simulation.Ablauffolge;
using static Fussballturnierverwaltung.Tests.Simulation.SimulationsEngineTestdaten;
using static Fussballturnierverwaltung.Tests.Simulation.SpielablaufTests;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>
/// Taktik im minutenweisen Spielablauf (#361, #673): wann ein Team seine Risikostufe wechselt und wie sich das auf
/// die Tore auswirkt. Zufallsfolgen und Teams wie in <see cref="SpielablaufTests"/>.
/// </summary>
public class SpielablaufTaktikTests
{
    // ── Taktikwechsel und Tore nacheinander (K3, K4, K5, K8) ──────────────

    /// <summary>#673 K7: Ohne weiteres Tor stellt ein Team, das jeden Wurf mit „noch nicht“ beantwortet, sieben
    /// Minuten nach der Schwelle um. 1:0 in Minute 1 – der Gast (Stärkeanteil 1/3) ist frühestens ab der 73.
    /// offensiv, ab der 88. mit Torwart; das Heimteam (2/3) wird frühestens ab der 73. defensiv (#667) und mauert
    /// frühestens ab der 83. In der Nachspielzeit läuft die Restzeit weiter, der Torwart geht in der 90.+5 nach vorn.</summary>
    [Fact]
    public void Rueckstand_TaktikwechselSpaetestensSiebenMinutenNachDerSchwelle()
    {
        var e = Simulieren(new Ablauffolge().MinuteMitTor(heim: true).Zufall());

        Kurz(e.Ereignisse.Where(x => x.Art == EreignisArt.Taktikwechsel)).Should().Equal(
            (80, null, EreignisArt.Taktikwechsel, true, "Zieht sich etwas zurück und sichert die Führung"),
            (80, null, EreignisArt.Taktikwechsel, false, "Stellt auf Offensive um"),
            (90, null, EreignisArt.Taktikwechsel, true, "Zieht sich zurück und verteidigt die Führung"),
            (90, 5, EreignisArt.Taktikwechsel, false, "Torwart geht mit nach vorn"));
    }

    /// <summary>#671 K3 und #673 K3: im Rückspiel zählt der Gesamtstand – bei 0:0 nach einem 0:5 im Hinspiel liegt
    /// das Rückspiel-Heimteam fünf Tore zurück: offensiv vom Anpfiff, alles nach vorn frühestens ab der 7., und als
    /// es ab der 61. aussichtslos ist, zurück zur gewohnten Ordnung – nie mit dem Torwart vorn. Der Gast mauert
    /// frühestens ab der 57., defensiv ist er frühestens ab der 47. (#667). Jeder Wurf heißt „noch nicht“, umgestellt
    /// wird also sieben Minuten nach der Schwelle.</summary>
    [Fact]
    public void Rueckspiel_TaktikNachGesamtstand_AussichtslosOhneTorwartVorn()
    {
        var e = SimulationsEngine.Simulieren(
            HeimTeam(), GastTeam(), [], ausfuehrlich: true, new Ablauffolge().Zufall(),
            hinspielstand: Hinspielstand.AusHinspiel(hinspielHeimTore: 5, hinspielAuswaertsTore: 0, auswaertstorRegelAktiv: false));

        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((0, 0));
        Kurz(e.Ereignisse).Should().Equal(
            (1, null, EreignisArt.Taktikwechsel, true, "Stellt auf Offensive um"),
            (14, null, EreignisArt.Taktikwechsel, true, "Wirft alles nach vorn"),
            (54, null, EreignisArt.Taktikwechsel, false, "Zieht sich etwas zurück und sichert die Führung"),
            (64, null, EreignisArt.Taktikwechsel, false, "Zieht sich zurück und verteidigt die Führung"),
            (68, null, EreignisArt.Taktikwechsel, true, "Kehrt zur gewohnten Ordnung zurück"));
    }

    /// <summary>#673 K3 – BVB – Málaga, Viertelfinale 2013: Hinspiel 0:0, Auswärtstorregel. Nach dem 1:2 in der 82.
    /// braucht der BVB zwei Tore (ein 2:2 reichte wegen der Auswärtstore nicht) und bleibt bis zum Schluss bei „alles
    /// nach vorn“; nach dem 2:2 in der 90.+1 geht sofort der Torwart mit nach vorn, nach dem 3:2 tauschen beide
    /// Teams die Rollen. Jeder Taktikwurf heißt „noch nicht“.</summary>
    [Fact]
    public void Rueckspiel_ZweiToreRueckstandSpaet_NieAussichtslos_NachDemAnschlusstorTorwartVorn()
    {
        var zufall = new Ablauffolge()
            .Ruhig(24).MinuteMitTor(heim: false)        // 25.: 0:1 – der BVB braucht zwei Tore
            .Ruhig(6).RuhigMitTaktikwurf(8)             // offensiv ab der 32. fällig, in der 39. sicher
            .MinuteMitTor(heim: true)                   // 40.: 1:1 – ein Tor reicht wieder
            .Ruhig(5).Nachspielzeit(JA).Ruhig(1)
            .Ruhig(1).RuhigMitTaktikwurf(8, teams: 2)   // BVB offensiv, Málaga defensiv (#667) ab der 47. fällig
            .Ruhig(2).RuhigMitTaktikwurf(8)             // Málaga mauert ab der 57. fällig
            .Ruhig(2).RuhigMitTaktikwurf(8)             // BVB alles nach vorn ab der 67. fällig
            .Ruhig(7).MinuteMitTor(heim: false)         // 82.: 1:2
            .Ruhig(8).Nachspielzeit(0.5)                // vier Minuten Nachspielzeit
            .MinuteMitTor(heim: true).Ruhig(1)          // 90.+1: 2:2
            .MinuteMitTor(heim: true)                   // 90.+3: 3:2
            .Zufall();

        var e = SimulationsEngine.Simulieren(
            HeimTeam(), GastTeam(), [], ausfuehrlich: true, zufall,
            hinspielstand: Hinspielstand.AusHinspiel(hinspielHeimTore: 0, hinspielAuswaertsTore: 0, auswaertstorRegelAktiv: true));

        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((3, 2));
        Kurz(e.Ereignisse.Where(x => x.Art == EreignisArt.Taktikwechsel)).Should().Equal(
            (39, null, EreignisArt.Taktikwechsel, true, "Stellt auf Offensive um"),
            (40, null, EreignisArt.Taktikwechsel, true, "Kehrt zur gewohnten Ordnung zurück"),
            (54, null, EreignisArt.Taktikwechsel, true, "Stellt auf Offensive um"),
            (54, null, EreignisArt.Taktikwechsel, false, "Zieht sich etwas zurück und sichert die Führung"),
            (64, null, EreignisArt.Taktikwechsel, false, "Zieht sich zurück und verteidigt die Führung"),
            (74, null, EreignisArt.Taktikwechsel, true, "Wirft alles nach vorn"),
            (90, 1, EreignisArt.Taktikwechsel, true, "Torwart geht mit nach vorn"),
            (90, 3, EreignisArt.Taktikwechsel, true, "Zieht sich zurück und verteidigt die Führung"),
            (90, 3, EreignisArt.Taktikwechsel, false, "Torwart geht mit nach vorn"));
    }

    [Fact]
    public void ZweiToreInEinerMinute_DasZweiteRechnetMitDemNeuenStand()
    {
        // Minute 86: Heimtor, beide Teams stellen sofort um (#673: ein Tor löst den Wechsel ohne Wurf aus), Ausgleich,
        // beide kehren zur gewohnten Ordnung zurück. Der Gast als Außenseiter ist erst offensiv, nicht alles nach vorn.
        var zufall = new Ablauffolge()
            .Ruhig(45).Nachspielzeit(JA).Ruhig(1).Ruhig(40)
            .Tor(heim: true).Tor(heim: false).KeinTor().Karten()
            .Zufall();

        var e = Simulieren(zufall);

        Kurz(e.Ereignisse).Should().Equal(
            (86, null, EreignisArt.Tor, true, KOPFBALL),
            (86, null, EreignisArt.Taktikwechsel, true, "Zieht sich zurück und verteidigt die Führung"),
            (86, null, EreignisArt.Taktikwechsel, false, "Stellt auf Offensive um"),
            (86, null, EreignisArt.Tor, false, KOPFBALL),
            (86, null, EreignisArt.Taktikwechsel, true, "Kehrt zur gewohnten Ordnung zurück"),
            (86, null, EreignisArt.Taktikwechsel, false, "Kehrt zur gewohnten Ordnung zurück"));
        (e.HeimToreRegulaer, e.AuswaertsToreRegulaer).Should().Be((1, 1));
    }

    [Theory]
    [InlineData(true, "Tor ins leere Tor")]
    [InlineData(false, KOPFBALL)]
    public void Brechstange_EinTeilDerGegentoreGehtInsLeereTor(bool insLeereTor, string erwartet)
    {
        // 1:0 in Minute 1; der Gast wird ab der 73. offensiv und das Heimteam defensiv (#667; Wurf je Minute und Team,
        // in der 80. sicher), das Heimteam könnte ab der 83. mauern, in der 88. geht der Torhüter des Gasts mit nach
        // vorn, in der 89. trifft das Heimteam.
        var folge = new Ablauffolge()
            .MinuteMitTor(heim: true).Ruhig(44).Nachspielzeit(JA).Ruhig(1).Ruhig(27)
            .RuhigMitTaktikwurf(8, teams: 2).Ruhig(2).RuhigMitTaktikwurf(5)
            .Taktikwurf().Taktikwurf(JA).Ruhig(1)
            .Taktikwurf().Hinzu(JA, JA, insLeereTor ? JA : NEIN);
        if (!insLeereTor)
            folge.Hinzu(TorstilOhneKonter);

        var e = Simulieren(folge.Zufall());

        e.Ereignisse.Last(x => x.Art == EreignisArt.Tor)
            .Should().Be(new SimuliertesEreignis(89, EreignisArt.Tor, true, erwartet));
    }

    // ── Entscheidungsmodell der Risikostufe (#673, K5, K7) ────────────────

    /// <summary>K7: 1:0 in Minute 1, danach Zufall. Der Gast (Stärkeanteil 1/3) darf ab der 73. offensiv werden und
    /// ist es spätestens in der 80.; dazwischen streut die Minute – im Mittel gut drei Minuten nach der Schwelle.</summary>
    [Fact]
    public void Umstellminute_StreutZwischenSchwelleUndSiebenMinutenDanach()
    {
        var rng = new Random(673);
        var minuten = new List<int>();

        for (int i = 0; i < 8000; i++)
        {
            var e = Simulieren(new Ablauffolge().MinuteMitTor(heim: true).Anfang(rng));
            if (e.Ereignisse.Count(x => x.Art == EreignisArt.Tor && x.Minute <= 80) != 1
                || e.Ereignisse.Any(x => x.Art == EreignisArt.RoteKarte && x.Minute < 80))
                continue; // nur Spiele, in denen allein die Zeit den Wechsel fällig macht
            minuten.Add(e.Ereignisse.First(x => x.Art == EreignisArt.Taktikwechsel && !x.IstHeim).Minute);
        }

        minuten.Should().HaveCountGreaterThan(400);
        minuten.Min().Should().Be(73);
        minuten.Max().Should().BeLessThanOrEqualTo(80);
        minuten.Distinct().Should().HaveCountGreaterThanOrEqualTo(6, "nicht alle Teams stellen in derselben Minute um");
        minuten.Average().Should().BeInRange(74.8, 75.8);
    }

    /// <summary>K5: Ein Favorit im Rückstand stellt im Mittel deutlich früher auf Offensive um als ein Außenseiter;
    /// ein Außenseiter in Führung mauert im Mittel deutlich früher als ein Favorit.</summary>
    [Fact]
    public void Kraefteverhaeltnis_FavoritDruecktFrueher_AussenseiterMauertFrueher()
    {
        var rng = new Random(6731);
        var offensiv = new Dictionary<bool, List<int>> { [true] = [], [false] = [] };
        var mauern   = new Dictionary<bool, List<int>> { [true] = [], [false] = [] };

        for (int i = 0; i < 3000; i++)
            foreach (var x in Simulieren(rng).Ereignisse.Where(x => x.Art == EreignisArt.Taktikwechsel))
                if (x.Beschreibung == "Stellt auf Offensive um")
                    offensiv[x.IstHeim].Add(x.Minute);
                else if (x.Beschreibung == "Zieht sich zurück und verteidigt die Führung")
                    mauern[x.IstHeim].Add(x.Minute);

        offensiv[true].Average().Should().BeLessThan(offensiv[false].Average() - 10, "der Favorit (Heim) drückt früher");
        mauern[false].Average().Should().BeLessThan(mauern[true].Average() - 10, "der Außenseiter (Gast) mauert früher");
    }

    /// <summary>K8 (Befund aus dem KI-Review): Zur gewohnten Ordnung kehrt ein Team nur nach einem Tor derselben
    /// Minute zurück oder wenn es aussichtslos (drei Tore und mehr) zurückliegt – nicht, weil die Restzeit nach der
    /// Nachspielzeit der ersten Hälfte wieder steigt, und nicht zu Beginn der Verlängerung.</summary>
    [Fact]
    public void RueckwechselZurGewohntenOrdnung_NurNachToroderAussichtslos()
    {
        var rng = new Random(6732);
        var unbegruendet = new List<string>();

        for (int i = 0; i < 3000; i++)
        {
            var ereignisse = Simulieren(rng, VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen).Ereignisse;
            int heimTore = 0, gastTore = 0;
            bool torInDieserMinute = false;
            (int, int?) minuteVorher = (0, null);
            foreach (var e in ereignisse)
            {
                if ((e.Minute, e.Nachspielminute) != minuteVorher)
                    torInDieserMinute = false;
                minuteVorher = (e.Minute, e.Nachspielminute);

                if (e.Art is EreignisArt.Tor or EreignisArt.GoldenGoal)
                {
                    torInDieserMinute = true;
                    if (e.IstHeim) heimTore++;
                    else gastTore++;
                }
                else if (e.Beschreibung == "Kehrt zur gewohnten Ordnung zurück" && !torInDieserMinute)
                {
                    int rueckstand = e.IstHeim ? gastTore - heimTore : heimTore - gastTore;
                    if (rueckstand < 3)
                        unbegruendet.Add($"Spiel {i}: {Spielminute.Text(e.Minute, e.Nachspielminute)} {(e.IstHeim ? "Heim" : "Gast")}");
                }
            }
        }

        unbegruendet.Should().BeEmpty();
    }

    // ── Grundausrichtung und Flexibilität (#667) ──────────────────────────

    private static Mannschaft Mit(Mannschaft team, Grundausrichtung grundausrichtung, Flexibilitaet flexibilitaet)
    {
        team.Grundausrichtung = grundausrichtung;
        team.Flexibilitaet    = flexibilitaet;
        return team;
    }

    /// <summary>K3: Ein Team geht in seiner Grundausrichtung ins Spiel – das ist kein Taktikwechsel.</summary>
    [Fact]
    public void Anpfiff_InDerGrundausrichtung_OhneTaktikwechsel()
    {
        var e = SimulationsEngine.Simulieren(
            Mit(HeimTeam(), Grundausrichtung.SehrOffensiv, Flexibilitaet.Starr),
            Mit(GastTeam(), Grundausrichtung.SehrDefensiv, Flexibilitaet.Flexibel),
            [], ausfuehrlich: true, new Ablauffolge().Zufall());

        e.Ereignisse.Should().BeEmpty();
    }

    /// <summary>K6: 1:0 in Minute 1, jeder Wurf heißt „noch nicht“. Das flexible Heimteam (Stärkeanteil 2/3) wird ab
    /// der 73. defensiv und ist es spätestens in der 76., mauert ab der 83. und spätestens in der 86.; der starre Gast
    /// ist ab der 73. offensiv fällig und stellt erst elf Minuten danach um.</summary>
    [Fact]
    public void Flexibilitaet_BestimmtWieSchnellUmgestelltWird()
    {
        var e = SimulationsEngine.Simulieren(
            Mit(HeimTeam(), Grundausrichtung.Ausgeglichen, Flexibilitaet.Flexibel),
            Mit(GastTeam(), Grundausrichtung.Ausgeglichen, Flexibilitaet.Starr),
            [], ausfuehrlich: true, new Ablauffolge().MinuteMitTor(heim: true).Zufall());

        Kurz(e.Ereignisse.Where(x => x.Art == EreignisArt.Taktikwechsel)).Should().Equal(
            (76, null, EreignisArt.Taktikwechsel, true, "Zieht sich etwas zurück und sichert die Führung"),
            (84, null, EreignisArt.Taktikwechsel, false, "Stellt auf Offensive um"),
            (86, null, EreignisArt.Taktikwechsel, true, "Zieht sich zurück und verteidigt die Führung"));
    }

    /// <summary>K3: Wer offensiv ins Spiel geht, schießt mehr Tore und kassiert überproportional mehr – gegen
    /// denselben Gegner, mit denselben Zufallszahlen.</summary>
    [Fact]
    public void Grundausrichtung_OffensivMehrToreUndMehrGegentore_DefensivWeniger()
    {
        var (eigeneOff, gegenOff) = Tore(Grundausrichtung.SehrOffensiv);
        var (eigeneAus, gegenAus) = Tore(Grundausrichtung.Ausgeglichen);
        var (eigeneDef, gegenDef) = Tore(Grundausrichtung.SehrDefensiv);

        eigeneOff.Should().BeGreaterThan(eigeneAus);
        gegenOff.Should().BeGreaterThan(gegenAus);
        (gegenOff / gegenAus).Should().BeGreaterThan(eigeneOff / eigeneAus, "die Abwehr öffnet sich überproportional");
        eigeneDef.Should().BeLessThan(eigeneAus);
        gegenDef.Should().BeLessThan(gegenAus);
    }

    private static (double Eigene, double Gegen) Tore(Grundausrichtung grundausrichtung)
    {
        var rng = new Random(667);
        var heim = Mit(HeimTeam(), grundausrichtung, Flexibilitaet.Normal);
        int eigene = 0, gegen = 0;
        for (int i = 0; i < 4000; i++)
        {
            var e = SimulationsEngine.Simulieren(heim, GastTeam(), [], ausfuehrlich: true, rng);
            eigene += e.HeimToreRegulaer;
            gegen  += e.AuswaertsToreRegulaer;
        }
        return (eigene, gegen);
    }
}
