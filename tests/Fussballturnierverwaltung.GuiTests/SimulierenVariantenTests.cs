using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Die Ebenen der Menüs „Simulieren“ und „Zurücksetzen“ im Spielplan (#534): Jede trifft genau ihre Spiele,
/// alle anderen bleiben, wie sie waren. Liga (Vorlage „Bundesliga“) für Spieltag, Hin- und Rückrunde, Mini-Turnier für
/// Gruppe, Auswahl und Runde. Geprüft werden Invarianten, keine festen Ergebnisse.</summary>
public sealed class SimulierenVariantenTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string SAISON = "Saison";
    private const int SPIELE_JE_SPIELTAG = 9;
    private const int SPIELTAGE = 34;
    private const int SPIELE_JE_HALBSERIE = SPIELE_JE_SPIELTAG * SPIELTAGE / 2;

    [Fact]
    public void Liga_SpieltagHinUndRueckrunde_TreffenNurIhreSpiele()
    {
        var turnier = app.Hauptfenster.TurnierAusVorlageAnlegen("Bundesliga", "18 Teams");
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(SAISON);
        auslosung.RanglisteWaehlen(RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(SAISON);

        spielplan.SpieltagWaehlen(1);
        spielplan.Simulieren(Spielmenge.Spieltag, SPIELE_JE_SPIELTAG);
        SpieltageSollten(spielplan, gespielt: [1], offen: [2]);

        spielplan.Simulieren(Spielmenge.Hinrunde, SPIELE_JE_HALBSERIE - SPIELE_JE_SPIELTAG);
        SpieltageSollten(spielplan, gespielt: [2, 17], offen: [18]);

        spielplan.SpieltagWaehlen(1);
        spielplan.Zuruecksetzen(Spielmenge.Spieltag, SPIELE_JE_SPIELTAG);
        SpieltageSollten(spielplan, gespielt: [2], offen: [1]);

        spielplan.Simulieren(Spielmenge.Rueckrunde, SPIELE_JE_HALBSERIE);
        SpieltageSollten(spielplan, gespielt: [18, 34], offen: [1]);

        spielplan.Zuruecksetzen(Spielmenge.Hinrunde, SPIELE_JE_HALBSERIE - SPIELE_JE_SPIELTAG);
        SpieltageSollten(spielplan, gespielt: [18, 34], offen: [1, 2, 17]);
        spielplan.Fortschritt.Should().Be((SPIELE_JE_HALBSERIE, 2 * SPIELE_JE_HALBSERIE));
    }

    [Fact]
    public void MiniTurnier_GruppeAuswahlUndRunde_TreffenNurIhreSpiele()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        var erstes = spielplan.Spiele()[0];

        spielplan.SpielWaehlen(erstes.Heim, erstes.Auswaerts);
        spielplan.Simulieren(Spielmenge.Gruppe, 6);
        var gruppe = GespielteGruppe(spielplan.Spiele(), erstes);

        spielplan.SpielWaehlen(erstes.Heim, erstes.Auswaerts);
        spielplan.Zuruecksetzen(Spielmenge.Gruppe, 6);
        spielplan.Spiele().Should().OnlyContain(s => !s.IstGespielt, "die Gruppe ist zurückgesetzt");

        var andere = spielplan.Spiele().First(s => !gruppe.Contains(s.Heim));
        spielplan.SpieleMarkieren((erstes.Heim, erstes.Auswaerts), (andere.Heim, andere.Auswaerts));
        spielplan.Simulieren(Spielmenge.Auswahl, 2);
        spielplan.Spiele().Where(s => s.IstGespielt).Select(s => (s.Heim, s.Auswaerts)).Should().BeEquivalentTo(
            [(erstes.Heim, erstes.Auswaerts), (andere.Heim, andere.Auswaerts)], "nur die markierten Spiele sind gespielt");

        spielplan.Zuruecksetzen(Spielmenge.Runde, 2);
        spielplan.Spiele().Should().HaveCount(SPIELE_GRUPPENPHASE).And.OnlyContain(s => !s.IstGespielt);
    }

    public void Dispose()
    {
        app.BeiFehlschlagBelegeSichern();
        app.Hauptfenster.Aufraeumen();
    }

    private static void SpieltageSollten(SpielplanReiter spielplan, int[] gespielt, int[] offen)
    {
        foreach (int spieltag in gespielt)
            spielplan.SpieltagWaehlen(spieltag).Should().HaveCount(SPIELE_JE_SPIELTAG)
                .And.OnlyContain(s => s.IstGespielt, "der {0}. Spieltag ist gespielt", spieltag);
        foreach (int spieltag in offen)
            spielplan.SpieltagWaehlen(spieltag).Should().HaveCount(SPIELE_JE_SPIELTAG)
                .And.OnlyContain(s => !s.IstGespielt, "der {0}. Spieltag ist offen", spieltag);
    }

    /// <summary>Gespielt sind genau die sechs Spiele einer Vierergruppe (jeder gegen jeden), darunter
    /// <paramref name="gewaehlt"/>. Liefert die Mannschaften der Gruppe.</summary>
    private static HashSet<string> GespielteGruppe(IReadOnlyList<SpielZeile> spiele, SpielZeile gewaehlt)
    {
        var gespielt = spiele.Where(s => s.IstGespielt).ToList();
        HashSet<string> gruppe = [.. gespielt.SelectMany(s => new[] { s.Heim, s.Auswaerts })];
        gruppe.Should().HaveCount(4).And.Contain([gewaehlt.Heim, gewaehlt.Auswaerts]);
        gespielt.Select(s => string.Join("|", new[] { s.Heim, s.Auswaerts }.Order())).Should()
            .OnlyHaveUniqueItems().And.HaveCount(6, "in der Gruppe spielt jeder einmal gegen jeden");
        return gruppe;
    }
}
