using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnisanzeige der Ligaphase als Gegner-Matrix je Topf (#362) samt Team-Fokus: ein angeklicktes
/// Team wird in allen Matrizen hervorgehoben und seine Gegnerliste eingeblendet. Kind-ViewModel von
/// <see cref="AuslosungViewModel"/>, ohne Datenbankzugriff.</summary>
public partial class GegnerMatrixViewModel : ObservableObject
{
    private IReadOnlyList<GegnerMatrixTopf> _alleMatrizen = [];
    private ObservableCollection<GegnerMatrixTopf> _matrizen = [];
    private ObservableCollection<FokusGegnerEintrag> _fokusGegner = [];
    private GegnerMatrixZelle? _fokusTeam;
    private bool _sichtbar;
    private int _aufbauLauf;

    /// <summary>Die angezeigten Matrizen — wächst nach dem Einblenden Topf für Topf (#428): alle vier Töpfe der
    /// Ligaphase in einem Zug aufzubauen hielt die Oberfläche gemessen 1,3–1,7 s an, und die laufende Aktion
    /// wartete so lange mit. Leer, solange das Ergebnis verborgen ist.</summary>
    public ObservableCollection<GegnerMatrixTopf> Matrizen
    {
        get => _matrizen;
        private set => SetProperty(ref _matrizen, value);
    }

    public ObservableCollection<FokusGegnerEintrag> FokusGegner
    {
        get => _fokusGegner;
        private set => SetProperty(ref _fokusGegner, value);
    }

    public GegnerMatrixZelle? FokusTeam
    {
        get => _fokusTeam;
        private set { if (SetProperty(ref _fokusTeam, value)) OnPropertyChanged(nameof(ZeigeFokusGegner)); }
    }

    public bool ZeigeFokusGegner => FokusTeam is not null;

    /// <summary>Zeigt <paramref name="matrizen"/> an und hebt einen bestehenden Team-Fokus auf.</summary>
    public void Zeigen(IReadOnlyList<GegnerMatrixTopf> matrizen)
    {
        _alleMatrizen = matrizen;
        FokusTeam     = null;
        FokusGegner   = [];
        AnzeigeNeuAufbauen();
    }

    /// <summary>Vom Eltern-ViewModel gemeldet, sobald das Ergebnis ein- oder ausgeblendet wird. Der Aufbau
    /// beginnt erst mit dem Einblenden: verborgen erzeugt die Oberfläche noch nichts, und das spätere
    /// Einblenden (Ende der TV-Ziehung) baute sonst wieder alles in einem Zug auf.</summary>
    public void SichtbarSetzen(bool sichtbar)
    {
        if (_sichtbar == sichtbar) return;
        _sichtbar = sichtbar;
        AnzeigeNeuAufbauen();
    }

    private void AnzeigeNeuAufbauen()
    {
        int lauf = ++_aufbauLauf;
        Matrizen = [];
        if (!_sichtbar || _alleMatrizen.Count == 0) return;

        Matrizen.Add(_alleMatrizen[0]);
        NaechsteMatrixEinreihen(lauf);
    }

    /// <summary>Je Leerlauf der Oberfläche ein weiterer Topf — dazwischen kommen Eingaben und die Fortsetzung der
    /// laufenden Aktion zum Zug. Ein neuerer Aufbau (anderes Ergebnis, ausgeblendet) lässt diesen enden.</summary>
    private void NaechsteMatrixEinreihen(int lauf)
    {
        if (Matrizen.Count >= _alleMatrizen.Count) return;

        DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (lauf != _aufbauLauf) return;
            Matrizen.Add(_alleMatrizen[Matrizen.Count]);
            NaechsteMatrixEinreihen(lauf);
        });
    }

    /// <summary>Team anklicken (eigene Zeile oder als Gegner in einer Zelle) → in allen Matrizen hervorheben und
    /// seine Gegnerliste zeigen; erneuter Klick auf dasselbe Team hebt den Fokus auf.</summary>
    [RelayCommand]
    private void TeamFokussieren(GegnerMatrixZelle? zelle)
    {
        if (zelle is { Schluessel: null }) return;   // leere Zelle — Fokus bleibt
        string? fokus = zelle?.Schluessel is { } s && s != FokusTeam?.Schluessel ? s : null;

        HervorhebungSetzen(fokus);
        var fokusZeile = fokus is null ? null : AlleZeilen.LastOrDefault(z => z.Team.Schluessel == fokus);
        FokusTeam   = fokusZeile?.Team;
        FokusGegner = fokusZeile is null ? [] : GegnerJeTopf(fokusZeile);
    }

    // Über alle Töpfe, nicht nur die schon angezeigten — ein Fokus während des Aufbaus gilt auch für die folgenden.
    private IEnumerable<GegnerMatrixZeile> AlleZeilen => _alleMatrizen.SelectMany(m => m.Zeilen);

    private void HervorhebungSetzen(string? fokus)
    {
        foreach (var zeile in AlleZeilen)
        {
            zeile.IstHervorgehoben = fokus is not null && zeile.Team.Schluessel == fokus;
            foreach (var gegner in zeile.Gegner)
                gegner.IstHervorgehoben = fokus is not null && gegner.Schluessel == fokus;
        }
    }

    /// <summary>Die Spalten liegen paarweise je Gegner-Topf vor (Heim, Auswärts) — daraus je Topf ein Eintrag.</summary>
    private ObservableCollection<FokusGegnerEintrag> GegnerJeTopf(GegnerMatrixZeile zeile) =>
        new(_alleMatrizen[0].Spalten
            .Select((spalte, index) => (spalte, index))
            .Where(x => x.index % 2 == 0)
            .Select(x => new FokusGegnerEintrag(x.spalte.TopfName, zeile.Gegner[x.index], zeile.Gegner[x.index + 1])));
}
