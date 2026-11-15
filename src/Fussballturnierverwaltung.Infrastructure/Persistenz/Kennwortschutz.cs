using System.Security.Cryptography;
using System.Text;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

/// <summary>Verschlüsselt das Datenbank-Kennwort für die Einstellungsdatei (#49) mit DPAPI für den angemeldeten
/// Windows-Benutzer: Kein eigener Schlüssel nötig, ein anderer Benutzer oder Rechner kann es nicht lesen.
/// Linux kommt erst mit dem Blazor-Host und braucht dort voraussichtlich gar kein Kennwort (Peer-Anmeldung).</summary>
public static class Kennwortschutz
{
    // Fester Zusatz, damit eine andere Anwendung desselben Benutzers den Wert nicht ohne Weiteres entschlüsselt.
    private static readonly byte[] _zusatz = "Turnierverwaltung.Datenbank"u8.ToArray();

    public static string Verschluesseln(string kennwort)
    {
        if (!OperatingSystem.IsWindows()) throw NurWindows();
        return Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(kennwort), _zusatz, DataProtectionScope.CurrentUser));
    }

    public static string Entschluesseln(string verschluesselt)
    {
        if (!OperatingSystem.IsWindows()) throw NurWindows();
        return Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(verschluesselt), _zusatz, DataProtectionScope.CurrentUser));
    }

    private static PlatformNotSupportedException NurWindows() => new(
        "Das gespeicherte Datenbank-Kennwort ist nur unter Windows verschlüsselt. Unter Linux das Kennwort leer lassen "
        + "(Kennwortdatei oder Peer-Anmeldung).");
}
