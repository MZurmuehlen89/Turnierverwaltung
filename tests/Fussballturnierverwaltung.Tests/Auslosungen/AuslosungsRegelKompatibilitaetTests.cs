using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Matrix Auslosungsregel × Auslosungsart × Rundenart (Issue #331).</summary>
public class AuslosungsRegelKompatibilitaetTests
{
    [Theory]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Zufall,        Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.NachSetzliste, Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Manuell,       Rundenart.Gruppenphase)]
    [InlineData(AuslosungsRegelArt.KeineGleicheGruppe,     Auslosungsart.NachToepfen,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.KeineGleicheGruppe,     Auslosungsart.Manuell,       Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos,       Auslosungsart.NachToepfen,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachSetzliste, Auslosungsart.NachSetzliste, Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.Zufall,        Rundenart.KoRunde)]
    // „Nach Töpfen“ und „Nach Setzliste“ losen den Überhang untereinander, bei ungerader Zahl mit Freilos (#495).
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.NachToepfen,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.NachSetzliste, Rundenart.KoRunde)]
    // Ziehung von Hand (#482): Heimrecht-Regeln außer „nach Los“, Freilos und Konföderationsverteilung wirken.
    [InlineData(AuslosungsRegelArt.HeimrechtNachSetzliste,     Auslosungsart.Manuell,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtFuerUnterklassige, Auslosungsart.Manuell,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,            Auslosungsart.Manuell,   Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.Konfoederationsverteilung,  Auslosungsart.Manuell,   Rundenart.Gruppenphase)]
    public void PassendeKombination_IstNichtGesperrt(AuslosungsRegelArt regel, Auslosungsart art, Rundenart rundenart) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(regel, art, rundenart, folgtAufGruppenphase: true).Should().BeNull();

    [Theory]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Zufall,        Rundenart.Liga)]
    [InlineData(AuslosungsRegelArt.KeinGleichesLand,       Auslosungsart.Zufall,        Rundenart.SchweizerSystem)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos,       Auslosungsart.Manuell,       Rundenart.KoRunde)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachLos,       Auslosungsart.Zufall,        Rundenart.Gruppenphase)]
    [InlineData(AuslosungsRegelArt.HeimrechtNachSetzliste, Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.Zufall,        Rundenart.Gruppenphase)]
    [InlineData(AuslosungsRegelArt.FreilosMoeglich,        Auslosungsart.Manuell,       Rundenart.Gruppenphase)]
    public void UnpassendeKombination_IstGesperrtMitBegruendung(AuslosungsRegelArt regel, Auslosungsart art, Rundenart rundenart) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(regel, art, rundenart, folgtAufGruppenphase: true).Should().NotBeNullOrWhiteSpace();

    [Fact]
    public void KeineGleicheGruppe_OhneVorangegangeneGruppenphase_IstGesperrt() =>
        AuslosungsRegelKompatibilitaet.SperrGrund(
                AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.Zufall, Rundenart.KoRunde, folgtAufGruppenphase: false)
            .Should().Contain("keine Gruppenphase");

    [Fact]
    public void KeineGleicheGruppe_BeiGruppensiegerGegenZweite_WirktImmer()
    {
        AuslosungsRegelKompatibilitaet.ImmerAktivGrund(
            AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.GruppensiegerGegenGruppenzweite).Should().NotBeNull();
        AuslosungsRegelKompatibilitaet.Wirkt(
                AuslosungsRegelArt.KeineGleicheGruppe, istAktiv: false, Auslosungsart.GruppensiegerGegenGruppenzweite,
                Rundenart.KoRunde, folgtAufGruppenphase: true)
            .Should().BeTrue();
    }

    [Fact]
    public void GesperrteRegel_WirktNicht_AuchWennAktiv() =>
        AuslosungsRegelKompatibilitaet.Wirkt(
                AuslosungsRegelArt.FreilosMoeglich, istAktiv: true, Auslosungsart.TopfZuordnung,
                Rundenart.TopfLiga, folgtAufGruppenphase: true)
            .Should().BeFalse();

    // Vorbelegung von Hand (#443): dieselbe Entscheidung wie beim Losen, auch für eine Manuell-Auslosung.
    [Theory]
    [InlineData(Rundenart.KoRunde,         true,  true)]
    [InlineData(Rundenart.Gruppenphase,    true,  true)]
    [InlineData(Rundenart.Liga,            true,  false)]
    [InlineData(Rundenart.SchweizerSystem, true,  false)]
    [InlineData(Rundenart.KoRunde,         false, false)]
    public void RegelDerAuslosung_WirktNurAktivUndBeiPassenderRunde(Rundenart rundenart, bool istAktiv, bool erwartet)
    {
        var auslosung = new Auslosung { Auslosungsart = Auslosungsart.Manuell };
        auslosung.Regeln.Add(new AuslosungsRegel { Regelart = AuslosungsRegelArt.KeinGleichesLand, IstAktiv = istAktiv });

        AuslosungsRegelKompatibilitaet.Wirkt(AuslosungsRegelArt.KeinGleichesLand, auslosung, rundenart).Should().Be(erwartet);
        AuslosungsRegelKompatibilitaet.Wirkt(AuslosungsRegelArt.KeineGleicheGruppe, auslosung, rundenart).Should().BeFalse();
    }

    [Fact]
    public void HeimrechtRegeln_SchliessenSichGegenseitigAus()
    {
        AuslosungsRegelKompatibilitaet.SchliessenSichAus(
            AuslosungsRegelArt.HeimrechtNachLos, AuslosungsRegelArt.HeimrechtNachSetzliste).Should().BeTrue();
        AuslosungsRegelKompatibilitaet.SchliessenSichAus(
            AuslosungsRegelArt.HeimrechtNachLos, AuslosungsRegelArt.KeinGleichesLand).Should().BeFalse();
        AuslosungsRegelKompatibilitaet.SchliessenSichAus(
            AuslosungsRegelArt.HeimrechtFuerUnterklassige, AuslosungsRegelArt.HeimrechtNachLos).Should().BeTrue();
        AuslosungsRegelKompatibilitaet.SchliessenSichAus(
            AuslosungsRegelArt.HeimrechtFuerUnterklassige, AuslosungsRegelArt.HeimrechtNachSetzliste).Should().BeTrue();
    }

    [Theory]
    [InlineData(Auslosungsart.Manuell,       Rundenart.Gruppenphase)]
    [InlineData(Auslosungsart.Zufall,        Rundenart.Gruppenphase)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    public void HeimrechtFuerUnterklassige_GesperrtWieDieAnderenHeimrechtRegeln(Auslosungsart art, Rundenart rundenart) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(AuslosungsRegelArt.HeimrechtFuerUnterklassige, art, rundenart, false)
            .Should().NotBeNull();

    [Fact]
    public void HeimrechtFuerUnterklassige_WirktImEinzelspielPokal() =>
        AuslosungsRegelKompatibilitaet.SperrGrund(
            AuslosungsRegelArt.HeimrechtFuerUnterklassige, Auslosungsart.Zufall, Rundenart.KoRunde, false)
            .Should().BeNull();

    [Fact]
    public void AngeboteneRegeln_EnthaltenKeineEntfallenenRegeln() =>
        AuslosungsRegelKompatibilitaet.AngeboteneRegeln.Should().HaveCount(7)
            .And.OnlyContain(r => AuslosungsRegelKompatibilitaet.SperrGrund(r, Auslosungsart.Zufall, Rundenart.KoRunde, true)
                                  != "Diese Regel wird nicht mehr unterstützt.");

    // ── Sperrgrund: genauer Text je Zweig (#502) ─────────────────────────────

    private const string LIGA = "In einer Liga spielt jeder gegen jeden — es gibt weder Gruppen noch ausgeloste " +
                                "Paarungen, auf die diese Regel wirken könnte.";
    private const string SCHWEIZER = "Beim Schweizer System bildet der Spielplan-Generator die Paarungen selbst — die " +
                                     "Auslosung legt nur die Startreihenfolge fest.";
    private const string OHNE_GRUPPENPHASE = "Diese Runde folgt auf keine Gruppenphase — es gibt keine früheren " +
                                             "Gruppen, deren Teams sich erneut begegnen könnten.";
    private const string HEIMRECHT_GRUPPENPHASE = "In der Gruppenphase werden Teams nur Gruppen zugeordnet — das " +
                                                  "Heimrecht ergibt sich aus dem Spielplan der Gruppe.";
    private const string HEIMRECHT_TOPFZUORDNUNG = "Die Topf-Zuordnung verteilt Heim- und Auswärtsspiele selbst (je " +
                                                   "Topf ein Heim- und ein Auswärtsgegner).";
    private const string HEIMRECHT_HAND = "Bei der Ziehung von Hand ist die Ziehung selbst das Los — das zuerst " +
                                          "gezogene Team spielt zu Hause.";
    private const string FREILOS_GRUPPENPHASE = "In der Gruppenphase gibt es kein Freilos — alle Teams werden auf " +
                                                "die Gruppen verteilt.";
    private const string FREILOS_TOPFZUORDNUNG = "Die Topf-Zuordnung erfordert gleich große Töpfe — ein Freilos ist " +
                                                 "nicht vorgesehen.";
    private const string KONFOEDERATION = "Die Konföderationsverteilung regelt die Verteilung auf Gruppen — in dieser " +
                                          "Runde werden keine Gruppen ausgelost.";
    private const string NICHT_UNTERSTUETZT = "Diese Regel wird nicht mehr unterstützt.";

    public static TheoryData<AuslosungsRegelArt, Auslosungsart, Rundenart?, bool, string?> SperrGruende() => new()
    {
        // Liga und Schweizer System sperren jede Regel, vor allem anderen — auch eine, die sonst immer gilt
        { AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Liga, true, LIGA },
        { AuslosungsRegelArt.FreilosMoeglich, Auslosungsart.TopfZuordnung, Rundenart.SchweizerSystem, true, SCHWEIZER },
        // Immer aktiv geht jeder anderen Sperre vor
        { AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.KoRunde, false, null },
        // Je Regel die eigene Sperre — „Kein gleiches Land“ hat keine
        { AuslosungsRegelArt.KeinGleichesLand, Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase, false, null },
        { AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.Zufall, Rundenart.KoRunde, false, OHNE_GRUPPENPHASE },
        { AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.Zufall, Rundenart.KoRunde, true, null },
        { AuslosungsRegelArt.HeimrechtFuerUnterklassige, Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase, true, HEIMRECHT_GRUPPENPHASE },
        { AuslosungsRegelArt.HeimrechtNachSetzliste, Auslosungsart.TopfZuordnung, Rundenart.KoRunde, true, HEIMRECHT_TOPFZUORDNUNG },
        { AuslosungsRegelArt.HeimrechtNachLos, Auslosungsart.Manuell, null, true, HEIMRECHT_HAND },
        { AuslosungsRegelArt.HeimrechtNachSetzliste, Auslosungsart.Manuell, Rundenart.KoRunde, true, null },
        { AuslosungsRegelArt.HeimrechtNachLos, Auslosungsart.Zufall, null, true, null },
        { AuslosungsRegelArt.FreilosMoeglich, Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase, true, FREILOS_GRUPPENPHASE },
        { AuslosungsRegelArt.FreilosMoeglich, Auslosungsart.TopfZuordnung, Rundenart.KoRunde, true, FREILOS_TOPFZUORDNUNG },
        { AuslosungsRegelArt.FreilosMoeglich, Auslosungsart.Benutzerdefiniert, Rundenart.Finale, true, null },
        { AuslosungsRegelArt.Konfoederationsverteilung, Auslosungsart.Zufall, null, true, KONFOEDERATION },
        { AuslosungsRegelArt.Konfoederationsverteilung, Auslosungsart.Zufall, Rundenart.KoRunde, true, KONFOEDERATION },
        { AuslosungsRegelArt.Konfoederationsverteilung, Auslosungsart.NachToepfen, Rundenart.Gruppenphase, false, null },
#pragma warning disable CS0618 // entfallene Regel: genau ihr Sperrgrund wird geprüft
        { AuslosungsRegelArt.GesetzteGegenUngesetzte, Auslosungsart.NachSetzliste, Rundenart.KoRunde, true, NICHT_UNTERSTUETZT },
#pragma warning restore CS0618
    };

    [Theory]
    [MemberData(nameof(SperrGruende))]
    public void SperrGrund_GenauerTextJeZweig(
        AuslosungsRegelArt regel, Auslosungsart art, Rundenart? rundenart, bool folgtAufGruppenphase, string? erwartet) =>
        AuslosungsRegelKompatibilitaet.SperrGrund(regel, art, rundenart, folgtAufGruppenphase).Should().Be(erwartet);

    [Theory]
    [InlineData("1. Gruppe A", "A", 1)]
    [InlineData("2. Gruppe D", "D", 2)]
    [InlineData("Sieger Gruppe B", "B", 1)]
    public void GruppenHerkunft_LiestGruppeUndPlatzAusPlatzhalter(string name, string gruppe, int platz) =>
        GruppenHerkunft.AusPlatzhalter(name).Should().Be((gruppe, platz));

    [Theory]
    [InlineData("3. Gruppe A/D/E/F")]
    [InlineData("Sieger Viertelfinale 1")]
    [InlineData("Team 4")]
    [InlineData(null)]
    public void GruppenHerkunft_OhneEindeutigeGruppe_IstNull(string? name) =>
        GruppenHerkunft.AusPlatzhalter(name).Should().BeNull();
}
