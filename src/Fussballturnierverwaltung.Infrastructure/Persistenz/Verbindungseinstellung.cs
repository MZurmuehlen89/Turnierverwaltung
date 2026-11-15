using System.Security.Cryptography;
using Fussballturnierverwaltung.Application.Einstellungen;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

/// <summary>Woher die App ihre Datenbankverbindung nimmt (#49). Vorrang hat eine Vorgabe aus der Konfiguration
/// (<c>ConnectionStrings:Standard</c>, in der Praxis die Umgebungsvariable <c>ConnectionStrings__Standard</c> der
/// GUI-Tests mit ihrer eigenen Testdatenbank), sonst gilt die Einstellung aus <c>einstellungen.json</c>.</summary>
public class Verbindungseinstellung(EinstellungenService einstellungen, IConfiguration konfiguration)
{
    // Einmal je Programmlauf: Speichern auf der Seite startet die App neu (scheitert der Neustart, bleibt es bis dahin
    // bei der alten Verbindung), und so liest nicht jeder DbContext die Einstellungsdatei und entschlüsselt das Kennwort.
    private string? _verbindungszeichenfolge;

    private string? Vorgabe => konfiguration.GetConnectionString("Standard");

    /// <summary>Die Verbindung ist von außen vorgegeben und in der App nicht änderbar.</summary>
    public bool IstVorgegeben => !string.IsNullOrWhiteSpace(Vorgabe);

    public string Verbindungszeichenfolge() =>
        _verbindungszeichenfolge ??= IstVorgegeben ? Vorgabe! : Zusammensetzen(einstellungen.Laden().Datenbank);

    /// <summary>Die geltende Verbindung zur Anzeige – bei einer Vorgabe aus deren Werten, ohne Kennwort.</summary>
    public DatenbankVerbindung Anzeigen() =>
        IstVorgegeben ? AusZeichenfolge(Vorgabe!) : einstellungen.Laden().Datenbank;

    /// <summary>Speichert die Verbindung; ein leeres <paramref name="neuesKennwort"/> behält das gespeicherte.</summary>
    public void Speichern(DatenbankVerbindung verbindung, string? neuesKennwort)
    {
        var alle = einstellungen.Laden();
        alle.Datenbank = MitKennwort(verbindung, alle.Datenbank.KennwortVerschluesselt, neuesKennwort);
        einstellungen.Speichern(alle);
    }

    /// <summary>Ab jetzt die gespeicherte Verbindung verwenden – für das Startfenster, das ohne Neustart weiterläuft.</summary>
    public void GespeicherteUebernehmen() => _verbindungszeichenfolge = null;

    /// <summary>Zeichenfolge für eine noch nicht gespeicherte Eingabe, etwa zum Prüfen vor dem Speichern.</summary>
    public string ZeichenfolgeFuerEingabe(DatenbankVerbindung verbindung, string? neuesKennwort) =>
        Zusammensetzen(MitKennwort(verbindung, einstellungen.Laden().Datenbank.KennwortVerschluesselt, neuesKennwort));

    // ── Reine Umwandlungen ────────────────────────

    public static string Zusammensetzen(DatenbankVerbindung verbindung)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = verbindung.Server,
            Port = verbindung.Port,
            Database = verbindung.Datenbank,
            Username = verbindung.Benutzer
        };
        // Ohne Kennwort sucht Npgsql es in der Kennwortdatei von PostgreSQL (pgpass.conf).
        if (verbindung.KennwortVerschluesselt is { } verschluesselt && Lesbar(verschluesselt) is { } kennwort)
            builder.Password = kennwort;
        return builder.ConnectionString;
    }

    // Ein Kennwort, das dieser Windows-Benutzer nicht entschlüsseln kann (anderer Benutzer, kopierte Datei), zählt wie
    // keins: Die Anmeldung scheitert dann mit einer verständlichen Ursache, und das Startfenster nimmt ein neues an.
    private static string? Lesbar(string verschluesselt)
    {
        try
        {
            return Kennwortschutz.Entschluesseln(verschluesselt);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    public static DatenbankVerbindung AusZeichenfolge(string zeichenfolge)
    {
        var builder = new NpgsqlConnectionStringBuilder(zeichenfolge);
        return new DatenbankVerbindung
        {
            Server = builder.Host ?? "",
            Port = builder.Port,
            Datenbank = builder.Database ?? "",
            Benutzer = builder.Username ?? ""
        };
    }

    /// <summary>Bereinigte Eingabe mit Kennwort: ein leeres neues behält das bisher gespeicherte.</summary>
    public static DatenbankVerbindung MitKennwort(
        DatenbankVerbindung verbindung, string? bisherVerschluesselt, string? neuesKennwort) => new()
    {
        Server = verbindung.Server.Trim(),
        Port = verbindung.Port,
        Datenbank = verbindung.Datenbank.Trim(),
        Benutzer = verbindung.Benutzer.Trim(),
        KennwortVerschluesselt = string.IsNullOrEmpty(neuesKennwort)
            ? bisherVerschluesselt
            : Kennwortschutz.Verschluesseln(neuesKennwort)
    };
}
