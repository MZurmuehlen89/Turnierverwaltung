<#
.SYNOPSIS
    Zeigt nach einem GUI-Testlauf, worauf der SQL Server gewartet hat (#631).

.DESCRIPTION
    Auf dem CI-Runner brauchten einzelne, sonst schnelle Datenbankzugriffe 6–11 s; ein solcher Stillstand ließ
    DfbPokalTests scheitern. Die Wartestatistik der Instanz und die Dateivergrößerungen aus dem Standard-Trace zeigen,
    ob Plattenzugriffe, Sperren oder das Vergrößern der Dateien dahinterstecken. Die Testdatenbanken sind am Ende des
    Laufs schon gelöscht; Wartestatistik und Trace gelten für die ganze Instanz und überdauern das.

    Wartet ein Zugriff auf eine Speicherzuteilung (RESOURCE_SEMAPHORE), zeigt die Wartestatistik nicht, welche Abfrage
    das war – im Lauf 43266141948 brauchten Löschen und das Laden der Teilnehmer statt 0,1 s über 10 s. Mit
    -AufzeichnungStarten (vor den Tests) legt das Skript dafür eine Extended-Events-Sitzung an; sie hält jede Wartezeit
    auf eine Zuteilung und jede große Zuteilung samt SQL-Text fest, unabhängig davon, ob die Datenbank noch existiert.

.PARAMETER Server
    SQL-Server-Instanz, Standard: die LocalDB des CI-Runners.

.PARAMETER AufzeichnungStarten
    Legt die Sitzung an und startet sie, statt auszuwerten.
#>
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [switch]$AufzeichnungStarten
)

# ── Aufzeichnung der Speicherzuteilungen ────────────────────────────────────

$SITZUNG = 'Speicherzuteilungen'

# Sqlcmd mit -I: die XML-Methoden der Auswertung brauchen QUOTED_IDENTIFIER. Die Abfrage geht als Datei hinein – mit
# -Q zerlegt die Übergabe an das Programm sie an den doppelten Anführungszeichen der XPath-Ausdrücke. Actions startet
# pwsh mit ErrorActionPreference Stop: Meldungen von sqlcmd auf stderr sollen nur in der Ausgabe landen, nicht den
# Schritt abbrechen.
function Sqlcmd-Ausfuehren([string]$abfrage) {
    $datei = New-TemporaryFile
    try {
        Set-Content $datei $abfrage -Encoding utf8
        & { $ErrorActionPreference = 'Continue'; sqlcmd -S $Server -E -C -I -W -s '|' -i $datei 2>&1 }
    }
    finally { Remove-Item $datei }
}

# Ziel ist eine Datei statt des Ringpuffers: Der verdrängt bei vielen großen Zuteilungen die seltenen Wartezeiten, und
# seine Ausgabe ist auf etwa 4 MB XML begrenzt. Ohne Pfad landet die Datei im Protokollordner der Instanz.
if ($AufzeichnungStarten) {
    Sqlcmd-Ausfuehren @"
CREATE EVENT SESSION [$SITZUNG] ON SERVER
ADD EVENT sqlserver.query_memory_grant_wait_end (ACTION (sqlserver.sql_text, sqlserver.database_name)),
ADD EVENT sqlserver.query_memory_grant_usage (ACTION (sqlserver.sql_text))
ADD TARGET package0.event_file (SET filename = N'$SITZUNG.xel', max_file_size = 100)
WITH (EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS, MAX_DISPATCH_LATENCY = 1 SECONDS);
ALTER EVENT SESSION [$SITZUNG] ON SERVER STATE = START;
SELECT name AS sitzung_aktiv FROM sys.dm_xe_sessions WHERE name = '$SITZUNG';
"@
    exit 0
}

# Den vollständigen Dateinamen meldet die laufende Sitzung; das Muster erfasst auch Folgedateien.
$ereignisse = @"
DECLARE @datei nvarchar(400) = (
    SELECT CAST(t.target_data AS xml).value('(EventFileTarget/File/@name)[1]', 'nvarchar(400)')
    FROM sys.dm_xe_session_targets t JOIN sys.dm_xe_sessions s ON s.address = t.event_session_address
    WHERE s.name = '$SITZUNG' AND t.target_name = 'event_file');
DECLARE @e TABLE (name nvarchar(60), d xml);
IF @datei IS NOT NULL
    INSERT @e SELECT f.object_name, CAST(f.event_data AS xml)
    FROM sys.fn_xe_file_target_read_file(REPLACE(@datei, N'.xel', N'*.xel'), NULL, NULL, NULL) f;
"@

$abfragen = [ordered]@{
    'Wartezeiten auf eine Speicherzuteilung (Extended Events), in zeitlicher Folge' = @"
SET NOCOUNT ON;
$ereignisse
IF @datei IS NULL SELECT 'keine Aufzeichnung' AS hinweis;
ELSE
    SELECT CONVERT(char(12), z.zeit, 114) AS ende_utc, z.warte_s, z.ausgang, z.angefordert_mb, z.erhalten_mb,
           z.verfuegbar_mb, z.vergeben_mb, z.zuteilungen, z.sql_text
    FROM @e AS q
    CROSS APPLY q.d.nodes('/event') AS n(e)
    CROSS APPLY (SELECT e.value('@timestamp', 'datetime2') AS zeit,
                        e.value('(data[@name="wait_time_sec"]/value)[1]', 'int') AS warte_s,
                        e.value('(data[@name="exit_cause"]/text)[1]', 'nvarchar(40)') AS ausgang,
                        e.value('(data[@name="requested_memory_kb"]/value)[1]', 'bigint') / 1024 AS angefordert_mb,
                        e.value('(data[@name="granted_memory_kb"]/value)[1]', 'bigint') / 1024 AS erhalten_mb,
                        e.value('(data[@name="total_available_memory_kb"]/value)[1]', 'bigint') / 1024 AS verfuegbar_mb,
                        e.value('(data[@name="total_granted_memory_kb"]/value)[1]', 'bigint') / 1024 AS vergeben_mb,
                        e.value('(data[@name="total_grantee_count"]/value)[1]', 'int') AS zuteilungen,
                        LEFT(REPLACE(REPLACE(e.value('(action[@name="sql_text"]/value)[1]', 'nvarchar(max)'),
                             CHAR(13), ' '), CHAR(10), ' '), 300) AS sql_text) AS z
    WHERE q.name = 'query_memory_grant_wait_end'
    ORDER BY z.zeit;
"@
    'Größte Speicherzuteilungen (Extended Events, ab 5 MB)' = @"
SET NOCOUNT ON;
$ereignisse
IF @datei IS NULL SELECT 'keine Aufzeichnung' AS hinweis;
ELSE
    SELECT TOP 15 COUNT(*) AS anzahl, MAX(erhalten_kb) / 1024 AS max_erhalten_mb, MAX(genutzt_kb) / 1024 AS max_genutzt_mb,
           sql_text
    FROM (SELECT e.value('(data[@name="granted_memory_kb"]/value)[1]', 'bigint') AS erhalten_kb,
                 e.value('(data[@name="used_memory_kb"]/value)[1]', 'bigint') AS genutzt_kb,
                 LEFT(REPLACE(REPLACE(e.value('(action[@name="sql_text"]/value)[1]', 'nvarchar(max)'),
                      CHAR(13), ' '), CHAR(10), ' '), 300) AS sql_text
          FROM @e AS q CROSS APPLY q.d.nodes('/event') AS n(e)
          WHERE q.name = 'query_memory_grant_usage') AS z
    GROUP BY sql_text
    ORDER BY max_erhalten_mb DESC;
"@
    'Speicher der Instanz und des Rechners am Ende des Laufs' = @'
SET NOCOUNT ON;
SELECT committed_kb / 1024 AS instanz_belegt_mb, committed_target_kb / 1024 AS instanz_ziel_mb FROM sys.dm_os_sys_info;
SELECT total_physical_memory_kb / 1024 AS rechner_gesamt_mb, available_physical_memory_kb / 1024 AS rechner_frei_mb,
       system_memory_state_desc AS zustand FROM sys.dm_os_sys_memory;
SELECT resource_semaphore_id AS semaphore, target_memory_kb / 1024 AS ziel_mb, max_target_memory_kb / 1024 AS max_ziel_mb,
       total_memory_kb / 1024 AS gesamt_mb, available_memory_kb / 1024 AS verfuegbar_mb
FROM sys.dm_exec_query_resource_semaphores WHERE pool_id = 2;
SELECT COUNT(*) AS speicher_knapp_meldungen FROM sys.dm_os_ring_buffers
WHERE ring_buffer_type = 'RING_BUFFER_RESOURCE_MONITOR' AND record LIKE '%LOW%';
'@
    'Wartezeiten (nur Platte, Protokoll, Sperren, Betriebssystem), nach längster Einzelwartezeit' = @'
SET NOCOUNT ON;
SELECT TOP 15 wait_type, waiting_tasks_count AS anzahl, wait_time_ms AS gesamt_ms, max_wait_time_ms AS laengste_ms
FROM sys.dm_os_wait_stats
WHERE waiting_tasks_count > 0
  AND (wait_type LIKE 'PAGEIOLATCH%' OR wait_type LIKE 'LCK%' OR wait_type LIKE 'PREEMPTIVE_OS_%'
       OR wait_type IN ('WRITELOG', 'LOGBUFFER', 'IO_COMPLETION', 'ASYNC_IO_COMPLETION', 'WRITE_COMPLETION',
                        'PAGELATCH_EX', 'PAGELATCH_SH', 'RESOURCE_SEMAPHORE', 'THREADPOOL', 'SOS_SCHEDULER_YIELD'))
ORDER BY max_wait_time_ms DESC;
'@
    'Dateivergrößerungen (Standard-Trace), nach Dauer' = @'
SET NOCOUNT ON;
DECLARE @pfad nvarchar(260) = (SELECT path FROM sys.traces WHERE is_default = 1);
IF @pfad IS NULL SELECT 'kein Standard-Trace' AS hinweis;
ELSE
    SELECT TOP 15 CASE EventClass WHEN 92 THEN 'Daten' ELSE 'Protokoll' END AS datei, DatabaseName AS datenbank,
           CONVERT(char(12), StartTime, 114) AS beginn, Duration / 1000 AS dauer_ms, IntegerData * 8 / 1024 AS mb
    FROM sys.fn_trace_gettable(@pfad, DEFAULT)
    WHERE EventClass IN (92, 93)
    ORDER BY Duration DESC;
'@
    'Vorgaben neuer Datenbanken (model) und Datenbankoptionen' = @'
SET NOCOUNT ON;
SELECT name, size * 8 / 1024 AS mb, CASE is_percent_growth WHEN 1 THEN CONCAT(growth, ' %')
       ELSE CONCAT(growth * 8 / 1024, ' MB') END AS wachstum
FROM model.sys.database_files;
SELECT name, is_auto_close_on, recovery_model_desc FROM sys.databases;
'@
}

$ausgabe = [System.Collections.Generic.List[string]]::new()
$ausgabe.Add('## Datenbank-Wartezeiten')
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    $ausgabe.Add('sqlcmd nicht gefunden – keine Auswertung.')
}
else {
    foreach ($titel in $abfragen.Keys) {
        $ausgabe.Add("### $titel")
        $ausgabe.Add('```')
        Sqlcmd-Ausfuehren $abfragen[$titel] | ForEach-Object { $ausgabe.Add("$_") }
        $ausgabe.Add('```')
    }
}

$ausgabe
if ($env:GITHUB_STEP_SUMMARY) { $ausgabe | Add-Content $env:GITHUB_STEP_SUMMARY }

# Reine Diagnose: Ein fehlgeschlagener sqlcmd-Aufruf darf den Lauf nicht rot färben (Actions übernimmt sonst den
# letzten Exitcode)
exit 0
