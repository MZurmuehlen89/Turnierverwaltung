using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnisanzeige der Ligaphase als Gegner-Matrix je Topf (#362) samt Team-Fokus: ein angeklicktes
/// Team wird in allen Matrizen hervorgehoben und seine Gegnerliste eingeblendet. Kind-ViewModel von
/// <see cref="AuslosungViewModel"/>, ohne Datenbankzugriff.</summary>
public partial class GegnerMatrixViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<GegnerMatrixTopf> _matrizen = [];
    [ObservableProperty] private ObservableCollection<FokusGegnerEintrag> _fokusGegner = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusGegner))]
    private GegnerMatrixZelle? _fokusTeam;

    public bool ZeigeFokusGegner => FokusTeam is not null;

    /// <summary>Zeigt <paramref name="matrizen"/> an und hebt einen bestehenden Team-Fokus auf.</summary>
    public void Zeigen(ObservableCollection<GegnerMatrixTopf> matrizen)
    {
        Matrizen    = matrizen;
        FokusTeam   = null;
        FokusGegner = [];
    }

    /// <summary>Team anklicken (eigene Zeile oder als Gegner in einer Zelle) → in allen Matrizen hervorheben und
    /// seine Gegnerliste zeigen; erneuter Klick auf dasselbe Team hebt den Fokus auf.</summary>
    [RelayCommand]
    private void TeamFokussieren(GegnerMatrixZelle? zelle)
    {
        if (zelle is { Schluessel: null }) return;   // leere Zelle — Fokus bleibt
        string? fokus = zelle?.Schluessel is { } s && s != FokusTeam?.Schluessel ? s : null;

        GegnerMatrixZeile? fokusZeile = null;
        foreach (var zeile in Matrizen.SelectMany(m => m.Zeilen))
        {
            zeile.IstHervorgehoben = fokus is not null && zeile.Team.Schluessel == fokus;
            if (zeile.IstHervorgehoben) fokusZeile = zeile;
            foreach (var gegner in zeile.Gegner)
                gegner.IstHervorgehoben = fokus is not null && gegner.Schluessel == fokus;
        }

        FokusTeam = fokusZeile?.Team;
        FokusGegner = fokusZeile is null
            ? []
            : new ObservableCollection<FokusGegnerEintrag>(Matrizen[0].Spalten
                .Select((spalte, index) => (spalte, index))
                .Where(x => x.index % 2 == 0)
                .Select(x => new FokusGegnerEintrag(x.spalte.TopfName, fokusZeile.Gegner[x.index], fokusZeile.Gegner[x.index + 1])));
    }
}
