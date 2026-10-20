using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Diagnose;
using Microsoft.UI.Dispatching;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Zentrale Ladeanzeige des Hauptfensters (#323): folgt den offenen Vorgängen des Ablaufprotokolls
/// (<see cref="AblaufVorgang"/>) — Balken ab 300 ms, Text ab 2 s (<see cref="LadeanzeigeStand"/>). Solange
/// ein Vorgang offen ist, prüft ein Timer den Stand nach, weil die Schwellen ohne neues Ereignis
/// überschritten werden.</summary>
public sealed partial class LadeanzeigeViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextSichtbar))]
    private string? _text;

    [ObservableProperty] private bool _balkenSichtbar;

    public bool TextSichtbar => Text is not null;

    /// <summary>Auf dem UI-Thread anzulegen — dessen DispatcherQueue nimmt die Ereignisse entgegen, die auf
    /// beliebigen Threads feuern können.</summary>
    public LadeanzeigeViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => Aktualisieren();

        AblaufVorgang.Geaendert += () => _dispatcher.TryEnqueue(Aktualisieren);
    }

    private void Aktualisieren()
    {
        var offene = AblaufVorgang.Offene();
        var stand = LadeanzeigeStand.Ermitteln(offene);
        BalkenSichtbar = stand.BalkenSichtbar;
        Text = stand.Text;

        if (offene.Count == 0) _timer.Stop();
        else if (!_timer.IsRunning) _timer.Start();
    }
}
