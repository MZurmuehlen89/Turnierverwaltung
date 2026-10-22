using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class AuslosungsErgebnisartTests
{
    private static Auslosung Auslosung(Auslosungsart art, int toepfe, params AuslosungsErgebnis[] ergebnisse) => new()
    {
        Auslosungsart = art,
        Lostoepfe = Enumerable.Range(1, toepfe).Select(i => new Lostopf { Reihenfolge = i }).ToList(),
        Ergebnisse = ergebnisse.ToList(),
    };

    private static AuslosungsErgebnis GruppenZeile(int gruppe) => new() { PaarungsNummer = gruppe, HeimMannschaftId = gruppe * 10 };
    private static AuslosungsErgebnis Paarung(int nr) => new() { PaarungsNummer = nr, HeimMannschaftId = 1, AuswaertsMannschaftId = 2 };

    [Fact]
    public void Von_OhneErgebnisse_Paarungen() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.TopfZuordnung, 4))
            .Should().Be(AuslosungsErgebnisart.Paarungen);

    [Fact]
    public void Von_NurZeilenOhneGegner_Gruppen() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.NachToepfen, 4, GruppenZeile(1), GruppenZeile(2)))
            .Should().Be(AuslosungsErgebnisart.Gruppen);

    [Fact]
    public void Von_GegnerAlsPlatzhalter_KeinGruppenergebnis() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.Zufall, 1,
                GruppenZeile(1), new AuslosungsErgebnis { PaarungsNummer = 2, HeimMannschaftId = 1, AuswaertsPlatzhalterName = "Sieger A" }))
            .Should().Be(AuslosungsErgebnisart.Paarungen);

    // Ein Freilos hat keinen Gegner, macht die Auslosung aber nicht zur Gruppenauslosung.
    [Fact]
    public void Von_FreilosOhneGegner_Paarungen() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.Zufall, 1,
                new AuslosungsErgebnis { PaarungsNummer = 1, HeimMannschaftId = 1, IstFreilos = true }))
            .Should().Be(AuslosungsErgebnisart.Paarungen);

    // #362: Ligaphase mit mehreren Töpfen.
    [Fact]
    public void Von_TopfZuordnungMitZweiToepfen_GegnerMatrix() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.TopfZuordnung, 2, Paarung(1)))
            .Should().Be(AuslosungsErgebnisart.GegnerMatrix);

    [Fact]
    public void Von_TopfZuordnungMitEinemTopf_Paarungen() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.TopfZuordnung, 1, Paarung(1)))
            .Should().Be(AuslosungsErgebnisart.Paarungen);

    [Fact]
    public void Von_AndereArtMitMehrerenToepfen_Paarungen() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.NachToepfen, 4, Paarung(1)))
            .Should().Be(AuslosungsErgebnisart.Paarungen);

    // Gruppen gehen vor: eine Topf-Zuordnung, die nur gegnerlose Zeilen gespeichert hat, ist ein Gruppenergebnis.
    [Fact]
    public void Von_TopfZuordnungNurZeilenOhneGegner_Gruppen() =>
        AuslosungsErgebnisartErmittlung.Von(Auslosung(Auslosungsart.TopfZuordnung, 4, GruppenZeile(1)))
            .Should().Be(AuslosungsErgebnisart.Gruppen);
}
