using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;
using static Fussballturnierverwaltung.GuiTests.HandTurnier;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Turnier ohne Vorlage von Hand aufbauen (#531): Turnierkopf mit Live-Prüfung, Runden mit Übergangsregeln,
/// Vorbelegung einer Runde mit fest vorgegebener Paarung, Löschen mit Rückfrage – und am Ende lässt sich das Turnier
/// auslosen und durchspielen. Jeder Test legt sein Turnier unter eindeutigem Namen selbst an; der Seed bleibt
/// unverändert.</summary>
public sealed class TurnierVonHandTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string NAME_LEER = "Turniername darf nicht leer sein.";
    private const string SAISON_LEER = "Saison/Jahr darf nicht leer sein.";
    private const string ANZAHL_KEINE_ZAHL = "Mannschaftsanzahl muss eine Zahl ≥ 2 sein.";

    /// <summary>Ein neues, leeres Formular meldet sofort beide Pflichtfelder; eine ungültige Mannschaftsanzahl kommt
    /// dazu. Speichern ist genau dann gesperrt, wenn Meldungen dastehen. Gespeicherte Änderungen stehen nach dem
    /// erneuten Öffnen der Seite noch da.</summary>
    [Fact]
    public void Turnierkopf_PrueftLiveUndSpeichertAenderungen()
    {
        var turniere = app.Hauptfenster.TurniereOeffnen();
        var kopf = turniere.Kopf();
        kopf.Neu();
        turniere.KopfFehlerAbwarten(NAME_LEER, SAISON_LEER).Should().BeEquivalentTo(NAME_LEER, SAISON_LEER);
        SpeichernFreigabeAbwarten(kopf, false).Should().BeFalse();

        string name = EindeutigerName();
        kopf.WertSetzen("Name", name);
        kopf.WertSetzen("Saison", "2027");
        kopf.WertSetzen("Mannschaftsanzahl", "acht");
        turniere.KopfFehlerAbwarten(ANZAHL_KEINE_ZAHL).Should().Equal(ANZAHL_KEINE_ZAHL);
        SpeichernFreigabeAbwarten(kopf, false).Should().BeFalse();

        kopf.WertSetzen("Mannschaftsanzahl", "8");
        turniere.KopfFehlerAbwarten().Should().BeEmpty();
        kopf.Speichern();

        kopf.WertSetzen("Variante", "von Hand");
        kopf.Speichern();

        app.Hauptfenster.MannschaftenOeffnen();
        kopf = app.Hauptfenster.TurniereOeffnen().Kopf();
        kopf.Auswaehlen(name);
        (kopf.Wert("Saison"), kopf.Wert("Mannschaftsanzahl"), kopf.Wert("Variante"))
            .Should().Be(("2027", "8", "von Hand"));
    }

    /// <summary>Runden anlegen, umbenennen und entfernen; die Live-Prüfung sperrt Speichern bei unstimmigen Zahlen.
    /// Nach dem Speichern und erneuten Öffnen stehen genau die gespeicherten Runden da.</summary>
    [Fact]
    public void Runden_AnlegenUmbenennenEntfernenUndSpeichern()
    {
        string name = EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, REGELWERK);

        var runden = turniere.Runden();
        runden.Anlegen(GRUPPENPHASE, "Gruppenphase", TEAMS, 4, gruppen: 2);
        runden.Anlegen("Zwischenrunde", "Liga", 4, 2);
        runden.Anlegen(FINALE, "Finale", 2, 1);

        runden.Auswaehlen(GRUPPENPHASE);
        runden.AusgangSetzen(TEAMS);
        runden.FehlerAbwarten("müssen kleiner als Eingangsteams")
            .Should().Contain(f => f.Contains("müssen kleiner als Eingangsteams", StringComparison.Ordinal));
        runden.SpeichernFreigabeAbwarten(false).Should().BeFalse();
        runden.AusgangSetzen(4);
        runden.KeineFehlerAbwarten().Should().BeEmpty();

        runden.Auswaehlen("Zwischenrunde");
        runden.Ausfuellen(HALBFINALE, "K.o.-Runde", 4, 2);
        runden.Anlegen("Überzählig", "Liga", 2, 1);
        runden.Entfernen("Überzählig");
        runden.KeineFehlerAbwarten().Should().BeEmpty();
        runden.Speichern();

        app.Hauptfenster.MannschaftenOeffnen();
        var wieder = app.Hauptfenster.TurniereOeffnen();
        wieder.Kopf().Auswaehlen(name);
        wieder.Runden().Rundennamen.Should().Equal(GRUPPENPHASE, HALBFINALE, FINALE);
    }

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

    /// <summary>Das von Hand gebaute Turnier läuft durch: Gruppenphase auslosen und simulieren, das vorbelegte
    /// Halbfinale füllt sich mit den Gruppenersten und -zweiten über Kreuz, das Finale mit den Halbfinalsiegern.</summary>
    [Fact]
    public void VonHandAufgebaut_LaesstSichAuslosenUndDurchspielen()
    {
        string name = EindeutigerName();
        Aufbauen(app.Hauptfenster, name);

        var turnier = app.Hauptfenster.TurnierdurchfuehrungOeffnen();
        turnier.GewaehltesTurnier.Should().Be(name);
        var auslosung = turnier.Auslosung();
        auslosung.Anlegen(GRUPPENPHASE);
        auslosung.RanglisteWaehlen(MiniTurnier.RANGLISTE);
        auslosung.ToepfeAutomatischBefuellen();
        auslosung.Durchfuehren();

        var gruppenspiele = RundeSimulieren(turnier, GRUPPENPHASE, SPIELE_GRUPPENPHASE);
        var halbfinale = RundeSimulieren(turnier, HALBFINALE, 2);
        var finale = RundeSimulieren(turnier, FINALE, 1);

        turnier.Spielplan().Fortschritt.Should().Be((SPIELE_GESAMT, SPIELE_GESAMT));
        gruppenspiele.Should().HaveCount(SPIELE_GRUPPENPHASE);
        var gruppen = Gruppen(gruppenspiele);
        gruppen.Should().HaveCount(2).And.OnlyContain(g => g.Count == 4);

        halbfinale.Should().HaveCount(2).And.OnlyContain(s => s.Sieger != null);
        foreach (var spiel in halbfinale)
            GruppeVon(gruppen, spiel.Heim).Should().NotBeSameAs(GruppeVon(gruppen, spiel.Auswaerts),
                "das Halbfinale paart über Kreuz: {0} – {1}", spiel.Heim, spiel.Auswaerts);
        foreach (var gruppe in gruppen)
            Teams(halbfinale).Count(gruppe.Contains).Should().Be(2, "je Gruppe kommen zwei Teams weiter");

        finale.Should().ContainSingle().Which.Sieger.Should().NotBeNull();
        Teams(finale).Should().BeEquivalentTo(halbfinale.Select(s => s.Sieger!));
    }

    [Fact]
    public void Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = EindeutigerName();
        var turniere = app.Hauptfenster.TurniereOeffnen();
        turniere.Anlegen(name, "2027", TEAMS, REGELWERK);
        var kopf = turniere.Kopf();

        kopf.Loeschen(bestaetigen: false);
        kopf.Eintrag(name, e => e).Should().NotBeNull("Abbrechen lässt das Turnier stehen");

        kopf.Auswaehlen(name);
        kopf.Loeschen(bestaetigen: true);
        kopf.NichtMehrGelistetAbwarten(name).Should().BeTrue("das Turnier ist gelöscht");
    }

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static bool SpeichernFreigabeAbwarten(StammdatenEditor kopf, bool erwartet)
    {
        GuiTest.Framework.Wait.Within(() => kopf.SpeichernFreigegeben == erwartet, GuiTest.Framework.Wait.DefaultTimeout);
        return kopf.SpeichernFreigegeben;
    }

    private static IReadOnlyList<SpielZeile> RundeSimulieren(TurnierdurchfuehrungSeite turnier, string runde, int anzahl)
    {
        var spielplan = turnier.Spielplan();
        spielplan.RundeWaehlen(runde);
        spielplan.Spiele().Should().HaveCount(anzahl, $"{runde} hat vor der Simulation {anzahl} Spiel(e)");
        spielplan.RundeSimulieren(anzahl);
        return spielplan.Spiele();
    }

    /// <summary>Die Gruppen aus den Gruppenspielen: Wer gegeneinander spielt, ist in derselben Gruppe.</summary>
    private static List<HashSet<string>> Gruppen(IEnumerable<SpielZeile> spiele)
    {
        var gruppen = new List<HashSet<string>>();
        foreach (var spiel in spiele)
        {
            var treffer = gruppen.Where(g => g.Contains(spiel.Heim) || g.Contains(spiel.Auswaerts)).ToList();
            var gruppe = new HashSet<string>(treffer.SelectMany(g => g)) { spiel.Heim, spiel.Auswaerts };
            gruppen.RemoveAll(treffer.Contains);
            gruppen.Add(gruppe);
        }
        return gruppen;
    }

    private static HashSet<string> GruppeVon(List<HashSet<string>> gruppen, string team) =>
        gruppen.Single(g => g.Contains(team));

    private static HashSet<string> Teams(IEnumerable<SpielZeile> spiele) =>
        [.. spiele.SelectMany(s => new[] { s.Heim, s.Auswaerts })];
}
