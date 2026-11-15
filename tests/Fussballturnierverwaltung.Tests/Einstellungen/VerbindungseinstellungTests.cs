using FluentAssertions;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Fussballturnierverwaltung.Tests.Einstellungen;

/// <summary>Datenbankverbindung aus Einstellung oder Vorgabe (#49).</summary>
public class VerbindungseinstellungTests
{
    private static DatenbankVerbindung Verbindung(string? kennwortVerschluesselt = null) => new()
    {
        Server = "server1",
        Port = 5433,
        Datenbank = "turniere",
        Benutzer = "helfer",
        KennwortVerschluesselt = kennwortVerschluesselt
    };

    [Fact]
    public void Zusammensetzen_OhneKennwort_LaesstPasswordWeg()
    {
        var builder = new NpgsqlConnectionStringBuilder(Verbindungseinstellung.Zusammensetzen(Verbindung()));

        builder.Host.Should().Be("server1");
        builder.Port.Should().Be(5433);
        builder.Database.Should().Be("turniere");
        builder.Username.Should().Be("helfer");
        builder.Password.Should().BeNull("ohne Kennwort sucht Npgsql es in pgpass.conf");
    }

    [Fact]
    public void AusZeichenfolge_LiestWerteOhneKennwort()
    {
        var verbindung = Verbindungseinstellung.AusZeichenfolge(
            "Host=testhost;Port=5432;Database=Fussballturnierverwaltung_GuiTest_1;Username=turnierverwaltung;Password=geheim");

        verbindung.Server.Should().Be("testhost");
        verbindung.Datenbank.Should().Be("Fussballturnierverwaltung_GuiTest_1");
        verbindung.Benutzer.Should().Be("turnierverwaltung");
        verbindung.KennwortVerschluesselt.Should().BeNull();
    }

    [Fact]
    public void Vorgabe_HatVorrangUndWirdAngezeigt()
    {
        var konfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Standard"] = "Host=vorgabe;Database=test;Username=tester"
            })
            .Build();
        var einstellung = new Verbindungseinstellung(new EinstellungenService(), konfiguration);

        einstellung.IstVorgegeben.Should().BeTrue();
        einstellung.Verbindungszeichenfolge().Should().Be("Host=vorgabe;Database=test;Username=tester");
        einstellung.Anzeigen().Server.Should().Be("vorgabe");
    }

    [Fact]
    public void MitKennwort_LeeresKennwortBehaeltGespeichertes()
    {
        var ergebnis = Verbindungseinstellung.MitKennwort(Verbindung(), "bisher", "");

        ergebnis.KennwortVerschluesselt.Should().Be("bisher");
    }

    [Fact]
    public void MitKennwort_BereinigtLeerzeichen()
    {
        var eingabe = Verbindung();
        eingabe.Server = " server1 ";
        eingabe.Benutzer = "helfer ";

        var ergebnis = Verbindungseinstellung.MitKennwort(eingabe, null, null);

        ergebnis.Server.Should().Be("server1");
        ergebnis.Benutzer.Should().Be("helfer");
        ergebnis.KennwortVerschluesselt.Should().BeNull();
    }

    [Fact]
    public void Kennwort_WirdVerschluesseltGespeichertUndBeimZusammensetzenEntschluesselt()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI gibt es nur unter Windows.");

        var ergebnis = Verbindungseinstellung.MitKennwort(Verbindung(), "bisher", "geheim");

        ergebnis.KennwortVerschluesselt.Should().NotBeNullOrEmpty().And.NotContain("geheim");
        new NpgsqlConnectionStringBuilder(Verbindungseinstellung.Zusammensetzen(ergebnis)).Password.Should().Be("geheim");
    }
}
