using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Stammdaten;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Anzeige der Ziehung von Hand (#482): Ansage, Lostöpfe, Gruppen bzw. Paarungen — anklickbar ist nur, was
/// der aktuelle Schritt anbietet. Die Logik liegt in <see cref="ManuelleZiehung"/>, dieses Kind-ViewModel von
/// <see cref="AuslosungViewModel"/> spiegelt nur ihren Stand und hat keinen Datenbankzugriff; Starten, Abschließen
/// und Abbrechen samt Rückfragen übernimmt der Eltern-ViewModel.</summary>
public partial class HandZiehungViewModel : ObservableObject
{
    private ManuelleZiehung? _ziehung;
    private IReadOnlyList<HandZiehungTeam> _teams = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsmodus))]
    [NotifyPropertyChangedFor(nameof(ZeigeGruppen))]
    private bool _laeuft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstPaarungsmodus))]
    [NotifyPropertyChangedFor(nameof(ZeigeGruppen))]
    private bool _istGruppenmodus;

    [ObservableProperty] private bool _istFertig;
    [ObservableProperty] private string _ansage = "";
    [ObservableProperty] private string _fortschrittText = "";
    [ObservableProperty] private string? _fehler;
    [ObservableProperty] private IReadOnlyList<HandZiehungTopf> _toepfe = [];
    [ObservableProperty] private IReadOnlyList<HandZiehungGruppe> _gruppen = [];
    [ObservableProperty] private IReadOnlyList<HandZiehungPaarung> _paarungen = [];

    public bool ZeigeGruppen     => Laeuft && IstGruppenmodus;
    public bool IstPaarungsmodus => Laeuft && !IstGruppenmodus;

    /// <summary>Die Auslosung, zu der die laufende Ziehung gehört.</summary>
    public int? AuslosungId { get; private set; }

    /// <summary>Ob schon gezogen wurde — dann geht beim Abbrechen oder Wechseln etwas verloren.</summary>
    public bool IstBegonnen => _ziehung?.IstBegonnen == true;

    public void Starten(int auslosungId, ManuelleZiehung ziehung, Qualifikanten qualifikanten)
    {
        _ziehung    = ziehung;
        AuslosungId = auslosungId;
        _teams      = [.. ziehung.Teilnehmer.Select(t => TeamAnzeige(t, qualifikanten))];
        Toepfe      = [.. ziehung.Toepfe.Select(t => new HandZiehungTopf { Name = t.Name, Teams = [.. t.Teilnehmer.Select(i => _teams[i])] })];
        Gruppen     = [.. ziehung.Gruppen.Select(g => new HandZiehungGruppe(g.Nr, g.Name, Waehlen))];
        Paarungen   = [.. Enumerable.Range(1, (ziehung.Teilnehmer.Count + 1) / 2).Select(nr => new HandZiehungPaarung { PaarungsNr = nr })];
        IstGruppenmodus = ziehung.IstGruppenmodus;
        Fehler      = null;
        Laeuft      = true;
        Aktualisieren();
    }

    public void Beenden()
    {
        if (!Laeuft) return;
        _ziehung    = null;
        AuslosungId = null;
        _teams      = [];
        Toepfe      = [];
        Gruppen     = [];
        Paarungen   = [];
        IstFertig   = false;
        Laeuft      = false;
        ZuruecknehmenCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<ManuellePaarungEingabe> Ergebnis() =>
        _ziehung?.Ergebnis() ?? throw new InvalidOperationException("Es läuft keine Ziehung.");

    [RelayCommand(CanExecute = nameof(KannZuruecknehmen))]
    private void Zuruecknehmen()
    {
        _ziehung?.Zuruecknehmen();
        Fehler = null;
        Aktualisieren();
    }

    private bool KannZuruecknehmen() => IstBegonnen;

    private void Waehlen(int wert)
    {
        if (_ziehung is null) return;
        Fehler = null;
        try { _ziehung.Waehlen(wert); }
        catch (InvalidOperationException ex) { Fehler = ex.Message; }
        Aktualisieren();
    }

    private HandZiehungTeam TeamAnzeige(ZiehungsTeilnehmer t, Qualifikanten qualifikanten)
    {
        var mannschaft  = t.Eintrag.Mannschaft ?? qualifikanten.Fuer(t.Eintrag.PlatzhalterName);
        var platzhalter = t.Eintrag.MannschaftId is null && t.Eintrag.PlatzhalterName != t.Name ? t.Eintrag.PlatzhalterName : null;
        return new HandZiehungTeam(t.Index, t.Name, platzhalter, MannschaftsIcon.Fuer(mannschaft), Waehlen);
    }

    // ── Stand spiegeln ────────────────────────────────────────────────────────

    private void Aktualisieren()
    {
        if (_ziehung is not { } ziehung) return;

        var ansage   = ziehung.Ansage;
        var optionen = ansage.Optionen.ToDictionary(o => o.Wert);
        TeamsAktualisieren(ziehung, ansage, optionen);
        GruppenAktualisieren(ziehung, ansage, optionen);
        PaarungenAktualisieren(ziehung);

        Ansage          = ansage.Text;
        IstFertig       = ziehung.IstFertig;
        int gezogen     = _teams.Count(t => t.IstGezogen);
        FortschrittText = $"{gezogen} von {_teams.Count} Teams gezogen";
        ZuruecknehmenCommand.NotifyCanExecuteChanged();
    }

    private void TeamsAktualisieren(
        ManuelleZiehung ziehung, ZiehungsAnsage ansage, IReadOnlyDictionary<int, ZiehungsOption> optionen)
    {
        var gezogen = ziehung.Gruppen.SelectMany(g => g.Plaetze).OfType<int>()
            .Concat(ziehung.Paarungen.SelectMany(p => p.Auswaerts is { } gast ? new[] { p.Heim, gast } : [p.Heim]))
            .ToHashSet();
        if (ziehung.AktuellesTeam is { } aktuell) gezogen.Add(aktuell);

        // _teams steht in der Reihenfolge der Teilnehmer — die Position ist ihr Index.
        bool teamSchritt = ansage.Art is ZiehungsSchrittArt.Team or ZiehungsSchrittArt.Gegner;
        for (int index = 0; index < _teams.Count; index++)
        {
            _teams[index].IstGezogen = gezogen.Contains(index);
            var option = teamSchritt ? optionen.GetValueOrDefault(index) : null;
            _teams[index].Wahl.Setzen(option?.IstFrei == true, option?.SperrGrund);
        }
    }

    private void GruppenAktualisieren(
        ManuelleZiehung ziehung, ZiehungsAnsage ansage, IReadOnlyDictionary<int, ZiehungsOption> optionen)
    {
        foreach (var (gruppe, stand) in Gruppen.Zip(ziehung.Gruppen))
        {
            var gruppenOption = ansage.Art == ZiehungsSchrittArt.Gruppe ? optionen.GetValueOrDefault(stand.Nr) : null;
            gruppe.Wahl.Setzen(gruppenOption?.IstFrei == true, gruppenOption?.SperrGrund);

            bool istZiel = ansage.Art == ZiehungsSchrittArt.Position && ansage.Gruppe == stand.Nr;
            gruppe.IstZiel = istZiel;

            while (gruppe.Plaetze.Count < stand.Plaetze.Count)
                gruppe.Plaetze.Add(new HandZiehungPlatz(gruppe.Plaetze.Count + 1, Waehlen));
            foreach (var platz in gruppe.Plaetze)
            {
                platz.Team = stand.Plaetze.ElementAtOrDefault(platz.Position - 1) is { } t ? _teams[t] : null;
                var platzOption = istZiel ? optionen.GetValueOrDefault(platz.Position) : null;
                platz.Wahl.Setzen(platzOption?.IstFrei == true, platzOption?.SperrGrund);
            }
        }
    }

    private void PaarungenAktualisieren(ManuelleZiehung ziehung)
    {
        var stand = ziehung.Paarungen;
        for (int i = 0; i < Paarungen.Count; i++)
        {
            var paarung = Paarungen[i];
            var p       = i < stand.Count ? stand[i] : null;
            paarung.Heim       = p is null ? null : _teams[p.Heim];
            paarung.Auswaerts  = p?.Auswaerts is { } gast ? _teams[gast] : null;
            paarung.IstFreilos = p?.IstFreilos == true;
            paarung.IstOffen   = p?.IstOffen == true;
        }
    }
}
