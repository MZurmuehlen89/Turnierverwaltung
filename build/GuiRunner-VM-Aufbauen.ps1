#Requires -Version 7
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Baut die Hyper-V-VM mit dem self-hosted GitHub-Runner für die GUI-Tests komplett unbeaufsichtigt (#609).

.DESCRIPTION
    Die GUI-Tests übernehmen Maus und Tastatur, deshalb laufen sie nicht auf dem Desktop des Entwicklungsrechners,
    sondern in einer eigenen VM (Windows Server 2025 Evaluation, Desktop Experience – dasselbe System wie der
    GitHub-Runner windows-2025, 180 Tage ohne Schlüssel lauffähig). Ablauf:

    1. Das Windows-Abbild aus der ISO direkt auf eine neue VHDX aufspielen (DISM + bcdboot) und die Antwortdatei
       dazulegen. Kein Start von der ISO: Eine Gen2-VM wartet dort auf „Press any key to boot from CD“, das ginge
       nur mit einer umgebauten ISO (ADK/oscdimg) unbeaufsichtigt.
    2. VM anlegen (Gen2, feste Auflösung 1920×1080) und starten; Windows richtet sich mit der Antwortdatei ein.
    3. Per PowerShell Direct (kein Netzwerkzugriff vom Host nötig) GuiRunner-VM\Gast-Einrichten.ps1 ausführen:
       Werkzeuge, Runner mit frischem Registrierungstoken (gilt nur 1 h, deshalb erst hier geholt), automatische
       Anmeldung, Runner-Autostart in der Konsolensitzung.
    4. VM herunterfahren, Snapshot „Grundzustand“ anlegen, wieder starten und warten, bis der Runner bei GitHub
       online ist.

    Das Administrator-Passwort der VM wird zufällig erzeugt und DPAPI-verschlüsselt als zugang.xml in der Ablage
    gespeichert (nur dieses Windows-Konto kann es lesen):
        (Import-Clixml C:\Hyper-V\Turnierverwaltung-GuiRunner\zugang.xml).GetNetworkCredential().Password

    Voraussetzungen: Hyper-V aktiv, PowerShell 7 als Administrator, gh angemeldet mit Admin-Recht auf das Repo.
    Das Protokoll jedes Laufs liegt unter <Ablage>\protokolle\.

.PARAMETER IsoPfad
    ISO von Windows Server 2025 Evaluation. Standard: die erste *SERVER_EVAL*.iso im Download-Ordner.

.PARAMETER Neuaufbau
    Eine vorhandene VM samt Platte und Snapshots löschen und den Runner bei GitHub abmelden, dann neu aufbauen
    (z. B. nach Ablauf der 180 Tage).

.EXAMPLE
    pwsh build/GuiRunner-VM-Aufbauen.ps1
.EXAMPLE
    pwsh build/GuiRunner-VM-Aufbauen.ps1 -Neuaufbau
#>
param(
    [string]$IsoPfad,
    [string]$VmName = 'Turnierverwaltung-GuiRunner',
    [string]$Ablage = 'C:\Hyper-V\Turnierverwaltung-GuiRunner',
    [string]$Switch = 'Default Switch',
    [int]$Kerne = 4,
    [int]$ArbeitsspeicherGB = 8,
    [int]$PlatteGB = 100,
    [switch]$Neuaufbau
)

$ErrorActionPreference = 'Stop'
$computerName = 'GUIRUNNER'   # höchstens 15 Zeichen (NetBIOS)
$skriptOrdner = Join-Path $PSScriptRoot 'GuiRunner-VM'

# ── Hilfsfunktionen ────────────────────────

function Schritt([string]$text) {
    Write-Host "[$(Get-Date -Format HH:mm:ss)] $text" -ForegroundColor Cyan
}

function Gh([string[]]$argumente) {
    $ausgabe = & gh @argumente
    if ($LASTEXITCODE -ne 0) { throw "gh $($argumente -join ' ') fehlgeschlagen (Exitcode $LASTEXITCODE)." }
    $ausgabe
}

function FreieLaufwerksbuchstaben([int]$anzahl) {
    $belegt = (Get-PSDrive -PSProvider FileSystem).Name
    $frei = [char[]]('G'..'Z') | Where-Object { "$_" -notin $belegt } | Select-Object -First $anzahl
    if ($frei.Count -lt $anzahl) { throw 'Nicht genug freie Laufwerksbuchstaben zum Einhängen der VHDX.' }
    $frei
}

# ── Vorbereitung ────────────────────────

function VoraussetzungenPruefen {
    Schritt 'Prüfe Voraussetzungen ...'
    if ((Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All).State -ne 'Enabled') {
        throw 'Hyper-V ist nicht aktiv (Windows-Features → Hyper-V, danach Neustart).'
    }
    if (-not (Get-VMSwitch -Name $Switch -ErrorAction SilentlyContinue)) {
        throw "Virtueller Switch '$Switch' fehlt."
    }
    if (-not (Test-Path $IsoPfad)) { throw "ISO nicht gefunden: $IsoPfad" }
    Gh @('auth', 'status') | Out-Null
    if ((Get-VM -Name $VmName -ErrorAction SilentlyContinue) -and -not $Neuaufbau) {
        throw "VM '$VmName' gibt es schon. Zum Neuaufbau -Neuaufbau angeben."
    }
}

function StandardIsoFinden {
    $iso = Get-ChildItem (Join-Path $env:USERPROFILE 'Downloads') -Filter '*SERVER_EVAL*.iso' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $iso) { throw 'Keine *SERVER_EVAL*.iso im Download-Ordner – Pfad mit -IsoPfad angeben.' }
    $iso.FullName
}

function RunnerAbmelden([string]$repo) {
    $id = Gh @('api', "repos/$repo/actions/runners", '--jq', ".runners[] | select(.name == `"$VmName`") | .id")
    if ($id) {
        Schritt "Melde alten Runner $VmName (Id $id) bei GitHub ab ..."
        Gh @('api', '-X', 'DELETE', "repos/$repo/actions/runners/$id") | Out-Null
    }
}

function AlteVmEntfernen([string]$repo) {
    $vm = Get-VM -Name $VmName -ErrorAction SilentlyContinue
    if ($vm) {
        Schritt "Entferne vorhandene VM $VmName ..."
        if ($vm.State -ne 'Off') { Stop-VM -VM $vm -TurnOff -Force }
        $platten = (Get-VMHardDiskDrive -VM $vm).Path
        Remove-VM -VM $vm -Force   # löscht auch die Snapshots
        $platten | Where-Object { Test-Path $_ } | Remove-Item -Force
    }
    RunnerAbmelden $repo
}

function ZugangErzeugen {
    # Nur Zeichen, die in XML und auf der Kommandozeile unkritisch sind; Präfix erfüllt die Komplexitätsregel
    $zeichen = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $zufall = -join (1..24 | ForEach-Object { $zeichen[[Security.Cryptography.RandomNumberGenerator]::GetInt32($zeichen.Length)] })
    $passwort = "Gr1-$zufall"
    $zugang = [pscredential]::new('Administrator', (ConvertTo-SecureString $passwort -AsPlainText -Force))
    $zugang | Export-Clixml (Join-Path $Ablage 'zugang.xml')
    $zugang
}

# ── Platte ────────────────────────

function AbbildIndexFinden([string]$wim) {
    # Datacenter mit Desktop Experience (Installation „Server“ statt „Server Core“) – wie der Runner windows-2025
    $anzahl = (& dism.exe /English /Get-WimInfo "/WimFile:$wim" | Select-String '^Index : ').Count
    foreach ($index in 1..$anzahl) {
        $info = & dism.exe /English /Get-WimInfo "/WimFile:$wim" "/Index:$index"
        $edition = ($info | Select-String '^Edition : (.+)$').Matches.Groups[1].Value
        $installation = ($info | Select-String '^Installation : (.+)$').Matches.Groups[1].Value
        if ($edition -like '*Datacenter*' -and $installation -eq 'Server') { return $index }
    }
    throw "Kein Abbild 'Datacenter, Desktop Experience' in $wim gefunden."
}

function PlattePartitionieren([int]$datentraeger, [char]$systemBuchstabe, [char]$windowsBuchstabe) {
    $befehle = @"
select disk $datentraeger
clean
convert gpt
create partition efi size=260
format quick fs=fat32 label=System
assign letter=$systemBuchstabe
create partition msr size=16
create partition primary
format quick fs=ntfs label=Windows
assign letter=$windowsBuchstabe
"@
    $befehle | diskpart.exe | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "diskpart fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

function AntwortdateiSchreiben([string]$windowsLaufwerk, [pscredential]$zugang) {
    $inhalt = (Get-Content (Join-Path $skriptOrdner 'unattend.xml') -Raw).
        Replace('{{COMPUTERNAME}}', $computerName).
        Replace('{{PASSWORT}}', $zugang.GetNetworkCredential().Password)
    $panther = Join-Path $windowsLaufwerk 'Windows\Panther'
    New-Item -ItemType Directory -Path $panther -Force | Out-Null
    Set-Content -Path (Join-Path $panther 'unattend.xml') -Value $inhalt -Encoding utf8
}

function PlatteAufbauen([string]$vhdx, [pscredential]$zugang) {
    Schritt "Lege VHDX an ($PlatteGB GB dynamisch) ..."
    New-VHD -Path $vhdx -SizeBytes ($PlatteGB * 1GB) -Dynamic | Out-Null
    $iso = Mount-DiskImage -ImagePath $IsoPfad -PassThru
    try {
        $disk = Mount-VHD -Path $vhdx -NoDriveLetter -Passthru | Get-Disk
        try {
            # Schutz vor „clean“ auf der falschen Platte: nur die eben eingehängte Datei-VHDX
            if ((Get-VHD -Path $vhdx).DiskNumber -ne $disk.Number) { throw "Datenträger $($disk.Number) ist nicht $vhdx." }
            $system, $windows = FreieLaufwerksbuchstaben 2
            PlattePartitionieren $disk.Number $system $windows

            $isoLaufwerk = "$(($iso | Get-Volume).DriveLetter):"
            $wim = @("$isoLaufwerk\sources\install.wim", "$isoLaufwerk\sources\install.esd") | Where-Object { Test-Path $_ } | Select-Object -First 1
            $index = AbbildIndexFinden $wim
            Schritt "Spiele Abbild $index aus $wim auf (dauert einige Minuten) ..."
            & dism.exe /English /Apply-Image "/ImageFile:$wim" "/Index:$index" "/ApplyDir:${windows}:\" | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "DISM Apply-Image fehlgeschlagen (Exitcode $LASTEXITCODE)." }
            & bcdboot.exe "${windows}:\Windows" /s "${system}:" /f UEFI | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "bcdboot fehlgeschlagen (Exitcode $LASTEXITCODE)." }
            AntwortdateiSchreiben "${windows}:\" $zugang
        }
        finally { Dismount-VHD -Path $vhdx }
    }
    finally { Dismount-DiskImage -ImagePath $IsoPfad | Out-Null }
}

# ── VM ────────────────────────

function VmAnlegen([string]$vhdx) {
    Schritt "Lege VM $VmName an ($Kerne Kerne, $ArbeitsspeicherGB GB) ..."
    $vm = New-VM -Name $VmName -Generation 2 -Path $Ablage -VHDPath $vhdx -SwitchName $Switch `
        -MemoryStartupBytes ($ArbeitsspeicherGB * 1GB)
    Set-VMProcessor -VM $vm -Count $Kerne
    Set-VMMemory -VM $vm -DynamicMemoryEnabled $false
    # Feste Auflösung: virtualisierte Listen und Layout hängen von der Fenstergröße ab
    Set-VMVideo -VM $vm -HorizontalResolution 1920 -VerticalResolution 1080 -ResolutionType Single
    # Mit dem Rechner starten und sauber herunterfahren; automatische Snapshots würden den Grundzustand verwässern
    Set-VM -VM $vm -AutomaticStartAction Start -AutomaticStopAction ShutDown -AutomaticCheckpointsEnabled $false
    $vm
}

function AufPowerShellDirectWarten([pscredential]$zugang) {
    Schritt 'Warte, bis Windows eingerichtet ist (PowerShell Direct erreichbar, ~5–15 min) ...'
    $bis = (Get-Date).AddMinutes(30)
    while ($true) {
        try {
            Invoke-Command -VMName $VmName -Credential $zugang -ScriptBlock { $env:COMPUTERNAME } -ErrorAction Stop | Out-Null
            return
        }
        catch {
            if ((Get-Date) -gt $bis) { throw "VM nach 30 min nicht per PowerShell Direct erreichbar: $($_.Exception.Message)" }
            Start-Sleep -Seconds 20
        }
    }
}

function GastEinrichten([pscredential]$zugang, [string]$repo) {
    Schritt 'Hole Registrierungstoken für den Runner ...'
    $token = Gh @('api', '-X', 'POST', "repos/$repo/actions/runners/registration-token", '--jq', '.token')
    Schritt 'Richte die VM ein (Werkzeuge, Runner, Anmeldung; ~10–20 min) ...'
    Invoke-Command -VMName $VmName -Credential $zugang -FilePath (Join-Path $skriptOrdner 'Gast-Einrichten.ps1') `
        -ArgumentList $zugang.GetNetworkCredential().Password, "https://github.com/$repo", $token, $VmName
}

function GrundzustandSichern {
    Schritt 'Fahre die VM herunter und lege Snapshot „Grundzustand“ an ...'
    Stop-VM -Name $VmName -Force
    Checkpoint-VM -Name $VmName -SnapshotName 'Grundzustand'
    Start-VM -Name $VmName
}

function AufRunnerWarten([string]$repo) {
    Schritt 'Warte, bis der Runner bei GitHub online ist ...'
    $bis = (Get-Date).AddMinutes(10)
    while ($true) {
        $status = Gh @('api', "repos/$repo/actions/runners", '--jq', ".runners[] | select(.name == `"$VmName`") | .status")
        if ($status -eq 'online') { return }
        if ((Get-Date) -gt $bis) { throw "Runner $VmName nach 10 min nicht online (Status: '$status')." }
        Start-Sleep -Seconds 15
    }
}

# ── Ablauf ────────────────────────

if (-not $IsoPfad) { $IsoPfad = StandardIsoFinden }
New-Item -ItemType Directory -Path (Join-Path $Ablage 'protokolle') -Force | Out-Null
Start-Transcript -Path (Join-Path $Ablage "protokolle\aufbau-$(Get-Date -Format yyyyMMdd-HHmmss).log") | Out-Null
Push-Location (Split-Path $PSScriptRoot -Parent)   # gh erkennt das Repo am Arbeitsordner; eine Admin-Konsole startet in System32
try {
    $start = Get-Date
    $repo = Gh @('repo', 'view', '--json', 'nameWithOwner', '-q', '.nameWithOwner')
    VoraussetzungenPruefen
    if ($Neuaufbau) { AlteVmEntfernen $repo }
    $zugang = ZugangErzeugen
    $vhdx = Join-Path $Ablage "$VmName.vhdx"
    PlatteAufbauen $vhdx $zugang
    VmAnlegen $vhdx | Start-VM
    AufPowerShellDirectWarten $zugang
    GastEinrichten $zugang $repo
    GrundzustandSichern
    AufRunnerWarten $repo
    Schritt "Fertig nach $([int]((Get-Date) - $start).TotalMinutes) min: Runner $VmName ist online."
}
finally {
    Pop-Location
    Stop-Transcript | Out-Null
}
