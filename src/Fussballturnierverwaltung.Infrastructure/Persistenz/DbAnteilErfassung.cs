using Fussballturnierverwaltung.Application.Diagnose;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fussballturnierverwaltung.Infrastructure.Persistenz;

/// <summary>Misst Datenbankbefehle, Verbindungsaufbau und SaveChanges und rechnet sie den offenen Vorgängen des
/// Ablaufprotokolls zu (#49, Stufe 0: Wie groß ist der DB-Anteil eines Vorgangs?). Getrennt von
/// <see cref="DbZugriffsDiagnose"/>, die Kollisionen und langsame Abfragen meldet. Die Kosten (ein Zeitstempel und
/// ein Tabelleneintrag je Befehl) liegen im Mikrosekundenbereich, deshalb läuft die Messung immer mit; geschrieben
/// wird nur, wenn das Ablaufprotokoll aktiv ist.</summary>
public sealed class DbAnteilErfassung : DbCommandInterceptor, IDbConnectionInterceptor, ISaveChangesInterceptor
{
    /// <summary>Ein laufender Befehl; <see cref="Ausgefuehrt"/> ist gesetzt, sobald der Reader da ist.</summary>
    private sealed class Messung(long start, Belegung? belegung)
    {
        public long Start { get; } = start;
        public Belegung? Belegung { get; } = belegung;
        public long? Ausgefuehrt { get; set; }
    }

    /// <summary>Wie lange auf einer Verbindung mindestens ein Befehl offen ist — die Obergrenze des DB-Anteils.
    /// Je Befehl gemessen zählte sie doppelt: Bei AsSplitQuery bleiben alle Teilbefehle einer Abfrage offen, bis
    /// die ganze Abfrage gelesen ist (Diagnose 14.11.2026: „Auslosung laden“ mit 5 Teilbefehlen, gemeinsam nach
    /// ~18 ms geschlossen, je Befehl summiert ~88 ms; die Kaskade kam so auf 205 ms DB in 73 ms Gesamtdauer).</summary>
    private sealed class Belegung
    {
        public int Offen;
        public long Beginn;
    }

    private readonly ConditionalWeakTable<DbCommand, Messung> _befehle = new();
    private readonly ConditionalWeakTable<DbConnection, Belegung> _belegungen = new();
    private readonly ConditionalWeakTable<DbConnection, StrongBox<long>> _verbindungen = new();
    private readonly ConditionalWeakTable<DbContext, StrongBox<long>> _speichern = new();

    // ── Befehle mit Reader ──────────────────────────────────────────────────

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Beginnen(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Beginnen(command);
        return ValueTask.FromResult(result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        ReaderDa(command);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        ReaderDa(command);
        return ValueTask.FromResult(result);
    }

    // Schließen und Dispose: Was zuerst kommt, beendet die Messung (Dispose ohne vorheriges Close kommt vor).
    public override InterceptionResult DataReaderClosing(
        DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
    {
        Beenden(command);
        return result;
    }

    public override ValueTask<InterceptionResult> DataReaderClosingAsync(
        DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
    {
        Beenden(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult DataReaderDisposing(
        DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        Beenden(command);
        return result;
    }

    // ── Befehle ohne Reader ─────────────────────────────────────────────────

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Beginnen(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Beginnen(command);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        ReaderDa(command);
        Beenden(command);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ReaderDa(command);
        Beenden(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Beginnen(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Beginnen(command);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        ReaderDa(command);
        Beenden(command);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        ReaderDa(command);
        Beenden(command);
        return ValueTask.FromResult(result);
    }

    // Ein gescheiterter Befehl zählt nicht als Befehl, gibt aber seine Belegung der Verbindung frei.
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Beenden(command);

    public override Task CommandFailedAsync(
        DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Beenden(command);
        return Task.CompletedTask;
    }

    private void Beginnen(DbCommand command)
    {
        long jetzt = Stopwatch.GetTimestamp();
        Belegung? belegung = null;
        if (command.Connection is { } verbindung)
        {
            belegung = _belegungen.GetValue(verbindung, _ => new Belegung());
            lock (belegung)
                if (belegung.Offen++ == 0) belegung.Beginn = jetzt;
        }
        _befehle.AddOrUpdate(command, new Messung(jetzt, belegung));
    }

    private void ReaderDa(DbCommand command)
    {
        if (_befehle.TryGetValue(command, out var messung))
            messung.Ausgefuehrt = Stopwatch.GetTimestamp();
    }

    private void Beenden(DbCommand command)
    {
        if (!_befehle.TryGetValue(command, out var messung)) return;
        _befehle.Remove(command);

        if (messung.Ausgefuehrt is { } ausgefuehrt)
            AblaufVorgang.DbBefehlErfassen(Stopwatch.GetElapsedTime(messung.Start, ausgefuehrt));

        if (messung.Belegung is not { } belegung) return;
        lock (belegung)
            if (--belegung.Offen == 0)
                AblaufVorgang.DbBelegungErfassen(Stopwatch.GetElapsedTime(belegung.Beginn));
    }

    // ── Verbindungsaufbau ───────────────────────────────────────────────────

    ValueTask<InterceptionResult> IDbConnectionInterceptor.ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken)
    {
        _verbindungen.AddOrUpdate(connection, new StrongBox<long>(Stopwatch.GetTimestamp()));
        return ValueTask.FromResult(result);
    }

    InterceptionResult IDbConnectionInterceptor.ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        _verbindungen.AddOrUpdate(connection, new StrongBox<long>(Stopwatch.GetTimestamp()));
        return result;
    }

    Task IDbConnectionInterceptor.ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken)
    {
        VerbindungAbschliessen(connection);
        return Task.CompletedTask;
    }

    void IDbConnectionInterceptor.ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        VerbindungAbschliessen(connection);

    private void VerbindungAbschliessen(DbConnection connection)
    {
        if (!_verbindungen.TryGetValue(connection, out var start)) return;
        _verbindungen.Remove(connection);
        AblaufVorgang.DbVerbindungErfassen(Stopwatch.GetElapsedTime(start.Value));
    }

    // ── SaveChanges ─────────────────────────────────────────────────────────

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
        SpeichernAbschliessen(eventData.Context);
        return ValueTask.FromResult(result);
    }

    int ISaveChangesInterceptor.SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SpeichernAbschliessen(eventData.Context);
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
        if (context is not null)
            _speichern.AddOrUpdate(context, new StrongBox<long>(Stopwatch.GetTimestamp()));
    }

    private void SpeichernAbschliessen(DbContext? context)
    {
        if (context is null || !_speichern.TryGetValue(context, out var start)) return;
        _speichern.Remove(context);
        AblaufVorgang.DbSpeichernErfassen(Stopwatch.GetElapsedTime(start.Value));
    }
}
