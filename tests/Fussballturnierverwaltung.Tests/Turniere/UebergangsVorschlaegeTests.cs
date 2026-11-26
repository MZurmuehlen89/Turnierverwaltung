using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turniere;

/// <summary>#167: Vorschläge für eingehende Übergangsregeln (Rechenregel, K1–K3, K6, K9).</summary>
public class UebergangsVorschlaegeTests
{
    private static TurnierRunde Runde(string name, Rundenart art, int eingang, int ausgang, int? gruppen = null) =>
        new() { Name = name, Rundenart = art, EingangsMannschaftsanzahl = eingang, AusgangsMannschaftsanzahl = ausgang, GruppenAnzahl = gruppen };

    private static UebergangsregelPositionsEingabe Regel(int quelle, int ziel, int? gruppe, int von, int bis, int? beste = null) =>
        new(quelle, ziel, gruppe, von, bis, beste);

    private static VorschlagsOption EinzigeOption(IReadOnlyList<UebergangsVorschlag> vorschlaege, int ziel)
    {
        var vorschlag = vorschlaege.Should().ContainSingle(v => v.ZielReihenfolge == ziel).Subject;
        return vorschlag.Optionen.Should().ContainSingle().Subject;
    }

    // ── K1: K.-o.-Quelle ────────────────────────────────────────────────────

    [Fact]
    public void K1_KoRunde_SchlaegtSiegerAllerSpieleVor()
    {
        var runden = new[]
        {
            Runde("Viertelfinale", Rundenart.KoRunde, 8, 4),
            Runde("Halbfinale", Rundenart.KoRunde, 4, 2),
        };

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(runden, []), ziel: 2);

        option.Regeln.Should().Equal(Regel(1, 2, 1, 1, 1), Regel(1, 2, 2, 1, 1), Regel(1, 2, 3, 1, 1), Regel(1, 2, 4, 1, 1));
    }

    [Fact]
    public void K1_SpielUmPlatzDreiBekommtVerlierer_FinaleBekommtSieger_DesHalbfinales()
    {
        var runden = new[]
        {
            Runde("Halbfinale", Rundenart.KoRunde, 4, 2),
            Runde("Spiel um Platz 3", Rundenart.SpielUmPlatzDrei, 2, 1),
            Runde("Finale", Rundenart.Finale, 2, 1),
        };

        var vorschlaege = UebergangsVorschlaege.Berechnen(runden, []);

        EinzigeOption(vorschlaege, ziel: 2).Regeln.Should().Equal(Regel(1, 2, 1, 2, 2), Regel(1, 2, 2, 2, 2));
        // Das Finale hängt am Halbfinale, nicht am Spiel um Platz 3 davor in der Liste.
        EinzigeOption(vorschlaege, ziel: 3).Regeln.Should().Equal(Regel(1, 3, 1, 1, 1), Regel(1, 3, 2, 1, 1));
    }

    // ── K2: Gruppenphase ────────────────────────────────────────────────────

    [Fact]
    public void K2_GleichmaessigVerteilt_SchlaegtPlatzEinsBisKJeGruppeVor()
    {
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, 8, 4, gruppen: 2),
            Runde("Halbfinale", Rundenart.KoRunde, 4, 2),
        };

        var vorschlag = UebergangsVorschlaege.Berechnen(runden, []).Single(v => v.ZielReihenfolge == 2);

        vorschlag.Hinweis.Should().BeNull();
        vorschlag.Optionen.Single().Regeln.Should().Equal(Regel(1, 2, 1, 1, 2), Regel(1, 2, 2, 1, 2));
    }

    [Fact]
    public void K2_UngleichmaessigVerteilt_SchlaegtPlatzEinsBisZweiUndDieBestenDrittenVor()
    {
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, 24, 16, gruppen: 6),
            Runde("Achtelfinale", Rundenart.KoRunde, 16, 8),
        };

        var vorschlag = UebergangsVorschlaege.Berechnen(runden, []).Single(v => v.ZielReihenfolge == 2);

        vorschlag.Hinweis.Should().NotBeNullOrWhiteSpace();
        vorschlag.Optionen.Single().Regeln.Should().Equal(
            Regel(1, 2, 1, 1, 2), Regel(1, 2, 2, 1, 2), Regel(1, 2, 3, 1, 2),
            Regel(1, 2, 4, 1, 2), Regel(1, 2, 5, 1, 2), Regel(1, 2, 6, 1, 2),
            Regel(1, 2, null, 3, 3, beste: 4));
    }

    // ── K3: Liga ────────────────────────────────────────────────────────────

    [Fact]
    public void K3_Liga_SchlaegtPlatzEinsBisNVor()
    {
        var runden = new[]
        {
            Runde("Liga", Rundenart.Liga, 8, 4),
            Runde("Halbfinale", Rundenart.KoRunde, 4, 2),
        };

        EinzigeOption(UebergangsVorschlaege.Berechnen(runden, []), ziel: 2).Regeln.Should().Equal(Regel(1, 2, null, 1, 4));
    }

    // ── K6: Zustände davor / dazwischen ─────────────────────────────────────

    [Fact]
    public void K6_ErsteRunde_HatKeinenVorschlag()
    {
        var runden = new[] { Runde("Liga", Rundenart.Liga, 8, 4), Runde("Halbfinale", Rundenart.KoRunde, 4, 2) };

        UebergangsVorschlaege.Berechnen(runden, []).Should().NotContain(v => v.ZielReihenfolge == 1);
    }

    [Fact]
    public void K6_TeilweiseAbgedeckt_SchlaegtNurDenRestVor()
    {
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, 24, 16, gruppen: 6),
            Runde("Achtelfinale", Rundenart.KoRunde, 16, 8),
        };
        var vorhanden = Enumerable.Range(1, 6).Select(g => Regel(1, 2, g, 1, 2)).ToList();

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(runden, vorhanden), ziel: 2);

        option.Regeln.Should().Equal(Regel(1, 2, null, 3, 3, beste: 4));
    }

    [Fact]
    public void K6_TeilweiseBesteVergeben_SchlaegtNurDieRestlichenBestenVor()
    {
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, 24, 16, gruppen: 6),
            Runde("Achtelfinale", Rundenart.KoRunde, 16, 8),
        };
        var vorhanden = Enumerable.Range(1, 6).Select(g => Regel(1, 2, g, 1, 2)).Append(Regel(1, 2, null, 3, 3, beste: 2)).ToList();

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(runden, vorhanden), ziel: 2);

        option.Regeln.Should().Equal(Regel(1, 2, null, 3, 3, beste: 2));
    }

    [Fact]
    public void K6_LueckeGeschlossen_KeinVorschlag()
    {
        var runden = new[]
        {
            Runde("Gruppenphase", Rundenart.Gruppenphase, 8, 4, gruppen: 2),
            Runde("Halbfinale", Rundenart.KoRunde, 4, 2),
        };

        UebergangsVorschlaege.Berechnen(runden, [Regel(1, 2, 1, 1, 2), Regel(1, 2, 2, 1, 2)]).Should().BeEmpty();
    }

    // ── K9: belegte Plätze ──────────────────────────────────────────────────

    [Fact]
    public void K9_VonAndererRegelVergebenePlaetze_WerdenNichtVorgeschlagen()
    {
        var runden = new[]
        {
            Runde("Liga", Rundenart.Liga, 24, 24),
            Runde("Playoffs", Rundenart.KoRunde, 16, 8),
            Runde("Achtelfinale", Rundenart.KoRunde, 8, 4),
        };

        // Platz 1–8 gehen schon ins Achtelfinale: die Playoffs bekommen Platz 9–24, nicht 1–16.
        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(runden, [Regel(1, 3, null, 1, 8)]), ziel: 2);

        option.Regeln.Should().Equal(Regel(1, 2, null, 9, 24));
    }

    // ── K4/K5: Vorlagenabgleich ─────────────────────────────────────────────

    private static TurnierRunde[] LigaUndHalbfinale(int ligaAusgang = 4) =>
    [
        Runde("Liga", Rundenart.Liga, 8, ligaAusgang),
        Runde("Halbfinale", Rundenart.KoRunde, 4, 2),
    ];

    private static VorlagenMuster Muster(string name, TurnierRunde[] runden, params UebergangsregelPositionsEingabe[] regeln) =>
        new(name, runden, regeln);

    [Fact]
    public void K4_PassendeVorlage_TraegtDieVorlageAlsHerkunft()
    {
        var vorlage = Muster("EM", LigaUndHalbfinale(), Regel(1, 2, null, 1, 4));

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(LigaUndHalbfinale(), [], [vorlage]), ziel: 2);

        option.Regeln.Should().Equal(Regel(1, 2, null, 1, 4));
        option.Herkuenfte.Should().Equal("EM");
    }

    [Fact]
    public void K4_AbweichendeStruktur_GibtKeinenVorschlagAusDerVorlage()
    {
        var vorlage = Muster("WM", LigaUndHalbfinale(ligaAusgang: 6), Regel(1, 2, null, 5, 8));

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(LigaUndHalbfinale(), [], [vorlage]), ziel: 2);

        option.Herkuenfte.Should().Equal("Rechenregel");
    }

    [Fact]
    public void K5_GleicheRegelsaetzeAusMehrerenVorlagen_ErgebenEinenVorschlagMitAllenHerkuenften()
    {
        var em = Muster("EM", LigaUndHalbfinale(), Regel(1, 2, null, 1, 4));
        var wm = Muster("WM", LigaUndHalbfinale(), Regel(1, 2, null, 1, 4));

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(LigaUndHalbfinale(), [], [em, wm]), ziel: 2);

        option.Herkuenfte.Should().Equal("EM", "WM");
    }

    [Fact]
    public void K5_VerschiedeneRegelsaetze_ErgebenMehrereOptionen_VorlageVorRechenregel()
    {
        var vorlage = Muster("WM", LigaUndHalbfinale(), Regel(1, 2, null, 5, 8));

        var vorschlag = UebergangsVorschlaege.Berechnen(LigaUndHalbfinale(), [], [vorlage]).Single(v => v.ZielReihenfolge == 2);

        vorschlag.Optionen.Select(o => o.Herkuenfte.Single()).Should().Equal("WM", "Rechenregel");
        vorschlag.Optionen[0].Regeln.Should().Equal(Regel(1, 2, null, 5, 8));
        vorschlag.Optionen[1].Regeln.Should().Equal(Regel(1, 2, null, 1, 4));
    }

    [Fact]
    public void K9_VorlagenregelAufBelegteQuellplaetze_WirdNichtVorgeschlagen()
    {
        var runden = new[]
        {
            Runde("Liga", Rundenart.Liga, 24, 24),
            Runde("Playoffs", Rundenart.KoRunde, 16, 8),
            Runde("Achtelfinale", Rundenart.KoRunde, 8, 4),
        };
        var vorlage = Muster("CL", runden, Regel(1, 2, null, 1, 16));

        var option = EinzigeOption(UebergangsVorschlaege.Berechnen(runden, [Regel(1, 3, null, 1, 8)], [vorlage]), ziel: 2);

        option.Herkuenfte.Should().Equal("Rechenregel");
    }
}
