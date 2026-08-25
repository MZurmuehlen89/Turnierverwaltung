using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>Eine Zeile innerhalb eines <see cref="UebergangsregelBlock"/> — je Gruppe (Gruppenphase-Quellrunde),
/// je Spiel (K.o.-Quellrunde) oder eine einzelne Zeile (Liga-Quellrunde, <see cref="GruppeIndex"/> = null).</summary>
public partial class UebergangsregelZeile : ObservableObject
{
    private bool _populiertGerade;

    /// <summary>Datenbank-Id der zugehörigen Uebergangsregel, oder null solange noch nicht gespeichert.</summary>
    public int? Id { get; set; }

    [ObservableProperty] private int? _gruppeIndex;
    [ObservableProperty] private int _platzierungVon = 1;
    [ObservableProperty] private int _platzierungBis = 1;
    [ObservableProperty] private bool _istAbweichend;

    // Gesetzt = gruppenübergreifende Rangfolge-Regel (Issue #184): GruppeIndex ist dann null, PlatzierungVon
    // == PlatzierungBis (genau eine Platzierung), AnzahlBeste ist die Anzahl der besten Teams über alle Gruppen.
    [ObservableProperty] private int? _anzahlBeste;

    public string GruppenLabel => AnzahlBeste is { } n ? $"Beste {n}" : GruppeIndex is null ? "Alle" : $"Gruppe {(char)('A' + GruppeIndex.Value - 1)}";

    // Nur relevant für K.o.-Sonderfall-Zeilen (GruppeIndex = Spielnummer): Checkbox-Ersatz für den früheren
    // "Platz von/bis"-Zahlenbereich (Issue #186-Folgefund) — Platz 1 = Sieger, Platz 2 = Verlierer. Aus einem
    // Spiel kann immer nur einer von beiden weiterziehen, daher gegenseitig exklusiv (radiobutton-artig) statt
    // unabhängig — eine Abwahl ohne die jeweils andere Auswahl wird ignoriert (Checkbox springt zurück).
    public bool IstSieger
    {
        get => PlatzierungVon == 1 && PlatzierungBis == 1;
        set { if (value) PlatzSetzen(sieger: true); else OnPropertyChanged(nameof(IstSieger)); }
    }

    public bool IstVerlierer
    {
        get => PlatzierungVon == 2 && PlatzierungBis == 2;
        set { if (value) PlatzSetzen(sieger: false); else OnPropertyChanged(nameof(IstVerlierer)); }
    }

    private void PlatzSetzen(bool sieger)
    {
        PlatzierungVon = sieger ? 1 : 2;
        PlatzierungBis = sieger ? 1 : 2;
        OnPropertyChanged(nameof(IstSieger));
        OnPropertyChanged(nameof(IstVerlierer));
    }

    // Nur relevant für gruppenübergreifende Zeilen (AnzahlBeste gesetzt): eine einzelne Platzierung statt
    // eines Von/Bis-Bereichs (Issue #184) - setzt beide Felder synchron.
    public int Platzierung
    {
        get => PlatzierungVon;
        set { PlatzierungVon = value; PlatzierungBis = value; }
    }

    partial void OnPlatzierungVonChanged(int value) { if (!_populiertGerade) IstAbweichend = true; }
    partial void OnPlatzierungBisChanged(int value) { if (!_populiertGerade) IstAbweichend = true; }

    /// <summary>Setzt von/bis ohne die Zeile als manuell abweichend zu markieren — für Initial-Befüllung
    /// und den "Standard übernehmen"-Reset.</summary>
    public void WerteSetzenOhneAbweichung(int von, int bis)
    {
        _populiertGerade = true;
        try { PlatzierungVon = von; PlatzierungBis = bis; IstAbweichend = false; }
        finally { _populiertGerade = false; }
    }
}

/// <summary>Welche Seite einer <see cref="Uebergangsregel"/> im Runden-Editor fest (die aktuell bearbeitete
/// Runde) und welche wählbar ist (Issue #186) — <see cref="Eingehend"/>: Zielrunde fest, Quellrunde wählbar
/// (bisheriges Verhalten seit #169). <see cref="Ausgehend"/>: Quellrunde fest, Zielrunde wählbar (Spiegelfall).</summary>
public enum UebergangsregelRichtung { Eingehend, Ausgehend }

/// <summary>Ein UI-Block im "Eingehende"- oder "Ausgehende Übergangsregeln"-Abschnitt des Runden-Editors —
/// je nach <see cref="Richtung"/> ist entweder die Zielrunde oder die Quellrunde fest (die aktuell bearbeitete
/// Runde), die jeweils andere Seite ist per Dropdown wählbar. Speichert direkt über die bestehenden
/// <see cref="UebergangsregelService"/>-Methoden, unabhängig vom großen Speichern-Knopf der Rundenliste.</summary>
public partial class UebergangsregelBlock : ObservableObject
{
    private readonly UebergangsregelService _service;
    // Issue #153: löst bei Bedarf das Speichern der gesamten Rundenliste aus, bevor Quell- und/oder Zielrunde
    // beim Blockspeichern noch keine echte Id haben (mehrere neue Runden + deren Übergangsregeln in einem Zug).
    private readonly Func<Task<bool>> _sicherstellenRundenGespeichertAsync;

    // Verhindert, dass das initiale Setzen der fixen Seite (QuellRunde bei Ausgehend, ZielRunde bei Eingehend)
    // im Konstruktor OnQuellRundeChanged auslöst und die Zeilen mit Default-Werten vorbefüllt, bevor
    // AusBestehendenRegelnErstellen sie aus bereits gespeicherten Regeln befüllt hat (Issue #186).
    private bool _wirdInitialisiert;

    // Baseline der beim Laden vorgefundenen Zeilen-Ids — Vergleichsbasis, um beim Speichern zu erkennen,
    // welche zuvor gespeicherten Zeilen inzwischen aus der Zeilen-Collection entfernt wurden (z.B. über
    // "Aktualisieren", Issue #183) und dementsprechend gelöscht werden müssen (Issue #180).
    private readonly HashSet<int> _urspruenglicheZeilenIds = [];

    public event Action? Geloescht;
    public event Action? QuellRundeAusgangGeaendert; // Issue #153
    public event Action? Gespeichert; // Issue #186-Folgefund: löst bei Eingehend-Blöcken die Eingang-Ableitung aus

    public UebergangsregelRichtung Richtung { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstLiga))]
    [NotifyPropertyChangedFor(nameof(IstKoSonderfall))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(NeueGruppenuebergreifendeZeileCommand))]
    private RundenEintrag? _quellRunde;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private RundenEintrag? _zielRunde;

    [ObservableProperty] private ObservableCollection<UebergangsregelZeile> _zeilen = [];

    // Gruppenübergreifende Rangfolge-Regeln (Issue #184) - bewusst eine separate Collection statt Teil von
    // Zeilen: die bestehende Pro-Gruppe-Sync-Logik (ErwarteteGruppenIndizes/PasstZurQuellrunde/Aktualisieren)
    // arbeitet ausschließlich mit Zeilen und darf davon unberührt bleiben, da diese Zeilen nicht 1:1 einer
    // Gruppe zugeordnet sind.
    [ObservableProperty] private ObservableCollection<UebergangsregelZeile> _gruppenuebergreifendeZeilen = [];

    [ObservableProperty] private int _standardPlatzierungVon = 1;
    [ObservableProperty] private int _standardPlatzierungBis = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private bool _isDirty;

    [ObservableProperty] private string? _fehlermeldung;

    public bool IstNeu => Zeilen.Count == 0 || Zeilen.All(z => z.Id is null);
    public bool IstGruppenphase => QuellRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool IstLiga => QuellRunde?.Rundenart == Rundenart.Liga;
    public bool IstKoSonderfall => QuellRunde is not null && !IstGruppenphase && !IstLiga;

    /// <summary>Erstellt einen neuen, leeren Block. <paramref name="fixierteRunde"/> ist die aktuell im
    /// Runden-Editor bearbeitete Runde — je nach <paramref name="richtung"/> wird sie als Ziel- oder Quellrunde
    /// fest gesetzt, die jeweils andere Seite bleibt zunächst leer und wird per Dropdown gewählt.</summary>
    public UebergangsregelBlock(
        UebergangsregelService service, UebergangsregelRichtung richtung, RundenEintrag fixierteRunde,
        Func<Task<bool>> sicherstellenRundenGespeichertAsync)
    {
        _service = service;
        Richtung = richtung;
        _sicherstellenRundenGespeichertAsync = sicherstellenRundenGespeichertAsync;

        _wirdInitialisiert = true;
        try
        {
            if (richtung == UebergangsregelRichtung.Eingehend) ZielRunde = fixierteRunde;
            else QuellRunde = fixierteRunde;
        }
        finally { _wirdInitialisiert = false; }
    }

    /// <summary>Baut einen Block aus bereits gespeicherten Regeln auf (Zeilen zuerst befüllen, dann die
    /// wählbare Seite setzen — verhindert, dass <see cref="OnQuellRundeChanged"/> die Zeilen fälschlich mit
    /// Default-Werten überschreibt, da dessen Auto-Befüllung nur bei leerer Zeilenliste greift).</summary>
    public static UebergangsregelBlock AusBestehendenRegelnErstellen(
        UebergangsregelService service, UebergangsregelRichtung richtung,
        RundenEintrag quellRunde, RundenEintrag zielRunde,
        IReadOnlyList<Uebergangsregel> regeln, Func<Task<bool>> sicherstellenRundenGespeichertAsync)
    {
        var fixierteRunde = richtung == UebergangsregelRichtung.Eingehend ? zielRunde : quellRunde;
        var block = new UebergangsregelBlock(service, richtung, fixierteRunde, sicherstellenRundenGespeichertAsync);
        foreach (var regel in regeln.OrderBy(r => r.GruppeIndex ?? 0))
        {
            var zeile = new UebergangsregelZeile { GruppeIndex = regel.GruppeIndex, AnzahlBeste = regel.AnzahlBeste, Id = regel.Id };
            zeile.WerteSetzenOhneAbweichung(regel.PlatzierungVon, regel.PlatzierungBis);
            (regel.AnzahlBeste is null ? block.Zeilen : block.GruppenuebergreifendeZeilen).Add(zeile);
            block._urspruenglicheZeilenIds.Add(regel.Id);
        }
        if (richtung == UebergangsregelRichtung.Eingehend) block.QuellRunde = quellRunde;
        else block.ZielRunde = zielRunde;
        foreach (var z in block.Zeilen.Concat(block.GruppenuebergreifendeZeilen))
            z.PropertyChanged += (_, __) => block.IsDirty = true;
        return block;
    }

    /// <summary>Fügt eine neue gruppenübergreifende Rangfolge-Regel-Zeile hinzu (Issue #184), nur sinnvoll bei
    /// einer Gruppenphase als Quellrunde. Default "Platz 3, beste 1" - der EM/WM-Klassiker als Startpunkt.</summary>
    [RelayCommand(CanExecute = nameof(KannGruppenuebergreifendeZeileHinzufuegen))]
    private void NeueGruppenuebergreifendeZeile()
    {
        var zeile = new UebergangsregelZeile { GruppeIndex = null, AnzahlBeste = 1 };
        zeile.WerteSetzenOhneAbweichung(3, 3);
        zeile.PropertyChanged += (_, __) => IsDirty = true;
        GruppenuebergreifendeZeilen.Add(zeile);
        IsDirty = true;
    }
    private bool KannGruppenuebergreifendeZeileHinzufuegen() => QuellRunde?.Rundenart == Rundenart.Gruppenphase;

    [RelayCommand]
    private void GruppenuebergreifendeZeileEntfernen(UebergangsregelZeile zeile)
    {
        GruppenuebergreifendeZeilen.Remove(zeile);
        IsDirty = true;
    }

    partial void OnQuellRundeChanged(RundenEintrag? value)
    {
        if (_wirdInitialisiert || value is null || Zeilen.Count > 0) return;

        if (value.Rundenart == Rundenart.Gruppenphase && value.GruppenAnzahl is > 0)
        {
            // Sinnvoller Startwert statt hartkodiert 1-1: "wie viele qualifizieren im Schnitt pro Gruppe" —
            // z.B. Ausgang 8 bei 4 Gruppen → Platz 1-2. Bleibt eine reine Vorbelegung, die pro Zeile
            // überschrieben werden kann.
            StandardPlatzierungVon = 1;
            StandardPlatzierungBis = Math.Max(1, value.AusgangsMannschaften / value.GruppenAnzahl.Value);

            for (int i = 1; i <= value.GruppenAnzahl.Value; i++)
            {
                var zeile = new UebergangsregelZeile { GruppeIndex = i };
                zeile.WerteSetzenOhneAbweichung(StandardPlatzierungVon, StandardPlatzierungBis);
                Zeilen.Add(zeile);
            }
        }
        else if (value.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            // Eine Zeile je Spiel der Quellrunde (bisher wurde hier immer nur 1 Zeile erzeugt, Issue #183).
            int anzahlSpiele = Math.Max(1, value.EingangsMannschaften / 2);
            for (int i = 1; i <= anzahlSpiele; i++)
            {
                var zeile = new UebergangsregelZeile { GruppeIndex = i };
                zeile.WerteSetzenOhneAbweichung(StandardPlatzierungVon, StandardPlatzierungBis);
                Zeilen.Add(zeile);
            }
        }
        else
        {
            var zeile = new UebergangsregelZeile { GruppeIndex = value.Rundenart == Rundenart.Liga ? null : 1 };
            zeile.WerteSetzenOhneAbweichung(StandardPlatzierungVon, StandardPlatzierungBis);
            Zeilen.Add(zeile);
        }

        foreach (var z in Zeilen)
            z.PropertyChanged += (_, __) => IsDirty = true;
        IsDirty = true;
        OnPropertyChanged(nameof(IstNeu));
    }

    /// <summary>Die Gruppenindizes/Spielnummern, die die aktuelle Quellrunde in ihrer jetzigen Form erwarten
    /// lässt — Vergleichsbasis für <see cref="PasstZurQuellrunde"/> und <see cref="Aktualisieren"/> (Issue #183).</summary>
    private IEnumerable<int?> ErwarteteGruppenIndizes()
    {
        if (QuellRunde is null) return [];
        if (QuellRunde.Rundenart == Rundenart.Gruppenphase && QuellRunde.GruppenAnzahl is > 0)
            return Enumerable.Range(1, QuellRunde.GruppenAnzahl.Value).Cast<int?>();
        if (QuellRunde.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
            return Enumerable.Range(1, Math.Max(1, QuellRunde.EingangsMannschaften / 2)).Cast<int?>();
        return [null];
    }

    /// <summary>False, wenn die Quellrunde inzwischen mehr oder weniger Gruppen/Spiele hat, als dieser Block
    /// Zeilen besitzt (z.B. weil die Quellrunde nach dem Anlegen dieser Übergangsregeln geändert wurde).</summary>
    public bool PasstZurQuellrunde =>
        QuellRunde is null || ErwarteteGruppenIndizes().ToHashSet().SetEquals(Zeilen.Select(z => z.GruppeIndex));

    /// <summary>Gleicht die Zeilen automatisch an die aktuelle Form der Quellrunde an: fehlende Gruppen/Spiele
    /// werden ergänzt, überzählige entfernt. Löscht dabei noch nichts in der DB — das übernimmt der Diff beim
    /// nächsten Klick auf "Speichern" (Issue #183).</summary>
    [RelayCommand(CanExecute = nameof(KannAktualisieren))]
    private void Aktualisieren()
    {
        if (QuellRunde is null) return;
        var erwartet = ErwarteteGruppenIndizes().ToList();

        foreach (var idx in erwartet.Where(i => Zeilen.All(z => z.GruppeIndex != i)))
        {
            var zeile = new UebergangsregelZeile { GruppeIndex = idx };
            zeile.WerteSetzenOhneAbweichung(StandardPlatzierungVon, StandardPlatzierungBis);
            zeile.PropertyChanged += (_, __) => IsDirty = true;
            Zeilen.Add(zeile);
        }
        foreach (var zeile in Zeilen.Where(z => !erwartet.Contains(z.GruppeIndex)).ToList())
            Zeilen.Remove(zeile);

        IsDirty = true;
        OnPropertyChanged(nameof(PasstZurQuellrunde));
        AktualisierenCommand.NotifyCanExecuteChanged();
    }
    private bool KannAktualisieren() => QuellRunde is not null && !PasstZurQuellrunde;

    [RelayCommand]
    private void StandardAnwenden()
    {
        foreach (var z in Zeilen.Where(z => !z.IstAbweichend))
            z.WerteSetzenOhneAbweichung(StandardPlatzierungVon, StandardPlatzierungBis);
        IsDirty = true;
    }

    [RelayCommand]
    private void ZeileZuruecksetzen(UebergangsregelZeile zeile)
    {
        zeile.WerteSetzenOhneAbweichung(StandardPlatzierungVon, StandardPlatzierungBis);
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (QuellRunde is null || ZielRunde is null) return;
        Fehlermeldung = null;
        try
        {
            if (QuellRunde.Id <= 0 || ZielRunde.Id <= 0)
            {
                var gespeichert = await _sicherstellenRundenGespeichertAsync();
                if (!gespeichert)
                {
                    Fehlermeldung = "Die Rundenliste konnte nicht automatisch gespeichert werden — siehe Meldung oben.";
                    return;
                }
            }

            var alleZeilen = Zeilen.Concat(GruppenuebergreifendeZeilen).ToList();
            var zeilenEingaben = alleZeilen
                .Select(z => new UebergangsregelZeileEingabe(z.Id, z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste))
                .ToList();
            var zuLoeschendeIds = _urspruenglicheZeilenIds
                .Except(alleZeilen.Where(z => z.Id.HasValue).Select(z => z.Id!.Value))
                .ToList();

            var ergebnis = await _service.BlockSpeichernAsync(QuellRunde.Id, ZielRunde.Id, zeilenEingaben, zuLoeschendeIds);

            for (int i = 0; i < alleZeilen.Count && i < ergebnis.Count; i++)
                alleZeilen[i].Id = ergebnis[i].Id;

            _urspruenglicheZeilenIds.Clear();
            foreach (var id in alleZeilen.Where(z => z.Id.HasValue).Select(z => z.Id!.Value))
                _urspruenglicheZeilenIds.Add(id);

            // Issue #153: Ausgang der Quellrunde direkt im geteilten RundenEintrag-Objekt aktualisieren
            // (Objektidentität, gleiches Muster wie die Id-Rückschreibung in RundenEditorViewModel.Speichern) —
            // ohne DB-Roundtrip zeigt die Rundenliste sofort den korrekten Wert. K.o.-Rundenarten ausgenommen
            // (Issue #186): deren Ausgang bleibt immer Eingang/2 (Issue #182) und kann sich strukturell auf
            // zwei Zielrunden aufteilen (Sieger/Verlierer) — die Summe eines einzelnen Blocks wäre hier falsch.
            var neuerAusgang = Zeilen.Sum(z => z.PlatzierungBis - z.PlatzierungVon + 1)
                + GruppenuebergreifendeZeilen.Sum(z => z.AnzahlBeste ?? 0);
            if (QuellRunde.AusgangsMannschaften != neuerAusgang && !IstKoRundenartVon(QuellRunde.Rundenart))
            {
                QuellRunde.AusgangsMannschaften = neuerAusgang;
                QuellRundeAusgangGeaendert?.Invoke();
            }

            IsDirty = false;
            OnPropertyChanged(nameof(IstNeu));
            Gespeichert?.Invoke();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
    private bool KannSpeichern() => IsDirty && QuellRunde is not null && ZielRunde is not null;

    private static bool IstKoRundenartVon(Rundenart art) =>
        art is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    [RelayCommand]
    private async Task Loeschen()
    {
        Fehlermeldung = null;
        try
        {
            foreach (var z in Zeilen.Concat(GruppenuebergreifendeZeilen).Where(z => z.Id is not null))
                await _service.LoeschenAsync(z.Id!.Value);
            Geloescht?.Invoke();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
}

public partial class RundenEditorViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _service;
    private readonly RegelwerkService _regelwerkService;
    private readonly TurnierVorlageService _vorlageService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;
    private int _populiertGerade;

    // Issue #166: aus einer Vorlage übernommene Übergangsregel-Struktur, per Objektreferenz auf die geladenen
    // RundenEintrag-Zeilen (statt Index) — bleibt so auch bei Umsortierung/Löschen einzelner Zeilen konsistent.
    // Wird erst beim nächsten Speichern in echte Uebergangsregeln zwischen den persistierten Runden übersetzt.
    private List<(RundenEintrag Quelle, RundenEintrag Ziel, int? GruppeIndex, int PlatzierungVon, int PlatzierungBis, int? AnzahlBeste)>
        _vorlagenUebergangsregelnPending = [];

    // Einmal je Instanz-Laden geholt (Issue #188) — Grundlage für die live berechneten RundenWarnungen,
    // erspart einen Service-Roundtrip bei jedem Tastendruck.
    private IReadOnlyList<Uebergangsregel> _alleUebergangsregelnFuerWarnung = [];

    // Preserved turnier metadata for VollAktualisierenAsync on save
    private string _instanzName = "";
    private string _instanzSaison = "";
    private DateOnly? _instanzStart;
    private DateOnly? _instanzEnd;
    private TurnierStatus _instanzStatus = TurnierStatus.Geplant;
    private int? _vorlageId;
    private int? _instanzVerbandId;
    private int? _instanzLandId;
    private MannschaftsartFilter _instanzTeamFilter = MannschaftsartFilter.Alle;

    private static readonly RegelwerkOption _turnierStandard = new("Turnierstandard", null);

    // ── Linke Spalte: Turnierliste (Geplant) ──────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<TurnierInstanz> _geplanteTurniere = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RundenEditorAktiv))]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    [NotifyCanExecuteChangedFor(nameof(NeueRundeCommand))]
    [NotifyCanExecuteChangedFor(nameof(VorlageRundenLadenCommand))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    // ── Mittlere Spalte: Vorlagenauswahl + Rundenliste ─────────────────────────
    [ObservableProperty]
    private ObservableCollection<TurnierVorlage> _turnierVorlagen = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VorlageRundenLadenCommand))]
    private TurnierVorlage? _ausgewaehlteVorlagenquelle;

    [ObservableProperty]
    private ObservableCollection<RundenEintrag> _runden = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeHochCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeRunterCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyCanExecuteChangedFor(nameof(NeuerUebergangsregelBlockCommand))]
    [NotifyCanExecuteChangedFor(nameof(NeuerAusgehenderUebergangsregelBlockCommand))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    [NotifyPropertyChangedFor(nameof(GruppenAnzahlSichtbar))]
    [NotifyPropertyChangedFor(nameof(EingehendeUebergangsregelnSichtbar))]
    [NotifyPropertyChangedFor(nameof(FrueherRundenOptionen))]
    [NotifyPropertyChangedFor(nameof(SpaeterRundenOptionen))]
    private RundenEintrag? _ausgewaehlteRunde;

    // ── Eingehende/Ausgehende Übergangsregeln (Issue #169, #186) ───────────────
    [ObservableProperty]
    private ObservableCollection<UebergangsregelBlock> _eingehendeUebergangsregelBloecke = [];

    [ObservableProperty]
    private ObservableCollection<UebergangsregelBlock> _ausgehendeUebergangsregelBloecke = [];

    // Jede Neuzuweisung der Collection (Laden/Zurücksetzen) enthält ggf. andere Blöcke mit anderen
    // Fehlermeldungen als zuvor — Issue #188-Folgefeedback.
    partial void OnEingehendeUebergangsregelBloeckeChanged(ObservableCollection<UebergangsregelBlock> value) => AktualisiereBlockFehlermeldungen();
    partial void OnAusgehendeUebergangsregelBloeckeChanged(ObservableCollection<UebergangsregelBlock> value) => AktualisiereBlockFehlermeldungen();

    // ── Rechte Spalte: Detailformular ─────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GruppenAnzahlSichtbar))]
    [NotifyPropertyChangedFor(nameof(IstKoRundenart))]
    [NotifyPropertyChangedFor(nameof(AusgangEditierbar))]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    [NotifyPropertyChangedFor(nameof(MitRueckspielVerfuegbar))]
    private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private string _eingang = "4";
    [ObservableProperty] private string _ausgang = "4";
    [ObservableProperty] private string _gruppenAnzahl = "";
    [ObservableProperty] private RegelwerkOption _regelwerk = _turnierStandard;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EingangEditierbar))]
    private bool _eingangUeberUebergangsregeln;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AusgangEditierbar))]
    [NotifyPropertyChangedFor(nameof(ZeigeUebergangsregelnAusgangHinweis))]
    private bool _hatAusgehendeUebergangsregeln;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EingangEditierbar))]
    [NotifyPropertyChangedFor(nameof(ZeigeUebergangsregelnEingangHinweis))]
    [NotifyPropertyChangedFor(nameof(ZeigeEingangUeberUebergangsregelnCheckbox))]
    private bool _hatEingehendeUebergangsregeln;

    // ── Regelwerke ────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private IReadOnlyList<Regelwerk> _regelwerke = [];

    // StandardRegelwerk der Instanz — nicht im View, nur für Auto-Fill neuer Runden und Speichern
    private int? _instanzStandardRegelwerkId;
    private Regelwerk? _instanzStandardRegelwerk;

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [_turnierStandard, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public IReadOnlyList<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>().ToList();

    // ── Zustand ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty] private bool _istLadend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    private string? _fehlermeldung;

    // ── Validierung ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRundenFehler))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _rundenFehler = [];

    public bool HatRundenFehler => RundenFehler.Count > 0;

    /// <summary>Live-Warnungen (Issue #188), kein Speichern-Blocker anders als <see cref="RundenFehler"/>:
    /// eine bereits gespeicherte Runde, die als Quellrunde für bestehende Übergangsregeln dient, hat ihre
    /// Eingangs-/Ausgangs-/Gruppenzahl seit dem Laden geändert. Ersetzt die frühere Speichervorgang-MessageBox
    /// (Issue #181) — passend zu den übrigen bereits vor dem Speichern sichtbaren Validierungen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRundenWarnungen))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    private IReadOnlyList<string> _rundenWarnungen = [];

    public bool HatRundenWarnungen => RundenWarnungen.Count > 0;

    /// <summary>Fehlermeldungen einzelner Übergangsregel-Blöcke (Issue #188-Folgefeedback, z.B. die
    /// UebergangsregelValidator-Meldung "Übergangsregeln für 'X' liefern zusammen Y Mannschaften, erwartet
    /// werden aber Z") — bisher nur inline im jeweiligen Block sichtbar, bei vielen Blöcken (z.B. viele
    /// Gruppen der Vorgängerrunde) nur durch Scrollen erreichbar. Mit Quell-/Zielrunde präfixiert, da in der
    /// Spalte der Blockkontext sonst verloren ginge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatBlockFehlermeldungen))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    private IReadOnlyList<string> _blockFehlermeldungen = [];

    public bool HatBlockFehlermeldungen => BlockFehlermeldungen.Count > 0;

    /// <summary>Steuert die rechte Meldungs-Spalte (Issue #188-Folgefeedback) — sammelt Fehlermeldung,
    /// Runden-Konsistenzfehler, Übergangsregel-Warnungen und Block-Fehlermeldungen dort, analog zu den
    /// Verband-Quoten in der Auslosung, damit sie bei vielen Runden/Gruppen nicht erst durch Scrollen
    /// sichtbar werden.</summary>
    public bool ZeigeMeldungsSpalte => Fehlermeldung is not null || HatRundenFehler || HatRundenWarnungen || HatBlockFehlermeldungen;

    private void AktualisiereBlockFehlermeldungen() => BlockFehlermeldungen =
        EingehendeUebergangsregelBloecke.Concat(AusgehendeUebergangsregelBloecke)
            .Where(b => b.Fehlermeldung is not null)
            .Select(b => $"{b.QuellRunde?.Name} → {b.ZielRunde?.Name}: {b.Fehlermeldung}")
            .ToList();

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool IstKoRundenart =>
        Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    // KoRundeGenerator bildet strikt n/2-Paarungen — Ausgang ist bei K.-o.-Rundenarten kein freier Wert,
    // sondern immer aus Eingang abgeleitet (Issue #182). Ausgang wird zusätzlich automatisch berechnet, sobald
    // die Runde bereits mindestens eine ausgehende Übergangsregel hat (Issue #153) — K.o.-Rundenarten haben
    // dabei weiterhin Vorrang.
    public bool AusgangEditierbar => !IstKoRundenart && !HatAusgehendeUebergangsregeln;
    public bool ZeigeUebergangsregelnAusgangHinweis => HatAusgehendeUebergangsregeln && !IstKoRundenart;

    // Issue #186-Folgefund: an die Checkbox gekoppelt statt direkt an HatEingehendeUebergangsregeln — die
    // Checkbox wird automatisch mitgesetzt, sobald eingehende Übergangsregeln existieren (siehe
    // EingangAusRegelnAbleiten), lässt sich aber auch unabhängig davon manuell aktivieren (Issue #178), und
    // soll auch dann bereits sperren, bevor eine konkrete Regel gespeichert ist. Anders als Ausgang keine
    // K.o.-Ausnahme nötig — Eingang ist bei keiner Rundenart durch eine feste Formel vorgegeben.
    public bool EingangEditierbar => !EingangUeberUebergangsregeln;
    public bool ZeigeUebergangsregelnEingangHinweis => HatEingehendeUebergangsregeln;
    public bool ZeigeEingangUeberUebergangsregelnCheckbox => !HatEingehendeUebergangsregeln;

    public bool MitRueckspielVerfuegbar =>
        Rundenart is Rundenart.KoRunde or Rundenart.Liga or Rundenart.Sonderrunde or Rundenart.Gruppenphase;

    public bool AuswaertstorAktivierbar => MitRueckspiel && IstKoRundenart;
    public bool RundenEditorAktiv       => AusgewaehlteInstanz is not null;
    public bool KeinTurnierGewaehlt     => AusgewaehlteInstanz is null;
    public bool FormularSichtbar        => AusgewaehlteInstanz is not null && AusgewaehlteRunde is not null;
    public bool GruppenAnzahlSichtbar   => FormularSichtbar && Rundenart == Rundenart.Gruppenphase;

    // Seit Issue #153 unabhängig davon nutzbar, ob die Zielrunde bereits eine echte Id hat — das eigentliche
    // Schreiben in die DB verzögert UebergangsregelBlock.Speichern() selbst bis Quell- UND Zielrunde persistiert
    // sind (siehe SicherstellenRundenGespeichertAsync).
    public bool EingehendeUebergangsregelnSichtbar => FormularSichtbar;
    public bool AusgehendeUebergangsregelnSichtbar => FormularSichtbar; // Issue #186

    // Seit Issue #153 dürfen auch noch nicht persistierte, in derselben Sitzung neu angelegte Runden als
    // Quellrunde gewählt werden (Chicken-and-egg-Fix für mehrere neue Runden + deren Übergangsregeln in einem
    // Zug). Die Listenposition allein (nicht mehr die Id) verhindert Zyklen/Vorwärtsreferenzen strukturell.
    public IEnumerable<RundenEintrag> FrueherRundenOptionen =>
        AusgewaehlteRunde is null
            ? []
            : Runden.Where(r => Runden.IndexOf(r) < Runden.IndexOf(AusgewaehlteRunde));

    // Spiegel von FrueherRundenOptionen für den "Ausgehende Übergangsregeln"-Abschnitt (Issue #186) — Zielrunden
    // liegen unter derselben Positions-Ordnung immer nach der aktuell bearbeiteten Runde.
    public IEnumerable<RundenEintrag> SpaeterRundenOptionen =>
        AusgewaehlteRunde is null
            ? []
            : Runden.Where(r => Runden.IndexOf(r) > Runden.IndexOf(AusgewaehlteRunde));

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel  => "Runden";
    public ICommand NeuBefehl      => NeueRundeCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => RundeEntfernenCommand;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Runden",
            Aktionen =
            [
                new RibbonAktion
                {
                    Header = "Runde nach oben",
                    Befehl = RundeHochCommand,
                    IconGlyph = "",
                },
                new RibbonAktion
                {
                    Header = "Runde nach unten",
                    Befehl = RundeRunterCommand,
                    IconGlyph = "",
                },
            ],
        },
    ];

    public RundenEditorViewModel(
        TurnierInstanzService service,
        RegelwerkService regelwerkService,
        TurnierVorlageService vorlageService,
        UebergangsregelService uebergangsregelService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _service = service;
        _regelwerkService = regelwerkService;
        _vorlageService = vorlageService;
        _uebergangsregelService = uebergangsregelService;
        _turnierAuswahlService = turnierAuswahlService;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var alleInstanzen = await _service.AlleAbrufenAsync();
            var regelwerke    = await _regelwerkService.AlleAbrufenAsync();
            var vorlagen      = await _vorlageService.AlleAbrufenAsync();

            GeplanteTurniere = new ObservableCollection<TurnierInstanz>(
                alleInstanzen
                    .Where(i => i.Status == TurnierStatus.Geplant)
                    .OrderByDescending(i => i.SaisonOderJahr)
                    .ThenBy(i => i.Name));
            Regelwerke       = regelwerke.ToList();
            TurnierVorlagen  = new ObservableCollection<TurnierVorlage>(vorlagen.OrderBy(v => v.Name));

            var neueInstanz = GeplanteTurniere.FirstOrDefault(i => i.Id == _turnierAuswahlService.TurnierInstanzId);

            // Bei erneuter Aktivierung (Tab-Wechsel innerhalb desselben Turnierdefinition-Composite, ohne
            // Wechsel der Navigation-Scope-DbContext) liefert AlleAbrufenAsync dank EF-Identity-Resolution
            // dieselbe Objektreferenz wie zuvor — OnAusgewaehlteInstanzChanged würde dann nicht auslösen und
            // die Runden (inkl. der darin eingebetteten Übergangsregeln) blieben veraltet.
            if (ReferenceEquals(neueInstanz, AusgewaehlteInstanz))
            {
                // Auswahl über den erzwungenen Reload hinweg erhalten, damit ein bloßer Tab-Wechsel
                // (anders als ein echter Instanzwechsel) nicht die gerade bearbeitete Runde abwählt.
                var vorherigeRundeId = AusgewaehlteRunde?.Id;
                await LadeRundenFuerInstanzAsync(neueInstanz);
                if (vorherigeRundeId is > 0)
                    AusgewaehlteRunde = Runden.FirstOrDefault(r => r.Id == vorherigeRundeId);
            }
            else
            {
                AusgewaehlteInstanz = neueInstanz;
            }
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Runden-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeueRunde))]
    private void NeueRunde()
    {
        var letzte  = Runden.LastOrDefault();
        var eingang = letzte?.AusgangsMannschaften ?? 4;
        // Neue Runde ist standardmäßig Rundenart.Liga (kein K.o.) — dort ist Ausgang ein frei wählbarer
        // Wert, sinnvoll vorbelegt mit Eingang statt der nur für K.o.-Rundenarten geltenden Eingang/2-Regel
        // (Issue #209, Issue #182).
        var eintrag = new RundenEintrag
        {
            EingangsMannschaften = eingang,
            AusgangsMannschaften = eingang,
            RegelwerkId          = 0,  // 0 = Turnierstandard (wird beim Speichern aufgelöst)
            RegelwerkName        = ""
        };
        Runden.Add(eintrag);
        AusgewaehlteRunde = eintrag;
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var entfernt = AusgewaehlteRunde;
        var idx = Runden.IndexOf(entfernt);
        Runden.Remove(entfernt);
        AusgewaehlteRunde = Runden.Count > 0 ? Runden[Math.Max(0, idx - 1)] : null;

        // Issue #166: eine noch nicht materialisierte Vorlagen-Übergangsregel auf die entfernte Runde
        // würde sonst beim nächsten Speichern gegen eine nicht mehr existierende Zeile aufgelöst.
        if (_vorlagenUebergangsregelnPending.Count > 0)
            _vorlagenUebergangsregelnPending.RemoveAll(p => p.Quelle == entfernt || p.Ziel == entfernt);

        AktualisiereRundenFehler();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannHoch))]
    private void RundeHoch()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        if (idx <= 0) return;
        Runden.Move(idx, idx - 1);
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRunter))]
    private void RundeRunter()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        if (idx < 0 || idx >= Runden.Count - 1) return;
        Runden.Move(idx, idx + 1);
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    // ── Vorlage laden ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannVorladeRundenLaden))]
    private async Task VorlageRundenLaden()
    {
        if (AusgewaehlteVorlagenquelle is null) return;
        IstLadend = true;
        Fehlermeldung = null;
        try
        {
            var vorlage = await _vorlageService.FindenNachIdMitRundenAsync(AusgewaehlteVorlagenquelle.Id);
            if (vorlage is null) return;
            var vorlagenRunden = vorlage.VorlagenRunden.OrderBy(r => r.Reihenfolge).ToList();
            var runden = vorlagenRunden
                .Select(r => new RundenEintrag
                {
                    Name                   = r.Name,
                    Rundenart              = r.Rundenart,
                    EingangsMannschaften   = r.EingangsMannschaftsanzahl,
                    AusgangsMannschaften   = r.AusgangsMannschaftsanzahl,
                    GruppenAnzahl          = r.GruppenAnzahl,
                    RegelwerkId            = r.RegelwerkId ?? 0,
                    RegelwerkName          = r.Regelwerk?.Name
                                            ?? RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == r.RegelwerkId)?.Wert?.Name
                                            ?? "",
                    MitRueckspiel          = r.MitRueckspiel,
                    AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv
                })
                .ToList();
            RundenZuruecksetzen(runden);
            _vorlageId = AusgewaehlteVorlagenquelle.Id;

            // Issue #166: Übergangsstruktur der Vorlage merken (per Objektreferenz auf die geladenen
            // RundenEintrag-Zeilen), wird beim nächsten Speichern in echte Uebergangsregeln übersetzt.
            var eintragNachVorlagenRundeId = vorlagenRunden.Zip(runden, (r, e) => (r.Id, e))
                .ToDictionary(p => p.Id, p => p.e);
            _vorlagenUebergangsregelnPending = vorlagenRunden
                .SelectMany(r => r.AusgehendeUebergangsregeln)
                .Where(regel => eintragNachVorlagenRundeId.ContainsKey(regel.ZielVorlagenRundeId))
                .Select(regel => (
                    Quelle: eintragNachVorlagenRundeId[regel.QuellVorlagenRundeId],
                    Ziel: eintragNachVorlagenRundeId[regel.ZielVorlagenRundeId],
                    regel.GruppeIndex, regel.PlatzierungVon, regel.PlatzierungBis, regel.AnzahlBeste))
                .ToList();

            IsDirty = true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AusgewaehlteInstanz is null) return;

        Fehlermeldung = null;
        try
        {
            var vollEingabe = new TurnierInstanzVollAktualisierenEingabe(
                _instanzName, _instanzSaison, _vorlageId, _instanzStart, _instanzEnd, _instanzStatus,
                _instanzVerbandId, _instanzLandId, _instanzTeamFilter,
                StandardRegelwerkId: _instanzStandardRegelwerkId);
            await _service.VollAktualisierenAsync(AusgewaehlteInstanz.Id, vollEingabe);
            var persistiert = await _service.RundenErsetzenAsync(AusgewaehlteInstanz.Id, RundenListeZuEingaben());

            // Ids der (ggf. neu angelegten) Runden zurückschreiben — sonst bleibt z.B. "Weitere Quellrunde"
            // für eine gerade erst gespeicherte Runde bis zum nächsten kompletten Neuladen deaktiviert.
            for (int i = 0; i < Runden.Count && i < persistiert.Count; i++)
                Runden[i].Id = persistiert[i].Id;
            OnPropertyChanged(nameof(FrueherRundenOptionen));
            NeuerUebergangsregelBlockCommand.NotifyCanExecuteChanged();

            // Issue #166: aus einer Vorlage übernommene Übergangsstruktur jetzt, da Quell- und Zielrunden
            // reale Ids haben, in echte Uebergangsregeln übersetzen. Einmalig — danach geleert, damit ein
            // zweites Speichern in derselben Sitzung nicht dieselben Regeln erneut anlegt.
            if (_vorlagenUebergangsregelnPending.Count > 0)
            {
                // Folgefix (manueller Test): RundenErsetzenAsync aktualisiert bestehende Runden id-stabil
                // in-place statt sie zu löschen/neu anzulegen (bewusst so, um Auslosungs-Referenzen zu
                // erhalten) — bei erneutem "Vorlage laden" auf eine bereits zuvor materialisierte Runde
                // träfe die Übersetzung sonst auf dieselbe Id und legte die Regeln ein zweites Mal an.
                // Ausgehende Regeln der betroffenen Quellrunden daher zuerst vollständig ersetzen.
                foreach (var quellRundeId in _vorlagenUebergangsregelnPending.Select(p => p.Quelle.Id).Distinct())
                    foreach (var bestehende in await _uebergangsregelService.AlleNachQuellRundeIdAsync(quellRundeId))
                        await _uebergangsregelService.LoeschenAsync(bestehende.Id);

                foreach (var (quelle, ziel, gruppeIndex, von, bis, anzahlBeste) in _vorlagenUebergangsregelnPending)
                    await _uebergangsregelService.ErstellenAsync(
                        new UebergangsregelEingabe(quelle.Id, ziel.Id, gruppeIndex, von, bis, anzahlBeste));
                _vorlagenUebergangsregelnPending = [];
            }

            // Neue Vergleichsbasis für die #181-Änderungserkennung — ein zweites Speichern in derselben
            // Sitzung soll nicht erneut warnen, wenn seither nichts weiter geändert wurde.
            foreach (var r in Runden)
            {
                r.UrspruenglichEingangsMannschaften = r.EingangsMannschaften;
                r.UrspruenglichAusgangsMannschaften = r.AusgangsMannschaften;
                r.UrspruenglichGruppenAnzahl        = r.GruppenAnzahl;
            }
            _alleUebergangsregelnFuerWarnung = await _uebergangsregelService.AlleNachTurnierInstanzIdAsync(AusgewaehlteInstanz.Id);
            AktualisiereRundenWarnungen();

            IsDirty = false;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen()
    {
        await LadeRundenFuerInstanzAsync(AusgewaehlteInstanz);
    }

    private void AktualisiereRundenWarnungen() => RundenWarnungen = BerechneRundenWarnungen();

    /// <summary>Warnt live, wenn eine bereits gespeicherte Runde, die als Quellrunde für bestehende
    /// Übergangsregeln dient, ihre Eingangs-/Ausgangs-/Gruppenzahl seit dem Laden geändert hat
    /// (Issue #181, seit #188 als Vorab-Anzeige statt Speichervorgang-MessageBox).</summary>
    private IReadOnlyList<string> BerechneRundenWarnungen()
    {
        var geaenderte = Runden.Where(r => r.Id > 0 && HatSichGeaendert(r)).ToList();
        if (geaenderte.Count == 0) return [];

        return geaenderte
            .Select(r => (Runde: r, Ziele: _alleUebergangsregelnFuerWarnung.Where(u => u.QuellRundeId == r.Id)
                                                                            .Select(u => u.ZielRunde.Name).Distinct().ToList()))
            .Where(x => x.Ziele.Count > 0)
            .Select(x => $"'{x.Runde.Name}' wird bereits als Quelle für Übergangsregeln verwendet in: " +
                         $"{string.Join(", ", x.Ziele)} — die Änderungen können diese Regeln ungültig machen.")
            .ToList();
    }

    private static bool HatSichGeaendert(RundenEintrag r) =>
        r.EingangsMannschaften != r.UrspruenglichEingangsMannschaften ||
        r.AusgangsMannschaften != r.UrspruenglichAusgangsMannschaften ||
        r.GruppenAnzahl != r.UrspruenglichGruppenAnzahl;

    // ── Reaktive Formular → Eintrag Synchronisation ───────────────────────────

    partial void OnNameChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.Name = value;
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnRundenartChanged(Rundenart value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.Rundenart = value;
        _populiertGerade++;
        try
        {
            // RundenEintrag.OnRundenartChanged may have auto-updated the name — sync to form field
            Name = AusgewaehlteRunde.Name;

            // Wechsel zu einer K.-o.-Rundenart: Ausgang ist kein freier Wert mehr, sondern immer
            // Eingang/2 (Issue #182) — sofort neu berechnen statt auf die nächste Eingang-Änderung zu warten.
            if (IstKoRundenart)
            {
                var ausgang = Math.Max(1, ParseAnzahl(Eingang, 4) / 2);
                AusgewaehlteRunde.AusgangsMannschaften = ausgang;
                Ausgang = ausgang.ToString();
            }
        }
        finally { _populiertGerade--; }
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnMitRueckspielChanged(bool value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.MitRueckspiel = value;
        if (!value || !IstKoRundenart) AuswaertstorRegelAktiv = false;
        IsDirty = true;
    }

    partial void OnEingangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null || !EingangEditierbar) return;
        var eingang = ParseAnzahl(value, 4);
        AusgewaehlteRunde.EingangsMannschaften = eingang;

        // Ausgang = Eingang/2 gilt nur für K.o.-Rundenarten (Issue #182) — bei Liga/Gruppenphase/Sonderrunde
        // ist Ausgang ein frei wählbarer Wert und darf hier nicht überschrieben werden (Issue #209).
        if (IstKoRundenart)
        {
            var ausgang = Math.Max(1, eingang / 2);
            AusgewaehlteRunde.AusgangsMannschaften = ausgang;
            _populiertGerade++;
            try { Ausgang = ausgang.ToString(); }
            finally { _populiertGerade--; }
        }
        AktualisiereRundenFehler();
        AktualisiereRundenWarnungen();
        IsDirty = true;
    }

    partial void OnAusgangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null || !AusgangEditierbar) return;
        AusgewaehlteRunde.AusgangsMannschaften = ParseAnzahl(value, 4);
        AktualisiereRundenFehler();
        AktualisiereRundenWarnungen();
        IsDirty = true;
    }

    partial void OnGruppenAnzahlChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.GruppenAnzahl = string.IsNullOrWhiteSpace(value) ? null : ParseAnzahl(value, 2);
        AktualisiereRundenFehler();
        AktualisiereRundenWarnungen();
        IsDirty = true;
    }

    partial void OnRegelwerkChanged(RegelwerkOption value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.RegelwerkId   = value.Wert?.Id ?? 0;  // 0 = Turnierstandard
        AusgewaehlteRunde.RegelwerkName = value.Wert?.Name ?? "";
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnAuswaertstorRegelAktivChanged(bool value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.AuswaertstorRegelAktiv = value;
        IsDirty = true;
    }

    partial void OnEingangUeberUebergangsregelnChanged(bool value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.EingangUeberUebergangsregeln = value;
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    private void AktualisiereRundenFehler() => RundenFehler = BerechneRundenFehler();

    private IReadOnlyList<string> BerechneRundenFehler()
    {
        if (Runden.Count == 0) return [];

        var fehler = new List<string>();

        for (int i = 0; i < Runden.Count; i++)
        {
            var r = Runden[i];
            bool istLetzte = i == Runden.Count - 1;
            string label = string.IsNullOrWhiteSpace(r.Name) ? $"Runde {i + 1}" : $"\"{r.Name}\"";

            if (string.IsNullOrWhiteSpace(r.Name))
                fehler.Add($"Runde {i + 1}: Name darf nicht leer sein.");

            if (r.RegelwerkId == 0 && _instanzStandardRegelwerkId is null)
                fehler.Add($"{label}: Kein Regelwerk gewählt. Wählen Sie ein konkretes Regelwerk oder setzen Sie ein Standard-Regelwerk im Turnier.");

            if (r.EingangsMannschaften < 2)
                fehler.Add($"{label}: Eingang muss mindestens 2 sein.");

            if (r.AusgangsMannschaften < 1)
                fehler.Add($"{label}: Ausgang muss mindestens 1 sein.");

            if (r.AusgangsMannschaften > r.EingangsMannschaften)
                fehler.Add($"{label}: Ausgang ({r.AusgangsMannschaften}) darf den Eingang ({r.EingangsMannschaften}) nicht überschreiten.");

            bool istKoRundenart = r.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

            if (istKoRundenart && r.EingangsMannschaften % 2 != 0)
                fehler.Add($"{label}: K.o.-Runden benötigen eine gerade Eingangsanzahl (aktuell: {r.EingangsMannschaften}).");
            else if (istKoRundenart && r.AusgangsMannschaften != r.EingangsMannschaften / 2)
                fehler.Add($"{label}: Ausgang muss bei K.o.-Runden genau der Hälfte des Eingangs entsprechen ({r.EingangsMannschaften} / 2 = {r.EingangsMannschaften / 2}, aktuell: {r.AusgangsMannschaften}).");

            if (r.Rundenart == Rundenart.Gruppenphase && (r.GruppenAnzahl == null || r.GruppenAnzahl < 2))
                fehler.Add($"{label}: Gruppenphase erfordert mindestens 2 Gruppen.");

            if (istLetzte && r.AusgangsMannschaften != 1)
                fehler.Add($"{label} (letzte Runde): Ausgang muss 1 sein — ein Turnier hat genau einen Sieger.");

            if (!istLetzte)
            {
                var naechste = Runden[i + 1];
                string naechsteLabel = string.IsNullOrWhiteSpace(naechste.Name)
                    ? $"Runde {i + 2}" : $"\"{naechste.Name}\"";

                if (naechste.EingangUeberUebergangsregeln)
                {
                    // Eingang wird über Übergangsregeln definiert, keine Kettenprüfung.
                }
                else if (i >= 1 && IstKoGeschwisterPaar(r.Rundenart, naechste.Rundenart))
                {
                    // Finale + Spiel um Platz 3 speisen sich beide aus derselben Vorrunde (Issue #178).
                    var vorherige = Runden[i - 1];
                    if (vorherige.AusgangsMannschaften != naechste.EingangsMannschaften)
                    {
                        string vorherigeLabel = string.IsNullOrWhiteSpace(vorherige.Name)
                            ? $"Runde {i}" : $"\"{vorherige.Name}\"";
                        fehler.Add($"Übergang {vorherigeLabel} -> {naechsteLabel}: Ausgang ({vorherige.AusgangsMannschaften}) != Eingang der Folgerunde ({naechste.EingangsMannschaften}).");
                    }
                }
                else if (r.AusgangsMannschaften != naechste.EingangsMannschaften)
                {
                    fehler.Add($"Übergang {label} -> {naechsteLabel}: Ausgang ({r.AusgangsMannschaften}) != Eingang der Folgerunde ({naechste.EingangsMannschaften}).");
                }
            }
        }

        return fehler;
    }

    /// <summary>Finale und Spiel um Platz 3 speisen sich strukturell immer aus derselben Vorrunde
    /// (Sieger- bzw. Verliereranzahl einer K.-o.-Runde sind immer gleich groß) — unabhängig davon,
    /// in welcher Reihenfolge sie in der Rundenliste stehen. Dupliziert TurnierValidator.IstKoGeschwisterPaar
    /// bewusst (WPF-seitiges Client-Duplikat, siehe #178-Plan — kein Refactor zur Konsolidierung).</summary>
    private static bool IstKoGeschwisterPaar(Rundenart a, Rundenart b) =>
        (a == Rundenart.Finale && b == Rundenart.SpielUmPlatzDrei) ||
        (a == Rundenart.SpielUmPlatzDrei && b == Rundenart.Finale);

    private static bool IstKoRundenartVon(Rundenart art) =>
        art is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    partial void OnAusgewaehlteInstanzChanged(TurnierInstanz? value)
    {
        _turnierAuswahlService.TurnierInstanzId = value?.Id;
        _ = LadeRundenFuerInstanzAsync(value);
    }

    private async Task LadeRundenFuerInstanzAsync(TurnierInstanz? instanz)
    {
        if (instanz is null)
        {
            RundenZuruecksetzen([]);
            _alleUebergangsregelnFuerWarnung = [];
            AktualisiereRundenWarnungen();
            return;
        }
        IstLadend = true;
        try
        {
            var detail = await _service.FindenNachIdMitRundenAsync(instanz.Id);
            if (detail is not null)
            {
                _instanzName       = detail.Name;
                _instanzSaison     = detail.SaisonOderJahr;
                _instanzStart      = detail.Startdatum;
                _instanzEnd        = detail.Enddatum;
                _instanzStatus     = detail.Status;
                _vorlageId         = detail.TurnierVorlageId;
                _instanzVerbandId          = detail.VerbandId;
                _instanzLandId             = detail.LandId;
                _instanzTeamFilter         = detail.MannschaftsartFilter;
                _instanzStandardRegelwerkId = detail.StandardRegelwerkId;
                _instanzStandardRegelwerk   = detail.StandardRegelwerk;
            }
            RundenZuruecksetzen(
                (detail?.Runden ?? []).OrderBy(r => r.Reihenfolge).Select(RundeZuEintrag));

            // Grundlage für die live berechneten RundenWarnungen (Issue #188/#181).
            _alleUebergangsregelnFuerWarnung = await _uebergangsregelService.AlleNachTurnierInstanzIdAsync(instanz.Id);
            AktualisiereRundenWarnungen();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    partial void OnAusgewaehlteRundeChanged(RundenEintrag? value)
    {
        if (_populiertGerade > 0) return;
        if (value is null)
        {
            FormularZuruecksetzen();
            EingehendeUebergangsregelBloecke = [];
            AusgehendeUebergangsregelBloecke = [];
            return;
        }
        _populiertGerade++;
        try
        {
            Name                   = value.Name;
            Rundenart              = value.Rundenart;
            Eingang                = value.EingangsMannschaften.ToString();
            Ausgang                = value.AusgangsMannschaften.ToString();
            GruppenAnzahl          = value.GruppenAnzahl?.ToString() ?? "";
            Regelwerk              = RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == value.RegelwerkId)
                                     ?? _turnierStandard;
            MitRueckspiel          = value.MitRueckspiel;
            AuswaertstorRegelAktiv = value.AuswaertstorRegelAktiv;
            EingangUeberUebergangsregeln = value.EingangUeberUebergangsregeln;
        }
        finally { _populiertGerade--; }

        EingehendeUebergangsregelBloecke = [];
        AusgehendeUebergangsregelBloecke = [];
        _ = LadeUebergangsregelnUndAusgangAsync(value);
    }

    /// <summary>Sequenziert alle Übergangsregeln-Ladevorgänge — sie nutzen denselben scope-langlebigen
    /// DbContext, parallel als Fire-and-Forget gestartet führte zu "A second operation was started on this
    /// context before a previous operation completed" (EF Core erlaubt keine parallelen Operationen auf einer
    /// DbContext-Instanz, vgl. Issue #45).</summary>
    private async Task LadeUebergangsregelnUndAusgangAsync(RundenEintrag runde)
    {
        await LadeUebergangsregelnAsync(runde);
        await LadeAusgehendeUebergangsregelnAsync(runde);
        await LadeAusgehendeUebergangsregelBloeckeAsync(runde);
    }

    /// <summary>Issue #153: prüft, ob die ausgewählte Runde bereits ausgehende Übergangsregeln hat (dann wird
    /// Ausgang schreibgeschützt/berechnet statt frei editierbar) und korrigiert einen von der Regel-Summe
    /// abweichenden gespeicherten Wert (z.B. Altdaten) nach Hinweis-Dialog.</summary>
    private async Task LadeAusgehendeUebergangsregelnAsync(RundenEintrag runde)
    {
        // K.o.-Rundenarten leiten Ausgang immer aus Eingang/2 ab (Issue #182) und haben Vorrang vor
        // ausgehenden Übergangsregeln — anders als bei Liga/Gruppenphase kann eine K.o.-Quellrunde in zwei
        // unabhängige Zielrunden verzweigen (Sieger → Finale, Verlierer → Spiel um Platz 3), deren Regel-Summe
        // deshalb nicht dem tatsächlichen Ausgang (nur die Siegeranzahl) entspricht.
        if (runde.Id <= 0 || IstKoRundenartVon(runde.Rundenart)) { HatAusgehendeUebergangsregeln = false; return; }
        try
        {
            var ausgehende = await _uebergangsregelService.AlleNachQuellRundeIdAsync(runde.Id);
            if (AusgewaehlteRunde != runde) return; // Runde inzwischen gewechselt (Muster wie LadeUebergangsregelnAsync)
            HatAusgehendeUebergangsregeln = ausgehende.Count > 0;
            if (!HatAusgehendeUebergangsregeln) return;

            var summe = UebergangsregelService.AusgangAbleiten(ausgehende);
            if (runde.AusgangsMannschaften == summe) return;

            MessageBox.Show(
                $"Der gespeicherte Ausgang von '{runde.Name}' ({runde.AusgangsMannschaften}) weicht von der " +
                $"Summe seiner ausgehenden Übergangsregeln ({summe}) ab und wird korrigiert.",
                "Ausgang korrigiert", MessageBoxButton.OK, MessageBoxImage.Information);

            runde.AusgangsMannschaften = summe;
            _populiertGerade++;
            try { Ausgang = summe.ToString(); }
            finally { _populiertGerade--; }
            IsDirty = true;
            AktualisiereRundenFehler();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    /// <param name="ausAktion">True, wenn dieser Reload gezielt durch das Speichern/Löschen eines eingehenden
    /// Blocks ausgelöst wurde (siehe <see cref="EingehendeRegelnNeuLaden"/>) statt durch einen normalen
    /// Rundenwechsel — nur dann darf <see cref="EingangAusRegelnAbleiten"/> die Checkbox "Eingang wird über
    /// Übergangsregeln definiert" bei fehlenden Regeln automatisch zurücksetzen (Issue #186-Folgefund).</param>
    private async Task LadeUebergangsregelnAsync(RundenEintrag runde, bool ausAktion = false)
    {
        if (runde.Id <= 0) return;
        try
        {
            var alleRegeln = await _uebergangsregelService.AlleNachZielRundeIdAsync(runde.Id);
            var bloecke = new List<UebergangsregelBlock>();
            foreach (var gruppe in alleRegeln.GroupBy(r => r.QuellRundeId))
            {
                var quellRunde = Runden.FirstOrDefault(r => r.Id == gruppe.Key);
                if (quellRunde is null) continue;
                var block = UebergangsregelBlock.AusBestehendenRegelnErstellen(
                    _uebergangsregelService, UebergangsregelRichtung.Eingehend, quellRunde, runde,
                    gruppe.ToList(), SicherstellenRundenGespeichertAsync);
                VerdrahteBlockEvents(block);
                bloecke.Add(block);
            }
            // Falls die Runde inzwischen gewechselt wurde, während der Ladevorgang lief, nicht mehr anwenden.
            if (AusgewaehlteRunde != runde) return;
            EingehendeUebergangsregelBloecke = new ObservableCollection<UebergangsregelBlock>(bloecke);
            EingangAusRegelnAbleiten(runde, alleRegeln, ausAktion);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    /// <summary>Issue #186-Folgefund, Spiegel von <see cref="LadeAusgehendeUebergangsregelnAsync"/> für die
    /// Eingangsseite: prüft, ob eingehende Übergangsregeln existieren (dann wird Eingang schreibgeschützt/
    /// berechnet statt frei editierbar), setzt in dem Fall automatisch die Checkbox "Eingang wird über
    /// Übergangsregeln definiert" mit (die klassische Kettenprüfung greift dann nicht mehr, siehe
    /// BerechneRundenFehler — Nutzer-Entscheidung: automatisch statt weiterhin manuell) und korrigiert einen
    /// von der Regel-Summe abweichenden gespeicherten Wert nach Hinweis-Dialog.</summary>
    private void EingangAusRegelnAbleiten(RundenEintrag runde, IReadOnlyList<Uebergangsregel> eingehendeRegeln, bool ausAktion)
    {
        HatEingehendeUebergangsregeln = eingehendeRegeln.Count > 0;
        if (!HatEingehendeUebergangsregeln)
        {
            // Checkbox nur zurücksetzen, wenn gerade die letzte Regel per Block-Aktion entfernt wurde — nicht
            // bei einem normalen Rundenwechsel, sonst würde ein bewusst ohne Regeln gesetzter manueller Haken
            // (Escape-Hatch für aktuell nicht modellierbare Fälle, z.B. turnierübergreifende Quellen, #177)
            // bei jeder erneuten Ansicht der Runde verworfen.
            if (ausAktion && runde.EingangUeberUebergangsregeln)
            {
                runde.EingangUeberUebergangsregeln = false;
                _populiertGerade++;
                try { EingangUeberUebergangsregeln = false; }
                finally { _populiertGerade--; }
                AktualisiereRundenFehler();
            }
            return;
        }

        if (!runde.EingangUeberUebergangsregeln)
        {
            runde.EingangUeberUebergangsregeln = true;
            _populiertGerade++;
            try { EingangUeberUebergangsregeln = true; }
            finally { _populiertGerade--; }
        }

        var summe = UebergangsregelService.EingangAbleiten(eingehendeRegeln);
        if (runde.EingangsMannschaften == summe) { AktualisiereRundenFehler(); return; }

        MessageBox.Show(
            $"Der gespeicherte Eingang von '{runde.Name}' ({runde.EingangsMannschaften}) weicht von der " +
            $"Summe seiner eingehenden Übergangsregeln ({summe}) ab und wird korrigiert.",
            "Eingang korrigiert", MessageBoxButton.OK, MessageBoxImage.Information);

        runde.EingangsMannschaften = summe;
        _populiertGerade++;
        try { Eingang = summe.ToString(); }
        finally { _populiertGerade--; }
        IsDirty = true;
        AktualisiereRundenFehler();
        AktualisiereRundenWarnungen();
    }

    /// <summary>Spiegel von <see cref="LadeUebergangsregelnAsync"/> für den "Ausgehende Übergangsregeln"-Bereich
    /// (Issue #186): gruppiert nach Zielrunde statt Quellrunde, die aktuell bearbeitete Runde ist überall
    /// Quellrunde. Bewusst nicht auf K.o.-Rundenarten beschränkt — anders als bei der reinen Ausgang-Wert-Ableitung
    /// in <see cref="LadeAusgehendeUebergangsregelnAsync"/> sollen K.o.-Runden ihre (ggf. zwei) ausgehenden Blöcke
    /// weiterhin sehen/bearbeiten können, nur der abgeleitete Ausgangs-Wert bleibt K.o.-spezifisch geschützt.</summary>
    private async Task LadeAusgehendeUebergangsregelBloeckeAsync(RundenEintrag runde)
    {
        if (runde.Id <= 0) return;
        try
        {
            var alleRegeln = await _uebergangsregelService.AlleNachQuellRundeIdAsync(runde.Id);
            var bloecke = new List<UebergangsregelBlock>();
            foreach (var gruppe in alleRegeln.GroupBy(r => r.ZielRundeId))
            {
                var zielRunde = Runden.FirstOrDefault(r => r.Id == gruppe.Key);
                if (zielRunde is null) continue;
                var block = UebergangsregelBlock.AusBestehendenRegelnErstellen(
                    _uebergangsregelService, UebergangsregelRichtung.Ausgehend, runde, zielRunde,
                    gruppe.ToList(), SicherstellenRundenGespeichertAsync);
                VerdrahteBlockEvents(block);
                bloecke.Add(block);
            }
            if (AusgewaehlteRunde == runde)
                AusgehendeUebergangsregelBloecke = new ObservableCollection<UebergangsregelBlock>(bloecke);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannNeuerUebergangsregelBlock))]
    private void NeuerUebergangsregelBlock()
    {
        if (AusgewaehlteRunde is null) return;
        var block = new UebergangsregelBlock(
            _uebergangsregelService, UebergangsregelRichtung.Eingehend, AusgewaehlteRunde, SicherstellenRundenGespeichertAsync);
        VerdrahteBlockEvents(block);
        EingehendeUebergangsregelBloecke.Add(block);
    }
    private bool KannNeuerUebergangsregelBlock() => AusgewaehlteRunde is not null;

    [RelayCommand(CanExecute = nameof(KannNeuerAusgehenderUebergangsregelBlock))]
    private void NeuerAusgehenderUebergangsregelBlock()
    {
        if (AusgewaehlteRunde is null) return;
        var block = new UebergangsregelBlock(
            _uebergangsregelService, UebergangsregelRichtung.Ausgehend, AusgewaehlteRunde, SicherstellenRundenGespeichertAsync);
        VerdrahteBlockEvents(block);
        AusgehendeUebergangsregelBloecke.Add(block);
    }
    // Die letzte Runde eines Turniers hat immer Ausgang 1 (der Sieger) und kann strukturell keine Zielrunde
    // haben — ohne spätere Runde in SpaeterRundenOptionen wäre ein neuer Block sofort ohne Auswahlmöglichkeit.
    private bool KannNeuerAusgehenderUebergangsregelBlock() => AusgewaehlteRunde is not null && SpaeterRundenOptionen.Any();

    private void VerdrahteBlockEvents(UebergangsregelBlock block)
    {
        // Issue #188-Folgefeedback: Block-Fehlermeldungen (z.B. UebergangsregelValidator-Meldungen) in die
        // rechte Meldungs-Spalte spiegeln, statt sie nur inline im ggf. langen Block sichtbar zu lassen.
        block.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UebergangsregelBlock.Fehlermeldung))
                AktualisiereBlockFehlermeldungen();
        };
        block.Geloescht += () =>
        {
            if (block.Richtung == UebergangsregelRichtung.Eingehend)
            {
                EingehendeUebergangsregelBloecke.Remove(block);
                EingehendeRegelnNeuLaden(block);
            }
            else AusgehendeUebergangsregelBloecke.Remove(block);
            AktualisiereBlockFehlermeldungen();
        };
        block.Gespeichert += () =>
        {
            if (block.Richtung == UebergangsregelRichtung.Eingehend)
                EingehendeRegelnNeuLaden(block);
        };
        // Unter der Positions-Ordnung von FrueherRundenOptionen kann bei einem eingehenden Block QuellRunde nie
        // mit AusgewaehlteRunde identisch sein (Quellrunde steht immer vor der aktuell bearbeiteten Zielrunde
        // in der Liste) — für eingehende Blöcke bleibt der Zweig unten daher weiterhin inert. Bei einem
        // ausgehenden Block (Issue #186) IST QuellRunde jedoch immer die fixe AusgewaehlteRunde, der Zweig
        // greift dort real: nach dem Speichern eines ausgehenden Blocks aktualisiert sich das Ausgang-Feld live.
        block.QuellRundeAusgangGeaendert += () =>
        {
            IsDirty = true;
            AktualisiereRundenFehler();
            AktualisiereRundenWarnungen();
            if (ReferenceEquals(AusgewaehlteRunde, block.QuellRunde) && block.QuellRunde is not null)
            {
                HatAusgehendeUebergangsregeln = true;
                _populiertGerade++;
                try { Ausgang = block.QuellRunde.AusgangsMannschaften.ToString(); }
                finally { _populiertGerade--; }
            }
        };
    }

    /// <summary>Issue #186-Folgefund: nach Speichern/Löschen eines eingehenden Blocks für die aktuell
    /// bearbeitete Runde muss die Eingang-Ableitung sofort neu laufen (nicht erst beim nächsten Rundenwechsel)
    /// — ein einzelner Block kennt seine Geschwister-Blöcke nicht und kann daher keine lokale Summe bilden,
    /// anders als beim Ausgang, wo <see cref="UebergangsregelBlock.Speichern"/> direkt zurückschreibt.</summary>
    private void EingehendeRegelnNeuLaden(UebergangsregelBlock block)
    {
        if (block.ZielRunde is null || !ReferenceEquals(AusgewaehlteRunde, block.ZielRunde)) return;
        _ = LadeUebergangsregelnAsync(AusgewaehlteRunde, ausAktion: true);
    }

    /// <summary>Issue #153: löst den normalen Rundenlisten-Speichern-Fluss aus, wenn ein Übergangsregel-Block
    /// gespeichert werden soll, obwohl Quell- und/oder Zielrunde noch keine echte Id haben (mehrere neue
    /// Runden + deren Übergangsregeln in einer Sitzung anlegen). Gibt zurück, ob anschließend alle Runden
    /// persistiert sind.</summary>
    private async Task<bool> SicherstellenRundenGespeichertAsync()
    {
        if (Runden.All(r => r.Id > 0)) return true;
        if (AusgewaehlteInstanz is null) return false;
        if (HatRundenFehler)
        {
            Fehlermeldung = "Die Rundenliste enthält Fehler (siehe unten) und kann nicht automatisch gespeichert werden, um Übergangsregeln anzulegen.";
            return false;
        }
        await Speichern();
        return Runden.All(r => r.Id > 0);
    }

    private void RundenZuruecksetzen(IEnumerable<RundenEintrag> runden)
    {
        _populiertGerade++;
        try
        {
            Runden            = new ObservableCollection<RundenEintrag>(runden);
            AusgewaehlteRunde = null;
            FormularZuruecksetzen();
            IsDirty       = false;
            Fehlermeldung = null;
            // Issue #166: jeder Reset invalidiert eine zuvor gemerkte Vorlagen-Übergangsstruktur —
            // VorlageRundenLaden setzt sie direkt danach bei Bedarf erneut.
            _vorlagenUebergangsregelnPending = [];
        }
        finally { _populiertGerade--; }
        AktualisiereRundenFehler();
        AktualisiereRundenWarnungen();
    }

    private void FormularZuruecksetzen()
    {
        _populiertGerade++;
        try
        {
            Name                   = "";
            Rundenart              = Rundenart.Liga;
            Eingang                = "4";
            Ausgang                = "4";
            GruppenAnzahl          = "";
            Regelwerk              = _turnierStandard;
            MitRueckspiel          = false;
            AuswaertstorRegelAktiv = false;
            EingangUeberUebergangsregeln = false;
        }
        finally { _populiertGerade--; }
        HatAusgehendeUebergangsregeln = false;
        HatEingehendeUebergangsregeln = false;
    }

    private IReadOnlyList<TurnierRundeEingabe> RundenListeZuEingaben()
        => Runden.Select(r =>
        {
            var regelwerkId = r.RegelwerkId != 0 ? r.RegelwerkId
                              : (_instanzStandardRegelwerkId ?? 0);
            return new TurnierRundeEingabe(
                r.Name, r.Rundenart, r.EingangsMannschaften, r.AusgangsMannschaften,
                regelwerkId, r.MitRueckspiel, r.AuswaertstorRegelAktiv,
                GruppenAnzahl: r.GruppenAnzahl,
                EingangUeberUebergangsregeln: r.EingangUeberUebergangsregeln);
        }).ToList();

    private static RundenEintrag RundeZuEintrag(TurnierRunde r) => new()
    {
        Id                     = r.Id,
        Name                   = r.Name,
        Rundenart              = r.Rundenart,
        EingangsMannschaften   = r.EingangsMannschaftsanzahl,
        AusgangsMannschaften   = r.AusgangsMannschaftsanzahl,
        GruppenAnzahl          = r.GruppenAnzahl,
        UrspruenglichEingangsMannschaften = r.EingangsMannschaftsanzahl,
        UrspruenglichAusgangsMannschaften = r.AusgangsMannschaftsanzahl,
        UrspruenglichGruppenAnzahl        = r.GruppenAnzahl,
        RegelwerkId            = r.RegelwerkId,
        RegelwerkName          = r.Regelwerk?.Name ?? "",
        MitRueckspiel          = r.MitRueckspiel,
        AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv,
        EingangUeberUebergangsregeln = r.EingangUeberUebergangsregeln
    };

    private bool KannSpeichern()          => IsDirty && AusgewaehlteInstanz is not null && !HatRundenFehler;
    private bool KannAbbrechen()          => IsDirty && AusgewaehlteInstanz is not null;

    public bool HatUngespeicherteAenderungen => IsDirty && AusgewaehlteInstanz is not null;
    private bool KannNeueRunde()          => RundenEditorAktiv;
    private bool KannEntfernen()          => AusgewaehlteRunde is not null;
    private bool KannHoch()               => AusgewaehlteRunde is not null && Runden.IndexOf(AusgewaehlteRunde) > 0;
    private bool KannRunter()             => AusgewaehlteRunde is not null
                                            && Runden.IndexOf(AusgewaehlteRunde) < Runden.Count - 1;
    private bool KannVorladeRundenLaden() => AusgewaehlteVorlagenquelle is not null && RundenEditorAktiv;

    private static int ParseAnzahl(string text, int fallback)
        => int.TryParse(text.Trim(), out var n) && n >= 1 ? n : fallback;
}
