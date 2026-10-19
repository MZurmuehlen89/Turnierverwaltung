using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Spieltag-Fokus des Spielplans (#402, aus <see cref="SpielplanViewModel"/> herausgelöst): welche
/// Spieltage die gerade betrachteten Spiele haben, welcher davon gezeigt wird (Issue #363, Vorbild kicker.de:
/// ‹ [n. Spieltag ▾] ›) und die daraus entstehende Liste. Welche Spiele betrachtet werden (Runde bzw.
/// Gruppenfilter), gibt der Spielplan vor.</summary>
public partial class SpieltagFokusViewModel : ObservableObject
{
    private readonly RelayCommand _vorigerSpieltagBefehl;
    private readonly RelayCommand _naechsterSpieltagBefehl;

    private IReadOnlyList<SpielZeile> _betrachteteSpiele = [];
    private bool _nachGruppeGegliedert;
    private List<int> _spieltage = [];
    private int _optionenImAufbau;

    // "Alle Spieltage" merkt sich den zuletzt gezeigten Spieltag: › führt von dort wieder zu ihm zurück.
    private bool _alleAktiv = true;
    private int _aktiverSpieltag;

    // false nach Runden-/Gruppenwechsel: der nächste Abgleich wählt den Start-Spieltag neu, statt die
    // bisherige Auswahl zu übernehmen (Spieltagnummern verschiedener Runden überschneiden sich).
    private bool _auswahlFestgelegt;

    public SpieltagFokusViewModel()
    {
        _vorigerSpieltagBefehl = new RelayCommand(VorigenSpieltagZeigen, KannVorigenSpieltagZeigen);
        _naechsterSpieltagBefehl = new RelayCommand(NaechstenSpieltagZeigen, KannNaechstenSpieltagZeigen);
    }

    public ObservableCollection<SpieltagOption> Optionen { get; } = [];

    /// <summary>Abgleich per Referenz aus <see cref="Optionen"/> (WinUI-ComboBox, siehe #347-Fund);
    /// <c>null</c> beim Neuaufbau der Liste wird ignoriert.</summary>
    public SpieltagOption? AusgewaehlteOption
    {
        get => Optionen.FirstOrDefault(o => o.Nummer == GewaehlterSpieltag);
        set
        {
            if (value is null || _optionenImAufbau > 0) return;
            if (value.Nummer is { } nummer) Zeigen(nummer);
            else AlleZeigen();
        }
    }

    /// <summary>Der gezeigte Spieltag; <c>null</c> = alle Spieltage.</summary>
    public int? GewaehlterSpieltag => _alleAktiv ? null : _aktiverSpieltag;

    public bool HatMehrereSpieltage => _spieltage.Count > 1;

    public ICommand VorigerSpieltagBefehl => _vorigerSpieltagBefehl;
    public ICommand NaechsterSpieltagBefehl => _naechsterSpieltagBefehl;

    /// <summary>Die betrachteten Spiele des gewählten Spieltags. Jeder Abruf liefert eine neue Liste und setzt
    /// die Gruppenköpfe der Zeilen passend zu ihr.</summary>
    public IReadOnlyList<SpielZeile> Spiele
    {
        get
        {
            var spiele = _alleAktiv
                ? _betrachteteSpiele
                : _betrachteteSpiele.Where(s => s.Spieltag == _aktiverSpieltag);

            // Issue #363: Gruppenphase ohne Gruppenfilter — ein Spieltag über alle Gruppen, nach Gruppe gegliedert.
            var liste = _nachGruppeGegliedert
                ? spiele.OrderBy(s => s.GruppeNummer ?? int.MaxValue).ToList()
                : spiele.ToList();
            GruppenkoepfeSetzen(liste);
            return liste;
        }
    }

    /// <summary>Der nächste Abgleich beginnt wieder beim Start-Spieltag (Rundenwechsel).</summary>
    public void AuswahlFreigeben() => _auswahlFestgelegt = false;

    /// <summary>Zeigt den Spieltag eines Spiels, falls gerade ein anderer gezeigt wird (Sprung zu einem Spiel, #62).</summary>
    public void SpieltagZeigen(int? spieltag)
    {
        if (!_alleAktiv && spieltag is { } st && st != _aktiverSpieltag && _spieltage.Contains(st)) Zeigen(st);
    }

    public void LetztenSpieltagZeigen()
    {
        if (_spieltage.Count > 0) Zeigen(_spieltage[^1]);
    }

    /// <summary>Die Liste soll neu abgerufen werden, ohne dass sich die betrachteten Spiele geändert haben.</summary>
    public void SpieleMelden() => OnPropertyChanged(nameof(Spiele));

    /// <summary>Gleicht Spieltagliste und Auswahl mit den betrachteten Spielen ab (Issue #363): die bisherige
    /// Auswahl bleibt erhalten, solange es sie noch gibt — sonst bzw. nach <see cref="AuswahlFreigeben"/> greift
    /// der Start-Spieltag (erster mit offenen Spielen, sonst der letzte).</summary>
    public void Abgleichen(IReadOnlyList<SpielZeile> betrachteteSpiele, bool nachGruppeGegliedert)
    {
        _betrachteteSpiele = betrachteteSpiele;
        _nachGruppeGegliedert = nachGruppeGegliedert;

        SpieltageUebernehmen(betrachteteSpiele
            .Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value).Distinct().Order().ToList());
        AuswahlSicherstellen();

        OnPropertyChanged(nameof(HatMehrereSpieltage));
        OnPropertyChanged(nameof(AusgewaehlteOption));
        OnPropertyChanged(nameof(Spiele));
        BefehleMelden();
    }

    private void SpieltageUebernehmen(List<int> spieltage)
    {
        if (spieltage.SequenceEqual(_spieltage)) return;

        _optionenImAufbau++;
        try
        {
            _spieltage = spieltage;
            Optionen.Clear();
            if (spieltage.Count > 1)
                Optionen.Add(new SpieltagOption(null, "Alle Spieltage"));
            foreach (var st in spieltage)
                Optionen.Add(new SpieltagOption(st, $"{st}. Spieltag"));
        }
        finally
        {
            _optionenImAufbau--;
        }
    }

    private void AuswahlSicherstellen()
    {
        bool auswahlGueltig = _auswahlFestgelegt &&
            (_alleAktiv ? _spieltage.Count > 1 : _spieltage.Contains(_aktiverSpieltag));
        if (auswahlGueltig) return;

        var start = SpieltagFokus.StartSpieltag(
            _betrachteteSpiele.Select(s => (s.Spieltag, s.Status == SpielStatus.Abgeschlossen)));
        if (start is { } spieltag)
        {
            Zeigen(spieltag);
            _auswahlFestgelegt = true;
        }
        else
        {
            AuswahlSetzen(alle: true, spieltag: 0);
        }
    }

    private bool KannVorigenSpieltagZeigen() => !_alleAktiv && _spieltage.IndexOf(_aktiverSpieltag) > 0;

    private void VorigenSpieltagZeigen()
    {
        if (KannVorigenSpieltagZeigen()) Zeigen(_spieltage[_spieltage.IndexOf(_aktiverSpieltag) - 1]);
    }

    private bool KannNaechstenSpieltagZeigen() =>
        _spieltage.Count > 1 && (_alleAktiv || _aktiverSpieltag < _spieltage[^1]);

    private void NaechstenSpieltagZeigen()
    {
        int idx = _spieltage.IndexOf(_aktiverSpieltag);
        if (_alleAktiv) Zeigen(_aktiverSpieltag);
        else if (idx >= 0 && idx + 1 < _spieltage.Count) Zeigen(_spieltage[idx + 1]);
    }

    private void Zeigen(int spieltag) => AuswahlSetzen(alle: false, spieltag);
    private void AlleZeigen() => AuswahlSetzen(alle: true, _aktiverSpieltag);

    private void AuswahlSetzen(bool alle, int spieltag)
    {
        if (_alleAktiv == alle && _aktiverSpieltag == spieltag) return;
        _alleAktiv = alle;
        _aktiverSpieltag = spieltag;

        OnPropertyChanged(nameof(GewaehlterSpieltag));
        OnPropertyChanged(nameof(AusgewaehlteOption));
        OnPropertyChanged(nameof(Spiele));
        BefehleMelden();
    }

    private void BefehleMelden()
    {
        _vorigerSpieltagBefehl.NotifyCanExecuteChanged();
        _naechsterSpieltagBefehl.NotifyCanExecuteChanged();
    }

    private void GruppenkoepfeSetzen(List<SpielZeile> liste)
    {
        int? vorigeGruppe = null;
        for (int i = 0; i < liste.Count; i++)
        {
            var zeile = liste[i];
            zeile.GruppenKopf = _nachGruppeGegliedert && (i == 0 || zeile.GruppeNummer != vorigeGruppe)
                ? (zeile.GruppeNummer is { } nr ? $"Gruppe {nr}" : "Ohne Gruppe")
                : null;
            vorigeGruppe = zeile.GruppeNummer;
        }
    }
}
