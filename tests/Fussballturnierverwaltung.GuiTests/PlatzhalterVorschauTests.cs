using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using GuiTest.Framework;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Platzhalter-Vorschau (#208, #534) am vorbelegten Halbfinale des <see cref="HandTurnier"/>: Nach dem ersten
/// Spieltag der Gruppenphase zeigt sie in Klammern, wer nach dem aktuellen Tabellenstand auf den Plätzen 1 und 2 der
/// Gruppen steht; ausgeschaltet stehen wieder die Platzhalter da. Eigene Klasse und damit eigene App-Sitzung, siehe
/// <see cref="HandTurnier"/>.</summary>
public sealed class PlatzhalterVorschauTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    [Fact]
    public void Vorschau_ZeigtDieAktuellenGruppenplaetze()
    {
        Aufbauen(app.Hauptfenster, EindeutigerName());
        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(GRUPPENPHASE);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();

        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(GRUPPENPHASE);
        spielplan.SpieltagWaehlen(1);
        spielplan.Simulieren(Spielmenge.Spieltag, TEAMS / 2);
        var tabellen = spielplan.GruppenTabellen().OrderBy(t => t.Key).Select(t => t.Value).ToList();
        tabellen.Should().HaveCount(2);
        var (a, b) = (tabellen[0], tabellen[1]);

        spielplan.RundeWaehlen(HALBFINALE);
        Halbfinale(spielplan).Should().Equal(KreuzPaarungen, "ohne Vorschau stehen die Platzhalter da");

        spielplan.PlatzhalterVorschauUmschalten();
        string[] erwartet = [$"({a[0]})", $"({b[1]})", $"({b[0]})", $"({a[1]})"];
        Wait.Within(() => Halbfinale(spielplan).SequenceEqual(erwartet), Wait.DefaultTimeout);
        Halbfinale(spielplan).Should().Equal(erwartet, "die Vorschau zeigt die Gruppenersten und -zweiten über Kreuz");

        spielplan.PlatzhalterVorschauUmschalten();
        Wait.Within(() => Halbfinale(spielplan).SequenceEqual(KreuzPaarungen), Wait.DefaultTimeout);
        Halbfinale(spielplan).Should().Equal(KreuzPaarungen, "ausgeschaltet stehen wieder die Platzhalter da");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    /// <summary>Heim, Auswärts, Heim, Auswärts der beiden Halbfinals.</summary>
    private static IReadOnlyList<string> Halbfinale(SpielplanReiter spielplan) =>
        [.. spielplan.Spiele().SelectMany(s => new[] { s.Heim, s.Auswaerts })];
}
