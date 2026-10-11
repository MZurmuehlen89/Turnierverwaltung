using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Converters;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record TurnierStatusOption(string Anzeige, TurnierStatus Wert);

/// <summary>Statusauswahl des Turnierkopfs (#448). Bei einem neuen, noch nicht gespeicherten Turnier ist die
/// Wahl Teil der Eingabe (es gibt noch keine Id, <see cref="EingabeGeaendert"/>) — bei einem bestehenden wirkt
/// sie sofort, unabhängig vom "Speichern" der übrigen Felder (Issue #297). Den Wechsel selbst (Rückfrage,
/// Speichern, Fehlermeldung) führt <see cref="TurnierkopfViewModel"/> aus; lehnt der Nutzer ab oder scheitert
/// der Wechsel, springt die Auswahl auf den gespeicherten Status zurück.</summary>
public partial class TurnierStatusViewModel : ObservableObject
{
    private readonly Func<TurnierStatus, Task<bool>> _wechseln;
    private int _reaktionenAusgesetzt;
    private bool _istEingabe;
    private TurnierStatus? _gespeicherterStatus;

    /// <summary>Der Nutzer hat den Status eines neuen Turniers gewählt.</summary>
    public event Action? EingabeGeaendert;

    [ObservableProperty] private TurnierStatusOption _auswahl = new(TurnierStatusZuTextConverter.StatusAnzeigeName(TurnierStatus.Geplant), TurnierStatus.Geplant);

    public IReadOnlyList<TurnierStatusOption> Optionen =>
        Enum.GetValues<TurnierStatus>().Select(s => new TurnierStatusOption(TurnierStatusZuTextConverter.StatusAnzeigeName(s), s)).ToList();

    public TurnierStatus Wert => Auswahl.Wert;

    /// <param name="wechseln">Wechselt den Status des gezeigten Turniers; <c>false</c>, wenn es beim bisherigen
    /// Status bleibt.</param>
    public TurnierStatusViewModel(Func<TurnierStatus, Task<bool>> wechseln)
    {
        _wechseln = wechseln;
    }

    /// <summary>Kein Turnier gezeigt: Eine Wahl bleibt ohne Folgen.</summary>
    public void Leeren()
    {
        _istEingabe = false;
        _gespeicherterStatus = null;
        AuswahlSetzen(TurnierStatus.Geplant);
    }

    /// <summary>Neues Turnier: Die Wahl gehört zur Eingabe und wird mit ihr gespeichert.</summary>
    public void AlsEingabeSetzen(TurnierStatus status)
    {
        _istEingabe = true;
        _gespeicherterStatus = null;
        AuswahlSetzen(status);
    }

    /// <summary>Bestehendes Turnier: Eine abweichende Wahl wechselt den Status sofort.</summary>
    public void GespeichertenSetzen(TurnierStatus status)
    {
        _istEingabe = false;
        _gespeicherterStatus = status;
        AuswahlSetzen(status);
    }

    /// <summary>Solange das Eltern-ViewModel ein Turnier lädt, bleiben Rückschreibungen der ComboBox ohne
    /// Folgen. Jeder Aufruf braucht sein <see cref="ReaktionenFortsetzen"/>.</summary>
    public void ReaktionenAussetzen() => _reaktionenAusgesetzt++;

    public void ReaktionenFortsetzen() => _reaktionenAusgesetzt--;

    partial void OnAuswahlChanged(TurnierStatusOption value)
    {
        if (_reaktionenAusgesetzt > 0) return;
        if (_istEingabe) { EingabeGeaendert?.Invoke(); return; }
        if (_gespeicherterStatus is { } gespeichert && value.Wert != gespeichert)
            _ = WechselnAsync(value.Wert);
    }

    private async Task WechselnAsync(TurnierStatus neuerStatus)
    {
        if (!await _wechseln(neuerStatus)) AuswahlZuruecksetzen();
    }

    // Bis der Wechsel entschieden ist, kann ein anderes Turnier gezeigt werden — maßgeblich ist dessen Status.
    private void AuswahlZuruecksetzen()
    {
        if (_gespeicherterStatus is { } gespeichert) AuswahlSetzen(gespeichert);
    }

    private void AuswahlSetzen(TurnierStatus status)
    {
        ReaktionenAussetzen();
        try { Auswahl = Optionen.First(o => o.Wert == status); }
        finally { ReaktionenFortsetzen(); }
    }
}
