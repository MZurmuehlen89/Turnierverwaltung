using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Lostopf-Verwaltung des <see cref="AuslosungService"/> sowie die Nachbereitung beim Zurücksetzen und
/// Löschen einer Auslosung (#437).</summary>
public class AuslosungServiceLostopfTests
{
    private readonly FakeAuslosungRepository _repo = new();
    private readonly FakePlatzhalterKaskadeService _kaskade = new();
    private readonly AuslosungService _service;
    private readonly Auslosung _auslosung;

    public AuslosungServiceLostopfTests()
    {
        _service = AuslosungDienste.Service(_repo, kaskade: _kaskade);

        // Topf 1: Mannschaften 1 und 2, Topf 2: Mannschaften 3 und 4
        _auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, [TopfMitTeams(1, 1, 2), TopfMitTeams(2, 3, 2)]);
        _repo.Speichern(_auslosung);
    }

    private IEnumerable<int?> MannschaftenInTopf(int reihenfolge) =>
        _auslosung.Lostoepfe.Single(l => l.Reihenfolge == reihenfolge).Eintraege.Select(e => e.MannschaftId);

    // ── Einträge hinzufügen und entfernen ─────────────────────────────────────

    [Fact]
    public async Task MannschaftZuLostopfHinzufuegen_HaengtEintragAnDenTopf()
    {
        await _service.MannschaftZuLostopfHinzufuegenAsync(_auslosung.Id, lostopfId: 2, mannschaftId: 5, setzPosition: 3);

        MannschaftenInTopf(2).Should().Equal(3, 4, 5);
        _auslosung.Lostoepfe.Single(l => l.Id == 2).Eintraege.Last().SetzPosition.Should().Be(3);
    }

    [Fact]
    public async Task MannschaftZuLostopfHinzufuegen_MannschaftSchonInAnderemTopf_Fehler()
    {
        var hinzufuegen = () => _service.MannschaftZuLostopfHinzufuegenAsync(_auslosung.Id, lostopfId: 2, mannschaftId: 1);

        await hinzufuegen.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bereits in einem Lostopf*");
    }

    [Fact]
    public async Task PlatzhalterZuLostopfHinzufuegen_NameWirdGetrimmt_DoppelterNameIstFehler()
    {
        await _service.PlatzhalterZuLostopfHinzufuegenAsync(_auslosung.Id, lostopfId: 1, " Sieger HF 1 ");

        _auslosung.Lostoepfe.Single(l => l.Id == 1).Eintraege.Last().PlatzhalterName.Should().Be("Sieger HF 1");

        var nochmal = () => _service.PlatzhalterZuLostopfHinzufuegenAsync(_auslosung.Id, lostopfId: 2, "Sieger HF 1");
        await nochmal.Should().ThrowAsync<InvalidOperationException>().WithMessage("*'Sieger HF 1'*bereits*");
    }

    [Fact]
    public async Task EintragAusLostopfEntfernen_EntferntNurDiesenEintrag()
    {
        await _service.EintragAusLostopfEntfernenAsync(_auslosung.Id, lostopfId: 1, eintragId: 2);

        MannschaftenInTopf(1).Should().Equal(1);
        MannschaftenInTopf(2).Should().Equal(3, 4);
    }

    [Fact]
    public async Task EintragAusLostopfEntfernen_EintragLiegtInAnderemTopf_AendertNichts()
    {
        await _service.EintragAusLostopfEntfernenAsync(_auslosung.Id, lostopfId: 1, eintragId: 3);

        MannschaftenInTopf(1).Should().Equal(1, 2);
        MannschaftenInTopf(2).Should().Equal(3, 4);
    }

    [Fact]
    public async Task LostopfLeeren_LeertNurDiesenTopf()
    {
        await _service.LostopfLeerenAsync(_auslosung.Id, lostopfId: 1);

        MannschaftenInTopf(1).Should().BeEmpty();
        MannschaftenInTopf(2).Should().Equal(3, 4);
    }

    [Fact]
    public async Task AlleToepfeZuruecksetzen_LeertAlleToepfe_ToepfeBleiben()
    {
        await _service.AlleToepfeZuruecksetzenAsync(_auslosung.Id);

        _auslosung.Lostoepfe.Should().HaveCount(2);
        _auslosung.Lostoepfe.Should().AllSatisfy(l => l.Eintraege.Should().BeEmpty());
    }

    // ── Gemeinsamer Vorspann: Auslosung bearbeitbar, Lostopf vorhanden ────────

    public static TheoryData<string, Func<AuslosungService, int, int, Task>> LostopfAenderungen => new()
    {
        { "Mannschaft hinzufügen", (s, auslosungId, lostopfId) => s.MannschaftZuLostopfHinzufuegenAsync(auslosungId, lostopfId, 5) },
        { "Platzhalter hinzufügen", (s, auslosungId, lostopfId) => s.PlatzhalterZuLostopfHinzufuegenAsync(auslosungId, lostopfId, "Sieger HF 1") },
        { "Eintrag entfernen", (s, auslosungId, lostopfId) => s.EintragAusLostopfEntfernenAsync(auslosungId, lostopfId, 1) },
        { "Lostopf leeren", (s, auslosungId, lostopfId) => s.LostopfLeerenAsync(auslosungId, lostopfId) }
    };

    [Theory]
    [MemberData(nameof(LostopfAenderungen))]
    public async Task LostopfAenderung_UnbekannterLostopf_Fehler(string _, Func<AuslosungService, int, int, Task> aendern)
    {
        var aenderung = () => aendern(_service, _auslosung.Id, 99);

        await aenderung.Should().ThrowAsync<InvalidOperationException>().WithMessage("Lostopf 99 nicht gefunden.");
    }

    [Theory]
    [MemberData(nameof(LostopfAenderungen))]
    public async Task LostopfAenderung_AbgeschlosseneAuslosung_Fehler(string _, Func<AuslosungService, int, int, Task> aendern)
    {
        _auslosung.Status = AuslosungsStatus.Abgeschlossen;

        var aenderung = () => aendern(_service, _auslosung.Id, 1);

        await aenderung.Should().ThrowAsync<InvalidOperationException>().WithMessage("*abgeschlossene Auslosung*");
        MannschaftenInTopf(1).Should().Equal(1, 2);
    }

    [Theory]
    [MemberData(nameof(LostopfAenderungen))]
    public async Task LostopfAenderung_UnbekannteAuslosung_Fehler(string _, Func<AuslosungService, int, int, Task> aendern)
    {
        var aenderung = () => aendern(_service, 123, 1);

        await aenderung.Should().ThrowAsync<InvalidOperationException>().WithMessage("Auslosung 123 nicht gefunden.");
    }

    // ── Zurücksetzen und Löschen: Spielplan der Runde und Folgerunden ─────────

    [Fact]
    public async Task Zuruecksetzen_LoeschtSpielplanDerRunde_UndLoestKaskadeAus()
    {
        await _service.ZuruecksetzenAsync(_auslosung.Id);

        _kaskade.GeloeschteRundenIds.Should().Equal(_auslosung.TurnierRundeId);
        _kaskade.AufrufeAnzahl.Should().Be(1);
    }

    [Fact]
    public async Task Loeschen_EntferntAuslosung_LoeschtSpielplanDerRunde_UndLoestKaskadeAus()
    {
        await _service.LoeschenAsync(_auslosung.Id);

        _repo.Aktuelle.Should().BeNull();
        _kaskade.GeloeschteRundenIds.Should().Equal(_auslosung.TurnierRundeId);
        _kaskade.AufrufeAnzahl.Should().Be(1);
    }
}
