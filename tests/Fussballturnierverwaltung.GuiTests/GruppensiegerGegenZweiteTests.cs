using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Auslosungsart „Gruppensieger vs. Zweite“ und K.-o. mit Hin- und Rückspiel (#532), an der Vorlage „UEFA
/// Nations League Liga A“: Gruppenphase (4×4, Hin- und Rückspiel) → Viertelfinale Gruppensieger gegen Zweite mit
/// Hin- und Rückspiel, der Gruppensieger hat im Rückspiel Heimrecht (von der Vorlage vorgegebene Regel) → Halbfinale.
/// Geprüft werden Invarianten, keine festen Werte.</summary>
public sealed class GruppensiegerGegenZweiteTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string VORLAGE = "UEFA Nations League Liga A";
    private const string VARIANTE = "16 Teams · ab 2024/25";
    private const string GRUPPENPHASE = "Gruppenphase";
    private const string VIERTELFINALE = "Viertelfinale";
    private const string HALBFINALE = "Halbfinale";

    /// <summary>16 UEFA-Nationalmannschaften; die Vorlage lässt nur Nationalmannschaften der UEFA zu.</summary>
    private static readonly string[] _nationen =
    [
        "Spanien", "Frankreich", "England", "Portugal", "Belgien", "Niederlande", "Italien", "Deutschland",
        "Kroatien", "Schweiz", "Dänemark", "Österreich", "Ungarn", "Serbien", "Polen", "Schottland",
    ];

    [Fact]
    public async Task Viertelfinale_PaartGruppensiegerMitZweitenUndDieSiegerKommenWeiter()
    {
        string rangliste = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var turnier = TurnierAnlegen();

        AuslosungDurchfuehren(turnier, GRUPPENPHASE, lostoepfe: 4, rangliste: rangliste);
        turnier.RundeSimulieren(GRUPPENPHASE, 48);
        var tabellen = turnier.Spielplan().GruppenTabellen();

        AuslosungDurchfuehren(turnier, VIERTELFINALE, lostoepfe: 2);
        var viertelfinale = turnier.RundeSimulieren(VIERTELFINALE, 8);

        AuslosungDurchfuehren(turnier, HALBFINALE, lostoepfe: 1, auslosungsart: "Zufällig");
        var halbfinale = turnier.SpieleDerRunde(HALBFINALE);

        GruppenVollstaendig(tabellen);
        var paare = Spielstand.HinUndRueckspiele(viertelfinale);
        paare.Should().HaveCount(4, "vier Paarungen mit Hin- und Rückspiel");
        GruppensiegerGegenZweite(paare, tabellen);
        var weiter = paare.Select(p => Spielstand.Weiterkommender(p.Hin, p.Rueck, auswaertstorregel: false)).ToList();
        weiter.Should().OnlyContain(t => t != null, "jede Paarung hat einen Sieger – notfalls im Elfmeterschießen");
        halbfinale.SelectMany(s => new[] { s.Heim, s.Auswaerts })
            .Should().BeEquivalentTo(weiter, "im Halbfinale stehen genau die Sieger der Viertelfinal-Paarungen");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private TurnierdurchfuehrungSeite TurnierAnlegen()
    {
        string name = $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
        app.Hauptfenster.TurniereOeffnen().AusVorlageErstellen(VORLAGE, VARIANTE, "2027", name);
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        return turnier;
    }

    /// <summary>Ohne <paramref name="rangliste"/> bleibt die Rangliste der vorigen Auslosung gewählt.
    /// <paramref name="lostoepfe"/> stellt der Test selbst ein: Die Auslosung startet mit einem Topf, auch wenn die
    /// Vorlage „Nach Töpfen“ (Gruppenphase: 4) bzw. „Gruppensieger vs. Zweite“ (2) vorgibt (Bug #564).</summary>
    private static void AuslosungDurchfuehren(TurnierdurchfuehrungSeite turnier, string runde, int lostoepfe,
        string? auslosungsart = null, string? rangliste = null)
    {
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(runde, auslosungsart);
        if (lostoepfe > 1) auslosung.LostoepfeSetzen(lostoepfe);
        if (rangliste is not null) auslosung.RanglisteWaehlen(rangliste);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();
    }

    // ── Invarianten ───────────────────────────────────────────────────────────

    private static void GruppenVollstaendig(IReadOnlyDictionary<string, IReadOnlyList<string>> tabellen)
    {
        tabellen.Should().HaveCount(4, "die Gruppentabellen stehen neben dem Spielplan der Gruppenphase");
        tabellen.Values.Should().OnlyContain(t => t.Count == 4);
        tabellen.Values.SelectMany(t => t).Should().BeEquivalentTo(_nationen);
    }

    /// <summary>Jede Paarung: ein Gruppensieger gegen den Zweiten einer anderen Gruppe, der Sieger mit Heimrecht im
    /// Rückspiel.</summary>
    private static void GruppensiegerGegenZweite(
        IReadOnlyList<(SpielZeile Hin, SpielZeile Rueck)> paare, IReadOnlyDictionary<string, IReadOnlyList<string>> tabellen)
    {
        var gruppeJeTeam = tabellen.SelectMany(t => t.Value.Select(team => (team, Gruppe: t.Key)))
            .ToDictionary(e => e.team, e => e.Gruppe);
        var sieger = tabellen.Values.Select(t => t[0]).ToHashSet();
        var zweite = tabellen.Values.Select(t => t[1]).ToHashSet();

        foreach (var (_, rueck) in paare)
        {
            sieger.Should().Contain(rueck.Heim, "der Gruppensieger hat im Rückspiel Heimrecht ({0} – {1})", rueck.Heim, rueck.Auswaerts);
            zweite.Should().Contain(rueck.Auswaerts, "ein Gruppensieger trifft auf einen Zweiten ({0} – {1})", rueck.Heim, rueck.Auswaerts);
            gruppeJeTeam[rueck.Heim].Should().NotBe(gruppeJeTeam[rueck.Auswaerts],
                "nie gegen den Zweiten der eigenen Gruppe ({0} – {1})", rueck.Heim, rueck.Auswaerts);
        }
    }
}
