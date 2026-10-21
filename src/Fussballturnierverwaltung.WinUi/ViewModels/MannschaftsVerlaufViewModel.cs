using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Kurve einer Tabellenrunde auf der Mannschaftsseite: die Tabelle (Liga bzw. Gruppe), in der die Mannschaft
/// spielt, mit ihr hervorgehoben.</summary>
public sealed record MannschaftsVerlaufBlock(
    string Titel, int RundeId, TabellenVerlauf Verlauf, IReadOnlyList<TabellenZone> Zonen,
    IReadOnlyList<VerlaufHervorhebung> Hervorhebung)
{
    public int MannschaftId => Hervorhebung[0].MannschaftId;
}

/// <summary>Block „Tabellenplatz-Verlauf“ der Mannschaftsseite im Statistik-Reiter (#480): je Runde mit Tabelle, in der
/// die gewählte Mannschaft spielt, ein <see cref="MannschaftsVerlaufBlock"/>. Die Verläufe einer Runde werden bis zum
/// nächsten <see cref="InstanzSetzen"/> zwischengespeichert — ein Mannschaftswechsel lädt nichts neu.</summary>
public partial class MannschaftsVerlaufViewModel : ObservableObject
{
    private readonly TabellenService _tabellenService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly Dictionary<int, (IReadOnlyList<TabellenVerlauf> Verlaeufe, IReadOnlyList<TabellenZone> Zonen)> _jeRunde = [];
    private IReadOnlyList<TurnierRunde> _runden = [];
    private int? _angefordert;

    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    /// <summary>Neue Fehlermeldung beim Laden; <c>null</c> räumt die bisherige ab.</summary>
    public event Action<string?>? FehlerGemeldet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatBloecke))]
    private IReadOnlyList<MannschaftsVerlaufBlock> _bloecke = [];

    public bool HatBloecke => Bloecke.Count > 0;

    public MannschaftsVerlaufViewModel(TabellenService tabellenService, UebergangsregelService uebergangsregelService)
    {
        _tabellenService = tabellenService;
        _uebergangsregelService = uebergangsregelService;
    }

    /// <summary>Neues Turnier bzw. neuer Datenstand (ohne Datenbankzugriff) — die zwischengespeicherten Verläufe
    /// verfallen.</summary>
    public void InstanzSetzen(TurnierInstanz? instanz)
    {
        _runden = TabellenVerlaufViewModel.RundenMitTabelle(instanz);
        _jeRunde.Clear();
    }

    /// <summary>Lädt die Blöcke der Mannschaft unter dem <see cref="DbContextSchutz"/>; schnelles Durchklicken
    /// überspringt veraltete Anforderungen. <c>null</c> leert den Block.</summary>
    public void Zeigen(int? mannschaftId)
    {
        _angefordert = mannschaftId;
        if (mannschaftId is not int id)
        {
            Bloecke = [];
            return;
        }
        _ = DbContextSchutz.AusfuehrenAsync(
            () => LadenAsync(id), ex => FehlerGemeldet?.Invoke(ex.Message), istNochAktuell: () => _angefordert == id);
    }

    private async Task LadenAsync(int mannschaftId)
    {
        var bloecke = new List<MannschaftsVerlaufBlock>();
        foreach (var runde in _runden)
        {
            var (verlaeufe, zonen) = await RundeLadenAsync(runde);
            var verlauf = verlaeufe.FirstOrDefault(v => v.Mannschaften.Any(m => m.Mannschaft.Id == mannschaftId));
            if (verlauf is not { HatSpieltage: true }) continue;

            var mannschaft = verlauf.Mannschaften.First(m => m.Mannschaft.Id == mannschaftId).Mannschaft;
            bloecke.Add(new MannschaftsVerlaufBlock(
                Titel(runde, verlauf), runde.Id, verlauf, zonen,
                VerlaufHervorhebungen.Umschalten([], mannschaftId, mannschaft.Name)));
        }
        if (_angefordert == mannschaftId) Bloecke = bloecke;
    }

    private async Task<(IReadOnlyList<TabellenVerlauf>, IReadOnlyList<TabellenZone>)> RundeLadenAsync(TurnierRunde runde)
    {
        if (_jeRunde.TryGetValue(runde.Id, out var geladen)) return geladen;

        var verlaeufe = await _tabellenService.VerlaeufeBerechnenAsync(runde.Id);
        var regeln = await _uebergangsregelService.AlleNachQuellRundeIdAsync(runde.Id);
        var zonen = await _tabellenService.ZonenErmittelnAsync(runde.Id, regeln);
        return _jeRunde[runde.Id] = (verlaeufe, zonen);
    }

    private static string Titel(TurnierRunde runde, TabellenVerlauf verlauf) =>
        verlauf.GruppenNummer is int gruppe ? $"{runde.Name} · Gruppe {GruppenHerkunft.Buchstabe(gruppe)}" : runde.Name;
}
