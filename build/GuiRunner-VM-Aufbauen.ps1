#Requires -Version 7
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Baut die VirtualBox-VM mit dem self-hosted GitHub-Runner für die GUI-Tests komplett unbeaufsichtigt (#609).

.DESCRIPTION
    Die GUI-Tests übernehmen Maus und Tastatur, deshalb laufen sie nicht auf dem Desktop des Entwicklungsrechners,
    sondern in einer eigenen VM (Windows Server 2025 Evaluation, Desktop Experience – dasselbe System wie der
    GitHub-Runner windows-2025, 180 Tage ohne Schlüssel lauffähig).

    VirtualBox statt Hyper-V: Hyper-V startet jede VM unter einem virtuellen Konto, das das Recht „Anmelden als
    Dienst“ braucht – eine Gruppenrichtlinie vergibt es ohne diese Gruppe (New-VM → 0x80070569). VirtualBox
    läuft unter dem eigenen Konto. Weil die Speicherintegrität den Windows-Hypervisor laufen lässt, nutzt VirtualBox
    die „Windows Hypervisor Platform“ (Windows-Feature, muss aktiv sein).

    Ablauf:
    1. Das Windows-Abbild aus der ISO direkt auf eine neue VHD aufspielen (DISM + bcdboot), Antwortdatei,
       GuiRunner-VM\Gast-Einrichten.ps1 und eine Parameterdatei (Passwort, Runner-Token) dazulegen. Kein Start von
       der ISO: Eine EFI-VM wartet dort auf „Press any key to boot from CD“.
    2. VM anlegen (EFI, feste Auflösung 1920×1080, serielle Schnittstelle in eine Protokolldatei) und starten.
       Windows richtet sich mit der Antwortdatei ein, meldet sich automatisch an und startet das Gast-Skript.
       Das meldet jeden Schritt über die serielle Schnittstelle – der einzige Rückkanal ohne Gasterweiterungen.
    3. Warten, bis der Runner bei GitHub online ist (das Gast-Skript startet die VM am Ende neu).
    4. VM herunterfahren, Snapshot „Grundzustand“ anlegen, geplante Aufgabe für den Autostart einrichten und
       darüber starten. Die Aufgabe stellt bei jeder Anmeldung am Rechner den Grundzustand wieder her und startet
       die VM ohne Fenster – jeder Start beginnt sauber, ein hartes Beenden beim Abmelden schadet nicht.

    Das Administrator-Passwort der VM wird zufällig erzeugt und DPAPI-verschlüsselt als zugang.xml in der Ablage
    gespeichert (nur dieses Windows-Konto kann es lesen):
        (Import-Clixml "$env:USERPROFILE\VirtualBox VMs\Turnierverwaltung-GuiRunner\zugang.xml").GetNetworkCredential().Password

    Voraussetzungen: VirtualBox 7.1+, Windows-Feature „Windows Hypervisor Platform“, PowerShell 7 als
    Administrator, gh angemeldet mit Admin-Recht auf das Repo. Das Protokoll jedes Laufs liegt unter
    <Ablage>\protokolle\ (aufbau-*.log vom Host, gast-*.log aus der VM).

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
    [string]$VmOrdner = (Join-Path $env:USERPROFILE 'VirtualBox VMs'),
    [int]$Kerne = 4,
    [int]$ArbeitsspeicherGB = 8,
    [int]$PlatteGB = 100,
    [switch]$Neuaufbau
)

$ErrorActionPreference = 'Stop'
$computerName = 'GUIRUNNER'   # höchstens 15 Zeichen (NetBIOS)
$skriptOrdner = Join-Path $PSScriptRoot 'GuiRunner-VM'
$vboxManage = Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'
$ablage = Join-Path $VmOrdner $VmName           # VirtualBox legt die VM unter <VmOrdner>\<VmName> an
$vhd = Join-Path $ablage "$VmName.vhd"
$zeitstempel = Get-Date -Format yyyyMMdd-HHmmss
$gastProtokoll = Join-Path $ablage "protokolle\gast-$zeitstempel.log"
$snapshot = 'Grundzustand'
$autostartAufgabe = 'Turnierverwaltung GUI-Runner starten'

# ── Hilfsfunktionen ────────────────────────

function Schritt([string]$text) {
    Write-Host "[$(Get-Date -Format HH:mm:ss)] $text" -ForegroundColor Cyan
}

function GhAufruf([string[]]$argumente) {
    # Ausdrücklich gh.exe: Eine Funktion namens „Gh“ hätte sich mit „& gh“ selbst aufgerufen (Namen ohne
    # Groß-/Kleinschreibung) – erster Aufbau brach mit Überlauf der Aufruftiefe ab
    $ausgabe = & gh.exe @argumente
    if ($LASTEXITCODE -ne 0) { throw "gh $($argumente -join ' ') fehlgeschlagen (Exitcode $LASTEXITCODE)." }
    $ausgabe
}

function VBox([string[]]$argumente) {
    $ausgabe = & $vboxManage @argumente 2>&1
    if ($LASTEXITCODE -ne 0) { throw "VBoxManage $($argumente -join ' ') fehlgeschlagen (Exitcode $LASTEXITCODE): $ausgabe" }
    $ausgabe
}

function VmVorhanden {
    & $vboxManage showvminfo $VmName --machinereadable *> $null
    $LASTEXITCODE -eq 0
}

function VmZustand {
    $zeile = VBox @('showvminfo', $VmName, '--machinereadable') | Select-String '^VMState="(.+)"$'
    $zeile.Matches.Groups[1].Value
}

# ── Vorbereitung ────────────────────────

function VoraussetzungenPruefen {
    Schritt 'Prüfe Voraussetzungen ...'
    if (-not (Test-Path $vboxManage)) { throw "VirtualBox nicht gefunden ($vboxManage)." }
    if (-not (Test-Path $IsoPfad)) { throw "ISO nicht gefunden: $IsoPfad" }
    GhAufruf @('auth', 'status') | Out-Null
    if (((VmVorhanden) -or (Test-Path $vhd)) -and -not $Neuaufbau) {
        throw "VM '$VmName' oder ihre Platte gibt es schon. Zum Neuaufbau -Neuaufbau angeben."
    }
}

function StandardIsoFinden {
    $iso = Get-ChildItem (Join-Path $env:USERPROFILE 'Downloads') -Filter '*SERVER_EVAL*.iso' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $iso) { throw 'Keine *SERVER_EVAL*.iso im Download-Ordner – Pfad mit -IsoPfad angeben.' }
    $iso.FullName
}

function RunnerAbmelden([string]$repo) {
    $id = GhAufruf @('api', "repos/$repo/actions/runners", '--jq', ".runners[] | select(.name == `"$VmName`") | .id")
    if ($id) {
        Schritt "Melde alten Runner $VmName (Id $id) bei GitHub ab ..."
        GhAufruf @('api', '-X', 'DELETE', "repos/$repo/actions/runners/$id") | Out-Null
    }
}

function AlteVmEntfernen([string]$repo) {
    if (VmVorhanden) {
        Schritt "Entferne vorhandene VM $VmName ..."
        if ((VmZustand) -notin 'poweroff', 'aborted', 'saved') {
            VBox @('controlvm', $VmName, 'poweroff') | Out-Null
            Start-Sleep -Seconds 5   # die Sitzung gibt die Platte erst kurz nach dem Ausschalten frei
        }
        VBox @('unregistervm', $VmName, '--delete') | Out-Null   # löscht auch Platte und Snapshots
    }
    # Auch die Platte eines abgebrochenen Aufbaus, zu dem es noch keine VM gibt
    if (Test-Path $vhd) {
        Dismount-DiskImage -ImagePath $vhd -ErrorAction SilentlyContinue | Out-Null
        Remove-Item $vhd -Force
    }
    RunnerAbmelden $repo
}

function ZugangErzeugen {
    # Nur Zeichen, die in XML, JSON und auf der Kommandozeile unkritisch sind; Präfix erfüllt die Komplexitätsregel
    $zeichen = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $zufall = -join (1..24 | ForEach-Object { $zeichen[[Security.Cryptography.RandomNumberGenerator]::GetInt32($zeichen.Length)] })
    $passwort = "Gr1-$zufall"
    $zugang = [pscredential]::new('Administrator', (ConvertTo-SecureString $passwort -AsPlainText -Force))
    $zugang | Export-Clixml (Join-Path $ablage 'zugang.xml')
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

function Diskpart([string]$befehle) {
    $befehle | diskpart.exe | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "diskpart fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

function VhdAnlegen {
    # diskpart statt New-VHD: das Hyper-V-Modul fehlt ohne Hyper-V. Endung .vhd → Format VHD, das VirtualBox
    # lesen und schreiben kann
    Diskpart "create vdisk file=`"$vhd`" maximum=$($PlatteGB * 1024) type=expandable"
}

function PlattePartitionieren([int]$datentraeger) {
    Diskpart @"
select disk $datentraeger
clean
convert gpt
create partition efi size=260
format quick fs=fat32 label=System
create partition msr size=16
create partition primary
format quick fs=ntfs label=Windows
"@
}

function LaufwerksbuchstabeZuweisen([int]$datentraeger, [string]$partitionsTyp) {
    # Buchstaben durchprobieren statt vorab „freie“ ermitteln: Get-PSDrive sieht weder Reservierungen der
    # Mount-Verwaltung (HKLM\SYSTEM\MountedDevices) noch Laufwerke, die nur in der Admin-Sitzung verbunden sind –
    # der zweite Aufbau scheiterte an G:, das dort belegt war („Laufwerkbuchstabe nicht zur Zuweisung frei“)
    $partition = Get-Partition -DiskNumber $datentraeger | Where-Object GptType -eq $partitionsTyp
    $belegt = (Get-PSDrive -PSProvider FileSystem).Name
    foreach ($buchstabe in [char[]]('G'..'Z') | Where-Object { "$_" -notin $belegt }) {
        try {
            Add-PartitionAccessPath -InputObject $partition -AccessPath "${buchstabe}:\" -ErrorAction Stop
            return $buchstabe
        }
        catch { Write-Host "    ${buchstabe}: nicht zuweisbar ($($_.Exception.Message.Trim()))" }
    }
    throw "Kein Laufwerksbuchstabe für die Partition $partitionsTyp zuweisbar."
}

function AntwortdateiSchreiben([string]$windowsLaufwerk, [pscredential]$zugang) {
    $inhalt = (Get-Content (Join-Path $skriptOrdner 'unattend.xml') -Raw).
        Replace('{{COMPUTERNAME}}', $computerName).
        Replace('{{PASSWORT}}', $zugang.GetNetworkCredential().Password)
    $panther = Join-Path $windowsLaufwerk 'Windows\Panther'
    New-Item -ItemType Directory -Path $panther -Force | Out-Null
    Set-Content -Path (Join-Path $panther 'unattend.xml') -Value $inhalt -Encoding utf8
}

function EinrichtungAblegen([string]$windowsLaufwerk, [pscredential]$zugang, [string]$repo) {
    $einrichtung = Join-Path $windowsLaufwerk 'Einrichtung'
    New-Item -ItemType Directory -Path $einrichtung -Force | Out-Null
    # Mit BOM: Windows PowerShell 5.1 liest eine Datei ohne BOM als ANSI, aus „–“ würde dabei ein typografisches
    # Anführungszeichen, das der Parser als Stringende nimmt
    Get-Content (Join-Path $skriptOrdner 'Gast-Einrichten.ps1') -Raw -Encoding utf8 |
        Set-Content (Join-Path $einrichtung 'Gast-Einrichten.ps1') -Encoding utf8BOM -NoNewline
    # Das Registrierungstoken gilt nur 1 h – deshalb erst nach dem Aufspielen geholt; das Gast-Skript registriert
    # den Runner als Erstes. Es löscht die Datei am Ende wieder (Passwort im Klartext)
    Schritt 'Hole Registrierungstoken für den Runner ...'
    $token = GhAufruf @('api', '-X', 'POST', "repos/$repo/actions/runners/registration-token", '--jq', '.token')
    [ordered]@{
        Passwort    = $zugang.GetNetworkCredential().Password
        RepoUrl     = "https://github.com/$repo"
        RunnerToken = $token
        RunnerName  = $VmName
    } | ConvertTo-Json | Set-Content -Path (Join-Path $einrichtung 'parameter.json') -Encoding utf8
}

function PlatteAufbauen([pscredential]$zugang, [string]$repo) {
    Schritt "Lege VHD an ($PlatteGB GB dynamisch) ..."
    VhdAnlegen
    $iso = Mount-DiskImage -ImagePath $IsoPfad -PassThru
    try {
        $disk = Mount-DiskImage -ImagePath $vhd -NoDriveLetter -Access ReadWrite -PassThru | Get-Disk
        try {
            # Schutz vor „clean“ auf der falschen Platte: nur die eben eingehängte Datei-VHD
            if ($disk.BusType -ne 'File Backed Virtual' -or $disk.Location -ne $vhd) {
                throw "Datenträger $($disk.Number) ($($disk.BusType), $($disk.Location)) ist nicht $vhd."
            }
            PlattePartitionieren $disk.Number
            $system = LaufwerksbuchstabeZuweisen $disk.Number '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}'    # EFI
            $windows = LaufwerksbuchstabeZuweisen $disk.Number '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}'   # Basisdaten

            $isoLaufwerk = "$(($iso | Get-Volume).DriveLetter):"
            $wim = @("$isoLaufwerk\sources\install.wim", "$isoLaufwerk\sources\install.esd") | Where-Object { Test-Path $_ } | Select-Object -First 1
            $index = AbbildIndexFinden $wim
            Schritt "Spiele Abbild $index aus $wim auf (dauert einige Minuten) ..."
            & dism.exe /English /Apply-Image "/ImageFile:$wim" "/Index:$index" "/ApplyDir:${windows}:\" | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "DISM Apply-Image fehlgeschlagen (Exitcode $LASTEXITCODE)." }
            & bcdboot.exe "${windows}:\Windows" /s "${system}:" /f UEFI | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "bcdboot fehlgeschlagen (Exitcode $LASTEXITCODE)." }
            AntwortdateiSchreiben "${windows}:\" $zugang
            EinrichtungAblegen "${windows}:\" $zugang $repo
        }
        finally { Dismount-DiskImage -ImagePath $vhd | Out-Null }
    }
    finally { Dismount-DiskImage -ImagePath $IsoPfad | Out-Null }
}

# ── VM ────────────────────────

function VmAnlegen {
    Schritt "Lege VM $VmName an ($Kerne Kerne, $ArbeitsspeicherGB GB) ..."
    VBox @('createvm', '--name', $VmName, '--ostype', 'Windows2025_64', '--basefolder', $VmOrdner, '--register') | Out-Null
    VBox @('modifyvm', $VmName, '--firmware', 'efi', '--cpus', $Kerne, '--memory', ($ArbeitsspeicherGB * 1024),
        '--ioapic', 'on', '--graphicscontroller', 'vboxsvga', '--vram', '128', '--nic1', 'nat',
        '--audio-enabled', 'off', '--boot1', 'disk', '--boot2', 'none', '--boot3', 'none', '--boot4', 'none',
        '--uart1', '0x3F8', '4', '--uart-mode1', 'file', $gastProtokoll) | Out-Null
    # Feste Auflösung: virtualisierte Listen und Layout hängen von der Fenstergröße ab. Ohne Gasterweiterungen
    # übernimmt Windows die Auflösung der EFI-Grafik
    VBox @('setextradata', $VmName, 'VBoxInternal2/EfiGraphicsResolution', '1920x1080') | Out-Null
    VBox @('storagectl', $VmName, '--name', 'SATA', '--add', 'sata', '--controller', 'IntelAhci', '--portcount', '1') | Out-Null
    VBox @('storageattach', $VmName, '--storagectl', 'SATA', '--port', '0', '--device', '0', '--type', 'hdd', '--medium', $vhd) | Out-Null
}

function VmStarten {
    VBox @('startvm', $VmName, '--type', 'headless') | Out-Null
}

function NeueGastZeilen([ref]$gelesen) {
    if (-not (Test-Path $gastProtokoll)) { return @() }
    $zeilen = @(Get-Content $gastProtokoll -ErrorAction SilentlyContinue | Where-Object { $_.Trim() })
    $neu = $zeilen | Select-Object -Skip $gelesen.Value
    $gelesen.Value = $zeilen.Count
    $neu
}

function AufEinrichtungWarten([string]$repo) {
    Schritt 'Warte auf die Einrichtung in der VM (Windows-Setup, Werkzeuge, Runner; ~30–60 min) ...'
    $bis = (Get-Date).AddMinutes(90)
    $gelesen = 0
    while ($true) {
        foreach ($zeile in NeueGastZeilen ([ref]$gelesen)) {
            Write-Host "    $zeile"
            if ($zeile -cmatch 'Gast: FEHLER') { throw "Einrichtung in der VM gescheitert – siehe $gastProtokoll" }
        }
        if ((RunnerStatus $repo) -eq 'online') { return }
        if ((Get-Date) -gt $bis) { throw "Runner $VmName nach 90 min nicht online – siehe $gastProtokoll" }
        Start-Sleep -Seconds 20
    }
}

function RunnerStatus([string]$repo) {
    GhAufruf @('api', "repos/$repo/actions/runners", '--jq', ".runners[] | select(.name == `"$VmName`") | .status")
}

function VmHerunterfahren {
    Schritt 'Fahre die VM herunter ...'
    VBox @('controlvm', $VmName, 'acpipowerbutton') | Out-Null
    $bis = (Get-Date).AddMinutes(5)
    while ((VmZustand) -ne 'poweroff') {
        if ((Get-Date) -gt $bis) {
            Schritt 'VM reagiert nicht auf den Ausschaltknopf – schalte hart aus ...'
            VBox @('controlvm', $VmName, 'poweroff') | Out-Null
        }
        Start-Sleep -Seconds 5
    }
    Start-Sleep -Seconds 5   # die Sitzung gibt die VM erst kurz nach dem Ausschalten frei
}

function GrundzustandSichern {
    Schritt "Lege Snapshot „$snapshot“ an ..."
    # Ohne serielle Schnittstelle: jeder Start würde sonst das Gast-Protokoll des Aufbaus überschreiben
    VBox @('modifyvm', $VmName, '--uart1', 'off') | Out-Null
    VBox @('snapshot', $VmName, 'take', $snapshot) | Out-Null
}

function AutostartEinrichten {
    Schritt "Richte Autostart ein (geplante Aufgabe „$autostartAufgabe“) ..."
    # Unter dem eigenen Konto ohne erhöhte Rechte – so wie der VirtualBox-Manager, sonst laufen zwei VBoxSVC
    $benutzer = "$env:USERDOMAIN\$env:USERNAME"
    $befehl = "`"$vboxManage`" snapshot $VmName restore $snapshot & `"$vboxManage`" startvm $VmName --type headless"
    $aktion = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/s /c `"$befehl`""
    $ausloeser = New-ScheduledTaskTrigger -AtLogOn -User $benutzer
    $principal = New-ScheduledTaskPrincipal -UserId $benutzer -LogonType Interactive -RunLevel Limited
    $einstellungen = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
    Register-ScheduledTask -TaskName $autostartAufgabe -Action $aktion -Trigger $ausloeser -Principal $principal `
        -Settings $einstellungen -Force | Out-Null
}

function AufRunnerWarten([string]$repo) {
    Schritt 'Starte die VM über die Aufgabe und warte, bis der Runner wieder online ist ...'
    Start-ScheduledTask -TaskName $autostartAufgabe
    $bis = (Get-Date).AddMinutes(10)
    # Erst offline abwarten: GitHub meldet den Runner nach dem Herunterfahren noch eine Weile als online
    $warOffline = $false
    while ($true) {
        $status = RunnerStatus $repo
        if ($status -ne 'online') { $warOffline = $true }
        elseif ($warOffline) { return }
        if ((Get-Date) -gt $bis) { throw "Runner $VmName nach 10 min nicht wieder online (Status: '$status')." }
        Start-Sleep -Seconds 15
    }
}

# ── Ablauf ────────────────────────

if (-not $IsoPfad) { $IsoPfad = StandardIsoFinden }
New-Item -ItemType Directory -Path (Join-Path $ablage 'protokolle') -Force | Out-Null
Start-Transcript -Path (Join-Path $ablage "protokolle\aufbau-$zeitstempel.log") | Out-Null
Push-Location (Split-Path $PSScriptRoot -Parent)   # gh erkennt das Repo am Arbeitsordner; eine Admin-Konsole startet in System32
try {
    $start = Get-Date
    $repo = GhAufruf @('repo', 'view', '--json', 'nameWithOwner', '-q', '.nameWithOwner')
    VoraussetzungenPruefen
    if ($Neuaufbau) { AlteVmEntfernen $repo }
    $zugang = ZugangErzeugen
    PlatteAufbauen $zugang $repo
    VmAnlegen
    VmStarten
    AufEinrichtungWarten $repo
    VmHerunterfahren
    GrundzustandSichern
    AutostartEinrichten
    AufRunnerWarten $repo
    Schritt "Fertig nach $([int]((Get-Date) - $start).TotalMinutes) min: Runner $VmName ist online."
}
finally {
    Pop-Location
    Stop-Transcript | Out-Null
}
