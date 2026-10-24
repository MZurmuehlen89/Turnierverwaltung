using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Das Programm legt Gastgeber und Titelverteidiger mit fester Gruppe selbst in Topf 1 — beim Anlegen der
/// Töpfe, nach dem Zurücksetzen und spätestens beim Auslosen (#487).</summary>
public class AuslosungServiceGruppenkoepfeTests
{
    private const int GASTGEBER = 10;

    private readonly FakeAuslosungRepository _repo = new();
    private readonly AuslosungService _service;

    public AuslosungServiceGruppenkoepfeTests()
    {
        _service = new AuslosungService(
            _repo, new FakeVerbandsQuoteRepository(), new FakeLandesQuoteRepository(), new FakeTurnierRundeRepository(),
            new AuslosungsValidator(), new FakePlatzhalterKaskadeService(), new FakeRanglisteRepository(),
            NullLogger<AuslosungService>.Instance);
        _repo.Kandidaten = new GruppenkopfKandidaten([GASTGEBER], null, new Dictionary<int, string> { [GASTGEBER] = "Luxemburg" });
    }

    // Drei Gruppen zu je drei Teams, der Gastgeber führt Gruppe B an.
    private Auslosung Gruppenauslosung(params Lostopf[] toepfe)
    {
        var auslosung = NeueAuslosung(Rundenart.Gruppenphase, Auslosungsart.NachToepfen, toepfe, gruppenAnzahl: 3);
        auslosung.TurnierRunde.GastgeberGruppen = [2];
        auslosung.TurnierRunde.EingangsMannschaftsanzahl = 9;
        _repo.Speichern(auslosung);
        return auslosung;
    }

    private static IEnumerable<int?> Topf1(Auslosung auslosung) =>
        auslosung.Lostoepfe.Single(l => l.Reihenfolge == 1).Eintraege.Select(e => e.MannschaftId);

    [Fact]
    public async Task LostoepfeAnlegen_GastgeberLiegtSofortInTopf1()
    {
        var auslosung = Gruppenauslosung();

        await _service.LostoepfeSynchronisierenAsync(auslosung.Id, 3);

        Topf1(auslosung).Should().Equal(GASTGEBER);
    }

    [Fact]
    public async Task AlleToepfeZuruecksetzen_GastgeberBleibtInTopf1()
    {
        var auslosung = Gruppenauslosung(TopfMitTeams(1, 1, 3), TopfMitTeams(2, 4, 3), TopfMitTeams(3, 7, 3));

        await _service.AlleToepfeZuruecksetzenAsync(auslosung.Id);

        Topf1(auslosung).Should().Equal(GASTGEBER);
        auslosung.Lostoepfe.Where(l => l.Reihenfolge > 1).Should().AllSatisfy(l => l.Eintraege.Should().BeEmpty());
    }

    // Der Gastgeber wurde erst nach dem Befüllen eingetragen, in Topf 1 ist aber noch ein Platz frei.
    [Fact]
    public async Task Ausfuehren_GastgeberFehlt_KommtInTopf1UndFuehrtSeineGruppeAn()
    {
        var auslosung = Gruppenauslosung(TopfMitTeams(1, 1, 2), TopfMitTeams(2, 4, 3), TopfMitTeams(3, 7, 3));

        var ergebnisse = await _service.AusfuehrenAsync(auslosung.Id);

        Topf1(auslosung).Should().Contain(GASTGEBER);
        ergebnisse.First(e => e.PaarungsNummer == 2).HeimMannschaftId.Should().Be(GASTGEBER);
    }

    [Fact]
    public async Task Ausfuehren_GastgeberFehltUndToepfeVoll_MeldungNenntIhn()
    {
        var auslosung = Gruppenauslosung(TopfMitTeams(1, 1, 3), TopfMitTeams(2, 4, 3), TopfMitTeams(3, 7, 3));

        var ausfuehren = () => _service.AusfuehrenAsync(auslosung.Id);

        await ausfuehren.Should().ThrowAsync<InvalidOperationException>().WithMessage("Luxemburg führt*schon voll*");
        _repo.GespeicherteErgebnisse.Should().BeNull();
    }

    // Ziehung von Hand (#482): dieselbe Vorbereitung wie beim Losen — der Gastgeber steht vor dem ersten Zug in B.
    [Fact]
    public async Task ZiehungStarten_GastgeberFehlt_StehtVorabInSeinerGruppe()
    {
        var auslosung = Gruppenauslosung(TopfMitTeams(1, 1, 2), TopfMitTeams(2, 4, 3), TopfMitTeams(3, 7, 3));
        auslosung.Auslosungsart = Auslosungsart.Manuell;

        var ziehung = await _service.ZiehungStartenAsync(auslosung.Id);

        var gastgeber = ziehung.Teilnehmer.Single(t => t.Eintrag.MannschaftId == GASTGEBER).Index;
        ziehung.Gruppen[1].Plaetze[0].Should().Be(gastgeber);
        _repo.GespeicherteErgebnisse.Should().BeNull();
    }

    [Fact]
    public async Task ZiehungStarten_NurBeiManuell()
    {
        var auslosung = Gruppenauslosung(TopfMitTeams(1, 1, 3), TopfMitTeams(2, 4, 3), TopfMitTeams(3, 7, 2));

        var starten = () => _service.ZiehungStartenAsync(auslosung.Id);

        await starten.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nur bei der Auslosungsart „Manuell“*");
    }

    [Fact]
    public async Task KeineGruppenkoepfeVorgegeben_ToepfeBleibenLeer()
    {
        var auslosung = Gruppenauslosung();
        auslosung.TurnierRunde.GastgeberGruppen = [];

        await _service.LostoepfeSynchronisierenAsync(auslosung.Id, 3);

        Topf1(auslosung).Should().BeEmpty();
    }
}
