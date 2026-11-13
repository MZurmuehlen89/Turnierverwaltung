using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

public class SimulationsEngineTests
{
    private static Mannschaft Team(int id, int off = 70, int mid = 70, int def = 70,
                                   int heim = 75, int ausw = 65) =>
        new() { Id = id, Name = $"Team {id}", Kurzname = $"T{id}",
                OffensivRating = off, MittelfeldRating = mid, DefensivRating = def,
                Heimstaerke = heim, Auswaertsstaerke = ausw };

    private static EntscheidungsVerfahren Verfahren(VerfahrenArt art, int rang = 1) =>
        new() { VerfahrenArt = art, Reihenfolge = rang };

    [Fact]
    public void GleicherSeed_LiefertGleichesErgebnis()
    {
        var heim = Team(1);
        var gast = Team(2);
        var r1 = SimulationsEngine.Simulieren(heim, gast, [], false, new Random(42));
        var r2 = SimulationsEngine.Simulieren(heim, gast, [], false, new Random(42));

        r1.Should().BeEquivalentTo(r2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(999)]
    public void ToranzahlenSindNichtNegativ(int seed)
    {
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(seed));

        erg.HeimToreRegulaer.Should().BeGreaterThanOrEqualTo(0);
        erg.AuswaertsToreRegulaer.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void OhneVerfahren_KeineVerlaengerungsOderElfmeterTore()
    {
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(42));

        erg.HeimToreVerlaengerung.Should().BeNull();
        erg.AuswaertsToreVerlaengerung.Should().BeNull();
        erg.HeimElfmetertore.Should().BeNull();
        erg.AuswaertsElfmetertore.Should().BeNull();
    }

    [Fact]
    public void SchnellSimulation_LiefertKeineEreignisse()
    {
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(42));

        erg.Ereignisse.Should().BeEmpty();
    }

    [Fact]
    public void SchnellSimulation_LiefertKeinenHalbzeitstand()
    {
        // Issue #104: ohne Ereignisse (keine Minutenangaben) darf kein Halbzeitstand vorgetäuscht werden.
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], false, new Random(42));

        erg.HalbzeitHeimTore.Should().BeNull();
        erg.HalbzeitAuswaertsTore.Should().BeNull();
    }

    [Fact]
    public void AusfuehrlicheSimulation_HalbzeitstandEntsprichtTorenBisMinute45()
    {
        // Issue #104: Halbzeitstand wird aus den generierten Torereignissen abgeleitet — muss über
        // viele Seeds hinweg exakt der Anzahl der Heim-/Auswärtstore mit Minute <= 45 entsprechen.
        foreach (var seed in Enumerable.Range(0, 50))
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(seed));

            int erwarteterHeim = erg.Ereignisse.Count(e => e.Art == EreignisArt.Tor && e.IstHeim && e.Minute <= 45);
            int erwarteterGast = erg.Ereignisse.Count(e => e.Art == EreignisArt.Tor && !e.IstHeim && e.Minute <= 45);

            erg.HalbzeitHeimTore.Should().Be(erwarteterHeim);
            erg.HalbzeitAuswaertsTore.Should().Be(erwarteterGast);
            erg.HalbzeitHeimTore.Should().BeLessThanOrEqualTo(erg.HeimToreRegulaer);
            erg.HalbzeitAuswaertsTore.Should().BeLessThanOrEqualTo(erg.AuswaertsToreRegulaer);
        }
    }

    [Fact]
    public void AusfuehrlicheSimulation_EreignisseHabenGueltigeMinuten()
    {
        // Seed 5 liefert mindestens ein Tor, sodass Ereignisse entstehen
        var erg = SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(5));

        erg.Ereignisse.Should().NotBeEmpty();
        erg.Ereignisse.Should().AllSatisfy(e => e.Minute.Should().BeInRange(1, 90));
    }

    [Fact]
    public void AusfuehrlicheSimulation_TorEreignisseHabenZufaelligenTorstil()
    {
        // Issue #126: Tor-Ereignisse tragen zur Flavor-Anreicherung einen zufälligen Torstil in der
        // Beschreibung ("Tor per <Stil>") statt nur "Tor". Seed durchsuchen, bis mindestens ein Tor fällt.
        var torEreignisse = Enumerable.Range(0, 50)
            .SelectMany(seed => SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(seed)).Ereignisse)
            .Where(e => e.Art == EreignisArt.Tor)
            .ToList();

        torEreignisse.Should().NotBeEmpty();
        torEreignisse.Should().AllSatisfy(e => e.Beschreibung.Should().StartWith("Tor per "));
    }

    [Fact]
    public void AusfuehrlicheSimulation_EinigeTorEreignisseSindKontertore()
    {
        // Issue #127: mit ~15% Wahrscheinlichkeit trägt ein Tor-Ereignis den Zusatz "nach Konter" —
        // über genug simulierte Tore hinweg müssen sowohl Kontertore als auch normale Tore vorkommen.
        var torBeschreibungen = Enumerable.Range(0, 200)
            .SelectMany(seed => SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(seed)).Ereignisse)
            .Where(e => e.Art == EreignisArt.Tor)
            .Select(e => e.Beschreibung)
            .ToList();

        torBeschreibungen.Should().Contain(b => b.Contains("nach Konter"));
        torBeschreibungen.Should().Contain(b => !b.Contains("nach Konter"));
    }

    [Fact]
    public void AusfuehrlicheSimulation_TorEreignisseHabenTorzoneInKlammern()
    {
        // Issue #128: jedes Tor-Ereignis trägt zusätzlich eine grob zugeloste Torzone in Klammern
        // am Ende der Beschreibung, z.B. "Tor per Kopfball (unten Mitte)".
        var torBeschreibungen = Enumerable.Range(0, 50)
            .SelectMany(seed => SimulationsEngine.Simulieren(Team(1), Team(2), [], true, new Random(seed)).Ereignisse)
            .Where(e => e.Art == EreignisArt.Tor)
            .Select(e => e.Beschreibung)
            .ToList();

        torBeschreibungen.Should().NotBeEmpty();
        torBeschreibungen.Should().AllSatisfy(b =>
        {
            b.Should().MatchRegex(@"\([a-zA-ZäöüÄÖÜ ]+\)$");
        });
    }

    [Fact]
    public void MitElfmeterVerfahren_BeimUnentschiedenImmerSieger()
    {
        var verfahren = new[] { Verfahren(VerfahrenArt.Elfmeterschiessen) };

        for (int seed = 0; seed < 200; seed++)
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), verfahren, false, new Random(seed));
            if (erg.HeimToreRegulaer == erg.AuswaertsToreRegulaer)
            {
                erg.EntscheidungArt.Should().Be(EntscheidungArt.Elfmeterschiessen);
                erg.HeimElfmetertore.Should().NotBeNull();
                erg.AuswaertsElfmetertore.Should().NotBeNull();
                erg.HeimElfmetertore.Should().NotBe(
                    erg.AuswaertsElfmetertore, "Elfmeter darf nicht mit Gleichstand enden");
            }
        }
    }

    [Fact]
    public void MitSilverGoalVerfahren_LiefertVerlaengerungstoreBeiEntscheidung()
    {
        // Regression #115: VerfahrenArt.SilverGoal fehlte im Engine-Switch und fiel in den
        // default-Zweig, wodurch HeimToreVerlaengerung/AuswaertsToreVerlaengerung trotz
        // konfigurierter Verlängerung immer null blieben.
        var verfahren = new[]
        {
            Verfahren(VerfahrenArt.SilverGoal, rang: 1),
            Verfahren(VerfahrenArt.Elfmeterschiessen, rang: 2)
        };

        for (int seed = 0; seed < 200; seed++)
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), verfahren, false, new Random(seed));
            if (erg.HeimToreRegulaer != erg.AuswaertsToreRegulaer) continue;

            erg.EntscheidungArt.Should().BeOneOf(
                EntscheidungArt.SilverGoal, EntscheidungArt.Verlaengerung, EntscheidungArt.Elfmeterschiessen);
            erg.HeimToreVerlaengerung.Should().NotBeNull();
            erg.AuswaertsToreVerlaengerung.Should().NotBeNull();

            if (erg.EntscheidungArt == EntscheidungArt.Elfmeterschiessen)
            {
                erg.HeimToreVerlaengerung.Should().Be(erg.AuswaertsToreVerlaengerung,
                    "Elfmeterschießen wird nur erreicht, wenn die Verlängerung unentschieden endet");
                erg.HeimElfmetertore.Should().NotBeNull();
                erg.AuswaertsElfmetertore.Should().NotBeNull();
            }
        }
    }

    [Fact]
    public void Elfmeterschiessen_BrichtAbSobaldRechnerischEntschieden()
    {
        // Regression #235: Sobald der Torvorsprung mit den verbleibenden Versuchen des
        // zurückliegenden Teams nicht mehr aufholbar ist, dürfen keine weiteren Schüsse
        // mehr protokolliert werden — auch nicht innerhalb der ersten 5 Runden.
        var verfahren = new[] { Verfahren(VerfahrenArt.Elfmeterschiessen) };

        for (int seed = 0; seed < 500; seed++)
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), verfahren, true, new Random(seed));
            if (erg.EntscheidungArt != EntscheidungArt.Elfmeterschiessen) continue;

            var schuesse = erg.Ereignisse
                .Where(e => e.Art is EreignisArt.ElfmeterTor or EreignisArt.ElfmeterVerschossen)
                .OrderBy(e => e.Minute)
                .ToList();

            int h = 0, g = 0, heimSchuesse = 0, gastSchuesse = 0;
            for (int i = 0; i < schuesse.Count; i++)
            {
                var schuss = schuesse[i];
                bool tor = schuss.Art == EreignisArt.ElfmeterTor;
                if (schuss.IstHeim) { heimSchuesse++; if (tor) h++; }
                else { gastSchuesse++; if (tor) g++; }

                if (heimSchuesse > 5 || gastSchuesse > 5) continue;

                int heimRest = 5 - heimSchuesse;
                int gastRest = 5 - gastSchuesse;
                bool entschieden = h - g > gastRest || g - h > heimRest;

                if (entschieden)
                    i.Should().Be(schuesse.Count - 1,
                        "nach diesem Schuss war das Ergebnis bereits rechnerisch entschieden");
            }
        }
    }

    [Fact]
    public void EntscheidungsverfahrenAnwenden_MitElfmeterschiessenImmerEntschieden()
    {
        // Issue #237: aus Simulieren() extrahierte Methode — von der freien Hin-/Rückspiel-Simulation
        // direkt genutzt, um erst nach dem Aggregat beider Beine (statt blind pro Bein) zu entscheiden,
        // ob überhaupt eine Verlängerung/ein Elfmeterschießen fällig ist.
        var verfahren = new[] { Verfahren(VerfahrenArt.Elfmeterschiessen) };

        for (int seed = 0; seed < 200; seed++)
        {
            var (_, _, he, ge, art, _, _) =
                SimulationsEngine.EntscheidungsverfahrenAnwenden(Team(1), Team(2), verfahren, false, new Random(seed));

            art.Should().Be(EntscheidungArt.Elfmeterschiessen);
            he.Should().NotBeNull();
            ge.Should().NotBeNull();
            he.Should().NotBe(ge, "Elfmeterschießen darf nicht mit Gleichstand enden");
        }
    }

    [Fact]
    public void EntscheidungsverfahrenAnwenden_OhneVerfahrenBleibtRegulaereSpielzeitOhneTore()
    {
        var (hv, gv, he, ge, art, ereignisse, losGewinnerHeim) =
            SimulationsEngine.EntscheidungsverfahrenAnwenden(Team(1), Team(2), [], false, new Random(1));

        art.Should().Be(EntscheidungArt.RegulaereSpielzeit);
        hv.Should().BeNull();
        gv.Should().BeNull();
        he.Should().BeNull();
        ge.Should().BeNull();
        ereignisse.Should().BeEmpty();
        losGewinnerHeim.Should().BeNull();
    }

    [Theory]
    [InlineData(VerfahrenArt.Losentscheid, EntscheidungArt.Losentscheid)]
    [InlineData(VerfahrenArt.Muenzwurf, EntscheidungArt.Muenzwurf)]
    public void EntscheidungsverfahrenAnwenden_LosOderMuenze_LiefertGewinner(VerfahrenArt verfahrenArt, EntscheidungArt erwartet)
    {
        // Issue #383: der Los-/Münzsieger ging verloren — nur Art und (keine) Tore kamen zurück.
        var gewinner = Enumerable.Range(0, 50)
            .Select(seed => SimulationsEngine.EntscheidungsverfahrenAnwenden(
                Team(1), Team(2), [Verfahren(verfahrenArt)], false, new Random(seed)))
            .Select(e =>
            {
                e.Art.Should().Be(erwartet);
                return e.LosGewinnerHeim;
            })
            .ToList();

        gewinner.Should().NotContainNulls();
        gewinner.Should().Contain(true).And.Contain(false);
    }

    [Fact]
    public void EntscheidungsverfahrenAnwenden_Elfmeterschiessen_LiefertKeinenLosGewinner()
    {
        var e = SimulationsEngine.EntscheidungsverfahrenAnwenden(
            Team(1), Team(2), [Verfahren(VerfahrenArt.Elfmeterschiessen)], false, new Random(3));

        e.LosGewinnerHeim.Should().BeNull();
    }

    [Fact]
    public void StarkesTeamGewinntHaeufiger()
    {
        var stark  = Team(1, off: 95, mid: 90, def: 90, heim: 95, ausw: 90);
        var schwach = Team(2, off: 40, mid: 40, def: 40, heim: 60, ausw: 55);

        int starkGewinnt = 0, schwachGewinnt = 0;
        for (int seed = 0; seed < 500; seed++)
        {
            var erg = SimulationsEngine.Simulieren(stark, schwach, [], false, new Random(seed));
            if (erg.HeimToreRegulaer      > erg.AuswaertsToreRegulaer) starkGewinnt++;
            else if (erg.AuswaertsToreRegulaer > erg.HeimToreRegulaer)  schwachGewinnt++;
        }

        starkGewinnt.Should().BeGreaterThan(schwachGewinnt * 3,
            "Ein stark überlegenes Team soll mehr als dreimal so oft gewinnen");
    }

    // ── Neutraler Spielort (Issue #352) ────────────────────────────────────

    [Theory]
    [InlineData(false, new int[0],    Spielortvorteil.Heim)]
    [InlineData(false, new[] { 2 },   Spielortvorteil.Heim)]
    [InlineData(true,  new int[0],    Spielortvorteil.Neutral)]
    [InlineData(true,  new[] { 1 },   Spielortvorteil.Heim)]
    [InlineData(true,  new[] { 2 },   Spielortvorteil.Auswaerts)]
    [InlineData(true,  new[] { 1, 2 }, Spielortvorteil.Neutral)]
    [InlineData(true,  new[] { 3 },   Spielortvorteil.Neutral)]
    public void SpielortvorteilErmitteln_BeruecksichtigtNeutralenPlatzUndGastgeber(
        bool neutral, int[] gastgeber, Spielortvorteil erwartet)
    {
        SimulationsEngine.SpielortvorteilErmitteln(neutral, 1, 2, gastgeber).Should().Be(erwartet);
    }

    private static (double Heim, double Gast) MittlereTore(
        Mannschaft heim, Mannschaft gast, Spielortvorteil vorteil, int ueberraschungsfaktor = 0)
    {
        var ergebnisse = Enumerable.Range(0, 4000)
            .Select(seed => SimulationsEngine.Simulieren(heim, gast, [], false, new Random(seed), vorteil, ueberraschungsfaktor))
            .ToList();
        return (ergebnisse.Average(e => e.HeimToreRegulaer), ergebnisse.Average(e => e.AuswaertsToreRegulaer));
    }

    [Fact]
    public void NeutralerPlatz_GleichstarkeTeams_KeinHeimvorteil()
    {
        // Großer Abstand Heim-/Auswärtsstärke, damit ein verbliebener Heimvorteil deutlich auffiele.
        var (heimMitVorteil, gastMitVorteil) = MittlereTore(Team(1, heim: 100, ausw: 50), Team(2, heim: 100, ausw: 50), Spielortvorteil.Heim);
        var (heimNeutral, gastNeutral)       = MittlereTore(Team(1, heim: 100, ausw: 50), Team(2, heim: 100, ausw: 50), Spielortvorteil.Neutral);

        (heimMitVorteil - gastMitVorteil).Should().BeGreaterThan(0.4, "mit Heimvorteil trifft das Heimteam klar häufiger");
        Math.Abs(heimNeutral - gastNeutral).Should().BeLessThan(0.1, "auf neutralem Platz sind gleich starke Teams gleichauf");
    }

    [Fact]
    public void NeutralerPlatz_OrtsfaktorIstMittelwertAusHeimUndAuswaertsstaerke()
    {
        // Mittelwert von 100/50 = 75 — entspricht einem Team, das daheim wie auswärts 75 hat.
        var (heimNeutral, _) = MittlereTore(Team(1, heim: 100, ausw: 50), Team(2), Spielortvorteil.Neutral);
        var (heimReferenz, _) = MittlereTore(Team(1, heim: 75, ausw: 75), Team(2), Spielortvorteil.Heim);

        heimNeutral.Should().BeApproximately(heimReferenz, 0.0001);
    }

    [Fact]
    public void GastgeberAlsGast_BekommtHeimstaerke()
    {
        var gastgeber = Team(1, heim: 100, ausw: 50);
        var gegner    = Team(2, heim: 100, ausw: 50);

        var (gegnerTore, gastgeberTore) = MittlereTore(gegner, gastgeber, Spielortvorteil.Auswaerts);

        (gastgeberTore - gegnerTore).Should().BeGreaterThan(0.4, "der nominell als Gast angesetzte Gastgeber hat den Ortsvorteil");
    }

    // ── Überraschungsfaktor (Issue #360) ───────────────────────────────────

    private static double SiegquoteSchwach(int ueberraschungsfaktor)
    {
        var stark   = Team(1, off: 90, mid: 88, def: 86, heim: 75, ausw: 75);
        var schwach = Team(2, off: 55, mid: 55, def: 54, heim: 75, ausw: 75);
        return Enumerable.Range(0, 4000)
            .Select(seed => SimulationsEngine.Simulieren(schwach, stark, [], false, new Random(seed),
                Spielortvorteil.Heim, ueberraschungsfaktor))
            .Count(e => e.HeimToreRegulaer > e.AuswaertsToreRegulaer) / 4000.0;
    }

    [Fact]
    public void Ueberraschungsfaktor_AussenseiterGewinntMitSteigendemFaktorOefter()
    {
        double ohne = SiegquoteSchwach(0), mittel = SiegquoteSchwach(50), voll = SiegquoteSchwach(100);

        mittel.Should().BeGreaterThan(ohne);
        voll.Should().BeGreaterThan(mittel);
    }

    [Fact]
    public void Ueberraschungsfaktor100_StaerkeSpieltKeineRolle()
    {
        var stark   = Team(1, off: 90, mid: 88, def: 86, heim: 75, ausw: 75);
        var schwach = Team(2, off: 55, mid: 55, def: 54, heim: 75, ausw: 75);

        // Bei gleichem Ortsfaktor ist die Torerwartung beider Teams identisch — mit gleichem Seed liefert die
        // vertauschte Paarung dann exakt dasselbe Ergebnis.
        for (int seed = 0; seed < 50; seed++)
        {
            var starkDaheim   = SimulationsEngine.Simulieren(stark, schwach, [], false, new Random(seed), Spielortvorteil.Heim, 100);
            var schwachDaheim = SimulationsEngine.Simulieren(schwach, stark, [], false, new Random(seed), Spielortvorteil.Heim, 100);
            starkDaheim.Should().BeEquivalentTo(schwachDaheim);
        }
    }

    [Fact]
    public void Ueberraschungsfaktor0_EntsprichtStandard()
    {
        var mitNull = SimulationsEngine.Simulieren(Team(1, off: 90), Team(2), [], true, new Random(7), Spielortvorteil.Heim, 0);
        var standard = SimulationsEngine.Simulieren(Team(1, off: 90), Team(2), [], true, new Random(7));

        mitNull.Should().BeEquivalentTo(standard);
    }

    [Fact]
    public void SilverGoal_NurBeiEntscheidungNachDerErstenHaelfteDerVerlaengerung()
    {
        // #391: Führt nach 105 Minuten niemand, wird weitergespielt — ein Sieg in der zweiten Hälfte ist ein
        // gewöhnlicher Sieg nach Verlängerung, kein Silver Goal.
        var verfahren = new[]
        {
            Verfahren(VerfahrenArt.SilverGoal, rang: 1),
            Verfahren(VerfahrenArt.Elfmeterschiessen, rang: 2)
        };
        int silverGoals = 0, verlaengerungen = 0;

        for (int seed = 0; seed < 2000; seed++)
        {
            var erg = SimulationsEngine.Simulieren(Team(1), Team(2), verfahren, true, new Random(seed));
            var ersteHaelfte = VerlaengerungsTore(erg, 91, 105);
            var zweiteHaelfte = VerlaengerungsTore(erg, 106, 120);

            if (erg.EntscheidungArt == EntscheidungArt.SilverGoal)
            {
                silverGoals++;
                ersteHaelfte.Heim.Should().NotBe(ersteHaelfte.Auswaerts, "Silver Goal heißt: nach 105 Minuten führt einer");
                (zweiteHaelfte.Heim + zweiteHaelfte.Auswaerts).Should().Be(0, "nach einem Silver Goal endet das Spiel");
            }
            else if (erg.EntscheidungArt == EntscheidungArt.Verlaengerung)
            {
                verlaengerungen++;
                ersteHaelfte.Heim.Should().Be(ersteHaelfte.Auswaerts, "sonst wäre es ein Silver Goal");
                zweiteHaelfte.Heim.Should().NotBe(zweiteHaelfte.Auswaerts, "die zweite Hälfte hat entschieden");
            }
        }

        silverGoals.Should().BePositive();
        verlaengerungen.Should().BePositive();
    }

    private static (int Heim, int Auswaerts) VerlaengerungsTore(SimulationsErgebnis erg, int von, int bis)
    {
        var tore = erg.Ereignisse.Where(e => e.Art == EreignisArt.Tor && e.Minute >= von && e.Minute <= bis).ToList();
        return (tore.Count(e => e.IstHeim), tore.Count(e => !e.IstHeim));
    }
}
