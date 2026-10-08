using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class ManuellePaarungEingabeTests
{
    [Fact]
    public void FuerGruppenplatz_Mannschaft_TraegtNurDieIdEinUndDieGruppeAlsPaarungsnummer() =>
        ManuellePaarungEingabe.FuerGruppenplatz(gruppenNr: 3, mannschaftId: 42, name: "FC Beispiel")
            .Should().Be(new ManuellePaarungEingabe(42, null, null, null, 3));

    [Fact]
    public void FuerGruppenplatz_Platzhalter_TraegtDenNamenEin() =>
        ManuellePaarungEingabe.FuerGruppenplatz(gruppenNr: 1, mannschaftId: null, name: "Platz 9 (Ligaphase)")
            .Should().Be(new ManuellePaarungEingabe(null, "Platz 9 (Ligaphase)", null, null, 1));

    [Fact]
    public void FuerPaarung_ZweiMannschaften_TraegtBeideIdsEin() =>
        ManuellePaarungEingabe.FuerPaarung(5, (42, "FC Beispiel"), (43, "SV Muster"))
            .Should().Be(new ManuellePaarungEingabe(42, null, 43, null, 5));

    [Fact]
    public void FuerPaarung_ZweiPlatzhalter_TraegtBeideNamenEin() =>
        ManuellePaarungEingabe.FuerPaarung(1, (null, "1. Gruppe A"), (null, "2. Gruppe B"))
            .Should().Be(new ManuellePaarungEingabe(null, "1. Gruppe A", null, "2. Gruppe B", 1));

    [Fact]
    public void FuerPaarung_MannschaftGegenPlatzhalter_MischtIdUndNamen() =>
        ManuellePaarungEingabe.FuerPaarung(2, (42, "FC Beispiel"), (null, "Sieger Halbfinale 1"))
            .Should().Be(new ManuellePaarungEingabe(42, null, null, "Sieger Halbfinale 1", 2));

    [Fact]
    public void FuerPaarung_OhneAuswaertsteam_LaesstDieAuswaertsseiteLeer() =>
        ManuellePaarungEingabe.FuerPaarung(4, (42, "FC Beispiel"), auswaerts: null)
            .Should().Be(new ManuellePaarungEingabe(42, null, null, null, 4));
}
