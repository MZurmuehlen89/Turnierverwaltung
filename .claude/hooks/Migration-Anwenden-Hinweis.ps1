# PostToolUse-Hook (#586): nach „dotnet ef migrations add“ daran erinnern, die Migration sofort auf die lokale
# Datenbank anzuwenden – sonst wirft MigrateAsync beim nächsten App-Start bzw. der GUI-Test läuft gegen ein altes Schema.

. "$PSScriptRoot/Hook-Gemeinsam.ps1"

$eingabe = Lies-HookEingabe
$befehl = Befehl-Aus $eingabe
if ($befehl -notmatch 'dotnet\s+ef\s+migrations\s+add\b') { exit 0 }
if ($befehl -match 'dotnet\s+ef\s+database\s+update\b') { exit 0 }

# Nur bei erfolgreich angelegter Migration: Der Befehlstext allein reicht nicht, er steht auch in Commit-Nachrichten
# (Fehlalarm beim ersten Commit von #586). EF meldet den Erfolg mit dem Hinweis auf „ef migrations remove“.
$antwort = $eingabe.tool_response | ConvertTo-Json -Depth 6 -Compress
if ($antwort -notmatch 'migrations remove') { exit 0 }

Melde-AnClaude ("Migration angelegt – jetzt sofort anwenden (#586):`n" +
    "dotnet ef database update --project src/Fussballturnierverwaltung.Infrastructure --startup-project src/Fussballturnierverwaltung.WinUi")
exit 2
