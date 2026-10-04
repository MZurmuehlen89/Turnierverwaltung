namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Serialisiert asynchrone DbContext-Zugriffe, die durch unabhängige Fire-and-Forget-Ketten (z.B.
/// zwei schnell aufeinanderfolgende, unabhängige WinUI-Auswahländerungen) überlappen könnten, und fängt die
/// sporadische EF-Core-"second operation"-Exception mit einem einmaligen Retry ab (#317/#338). Jedes
/// ViewModel mit mehreren solcher unabhängigen Fire-and-Forget-Ketten auf demselben Scoped-DbContext
/// braucht eine EIGENE Instanz — eine geteilte/statische Instanz würde unabhängige ViewModels ohne
/// gemeinsamen DbContext unnötig gegeneinander serialisieren. Ursprünglich lokal in
/// <see cref="TurnierTabKoordinator"/> implementiert (#189-Nebenfund), hierher extrahiert, damit
/// <see cref="SpielplanViewModel"/>/<see cref="SimulationsViewModel"/> (Runde-/Spiel-Wechsel-Reentrancy,
/// #338) dieselbe Basis nutzen können, statt sie ein weiteres Mal zu kopieren.</summary>
public sealed class DbContextReentrancySchutz
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Wartet, bis kein anderer Zugriff mehr läuft, und hält den Schutz bis zum Dispose der Rückgabe
    /// (<c>using var schutz = await …BetretenAsync();</c>). Für Nutzeraktionen mit eigener Fehlerbehandlung
    /// (#413): anders als <see cref="AusfuehrenAsync"/> ohne Wiederholversuch — eine schreibende Aktion darf
    /// nicht ein zweites Mal laufen. Nicht wiedereintrittsfähig: wer den Schutz hält, darf nichts abwarten,
    /// das ihn selbst betritt.</summary>
    public async Task<IDisposable> BetretenAsync()
    {
        await _lock.WaitAsync();
        return new Freigabe(_lock);
    }

    private sealed class Freigabe(SemaphoreSlim sperre) : IDisposable
    {
        private bool _freigegeben;

        public void Dispose()
        {
            if (_freigegeben) return;
            _freigegeben = true;
            sperre.Release();
        }
    }

    /// <param name="istNochAktuell">Optionaler Stale-Check, direkt nach Lock-Erhalt ausgewertet: liefert er
    /// <c>false</c>, wird <paramref name="aktion"/> übersprungen. Deckt schnelles Durchklicken (z.B.
    /// Pfeiltasten durch eine Auswahlliste) ab — ohne den Check arbeitet der Lock jede in der Warteschlange
    /// wartende Zwischenauswahl vollständig samt DB-Zugriff ab, bevor die tatsächlich aktuelle drankommt,
    /// was sich als spürbares Ruckeln/Nachhinken der UI bemerkbar macht (Fund aus #338-Test). Weglassen
    /// (wie bisher bei <see cref="TurnierTabKoordinator"/>) erhält das alte "jede Anfrage läuft durch"-
    /// Verhalten.</param>
    public async Task AusfuehrenAsync(Func<Task> aktion, Action<Exception>? fehlerBehandlung = null, Func<bool>? istNochAktuell = null)
    {
        await _lock.WaitAsync();
        try
        {
            if (istNochAktuell?.Invoke() == false) return;

            const int MAX_VERSUCHE = 2;
            for (int versuch = 1; versuch <= MAX_VERSUCHE; versuch++)
            {
                try
                {
                    await aktion();
                    return;
                }
                catch (InvalidOperationException ex) when (versuch < MAX_VERSUCHE && ex.Message.Contains("second operation"))
                {
                    await Task.Delay(50);
                }
            }
        }
        catch (Exception ex)
        {
            // Läuft typischerweise als Fire-and-Forget (_ = ...) ohne Awaiter beim Aufrufer — eine
            // Exception hier würde sonst spurlos verschwinden.
            fehlerBehandlung?.Invoke(ex);
        }
        finally
        {
            _lock.Release();
        }
    }
}
