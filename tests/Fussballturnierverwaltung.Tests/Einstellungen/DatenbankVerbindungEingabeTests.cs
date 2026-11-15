using System.Text.Json;
using FluentAssertions;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Application.Validierung;

namespace Fussballturnierverwaltung.Tests.Einstellungen;

/// <summary>Formular der Datenbankverbindung (#49, AK3/AK4/AK8).</summary>
public class DatenbankVerbindungEingabeTests
{
    private static readonly DatenbankVerbindung _gespeichert = new()
    {
        Server = "localhost", Port = 5432, Datenbank = "turnierverwaltung", Benutzer = "turnierverwaltung",
        KennwortVerschluesselt = "xyz"
    };

    // ── AK3: geändert / zurücksetzen ─────────────────

    [Fact]
    public void Aus_UebernimmtGespeicherteWerte_UndWeichtNichtAb()
    {
        var eingabe = DatenbankVerbindungEingabe.Aus(_gespeichert);

        eingabe.Should().Be(new DatenbankVerbindungEingabe("localhost", "5432", "turnierverwaltung", "turnierverwaltung"));
        eingabe.WeichtAbVon(_gespeichert).Should().BeFalse();
    }

    [Theory]
    [InlineData("server2", "5432", "turnierverwaltung", "turnierverwaltung")]
    [InlineData("localhost", "5433", "turnierverwaltung", "turnierverwaltung")]
    [InlineData("localhost", "5432", "andere", "turnierverwaltung")]
    [InlineData("localhost", "5432", "turnierverwaltung", "postgres")]
    public void WeichtAbVon_JedesGeaenderteFeld(string server, string port, string datenbank, string benutzer) =>
        new DatenbankVerbindungEingabe(server, port, datenbank, benutzer).WeichtAbVon(_gespeichert).Should().BeTrue();

    [Fact]
    public void WeichtAbVon_LeerzeichenAmRandZaehlenNicht() =>
        new DatenbankVerbindungEingabe(" localhost ", "5432 ", "turnierverwaltung", " turnierverwaltung")
            .WeichtAbVon(_gespeichert).Should().BeFalse();

    [Fact]
    public void AlsVerbindung_BereinigtUndWandeltPort()
    {
        var verbindung = new DatenbankVerbindungEingabe(" server2 ", " 5433", "turniere ", "helfer").AlsVerbindung();

        verbindung.Server.Should().Be("server2");
        verbindung.Port.Should().Be(5433);
        verbindung.Datenbank.Should().Be("turniere");
        verbindung.Benutzer.Should().Be("helfer");
        verbindung.KennwortVerschluesselt.Should().BeNull("das Kennwort kommt getrennt dazu");
    }

    // ── AK4: ungültige Eingabe ───────────────────────

    [Fact]
    public void Validieren_GueltigeEingabe_Ok() =>
        new DatenbankVerbindungValidator().Validieren(DatenbankVerbindungEingabe.Aus(_gespeichert))
            .IstGueltig.Should().BeTrue();

    [Theory]
    [InlineData(" ", "5432", "db", "user", nameof(DatenbankVerbindungEingabe.Server))]
    [InlineData("host", "", "db", "user", nameof(DatenbankVerbindungEingabe.Port))]
    [InlineData("host", "abc", "db", "user", nameof(DatenbankVerbindungEingabe.Port))]
    [InlineData("host", "0", "db", "user", nameof(DatenbankVerbindungEingabe.Port))]
    [InlineData("host", "65536", "db", "user", nameof(DatenbankVerbindungEingabe.Port))]
    [InlineData("host", "5432", "", "user", nameof(DatenbankVerbindungEingabe.Datenbank))]
    [InlineData("host", "5432", "db", "  ", nameof(DatenbankVerbindungEingabe.Benutzer))]
    public void Validieren_UngueltigesFeld_WirdMarkiert(string server, string port, string datenbank, string benutzer, string feld)
    {
        var ergebnis = new DatenbankVerbindungValidator().Validieren(new(server, port, datenbank, benutzer));

        ergebnis.IstGueltig.Should().BeFalse();
        ergebnis.Meldungen.Should().ContainSingle().Which.Feld.Should().Be(feld);
    }

    // ── AK8: Vorgaben, auch für ältere Einstellungsdateien ──

    [Fact]
    public void AlteEinstellungsdateiOhneDatenbank_ErgibtVorgaben()
    {
        var einstellungen = JsonSerializer.Deserialize<BenutzerEinstellungen>(
            """{ "ThemeModus": 1, "AblaufprotokollAktiv": true }""")!;

        einstellungen.Datenbank.Should().BeEquivalentTo(new DatenbankVerbindung
        {
            Server = "localhost", Port = 5432, Datenbank = "turnierverwaltung", Benutzer = "turnierverwaltung",
            KennwortVerschluesselt = null
        });
    }
}
