using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ein Eintrag der Mannschaftsauswahl; <see cref="Alle"/> steht für „kein Filter“.</summary>
public sealed record MannschaftsOption(int Id, string Name, byte[]? Wappen)
{
    public static MannschaftsOption Alle { get; } = new(0, "Alle Mannschaften", null);

    public bool IstAlle => ReferenceEquals(this, Alle);

    public override string ToString() => Name;
}

/// <summary>Auswahlbox „Mannschaft“ (#479) mit Suche beim Tippen über die Teilnehmer des Turniers. Im Spielplan steht
/// „Alle Mannschaften“ als Standard vorn; die Statistik (#324) wählt immer eine Mannschaft und verzichtet darauf
/// (<c>mitAlleOption: false</c>). Die Box fragt eine Wahl nur an (<see cref="WahlAngefordert"/>) — ob sie gilt,
/// entscheidet der Besitzer (der Spielplan z.B. nach der Ungespeichert-Abfrage) und zeigt sie mit
/// <see cref="Anzeigen"/>.</summary>
public partial class MannschaftsfilterViewModel(bool mitAlleOption = true) : ObservableObject
{
    private IReadOnlyList<Mannschaft> _teilnehmer = [];

    /// <summary>Die Vorschläge zur aktuellen Eingabe, ohne Eingabe vorn „Alle Mannschaften“.</summary>
    public ObservableCollection<MannschaftsOption> Vorschlaege { get; } = [];

    /// <summary>Die gefilterte Mannschaft; <c>null</c> = alle.</summary>
    public MannschaftsOption? Gewaehlt { get; private set; }

    /// <summary>Text der Box — beim Tippen die Eingabe, sonst der Name der gewählten Mannschaft.</summary>
    [ObservableProperty] private string _suchText = "";

    public Func<MannschaftsOption?, Task>? WahlAngefordert { get; set; }

    public void TeilnehmerSetzen(IReadOnlyList<Mannschaft> teilnehmer)
    {
        _teilnehmer = teilnehmer;
        VorschlaegeSetzen("");
    }

    public bool IstTeilnehmer(int mannschaftId) => _teilnehmer.Any(m => m.Id == mannschaftId);

    /// <summary>Der Eintrag einer Mannschaft; <c>null</c>, wenn sie nicht zu den Teilnehmern gehört.</summary>
    public MannschaftsOption? OptionFuer(int mannschaftId)
        => _teilnehmer.FirstOrDefault(m => m.Id == mannschaftId) is { } mannschaft ? Option(mannschaft) : null;

    /// <summary>Eingabe in der Box: Vorschläge neu filtern.</summary>
    public void Suchen(string eingabe)
    {
        SuchText = eingabe;
        VorschlaegeSetzen(eingabe);
    }

    /// <summary>Box bekommt den Fokus: alle Vorschläge, damit die Liste wie bei einer Auswahlbox aufklappt.</summary>
    public void AlleVorschlaegeZeigen() => VorschlaegeSetzen("");

    /// <summary>Eingabe bestätigt: der gewählte Vorschlag, sonst der erste Treffer; leere Eingabe = alle (ohne
    /// „Alle Mannschaften“ bleibt die bisherige Wahl).</summary>
    public Task BestaetigenAsync(MannschaftsOption? vorschlag)
    {
        var ziel = vorschlag ?? (SuchText.Trim().Length > 0 ? Vorschlaege.FirstOrDefault()
                                 : mitAlleOption ? MannschaftsOption.Alle : null);
        if (ziel is null)
        {
            EingabeVerwerfen();
            return Task.CompletedTask;
        }
        return WaehlenAsync(ziel.IstAlle ? null : ziel);
    }

    /// <summary>Box verliert den Fokus: eine geleerte Eingabe hebt den Filter auf, sonst zeigt die Box wieder die
    /// gewählte Mannschaft.</summary>
    public Task FokusVerlorenAsync()
    {
        if (_wahlLaeuft) return Task.CompletedTask;
        if (mitAlleOption && SuchText.Trim().Length == 0 && Gewaehlt is not null) return WaehlenAsync(null);
        EingabeVerwerfen();
        return Task.CompletedTask;
    }

    /// <summary>Wahl von außen, z.B. aus dem Kontextmenü „Alle Spiele des Teams anzeigen“.</summary>
    public Task WaehlenNachIdAsync(int mannschaftId) =>
        OptionFuer(mannschaftId) is { } option ? WaehlenAsync(option) : Task.CompletedTask;

    // Solange eine Wahl läuft (z.B. offene Ungespeichert-Abfrage), zählt keine weitere — zwei Abfragen gleichzeitig
    // gingen nicht (ein ContentDialog je Fenster).
    private bool _wahlLaeuft;

    public async Task WaehlenAsync(MannschaftsOption? option)
    {
        if (option?.Id == Gewaehlt?.Id)
        {
            EingabeVerwerfen();
            return;
        }
        if (_wahlLaeuft || WahlAngefordert is null) return;

        _wahlLaeuft = true;
        try
        {
            await WahlAngefordert(option);
        }
        finally
        {
            _wahlLaeuft = false;
        }
    }

    /// <summary>Zeigt die vom Spielplan übernommene Wahl.</summary>
    public void Anzeigen(MannschaftsOption? option)
    {
        Gewaehlt = option;
        OnPropertyChanged(nameof(Gewaehlt));
        EingabeVerwerfen();
    }

    private void EingabeVerwerfen() => SuchText = Gewaehlt?.Name ?? "";

    private void VorschlaegeSetzen(string eingabe)
    {
        Vorschlaege.Clear();
        if (mitAlleOption && eingabe.Trim().Length == 0) Vorschlaege.Add(MannschaftsOption.Alle);
        foreach (var mannschaft in MannschaftsSuche.Treffer(_teilnehmer, eingabe))
            Vorschlaege.Add(Option(mannschaft));
    }

    private static MannschaftsOption Option(Mannschaft m) => new(m.Id, m.Name, MannschaftsIcon.Fuer(m));
}
