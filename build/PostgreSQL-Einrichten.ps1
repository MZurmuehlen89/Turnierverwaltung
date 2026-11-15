<#
.SYNOPSIS
    Richtet PostgreSQL für die Turnierverwaltung ein (#49): Anmeldung „turnierverwaltung“ und Datenbank.

.DESCRIPTION
    Einmalig nach der Installation von PostgreSQL ausführen, für jede Neuinstallation gleich. Wiederholbar: Was schon
    da ist, bleibt unverändert.

    - Legt die Anmeldung „turnierverwaltung“ an (kein Superuser; darf Datenbanken anlegen, weil die GUI-Tests je Lauf
      eine eigene anlegen und löschen) mit einem zufälligen Kennwort.
    - Legt die Datenbank „turnierverwaltung“ mit deutscher Sortierung (ICU de-DE) an, Besitzer ist diese Anmeldung.
      Das Schema legt die App beim ersten Start per Migration an.
    - Trägt das Kennwort in die Kennwortdatei von PostgreSQL für den aktuellen Windows-Benutzer ein
      (%APPDATA%\postgresql\pgpass.conf). Die App und die Werkzeuge (dotnet ef) finden es dort, solange in der App
      kein Kennwort eingetragen ist – es muss also nirgends abgetippt werden.

    Die Verbindung als Administrator „postgres“ braucht dessen Kennwort; psql fragt danach, falls es nicht schon in
    der Kennwortdatei steht.
#>
param(
    [string]$Server = 'localhost',
    [int]$Port = 5432,
    [string]$Datenbank = 'turnierverwaltung',
    [string]$Anmeldung = 'turnierverwaltung',
    [string]$Administrator = 'postgres'
)

$ErrorActionPreference = 'Stop'

$psql = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
    Sort-Object { [int]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (-not $psql) { throw 'psql.exe nicht gefunden – ist PostgreSQL unter „C:\Program Files\PostgreSQL“ installiert?' }

function Abfrage([string]$sql, [string]$db = 'postgres') {
    $ergebnis = & $psql.FullName -h $Server -p $Port -U $Administrator -d $db -v ON_ERROR_STOP=1 -tAc $sql
    if ($LASTEXITCODE -ne 0) { throw "psql meldet Fehler (Exitcode $LASTEXITCODE) bei: $sql" }
    return $ergebnis
}

# ── Anmeldung ────────────────────────────────────────
$pgpass = Join-Path $env:APPDATA 'postgresql\pgpass.conf'
$schluessel = "${Server}:${Port}:*:${Anmeldung}:"
if ((Abfrage "SELECT 1 FROM pg_roles WHERE rolname = '$Anmeldung'") -ne '1') {
    $kennwort = -join ((1..32) | ForEach-Object { [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789' | Get-Random })
    Abfrage "CREATE ROLE $Anmeldung LOGIN CREATEDB PASSWORD '$kennwort'" | Out-Null

    New-Item -ItemType Directory -Force (Split-Path $pgpass) | Out-Null
    $zeilen = @(if (Test-Path $pgpass) { Get-Content $pgpass | Where-Object { -not $_.StartsWith($schluessel) } })
    Set-Content $pgpass -Value ($zeilen + "$schluessel$kennwort") -Encoding ascii
    Write-Host "Anmeldung „$Anmeldung“ angelegt, Kennwort in $pgpass eingetragen."
} else {
    Write-Host "Anmeldung „$Anmeldung“ besteht bereits – unverändert."
    if (-not ((Test-Path $pgpass) -and (Get-Content $pgpass | Where-Object { $_.StartsWith($schluessel) }))) {
        Write-Warning "In $pgpass steht kein Kennwort für „$Anmeldung“. Die App fragt dann beim Start danach."
    }
}

# ── Datenbank ────────────────────────────────────────
if ((Abfrage "SELECT 1 FROM pg_database WHERE datname = '$Datenbank'") -ne '1') {
    Abfrage "CREATE DATABASE $Datenbank OWNER $Anmeldung TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER icu ICU_LOCALE 'de-DE' LOCALE 'C'" | Out-Null
    Write-Host "Datenbank „$Datenbank“ angelegt (ICU de-DE). Das Schema legt die App beim ersten Start an."
} else {
    Write-Host "Datenbank „$Datenbank“ besteht bereits – unverändert."
}
