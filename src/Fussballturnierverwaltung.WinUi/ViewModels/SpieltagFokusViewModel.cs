using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Spieltag-Fokus des Spielplans (#402, aus <see cref="SpielplanViewModel"/> herausgelöst): welche
/// Spieltage die gerade betrachteten Spiele haben, welcher davon gezeigt wird (Issue #363) und die daraus
/// entstehende Liste. Welche Spiele betrachtet werden (Runde bzw. Gruppenfilter), gibt der Spielplan vor.</summary>
public partial class SpieltagFokusViewModel : ObservableObject
{
    private IReadOnlyList<SpielZeile> _relevante = [];
    private bool _nachGruppe;

    public ObservableCollection<int> FokusSpieltage { get; } = [];

    // ── Spieltag-Auswahl (Issue #363, Vorbild kicker.de: ‹ [n. Spieltag ▾] ›) ─────────────────────────

    public ObservableCollection<SpieltagOption> SpieltagOptionen { get; } = [];
    private int _spieltagOptionenImAufbau;

    // false nach Runden-/Gruppenwechsel: der nächste Abgleich wählt den Start-Spieltag neu, statt die
    // bisherige Auswahl zu übernehmen (Spieltagnummern verschiedener Runden überschneiden sich).
    private bool _spieltagAuswahlFestgelegt;

    /// <summary>Abgleich per Referenz aus <see cref="SpieltagOptionen"/> (WinUI-ComboBox, siehe #347-Fund);
    /// <c>null</c> beim Neuaufbau der Liste wird ignoriert.</summary>
    public SpieltagOption? AusgewaehlteSpieltagOption
    {
        get => FokusAlleAktiv
            ? SpieltagOptionen.FirstOrDefault(o => o.Nummer is null)
            : SpieltagOptionen.FirstOrDefault(o => o.Nummer == FokusAktiverSpieltag);
        set
        {
            if (value is null || _spieltagOptionenImAufbau > 0) return;
            if (value.Nummer is { } nummer)
            {
                FokusAktiverSpieltag = nummer;
                FokusAlleAktiv = false;
            }
            else
            {
                FokusAlleAktiv = true;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    private bool _fokusAlleAktiv = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FokusSpiele))]
    private int _fokusAktiverSpieltag;

    public bool HatMehrereSpieltage => FokusSpieltage.Count > 1;

    public bool KannFokusZurueck => !FokusAlleAktiv && FokusSpieltage.Count > 0 && FokusAktiverSpieltag > FokusSpieltage[0];
    public bool KannFokusVor => FokusSpieltage.Count > 1 && (FokusAlleAktiv || FokusAktiverSpieltag < FokusSpieltage[^1]);

    public IReadOnlyList<SpielZeile> FokusSpiele => BerechneFokusSpiele();

    private readonly RelayCommand _fokusVorigerSpieltag;
    private readonly RelayCommand _fokusNaechsterSpieltag;

    public ICommand FokusVorigerSpieltag => _fokusVorigerSpieltag;
    public ICommand FokusNaechsterSpieltag => _fokusNaechsterSpieltag;

    public SpieltagFokusViewModel()
    {
        _fokusVorigerSpieltag = new RelayCommand(FokusEinSpieltag_Vorig, () => KannFokusZurueck);
        _fokusNaechsterSpieltag = new RelayCommand(FokusEinSpieltag_Vor, () => KannFokusVor);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(FokusAlleAktiv) or nameof(FokusAktiverSpieltag))
        {
            OnPropertyChanged(nameof(AusgewaehlteSpieltagOption));
            _fokusVorigerSpieltag.NotifyCanExecuteChanged();
            _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
        }
    }

    public void SpieltagAuswahlFreigeben() => _spieltagAuswahlFestgelegt = false;

    public void LetztenSpieltagZeigen()
    {
        if (FokusSpieltage.Count == 0) return;
        FokusAktiverSpieltag = FokusSpieltage[^1];
        FokusAlleAktiv = false;
    }

    public void FokusSpieleMelden() => OnPropertyChanged(nameof(FokusSpiele));

    private void FokusEinSpieltag_Vorig()
    {
        if (FokusAlleAktiv) return;
        int idx = FokusSpieltage.IndexOf(FokusAktiverSpieltag);
        if (idx > 0) FokusAktiverSpieltag = FokusSpieltage[idx - 1];
    }

    private void FokusEinSpieltag_Vor()
    {
        if (FokusAlleAktiv)
        {
            FokusAlleAktiv = false;
            return;
        }
        int idx = FokusSpieltage.IndexOf(FokusAktiverSpieltag);
        if (idx >= 0 && idx + 1 < FokusSpieltage.Count)
            FokusAktiverSpieltag = FokusSpieltage[idx + 1];
    }

    /// <summary>Gleicht Spieltagliste und Auswahl mit den aktuell geladenen Spielen ab (Issue #363): die
    /// bisherige Auswahl bleibt erhalten, solange es sie noch gibt — sonst bzw. nach Runden-/Gruppenwechsel
    /// greift der Start-Spieltag (erster mit offenen Spielen, sonst der letzte).</summary>
    public void FokusSpieltageAbgleichen(IReadOnlyList<SpielZeile> relevante, bool nachGruppe)
    {
        _relevante = relevante;
        _nachGruppe = nachGruppe;
        var spieltage = relevante.Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value)
            .Distinct().Order().ToList();

        if (!spieltage.SequenceEqual(FokusSpieltage))
        {
            _spieltagOptionenImAufbau++;
            try
            {
                FokusSpieltage.Clear();
                SpieltagOptionen.Clear();
                if (spieltage.Count > 1)
                    SpieltagOptionen.Add(new SpieltagOption(null, "Alle Spieltage"));
                foreach (var st in spieltage)
                {
                    FokusSpieltage.Add(st);
                    SpieltagOptionen.Add(new SpieltagOption(st, $"{st}. Spieltag"));
                }
            }
            finally
            {
                _spieltagOptionenImAufbau--;
            }
        }

        bool auswahlGueltig = _spieltagAuswahlFestgelegt &&
            (FokusAlleAktiv ? spieltage.Count > 1 : spieltage.Contains(FokusAktiverSpieltag));

        if (!auswahlGueltig)
        {
            if (spieltage.Count == 0)
            {
                FokusAktiverSpieltag = 0;
                FokusAlleAktiv = true;
            }
            else
            {
                FokusAktiverSpieltag = spieltage
                    .Where(st => relevante.Any(s => s.Spieltag == st && s.Status != SpielStatus.Abgeschlossen))
                    .DefaultIfEmpty(spieltage[^1])
                    .First();
                FokusAlleAktiv = false;
                _spieltagAuswahlFestgelegt = true;
            }
        }

        OnPropertyChanged(nameof(HatMehrereSpieltage));
        OnPropertyChanged(nameof(AusgewaehlteSpieltagOption));
        OnPropertyChanged(nameof(FokusSpiele));
        _fokusVorigerSpieltag.NotifyCanExecuteChanged();
        _fokusNaechsterSpieltag.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<SpielZeile> BerechneFokusSpiele()
    {
        IEnumerable<SpielZeile> spiele = _relevante;

        if (!FokusAlleAktiv)
            spiele = spiele.Where(s => s.Spieltag == FokusAktiverSpieltag);

        // Issue #363: Gruppenphase ohne Gruppenfilter — ein Spieltag über alle Gruppen, nach Gruppe gegliedert.
        bool nachGruppe = _nachGruppe;
        var liste = nachGruppe
            ? spiele.OrderBy(s => s.GruppeNummer ?? int.MaxValue).ToList()
            : spiele.ToList();

        int? vorigeGruppe = null;
        for (int i = 0; i < liste.Count; i++)
        {
            var zeile = liste[i];
            zeile.GruppenKopf = nachGruppe && (i == 0 || zeile.GruppeNummer != vorigeGruppe)
                ? (zeile.GruppeNummer is { } nr ? $"Gruppe {nr}" : "Ohne Gruppe")
                : null;
            vorigeGruppe = zeile.GruppeNummer;
        }
        return liste;
    }
}
