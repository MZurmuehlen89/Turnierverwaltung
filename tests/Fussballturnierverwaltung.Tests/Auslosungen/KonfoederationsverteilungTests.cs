using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Application.Zufall;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>
/// Auslosungsregel „Konföderationsverteilung“ und die Gruppenauslosung nach FIFA-Verfahren (Issue #356): Grenzen je
/// Verband, Ziehung ohne Sackgassen, Vorgaben der Runde beim Anlegen einer Auslosung.
/// </summary>
public class KonfoederationsverteilungTests
{
    private const int WIEDERHOLUNGEN = 60;
    private const int UEFA = 1, CONMEBOL = 2, CONCACAF = 3, CAF = 4, AFC = 5, OFC = 6;

    private readonly FakeAuslosungRepository _repo = new();
    private readonly FakeTurnierRundeRepository _runden = new();
    private readonly AuslosungService _service;
    private int _naechsteId = 1;

    public KonfoederationsverteilungTests()
    {
        _service = AuslosungDienste.Service(_repo, runden: _runden);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    // Jedes Team bekommt ein eigenes Land im angegebenen Verband.
    private LostopfEintrag Team(int verbandId, int? landId = null, int? setzPos = null)
    {
        int id = _naechsteId++;
        var land = new Land { Id = landId ?? 1000 + id, VerbandId = verbandId };
        var m = new Mannschaft { Id = id, Name = $"Team {id}", LandId = land.Id, Land = land };
        return new LostopfEintrag { Id = id, MannschaftId = id, Mannschaft = m, SetzPosition = setzPos };
    }

    private LostopfEintrag Platzhalter(string name) => new() { Id = _naechsteId++, PlatzhalterName = name };

    private LostopfEintrag[] Teams(params (int Verband, int Anzahl)[] verteilung) =>
        [.. verteilung.SelectMany(v => Enumerable.Range(0, v.Anzahl).Select(_ => Team(v.Verband)))];

    private static Lostopf Topf(int reihenfolge, params LostopfEintrag[] eintraege) =>
        new() { Id = reihenfolge, Reihenfolge = reihenfolge, Name = $"Topf {reihenfolge}", Eintraege = [.. eintraege] };

    private static AuslosungsRegel Regel(AuslosungsRegelArt art) =>
        new() { Regelart = art, Beschreibung = art.ToString(), IstAktiv = true };

    private async Task<List<List<LostopfEintrag>>> GruppenAuslosen(
        Auslosungsart art, int gruppenAnzahl, Lostopf[] toepfe, params AuslosungsRegelArt[] regeln)
    {
        var auslosung = new Auslosung
        {
            Id = 1,
            TurnierRunde = new TurnierRunde { Id = 1, Name = "Gruppenphase", Rundenart = Rundenart.Gruppenphase, GruppenAnzahl = gruppenAnzahl, RegelwerkId = 1 },
            Auslosungsart = art, Status = AuslosungsStatus.Vorbereitung
        };
        foreach (var t in toepfe) auslosung.Lostoepfe.Add(t);
        foreach (var r in regeln) auslosung.Regeln.Add(Regel(r));
        _repo.Speichern(auslosung);

        var ergebnisse = await _service.AusfuehrenAsync(1);

        var alle = toepfe.SelectMany(t => t.Eintraege).ToList();
        return ergebnisse.GroupBy(e => e.PaarungsNummer).OrderBy(g => g.Key)
            .Select(g => g.Select(e => alle.Single(x =>
                e.HeimMannschaftId is { } id ? x.MannschaftId == id : x.PlatzhalterName == e.HeimPlatzhalterName)).ToList())
            .ToList();
    }

    private static int AnzahlAusVerband(IEnumerable<LostopfEintrag> gruppe, int verbandId) =>
        gruppe.Count(e => e.Mannschaft?.Land?.VerbandId == verbandId);

    // Töpfe wie bei der WM 2026: 16 UEFA, 6 CONMEBOL, 6 CONCACAF, 9 CAF, 8 AFC, 1 OFC und zwei noch offene
    // Plätze der interkontinentalen Playoffs.
    private Lostopf[] WmToepfe2026() =>
    [
        Topf(1, Teams((UEFA, 7), (CONMEBOL, 2), (CONCACAF, 3))),
        Topf(2, Teams((UEFA, 3), (CONMEBOL, 3), (CAF, 2), (AFC, 4))),
        Topf(3, Teams((UEFA, 2), (CONMEBOL, 1), (CONCACAF, 1), (CAF, 5), (AFC, 3))),
        Topf(4, [.. Teams((UEFA, 4), (CAF, 2), (CONCACAF, 2), (AFC, 1), (OFC, 1)),
                 Platzhalter("Playoff-Sieger 1"), Platzhalter("Playoff-Sieger 2")])
    ];

    // ══════════════════════════════════════════════════════════════════════════
    // GRENZEN
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(16, 12, 1, 2)] // WM 2026: UEFA
    [InlineData(14, 8, 1, 2)]  // WM 2018: UEFA
    [InlineData(13, 8, 1, 2)]  // WM 2022: UEFA
    [InlineData(13, 6, 2, 3)]  // WM 1994: UEFA — „mindestens zwei, eine Gruppe mit drei“
    [InlineData(6, 12, 0, 1)]  // WM 2026: CONMEBOL
    [InlineData(8, 8, 1, 1)]
    public void Grenzen_ErgebenDieFifaVorgabenOhneEinstellbareWerte(int teams, int gruppen, int min, int max)
    {
        var grenzen = new KonfoederationsGrenzen(Enumerable.Repeat<int?>(UEFA, teams), gruppen);

        grenzen.Min(UEFA).Should().Be(min);
        grenzen.Max(UEFA).Should().Be(max);
    }

    [Fact]
    public void Grenzen_TeilnehmerOhneVerband_ZaehlenNichtMit()
    {
        var grenzen = new KonfoederationsGrenzen([UEFA, UEFA, null, null], gruppenAnzahl: 2);

        grenzen.Verbaende.Should().BeEquivalentTo([UEFA]);
        grenzen.Max(UEFA).Should().Be(1);
        grenzen.Min(CAF).Should().Be(0, "ein nicht vertretener Verband hat keine Mindestanzahl");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AUSLOSUNG
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NachToepfen_WmToepfe_JedeGruppeEinBisZweiUefaUndSonstHoechstensEinTeamJeVerband()
    {
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var toepfe = WmToepfe2026();

            var gruppen = await GruppenAuslosen(Auslosungsart.NachToepfen, 12, toepfe, AuslosungsRegelArt.Konfoederationsverteilung);

            gruppen.Should().HaveCount(12);
            gruppen.Should().AllSatisfy(gruppe =>
            {
                gruppe.Should().HaveCount(4);
                AnzahlAusVerband(gruppe, UEFA).Should().BeInRange(1, 2);
                foreach (int verband in new[] { CONMEBOL, CONCACAF, CAF, AFC, OFC })
                    AnzahlAusVerband(gruppe, verband).Should().BeLessThanOrEqualTo(1);
                foreach (var topf in toepfe)
                    gruppe.Count(topf.Eintraege.Contains).Should().Be(1, "aus jedem Topf genau ein Team je Gruppe");
            });
        }
    }

    [Fact]
    public async Task NachToepfen_OhneRegel_BleibtFreiVerteilt()
    {
        // Gegenprobe: ohne die Regel kommen über viele Läufe Gruppen mit drei UEFA-Teams oder ganz ohne vor.
        bool verstossGesehen = false;
        for (int lauf = 0; lauf < WIEDERHOLUNGEN && !verstossGesehen; lauf++)
        {
            var gruppen = await GruppenAuslosen(Auslosungsart.NachToepfen, 12, WmToepfe2026());
            verstossGesehen = gruppen.Any(g => AnzahlAusVerband(g, UEFA) is 0 or > 2);
        }

        verstossGesehen.Should().BeTrue();
    }

    [Fact]
    public async Task Zufall_MindestUndHoechstanzahlJeVerbandWerdenEingehalten()
    {
        // 5 Teams aus A und 3 aus B auf 2 Gruppen → A je Gruppe 2–3, B je Gruppe 1–2
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var gruppen = await GruppenAuslosen(Auslosungsart.Zufall, 2,
                [Topf(1, Teams((UEFA, 5), (CAF, 3)))], AuslosungsRegelArt.Konfoederationsverteilung);

            gruppen.Should().AllSatisfy(gruppe =>
            {
                gruppe.Should().HaveCount(4);
                AnzahlAusVerband(gruppe, UEFA).Should().BeInRange(2, 3);
                AnzahlAusVerband(gruppe, CAF).Should().BeInRange(1, 2);
            });
        }
    }

    [Fact]
    public async Task NachSetzliste_GesetzteZaehlenBeiDerVerteilungMit()
    {
        // 4 gesetzte UEFA-Gruppenköpfe + 4 weitere UEFA-Teams und 8 andere auf 4 Gruppen → je Gruppe genau 2 UEFA
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var gesetzte = Enumerable.Range(1, 4).Select(pos => Team(UEFA, setzPos: pos)).ToArray();
            var toepfe = new[]
            {
                Topf(1, gesetzte),
                Topf(2, Teams((UEFA, 2), (CAF, 2))),
                Topf(3, Teams((UEFA, 2), (AFC, 2))),
                Topf(4, Teams((CONMEBOL, 4)))
            };

            var gruppen = await GruppenAuslosen(Auslosungsart.NachSetzliste, 4, toepfe, AuslosungsRegelArt.Konfoederationsverteilung);

            gruppen.Should().AllSatisfy(gruppe =>
            {
                gruppe.Should().HaveCount(4);
                gruppe.Count(gesetzte.Contains).Should().Be(1);
                AnzahlAusVerband(gruppe, UEFA).Should().Be(2);
                AnzahlAusVerband(gruppe, CONMEBOL).Should().Be(1);
            });
        }
    }

    [Fact]
    public async Task NachToepfen_KeinGleichesLand_FaehrtSichNichtFest()
    {
        // Liegen die Teams aus Land 1 (Topf 1) und Land 4 (Topf 2) in derselben Gruppe, passt aus Topf 3 (Land 1,
        // 4, 4) keines mehr dorthin. Das frühere Mischen je Topf lief in jedem dritten Fall in diese Sackgasse
        // und meldete „nicht erfüllbar“ — die Vorausschau überspringt die Gruppe schon bei Topf 2.
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var toepfe = new[]
            {
                Topf(1, Team(UEFA, landId: 1), Team(UEFA, landId: 2), Team(UEFA, landId: 3)),
                Topf(2, Team(UEFA, landId: 4), Team(UEFA, landId: 5), Team(UEFA, landId: 6)),
                Topf(3, Team(UEFA, landId: 1), Team(UEFA, landId: 4), Team(UEFA, landId: 4))
            };

            var gruppen = await GruppenAuslosen(Auslosungsart.NachToepfen, 3, toepfe, AuslosungsRegelArt.KeinGleichesLand);

            gruppen.Should().AllSatisfy(gruppe =>
                gruppe.Select(e => e.Mannschaft!.LandId).Should().OnlyHaveUniqueItems());
        }
    }

    [Fact]
    public async Task NichtErfuellbar_MeldetDieBeteiligtenRegeln()
    {
        // Drei Teams aus demselben Land passen nicht in zwei Gruppen.
        var toepfe = new[] { Topf(1, Team(UEFA, landId: 1), Team(UEFA, landId: 1), Team(UEFA, landId: 1), Team(CAF)) };

        var act = () => GruppenAuslosen(Auslosungsart.Zufall, 2, toepfe,
            AuslosungsRegelArt.KeinGleichesLand, AuslosungsRegelArt.Konfoederationsverteilung);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*'Kein gleiches Land'*'Konföderationsverteilung'*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VORGABEN DER RUNDE
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Erstellen_UebernimmtDieVorgegebenenRegelnDerRundeAlsAktiv()
    {
        _runden.Speichern(new TurnierRunde
        {
            Id = 7, TurnierInstanzId = 5, Rundenart = Rundenart.Gruppenphase, GruppenAnzahl = 8,
            VorgegebeneAuslosungsregeln = [AuslosungsRegelArt.Konfoederationsverteilung, AuslosungsRegelArt.KeinGleichesLand]
        });

        var auslosung = await _service.ErstellenAsync(
            new AuslosungErstellenEingabe(5, 7, "Gruppenphase", Auslosungsart.NachToepfen, IstAutomatisch: false));

        auslosung.Regeln.Should().OnlyContain(r => r.IstAktiv);
        auslosung.Regeln.Select(r => r.Regelart).Should().BeEquivalentTo(
            [AuslosungsRegelArt.Konfoederationsverteilung, AuslosungsRegelArt.KeinGleichesLand]);
        auslosung.Regeln.Single(r => r.Regelart == AuslosungsRegelArt.Konfoederationsverteilung)
            .Beschreibung.Should().Be("Konföderationsverteilung");
    }

    [Fact]
    public async Task Erstellen_UebernimmtDieVorgegebeneGruppenpositionDerRunde()
    {
        _runden.Speichern(new TurnierRunde
        {
            Id = 7, TurnierInstanzId = 5, Rundenart = Rundenart.Gruppenphase, GruppenAnzahl = 8,
            VorgegebeneGruppenposition = Gruppenposition.Topf1Fest
        });

        var auslosung = await _service.ErstellenAsync(
            new AuslosungErstellenEingabe(5, 7, "Gruppenphase", Auslosungsart.NachToepfen, IstAutomatisch: false));

        auslosung.Gruppenposition.Should().Be(Gruppenposition.Topf1Fest);
    }

    [Fact]
    public async Task Erstellen_OhneVorgabe_LegtKeineRegelnAn()
    {
        _runden.Speichern(new TurnierRunde { Id = 7, TurnierInstanzId = 5, Rundenart = Rundenart.KoRunde });

        var auslosung = await _service.ErstellenAsync(
            new AuslosungErstellenEingabe(5, 7, "Achtelfinale", Auslosungsart.Zufall, IstAutomatisch: false));

        auslosung.Regeln.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // REGEL-MATRIX
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(Auslosungsart.Zufall)]
    [InlineData(Auslosungsart.NachToepfen)]
    [InlineData(Auslosungsart.NachSetzliste)]
    public void Matrix_InDerGruppenphaseWirksam(Auslosungsart art) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(
                AuslosungsRegelArt.Konfoederationsverteilung, art, Rundenart.Gruppenphase, folgtAufGruppenphase: false)
            .Should().BeNull();

    [Theory]
    [InlineData(Auslosungsart.Zufall, Rundenart.KoRunde)]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.Finale)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.Zufall, Rundenart.Liga)]
    public void Matrix_AusserhalbDerGruppenauslosungGesperrtMitBegruendung(Auslosungsart art, Rundenart rundenart) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(
                AuslosungsRegelArt.Konfoederationsverteilung, art, rundenart, folgtAufGruppenphase: true)
            .Should().NotBeNullOrWhiteSpace();

    [Fact]
    public void AlsVorgabeMoeglich_RichtetSichNachRundenartUndVorgegebenerAuslosungsart()
    {
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            AuslosungsRegelArt.Konfoederationsverteilung, Rundenart.Gruppenphase, null, false, false).Should().BeTrue();
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            AuslosungsRegelArt.Konfoederationsverteilung, Rundenart.KoRunde, null, true, false).Should().BeFalse();
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            AuslosungsRegelArt.HeimrechtNachLos, Rundenart.KoRunde, null, false, false).Should().BeTrue();
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            AuslosungsRegelArt.HeimrechtNachLos, Rundenart.KoRunde, Auslosungsart.TopfZuordnung, false, false)
            .Should().BeFalse("die Topf-Zuordnung verteilt das Heimrecht selbst");
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            AuslosungsRegelArt.KeineGleicheGruppe, Rundenart.KoRunde, null, folgtAufGruppenphase: false, paarungFestVorgegeben: false).Should().BeFalse();
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            AuslosungsRegelArt.KeinGleichesLand, Rundenart.Liga, null, false, false).Should().BeFalse();
    }

    // #463: Eine fest vorgegebene Runde paart Platzhalter in der Vorbelegung — dort wirkt keine Regel.
    [Theory]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand)]
    [InlineData(AuslosungsRegelArt.KeineGleicheGruppe)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos)]
    public void AlsVorgabeMoeglich_BeiFestVorgegebenerPaarung_Nie(AuslosungsRegelArt regel) =>
        AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
                regel, Rundenart.KoRunde, null, folgtAufGruppenphase: true, paarungFestVorgegeben: true)
            .Should().BeFalse();
}
