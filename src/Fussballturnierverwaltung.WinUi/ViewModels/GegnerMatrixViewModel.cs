using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ergebnisanzeige der Ligaphase als Gegner-Matrix je Topf (#362) samt Team-Fokus: ein angeklicktes
/// Team wird in allen Matrizen hervorgehoben und seine Gegnerliste eingeblendet. Kind-ViewModel von
/// <see cref="AuslosungViewModel"/>, ohne Datenbankzugriff.</summary>
public partial class GegnerMatrixViewModel : ObservableObject
{
    private ObservableCollection<GegnerMatrixTopf> _matrizen = [];
    private ObservableCollection<FokusGegnerEintrag> _fokusGegner = [];
    private GegnerMatrixZelle? _fokusTeam;

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

        HervorhebungSetzen(fokus);
        var fokusZeile = fokus is null ? null : AlleZeilen.LastOrDefault(z => z.Team.Schluessel == fokus);
        FokusTeam   = fokusZeile?.Team;
        FokusGegner = fokusZeile is null ? [] : GegnerJeTopf(fokusZeile);
    }

    private IEnumerable<GegnerMatrixZeile> AlleZeilen => Matrizen.SelectMany(m => m.Zeilen);

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
        new(Matrizen[0].Spalten
            .Select((spalte, index) => (spalte, index))
            .Where(x => x.index % 2 == 0)
            .Select(x => new FokusGegnerEintrag(x.spalte.TopfName, zeile.Gegner[x.index], zeile.Gegner[x.index + 1])));
}
