using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Anzeigetypen für das Platzraster der Vorbelegung ────────────────────────

/// <summary>Ein Platz im Raster — in einer Gruppe oder als Heim- bzw. Auswärtsplatz einer Paarung. Besetzt und
/// geleert wird er über die <see cref="Platzbelegung"/>, die dabei die verfügbaren Teilnehmer nachführt.</summary>
public class VorbelegungsPlatz : ObservableObject
{
    private TeilnehmerAnzeige? _team;

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

public record ManuelleKoPaarungAnzeige(int PaarungsNr, VorbelegungsPlatz HeimSlot, VorbelegungsPlatz AuswaertsSlot);

public record ManuelleGruppe(int GruppenNr, string GruppenName, IReadOnlyList<VorbelegungsPlatz> Slots);
