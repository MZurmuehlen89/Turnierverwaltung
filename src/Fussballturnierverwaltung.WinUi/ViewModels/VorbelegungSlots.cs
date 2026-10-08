using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Anzeigetypen für die Slot-Grids — 1:1 aus Wpf/ViewModels/ManuellePaarungWorkspaceViewModel.cs portiert ──

public class ManuellerKoSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int PaarungsNr { get; init; }
    public bool IstHeim { get; init; }

    public TeilnehmerAnzeige? Team
    {
        get => _team;
        private set
        {
            if (SetProperty(ref _team, value))
            {
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(AnzeigeName));
            }
        }
    }
    public bool IstLeer => Team is null;
    public string AnzeigeName => Team?.Name ?? "…";

    public void Besetzen(TeilnehmerAnzeige team) => Team = team;
    public TeilnehmerAnzeige? Leeren()
    {
        var team = Team;
        Team = null;
        return team;
    }
}

public record ManuelleKoPaarungAnzeige(int PaarungsNr, ManuellerKoSlot HeimSlot, ManuellerKoSlot AuswaertsSlot);

public class ManuellerGruppenSlot : ObservableObject
{
    private TeilnehmerAnzeige? _team;
    public int GruppenNr { get; init; }
    public int SlotNr { get; init; }

    public TeilnehmerAnzeige? Team
    {
        get => _team;
        private set
        {
            if (SetProperty(ref _team, value))
            {
                OnPropertyChanged(nameof(IstLeer));
                OnPropertyChanged(nameof(AnzeigeName));
            }
        }
    }
    public bool IstLeer => Team is null;
    public string AnzeigeName => Team?.Name ?? "…";

    public void Besetzen(TeilnehmerAnzeige team) => Team = team;
    public TeilnehmerAnzeige? Leeren()
    {
        var team = Team;
        Team = null;
        return team;
    }
}

public class ManuelleGruppe : ObservableObject
{
    public int GruppenNr { get; init; }
    public string GruppenName { get; init; } = "";
    public ObservableCollection<ManuellerGruppenSlot> Slots { get; init; } = [];
}
