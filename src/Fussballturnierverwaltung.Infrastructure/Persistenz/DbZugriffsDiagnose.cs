using Fussballturnierverwaltung.Application.Diagnose;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

/// <summary>Dauerhafte Diagnose für "A second operation was started on this context"-Kollisionen und langsame
/// Abfragen (#317/#338-Folgefund 2026-09-22). EF-Core protokolliert bei der Kollision nur eigene Frames — der
/// App-seitige Aufrufer und vor allem die Abfrage, die den Kontext gerade belegt, fehlen. Merkt sich deshalb pro
/// DbContext die aktuell laufende Abfrage (Start, SQL-Anfang, App-Aufrufer) und schreibt beides ins Log:
/// langsame Abfragen (&gt; <see cref="LANGSAM_AB_MS"/>) als Warnung, Kollisionen mit beiden Seiten als Fehler.</summary>
public sealed class DbZugriffsDiagnose(ILogger<DbZugriffsDiagnose> logger)
    : DbCommandInterceptor, IDbConnectionInterceptor, ISaveChangesInterceptor
{
    private const int LANGSAM_AB_MS = 500;

    private sealed class LaufendeAbfrage(string aufrufer)
    {
        public Stopwatch Dauer { get; } = Stopwatch.StartNew();
        public string Aufrufer { get; } = aufrufer;
        public string Sql { get; set; } = "";
    }

    private readonly ConditionalWeakTable<DbContext, LaufendeAbfrage> _laufend = new();

    // ── Abfrage-Beginn/-Ende ────────────────────────────────────────────────

    /// <summary>Aufrufer so früh wie möglich erfassen: ConnectionOpening läuft noch synchron im App-Aufrufpfad —
    /// ab dem EF-internen await auf das Öffnen der Verbindung fehlen die App-Frames im Stack.</summary>
    ValueTask<InterceptionResult> IDbConnectionInterceptor.ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken)
    {
        Beginnen(eventData.Context);
        return ValueTask.FromResult(result);
    }

    InterceptionResult IDbConnectionInterceptor.ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Beginnen(eventData.Context);
        return result;
    }

    /// <summary>Fallback bei bereits offener Verbindung (z. B. in einer Transaktion): dann gibt es kein
    /// ConnectionOpening, und CommandCreating ist der früheste Punkt.</summary>
    public override InterceptionResult<DbCommand> CommandCreating(CommandCorrelatedEventData eventData, InterceptionResult<DbCommand> result)
    {
        if (eventData.Context is { } context && !_laufend.TryGetValue(context, out _))
            Beginnen(context);
        return result;
    }

    private void Beginnen(DbContext? context)
    {
        if (context is not null)
            _laufend.AddOrUpdate(context, new LaufendeAbfrage(AppAufrufer()));
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Merken(eventData.Context, command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Merken(eventData.Context, command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult DataReaderDisposing(
        DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        Abschliessen(eventData.Context);
        return result;
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
        Abschliessen(eventData.Context);

    public override Task CommandFailedAsync(
        DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Abschliessen(eventData.Context);
        return Task.CompletedTask;
    }

    private void Merken(DbContext? context, DbCommand command)
    {
        if (context is not null && _laufend.TryGetValue(context, out var abfrage))
            abfrage.Sql = SqlAnfang(command.CommandText);
    }

    private void Abschliessen(DbContext? context)
    {
        if (context is null || !_laufend.TryGetValue(context, out var abfrage)) return;
        _laufend.Remove(context);

        if (abfrage.Dauer.ElapsedMilliseconds > LANGSAM_AB_MS)
            logger.LogWarning(
                "Langsame DB-Abfrage: {Dauer} ms — {Sql}{NewLine}Aufrufer:{NewLine}{Aufrufer}",
                abfrage.Dauer.ElapsedMilliseconds, abfrage.Sql, Environment.NewLine, Environment.NewLine, abfrage.Aufrufer);
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    // Ablaufprotokoll (#414): je Speichervorgang Dauer, geschriebene Zeilen und Größe des Change-Trackers. Der
    // Kontext lebt in der App so lange wie die Seite — mit jedem geladenen Turnier wächst die Zahl der verfolgten
    // Objekte, die EF vor jedem Speichern auf Änderungen durchsucht.
    private readonly ConditionalWeakTable<DbContext, Stopwatch> _speichern = new();

    ValueTask<InterceptionResult<int>> ISaveChangesInterceptor.SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
    {
        SpeichernBeginnen(eventData.Context);
        return ValueTask.FromResult(result);
    }

    InterceptionResult<int> ISaveChangesInterceptor.SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SpeichernBeginnen(eventData.Context);
        return result;
    }

    ValueTask<int> ISaveChangesInterceptor.SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken)
    {
        SpeichernAbschliessen(eventData.Context, result);
        return ValueTask.FromResult(result);
    }

    int ISaveChangesInterceptor.SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SpeichernAbschliessen(eventData.Context, result);
        return result;
    }

    Task ISaveChangesInterceptor.SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken)
    {
        if (eventData.Context is { } context) _speichern.Remove(context);
        return Task.CompletedTask;
    }

    void ISaveChangesInterceptor.SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context) _speichern.Remove(context);
    }

    private void SpeichernBeginnen(DbContext? context)
    {
        if (context is not null && logger.IsEnabled(LogLevel.Information))
            _speichern.AddOrUpdate(context, Stopwatch.StartNew());
    }

    private void SpeichernAbschliessen(DbContext? context, int geschrieben)
    {
        if (context is null || !_speichern.TryGetValue(context, out var dauer)) return;
        _speichern.Remove(context);
        long ms = dauer.ElapsedMilliseconds;

        // Entries() löst sonst selbst eine Änderungssuche aus — hier soll nur gezählt werden.
        var tracker = context.ChangeTracker;
        bool automatisch = tracker.AutoDetectChangesEnabled;
        tracker.AutoDetectChangesEnabled = false;
        int verfolgt;
        try { verfolgt = tracker.Entries().Count(); }
        finally { tracker.AutoDetectChangesEnabled = automatisch; }

        logger.LogInformation(
            "Speichern: {Dauer} ms, {Geschrieben} Zeile(n) geschrieben, {Verfolgt} verfolgte Objekte",
            ms, geschrieben, verfolgt);
    }

    // ── Kollision ───────────────────────────────────────────────────────────

    /// <summary>Abonniert EFs <see cref="DiagnosticListener"/> ("Microsoft.EntityFrameworkCore") statt
    /// <c>LogTo</c>, damit das bestehende EF-Logging über Serilog unverändert bleibt. Einmal beim Start aufrufen.</summary>
    public void KollisionenUeberwachen() =>
        DiagnosticListener.AllListeners.Subscribe(new Beobachter<DiagnosticListener>(listener =>
        {
            if (listener.Name == DbLoggerCategory.Name)
                listener.Subscribe(new Beobachter<KeyValuePair<string, object?>>(ereignis =>
                {
                    if (ereignis.Key == CoreEventId.QueryIterationFailed.Name && ereignis.Value is EventData daten)
                        AbfrageFehlgeschlagen(daten);
                }));
        }));

    /// <summary>Läuft synchron im Aufrufpfad der gescheiterten Abfrage, daher enthält der aktuelle Stack deren
    /// App-Aufrufer. EF liefert hier nur den Kontext-TYP (<see cref="DbContextTypeErrorEventData"/>), nicht die
    /// Instanz — deshalb werden alle gerade laufenden Abfragen gelistet (im Normalfall genau die blockierende).</summary>
    private void AbfrageFehlgeschlagen(EventData eventData)
    {
        if (eventData is not DbContextTypeErrorEventData fehler || !fehler.Exception.Message.Contains("second operation")) return;

        var laufende = _laufend.Select(kv => kv.Value).ToList();
        string belegtDurch = laufende.Count == 0
            ? "(keine laufende SQL-Abfrage bekannt — Kontext belegt durch SaveChanges, Materialisierung o. ä.)"
            : string.Join(Environment.NewLine, laufende.Select(a =>
                $"seit {a.Dauer.ElapsedMilliseconds} ms: {a.Sql}{Environment.NewLine}{a.Aufrufer}"));

        // Offene Vorgänge des Ablaufprotokolls (#413): zeigen, welche Aktion/welcher Ladevorgang gerade lief —
        // der Stack oben endet an der letzten async-Wiederaufnahme und nennt den Auslöser oft nicht.
        string offeneVorgaenge = AblaufVorgang.OffeneBeschreiben();
        if (offeneVorgaenge.Length == 0) offeneVorgaenge = "(keine)";

        logger.LogError(
            "DbContext-Kollision (second operation).{NewLine}Gescheiterter Aufrufer:{NewLine}{Aufrufer}{NewLine}" +
            "Laufende Abfragen:{NewLine}{BelegtDurch}{NewLine}Offene Vorgänge: {OffeneVorgaenge}",
            Environment.NewLine, Environment.NewLine, AppAufrufer(), Environment.NewLine, Environment.NewLine, belegtDurch,
            Environment.NewLine, offeneVorgaenge);
    }

    // ── Hilfsmittel ─────────────────────────────────────────────────────────

    private sealed class Beobachter<T>(Action<T> beiWert) : IObserver<T>
    {
        public void OnNext(T value) => beiWert(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private static string SqlAnfang(string sql)
    {
        var einzeilig = string.Join(' ', sql.Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(z => z.Trim()));
        return einzeilig.Length > 200 ? einzeilig[..200] + " …" : einzeilig;
    }

    /// <summary>Nur die eigenen Frames (Application/WinUi/Repositories) aus dem aktuellen Stack — bei async-Code
    /// reicht der synchrone Teil bis zur letzten Wiederaufnahme, das genügt, um die auslösende Methode zu sehen.</summary>
    private static string AppAufrufer() =>
        string.Join(Environment.NewLine, new StackTrace(skipFrames: 2, fNeedFileInfo: false).GetFrames()
            .Select(f => f.GetMethod())
            .Where(m => m?.DeclaringType?.FullName is { } typ
                        && typ.StartsWith("Fussballturnierverwaltung", StringComparison.Ordinal)
                        && !typ.Contains(nameof(DbZugriffsDiagnose), StringComparison.Ordinal))
            .Select(m => $"   {m!.DeclaringType!.FullName}.{m.Name}")
            .Distinct()
            .Take(15));
}
