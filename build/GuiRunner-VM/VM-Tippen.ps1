#Requires -Version 7
<#
.SYNOPSIS
    Tippt Text in die GUI-Runner-VM – zur Diagnose, wenn die Einrichtung in der VM scheitert (#609).

.DESCRIPTION
    Die VM hat weder Gasterweiterungen noch Netzwerkzugang vom Host. Was bleibt: Tastatureingaben per
    „VBoxManage controlvm … keyboardputscancode“ und Bildschirmfotos per „controlvm … screenshotpng“.
    keyboardputstring setzt die US-Belegung voraus, die VM hat aber die deutsche – deshalb übersetzt dieses Skript
    jedes Zeichen selbst in deutsche Scancodes (Umschalt bzw. AltGr wo nötig).

    Ergebnisse kommen am bequemsten über COM1 zurück, solange die serielle Schnittstelle noch an ist (bis zum
    Snapshot): im getippten Befehl
        $p=New-Object System.IO.Ports.SerialPort COM1,115200; $p.Open(); $p.WriteLine("..."); $p.Close()
    und auf dem Host protokolle\gast-*.log lesen.

    Fallstrick: Eine Fehlermeldung, die ein Prozess öffnet, hat oft keinen Fokus – die Tasten landen dann gepuffert in
    der Konsole. Mit -AltTab vorholen, mit -Enter bestätigen.

.EXAMPLE
    pwsh build/GuiRunner-VM/VM-Tippen.ps1 -WinR -Text powershell -Enter
.EXAMPLE
    pwsh build/GuiRunner-VM/VM-Tippen.ps1 -Text 'Get-Service SQLWriter' -Enter
#>
param(
    [string]$Text = '',
    [switch]$Enter,
    [switch]$WinR,
    [switch]$AltTab,
    [string]$VmName = 'Turnierverwaltung-GuiRunner'
)

$ErrorActionPreference = 'Stop'
$vboxManage = Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'

# ── Belegung (deutsch, Scancode-Satz 1) ────────────────────────

# Case-sensitive: Hashtables in PowerShell unterscheiden Groß-/Kleinschreibung sonst nicht
$normal = [Collections.Generic.Dictionary[string, int]]::new()
$umschalt = [Collections.Generic.Dictionary[string, int]]::new()
$altGr = [Collections.Generic.Dictionary[string, int]]::new()

function TastenreiheEintragen([string]$zeichen, [int]$ersterCode) {
    for ($i = 0; $i -lt $zeichen.Length; $i++) {
        $normal[[string]$zeichen[$i]] = $ersterCode + $i
        if ($zeichen[$i] -match '[a-z]') { $umschalt[([string]$zeichen[$i]).ToUpper()] = $ersterCode + $i }
    }
}

TastenreiheEintragen '1234567890' 0x02
TastenreiheEintragen 'qwertzuiop' 0x10
TastenreiheEintragen 'asdfghjkl' 0x1E
TastenreiheEintragen 'yxcvbnm' 0x2C
$ziffernUmschalt = '!"§$%&/()='
for ($i = 0; $i -lt $ziffernUmschalt.Length; $i++) { $umschalt[[string]$ziffernUmschalt[$i]] = 0x02 + $i }
$normal['ß'] = 0x0C; $umschalt['?'] = 0x0C; $altGr['\'] = 0x0C
$normal['+'] = 0x1B; $umschalt['*'] = 0x1B; $altGr['~'] = 0x1B
$normal['#'] = 0x2B; $umschalt["'"] = 0x2B
$normal[','] = 0x33; $umschalt[';'] = 0x33
$normal['.'] = 0x34; $umschalt[':'] = 0x34
$normal['-'] = 0x35; $umschalt['_'] = 0x35
$normal['<'] = 0x56; $umschalt['>'] = 0x56; $altGr['|'] = 0x56
$altGr['{'] = 0x08; $altGr['['] = 0x09; $altGr[']'] = 0x0A; $altGr['}'] = 0x0B; $altGr['@'] = 0x10
$normal[' '] = 0x39

# ── Senden ────────────────────────

function Hex([int]$code) { '{0:x2}' -f $code }

function Taste([int]$code) { @((Hex $code), (Hex ($code + 0x80))) }   # drücken, loslassen

function ScancodesFuer([string]$zeichen) {
    if ($normal.ContainsKey($zeichen)) { return Taste $normal[$zeichen] }
    if ($umschalt.ContainsKey($zeichen)) { return @('2a') + (Taste $umschalt[$zeichen]) + @('aa') }
    if ($altGr.ContainsKey($zeichen)) { return @('e0', '38') + (Taste $altGr[$zeichen]) + @('e0', 'b8') }
    throw "Zeichen '$zeichen' ist nicht abgebildet."
}

function Senden([string[]]$codes) {
    & $vboxManage controlvm $VmName keyboardputscancode @codes
    if ($LASTEXITCODE -ne 0) { throw "keyboardputscancode fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

if ($AltTab) { Senden @('38', '0f', '8f', 'b8'); Start-Sleep -Milliseconds 800 }
if ($WinR) { Senden @('e0', '5b', '13', '93', 'e0', 'db'); Start-Sleep -Milliseconds 800 }
$puffer = [Collections.Generic.List[string]]::new()
foreach ($zeichen in $Text.ToCharArray()) {
    $puffer.AddRange([string[]](ScancodesFuer ([string]$zeichen)))
    if ($puffer.Count -ge 60) { Senden $puffer.ToArray(); $puffer.Clear() }
}
if ($puffer.Count) { Senden $puffer.ToArray() }
if ($Enter) { Senden @('1c', '9c') }
