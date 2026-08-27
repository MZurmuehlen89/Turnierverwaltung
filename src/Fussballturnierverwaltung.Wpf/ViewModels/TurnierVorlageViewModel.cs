using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.Wpf.Converters;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public record VorlagenKategorieOption(string Anzeige, VorlagenKategorie? Wert);

// ViewModel-Zeile für das Runden-DataGrid; jede Eigenschaft ist observable
// damit Inline-Editing im DataGrid sofort den Dirty-Zustand der Vorlage setzt.
public partial class VorlagenRundeZeile : ObservableObject
{
    public int? DbId { get; init; }

    [ObservableProperty] private string _name = RundenartZuTextConverter.RundenartAnzeigeName(Rundenart.Liga);
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;

    partial void OnRundenartChanged(Rundenart oldValue, Rundenart newValue)
    {
        if (Name == RundenartZuTextConverter.RundenartAnzeigeName(oldValue))
            Name = RundenartZuTextConverter.RundenartAnzeigeName(newValue);
    }
    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private int? _gruppenAnzahl;
    [ObservableProperty] private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private RegelwerkOption _regelwerk = new("— Wählen —", null);
}

/// <summary>Vorlagen-Variante der Übergangsregel (Issue #166) — Quelle/Ziel werden per Objektreferenz auf
/// eine <see cref="VorlagenRundeZeile"/> gebunden statt per Id, da neue Runden vor dem Speichern noch keine
/// reale Id haben; beim Speichern wird daraus die 1-basierte Listenposition (Reihenfolge) abgeleitet.</summary>
public partial class VorlagenUebergangsRegelZeile : ObservableObject
{
    public int? DbId { get; init; }

    [ObservableProperty] private VorlagenRundeZeile? _quelle;
    [ObservableProperty] private VorlagenRundeZeile? _ziel;
    [ObservableProperty] private int? _gruppeIndex;
    [ObservableProperty] private int _platzierungVon = 1;
    [ObservableProperty] private int _platzierungBis = 1;

    // Issue #217: gruppenübergreifende Rangfolge-Regel, Vorlagen-Pendant zu Uebergangsregel.AnzahlBeste (#184).
    [ObservableProperty] private int? _anzahlBeste;
}

public partial class TurnierVorlageViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierVorlageService _service;
    private readonly RegelwerkService _regelwerkService;
    private readonly VorlagenUebergangsregelValidator _uebergangsregelValidator;
    private readonly TurnierVorlageValidator _vorlageValidator;
    private int _populiertGerade;

    private static readonly RegelwerkOption _keinRegelwerk = new("— Wählen —", null);

    private static readonly VorlagenKategorieOption _alleKategorien = new("— Alle —", null);

    // ── Liste ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlagenGefiltert))]
    private ObservableCollection<TurnierVorlage> _vorlagen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlagenGefiltert))]
    private string _suchbegriff = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlagenGefiltert))]
    private VorlagenKategorieOption _kategorieFilter = _alleKategorien;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private TurnierVorlage? _ausgewaehltes;

    // ── Formular-Zustand ───────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _formularAktiv;

    partial void OnFormularAktivChanged(bool value) => AktualisiereFormularFehler();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoeschenCommand))]
    private bool _istNeuModus;

    [ObservableProperty] private bool _istLadend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    private string? _fehlermeldung;

    // ── Validierung (Issue #222) ─────────────────────────────────────────────
    /// <summary>Live-Vorschau der <see cref="VorlagenUebergangsregelValidator"/>-Prüfung, die bisher nur beim
    /// Speichern lief (<see cref="TurnierVorlageService.RundenSynchronisierenAsync"/>). Der Validator ist
    /// zustandslos, daher hier ohne Service-Roundtrip bei jeder Runden-/Übergangsregel-Änderung neu berechnet.
    /// Unvollständige Übergangsregel-Zeilen (Quelle/Ziel noch nicht gewählt) werden dabei stillschweigend
    /// übersprungen statt wie beim Speichern eine Exception zu werfen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatStrukturFehler))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _strukturFehler = [];

    public bool HatStrukturFehler => StrukturFehler.Count > 0;

    /// <summary>Live-Vorschau von <see cref="TurnierVorlageValidator"/> (Issue #234) — Basisdaten,
    /// Turniereinstellungen (bisher stilles Clamping in <see cref="ParseAnzahl"/>/<see cref="ParseJahr"/>)
    /// und einzelne Runden-Felder, die bisher weder client- noch serverseitig geprüft wurden.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatFormularFehler))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _formularFehler = [];

    public bool HatFormularFehler => FormularFehler.Count > 0;

    /// <summary>Steuert die rechte Meldungs-Spalte (Issue #222, analog #188) — Speicherfehler und Live-
    /// Strukturfehler erscheinen dort dauerhaft statt im scrollenden Formular unterzugehen.</summary>
    public bool ZeigeMeldungsSpalte => Fehlermeldung is not null || HatStrukturFehler || HatFormularFehler;

    private void AktualisiereStrukturFehler()
    {
        var rundenEingaben = RundenZeilen.Select(z => new VorlagenRundeEingabe(
            z.Name, z.Rundenart,
            z.EingangsMannschaftsanzahl, z.AusgangsMannschaftsanzahl,
            z.MitRueckspiel, z.AuswaertstorRegelAktiv, z.Regelwerk.Wert?.Id,
            z.Rundenart == Rundenart.Gruppenphase ? z.GruppenAnzahl : null)).ToList();

        var uebergangsregelEingaben = UebergangsregelZeilen
            .Where(z => z.Quelle is not null && z.Ziel is not null && !ReferenceEquals(z.Quelle, z.Ziel))
            .Select(z => new VorlagenUebergangsRegelEingabe(
                RundenZeilen.IndexOf(z.Quelle!) + 1, RundenZeilen.IndexOf(z.Ziel!) + 1,
                z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste))
            .ToList();

        StrukturFehler = _uebergangsregelValidator.Validieren(rundenEingaben, uebergangsregelEingaben).Fehler;
    }

    /// <summary>Nicht als Zahl parsbare Werte fließen nicht in <see cref="TurnierVorlageValidator.BasisdatenValidieren"/>
    /// ein (der erwartet bereits geparste Werte wie <see cref="TurnierVorlageService"/>) — dafür hier je ein
    /// eigener Format-Fehler, analog zum Umgang mit unvollständigen Zeilen in <see cref="AktualisiereStrukturFehler"/>.</summary>
    private void AktualisiereFormularFehler()
    {
        if (!FormularAktiv) { FormularFehler = []; return; }

        var fehler = new List<string>();

        var anzahlText = AnzahlMannschaften.Trim();
        if (!int.TryParse(anzahlText, out var anzahl))
        {
            fehler.Add("Erforderliche Mannschaftsanzahl muss eine gültige Zahl sein.");
            anzahl = 2;
        }

        if (!string.IsNullOrWhiteSpace(GueltigVon) && ParseJahr(GueltigVon) is null)
            fehler.Add("Gültig von muss eine Jahreszahl zwischen 1800 und 2100 sein.");
        if (!string.IsNullOrWhiteSpace(GueltigBis) && ParseJahr(GueltigBis) is null)
            fehler.Add("Gültig bis muss eine Jahreszahl zwischen 1800 und 2100 sein.");

        fehler.AddRange(_vorlageValidator.BasisdatenValidieren(Name, anzahl, ParseJahr(GueltigVon), ParseJahr(GueltigBis)).Fehler);

        var rundenEingaben = RundenZeilen.Select(z => new VorlagenRundeEingabe(
            z.Name, z.Rundenart,
            z.EingangsMannschaftsanzahl, z.AusgangsMannschaftsanzahl,
            z.MitRueckspiel, z.AuswaertstorRegelAktiv, z.Regelwerk.Wert?.Id,
            z.Rundenart == Rundenart.Gruppenphase ? z.GruppenAnzahl : null)).ToList();
        fehler.AddRange(_vorlageValidator.RundenValidieren(rundenEingaben).Fehler);

        FormularFehler = fehler;
    }

    // ── Formularfelder ────────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _beschreibung = "";
    [ObservableProperty] private string _anzahlMannschaften = "8";
    [ObservableProperty] private VorlagenKategorieOption _ausgewaehlteKategorie = new(VorlagenKategorieZuTextConverter.KategorieAnzeigeName(VorlagenKategorie.Benutzerdefiniert), VorlagenKategorie.Benutzerdefiniert);
    [ObservableProperty] private bool _istHistorisch;
    [ObservableProperty] private string _gueltigVon = "";
    [ObservableProperty] private string _gueltigBis = "";

    // ── Runden ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeNachObenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeNachUntenCommand))]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyPropertyChangedFor(nameof(KeineRundeFokussiert))]
    private VorlagenRundeZeile? _ausgewaehlteRunde;

    partial void OnAusgewaehlteRundeChanged(VorlagenRundeZeile? value) => AktualisiereUebergangsregelnGefiltert();

    public bool KeineRundeFokussiert => AusgewaehlteRunde is null;

    [ObservableProperty] private ObservableCollection<VorlagenRundeZeile> _rundenZeilen = [];

    partial void OnRundenZeilenChanged(ObservableCollection<VorlagenRundeZeile> value)
    {
        AktualisiereStrukturFehler();
        AktualisiereFormularFehler();
    }

    // ── Übergangsregeln (Issue #166) ─────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UebergangsregelEntfernenCommand))]
    private VorlagenUebergangsRegelZeile? _ausgewaehlteUebergangsregel;

    [ObservableProperty] private ObservableCollection<VorlagenUebergangsRegelZeile> _uebergangsregelZeilen = [];

    partial void OnUebergangsregelZeilenChanged(ObservableCollection<VorlagenUebergangsRegelZeile> value)
    {
        AktualisiereUebergangsregelnGefiltert();
        AktualisiereStrukturFehler();
    }

    /// <summary>Issue #210: statt aller Übergangsregeln der Vorlage nur die der in <see cref="RundenZeilen"/>
    /// fokussierten Runde (als Quelle oder Ziel) — analog zur eingehend/ausgehend-Filterung im
    /// <c>RundenEditorViewModel</c>, hier aber als eine gemeinsame Liste statt getrennter Blöcke, da das
    /// ordnungsbasierte (statt id-basierte) Vorlagen-Datenmodell keine 1:1-Übernahme erlaubt.
    ///
    /// Bewusst eine stabile, in-place per Add/Remove synchronisierte ObservableCollection statt einer per
    /// Getter neu berechneten IEnumerable/LINQ-Projektion (Issue #210-Folgefund): DataGrid löst "'EditItem' ist
    /// für diese Ansicht nicht zulässig" aus, sobald ItemsSource nur IEnumerable (keine IList) implementiert —
    /// WPF nutzt dafür intern die nicht editierbare EnumerableCollectionView statt ListCollectionView.</summary>
    public ObservableCollection<VorlagenUebergangsRegelZeile> UebergangsregelZeilenFuerFokussierteRunde { get; } = [];

    private void AktualisiereUebergangsregelnGefiltert()
    {
        var soll = AusgewaehlteRunde is null
            ? []
            : UebergangsregelZeilen.Where(z => z.Quelle == AusgewaehlteRunde || z.Ziel == AusgewaehlteRunde).ToList();

        foreach (var alt in UebergangsregelZeilenFuerFokussierteRunde.Where(z => !soll.Contains(z)).ToList())
            UebergangsregelZeilenFuerFokussierteRunde.Remove(alt);
        foreach (var neu in soll.Where(z => !UebergangsregelZeilenFuerFokussierteRunde.Contains(z)))
            UebergangsregelZeilenFuerFokussierteRunde.Add(neu);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    // ── Berechnete Listen ─────────────────────────────────────────────────────
    public IEnumerable<TurnierVorlage> VorlagenGefiltert => Vorlagen
        .Where(v =>
            (string.IsNullOrWhiteSpace(Suchbegriff) || v.Name.Contains(Suchbegriff, StringComparison.OrdinalIgnoreCase))
            && (KategorieFilter.Wert is null || v.VorlagenKategorie == KategorieFilter.Wert));

    public IReadOnlyList<VorlagenKategorieOption> KategorieFilterOptionen =>
        [_alleKategorien, .. Enum.GetValues<VorlagenKategorie>()
            .Select(k => new VorlagenKategorieOption(VorlagenKategorieZuTextConverter.KategorieAnzeigeName(k), k))];

    public IReadOnlyList<VorlagenKategorieOption> KategorieOptionen =>
        Enum.GetValues<VorlagenKategorie>()
            .Select(k => new VorlagenKategorieOption(VorlagenKategorieZuTextConverter.KategorieAnzeigeName(k), k))
            .ToList();

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [_keinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    private static readonly HashSet<string> _formFelder =
    [
        nameof(Name), nameof(Beschreibung), nameof(AnzahlMannschaften),
        nameof(AusgewaehlteKategorie), nameof(IstHistorisch),
        nameof(GueltigVon), nameof(GueltigBis)
    ];

    // ── IRibbonAware ───────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Turniervorlagen";
    public ICommand NeuBefehl => NeuCommand;
    public ICommand SpeichernBefehl => SpeichernCommand;
    public ICommand AbbrechenBefehl => AbbrechenCommand;
    public ICommand LoeschenBefehl => LoeschenCommand;

    public TurnierVorlageViewModel(
        TurnierVorlageService service, RegelwerkService regelwerkService,
        VorlagenUebergangsregelValidator uebergangsregelValidator,
        TurnierVorlageValidator vorlageValidator)
    {
        _service = service;
        _regelwerkService = regelwerkService;
        _uebergangsregelValidator = uebergangsregelValidator;
        _vorlageValidator = vorlageValidator;
    }

    public override async Task AktivierenAsync() => await LadenAsync();

    private async Task LadenAsync()
    {
        IstLadend = true;
        try
        {
            var vorlagen   = await _service.AlleAbrufenAsync();
            var regelwerke = await _regelwerkService.AlleAbrufenAsync();
            Vorlagen   = new ObservableCollection<TurnierVorlage>(vorlagen);
            Regelwerke = new ObservableCollection<Regelwerk>(regelwerke);
        }
        finally
        {
            IstLadend = false;
        }
    }

    private void FormularAusFuellenMit(TurnierVorlage? vorlage)
    {
        _populiertGerade++;
        try
        {
            if (vorlage is null)
            {
                FormularAktiv = false;
                IstNeuModus = false;
                Name = Beschreibung = GueltigVon = GueltigBis = "";
                AnzahlMannschaften = "8";
                AusgewaehlteKategorie = KategorieOptionen.First(o => o.Wert == VorlagenKategorie.Benutzerdefiniert);
                IstHistorisch = false;
                RundenZeilen = [];
                UebergangsregelZeilen = [];
                AusgewaehlteRunde = null;
            }
            else
            {
                FormularAktiv = true;
                IstNeuModus = false;
                Name = vorlage.Name;
                Beschreibung = vorlage.Beschreibung;
                AnzahlMannschaften = vorlage.ErforderlicheMannschaftsanzahl.ToString();
                AusgewaehlteKategorie = KategorieOptionen.FirstOrDefault(o => o.Wert == vorlage.VorlagenKategorie)
                    ?? KategorieOptionen.First();
                IstHistorisch = vorlage.IstHistorisch;
                GueltigVon = vorlage.GueltigVon?.Year.ToString() ?? "";
                GueltigBis = vorlage.GueltigBis?.Year.ToString() ?? "";
                // VorlagenRunden werden async in AusgewaehltesGeaendertAsync geladen
            }
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    partial void OnAusgewaehltesChanged(TurnierVorlage? value)
        => _ = AusgewaehltesGeaendertAsync(value);

    private async Task AusgewaehltesGeaendertAsync(TurnierVorlage? vorlage)
    {
        FormularAusFuellenMit(vorlage);
        if (vorlage is null) return;

        IstLadend = true;
        try
        {
            var mitRunden = await _service.FindenNachIdMitRundenAsync(vorlage.Id);
            _populiertGerade++;
            try
            {
                var vorlagenRunden = (mitRunden?.VorlagenRunden ?? []).OrderBy(r => r.Reihenfolge).ToList();
                var zeilen = vorlagenRunden.Select(r => new VorlagenRundeZeile
                {
                    DbId                      = r.Id,
                    Name                      = r.Name,
                    Rundenart                 = r.Rundenart,
                    EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
                    AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
                    GruppenAnzahl             = r.GruppenAnzahl,
                    MitRueckspiel             = r.MitRueckspiel,
                    AuswaertstorRegelAktiv    = r.AuswaertstorRegelAktiv,
                    Regelwerk                 = RegelwerkOptionen.FirstOrDefault(o => o.Wert?.Id == r.RegelwerkId)
                                                ?? _keinRegelwerk
                }).ToList();
                RundenZeilen = new ObservableCollection<VorlagenRundeZeile>(zeilen);
                AbonniereRundenAenderungen();

                // Issue #166: Übergangsregeln aus derselben Zip-Reihenfolge auflösen (VorlagenRunden und
                // RundenZeilen stehen an identischer Position, da beide aus derselben sortierten Quelle stammen).
                var zeileNachVorlagenRundeId = vorlagenRunden.Zip(zeilen, (r, z) => (r.Id, z))
                    .ToDictionary(p => p.Id, p => p.z);
                UebergangsregelZeilen = new ObservableCollection<VorlagenUebergangsRegelZeile>(
                    vorlagenRunden.SelectMany(r => r.AusgehendeUebergangsregeln)
                        .Where(regel => zeileNachVorlagenRundeId.ContainsKey(regel.ZielVorlagenRundeId))
                        .Select(regel => new VorlagenUebergangsRegelZeile
                        {
                            DbId           = regel.Id,
                            Quelle         = zeileNachVorlagenRundeId[regel.QuellVorlagenRundeId],
                            Ziel           = zeileNachVorlagenRundeId[regel.ZielVorlagenRundeId],
                            GruppeIndex    = regel.GruppeIndex,
                            PlatzierungVon = regel.PlatzierungVon,
                            PlatzierungBis = regel.PlatzierungBis,
                            AnzahlBeste    = regel.AnzahlBeste
                        }));
                AbonniereUebergangsregelnAenderungen();
                IsDirty = false;
            }
            finally
            {
                _populiertGerade--;
            }
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

    // Setzt IsDirty wenn eine Zeile inline bearbeitet wird
    private void AbonniereRundenAenderungen()
    {
        foreach (var zeile in RundenZeilen)
            zeile.PropertyChanged += OnRundeZeileGeaendert;
    }

    private void OnRundeZeileGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (_populiertGerade == 0)
            IsDirty = true;
        AktualisiereUebergangsregelnGefiltert();
        AktualisiereStrukturFehler();
        AktualisiereFormularFehler();
    }

    // Issue #166: analoges Dirty-Tracking für die Übergangsregel-Zeilen
    private void AbonniereUebergangsregelnAenderungen()
    {
        foreach (var zeile in UebergangsregelZeilen)
            zeile.PropertyChanged += OnRundeZeileGeaendert;
    }

    // ── Runden-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void RundeHinzufuegen()
    {
        var letzte = RundenZeilen.LastOrDefault();
        var neu = new VorlagenRundeZeile
        {
            Rundenart                 = Rundenart.Liga,
            EingangsMannschaftsanzahl = letzte?.AusgangsMannschaftsanzahl ?? 8,
            AusgangsMannschaftsanzahl = Math.Max(1, (letzte?.AusgangsMannschaftsanzahl ?? 8) / 2)
        };
        neu.PropertyChanged += OnRundeZeileGeaendert;
        RundenZeilen.Add(neu);
        AusgewaehlteRunde = neu;
        IsDirty = true;
        AktualisiereStrukturFehler();
        AktualisiereFormularFehler();
    }

    [RelayCommand(CanExecute = nameof(KannRundeEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var entfernt = AusgewaehlteRunde;
        RundenZeilen.Remove(entfernt);
        AusgewaehlteRunde = RundenZeilen.LastOrDefault();

        // Issue #166: Übergangsregeln, die auf die entfernte Runde verweisen, sind sonst verwaist.
        foreach (var regel in UebergangsregelZeilen.Where(z => z.Quelle == entfernt || z.Ziel == entfernt).ToList())
            UebergangsregelZeilen.Remove(regel);

        AktualisiereUebergangsregelnGefiltert();
        IsDirty = true;
        AktualisiereStrukturFehler();
        AktualisiereFormularFehler();
    }

    // ── Übergangsregel-Befehle (Issue #166) ──────────────────────────────────────

    [RelayCommand]
    private void UebergangsregelHinzufuegen()
    {
        // Issue #210: neue Regel geht standardmäßig von der fokussierten Runde aus, damit sie sofort in der
        // gefilterten Liste erscheint statt spurlos in einer unfokussierten Runde zu verschwinden.
        var neu = new VorlagenUebergangsRegelZeile { Quelle = AusgewaehlteRunde };
        neu.PropertyChanged += OnRundeZeileGeaendert;
        UebergangsregelZeilen.Add(neu);
        AusgewaehlteUebergangsregel = neu;
        AktualisiereUebergangsregelnGefiltert();
        IsDirty = true;
        AktualisiereStrukturFehler();
    }

    [RelayCommand(CanExecute = nameof(KannUebergangsregelEntfernen))]
    private void UebergangsregelEntfernen()
    {
        if (AusgewaehlteUebergangsregel is null) return;
        UebergangsregelZeilen.Remove(AusgewaehlteUebergangsregel);
        AusgewaehlteUebergangsregel = UebergangsregelZeilen.LastOrDefault();
        AktualisiereUebergangsregelnGefiltert();
        IsDirty = true;
        AktualisiereStrukturFehler();
    }

    private bool KannUebergangsregelEntfernen() => AusgewaehlteUebergangsregel is not null;

    [RelayCommand(CanExecute = nameof(KannRundeNachOben))]
    private void RundeNachOben()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = RundenZeilen.IndexOf(AusgewaehlteRunde);
        if (idx <= 0) return;
        RundenZeilen.Move(idx, idx - 1);
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannRundeNachUnten))]
    private void RundeNachUnten()
    {
        if (AusgewaehlteRunde is null) return;
        var idx = RundenZeilen.IndexOf(AusgewaehlteRunde);
        if (idx < 0 || idx >= RundenZeilen.Count - 1) return;
        RundenZeilen.Move(idx, idx + 1);
        IsDirty = true;
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Neu()
    {
        _populiertGerade++;
        try
        {
            Ausgewaehltes = null;
            FormularAktiv = true;
            IstNeuModus = true;
            Name = Beschreibung = GueltigVon = GueltigBis = "";
            AnzahlMannschaften = "8";
            AusgewaehlteKategorie = KategorieOptionen.First(o => o.Wert == VorlagenKategorie.Benutzerdefiniert);
            IstHistorisch = false;
            RundenZeilen = [];
            UebergangsregelZeilen = [];
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        Fehlermeldung = null;
        try
        {
            var anzahl = ParseAnzahl(AnzahlMannschaften);
            var gueltigVon = ParseJahr(GueltigVon);
            var gueltigBis = ParseJahr(GueltigBis);
            var kategorie = AusgewaehlteKategorie.Wert ?? VorlagenKategorie.Benutzerdefiniert;
            var rundenEingaben = RundenZeilen.Select(z => new VorlagenRundeEingabe(
                z.Name, z.Rundenart,
                z.EingangsMannschaftsanzahl, z.AusgangsMannschaftsanzahl,
                z.MitRueckspiel, z.AuswaertstorRegelAktiv, z.Regelwerk.Wert?.Id,
                z.Rundenart == Rundenart.Gruppenphase ? z.GruppenAnzahl : null)).ToList();

            // Issue #166: Quelle/Ziel sind Objektreferenzen auf RundenZeilen — für die Persistierung in die
            // 1-basierte Listenposition (Reihenfolge) auflösen, die RundenSynchronisierenAsync den neu
            // angelegten VorlagenRunden in derselben Listenreihenfolge zuweist.
            var uebergangsregelEingaben = UebergangsregelZeilen.Select(z =>
            {
                if (z.Quelle is null || z.Ziel is null)
                    throw new ArgumentException("Übergangsregel benötigt Quell- und Zielrunde.");
                if (ReferenceEquals(z.Quelle, z.Ziel))
                    throw new ArgumentException("Übergangsregel: Quell- und Zielrunde dürfen nicht identisch sein.");
                return new VorlagenUebergangsRegelEingabe(
                    RundenZeilen.IndexOf(z.Quelle) + 1, RundenZeilen.IndexOf(z.Ziel) + 1,
                    z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste);
            }).ToList();

            if (IstNeuModus)
            {
                var eingabe = new TurnierVorlageErstellenEingabe(
                    Name, Beschreibung, anzahl, kategorie, IstHistorisch, gueltigVon, gueltigBis);
                var neu = await _service.ErstellenAsync(eingabe);
                await _service.RundenSynchronisierenAsync(neu.Id, rundenEingaben, uebergangsregelEingaben);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == neu.Id);
            }
            else if (Ausgewaehltes is not null)
            {
                var id = Ausgewaehltes.Id;
                var eingabe = new TurnierVorlageAktualisierenEingabe(
                    Name, Beschreibung, anzahl, kategorie, IstHistorisch, gueltigVon, gueltigBis);
                await _service.AktualisierenAsync(id, eingabe);
                await _service.RundenSynchronisierenAsync(id, rundenEingaben, uebergangsregelEingaben);
                await LadenAsync();
                Ausgewaehltes = Vorlagen.FirstOrDefault(v => v.Id == id);
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => IsDirty = false,
                System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen() => _ = AusgewaehltesGeaendertAsync(IstNeuModus ? null : Ausgewaehltes);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task Loeschen()
    {
        if (Ausgewaehltes is null) return;
        var ergebnis = MessageBox.Show(
            $"Soll '{Ausgewaehltes.Name}' wirklich gelöscht werden?",
            "Turniervorlage löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ergebnis != MessageBoxResult.Yes) return;
        Fehlermeldung = null;
        try
        {
            var id = Ausgewaehltes.Id;
            await _service.LoeschenAsync(id);
            await LadenAsync();
            Ausgewaehltes = null;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_formFelder.Contains(e.PropertyName!))
        {
            if (_populiertGerade == 0)
                IsDirty = true;
            AktualisiereFormularFehler();
        }
    }

    private bool KannSpeichern() => FormularAktiv && IsDirty && !HatStrukturFehler && !HatFormularFehler;
    private bool KannAbbrechen() => FormularAktiv && IsDirty;

    public bool HatUngespeicherteAenderungen => FormularAktiv && IsDirty;
    private bool KannLoeschen() => Ausgewaehltes is not null && !IstNeuModus;
    private bool KannRundeEntfernen() => AusgewaehlteRunde is not null;
    private bool KannRundeNachOben() => AusgewaehlteRunde is not null && RundenZeilen.IndexOf(AusgewaehlteRunde) > 0;
    private bool KannRundeNachUnten() => AusgewaehlteRunde is not null && RundenZeilen.IndexOf(AusgewaehlteRunde) < RundenZeilen.Count - 1;

    private static int ParseAnzahl(string text)
        => int.TryParse(text.Trim(), out var n) && n >= 2 ? n : 2;

    private static DateOnly? ParseJahr(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var jahr) && jahr >= 1800 && jahr <= 2100
            ? new DateOnly(jahr, 1, 1)
            : null;
    }
}
