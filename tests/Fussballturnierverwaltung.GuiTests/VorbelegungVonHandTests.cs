using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Vorbelegung einer Runde mit fest vorgegebener Paarung (#531). Eigene Klasse und damit eigene App-Sitzung,
/// siehe <see cref="HandTurnier"/>.</summary>
public sealed class VorbelegungVonHandTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    /// <summary>Vorbelegung des Halbfinales: Teams per Doppelklick setzen und per Rechtsklick wieder herausnehmen,
    /// abschließen, zurücksetzen. Abgeschlossen steht der Spielplan des Halbfinales mit den gesetzten Paarungen da.</summary>
    [Fact]
    public void Vorbelegung_SetzenEntfernenAbschliessenZuruecksetzen()
    {
        string name = EindeutigerName();
        var vorbelegung = RundenAufbauen(app.Hauptfenster, name).Vorbelegung();
        vorbelegung.RundeWaehlen(HALBFINALE);
        vorbelegung.Plaetze.Should().Equal(Enumerable.Repeat(VorbelegungReiter.FREI, 4));
        vorbelegung.Verfuegbar.Should().BeEquivalentTo(KreuzPaarungen);

        foreach (var platzhalter in KreuzPaarungen)
            vorbelegung.Platzieren(platzhalter);
        vorbelegung.Plaetze.Should().Equal(KreuzPaarungen);
        vorbelegung.Verfuegbar.Should().BeEmpty();

        vorbelegung.Entfernen(KreuzPaarungen[1]);
        vorbelegung.Plaetze.Should().Equal(KreuzPaarungen[0], VorbelegungReiter.FREI, KreuzPaarungen[2], KreuzPaarungen[3]);
        vorbelegung.Verfuegbar.Should().Equal(KreuzPaarungen[1]);
        vorbelegung.Platzieren(KreuzPaarungen[1]);
        vorbelegung.Abschliessen();

        var spielplan = app.Hauptfenster.TurnierdurchfuehrungOeffnen().Spielplan();
        spielplan.RundeWaehlen(HALBFINALE);
        spielplan.Spiele().Select(s => (s.Heim, s.Auswaerts)).Should().Equal(
            (KreuzPaarungen[0], KreuzPaarungen[1]), (KreuzPaarungen[2], KreuzPaarungen[3]));

        vorbelegung = app.Hauptfenster.TurniereOeffnen().Vorbelegung();
        vorbelegung.RundeWaehlen(HALBFINALE);
        vorbelegung.Abgeschlossen.Should().BeTrue();
        vorbelegung.Zuruecksetzen();
        vorbelegung.Verfuegbar.Should().BeEquivalentTo(KreuzPaarungen);
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();
}
