using System.Net.Sockets;
using FluentAssertions;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using Npgsql;

namespace Fussballturnierverwaltung.Tests.Einstellungen;

/// <summary>Ursache eines Verbindungsfehlers in Worten (#49, AK5).</summary>
public class VerbindungspruefungTests
{
    private static readonly NpgsqlConnectionStringBuilder _ziel =
        new("Host=server1;Port=5433;Database=turniere;Username=helfer");

    private static PostgresException Postgres(string sqlState) => new("Meldung des Servers", "FATAL", "FATAL", sqlState);

    [Theory]
    [InlineData(PostgresErrorCodes.InvalidPassword)]
    [InlineData(PostgresErrorCodes.InvalidAuthorizationSpecification)]
    public void Anmeldung_Abgelehnt(string sqlState) =>
        Verbindungspruefung.Ursache(Postgres(sqlState), _ziel, pgPassword: null)
            .Should().Be("Anmeldung als „helfer“ abgelehnt: Kennwort falsch oder fehlt.");

    [Fact]
    public void Anmeldung_Abgelehnt_OhneKennwort_PgPasswordGesetzt_NenntVariable() =>
        Verbindungspruefung.Ursache(Postgres(PostgresErrorCodes.InvalidPassword), _ziel, pgPassword: "alt")
            .Should().Be("Anmeldung als „helfer“ abgelehnt: Kennwort falsch oder fehlt. Die Umgebungsvariable "
                         + "PGPASSWORD ist gesetzt und hat Vorrang vor der Kennwortdatei (pgpass.conf).");

    // Mit Kennwort in der Verbindung spielt PGPASSWORD keine Rolle
    [Fact]
    public void Anmeldung_Abgelehnt_MitKennwort_PgPasswordGesetzt_OhneHinweis() =>
        Verbindungspruefung.Ursache(Postgres(PostgresErrorCodes.InvalidPassword),
                new NpgsqlConnectionStringBuilder(_ziel.ConnectionString) { Password = "geheim" }, pgPassword: "alt")
            .Should().Be("Anmeldung als „helfer“ abgelehnt: Kennwort falsch oder fehlt.");

    [Fact]
    public void Datenbank_FehltUndDarfNichtAngelegtWerden() =>
        Verbindungspruefung.Ursache(Postgres(PostgresErrorCodes.InvalidCatalogName), _ziel)
            .Should().Be("Die Datenbank „turniere“ gibt es nicht, und „helfer“ darf keine anlegen.");

    [Fact]
    public void FehlendeRechte_NenntDatenbank() =>
        Verbindungspruefung.Ursache(Postgres(PostgresErrorCodes.InsufficientPrivilege), _ziel)
            .Should().Be("„helfer“ fehlen Rechte an der Datenbank „turniere“ (anlegen oder ändern).");

    [Theory]
    [InlineData(PostgresErrorCodes.InvalidPassword)]
    [InlineData(PostgresErrorCodes.InvalidCatalogName)]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege)]
    public void VerbindungsfehlerFinden_ZugangsfehlerAuchAlsInnereAusnahme(string sqlState)
    {
        var innen = Postgres(sqlState);

        Verbindungspruefung.VerbindungsfehlerFinden(new InvalidOperationException("außen", innen)).Should().BeSameAs(innen);
    }

    [Fact]
    public void VerbindungsfehlerFinden_AndererServerfehler_IstKeinVerbindungsfehler() =>
        Verbindungspruefung.VerbindungsfehlerFinden(Postgres(PostgresErrorCodes.SyntaxError)).Should().BeNull();

    [Fact]
    public void Dienst_NichtErreichbar() =>
        Verbindungspruefung.Ursache(
                new NpgsqlException("Failed to connect", new SocketException((int)SocketError.ConnectionRefused)), _ziel)
            .Should().Be("Keine Verbindung zu server1:5433 – läuft der PostgreSQL-Dienst?");

    [Fact]
    public void Zeitueberschreitung_GiltAlsNichtErreichbar() =>
        Verbindungspruefung.Ursache(new NpgsqlException("Timeout", new TimeoutException()), _ziel)
            .Should().StartWith("Keine Verbindung zu server1:5433");

    [Fact]
    public void AndererServerfehler_ZeigtDessenText() =>
        Verbindungspruefung.Ursache(Postgres(PostgresErrorCodes.TooManyConnections), _ziel)
            .Should().Be("Meldung des Servers");
}
