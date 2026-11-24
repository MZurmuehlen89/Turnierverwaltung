using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using static Fussballturnierverwaltung.Tests.Auslosungen.AuslosungTestdaten;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

/// <summary>Regel-Schalter des <see cref="AuslosungService"/> (#626): Aktivieren und Deaktivieren sind an einer
/// abgeschlossenen Auslosung gleichermaßen gesperrt — vorher ging nur das Abhaken durch.</summary>
public class AuslosungServiceRegelTests
{
    private const AuslosungsRegelArt REGEL = AuslosungsRegelArt.KeinGleichesLand;

    private readonly FakeAuslosungRepository _repo = new();
    private readonly AuslosungService _service;
    private readonly Auslosung _auslosung;

    public AuslosungServiceRegelTests()
    {
        _service   = AuslosungDienste.Service(_repo);
        _auslosung = NeueAuslosung(Rundenart.KoRunde, Auslosungsart.NachToepfen, [TopfMitTeams(1, 1, 2), TopfMitTeams(2, 3, 2)]);
        _auslosung.Regeln.Add(new AuslosungsRegel { AuslosungId = _auslosung.Id, Regelart = REGEL, Beschreibung = "x", IstAktiv = true });
        _repo.Speichern(_auslosung);
    }

    private bool RegelAktiv() => _auslosung.Regeln.Single(r => r.Regelart == REGEL).IstAktiv;

    [Fact]
    public async Task RegelDeaktivieren_OffeneAuslosung_SchaltetAb()
    {
        await _service.RegelDeaktivierenAsync(_auslosung.Id, REGEL);

        RegelAktiv().Should().BeFalse();
    }

    [Fact]
    public async Task RegelAktivieren_OffeneAuslosung_SchaltetAn()
    {
        await _service.RegelDeaktivierenAsync(_auslosung.Id, REGEL);
        await _service.RegelHinzufuegenOderAktivierenAsync(_auslosung.Id, REGEL, "x");

        RegelAktiv().Should().BeTrue();
    }

    [Fact]
    public async Task RegelDeaktivieren_AbgeschlosseneAuslosung_FehlerUndRegelBleibtAktiv()
    {
        _auslosung.Status = AuslosungsStatus.Abgeschlossen;

        var abhaken = () => _service.RegelDeaktivierenAsync(_auslosung.Id, REGEL);

        await abhaken.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nicht geändert*");
        RegelAktiv().Should().BeTrue();
    }

    [Fact]
    public async Task RegelDeaktivieren_AbgebrocheneAuslosung_FehlerUndRegelBleibtAktiv()
    {
        _auslosung.Status = AuslosungsStatus.Abgebrochen;

        var abhaken = () => _service.RegelDeaktivierenAsync(_auslosung.Id, REGEL);

        await abhaken.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nicht geändert*");
        RegelAktiv().Should().BeTrue();
    }

    [Fact]
    public async Task Bearbeiten_AbgebrocheneAuslosung_Fehler()
    {
        _auslosung.Status = AuslosungsStatus.Abgebrochen;

        var bearbeiten = () => _service.BearbeitenAsync(
            _auslosung.Id, "neu", _auslosung.Auslosungsart, _auslosung.Gruppenposition);

        await bearbeiten.Should().ThrowAsync<InvalidOperationException>().WithMessage("*abgebrochene Auslosung*");
        _auslosung.Name.Should().NotBe("neu");
    }

    [Fact]
    public async Task RegelAktivieren_AbgeschlosseneAuslosung_Fehler()
    {
        _auslosung.Status = AuslosungsStatus.Abgeschlossen;

        var anhaken = () => _service.RegelHinzufuegenOderAktivierenAsync(_auslosung.Id, AuslosungsRegelArt.KeineGleicheGruppe, "y");

        await anhaken.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nicht geändert*");
        _auslosung.Regeln.Should().ContainSingle();
    }
}
