using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.MiniTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Nebenwege der Auslosung (#533): zurücksetzen und neu losen, bei der Ziehung von Hand den letzten Zug
/// zurücknehmen und abbrechen, im TV-Modus den Rest sofort anzeigen, und eine Auslosungs-Regel, deren Wirkung im
/// Spielplan sichtbar wird. Jeder Test legt sein eigenes Mini-Turnier an.</summary>
public sealed class AuslosungsNebenwegeTests(TurnierverwaltungApp app, TestDatenbank datenbank)
    : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    /// <summary>Nach dem Zurücksetzen lässt sich neu losen, ohne dass Spiele doppelt im Spielplan stehen.</summary>
    [Fact]
    public void Zuruecksetzen_UndNeuLosen_ErgibtEinenEinfachenSpielplan()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.Durchfuehren();

        auslosung.Zuruecksetzen();
        auslosung.Status.Should().Be(AuslosungReiter.VORBEREITUNG);

        auslosung.Durchfuehren();
        auslosung.Status.Should().Be(AuslosungReiter.ABGESCHLOSSEN);
        GruppenphaseSpiele(turnier).Should().HaveCount(SPIELE_GRUPPENPHASE);
    }

    [Fact]
    public void HandZiehung_LetzterZugZurueckgenommen_StehtWiederAmAnfang()
    {
        var (_, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster, "Manuell");
        auslosung.HandZiehungStarten();
        string anfangsAnsage = auslosung.HandAnsage;

        auslosung.HandEinenSchrittZiehen();
        auslosung.HandAnsage.Should().NotBe(anfangsAnsage, "der Zug ist angenommen");

        auslosung.HandZugZuruecknehmen(anfangsAnsage);
        auslosung.HandFortschritt.Should().Be($"0 von {TEAMS} Teams gezogen");
    }

    /// <summary>Nach dem ersten Zug fragt Abbrechen nach: „Nein“ lässt die Ziehung laufen, „Ja“ verwirft sie, und die
    /// Auslosung steht wieder in der Vorbereitung.</summary>
    [Fact]
    public void HandZiehung_Abbrechen_VerwirftErstNachBestaetigung()
    {
        var (_, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster, "Manuell");
        auslosung.HandZiehungStarten();
        auslosung.HandEinenSchrittZiehen();
        string ansage = auslosung.HandAnsage;

        auslosung.HandZiehungAbbrechen(bestaetigen: false);
        auslosung.HandAnsage.Should().Be(ansage, "„Nein“ lässt die Ziehung weiterlaufen");

        auslosung.HandZiehungAbbrechen(bestaetigen: true);
        auslosung.HandAnsage.Should().BeEmpty("die Ziehung ist beendet");
        auslosung.Status.Should().Be(AuslosungReiter.VORBEREITUNG);
    }

    [Fact]
    public void TvZiehung_RestSofortAnzeigen_ZeigtDasVollstaendigeErgebnis()
    {
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster);
        auslosung.MitTvModusManuellDurchfuehren();

        auslosung.RestSofortAnzeigen();

        var gezogen = auslosung.ZiehungsErgebnis().Values.SelectMany(t => t).ToList();
        gezogen.Should().OnlyHaveUniqueItems().And.HaveCount(TEAMS);
        GruppenphaseSpiele(turnier).SelectMany(s => new[] { s.Heim, s.Auswaerts }).Distinct()
            .Should().BeEquivalentTo(gezogen);
    }

    /// <summary>Mit der Konföderationsverteilung landen die beiden CONMEBOL-Teams in verschiedenen Gruppen – sie
    /// spielen in der Gruppenphase also nie gegeneinander. Der Test legt eine eigene Rangliste mit genau seinen acht
    /// Nationen an, damit die Töpfe nicht vom Seed-Stand der FIFA-Weltrangliste abhängen.</summary>
    [Fact]
    public async Task Konfoederationsverteilung_TrenntDieSuedamerikaner()
    {
        string rangliste = await TestRanglisten.NationenAnlegenAsync(datenbank, _nationen);
        var (turnier, auslosung) = GruppenauslosungVorbereiten(app.Hauptfenster, rangliste: rangliste);
        auslosung.RegelSetzen("Konfoederationsverteilung", aktiv: true);

        auslosung.Durchfuehren();

        var spiele = GruppenphaseSpiele(turnier);
        spiele.Should().HaveCount(SPIELE_GRUPPENPHASE);
        spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts }).Should().Contain(["Argentinien", "Brasilien"]);
        spiele.Should().NotContain(s =>
            (s.Heim == "Argentinien" && s.Auswaerts == "Brasilien") ||
            (s.Heim == "Brasilien" && s.Auswaerts == "Argentinien"));
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    /// <summary>Acht Nationalmannschaften aus drei Konföderationen: fünf UEFA-Teams, Argentinien und Brasilien
    /// (CONMEBOL) sowie Marokko (CAF) – in dieser Reihenfolge, also je Topf zwei Teams.</summary>
    private static readonly string[] _nationen =
        ["Spanien", "Argentinien", "Frankreich", "England", "Portugal", "Brasilien", "Marokko", "Belgien"];

    private static IReadOnlyList<SpielZeile> GruppenphaseSpiele(TurnierdurchfuehrungSeite turnier) =>
        turnier.SpieleDerRunde(GRUPPENPHASE);
}
