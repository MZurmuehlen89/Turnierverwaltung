using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>
/// Wirkung der Auslosungsregeln (Issue #331): „Keine gleiche Gruppe“ (aus Platzhaltern und aus einer
/// vorangegangenen Gruppen-Auslosung), die implizite Gruppenregel bei „Gruppensieger vs. Zweite“,
/// „Heimrecht nach Setzliste“, Regeln bei „Nach Setzliste“ und gesperrte Regeln.
/// </summary>
public class AuslosungsRegelnTests
{
    private const int INSTANZ_ID = 5;
    private const int WIEDERHOLUNGEN = 40;

    private readonly FakeAuslosungRepository _repo = new();
    private readonly FakeTurnierRundeRepository _runden = new();
    private readonly FakeRanglisteRepository _ranglisten = new();
    private readonly AuslosungService _service;

    public AuslosungsRegelnTests()
    {
        _service = AuslosungDienste.Service(_repo, runden: _runden, ranglisten: _ranglisten);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static Mannschaft Team(int id, int? landId = null) => new() { Id = id, Name = $"Team {id}", LandId = landId ?? id };

    private static LostopfEintrag Eintrag(Mannschaft m, int? setzPos = null) =>
        new() { Id = m.Id, MannschaftId = m.Id, Mannschaft = m, SetzPosition = setzPos };

    private static LostopfEintrag Platzhalter(int id, string name) => new() { Id = id, PlatzhalterName = name };

    private static Lostopf Topf(int reihenfolge, params LostopfEintrag[] eintraege) =>
        new() { Id = reihenfolge, Reihenfolge = reihenfolge, Name = $"Topf {reihenfolge}", Eintraege = [.. eintraege] };

    private static AuslosungsRegel Regel(AuslosungsRegelArt art) =>
        new() { Regelart = art, Beschreibung = art.ToString(), IstAktiv = true };

    private static IEnumerable<string> Gruppen(string von, string bis) =>
        Enumerable.Range(von[0], bis[0] - von[0] + 1).Select(c => ((char)c).ToString());

    private async Task<IReadOnlyList<AuslosungsErgebnis>> Ausfuehren(
        Rundenart rundenart, Auslosungsart art, IEnumerable<Lostopf> toepfe,
        IEnumerable<AuslosungsRegel>? regeln = null, bool mitRueckspiel = false,
        int rundeId = 1, int auslosungId = 1)
    {
        var runde = _runden.FindenNachIdAsync(rundeId).Result
            ?? new TurnierRunde { Id = rundeId, Name = "Runde", Rundenart = rundenart, RegelwerkId = 1 };
        runde.MitRueckspiel = mitRueckspiel;

        var auslosung = new Auslosung
        {
            Id = auslosungId, TurnierInstanzId = INSTANZ_ID, TurnierRundeId = rundeId, TurnierRunde = runde,
            Auslosungsart = art, Status = AuslosungsStatus.Vorbereitung
        };
        foreach (var t in toepfe) auslosung.Lostoepfe.Add(t);
        foreach (var r in regeln ?? []) auslosung.Regeln.Add(r);
        _repo.Speichern(auslosung);

        return await _service.AusfuehrenAsync(auslosungId);
    }

    private static string? Gruppe(string? platzhalter) => GruppenHerkunft.AusPlatzhalter(platzhalter)?.Gruppe;

    // ══════════════════════════════════════════════════════════════════════════
    // KEINE GLEICHE GRUPPE
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GruppensiegerGegenZweite_PaartNieMitDemZweitenDerEigenenGruppe_AuchOhneHaekchen()
    {
        // Nations-League-Viertelfinale: 4 Sieger gegen 4 Zweite, eigene Gruppe real ausgeschlossen
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var sieger = Gruppen("A", "D").Select((g, i) => Platzhalter(i + 1, $"1. Gruppe {g}")).ToArray();
            var zweite = Gruppen("A", "D").Select((g, i) => Platzhalter(i + 11, $"2. Gruppe {g}")).ToArray();

            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite,
                [Topf(1, sieger), Topf(2, zweite)]);

            ergebnisse.Should().HaveCount(4);
            ergebnisse.Should().AllSatisfy(e =>
                Gruppe(e.HeimPlatzhalterName).Should().NotBe(Gruppe(e.AuswaertsPlatzhalterName)));
        }
    }

    [Fact]
    public async Task Zufall_KeineGleicheGruppe_PaartKeineTeamsAusDerselbenGruppe()
    {
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var eintraege = Gruppen("A", "D")
                .SelectMany((g, i) => new[] { Platzhalter(i * 2 + 1, $"1. Gruppe {g}"), Platzhalter(i * 2 + 2, $"2. Gruppe {g}") })
                .ToArray();

            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.Zufall, [Topf(1, eintraege)],
                [Regel(AuslosungsRegelArt.KeineGleicheGruppe)]);

            ergebnisse.Should().HaveCount(4);
            ergebnisse.Should().AllSatisfy(e =>
                Gruppe(e.HeimPlatzhalterName).Should().NotBe(Gruppe(e.AuswaertsPlatzhalterName)));
        }
    }

    [Fact]
    public async Task NachToepfen_KeineGleicheGruppe_ErmitteltGruppeEchterMannschaftenAusDerGruppenAuslosung()
    {
        // Gruppenphase (Runde 10): Team 1+5 in Gruppe A, 2+6 in B, 3+7 in C, 4+8 in D
        _runden.Speichern(
            new TurnierRunde { Id = 10, TurnierInstanzId = INSTANZ_ID, Reihenfolge = 1, Rundenart = Rundenart.Gruppenphase, GruppenAnzahl = 4, RegelwerkId = 1 },
            new TurnierRunde { Id = 11, TurnierInstanzId = INSTANZ_ID, Reihenfolge = 2, Rundenart = Rundenart.KoRunde, RegelwerkId = 1 });
        var gruppenAuslosung = new Auslosung
        {
            Id = 99, TurnierInstanzId = INSTANZ_ID, TurnierRundeId = 10, Status = AuslosungsStatus.Abgeschlossen,
            Auslosungsart = Auslosungsart.Zufall
        };
        for (int team = 1; team <= 8; team++)
            gruppenAuslosung.Ergebnisse.Add(new AuslosungsErgebnis { PaarungsNummer = (team - 1) % 4 + 1, HeimMannschaftId = team });
        _repo.Speichern(gruppenAuslosung);

        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachToepfen,
                [Topf(1, [.. Enumerable.Range(1, 4).Select(i => Eintrag(Team(i)))]),
                 Topf(2, [.. Enumerable.Range(5, 4).Select(i => Eintrag(Team(i)))])],
                [Regel(AuslosungsRegelArt.KeineGleicheGruppe)], rundeId: 11);

            ergebnisse.Should().HaveCount(4);
            ergebnisse.Should().AllSatisfy(e =>
                ((e.HeimMannschaftId!.Value - 1) % 4).Should().NotBe((e.AuswaertsMannschaftId!.Value - 1) % 4,
                    "Teams aus derselben Gruppe dürfen sich nicht erneut begegnen"));
        }
    }

    [Fact]
    public async Task KeineGleicheGruppe_Unerfuellbar_WirftVerstaendlicheMeldung()
    {
        var eintraege = new[] { Platzhalter(1, "1. Gruppe A"), Platzhalter(2, "2. Gruppe A") };

        var aktion = () => Ausfuehren(Rundenart.KoRunde, Auslosungsart.Zufall, [Topf(1, eintraege)],
            [Regel(AuslosungsRegelArt.KeineGleicheGruppe)]);

        await aktion.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Keine gleiche Gruppe*");
    }

    [Fact]
    public async Task Gruppenphase_KeineGleicheGruppe_VerteiltTeamsDerselbenVorgruppeAufVerschiedeneGruppen()
    {
        // Zweite Gruppenphase: je 2 Teams aus Vorgruppe A–D auf 2 neue Gruppen
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var eintraege = Gruppen("A", "D")
                .SelectMany((g, i) => new[] { Platzhalter(i * 2 + 1, $"1. Gruppe {g}"), Platzhalter(i * 2 + 2, $"2. Gruppe {g}") })
                .ToArray();
            _runden.Speichern(new TurnierRunde { Id = 20 + lauf, Rundenart = Rundenart.Gruppenphase, GruppenAnzahl = 2, RegelwerkId = 1 });

            var ergebnisse = await Ausfuehren(Rundenart.Gruppenphase, Auslosungsart.Zufall, [Topf(1, eintraege)],
                [Regel(AuslosungsRegelArt.KeineGleicheGruppe)], rundeId: 20 + lauf);

            ergebnisse.GroupBy(e => e.PaarungsNummer).Should().AllSatisfy(neueGruppe =>
                neueGruppe.Select(e => Gruppe(e.HeimPlatzhalterName)).Should().OnlyHaveUniqueItems());
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HEIMRECHT NACH SETZLISTE
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HeimrechtNachSetzliste_MitRueckspiel_HoehererTopfSpieltRueckspielZuHause()
    {
        var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachToepfen,
            [Topf(1, [.. Enumerable.Range(1, 4).Select(i => Eintrag(Team(i)))]),
             Topf(2, [.. Enumerable.Range(5, 4).Select(i => Eintrag(Team(i)))])],
            [Regel(AuslosungsRegelArt.HeimrechtNachSetzliste)], mitRueckspiel: true);

        // Das Rückspiel wird aus dem Hinspiel gespiegelt — der Topf-1-Klub steht im Hinspiel auswärts
        ergebnisse.Should().AllSatisfy(e => e.AuswaertsMannschaftId.Should().BeLessThanOrEqualTo(4));
    }

    [Fact]
    public async Task HeimrechtNachSetzliste_OhneRueckspiel_HoehererTopfHatHeimrecht()
    {
        var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachToepfen,
            [Topf(1, [.. Enumerable.Range(1, 4).Select(i => Eintrag(Team(i)))]),
             Topf(2, [.. Enumerable.Range(5, 4).Select(i => Eintrag(Team(i)))])],
            [Regel(AuslosungsRegelArt.HeimrechtNachSetzliste)]);

        ergebnisse.Should().AllSatisfy(e => e.HeimMannschaftId.Should().BeLessThanOrEqualTo(4));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HEIMRECHT FÜR UNTERKLASSIGE (#393)
    // ══════════════════════════════════════════════════════════════════════════

    // Ligapyramide als Standard-Rangliste 7: Ids 1–4 Bundesliga (Klasse 1), Ids 5–8 Regionalliga (Klasse 4).
    private void PyramideAnlegen(int? profigrenze)
    {
        _ranglisten.Speichern(new Rangliste
        {
            Id = 7, Name = "Ligapyramide", ProfigrenzeSpielklasse = profigrenze,
            Abschnitte =
            [
                new RanglistenAbschnitt { Name = "Bundesliga", Spielklasse = 1, BisPlatz = 4 },
                new RanglistenAbschnitt { Name = "Regionalliga", Spielklasse = 4, BisPlatz = 8 }
            ],
            Eintraege = [.. Enumerable.Range(1, 8).Select(i => new RanglistenEintrag { MannschaftId = i, Rang = i })]
        });
        _runden.Speichern(new TurnierRunde
        {
            Id = 1, Name = "1. Runde", Rundenart = Rundenart.KoRunde, RegelwerkId = 1, StandardRanglisteId = 7
        });
    }

    [Fact]
    public async Task HeimrechtFuerUnterklassige_SpielklasseAusStandardRangliste_RegionalligistSpieltZuHause()
    {
        PyramideAnlegen(profigrenze: 2);

        for (int i = 0; i < WIEDERHOLUNGEN; i++)
        {
            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachToepfen,
                [Topf(1, [.. Enumerable.Range(1, 4).Select(id => Eintrag(Team(id)))]),
                 Topf(2, [.. Enumerable.Range(5, 4).Select(id => Eintrag(Team(id)))])],
                [Regel(AuslosungsRegelArt.HeimrechtFuerUnterklassige)]);

            ergebnisse.Should().AllSatisfy(e => e.HeimMannschaftId.Should().BeGreaterThanOrEqualTo(5));
        }
    }

    [Fact]
    public async Task HeimrechtFuerUnterklassige_RundeOhneStandardRangliste_EntscheidetDasLos()
    {
        var heimIds = new List<int>();
        for (int i = 0; i < WIEDERHOLUNGEN; i++)
        {
            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachToepfen,
                [Topf(1, [.. Enumerable.Range(1, 4).Select(id => Eintrag(Team(id)))]),
                 Topf(2, [.. Enumerable.Range(5, 4).Select(id => Eintrag(Team(id)))])],
                [Regel(AuslosungsRegelArt.HeimrechtFuerUnterklassige)]);
            heimIds.AddRange(ergebnisse.Select(e => e.HeimMannschaftId!.Value));
        }

        heimIds.Should().Contain(id => id <= 4).And.Contain(id => id >= 5);
    }

    [Fact]
    public async Task HeimrechtNachSetzliste_EinTopf_BessererGruppenplatzHatHeimrecht()
    {
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            var eintraege = Gruppen("A", "D")
                .SelectMany((g, i) => new[] { Platzhalter(i * 2 + 1, $"1. Gruppe {g}"), Platzhalter(i * 2 + 2, $"2. Gruppe {g}") })
                .ToArray();

            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.Zufall, [Topf(1, eintraege)],
                [Regel(AuslosungsRegelArt.HeimrechtNachSetzliste)]);

            foreach (var e in ergebnisse)
            {
                int heimPlatz = GruppenHerkunft.AusPlatzhalter(e.HeimPlatzhalterName)!.Value.Platz;
                int gastPlatz = GruppenHerkunft.AusPlatzhalter(e.AuswaertsPlatzhalterName)!.Value.Platz;
                heimPlatz.Should().BeLessThanOrEqualTo(gastPlatz);
            }
        }
    }

    [Fact]
    public async Task NachSetzliste_HeimrechtNachSetzliste_MitRueckspiel_GesetzterSpieltRueckspielZuHause()
    {
        var gesetzte   = Enumerable.Range(1, 4).Select(i => Eintrag(Team(i), setzPos: i));
        var ungesetzte = Enumerable.Range(5, 4).Select(i => Eintrag(Team(i)));

        var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachSetzliste,
            [Topf(1, [.. gesetzte, .. ungesetzte])],
            [Regel(AuslosungsRegelArt.HeimrechtNachSetzliste)], mitRueckspiel: true);

        ergebnisse.Should().HaveCount(4);
        ergebnisse.Should().AllSatisfy(e => e.AuswaertsMannschaftId.Should().BeLessThanOrEqualTo(4));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NACH SETZLISTE BEACHTET EINSCHRÄNKUNGEN
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NachSetzliste_KeinGleichesLand_PaartGesetzteNieMitUngesetztenAusDemselbenLand()
    {
        for (int lauf = 0; lauf < WIEDERHOLUNGEN; lauf++)
        {
            // Gesetzte 1–3 aus Land 100, 200, 300; Ungesetzte 4–6 ebenfalls — nur die „Rotation“ ist gültig
            var gesetzte   = new[] { Eintrag(Team(1, 100), 1), Eintrag(Team(2, 200), 2), Eintrag(Team(3, 300), 3) };
            var ungesetzte = new[] { Eintrag(Team(4, 100)), Eintrag(Team(5, 200)), Eintrag(Team(6, 300)) };

            var ergebnisse = await Ausfuehren(Rundenart.KoRunde, Auslosungsart.NachSetzliste,
                [Topf(1, [.. gesetzte, .. ungesetzte])], [Regel(AuslosungsRegelArt.KeinGleichesLand)]);

            ergebnisse.Should().HaveCount(3);
            ergebnisse.Should().AllSatisfy(e =>
                (e.HeimMannschaftId!.Value % 3).Should().NotBe(e.AuswaertsMannschaftId!.Value % 3));
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GESPERRTE REGELN WIRKEN NICHT
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Liga_KeinGleichesLand_WirdIgnoriert_StattAuslosungAbzubrechen()
    {
        // Liga = eine Gruppe, alle aus demselben Land — früher scheiterte die Auslosung daran
        var eintraege = Enumerable.Range(1, 4).Select(i => Eintrag(Team(i, landId: 7))).ToArray();

        var ergebnisse = await Ausfuehren(Rundenart.Liga, Auslosungsart.Zufall, [Topf(1, eintraege)],
            [Regel(AuslosungsRegelArt.KeinGleichesLand)]);

        ergebnisse.Should().HaveCount(4);
    }
}
