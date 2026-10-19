using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Wann die Auswahl der Gruppenposition wirkt (Issue #475).</summary>
public class GruppenpositionKompatibilitaetTests
{
    [Theory]
    [InlineData(Auslosungsart.NachToepfen)]
    [InlineData(Auslosungsart.NachSetzliste)]
    [InlineData(null)]
    public void Gruppenphase_MitToepfenOderOhneVorgabe_Waehlbar(Auslosungsart? art) =>
        GruppenpositionKompatibilitaet.SperrGrund(Rundenart.Gruppenphase, art).Should().BeNull();

    [Theory]
    [InlineData(Rundenart.Gruppenphase, Auslosungsart.Zufall, "*immer gelost*")]
    [InlineData(Rundenart.Gruppenphase, Auslosungsart.Manuell, "*manuellen*")]
    [InlineData(Rundenart.Liga, Auslosungsart.NachToepfen, "*Liga*")]
    [InlineData(Rundenart.KoRunde, Auslosungsart.NachToepfen, "*nur in der Gruppenphase*")]
    public void Gesperrt_MitGrund(Rundenart rundenart, Auslosungsart art, string grund) =>
        GruppenpositionKompatibilitaet.SperrGrund(rundenart, art).Should().Match(grund);

    [Theory]
    [InlineData(Auslosungsart.NachToepfen, Gruppenposition.Topf1Fest, Gruppenposition.Topf1Fest)]
    [InlineData(Auslosungsart.NachSetzliste, Gruppenposition.NachTopf, Gruppenposition.NachTopf)]
    [InlineData(Auslosungsart.Zufall, Gruppenposition.NachTopf, Gruppenposition.AlleGelost)]
    public void Wirksam_OhneToepfeImmerGelost(Auslosungsart art, Gruppenposition gewaehlt, Gruppenposition erwartet) =>
        GruppenpositionKompatibilitaet.Wirksam(art, gewaehlt).Should().Be(erwartet);
}
