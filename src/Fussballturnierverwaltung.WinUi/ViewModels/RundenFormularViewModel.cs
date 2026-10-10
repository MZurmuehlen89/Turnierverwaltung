using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Rundenformular des „Runden“-Reiters (#435): die Felder der gewählten Runde samt abgeleiteten
/// Schaltern, Vorschlägen und Auswahllisten. Kind-ViewModel von <see cref="RundenViewModel"/> — das Formular
/// schreibt Nutzeränderungen in die gezeigte <see cref="RundeZeile"/> und meldet sie über
/// <see cref="Geaendert"/>; was daraus folgt (ungespeichert, Validierung), entscheidet das Eltern-ViewModel.</summary>
public partial class RundenFormularViewModel : ObservableObject
{
    private readonly RegelwerkService _regelwerkService;
    private readonly RanglistenQuelleService _ranglistenQuelleService;
    private int _populiertGerade;

    // Die gezeigte Runde und ob vor ihr eine Gruppenphase liegt (bestimmt, welche Auslosungsregeln wirken können).
    private RundeZeile? _zeile;
    private bool _folgtAufGruppenphase;

    /// <summary>Der Nutzer hat ein Feld der gezeigten Runde geändert; die Zeile trägt den neuen Wert bereits.</summary>
    public event Action? Geaendert;

    public RundenFormularViewModel(RegelwerkService regelwerkService, RanglistenQuelleService ranglistenQuelleService)
    {
        _regelwerkService = regelwerkService;
        _ranglistenQuelleService = ranglistenQuelleService;
    }

    /// <summary>Lädt die Auswahllisten für Regelwerk und Standard-Rangliste, sofern noch nicht geschehen.</summary>
    public async Task StammdatenSicherstellenAsync()
    {
        if (Regelwerke.Count == 0)
            Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());
        if (RanglistenQuelleOptionen.Count == 1)
            RanglistenQuelleOptionen =
            [
                RundeZeile.KeineRanglistenQuelle,
                .. (await _ranglistenQuelleService.AlleAbrufenAsync())
                    .OrderBy(q => q.Name)
                    .Select(q => new RanglistenQuelleOption(q.Name, q.Id))
            ];
    }

    /// <summary>Zeigt die Runde im Formular (<c>null</c> leert es).</summary>
    public void Zeigen(RundeZeile? zeile, bool folgtAufGruppenphase)
    {
        _zeile = zeile;
        _folgtAufGruppenphase = folgtAufGruppenphase;
        FormularAusFuellenMit(zeile);
    }

    // ── Rundenformular ────────────────────────────────────────────────────────
    // Bewusst VM-Felder statt Direktbindung auf AusgewaehlteRunde.<Feld> (siehe #284-Umsetzungsnotiz für die
    // ausführliche Begründung: eine Direktbindung auf ein bei Fokuswechsel wechselndes Objekt hat wiederholt
    // zu Datenkorruption geführt). Dasselbe Muster gilt im Regelformular des Übergangsregel-Editors.
    private static readonly HashSet<string> FormFelder =
    [
        nameof(Name), nameof(Rundenart), nameof(EingangsMannschaftsanzahl), nameof(AusgangsMannschaftsanzahl),
        nameof(GruppenAnzahl), nameof(AnzahlSpieltage), nameof(MitRueckspiel), nameof(AuswaertstorRegelAktiv), nameof(NeutralerSpielort),
        nameof(Ueberraschungsfaktor),
        nameof(Regelwerk), nameof(PaarungFestVorgegeben), nameof(VorgegebeneAuslosungsart),
        nameof(StandardRanglistenQuelle), nameof(TitelverteidigerInTopf1)
    ];

    [ObservableProperty] private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRundenart))]
    [NotifyPropertyChangedFor(nameof(IstSchweizerSystem))]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    [NotifyPropertyChangedFor(nameof(RundenartAuswahl))]
    private Rundenart _rundenart = Rundenart.Liga;

    // ── Auswahl-Reset beim ersten Laden des Formulars ────────────────────────
    // Wird die Seite neu erzeugt (Wechsel aus einem anderen Editor), bekommen die Formular-ComboBoxen ihre
    // Auswahl, bevor sie geladen sind — beim ersten Laden verwirft WinUI diese Auswahl und schreibt über die
    // TwoWay-Bindung null zurück (per Diagnose belegt, #349-Nachtest: SelectionChanged Removed=[KoRunde],
    // Index=-1, erst danach Loaded). Beim Enum Rundenart kam das als Liga (= 0) an und war von einer echten
    // Auswahl nicht zu unterscheiden — die Runde wechselte stillschweigend die Rundenart. Deshalb bindet die
    // ComboBox an diese Nullable-Hülle: null wird verworfen und die Anzeige danach wiederhergestellt.
    public Rundenart? RundenartAuswahl
    {
        get => Rundenart;
        set
        {
            if (value is { } art) Rundenart = art;
            else NachComboBoxResetWiederherstellen(() => OnPropertyChanged(nameof(RundenartAuswahl)));
        }
    }

    /// <summary>Stellt eine vom ComboBox-Reset verworfene Auswahl nach dem Laden wieder her — auf niedriger
    /// Priorität, damit die Wiederherstellung erst nach dem laufenden Lade-/Layoutdurchgang greift.</summary>
    private void NachComboBoxResetWiederherstellen(Action wiederherstellen) =>
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _populiertGerade++;
                try { wiederherstellen(); }
                finally { _populiertGerade--; }
            });

    partial void OnVorgegebeneAuslosungsartChanged(VorgegebeneAuslosungsartOption value)
    {
        // Siehe RundenartAuswahl: null ist nie eine Nutzerauswahl; die Rundenzeile hält weiter den richtigen Wert.
        if (value is null && _populiertGerade == 0 && _zeile is { } zeile)
            NachComboBoxResetWiederherstellen(() =>
            {
                if (_zeile == zeile && VorgegebeneAuslosungsart is null)
                    VorgegebeneAuslosungsart = zeile.VorgegebeneAuslosungsart;
            });

        if (value is not null && _populiertGerade == 0) RegelVorgabenAnRundePassen();
    }

    // ── Auslosungsregeln als Vorgabe (Issue #356) ────────────────────────────

    /// <summary>Alle anbietbaren Regeln, einmal erzeugt und nie ausgetauscht (siehe <see cref="RegelVorgabeOption"/>).</summary>
    public IReadOnlyList<RegelVorgabeOption> RegelVorgabeOptionen => _regelVorgabeOptionen ??= RegelVorgabeOptionenErstellen();
    private IReadOnlyList<RegelVorgabeOption>? _regelVorgabeOptionen;

    [ObservableProperty] private bool _hatRegelVorgabeOptionen;

    private IReadOnlyList<RegelVorgabeOption> RegelVorgabeOptionenErstellen()
    {
        var optionen = AuslosungsRegelKompatibilitaet.AngeboteneRegeln
            .Select(art => new RegelVorgabeOption(
                art, AuslosungsRegelKompatibilitaet.AnzeigeName(art), AuslosungAnzeigeTexte.RegelBeschreibung(art)))
            .ToList();
        foreach (var option in optionen)
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RegelVorgabeOption.IstAktiv)) RegelVorgabeGeaendert(option);
            };
        return optionen;
    }

    private bool RegelVorgabeMoeglich(AuslosungsRegelArt regel)
    {
        if (_zeile is null) return false;
        return AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            regel, Rundenart, VorgegebeneAuslosungsart?.Wert, _folgtAufGruppenphase);
    }

    /// <summary>Formular befüllen: Verfügbarkeit und Häkchen aus der gewählten Runde übernehmen.</summary>
    private void RegelVorgabenAusZeile(RundeZeile? zeile)
    {
        foreach (var option in RegelVorgabeOptionen)
        {
            option.IstVerfuegbar = RegelVorgabeMoeglich(option.Regelart);
            option.IstAktiv = option.IstVerfuegbar && zeile?.VorgegebeneAuslosungsregeln.Contains(option.Regelart) == true;
        }
        HatRegelVorgabeOptionen = RegelVorgabeOptionen.Any(o => o.IstVerfuegbar);
    }

    /// <summary>Nach einem Wechsel von Rundenart oder Auslosungsart-Vorgabe: Regeln, die dort nicht wirken können,
    /// verschwinden samt Häkchen — Prävention statt einer wirkungslosen Vorgabe.</summary>
    private void RegelVorgabenAnRundePassen()
    {
        _populiertGerade++;
        try
        {
            foreach (var option in RegelVorgabeOptionen)
            {
                option.IstVerfuegbar = RegelVorgabeMoeglich(option.Regelart);
                if (!option.IstVerfuegbar) option.IstAktiv = false;
            }
        }
        finally { _populiertGerade--; }
        HatRegelVorgabeOptionen = RegelVorgabeOptionen.Any(o => o.IstVerfuegbar);
        RegelVorgabenInZeileSchreiben();
    }

    private void RegelVorgabeGeaendert(RegelVorgabeOption option)
    {
        if (_populiertGerade > 0) return;

        // Die Heimrecht-Regeln schließen sich aus: Anhaken der einen hakt die andere ab (wie in der Auslosung, #331).
        if (option.IstAktiv)
        {
            _populiertGerade++;
            try
            {
                foreach (var andere in RegelVorgabeOptionen.Where(o =>
                             o.IstAktiv && AuslosungsRegelKompatibilitaet.SchliessenSichAus(option.Regelart, o.Regelart)))
                    andere.IstAktiv = false;
            }
            finally { _populiertGerade--; }
        }
        RegelVorgabenInZeileSchreiben();
    }

    private void RegelVorgabenInZeileSchreiben()
    {
        if (_zeile is not { } zeile) return;
        var aktiv = RegelVorgabeOptionen.Where(o => o.IstAktiv).Select(o => o.Regelart).ToList();
        if (aktiv.SequenceEqual(zeile.VorgegebeneAuslosungsregeln)) return;
        zeile.VorgegebeneAuslosungsregeln = aktiv;
        Geaendert?.Invoke();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzahlSpieltageVorschlag))]
    [NotifyPropertyChangedFor(nameof(GruppenAnzahlVorschlaege))]
    [NotifyPropertyChangedFor(nameof(HatGruppenAnzahlVorschlaege))]
    private int _eingangsMannschaftsanzahl = 8;

    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private int? _gruppenAnzahl;
    [ObservableProperty] private int? _anzahlSpieltage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private bool _mitRueckspiel;

    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private bool _neutralerSpielort;
    [ObservableProperty] private int _ueberraschungsfaktor;
    [ObservableProperty] private RegelwerkOption _regelwerk = RundeZeile.KeinRegelwerk;
    [ObservableProperty] private bool _paarungFestVorgegeben;
    [ObservableProperty] private VorgegebeneAuslosungsartOption _vorgegebeneAuslosungsart = RundeZeile.KeineAuslosungsartVorgabe;

    // ── Standard-Rangliste für die Lostopf-Vorbelegung (Issue #349) ──────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitelverteidigerInTopf1Aktivierbar))]
    private RanglistenQuelleOption _standardRanglistenQuelle = RundeZeile.KeineRanglistenQuelle;

    [ObservableProperty] private bool _titelverteidigerInTopf1;

    /// <summary>Einmal beim ersten Laden befüllt (analog <see cref="Regelwerke"/>), danach stabil — siehe
    /// <see cref="RanglistenQuelleOptionFuer"/>.</summary>
    [ObservableProperty]
    private IReadOnlyList<RanglistenQuelleOption> _ranglistenQuelleOptionen = [RundeZeile.KeineRanglistenQuelle];

    public bool TitelverteidigerInTopf1Aktivierbar => StandardRanglistenQuelle?.Wert is not null;

    partial void OnStandardRanglistenQuelleChanged(RanglistenQuelleOption value)
    {
        // Ohne Standard-Rangliste gibt es keine automatische Topfbildung, in die der Titelverteidiger käme.
        // value == null kommt nur transient aus der ComboBox (ItemsSource-Wechsel) und ist keine Nutzerauswahl.
        if (_populiertGerade == 0 && value is not null && value.Wert is null) TitelverteidigerInTopf1 = false;

        // Siehe RundenartAuswahl.
        if (value is null && _populiertGerade == 0 && _zeile is { } zeile)
            NachComboBoxResetWiederherstellen(() =>
            {
                if (_zeile == zeile && StandardRanglistenQuelle is null)
                    StandardRanglistenQuelle = zeile.StandardRanglistenQuelle;
            });
    }

    /// <summary>Genau eine Option je Quelle (Referenzabgleich der ComboBox). Eine nicht (mehr) bekannte Quelle
    /// erscheint als "keine".</summary>
    public RanglistenQuelleOption RanglistenQuelleOptionFuer(int? quelleId) =>
        RanglistenQuelleOptionen.FirstOrDefault(o => o.Wert == quelleId && quelleId is not null) ?? RundeZeile.KeineRanglistenQuelle;

    /// <summary>Nur zur Rundenart kompatible Auslosungsarten (Issue #347, Prävention statt Speicher-Validierung).
    /// Gecacht und nur bei Rundenart-Wechsel neu gesetzt statt pro Binding-Zugriff neu berechnet — eine
    /// dauerhaft neue ItemsSource-Instanz ist ein bekannter WinUI3-ComboBox-Fallstrick (WinUi/CLAUDE.md).</summary>
    [ObservableProperty]
    private IReadOnlyList<VorgegebeneAuslosungsartOption> _vorgegebeneAuslosungsartOptionen = AuslosungsartVorgabeOptionenFuer(Rundenart.Liga);

    /// <summary>Genau eine Instanz je Auslosungsart: die WinUI3-ComboBox gleicht <c>SelectedItem</c> per Referenz
    /// (nicht per Record-Gleichheit) mit der ItemsSource ab — mit jeweils neuen Instanzen fand sie die Auswahl
    /// nach einem ItemsSource-Wechsel nicht mehr und schrieb über TwoWay null zurück (NullReferenceException).</summary>
    private static readonly Dictionary<Auslosungsart, VorgegebeneAuslosungsartOption> _auslosungsartVorgabeOptionen =
        Enum.GetValues<Auslosungsart>().ToDictionary(
            a => a, a => new VorgegebeneAuslosungsartOption(AuslosungAnzeigeTexte.AuslosungsartAnzeigeName(a), a));

    // Manuell wird nicht angeboten — das regelt die Checkbox "Paarung fest vorgegeben".
    private static IReadOnlyList<VorgegebeneAuslosungsartOption> AuslosungsartVorgabeOptionenFuer(Rundenart rundenart) =>
    [
        RundeZeile.KeineAuslosungsartVorgabe,
        .. Enum.GetValues<Auslosungsart>()
            .Where(a => a != Auslosungsart.Manuell && AuslosungsKompatibilitaet.IstKompatibel(rundenart, a))
            .Select(a => _auslosungsartVorgabeOptionen[a])
    ];

    public static VorgegebeneAuslosungsartOption AuslosungsartVorgabeOptionFuer(Auslosungsart? art) =>
        art is { } a ? _auslosungsartVorgabeOptionen[a] : RundeZeile.KeineAuslosungsartVorgabe;

    public bool IstGruppenphase => Rundenart == Rundenart.Gruppenphase;
    public bool IstSchweizerSystem => Rundenart == Rundenart.SchweizerSystem;

    /// <summary>Fachlicher Standard-Vorschlag ceil(log2(n)) für <see cref="AnzahlSpieltage"/> (Issue #329 Teil D) —
    /// nur eine Anzeige-Hilfe, <see cref="AnzahlSpieltage"/> bleibt frei editierbar.</summary>
    public int AnzahlSpieltageVorschlag => RundenKonfiguration.VorschlagAnzahlSpieltage(EingangsMannschaftsanzahl);

    /// <summary>Gruppenanzahl/Gruppengröße-Kombinationen zur Auswahl statt blinder Eingabe (Issue #215) —
    /// nur bei Gruppenphase relevant, aus den Teilern von <see cref="EingangsMannschaftsanzahl"/> abgeleitet.</summary>
    public IReadOnlyList<GruppenAnzahlVorschlag> GruppenAnzahlVorschlaege => RundenKonfiguration
        .GruppenAnzahlVorschlaege(EingangsMannschaftsanzahl)
        .Select(v => new GruppenAnzahlVorschlag(v.GruppenAnzahl, v.Gruppengroesse))
        .ToList();

    public bool HatGruppenAnzahlVorschlaege => GruppenAnzahlVorschlaege.Count > 0;

    [RelayCommand]
    private void GruppenAnzahlVorschlagWaehlen(GruppenAnzahlVorschlag vorschlag) => GruppenAnzahl = vorschlag.GruppenAnzahl;

    /// <summary>Die Auswärtstorregel ergibt nur bei Hin-/Rückspiel-Aggregaten in K.o.-Rundenarten Sinn
    /// (AD-001) — analog WPF-Original <c>RundenEditorViewModel.AuswaertstorAktivierbar</c>.</summary>
    public bool IstKoRundenart => Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
    public bool AuswaertstorAktivierbar => MitRueckspiel && IstKoRundenart;

    partial void OnMitRueckspielChanged(bool value)
    {
        if (_populiertGerade > 0) return;
        if (!AuswaertstorAktivierbar) AuswaertstorRegelAktiv = false;
    }

    partial void OnRundenartChanged(Rundenart value)
    {
        // Vor dem Populier-Guard: die Optionsliste muss auch beim Befüllen des Formulars zur Rundenart passen.
        // Erst den Zielwert, dann die ItemsSource setzen (siehe AuslosungViewModel.OnNeuRundeChanged).
        if (VorgegebeneAuslosungsart?.Wert is { } vorgabe &&!AuslosungsKompatibilitaet.IstKompatibel(value, vorgabe))
            VorgegebeneAuslosungsart = RundeZeile.KeineAuslosungsartVorgabe;
        VorgegebeneAuslosungsartOptionen = AuslosungsartVorgabeOptionenFuer(value);
        OnPropertyChanged(nameof(VorgegebeneAuslosungsart));

        if (_populiertGerade > 0) return;
        RegelVorgabenAnRundePassen();
        if (!AuswaertstorAktivierbar) AuswaertstorRegelAktiv = false;
        // Nur ein Vorschlag (Normalfall: nur Sieger steigen auf) — bleibt frei editierbar, siehe Issue #334:
        // der K.o.-Split-Sonderfall (Sieger UND Verlierer steigen in getrennte Zielrunden auf) braucht die
        // volle Eingangsmannschaftsanzahl statt der Hälfte.
        if (IstKoRundenart) AusgangsMannschaftsanzahl = Math.Max(1, EingangsMannschaftsanzahl / 2);
        if (value == Rundenart.SchweizerSystem && AnzahlSpieltage is null) AnzahlSpieltage = AnzahlSpieltageVorschlag;
    }

    partial void OnEingangsMannschaftsanzahlChanged(int value)
    {
        if (_populiertGerade > 0) return;
        if (IstKoRundenart) AusgangsMannschaftsanzahl = Math.Max(1, value / 2);
        if (Rundenart == Rundenart.SchweizerSystem && AnzahlSpieltage is null) AnzahlSpieltage = AnzahlSpieltageVorschlag;
    }

    private void FormularAusFuellenMit(RundeZeile? zeile)
    {
        _populiertGerade++;
        try
        {
            Name = zeile?.Name ?? "";
            Rundenart = zeile?.Rundenart ?? Rundenart.Liga;
            EingangsMannschaftsanzahl = zeile?.EingangsMannschaftsanzahl ?? 8;
            AusgangsMannschaftsanzahl = zeile?.AusgangsMannschaftsanzahl ?? 4;
            GruppenAnzahl = zeile?.GruppenAnzahl;
            AnzahlSpieltage = zeile?.AnzahlSpieltage;
            MitRueckspiel = zeile?.MitRueckspiel ?? false;
            AuswaertstorRegelAktiv = zeile?.AuswaertstorRegelAktiv ?? false;
            NeutralerSpielort = zeile?.NeutralerSpielort ?? false;
            Ueberraschungsfaktor = zeile?.Ueberraschungsfaktor ?? 0;
            Regelwerk = zeile?.Regelwerk ?? RundeZeile.KeinRegelwerk;
            PaarungFestVorgegeben = zeile?.PaarungFestVorgegeben ?? false;
            VorgegebeneAuslosungsart = zeile?.VorgegebeneAuslosungsart ?? RundeZeile.KeineAuslosungsartVorgabe;
            StandardRanglistenQuelle = zeile?.StandardRanglistenQuelle ?? RundeZeile.KeineRanglistenQuelle;
            TitelverteidigerInTopf1 = zeile?.TitelverteidigerInTopf1 ?? false;
            RegelVorgabenAusZeile(zeile);
        }
        finally
        {
            _populiertGerade--;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade > 0 || e.PropertyName is null || _zeile is null) return;
        if (!FormFelder.Contains(e.PropertyName)) return;

        _zeile.Name = Name;
        _zeile.Rundenart = Rundenart;
        _zeile.EingangsMannschaftsanzahl = EingangsMannschaftsanzahl;
        _zeile.AusgangsMannschaftsanzahl = AusgangsMannschaftsanzahl;
        _zeile.GruppenAnzahl = GruppenAnzahl;
        _zeile.AnzahlSpieltage = AnzahlSpieltage;
        _zeile.MitRueckspiel = MitRueckspiel;
        _zeile.AuswaertstorRegelAktiv = AuswaertstorRegelAktiv;
        _zeile.NeutralerSpielort = NeutralerSpielort;
        _zeile.Ueberraschungsfaktor = Ueberraschungsfaktor;
        _zeile.Regelwerk = Regelwerk;
        _zeile.PaarungFestVorgegeben = PaarungFestVorgegeben;
        // null kommt nur transient aus der ComboBox (Auswahl beim ItemsSource-Wechsel nicht gefunden) — nie übernehmen.
        if (VorgegebeneAuslosungsart is not null)
            _zeile.VorgegebeneAuslosungsart = VorgegebeneAuslosungsart;
        if (StandardRanglistenQuelle is not null)
            _zeile.StandardRanglistenQuelle = StandardRanglistenQuelle;
        _zeile.TitelverteidigerInTopf1 = TitelverteidigerInTopf1;

        Geaendert?.Invoke();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [RundeZeile.KeinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();
}
