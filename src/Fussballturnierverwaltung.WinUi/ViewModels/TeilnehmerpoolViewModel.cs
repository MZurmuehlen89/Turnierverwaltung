using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Was der Teilnehmerpool von seinem Eltern-ViewModel braucht: Bescheid, wenn sich der Pool geändert hat.</summary>
public interface ITeilnehmerpoolKontext
{
    /// <summary><paramref name="herausgenommen"/> sind Mannschaften, die den Pool verlassen haben und noch in einem Topf
    /// liegen — sie verlassen auch ihren Topf.</summary>
    Task PoolGeaendertAsync(IReadOnlyCollection<int> herausgenommen);
}

/// <summary>Zeile einer der beiden Listen des Teilnehmerpools: <see cref="Rang"/> in der Rangliste der Automatik,
/// <see cref="Zusatz"/> das Land bzw. der Topf, in dem das Team schon liegt.</summary>
public sealed record TeilnehmerpoolZeile(TeilnehmerAnzeige Teilnehmer, string Rang, string Zusatz);

/// <summary>Eine Gruppe der Pool-Liste: alle Teams eines Landes bzw. Verbands, im Kopf der Stand seiner Quote.</summary>
public sealed class TeilnehmerpoolGruppe(int schluessel) : ObservableCollection<TeilnehmerpoolZeile>
{
    private string _kopf = "";

    public int Schluessel { get; } = schluessel;

    public string Kopf
    {
        get => _kopf;
        set
        {
            if (_kopf == value) return;
            _kopf = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Kopf)));
        }
    }
}

/// <summary>Reiter „Teilnehmerpool“ der Auslosung (#576): wer in der ersten Runde eines Turniers mit Quoten dabei ist.
/// Vorbelegt aus den Ranglisten (<see cref="Teilnehmerpool.Vorschlagen"/>), frei tauschbar zwischen „Weitere verfügbar“
/// und dem Pool, darüber die Kacheln der Quoten. Der Pool lebt nur im Speicher: beim erneuten Öffnen wird er aus der
/// Topfbelegung und dem Vorschlag aufgebaut — weicht er davon ab, fragt die Auslosung vor dem Verlassen nach. Kind-ViewModel von
/// <see cref="AuslosungViewModel"/>.</summary>
public partial class TeilnehmerpoolViewModel : ObservableObject
{
    private readonly ITeilnehmerpoolKontext _kontext;
    private readonly TeilnehmerpoolVorbelegung _vorbelegung;

    /// <summary>Alle zugelassenen Teams der Runde; Quelle der Filteroptionen.</summary>
    private readonly ObservableCollection<TeilnehmerAnzeige> _alle = [];
    private Teilnehmerpool? _grundlage;
    private HashSet<int> _pool = [];
    private Dictionary<int, string> _topfJeMannschaft = [];
    private int? _auslosungId;
    private int? _ranglisteId;
    private bool _bearbeitbar;

    public TeilnehmerpoolViewModel(ITeilnehmerpoolKontext kontext, TeilnehmerpoolVorbelegung vorbelegung)
    {
        _kontext = kontext;
        _vorbelegung = vorbelegung;
        Filter = new TeilnehmerFilterViewModel(_alle);
        Quoten = new QuotenAnzeigeViewModel();
        // Klick auf eine Kachel filtert beide Listen, die Hervorhebung folgt dem Filter (#573).
        Quoten.LandGewaehlt += Filter.LandUmschalten;
        Quoten.VerbandGewaehlt += Filter.VerbandUmschalten;
        Filter.Geaendert += () =>
        {
            Quoten.Hervorheben(Filter.AktiverVerband, Filter.AktivesLand);
            ListenAbgleichen();
        };
    }

    /// <summary>Text-, Verbands- und Länderfilter über beiden Listen.</summary>
    public TeilnehmerFilterViewModel Filter { get; }

    /// <summary>Kacheln der Quoten über den Listen.</summary>
    public QuotenAnzeigeViewModel Quoten { get; }

    // Stabile Instanzen, abgeglichen statt neu angelegt — sonst springen die Listen nach jedem Tausch nach oben (#388).
    public ObservableCollection<TeilnehmerpoolZeile> WeitereVerfuegbar { get; } = [];
    public ObservableCollection<TeilnehmerpoolGruppe> Gruppen { get; } = [];

    /// <summary>Ob die gezeigte Auslosung einen Pool hat: nur die erste Runde eines Turniers mit Quoten.</summary>
    [ObservableProperty] private bool _aktiv;

    /// <summary>Welcher Reiter offen ist — der Pool oder die Lostöpfe.</summary>
    [ObservableProperty] private bool _poolReiterAktiv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatMaengel))]
    [NotifyPropertyChangedFor(nameof(MaengelText))]
    [NotifyPropertyChangedFor(nameof(ReiterText))]
    private TeilnehmerpoolStand? _stand;

    /// <summary>Der Pool weicht von dem ab, was beim erneuten Öffnen aufgebaut würde.</summary>
    [ObservableProperty] private bool _hatUngespeicherteAenderungen;

    public bool HatMaengel => Stand is { Erfuellt: false };

    /// <summary>Ohne Pool gibt es nichts zu erfüllen.</summary>
    public bool Erfuellt => !Aktiv || Stand is { Erfuellt: true };

    public string MaengelText => string.Join(" · ", Stand?.Maengel ?? []);

    public string ReiterText => HatMaengel ? "Teilnehmerpool – Quoten offen" : "Teilnehmerpool";

    public int PoolAnzahl => _pool.Count;

    // ── Vom Eltern-ViewModel aufgerufen ────────────────────────────────────────

    /// <summary>Zeigt den Pool von <paramref name="detail"/>. Eine neu gezeigte Auslosung bekommt den Vorschlag zur
    /// Topfbelegung; nach einem Neuladen derselben Auslosung (jede Topf-Änderung lädt neu) bleibt der Pool stehen und
    /// nimmt nur auf, was inzwischen in einem Topf liegt. <paramref name="teilnehmer"/> sind alle zugelassenen Teams.</summary>
    public async Task ZeigenAsync(Auslosung detail, IReadOnlyList<TeilnehmerAnzeige> teilnehmer, int? ranglisteId,
        MannschaftsartFilter mannschaftsart)
    {
        _topfJeMannschaft = TopfJeMannschaft(detail);
        if (_auslosungId != detail.Id)
        {
            _auslosungId = detail.Id;
            _ranglisteId = ranglisteId;
            _grundlage = await _vorbelegung.LadenAsync(detail, teilnehmer, ranglisteId);
            _pool = _grundlage is null ? [] : [.. _grundlage.Vorschlagen(_topfJeMannschaft.Keys)];
            PoolReiterAktiv = _grundlage is not null && !_pool.IsSubsetOf(_topfJeMannschaft.Keys);
            Filter.Zuruecksetzen();
        }
        else
            _pool.UnionWith(_topfJeMannschaft.Keys);

        Aktiv = _grundlage is not null;
        SammlungsAbgleich.Abgleichen(_alle, [.. teilnehmer.Where(t => t.MannschaftId.HasValue)], t => t.MannschaftId);
        Filter.OptionenAbgleichen(mannschaftsart);
        Aktualisieren();
    }

    /// <summary>Turnierwechsel: alles vergessen.</summary>
    public void Leeren()
    {
        _auslosungId = null;
        _grundlage = null;
        _pool = [];
        _topfJeMannschaft = [];
        _alle.Clear();
        Aktiv = false;
        PoolReiterAktiv = false;
        Filter.Zuruecksetzen();
        Aktualisieren();
    }

    public void BearbeitbarSetzen(bool bearbeitbar)
    {
        _bearbeitbar = bearbeitbar;
        NeuVorbelegenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Für die Lostöpfe: bei aktivem Pool nur seine Teams, sonst alle.</summary>
    public IReadOnlyList<TeilnehmerAnzeige> FuerToepfe(IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        Aktiv ? [.. teilnehmer.Where(t => t.MannschaftId is int id && _pool.Contains(id))] : teilnehmer;

    /// <summary>Andere Rangliste der Automatik: ordnet die Listen neu, der Pool selbst bleibt.</summary>
    public async Task RanglisteSetzenAsync(int? ranglisteId)
    {
        if (_grundlage is null || _ranglisteId == ranglisteId) return;
        _ranglisteId = ranglisteId;
        _grundlage = _grundlage with { Rang = await _vorbelegung.RangAsync(ranglisteId) };
        Aktualisieren();
    }

    // ── Tausch (aus dem Code-Behind: Doppelklick, Ziehen, Rechtsklick) ──────────

    public Task AufnehmenAsync(IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        Aendern(teilnehmer, (pool, id) => pool.Add(id));

    public Task HerausnehmenAsync(IReadOnlyList<TeilnehmerAnzeige> teilnehmer) =>
        Aendern(teilnehmer, (pool, id) => pool.Remove(id));

    /// <summary>Stellt den Vorschlag bei leeren Töpfen wieder her; wer dabei herausfällt, verlässt auch seinen Topf.</summary>
    [RelayCommand(CanExecute = nameof(KannNeuVorbelegen))]
    private Task NeuVorbelegen()
    {
        if (_grundlage is null) return Task.CompletedTask;
        _pool = [.. _grundlage.Vorschlagen([])];
        Aktualisieren();
        return _kontext.PoolGeaendertAsync([.. _topfJeMannschaft.Keys.Where(id => !_pool.Contains(id))]);
    }

    private bool KannNeuVorbelegen() => _bearbeitbar && Aktiv;

    private Task Aendern(IReadOnlyList<TeilnehmerAnzeige> teilnehmer, Func<HashSet<int>, int, bool> aenderung)
    {
        if (!_bearbeitbar || !Aktiv) return Task.CompletedTask;
        var geaendert = teilnehmer.Select(t => t.MannschaftId).OfType<int>().Where(id => aenderung(_pool, id)).ToList();
        if (geaendert.Count == 0) return Task.CompletedTask;
        Aktualisieren();
        return _kontext.PoolGeaendertAsync([.. geaendert.Where(id => !_pool.Contains(id) && _topfJeMannschaft.ContainsKey(id))]);
    }

    // ── Stand und Listen ──────────────────────────────────────────────────────

    private void Aktualisieren()
    {
        Stand = _grundlage?.Stand(_pool);
        HatUngespeicherteAenderungen = _grundlage is not null && !_pool.SetEquals(_grundlage.Vorschlagen(_topfJeMannschaft.Keys));
        Quoten.Zeigen(Stand);
        OnPropertyChanged(nameof(Erfuellt));
        OnPropertyChanged(nameof(PoolAnzahl));
        NeuVorbelegenCommand.NotifyCanExecuteChanged();
        ListenAbgleichen();
    }

    private void ListenAbgleichen()
    {
        var sichtbar = _alle.Where(Filter.Passt).OrderBy(RangOderEnde).ThenBy(t => t.AnzeigeName).ToList();
        SammlungsAbgleich.Abgleichen(
            WeitereVerfuegbar,
            [.. sichtbar.Where(t => !_pool.Contains(t.MannschaftId!.Value)).Select(t => Zeile(t, t.LandName ?? ""))],
            z => z.Teilnehmer.MannschaftId,
            GleicheZeile);
        GruppenAbgleichen([.. sichtbar.Where(t => _pool.Contains(t.MannschaftId!.Value))]);
    }

    /// <summary>Nach Land gruppiert, wenn das Turnier Landesquoten hat, sonst nach Verband; Länder bzw. Verbände mit Quote
    /// in der Reihenfolge der Kacheln vorn, die übrigen alphabetisch dahinter.</summary>
    private void GruppenAbgleichen(IReadOnlyList<TeilnehmerAnzeige> imPool)
    {
        var nachLand = Stand is null || Stand.Landesquoten.Count > 0;
        var quoten = (nachLand ? Stand?.Landesquoten : Stand?.Verbandsquoten) ?? [];
        var reihenfolge = quoten.Where(q => q.Schluessel.HasValue).Select((q, i) => (q.Schluessel!.Value, i)).ToDictionary();
        var soll = imPool
            .GroupBy(t => (nachLand ? t.LandId : t.VerbandId) ?? 0)
            .OrderBy(g => reihenfolge.TryGetValue(g.Key, out var i) ? i : int.MaxValue)
            .ThenBy(g => GruppenName(g.First(), nachLand))
            .ToList();

        SammlungsAbgleich.Abgleichen(Gruppen, [.. soll.Select(g => new TeilnehmerpoolGruppe(g.Key))], g => g.Schluessel, (_, _) => true);
        foreach (var gruppe in Gruppen)
        {
            var teams = soll.First(g => g.Key == gruppe.Schluessel).ToList();
            gruppe.Kopf = GruppenKopf(GruppenName(teams[0], nachLand), teams.Count, quoten.FirstOrDefault(q => q.Schluessel == gruppe.Schluessel));
            SammlungsAbgleich.Abgleichen(
                gruppe,
                [.. teams.Select(t => Zeile(t, _topfJeMannschaft.GetValueOrDefault(t.MannschaftId!.Value, "")))],
                z => z.Teilnehmer.MannschaftId,
                GleicheZeile);
        }
    }

    private static string GruppenName(TeilnehmerAnzeige t, bool nachLand) =>
        (nachLand ? t.LandName : t.VerbandName) ?? "Ohne Zuordnung";

    /// <summary>„Deutschland · 4/4 + 1 auf freiem Platz“, ohne Quote „Schottland · 1 auf freiem Platz“.</summary>
    private static string GruppenKopf(string name, int anzahl, QuotenStand? quote) =>
        quote is null
            ? $"{name}  ·  {anzahl} auf freiem Platz"
            : $"{name}  ·  {quote.Besetzt}/{quote.Anzahl}" + (quote.Ueberzaehlig > 0 ? $" + {quote.Ueberzaehlig} auf freiem Platz" : "");

    private TeilnehmerpoolZeile Zeile(TeilnehmerAnzeige t, string zusatz) =>
        new(t, _grundlage?.RangVon(t.MannschaftId!.Value) is int rang ? $"{rang}." : "", zusatz);

    // Das Wappen kommt bei jedem Neuladen als neues Array — der Vergleich darf nur sichtbare Texte prüfen.
    private static bool GleicheZeile(TeilnehmerpoolZeile ist, TeilnehmerpoolZeile soll) =>
        ist.Rang == soll.Rang && ist.Zusatz == soll.Zusatz && ist.Teilnehmer.AnzeigeName == soll.Teilnehmer.AnzeigeName;

    private int RangOderEnde(TeilnehmerAnzeige t) => _grundlage?.RangVon(t.MannschaftId!.Value) ?? int.MaxValue;

    private static Dictionary<int, string> TopfJeMannschaft(Auslosung detail) =>
        detail.Lostoepfe
            .SelectMany(l => l.Eintraege.Where(e => e.MannschaftId.HasValue).Select(e => (Id: e.MannschaftId!.Value, l.Name)))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);
}
