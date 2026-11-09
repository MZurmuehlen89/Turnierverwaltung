using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>
/// Prüft die zentrale Rundenart × Auslosungsart-Matrix (Issue #94) — einzige Quelle für die
/// serverseitige Hart-Validierung (<see cref="AuslosungsValidatorTests"/> deckt die Verwendung
/// im Validator bereits ab) und die WPF-ComboBox-Filterung.
/// </summary>
public class AuslosungsKompatibilitaetTests
{
    [Theory]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Liga)]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Gruppenphase)]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.SchweizerSystem)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.Gruppenphase)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.KoRunde)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.Finale)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.SpielUmPlatzDrei)]
    // #627: lost alle Gegner im Voraus, im Schweizer System folgen sie je Spieltag aus der Tabelle
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.SchweizerSystem)]
    // Issue #347: TopfLiga nur per Topf-Zuordnung (bzw. Manuell über die Vorbelegung, siehe unten)
    [InlineData(Auslosungsart.Manuell, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.Zufall, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.NachToepfen, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.NachSetzliste, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.Benutzerdefiniert, Rundenart.TopfLiga)]
    public void InkompatibleKombination_IstNichtKompatibelUndLiefertGrund(Auslosungsart art, Rundenart rundenart)
    {
        AuslosungsKompatibilitaet.IstKompatibel(rundenart, art).Should().BeFalse();
        AuslosungsKompatibilitaet.InkompatibilitaetsGrund(rundenart, art).Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.KoRunde)]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.Finale)]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite, Rundenart.SpielUmPlatzDrei)]
    [InlineData(Auslosungsart.TopfZuordnung, Rundenart.TopfLiga)]
    [InlineData(Auslosungsart.Manuell, Rundenart.KoRunde)]
    [InlineData(Auslosungsart.Manuell, Rundenart.Gruppenphase)]
    // Bewusst nur schwach gewarnt statt hart gesperrt (Nutzer-Entscheidung Issue #94): funktioniert
    // korrekt (Bugfix 606ba6f), hat bei Liga (eine Gruppe) nur keine sichtbare Wirkung.
    [InlineData(Auslosungsart.NachToepfen, Rundenart.Liga)]
    [InlineData(Auslosungsart.NachSetzliste, Rundenart.Liga)]
    [InlineData(Auslosungsart.NachSetzliste, Rundenart.SchweizerSystem)]
    public void KompatibleKombination_IstKompatibelUndLiefertKeinenGrund(Auslosungsart art, Rundenart rundenart)
    {
        AuslosungsKompatibilitaet.IstKompatibel(rundenart, art).Should().BeTrue();
        AuslosungsKompatibilitaet.InkompatibilitaetsGrund(rundenart, art).Should().BeNull();
    }

    // Die Vorbelegung einer TopfLiga bleibt erlaubt, nur die Ziehung von Hand nicht (#482).
    [Fact]
    public void Manuell_BeiTopfLiga_NurMitFestVorgegebenerPaarung() =>
        AuslosungsKompatibilitaet.IstKompatibel(Rundenart.TopfLiga, Auslosungsart.Manuell, paarungFestVorgegeben: true)
            .Should().BeTrue();

    [Theory]
    [MemberData(nameof(AlleRundenarten))]
    public void Zufall_IstMitJederRundenartAusserTopfLigaKompatibel(Rundenart rundenart) =>
        AuslosungsKompatibilitaet.IstKompatibel(rundenart, Auslosungsart.Zufall).Should().BeTrue();

    public static TheoryData<Rundenart> AlleRundenarten() =>
        [.. Enum.GetValues<Rundenart>().Where(r => r != Rundenart.TopfLiga)];
}
