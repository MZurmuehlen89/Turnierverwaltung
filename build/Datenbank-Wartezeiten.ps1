<#
.SYNOPSIS
    Zeigt nach einem GUI-Testlauf, worauf der SQL Server gewartet hat (#631).

.DESCRIPTION
    Auf dem CI-Runner brauchten einzelne, sonst schnelle Datenbankzugriffe 6–11 s; ein solcher Stillstand ließ
    DfbPokalTests scheitern. Die Wartestatistik der Instanz und die Dateivergrößerungen aus dem Standard-Trace zeigen,
    ob Plattenzugriffe, Sperren oder das Vergrößern der Dateien dahinterstecken. Die Testdatenbanken sind am Ende des
    Laufs schon gelöscht; Wartestatistik und Trace gelten für die ganze Instanz und überdauern das.

.PARAMETER Server
    SQL-Server-Instanz, Standard: die LocalDB des CI-Runners.
#>
param([string]$Server = '(localdb)\MSSQLLocalDB')

$abfragen = [ordered]@{
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
        $ergebnis = sqlcmd -S $Server -E -C -W -s ' | ' -Q $abfragen[$titel] 2>&1
        $ergebnis | ForEach-Object { $ausgabe.Add("$_") }
        $ausgabe.Add('```')
    }
}

$ausgabe
if ($env:GITHUB_STEP_SUMMARY) { $ausgabe | Add-Content $env:GITHUB_STEP_SUMMARY }
