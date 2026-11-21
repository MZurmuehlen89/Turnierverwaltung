using FluentAssertions;
using Fussballturnierverwaltung.GuiTests.Seiten;

namespace Fussballturnierverwaltung.GuiTests;

/// <summary>Stammdatenpflege am Beispiel Mannschaft (#523): anlegen, bearbeiten, Live-Prüfung des Formulars und
/// Löschen mit Rückfrage. Jeder Test legt seine Mannschaft unter eindeutigem Namen selbst an; der Seed bleibt
/// unverändert.</summary>
public sealed class MannschaftsPflegeTests(TurnierverwaltungApp app) : IClassFixture<TurnierverwaltungApp>, IDisposable
{
    private const string NAME_LEER = "Mannschaftsname darf nicht leer sein.";
    private const string KUERZEL_LEER = "Kürzel darf nicht leer sein.";

    private readonly MannschaftenSeite _seite = app.Hauptfenster.MannschaftenOeffnen();

    [Fact]
    public void Anlegen_ErscheintMitSeinenWertenInDerListe()
    {
        string name = EindeutigerName();

        _seite.Anlegen(name, "GTA", 80);

        _seite.Eintrag(name).Should().Be(new MannschaftsEintrag(name, "GTA", 80));
        _seite.FehlerAbwarten().Should().BeEmpty();
    }

    [Fact]
    public void Bearbeiten_UebernimmtDieAenderungen()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, "GTB", 80);

        _seite.Auswaehlen(name);
        _seite.KurznameEingeben("GTX");
        _seite.OffensivSetzen(55);
        _seite.Speichern();

        _seite.Eintrag(name).Should().Be(new MannschaftsEintrag(name, "GTX", 55));
        _seite.Auswaehlen(name);
        _seite.Kurzname.Should().Be("GTX");
    }

    /// <summary>Die Prüfung läuft bei jeder Eingabe: Ein neues, leeres Formular meldet sofort beide Pflichtfelder, ein
    /// vollständiges gar nichts, und Speichern ist genau dann gesperrt, wenn Meldungen dastehen.</summary>
    [Fact]
    public void LiveValidierung_SperrtSpeichernSolangeFehlerBestehen()
    {
        _seite.Neu();
        _seite.FehlerAbwarten(NAME_LEER, KUERZEL_LEER).Should().BeEquivalentTo(NAME_LEER, KUERZEL_LEER);
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse();

        _seite.NameEingeben(EindeutigerName());
        _seite.KurznameEingeben("GTV");
        _seite.FehlerAbwarten().Should().BeEmpty();
        _seite.SpeichernFreigabeAbwarten(true).Should().BeTrue();

        _seite.NameEingeben("");
        _seite.FehlerAbwarten(NAME_LEER).Should().Equal(NAME_LEER);
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse();

        _seite.Abbrechen();
    }

    /// <summary>Feldmarkierung (#595): Der Fehler steht am Feld (HelpText), ein Klick auf ihn in der Meldungsleiste
    /// setzt den Fokus ins Feld, und sobald das Feld gültig ist, ist die Markierung weg.</summary>
    [Fact]
    public void Feldmarkierung_ZeigtDenFehlerAmFeld_UndDieLeisteSpringtHin()
    {
        _seite.Neu();
        Feldmarkierung.FehlerAbwarten(_seite.Feld("Name"), NAME_LEER).Should().Be(NAME_LEER);
        Feldmarkierung.FehlerAbwarten(_seite.Feld("Kurzname"), KUERZEL_LEER).Should().Be(KUERZEL_LEER);

        _seite.Meldungen.Anspringen(KUERZEL_LEER);
        Feldmarkierung.FokusAbwarten(_seite.Feld("Kurzname")).Should().BeTrue("der Klick springt ins Feld des Fehlers");

        _seite.NameEingeben(EindeutigerName());
        Feldmarkierung.FehlerAbwarten(_seite.Feld("Name"), "").Should().BeEmpty("der Name ist jetzt gültig");
        Feldmarkierung.FehlerAbwarten(_seite.Feld("Kurzname"), KUERZEL_LEER).Should().Be(KUERZEL_LEER);

        _seite.Abbrechen();
    }

    [Fact]
    public void Loeschen_FragtNachUndEntferntErstNachBestaetigung()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, "GTL", 70);

        _seite.Auswaehlen(name);
        _seite.Loeschen(bestaetigen: false);
        _seite.Eintrag(name).Name.Should().Be(name, "Abbrechen lässt die Mannschaft stehen");

        _seite.Auswaehlen(name);
        _seite.Loeschen(bestaetigen: true);
        _seite.NichtMehrGelistetAbwarten(name).Should().BeTrue("die Mannschaft ist gelöscht");
    }

    /// <summary>#667 K12, K13: Eine neue Mannschaft startet ausgeglichen und normal flexibel. Der Vorschlag aus den
    /// Ratings folgt sofort dem Offensiv-Regler (Defensiv bleibt auf 70, Normalabstand +2): 70 → defensiv, 72 → passt,
    /// 74 → offensiv. „Übernehmen“ steht nur bei Abweichung da und wählt den Vorschlag.</summary>
    [Fact]
    public void Spielweise_NeueMannschaft_VorschlagFolgtDenRatingsUndLaesstSichUebernehmen()
    {
        _seite.Neu();
        _seite.GrundausrichtungAbwarten("Ausgeglichen").Should().Be("Ausgeglichen");
        _seite.FlexibilitaetAbwarten("Normal").Should().Be("Normal");
        _seite.VorschlagAbwarten(VORSCHLAG + "defensiv").Should().Be(VORSCHLAG + "defensiv");
        _seite.UebernehmenSichtbarAbwarten(true).Should().BeTrue();

        _seite.OffensivSetzen(72);
        _seite.VorschlagAbwarten(PASST).Should().Be(PASST);
        _seite.UebernehmenSichtbarAbwarten(false).Should().BeFalse("ohne Abweichung gibt es nichts zu übernehmen");

        _seite.OffensivSetzen(74);
        _seite.VorschlagAbwarten(VORSCHLAG + "offensiv").Should().Be(VORSCHLAG + "offensiv");
        _seite.VorschlagUebernehmen();
        _seite.GrundausrichtungAbwarten("Offensiv").Should().Be("Offensiv");
        _seite.VorschlagAbwarten(PASST).Should().Be(PASST);

        _seite.Abbrechen();
    }

    /// <summary>#667 K14: Grundausrichtung und Flexibilität machen das Formular „geändert“ und bleiben nach dem
    /// Speichern erhalten.</summary>
    [Fact]
    public void Spielweise_WirdGespeichertUndWiederGezeigt()
    {
        string name = EindeutigerName();
        _seite.Anlegen(name, "GTS", 72);
        _seite.Auswaehlen(name);
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse("frisch geladen ist nichts geändert");

        _seite.GrundausrichtungWaehlen("SehrDefensiv");
        _seite.SpeichernFreigabeAbwarten(true).Should().BeTrue("die Grundausrichtung ist eine Änderung");
        _seite.FlexibilitaetWaehlen("Starr");
        _seite.Speichern();

        _seite.Auswaehlen(name);
        _seite.GrundausrichtungAbwarten("SehrDefensiv").Should().Be("SehrDefensiv");
        _seite.FlexibilitaetAbwarten("Starr").Should().Be("Starr");
    }

    /// <summary>#677 K8: Eine neue und eine bestehende Mannschaft zeigen die Kondition „normal“; eine gewählte
    /// „schwach“ macht das Formular „geändert“ und steht nach dem Speichern und erneuten Öffnen da.</summary>
    [Fact]
    public void Kondition_StartetNormal_WirdGespeichertUndWiederGezeigt()
    {
        _seite.Neu();
        _seite.KonditionAbwarten("Normal").Should().Be("Normal");
        _seite.Abbrechen();

        string name = EindeutigerName();
        _seite.Anlegen(name, "GTK", 72);
        _seite.Auswaehlen(name);
        _seite.KonditionAbwarten("Normal").Should().Be("Normal");
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse("frisch geladen ist nichts geändert");

        _seite.KonditionWaehlen("Schwach");
        _seite.SpeichernFreigabeAbwarten(true).Should().BeTrue("die Kondition ist eine Änderung");
        _seite.Speichern();

        _seite.Auswaehlen(name);
        _seite.KonditionAbwarten("Schwach").Should().Be("Schwach");
    }

    /// <summary>#669 K6: Eine neue und eine bestehende Mannschaft zeigen die Nervenstärke „normal“; eine gewählte
    /// „stark“ macht das Formular „geändert“ und steht nach dem Speichern und erneuten Öffnen da.</summary>
    [Fact]
    public void Nervenstaerke_StartetNormal_WirdGespeichertUndWiederGezeigt()
    {
        _seite.Neu();
        _seite.NervenstaerkeAbwarten("Normal").Should().Be("Normal");
        _seite.Abbrechen();

        string name = EindeutigerName();
        _seite.Anlegen(name, "GTN", 72);
        _seite.Auswaehlen(name);
        _seite.NervenstaerkeAbwarten("Normal").Should().Be("Normal");
        _seite.SpeichernFreigabeAbwarten(false).Should().BeFalse("frisch geladen ist nichts geändert");

        _seite.NervenstaerkeWaehlen("Stark");
        _seite.SpeichernFreigabeAbwarten(true).Should().BeTrue("die Nervenstärke ist eine Änderung");
        _seite.Speichern();

        _seite.Auswaehlen(name);
        _seite.NervenstaerkeAbwarten("Stark").Should().Be("Stark");
    }

    private const string VORSCHLAG = "Vorschlag aus den Ratings: ";
    private const string PASST = "Entspricht dem Vorschlag aus den Ratings.";

    public void Dispose() => app.BeiFehlschlagBelegeSichern();

    private static string EindeutigerName() => $"GuiTest {Guid.NewGuid().ToString("N")[..8]}";
}
