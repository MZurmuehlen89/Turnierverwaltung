using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Statistiken;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Bereich „Mannschaft“ im Statistik-Reiter (#324): eine Mannschaft wählen, darunter alle ihre Werte mit Platz
/// im Turnier, Tore nach Spielabschnitt und ihre Spiele. Rechnet nur auf den Spielen, die
/// <see cref="StatistikViewModel"/> geladen hat.</summary>
public partial class MannschaftsStatistikViewModel : ObservableObject
{
    private const string HINWEIS_KEINE_AUSWAHL =
        "Mannschaft oben auswählen – oder per Rechtsklick auf eine Mannschaft „Statistik anzeigen“.";

    private IReadOnlyList<MannschaftsSpiel> _spiele = [];

    public MannschaftsfilterViewModel Auswahl { get; } = new(mitAlleOption: false);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hinweis))]
    [NotifyCanExecuteChangedFor(nameof(AlleSpieleZeigenCommand))]
    private MannschaftsSeite? _seite;

    /// <summary>Nur solange es Spiele gibt, aber keine Mannschaft gewählt ist — ohne Spiele meldet sich der Reiter
    /// selbst.</summary>
    public string? Hinweis => Seite is null && _spiele.Count > 0 ? HINWEIS_KEINE_AUSWAHL : null;

    /// <summary>„Alle Spiele im Spielplan zeigen“ — der Reiterwechsel ist Sache der Turnierdurchführung.</summary>
    public event EventHandler<int>? AlleSpieleAngefordert;

    public MannschaftsStatistikViewModel()
    {
        Auswahl.WahlAngefordert = option =>
        {
            Zeigen(option?.Id);
            return Task.CompletedTask;
        };
    }

    /// <summary>Neue Spiele (Turnier- oder Reiterwechsel): Die gewählte Mannschaft bleibt, solange sie noch Spiele hat.</summary>
    public void SpieleUebernehmen(IReadOnlyList<MannschaftsSpiel> spiele)
    {
        _spiele = spiele;
        Auswahl.TeilnehmerSetzen([.. spiele.Select(s => s.Mannschaft).DistinctBy(m => m.Id)]);
        Zeigen(Auswahl.Gewaehlt?.Id);
        OnPropertyChanged(nameof(Hinweis));
    }

    /// <summary>Zeigt die Mannschaft; eine Mannschaft ohne abgeschlossene Spiele (oder <c>null</c>) leert die Seite.</summary>
    public void Zeigen(int? mannschaftId)
    {
        var option = mannschaftId is int id ? Auswahl.OptionFuer(id) : null;
        Auswahl.Anzeigen(option);
        Seite = option is null ? null : MannschaftsSeitenBuilder.Erstellen(option.Id, _spiele);
    }

    [RelayCommand(CanExecute = nameof(KannAlleSpieleZeigen))]
    private void AlleSpieleZeigen()
    {
        if (Seite is { } seite) AlleSpieleAngefordert?.Invoke(this, seite.Mannschaft.Id);
    }

    private bool KannAlleSpieleZeigen() => Seite is not null;
}
