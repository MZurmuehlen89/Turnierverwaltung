using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Tabellenbereiche der gewählten Runde im Rundenformular (Issue #420) — Kind-ViewModel von
/// <see cref="RundenFormularViewModel"/>. Jede Änderung schreibt die vollständige Liste in die gezeigte
/// <see cref="RundeZeile"/> und meldet sie über <see cref="Geaendert"/>.</summary>
public partial class TabellenBereicheViewModel : ObservableObject
{
    private RundeZeile? _zeile;

    public event Action? Geaendert;

    public ObservableCollection<TabellenBereichZeile> Zeilen { get; } = [];

    /// <summary>Zeigt die Bereiche der Runde (<c>null</c> leert die Liste).</summary>
    public void Zeigen(RundeZeile? zeile)
    {
        if (_zeile is not null) _zeile.PropertyChanged -= RundeGeaendert;
        _zeile = zeile;
        if (zeile is not null) zeile.PropertyChanged += RundeGeaendert;
        Zeilen.Clear();
        foreach (var bereich in zeile?.TabellenBereiche ?? [])
            Zeilen.Add(NeueZeile(bereich));
        MeldungenVerteilen();
        Offen = Zeilen.Count > 0;
        OnPropertyChanged(nameof(Kopf));
    }

    private void RundeGeaendert(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RundeZeile.BereichsMeldungen)) MeldungenVerteilen();
    }

    /// <summary>Die Zeilen stehen in der Reihenfolge von <see cref="RundeZeile.TabellenBereiche"/> – die Unterzeile der
    /// Prüffehler ist ihre Position (#601).</summary>
    private void MeldungenVerteilen()
    {
        var meldungen = _zeile?.BereichsMeldungen ?? [];
        for (int i = 0; i < Zeilen.Count; i++)
            Zeilen[i].Meldungen = [.. meldungen.Where(m => m.Unterzeile == i)];
    }

    // ── Aufklappbarer Bereich (Issue #476) ────────────────────────────────────
    [ObservableProperty] private bool _offen;

    // Bis zu drei Bereiche stehen ausgeschrieben in der Kopfzeile, darüber nur ihre Anzahl.
    public AbschnittKopf Kopf => AbschnittKopf.Aus("TABELLENBEREICHE",
        Zeilen.Count <= 3 ? Zeilen.Select(z => z.Kurzfassung) : [$"{Zeilen.Count} Bereiche"],
        "keine");

    [RelayCommand]
    private void Hinzufuegen()
    {
        if (_zeile is null) return;
        int platz = (Zeilen.Count == 0 ? 0 : Zeilen.Max(z => z.PlatzBis)) + 1;
        Zeilen.Add(NeueZeile(new TabellenBereichEingabe(platz, platz, TabellenBereichArt.Weiterkommen, "")));
        InZeileSchreiben();
    }

    [RelayCommand]
    private void Entfernen(TabellenBereichZeile zeile)
    {
        if (Zeilen.Remove(zeile)) InZeileSchreiben();
    }

    private TabellenBereichZeile NeueZeile(TabellenBereichEingabe bereich)
    {
        var zeile = new TabellenBereichZeile(bereich);
        // Nur Zeilen der gezeigten Runde schreiben zurück: Eingabefelder einer eben verworfenen Liste können beim
        // Rundenwechsel noch einen letzten Wert melden (#284).
        zeile.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(TabellenBereichZeile.Meldungen) && Zeilen.Contains(zeile)) InZeileSchreiben();
        };
        return zeile;
    }

    private void InZeileSchreiben()
    {
        if (_zeile is null) return;
        OnPropertyChanged(nameof(Kopf));
        // Auch die Wiederherstellung nach dem ComboBox-Reset meldet eine Änderung — ohne neuen Wert keine Meldung.
        List<TabellenBereichEingabe> bereiche = [.. Zeilen.Select(z => z.ZuEingabe())];
        if (bereiche.SequenceEqual(_zeile.TabellenBereiche)) return;
        _zeile.TabellenBereiche = bereiche;
        Geaendert?.Invoke();
    }
}

/// <summary>Ein Tabellenbereich im Formular (Issue #420).</summary>
public partial class TabellenBereichZeile : ObservableObject
{
    private static readonly IReadOnlyList<TabellenBereichArtOption> _artOptionen =
    [
        new("Weiterkommen", TabellenBereichArt.Weiterkommen),
        new("Relegation", TabellenBereichArt.Relegation),
        new("Abstieg", TabellenBereichArt.Abstieg),
    ];

    // Instanz-Property für die Bindung im Zeilen-Template; die Optionen selbst gibt es nur einmal.
    public IReadOnlyList<TabellenBereichArtOption> ArtOptionen => _artOptionen;

    public TabellenBereichZeile(TabellenBereichEingabe bereich)
    {
        _platzVon = bereich.PlatzVon;
        _platzBis = bereich.PlatzBis;
        _art = _artOptionen.Single(o => o.Wert == bereich.Art);
        _bezeichnung = bereich.Bezeichnung;
    }

    /// <summary>Prüffehler dieses Bereichs (#601) – reine Anzeige.</summary>
    [ObservableProperty] private IReadOnlyList<Feldfehler> _meldungen = [];

    [ObservableProperty] private int _platzVon;
    [ObservableProperty] private int _platzBis;
    [ObservableProperty] private string _bezeichnung;

    private TabellenBereichArtOption _art;

    /// <summary>Die ComboBox schreibt beim ersten Laden <c>null</c> zurück (WinUi/CLAUDE.md, ComboBox-Reset) — das
    /// ist nie eine Nutzerauswahl: verwerfen und die Anzeige danach wiederherstellen.</summary>
    public TabellenBereichArtOption? Art
    {
        get => _art;
        set
        {
            if (value is null)
            {
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
                    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => OnPropertyChanged(nameof(Art)));
                return;
            }
            SetProperty(ref _art, value);
        }
    }

    public TabellenBereichEingabe ZuEingabe() => new(PlatzVon, PlatzBis, _art.Wert, Bezeichnung);

    /// <summary>Für die Kopfzeile des Bereichs: „1–4 Champions League“, ohne Bezeichnung die Art.</summary>
    public string Kurzfassung =>
        $"{(PlatzVon == PlatzBis ? $"{PlatzVon}" : $"{PlatzVon}–{PlatzBis}")} " +
        $"{(string.IsNullOrWhiteSpace(Bezeichnung) ? _art.Anzeige : Bezeichnung.Trim())}";
}

/// <summary>Option der Art-ComboBox eines Tabellenbereichs — feste Instanzen, weil die ComboBox per Referenz
/// vergleicht.</summary>
public record TabellenBereichArtOption(string Anzeige, TabellenBereichArt Wert);
