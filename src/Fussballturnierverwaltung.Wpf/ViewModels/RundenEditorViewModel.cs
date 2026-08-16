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

    public string GruppenLabel => GruppeIndex is null ? "Alle" : $"Gruppe {(char)('A' + GruppeIndex.Value - 1)}";

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

/// <summary>Eine Quellrunde, aus der Platzierungen in die aktuell im Runden-Editor ausgewählte Zielrunde
/// qualifizieren — UI-Block im "Eingehende Übergangsregeln"-Abschnitt. Speichert direkt über die bestehenden
/// <see cref="UebergangsregelService"/>-Methoden, unabhängig vom großen Speichern-Knopf der Rundenliste.</summary>
public partial class UebergangsregelBlock : ObservableObject
{
    private readonly UebergangsregelService _service;
    private readonly RundenEintrag _zielRunde;
    // Issue #153: löst bei Bedarf das Speichern der gesamten Rundenliste aus, bevor Quell- und/oder Zielrunde
    // beim Blockspeichern noch keine echte Id haben (mehrere neue Runden + deren Übergangsregeln in einem Zug).
    private readonly Func<Task<bool>> _sicherstellenRundenGespeichertAsync;

    // Baseline der beim Laden vorgefundenen Zeilen-Ids — Vergleichsbasis, um beim Speichern zu erkennen,
    // welche zuvor gespeicherten Zeilen inzwischen aus der Zeilen-Collection entfernt wurden (z.B. über
    // "Aktualisieren", Issue #183) und dementsprechend gelöscht werden müssen (Issue #180).
    private readonly HashSet<int> _urspruenglicheZeilenIds = [];

    public event Action? Geloescht;
    public event Action? QuellRundeAusgangGeaendert; // Issue #153

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstLiga))]
    [NotifyPropertyChangedFor(nameof(IstKoSonderfall))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private RundenEintrag? _quellRunde;

    [ObservableProperty] private ObservableCollection<UebergangsregelZeile> _zeilen = [];
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

    public UebergangsregelBlock(
        UebergangsregelService service, RundenEintrag zielRunde, Func<Task<bool>> sicherstellenRundenGespeichertAsync)
    {
        _service = service;
        _zielRunde = zielRunde;
        _sicherstellenRundenGespeichertAsync = sicherstellenRundenGespeichertAsync;
    }

    /// <summary>Baut einen Block aus bereits gespeicherten Regeln auf (Zeilen zuerst befüllen, dann QuellRunde
    /// setzen — verhindert, dass <see cref="OnQuellRundeChanged"/> die Zeilen fälschlich mit Default-Werten
    /// überschreibt, da dessen Auto-Befüllung nur bei leerer Zeilenliste greift).</summary>
    public static UebergangsregelBlock AusBestehendenRegelnErstellen(
        UebergangsregelService service, RundenEintrag zielRunde, RundenEintrag quellRunde,
        IReadOnlyList<Uebergangsregel> regeln, Func<Task<bool>> sicherstellenRundenGespeichertAsync)
    {
        var block = new UebergangsregelBlock(service, zielRunde, sicherstellenRundenGespeichertAsync);
        foreach (var regel in regeln.OrderBy(r => r.GruppeIndex ?? 0))
        {
            var zeile = new UebergangsregelZeile { GruppeIndex = regel.GruppeIndex, Id = regel.Id };
            zeile.WerteSetzenOhneAbweichung(regel.PlatzierungVon, regel.PlatzierungBis);
            block.Zeilen.Add(zeile);
            block._urspruenglicheZeilenIds.Add(regel.Id);
        }
        block.QuellRunde = quellRunde;
        foreach (var z in block.Zeilen)
            z.PropertyChanged += (_, __) => block.IsDirty = true;
        return block;
    }

    partial void OnQuellRundeChanged(RundenEintrag? value)
    {
        if (value is null || Zeilen.Count > 0) return;

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
        if (QuellRunde is null) return;
        Fehlermeldung = null;
        try
        {
            if (QuellRunde.Id <= 0 || _zielRunde.Id <= 0)
            {
                var gespeichert = await _sicherstellenRundenGespeichertAsync();
                if (!gespeichert)
                {
                    Fehlermeldung = "Die Rundenliste konnte nicht automatisch gespeichert werden — siehe Meldung oben.";
                    return;
                }
            }

            var zeilenEingaben = Zeilen
                .Select(z => new UebergangsregelZeileEingabe(z.Id, z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis))
                .ToList();
            var zuLoeschendeIds = _urspruenglicheZeilenIds
                .Except(Zeilen.Where(z => z.Id.HasValue).Select(z => z.Id!.Value))
                .ToList();

            var ergebnis = await _service.BlockSpeichernAsync(QuellRunde.Id, _zielRunde.Id, zeilenEingaben, zuLoeschendeIds);

            for (int i = 0; i < Zeilen.Count && i < ergebnis.Count; i++)
                Zeilen[i].Id = ergebnis[i].Id;

            _urspruenglicheZeilenIds.Clear();
            foreach (var id in Zeilen.Where(z => z.Id.HasValue).Select(z => z.Id!.Value))
                _urspruenglicheZeilenIds.Add(id);

            // Issue #153: Ausgang der Quellrunde direkt im geteilten RundenEintrag-Objekt aktualisieren
            // (Objektidentität, gleiches Muster wie die Id-Rückschreibung in RundenEditorViewModel.Speichern) —
            // ohne DB-Roundtrip zeigt die Rundenliste sofort den korrekten Wert.
            var neuerAusgang = Zeilen.Sum(z => z.PlatzierungBis - z.PlatzierungVon + 1);
            if (QuellRunde.AusgangsMannschaften != neuerAusgang)
            {
                QuellRunde.AusgangsMannschaften = neuerAusgang;
                QuellRundeAusgangGeaendert?.Invoke();
            }

            IsDirty = false;
            OnPropertyChanged(nameof(IstNeu));
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
    private bool KannSpeichern() => IsDirty && QuellRunde is not null;

    [RelayCommand]
    private async Task Loeschen()
    {
        Fehlermeldung = null;
        try
        {
            foreach (var z in Zeilen.Where(z => z.Id is not null))
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

    // Preserved turnier metadata for VollAktualisierenAsync on save
    private string _instanzName = "";
    private string _instanzSaison = "";
    private DateOnly? _instanzStart;
    private DateOnly? _instanzEnd;
    private TurnierStatus _instanzStatus = TurnierStatus.Geplant;
    private int? _vorlageId;
    private int? _instanzVerbandId;
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
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    [NotifyPropertyChangedFor(nameof(GruppenAnzahlSichtbar))]
    [NotifyPropertyChangedFor(nameof(EingehendeUebergangsregelnSichtbar))]
    [NotifyPropertyChangedFor(nameof(FrueherRundenOptionen))]
    private RundenEintrag? _ausgewaehlteRunde;

    // ── Eingehende Übergangsregeln (Issue #169) ────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<UebergangsregelBlock> _uebergangsregelBloecke = [];

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
    [ObservableProperty] private bool _eingangUeberUebergangsregeln;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AusgangEditierbar))]
    [NotifyPropertyChangedFor(nameof(ZeigeUebergangsregelnAusgangHinweis))]
    private bool _hatAusgehendeUebergangsregeln;

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
    [ObservableProperty] private string? _fehlermeldung;

    // ── Validierung ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRundenFehler))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _rundenFehler = [];

    public bool HatRundenFehler => RundenFehler.Count > 0;

    // ── Berechnete Eigenschaften ──────────────────────────────────────────────
    public bool IstKoRundenart =>
        Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    // KoRundeGenerator bildet strikt n/2-Paarungen — Ausgang ist bei K.-o.-Rundenarten kein freier Wert,
    // sondern immer aus Eingang abgeleitet (Issue #182). Ausgang wird zusätzlich automatisch berechnet, sobald
    // die Runde bereits mindestens eine ausgehende Übergangsregel hat (Issue #153) — K.o.-Rundenarten haben
    // dabei weiterhin Vorrang.
    public bool AusgangEditierbar => !IstKoRundenart && !HatAusgehendeUebergangsregeln;
    public bool ZeigeUebergangsregelnAusgangHinweis => HatAusgehendeUebergangsregeln && !IstKoRundenart;

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

    // Seit Issue #153 dürfen auch noch nicht persistierte, in derselben Sitzung neu angelegte Runden als
    // Quellrunde gewählt werden (Chicken-and-egg-Fix für mehrere neue Runden + deren Übergangsregeln in einem
    // Zug). Die Listenposition allein (nicht mehr die Id) verhindert Zyklen/Vorwärtsreferenzen strukturell.
    public IEnumerable<RundenEintrag> FrueherRundenOptionen =>
        AusgewaehlteRunde is null
            ? []
            : Runden.Where(r => Runden.IndexOf(r) < Runden.IndexOf(AusgewaehlteRunde));

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
        var eintrag = new RundenEintrag
        {
            EingangsMannschaften = eingang,
            AusgangsMannschaften = Math.Max(1, eingang / 2),
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
        var idx = Runden.IndexOf(AusgewaehlteRunde);
        Runden.Remove(AusgewaehlteRunde);
        AusgewaehlteRunde = Runden.Count > 0 ? Runden[Math.Max(0, idx - 1)] : null;
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
            var runden = vorlage.VorlagenRunden
                .OrderBy(r => r.Reihenfolge)
                .Select(r => new RundenEintrag
                {
                    Name                   = r.Name,
                    Rundenart              = r.Rundenart,
                    EingangsMannschaften   = r.EingangsMannschaftsanzahl,
                    AusgangsMannschaften   = r.AusgangsMannschaftsanzahl,
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
        if (!await BestaetigeAenderungenAnQuellrundenAsync()) return;

        Fehlermeldung = null;
        try
        {
            var vollEingabe = new TurnierInstanzVollAktualisierenEingabe(
                _instanzName, _instanzSaison, _vorlageId, _instanzStart, _instanzEnd, _instanzStatus,
                _instanzVerbandId, _instanzTeamFilter,
                StandardRegelwerkId: _instanzStandardRegelwerkId);
            await _service.VollAktualisierenAsync(AusgewaehlteInstanz.Id, vollEingabe);
            var persistiert = await _service.RundenErsetzenAsync(AusgewaehlteInstanz.Id, RundenListeZuEingaben());

            // Ids der (ggf. neu angelegten) Runden zurückschreiben — sonst bleibt z.B. "Weitere Quellrunde"
            // für eine gerade erst gespeicherte Runde bis zum nächsten kompletten Neuladen deaktiviert.
            for (int i = 0; i < Runden.Count && i < persistiert.Count; i++)
                Runden[i].Id = persistiert[i].Id;
            OnPropertyChanged(nameof(FrueherRundenOptionen));
            NeuerUebergangsregelBlockCommand.NotifyCanExecuteChanged();

            // Neue Vergleichsbasis für die #181-Änderungserkennung — ein zweites Speichern in derselben
            // Sitzung soll nicht erneut warnen, wenn seither nichts weiter geändert wurde.
            foreach (var r in Runden)
            {
                r.UrspruenglichEingangsMannschaften = r.EingangsMannschaften;
                r.UrspruenglichAusgangsMannschaften = r.AusgangsMannschaften;
                r.UrspruenglichGruppenAnzahl        = r.GruppenAnzahl;
            }

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

    /// <summary>Warnt, falls eine bereits gespeicherte Runde, die als Quellrunde für bestehende Übergangsregeln
    /// dient, ihre Eingangs-/Ausgangs-/Gruppenzahl seit dem Laden geändert hat (Issue #181). Läuft nur beim
    /// Speichern (nicht pro Tastendruck), da genau dort eine Änderung tatsächlich wirksam wird.</summary>
    private async Task<bool> BestaetigeAenderungenAnQuellrundenAsync()
    {
        var geaenderte = Runden.Where(r => r.Id > 0 && HatSichGeaendert(r)).ToList();
        if (geaenderte.Count == 0) return true;

        var alleRegeln = await _uebergangsregelService.AlleNachTurnierInstanzIdAsync(AusgewaehlteInstanz!.Id);
        var betroffene = geaenderte
            .Select(r => (Runde: r, Ziele: alleRegeln.Where(u => u.QuellRundeId == r.Id)
                                                       .Select(u => u.ZielRunde.Name).Distinct().ToList()))
            .Where(x => x.Ziele.Count > 0)
            .ToList();
        if (betroffene.Count == 0) return true;

        var text = string.Join("\n", betroffene.Select(b => $"'{b.Runde.Name}' → verwendet in: {string.Join(", ", b.Ziele)}"));
        var ergebnis = MessageBox.Show(
            $"Folgende geänderte Runden werden bereits als Quelle für Übergangsregeln verwendet:\n\n{text}\n\n" +
            "Die Änderungen können diese Regeln ungültig machen (siehe Hinweis \"Aktualisieren\" im Übergangsregeln-Bereich der Zielrunde). Trotzdem speichern?",
            "Übergangsregeln betroffen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return ergebnis == MessageBoxResult.Yes;
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
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        var eingang = ParseAnzahl(value, 4);
        var ausgang = Math.Max(1, eingang / 2);
        AusgewaehlteRunde.EingangsMannschaften = eingang;
        AusgewaehlteRunde.AusgangsMannschaften = ausgang;
        _populiertGerade++;
        try { Ausgang = ausgang.ToString(); }
        finally { _populiertGerade--; }
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnAusgangChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null || !AusgangEditierbar) return;
        AusgewaehlteRunde.AusgangsMannschaften = ParseAnzahl(value, 4);
        AktualisiereRundenFehler();
        IsDirty = true;
    }

    partial void OnGruppenAnzahlChanged(string value)
    {
        if (_populiertGerade > 0 || AusgewaehlteRunde is null) return;
        AusgewaehlteRunde.GruppenAnzahl = string.IsNullOrWhiteSpace(value) ? null : ParseAnzahl(value, 2);
        AktualisiereRundenFehler();
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
                _instanzTeamFilter         = detail.MannschaftsartFilter;
                _instanzStandardRegelwerkId = detail.StandardRegelwerkId;
                _instanzStandardRegelwerk   = detail.StandardRegelwerk;
            }
            RundenZuruecksetzen(
                (detail?.Runden ?? []).OrderBy(r => r.Reihenfolge).Select(RundeZuEintrag));
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
        if (value is null) { FormularZuruecksetzen(); UebergangsregelBloecke = []; return; }
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

        UebergangsregelBloecke = [];
        _ = LadeUebergangsregelnUndAusgangAsync(value);
    }

    /// <summary>Sequenziert die beiden Übergangsregeln-Ladevorgänge — beide nutzen denselben scope-langlebigen
    /// DbContext, parallel als Fire-and-Forget gestartet führte zu "A second operation was started on this
    /// context before a previous operation completed" (EF Core erlaubt keine parallelen Operationen auf einer
    /// DbContext-Instanz, vgl. Issue #45).</summary>
    private async Task LadeUebergangsregelnUndAusgangAsync(RundenEintrag runde)
    {
        await LadeUebergangsregelnAsync(runde);
        await LadeAusgehendeUebergangsregelnAsync(runde);
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

    private async Task LadeUebergangsregelnAsync(RundenEintrag runde)
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
                    _uebergangsregelService, runde, quellRunde, gruppe.ToList(), SicherstellenRundenGespeichertAsync);
                VerdrahteBlockEvents(block);
                bloecke.Add(block);
            }
            // Falls die Runde inzwischen gewechselt wurde, während der Ladevorgang lief, nicht mehr anwenden.
            if (AusgewaehlteRunde == runde)
                UebergangsregelBloecke = new ObservableCollection<UebergangsregelBlock>(bloecke);
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
        var block = new UebergangsregelBlock(_uebergangsregelService, AusgewaehlteRunde, SicherstellenRundenGespeichertAsync);
        VerdrahteBlockEvents(block);
        UebergangsregelBloecke.Add(block);
    }
    private bool KannNeuerUebergangsregelBlock() => AusgewaehlteRunde is not null;

    private void VerdrahteBlockEvents(UebergangsregelBlock block)
    {
        block.Geloescht += () => UebergangsregelBloecke.Remove(block);
        // Unter der Positions-Ordnung von FrueherRundenOptionen kann QuellRunde nie mit AusgewaehlteRunde
        // identisch sein (Quellrunde steht immer vor der aktuell bearbeiteten Zielrunde in der Liste) — der
        // Zweig unten ist aktuell inert, aber billige Absicherung, falls sich das künftig ändert (Issue #153).
        block.QuellRundeAusgangGeaendert += () =>
        {
            IsDirty = true;
            AktualisiereRundenFehler();
            if (ReferenceEquals(AusgewaehlteRunde, block.QuellRunde) && block.QuellRunde is not null)
            {
                HatAusgehendeUebergangsregeln = true;
                _populiertGerade++;
                try { Ausgang = block.QuellRunde.AusgangsMannschaften.ToString(); }
                finally { _populiertGerade--; }
            }
        };
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
        }
        finally { _populiertGerade--; }
        AktualisiereRundenFehler();
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
