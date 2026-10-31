using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Rundenart „Topf-Liga“ (#560), an der Ligaphase der UEFA-Klubwettbewerbe: Champions League (4 Töpfe à 9,
/// je 2 Gegner aus jedem Topf) und Conference League (6 Töpfe à 6, je 1 Gegner). Die Vorlagen geben „Kein gleiches
/// Land“ vor: kein Gegner aus dem eigenen Land, höchstens zwei aus demselben Land. Nur ausgelost, nicht simuliert –
/// das Rechnen der Tabelle deckt <see cref="LigaTests"/> ab. Geprüft werden Invarianten, keine festen Werte.</summary>
public sealed class TopfLigaTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string VARIANTE = "36 Teams · ab 2024/25";
    private const string LIGAPHASE = "Ligaphase";
    private const int TEAMS = 36;
    private const int MAX_GEGNER_JE_LAND = 2;

    /// <summary>36 Vereine aus sieben Ländern, reihum in die Rangliste: Bei höchstens zwei Gegnern je Land braucht
    /// ein Team in der Champions League mindestens vier fremde Länder. Reihum hat jeder Topf höchstens zwei Vereine
    /// eines Landes, die Auslosung bleibt so gut lösbar.</summary>
    private static readonly (string Land, int Anzahl)[] _laender =
    [
        ("England", 6), ("Deutschland", 6), ("Italien", 6), ("Spanien", 6), ("Frankreich", 6),
        ("Portugal", 3), ("Niederlande", 3),
    ];

    [Theory]
    [InlineData("UEFA Champions League", 4, 2)]
    [InlineData("UEFA Conference League", 6, 1)]
    // Kurzer Name: Mit den Theory-Parametern wird er Teil des Belegpfads, der auf dem CI-Runner sonst die 260 Zeichen
    // überschreitet (wie in #537)
    public async Task Ligaphase_HaeltDieRegelnEin(
        string vorlage, int toepfe, int gegnerJeTopf)
    {
        var (rangliste, vereine) = await TestRanglisten.VereineAnlegenAsync(datenbank, _laender);
        string name = app.Hauptfenster.TurnierAusVorlageErstellen(vorlage, VARIANTE);
        // Die Kontingente der Champions-League-Vorlage (#574) verlangen Vereine aus den Ranglisten ihrer Länder, die
        // Testvereine stehen nur in der eigenen Rangliste.
        await TestTurniere.QuotenEntfernenAsync(datenbank, name);
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen(name);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(LIGAPHASE);
        auslosung.RanglisteWaehlen(rangliste);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();
        int gegner = toepfe * gegnerJeTopf;
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(LIGAPHASE);
        var spiele = spielplan.SpieleJeSpieltag(gegner);

        var teams = vereine.Select(v => v.Verein).ToHashSet();
        spiele.Should().HaveCount(TEAMS * gegner / 2);
        spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).Distinct().Should().BeEquivalentTo(teams);
        KeinPaarDoppelt(spiele);
        JedesTeamHatSeineGegner(spiele, vereine, TEAMS / toepfe, gegnerJeTopf, gegner);
        SpielplanInvarianten.JedesTeamEinmalJeSpieltag(spiele, teams, gegner);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static void KeinPaarDoppelt(IReadOnlyList<SpielZeile> spiele) =>
        spiele.Select(s => string.CompareOrdinal(s.Heim, s.Auswaerts) < 0 ? (s.Heim, s.Auswaerts) : (s.Auswaerts, s.Heim))
            .Should().OnlyHaveUniqueItems("in der Ligaphase trifft jedes Paar höchstens einmal aufeinander");

    /// <summary>Je Team: <paramref name="gegner"/> Spiele, die Hälfte zu Hause; aus jedem Topf
    /// <paramref name="gegnerJeTopf"/> Gegner; keiner aus dem eigenen Land, höchstens zwei aus demselben. Der Topf
    /// folgt aus dem Rang – „Töpfe automatisch befüllen“ füllt sie der Rangliste nach.</summary>
    private static void JedesTeamHatSeineGegner(IReadOnlyList<SpielZeile> spiele,
        IReadOnlyList<(string Verein, string Land)> vereine, int topfGroesse, int gegnerJeTopf, int gegner)
    {
        var topf = vereine.Select((v, rang) => (v.Verein, Topf: rang / topfGroesse)).ToDictionary(e => e.Verein, e => e.Topf);
        var land = vereine.ToDictionary(v => v.Verein, v => v.Land);

        foreach (var (team, _) in vereine)
        {
            var heim = spiele.Where(s => s.Heim == team).Select(s => s.Auswaerts).ToList();
            var auswaerts = spiele.Where(s => s.Auswaerts == team).Select(s => s.Heim).ToList();
            var alle = heim.Concat(auswaerts).ToList();

            alle.Should().HaveCount(gegner, team);
            heim.Should().HaveCount(gegner / 2, "{0} spielt die Hälfte zu Hause", team);
            alle.GroupBy(g => topf[g]).Should().HaveCount(topf.Values.Distinct().Count(), "{0} hat Gegner aus jedem Topf", team)
                .And.OnlyContain(t => t.Count() == gegnerJeTopf, "{0} hat {1} Gegner je Topf", team, gegnerJeTopf);
            alle.Should().NotContain(g => land[g] == land[team], "{0} trifft auf keinen Verein aus {1}", team, land[team]);
            alle.GroupBy(g => land[g]).Should().OnlyContain(l => l.Count() <= MAX_GEGNER_JE_LAND,
                "{0} hat höchstens {1} Gegner aus demselben Land", team, MAX_GEGNER_JE_LAND);
        }
    }
}
