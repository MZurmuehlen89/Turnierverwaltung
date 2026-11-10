<#
.SYNOPSIS
    Richtet die GUI-Runner-VM ein (#609). Läuft IN der VM: GuiRunner-VM-Aufbauen.ps1 legt das Skript mit
    parameter.json nach C:\Einrichtung, die Antwortdatei startet es bei der ersten automatischen Anmeldung als
    eingebauter Administrator.

.DESCRIPTION
    Windows PowerShell 5.1 (frisches System) – keine Sprachmittel aus PowerShell 7.
    Registriert zuerst den GitHub-Actions-Runner (das Token gilt nur 1 h), installiert dann .NET 10 SDK, SQL Server
    LocalDB, Git und PowerShell 7, schaltet Bildschirmsperre, Energiesparen und automatische Updates ab und lässt den
    Runner bei der automatischen Anmeldung interaktiv starten: FlaUI braucht den Desktop der Konsolensitzung, ein
    Dienst hätte keinen. Am Ende Neustart – danach läuft der Runner.

    Jeder Schritt geht zusätzlich auf COM1: VirtualBox schreibt die serielle Schnittstelle in eine Datei auf dem
    Host, das Aufbauskript verfolgt sie (ohne Gasterweiterungen der einzige Rückkanal). Eine Zeile mit
    „Gast: FEHLER“ bricht den Aufbau auf dem Host ab. Vollständiges Protokoll in der VM: C:\GuiRunner-Einrichtung.log
#>

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest ist in 5.1 mit Fortschrittsanzeige um ein Vielfaches langsamer
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$einrichtung = 'C:\Einrichtung'
$runnerOrdner = 'C:\actions-runner'
$dotnetOrdner = 'C:\Program Files\dotnet'   # Standardziel von actions/setup-dotnet unter Windows

# ── Hilfsfunktionen ────────────────────────

function Schritt([string]$text) {
    $zeile = "[$(Get-Date -Format HH:mm:ss)] Gast: $text"
    Write-Host $zeile
    try {
        $port = New-Object System.IO.Ports.SerialPort 'COM1', 115200
        $port.Open()
        try { $port.WriteLine($zeile) } finally { $port.Close() }
    } catch { Write-Host "COM1 nicht beschreibbar: $($_.Exception.Message)" }
}

function Herunterladen([string]$url, [string]$dateiname) {
    $ziel = Join-Path $einrichtung $dateiname
    Invoke-WebRequest -Uri $url -OutFile $ziel -UseBasicParsing
    $ziel
}

function GitHubReleaseAsset([string]$repo, [string]$muster) {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -UseBasicParsing
    $asset = $release.assets | Where-Object name -like $muster | Select-Object -First 1
    if (-not $asset) { throw "Kein Release-Asset '$muster' in $repo ($($release.tag_name))." }
    $asset
}

function Ausfuehren([string]$datei, [string]$argumente) {
    $prozess = Start-Process -FilePath $datei -ArgumentList $argumente -Wait -PassThru
    if ($prozess.ExitCode -notin 0, 3010) { throw "$datei $argumente endete mit Exitcode $($prozess.ExitCode)." }
}

function PfadErgaenzen([string]$ordner) {
    $pfad = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    if (($pfad -split ';') -notcontains $ordner) {
        [Environment]::SetEnvironmentVariable('Path', "$pfad;$ordner", 'Machine')
    }
    $env:Path = "$env:Path;$ordner"
}

function Registry([string]$schluessel, [string]$name, $wert, [string]$typ = 'DWord') {
    if (-not (Test-Path $schluessel)) { New-Item -Path $schluessel -Force | Out-Null }
    New-ItemProperty -Path $schluessel -Name $name -Value $wert -PropertyType $typ -Force | Out-Null
}

# ── Netz ────────────────────────

function AufNetzWarten {
    Schritt 'Warte auf Internetverbindung ...'
    $bis = (Get-Date).AddMinutes(10)
    while ($true) {
        try { Invoke-WebRequest -Uri 'https://api.github.com' -UseBasicParsing -TimeoutSec 10 | Out-Null; return }
        catch {
            if ((Get-Date) -gt $bis) { throw "Kein Internet in der VM: $($_.Exception.Message)" }
            Start-Sleep -Seconds 10
        }
    }
}

# ── Werkzeuge ────────────────────────

function PowerShell7Installieren {
    Schritt 'PowerShell 7 ...'
    $asset = GitHubReleaseAsset 'PowerShell/PowerShell' 'PowerShell-*-win-x64.msi'
    $msi = Herunterladen $asset.browser_download_url $asset.name
    Ausfuehren 'msiexec.exe' "/i `"$msi`" /quiet /norestart ADD_PATH=1"
}

function GitInstallieren {
    Schritt 'Git ...'
    $asset = GitHubReleaseAsset 'git-for-windows/git' 'Git-*-64-bit.exe'
    $setup = Herunterladen $asset.browser_download_url $asset.name
    Ausfuehren $setup '/VERYSILENT /NORESTART /NOCANCEL /SP- /SUPPRESSMSGBOXES'
    # Belegnamen der GUI-Tests werden lang (MAX_PATH, siehe #569/#560)
    & 'C:\Program Files\Git\cmd\git.exe' config --system core.longpaths true
}

function DotnetInstallieren {
    Schritt '.NET 10 SDK ...'
    $skript = Herunterladen 'https://dot.net/v1/dotnet-install.ps1' 'dotnet-install.ps1'
    & $skript -Channel 10.0 -InstallDir $dotnetOrdner -NoPath
    PfadErgaenzen $dotnetOrdner
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dotnetOrdner, 'Machine')
}

function LocalDbInstallieren {
    Schritt 'SQL Server LocalDB ...'
    # Der Express-Installer lädt das LocalDB-MSI nur herunter (/ACTION=Download), installiert wird es danach direkt
    $ssei = Herunterladen 'https://go.microsoft.com/fwlink/p/?linkid=2216019' 'SQL2022-SSEI-Expr.exe'
    $medien = Join-Path $einrichtung 'localdb'
    Ausfuehren $ssei "/ACTION=Download /MEDIAPATH=`"$medien`" /MEDIATYPE=LocalDB /QUIET /LANGUAGE=en-US"
    $msi = Get-ChildItem $medien -Filter 'SqlLocalDB.msi' -Recurse | Select-Object -First 1
    if (-not $msi) { throw "SqlLocalDB.msi wurde nicht nach $medien geladen." }
    Ausfuehren 'msiexec.exe' "/i `"$($msi.FullName)`" /qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES"
}

# ── Runner ────────────────────────

function RunnerEinrichten {
    Schritt 'GitHub-Actions-Runner ...'
    $asset = GitHubReleaseAsset 'actions/runner' 'actions-runner-win-x64-*.zip'
    $zip = Herunterladen $asset.browser_download_url $asset.name
    if (Test-Path $runnerOrdner) { Remove-Item $runnerOrdner -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $runnerOrdner
    # Standard-Labels self-hosted, Windows, X64 kommen von selbst dazu; gui-tests.yml verlangt zusätzlich „gui“
    & (Join-Path $runnerOrdner 'config.cmd') --unattended --url $parameter.RepoUrl --token $parameter.RunnerToken `
        --name $parameter.RunnerName --labels gui --work _work --replace
    if ($LASTEXITCODE -ne 0) { throw "Runner-Registrierung fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

function RunnerAutostartEinrichten {
    Schritt 'Runner-Autostart bei der Anmeldung ...'
    $benutzer = "$env:COMPUTERNAME\Administrator"
    $aktion = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c `"$runnerOrdner\run.cmd`"" -WorkingDirectory $runnerOrdner
    $ausloeser = New-ScheduledTaskTrigger -AtLogOn -User $benutzer
    # Interactive = läuft in der angemeldeten Konsolensitzung, dort sehen die Tests den Desktop
    $principal = New-ScheduledTaskPrincipal -UserId $benutzer -LogonType Interactive -RunLevel Highest
    $einstellungen = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName 'GitHub-Actions-Runner' -Action $aktion -Trigger $ausloeser `
        -Principal $principal -Settings $einstellungen -Force | Out-Null
}

# ── Sitzung und System ────────────────────────

function AutomatischeAnmeldungEinrichten {
    Schritt 'Automatische Anmeldung ...'
    $winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
    Registry $winlogon 'AutoAdminLogon' '1' 'String'
    Registry $winlogon 'DefaultUserName' 'Administrator' 'String'
    Registry $winlogon 'DefaultDomainName' $env:COMPUTERNAME 'String'
    Registry $winlogon 'DefaultPassword' $parameter.Passwort 'String'
    Remove-ItemProperty -Path $winlogon -Name 'AutoLogonCount' -ErrorAction SilentlyContinue
    # Kein Strg+Alt+Entf und keine Rechtshinweis-Seite vor der Anmeldung
    Registry 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'DisableCAD' 1
}

function SperreUndEnergiesparenAbschalten {
    Schritt 'Bildschirmsperre und Energiesparen aus ...'
    powercfg /change monitor-timeout-ac 0
    powercfg /change standby-timeout-ac 0
    powercfg /hibernate off
    powercfg /setacvalueindex SCHEME_CURRENT SUB_NONE CONSOLELOCK 0
    powercfg /setactive SCHEME_CURRENT
    Registry 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization' 'NoLockScreen' 1
    Registry 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'InactivityTimeoutSecs' 0
    Registry 'HKCU:\Control Panel\Desktop' 'ScreenSaveActive' '0' 'String'
    Registry 'HKCU:\Software\Policies\Microsoft\Windows\Control Panel\Desktop' 'ScreenSaveActive' '0' 'String'
}

function SystemFuerTestsEinstellen {
    Schritt 'Updates, lange Pfade, Defender-Ausnahmen ...'
    # Wegwerf-VM: ein Neustart durch Windows Update mitten im Lauf wäre schlimmer als fehlende Updates
    Registry 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' 'NoAutoUpdate' 1
    Registry 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' 'LongPathsEnabled' 1
    Registry 'HKLM:\SOFTWARE\Microsoft\ServerManager' 'DoNotOpenServerManagerAtLogon' 1
    Registry 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Reliability' 'ShutdownReasonOn' 0
    try {
        Add-MpPreference -ExclusionPath $runnerOrdner, $dotnetOrdner, "$env:USERPROFILE\.nuget"
    } catch { Schritt "Defender-Ausnahmen nicht gesetzt: $($_.Exception.Message)" }
}

function Aufraeumen {
    Schritt 'Aufräumen ...'
    # Antwortdatei und parameter.json enthalten das Passwort im Klartext
    Remove-Item 'C:\Windows\Panther\unattend.xml' -Force -ErrorAction SilentlyContinue
    Remove-Item $einrichtung -Recurse -Force -ErrorAction SilentlyContinue
}

# ── Ablauf ────────────────────────

Start-Transcript -Path 'C:\GuiRunner-Einrichtung.log' -Append | Out-Null
try {
    Schritt 'Einrichtung beginnt.'
    $parameter = Get-Content (Join-Path $einrichtung 'parameter.json') -Raw | ConvertFrom-Json
    AufNetzWarten
    RunnerEinrichten
    PowerShell7Installieren
    GitInstallieren
    DotnetInstallieren
    LocalDbInstallieren
    RunnerAutostartEinrichten
    AutomatischeAnmeldungEinrichten
    SperreUndEnergiesparenAbschalten
    SystemFuerTestsEinstellen
    Aufraeumen
    Schritt 'Einrichtung fertig, Neustart.'
    Stop-Transcript | Out-Null
    Restart-Computer -Force
}
catch {
    Schritt "FEHLER: $($_.Exception.Message) ($($_.InvocationInfo.PositionMessage -replace '\s+', ' '))"
    Stop-Transcript | Out-Null
}
