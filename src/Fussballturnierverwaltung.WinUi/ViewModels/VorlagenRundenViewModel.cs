using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Application.Turniere.Eingaben;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record RegelwerkOption(string Anzeige, Regelwerk? Wert);

/// <summary>Eine Zeile innerhalb eines <see cref="VorlagenUebergangsregelBlock"/> — je Gruppe (Gruppenphase-
/// Quellrunde), je Spiel (K.o.-Quellrunde) oder eine einzelne Zeile (Liga-Quellrunde, <see cref="GruppeIndex"/>
/// = null). <see cref="AnzahlBeste"/> (gruppenübergreifende Rangfolge-Regel, Issue #217) wird in dieser ersten
/// Etappe nicht über die UI gesetzt, bleibt aber im Modell für eine spätere Erweiterung erhalten.</summary>
public partial class VorlagenUebergangsregelZeile : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstSieger))]
    [NotifyPropertyChangedFor(nameof(IstVerlierer))]
    private int? _gruppeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstSieger))]
    [NotifyPropertyChangedFor(nameof(IstVerlierer))]
    private int _platzierungVon = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstSieger))]
    [NotifyPropertyChangedFor(nameof(IstVerlierer))]
    private int _platzierungBis = 1;

    [ObservableProperty] private int? _anzahlBeste;

    public string GruppenLabel => AnzahlBeste is { } n
        ? $"Beste {n}"
        : GruppeIndex is null ? "—" : $"Gruppe {(char)('A' + GruppeIndex.Value - 1)}";

    // K.o.-Quellrunde: pro Spiel kann immer nur Sieger ODER Verlierer weiterziehen, daher exklusiv
    // (radiobutton-artig) statt unabhängig — Abwahl ohne Gegenauswahl wird ignoriert (Checkbox springt
    // zurück), analog WPF-Original RundenEditorViewModel.UebergangsregelZeile.
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
    }
}

/// <summary>Eine Übergangsregel (Quelle→Ziel + Detailzeilen) — zentrale Datenhaltung in
/// <see cref="VorlagenRundenViewModel"/>.AlleUebergaenge, gemeinsam genutzt von den Tabs "Runden" und
/// "Rundenübergänge" (<see cref="VorlagenRundenUebergaengeViewModel"/>). Bearbeitet wird sie AUSSCHLIESSLICH
/// über das stabile Regelformular in <see cref="VorlagenRundenUebergaengeViewModel"/> (Populier-/Zurückschreib-
/// Muster) — dieser Typ selbst enthält keine TwoWay-Bindungspunkte für Quelle/Ziel (siehe #284-Redesign-
/// Historie: eine frühere Fassung mit eingebetteter ComboBox pro Karte hat bei jedem Rundenfokus-Wechsel
/// wiederholt zu Datenkorruption geführt, weil die Karte selbst bei jedem Wechsel neu aufgebaut wurde). Nur
/// die einzelnen <see cref="Zeilen"/> (PlatzierungVon/Bis, Sieger/Verlierer) bleiben direkt TwoWay-editierbar —
/// das ist unproblematisch, da sie in TextBoxen/CheckBoxen ohne eigenen Selektions-State liegen.</summary>
public partial class VorlagenUebergangsregelBlock : ObservableObject
{
    private bool _wirdInitialisiert;

    /// <summary>Feuert bei jeder inhaltlichen Änderung (Quelle/Ziel/Zeilen) — <see cref="VorlagenRundenViewModel"/>
    /// aktualisiert daraufhin IsDirty, die Validierung und feuert <see cref="VorlagenRundenViewModel.UebergaengeGeaendert"/>.</summary>
    public event Action? Geaendert;

    [ObservableProperty] private VorlagenRundeZeile? _quellRunde;
    [ObservableProperty] private VorlagenRundeZeile? _zielRunde;
    [ObservableProperty] private ObservableCollection<VorlagenUebergangsregelZeile> _zeilen = [];

    /// <summary>Vom Übergänge-Tab gesetzt, wenn dieser Block gerade im Regelformular bearbeitet wird — reine
    /// Anzeige-Markierung für die Hervorhebung in den Eingehend-/Ausgehend-Listen, keine Nutzereingabe.</summary>
    [ObservableProperty] private bool _istAusgewaehlt;

    public static VorlagenUebergangsregelBlock NeuLeer(VorlagenRundeZeile? quelle, VorlagenRundeZeile? ziel)
    {
        var block = new VorlagenUebergangsregelBlock();
        block._wirdInitialisiert = true;
        try
        {
            block.QuellRunde = quelle;
            block.ZielRunde = ziel;
        }
        finally
        {
            block._wirdInitialisiert = false;
        }
        if (quelle is not null) block.BefuelleStandardZeilen();
        return block;
    }

    public static VorlagenUebergangsregelBlock AusBestehendenZeilenErstellen(
        VorlagenRundeZeile quelle, VorlagenRundeZeile ziel, IEnumerable<VorlagenUebergangsregelZeile> zeilen)
    {
        var block = new VorlagenUebergangsregelBlock();
        block._wirdInitialisiert = true;
        try
        {
            block.QuellRunde = quelle;
            block.ZielRunde = ziel;
            block.Zeilen = new ObservableCollection<VorlagenUebergangsregelZeile>(zeilen);
        }
        finally
        {
            block._wirdInitialisiert = false;
        }
        foreach (var z in block.Zeilen)
            z.PropertyChanged += (_, __) => block.Geaendert?.Invoke();
        return block;
    }

    partial void OnQuellRundeChanged(VorlagenRundeZeile? value)
    {
        if (!_wirdInitialisiert) Geaendert?.Invoke();
        if (_wirdInitialisiert || value is null || Zeilen.Count > 0) return;
        BefuelleStandardZeilen();
    }

    partial void OnZielRundeChanged(VorlagenRundeZeile? value)
    {
        if (!_wirdInitialisiert) Geaendert?.Invoke();
    }

    private void BefuelleStandardZeilen()
    {
        var quelle = QuellRunde;
        if (quelle is null) return;

        void ZeileHinzufuegen(int? gruppeIndex, int von, int bis)
        {
            var zeile = new VorlagenUebergangsregelZeile { GruppeIndex = gruppeIndex, PlatzierungVon = von, PlatzierungBis = bis };
            zeile.PropertyChanged += (_, __) => Geaendert?.Invoke();
            Zeilen.Add(zeile);
        }

        if (quelle.Rundenart == Rundenart.Gruppenphase && quelle.GruppenAnzahl is > 0)
        {
            var bis = Math.Max(1, quelle.AusgangsMannschaftsanzahl / quelle.GruppenAnzahl.Value);
            for (int i = 1; i <= quelle.GruppenAnzahl.Value; i++)
                ZeileHinzufuegen(i, 1, bis);
        }
        else if (quelle.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei)
        {
            var anzahlSpiele = Math.Max(1, quelle.EingangsMannschaftsanzahl / 2);
            for (int i = 1; i <= anzahlSpiele; i++)
                ZeileHinzufuegen(i, 1, 1);
        }
        else
        {
            ZeileHinzufuegen(null, 1, 1);
        }
        Geaendert?.Invoke();
    }
}

/// <summary>Ein fest zugewiesener Platzhalter-Slot innerhalb einer <see cref="VorlagenRundeZeile"/> (Issue
/// #277) — Spiegel eines <see cref="VorlagenPaarung"/>-Eintrags. Wird sowohl als Datenhaltung auf
/// <see cref="VorlagenRundeZeile.Paarungen"/> als auch als Anzeige-/Bearbeitungszeile im "Paarungen"-Tab
/// (<see cref="VorlagenPaarungenViewModel"/>) verwendet. <see cref="PaarungsNummer"/>/<see cref="SlotNr"/>
/// sind Identität, nicht editierbar — nur <see cref="PlatzhalterBezeichnung"/> ändert sich per Formular.</summary>
public partial class VorlagenPaarungSlot : ObservableObject
{
    public required int PaarungsNummer { get; init; }
    public required int SlotNr { get; init; }
    public required string Label { get; init; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstBesetzt))]
    [NotifyPropertyChangedFor(nameof(AnzeigeText))]
    private string? _platzhalterBezeichnung;
    public bool IstBesetzt => !string.IsNullOrEmpty(PlatzhalterBezeichnung);
    public string AnzeigeText => IstBesetzt ? PlatzhalterBezeichnung! : "— nicht zugewiesen —";
}

/// <summary>ViewModel-Zeile für die Rundenliste — Spiegel einer <see cref="VorlagenRunde"/>, reiner UI-Zustand
/// bis zum nächsten Speichern (analog dem WPF-Original <c>VorlagenRundeZeile</c>). Bewusst ein reiner
/// Datenträger ohne eigene Änderungs-/Berechnungslogik — die Formulare binden NICHT direkt auf diese Instanz,
/// sondern nur die Rundenliste, die Perlenkette-Karten und die (nur lesenden) Übergangsregel-Zeilen
/// referenzieren sie per Objektidentität.</summary>
public partial class VorlagenRundeZeile : ObservableObject
{
    public static readonly RegelwerkOption KeinRegelwerk = new("— Turnierstandard —", null);

    public int? Id { get; set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private int? _gruppenAnzahl;
    [ObservableProperty] private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private RegelwerkOption _regelwerk = KeinRegelwerk;
    [ObservableProperty] private bool _paarungFestVorgegeben;

    /// <summary>Feste Paarungszuweisungen dieser Runde (Issue #277) — vom "Paarungen"-Tab gepflegt, hier nur
    /// als Datenhaltung zwischen Laden und Speichern (analog <see cref="VorlagenRundenViewModel.AlleUebergaenge"/>
    /// für Übergangsregeln, nur pro Runde statt global, da eine Paarung nie rundenübergreifend ist).</summary>
    public ObservableCollection<VorlagenPaarungSlot> Paarungen { get; set; } = [];
}

/// <summary>"Runden"-Tab von <see cref="TurniervorlagenViewModel"/> (#284) — Rundenliste + Rundenformular (nur
/// Rundendaten: Name/Rundenart/Eingang/Ausgang/GruppenAnzahl/MitRueckspiel/AuswaertstorRegelAktiv/Regelwerk).
/// Zugleich alleiniger DATENHALTER für Runden UND Übergangsregeln (<see cref="AlleUebergaenge"/>) — der
/// separate Tab "Rundenübergänge" (<see cref="VorlagenRundenUebergaengeViewModel"/>) referenziert dieses
/// ViewModel per DI (beide Scoped in derselben Navigations-Scope) und liest/schreibt die geteilten Daten über
/// die öffentlichen Methoden/Property unten, statt eigene Kopien zu halten — EIN "Speichern" committet Runden
/// UND Übergangsregeln gemeinsam über <see cref="TurnierVorlageService.RundenSynchronisierenAsync"/>,
/// unabhängig davon, welcher der beiden Tabs gerade aktiv ist.</summary>
public partial class VorlagenRundenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierVorlageService _service;
    private readonly RegelwerkService _regelwerkService;
    private readonly TurnierVorlageValidator _vorlageValidator;
    private readonly VorlagenUebergangsregelValidator _uebergangsregelValidator;
    private readonly VorlagenPaarungValidator _paarungValidator;
    private int _populiertGerade;

    // Reentrancy-Guard: Vorlage-Id, deren Ladevorgang gerade läuft — verhindert, dass ein schneller
    // Vorlagenwechsel (Klick A, sofort Klick B) den älteren, noch laufenden Ladevorgang anwendet.
    private int _ladevorgangLaufendFuer = -1;

    private readonly List<VorlagenUebergangsregelBlock> _alleUebergaenge = [];

    /// <summary>Geteilte Übergangsregel-Datenhaltung, gelesen von <see cref="VorlagenRundenUebergaengeViewModel"/>.</summary>
    public IReadOnlyList<VorlagenUebergangsregelBlock> AlleUebergaenge => _alleUebergaenge;

    /// <summary>Feuert bei jeder strukturellen (hinzugefügt/entfernt) oder inhaltlichen (Quelle/Ziel/Zeilen)
    /// Änderung an <see cref="AlleUebergaenge"/> sowie bei jeder Rundendaten-Änderung, die die Perlenkette-
    /// Anzeige betrifft (Name/Eingang/Ausgang) — <see cref="VorlagenRundenUebergaengeViewModel"/> abonniert
    /// dies, um Perlenkette/Eingehend-/Ausgehend-Listen zu aktualisieren.</summary>
    public event Action? UebergaengeGeaendert;

    /// <summary>Feuert <see cref="UebergaengeGeaendert"/> und aktualisiert die davon abhängigen, aber nicht
    /// automatisch benachrichtigten Properties (Issue #277: <see cref="PaarungFestVorgegebenVerfuegbar"/>
    /// hängt an <see cref="AlleUebergaenge"/>, das kein eigenes PropertyChanged auslöst).</summary>
    private void UebergaengeGeaendertMelden()
    {
        OnPropertyChanged(nameof(PaarungFestVorgegebenVerfuegbar));
        UebergaengeGeaendert?.Invoke();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatVorlage))]
    [NotifyCanExecuteChangedFor(nameof(NeueRundeCommand))]
    private TurnierVorlage? _aktuelleVorlage;

    public bool HatVorlage => AktuelleVorlage is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    private VorlagenRundeZeile? _ausgewaehlteRunde;

    partial void OnAusgewaehlteRundeChanged(VorlagenRundeZeile? value)
    {
        FormularAusFuellenMit(value);
        OnPropertyChanged(nameof(PaarungFestVorgegebenVerfuegbar));
    }

    public bool FormularSichtbar => AusgewaehlteRunde is not null;

    // ── Rundenformular ────────────────────────────────────────────────────────
    // Bewusst VM-Felder statt Direktbindung auf AusgewaehlteRunde.<Feld> (siehe #284-Umsetzungsnotiz für die
    // ausführliche Begründung: eine Direktbindung auf ein bei Fokuswechsel wechselndes Objekt hat wiederholt
    // zu Datenkorruption geführt). Dasselbe Muster gilt im Regelformular des Übergänge-Tabs.
    private static readonly HashSet<string> FormFelder =
    [
        nameof(Name), nameof(Rundenart), nameof(EingangsMannschaftsanzahl), nameof(AusgangsMannschaftsanzahl),
        nameof(GruppenAnzahl), nameof(MitRueckspiel), nameof(AuswaertstorRegelAktiv), nameof(Regelwerk),
        nameof(PaarungFestVorgegeben)
    ];

    [ObservableProperty] private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRundenart))]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    [NotifyPropertyChangedFor(nameof(AusgangEditierbar))]
    private Rundenart _rundenart = Rundenart.Liga;

    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private int? _gruppenAnzahl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private bool _mitRueckspiel;

    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private RegelwerkOption _regelwerk = VorlagenRundeZeile.KeinRegelwerk;
    [ObservableProperty] private bool _paarungFestVorgegeben;

    public bool IstGruppenphase => Rundenart == Rundenart.Gruppenphase;

    /// <summary>Feste Paarungen (Issue #277) ergeben nur für eine Runde mit mindestens einer eingehenden
    /// Übergangsregel Sinn — die erste Runde einer Vorlage hat noch keine Platzhalter-Basis und durchläuft in
    /// jedem realen Turnier zwingend eine echte Auslosung (siehe VorlagenPaarungValidator).</summary>
    public bool PaarungFestVorgegebenVerfuegbar =>
        AusgewaehlteRunde is not null && AlleUebergaenge.Any(b => b.ZielRunde == AusgewaehlteRunde);

    /// <summary>Die Auswärtstorregel ergibt nur bei Hin-/Rückspiel-Aggregaten in K.o.-Rundenarten Sinn
    /// (AD-001) — analog WPF-Original <c>RundenEditorViewModel.AuswaertstorAktivierbar</c>.</summary>
    public bool IstKoRundenart => Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
    public bool AuswaertstorAktivierbar => MitRueckspiel && IstKoRundenart;

    /// <summary>Ausgang ist bei K.o.-Rundenarten kein frei wählbarer Wert, sondern immer Eingang/2 (Issue
    /// #182, analog WPF-Original) — bei Liga/Gruppenphase/Sonderrunde bleibt es frei editierbar.</summary>
    public bool AusgangEditierbar => !IstKoRundenart;

    partial void OnMitRueckspielChanged(bool value)
    {
        if (_populiertGerade > 0) return;
        if (!AuswaertstorAktivierbar) AuswaertstorRegelAktiv = false;
    }

    partial void OnRundenartChanged(Rundenart value)
    {
        if (_populiertGerade > 0) return;
        if (!AuswaertstorAktivierbar) AuswaertstorRegelAktiv = false;
        if (IstKoRundenart) AusgangsMannschaftsanzahl = Math.Max(1, EingangsMannschaftsanzahl / 2);
    }

    partial void OnEingangsMannschaftsanzahlChanged(int value)
    {
        if (_populiertGerade > 0) return;
        if (IstKoRundenart) AusgangsMannschaftsanzahl = Math.Max(1, value / 2);
    }

    private void FormularAusFuellenMit(VorlagenRundeZeile? zeile)
    {
        _populiertGerade++;
        try
        {
            Name = zeile?.Name ?? "";
            Rundenart = zeile?.Rundenart ?? Rundenart.Liga;
            EingangsMannschaftsanzahl = zeile?.EingangsMannschaftsanzahl ?? 8;
            AusgangsMannschaftsanzahl = zeile?.AusgangsMannschaftsanzahl ?? 4;
            GruppenAnzahl = zeile?.GruppenAnzahl;
            MitRueckspiel = zeile?.MitRueckspiel ?? false;
            AuswaertstorRegelAktiv = zeile?.AuswaertstorRegelAktiv ?? false;
            Regelwerk = zeile?.Regelwerk ?? VorlagenRundeZeile.KeinRegelwerk;
            PaarungFestVorgegeben = zeile?.PaarungFestVorgegeben ?? false;
        }
        finally
        {
            _populiertGerade--;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade > 0 || e.PropertyName is null || AusgewaehlteRunde is null) return;
        if (!FormFelder.Contains(e.PropertyName)) return;

        AusgewaehlteRunde.Name = Name;
        AusgewaehlteRunde.Rundenart = Rundenart;
        AusgewaehlteRunde.EingangsMannschaftsanzahl = EingangsMannschaftsanzahl;
        AusgewaehlteRunde.AusgangsMannschaftsanzahl = AusgangsMannschaftsanzahl;
        AusgewaehlteRunde.GruppenAnzahl = GruppenAnzahl;
        AusgewaehlteRunde.MitRueckspiel = MitRueckspiel;
        AusgewaehlteRunde.AuswaertstorRegelAktiv = AuswaertstorRegelAktiv;
        AusgewaehlteRunde.Regelwerk = Regelwerk;

        // Wird das Flag deaktiviert, sind vorhandene Zuweisungen verwaist (VorlagenPaarungValidator lehnt
        // Paarungen an einer nicht als fest markierten Runde ab) — konsistent leeren statt stillen Fehler
        // erst beim Speichern zu produzieren.
        if (AusgewaehlteRunde.PaarungFestVorgegeben != PaarungFestVorgegeben)
        {
            AusgewaehlteRunde.PaarungFestVorgegeben = PaarungFestVorgegeben;
            if (!PaarungFestVorgegeben) AusgewaehlteRunde.Paarungen.Clear();
        }

        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden(); // Name/Eingang/Ausgang betreffen die Perlenkette-Kartenanzeige
    }

    [ObservableProperty] private ObservableCollection<VorlagenRundeZeile> _runden = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    [NotifyCanExecuteChangedFor(nameof(AbbrechenCommand))]
    private bool _isDirty;

    [ObservableProperty] private bool _istLadend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRundenFehler))]
    [NotifyPropertyChangedFor(nameof(ZeigeMeldungsSpalte))]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    private IReadOnlyList<string> _rundenFehler = [];

    public bool HatRundenFehler => RundenFehler.Count > 0;
    public bool ZeigeMeldungsSpalte => Fehlermeldung is not null || HatRundenFehler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [VorlagenRundeZeile.KeinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Runden";
    public ICommand? NeuBefehl => NeueRundeCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => RundeEntfernenCommand;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    public VorlagenRundenViewModel(
        TurnierVorlageService service, RegelwerkService regelwerkService,
        TurnierVorlageValidator vorlageValidator, VorlagenUebergangsregelValidator uebergangsregelValidator,
        VorlagenPaarungValidator paarungValidator)
    {
        _service = service;
        _regelwerkService = regelwerkService;
        _vorlageValidator = vorlageValidator;
        _uebergangsregelValidator = uebergangsregelValidator;
        _paarungValidator = paarungValidator;
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Vorlagenauswahl ändert — lädt Runden und
    /// Übergangsregeln der neuen Vorlage in einem Rutsch (<see cref="TurnierVorlageService.FindenNachIdMitRundenAsync"/>).</summary>
    public async Task VorlageSetzenAsync(TurnierVorlage? vorlage)
    {
        AktuelleVorlage = vorlage;
        var meineId = vorlage?.Id ?? -1;
        _ladevorgangLaufendFuer = meineId;

        if (vorlage is null)
        {
            RundenZuruecksetzen([]);
            return;
        }

        IstLadend = true;
        try
        {
            if (Regelwerke.Count == 0)
                Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());

            var mitRunden = await _service.FindenNachIdMitRundenAsync(vorlage.Id);
            if (_ladevorgangLaufendFuer != meineId) return;

            var vorlagenRunden = (mitRunden?.VorlagenRunden ?? []).OrderBy(r => r.Reihenfolge).ToList();
            var zeilen = vorlagenRunden.Select(RundeZuZeile).ToList();
            var zeileNachId = vorlagenRunden.Zip(zeilen, (r, z) => (r.Id, z)).ToDictionary(p => p.Id, p => p.z);

            RundenZuruecksetzen(zeilen);

            _populiertGerade++;
            try
            {
                var regelnMitQuelle = vorlagenRunden
                    .SelectMany(q => q.AusgehendeUebergangsregeln.Select(regel => (Quelle: q, Regel: regel)))
                    .Where(x => zeileNachId.ContainsKey(x.Regel.ZielVorlagenRundeId));

                foreach (var gruppe in regelnMitQuelle.GroupBy(x => (x.Quelle.Id, x.Regel.ZielVorlagenRundeId)))
                {
                    var quellZeile = zeileNachId[gruppe.Key.Id];
                    var zielZeile = zeileNachId[gruppe.Key.ZielVorlagenRundeId];
                    var blockZeilen = gruppe.OrderBy(x => x.Regel.GruppeIndex ?? 0).Select(x => new VorlagenUebergangsregelZeile
                    {
                        GruppeIndex = x.Regel.GruppeIndex,
                        PlatzierungVon = x.Regel.PlatzierungVon,
                        PlatzierungBis = x.Regel.PlatzierungBis,
                        AnzahlBeste = x.Regel.AnzahlBeste
                    });
                    var block = VorlagenUebergangsregelBlock.AusBestehendenZeilenErstellen(quellZeile, zielZeile, blockZeilen);
                    VerdrahteBlock(block);
                    _alleUebergaenge.Add(block);
                }
            }
            finally
            {
                _populiertGerade--;
            }

            AktualisiereRundenFehler();
            UebergaengeGeaendertMelden();
            IsDirty = false;
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

    private static VorlagenRundeZeile RundeZuZeile(VorlagenRunde r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Rundenart = r.Rundenart,
        EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
        GruppenAnzahl = r.GruppenAnzahl,
        MitRueckspiel = r.MitRueckspiel,
        AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv,
        Regelwerk = r.Regelwerk is not null ? new RegelwerkOption(r.Regelwerk.Name, r.Regelwerk) : VorlagenRundeZeile.KeinRegelwerk,
        PaarungFestVorgegeben = r.PaarungFestVorgegeben,
        Paarungen = new ObservableCollection<VorlagenPaarungSlot>(r.Paarungen.Select(p => new VorlagenPaarungSlot
        {
            PaarungsNummer = p.PaarungsNummer,
            SlotNr = p.SlotNr,
            Label = "", // wird beim Öffnen des Paarungen-Tabs aus Rundenart/Slot-Position neu ermittelt
            PlatzhalterBezeichnung = p.PlatzhalterBezeichnung
        }))
    };

    private void RundenZuruecksetzen(IEnumerable<VorlagenRundeZeile> runden)
    {
        _populiertGerade++;
        try
        {
            Runden = new ObservableCollection<VorlagenRundeZeile>(runden);
            _alleUebergaenge.Clear();
            AusgewaehlteRunde = null;
            IsDirty = false;
            Fehlermeldung = null;
        }
        finally
        {
            _populiertGerade--;
        }
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }

    // ── Runden-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannNeueRunde))]
    private void NeueRunde()
    {
        var letzte = Runden.LastOrDefault();
        var eingang = letzte?.AusgangsMannschaftsanzahl ?? 8;
        var zeile = new VorlagenRundeZeile { Name = $"Runde {Runden.Count + 1}", EingangsMannschaftsanzahl = eingang, AusgangsMannschaftsanzahl = eingang };
        Runden.Add(zeile);
        AusgewaehlteRunde = zeile;
        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }
    private bool KannNeueRunde() => HatVorlage;

    [RelayCommand(CanExecute = nameof(KannRundeEntfernen))]
    private void RundeEntfernen()
    {
        if (AusgewaehlteRunde is null) return;
        var entfernt = AusgewaehlteRunde;
        var idx = Runden.IndexOf(entfernt);
        Runden.Remove(entfernt);

        // Regeln, die auf die entfernte Runde verweisen, sind sonst verwaist.
        _alleUebergaenge.RemoveAll(b => b.QuellRunde == entfernt || b.ZielRunde == entfernt);

        AusgewaehlteRunde = Runden.Count > 0 ? Runden[Math.Max(0, idx - 1)] : null;
        AktualisiereRundenFehler();
        IsDirty = true;
        UebergaengeGeaendertMelden();
    }
    private bool KannRundeEntfernen() => AusgewaehlteRunde is not null;

    // ── Übergangsregel-Datenhaltung (öffentlich, vom Übergänge-Tab genutzt) ─────

    public void UebergangHinzufuegen(VorlagenUebergangsregelBlock block)
    {
        VerdrahteBlock(block);
        _alleUebergaenge.Add(block);
        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }

    public void UebergangEntfernen(VorlagenUebergangsregelBlock block)
    {
        _alleUebergaenge.Remove(block);
        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }

    private void VerdrahteBlock(VorlagenUebergangsregelBlock block)
    {
        block.Geaendert += () =>
        {
            if (_populiertGerade == 0) IsDirty = true;
            AktualisiereRundenFehler();
            UebergaengeGeaendertMelden();
        };
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    public List<VorlagenRundeEingabe> RundenZuEingaben() => Runden.Select(z => new VorlagenRundeEingabe(
        z.Name, z.Rundenart, z.EingangsMannschaftsanzahl, z.AusgangsMannschaftsanzahl,
        z.MitRueckspiel, z.AuswaertstorRegelAktiv, z.Regelwerk.Wert?.Id,
        z.Rundenart == Rundenart.Gruppenphase ? z.GruppenAnzahl : null,
        z.PaarungFestVorgegeben)).ToList();

    public List<VorlagenUebergangsRegelEingabe> UebergangsregelnZuEingaben() => _alleUebergaenge
        .Where(b => b.QuellRunde is not null && b.ZielRunde is not null && b.QuellRunde != b.ZielRunde)
        .SelectMany(b => b.Zeilen.Select(z => new VorlagenUebergangsRegelEingabe(
            Runden.IndexOf(b.QuellRunde!) + 1, Runden.IndexOf(b.ZielRunde!) + 1,
            z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste)))
        .ToList();

    /// <summary>Feste Paarungszuweisungen aller Runden (Issue #277) — nur belegte Slots, referenziert die
    /// Runde über ihre 1-basierte Listenposition analog <see cref="UebergangsregelnZuEingaben"/>.</summary>
    public List<VorlagenPaarungEingabe> PaarungenZuEingaben() => Runden
        .SelectMany((z, i) => z.Paarungen
            .Where(p => p.IstBesetzt)
            .Select(p => new VorlagenPaarungEingabe(i + 1, p.PaarungsNummer, p.SlotNr, p.PlatzhalterBezeichnung!)))
        .ToList();

    private void AktualisiereRundenFehler()
    {
        if (Runden.Count == 0) { RundenFehler = []; return; }
        var rundenEingaben = RundenZuEingaben();
        var uebergangsregelEingaben = UebergangsregelnZuEingaben();
        var fehler = new List<string>(_vorlageValidator.RundenValidieren(rundenEingaben).Fehler);
        fehler.AddRange(_uebergangsregelValidator.Validieren(rundenEingaben, uebergangsregelEingaben).Fehler);
        fehler.AddRange(_paarungValidator.Validieren(rundenEingaben, uebergangsregelEingaben, PaarungenZuEingaben()).Fehler);
        RundenFehler = fehler;
    }

    /// <summary>Vom "Paarungen"-Tab (<see cref="VorlagenPaarungenViewModel"/>) aufgerufen, sobald sich eine
    /// Slot-Zuweisung ändert — die Slots selbst liegen direkt auf <see cref="VorlagenRundeZeile.Paarungen"/>
    /// (kein eigener Datenbestand hier, analog Übergangsregeln in umgekehrter Datenhaltungs-Richtung).</summary>
    public void PaarungenGeaendert()
    {
        IsDirty = true;
        AktualisiereRundenFehler();
    }

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AktuelleVorlage is null) return;
        Fehlermeldung = null;
        try
        {
            await _service.RundenSynchronisierenAsync(
                AktuelleVorlage.Id, RundenZuEingaben(), UebergangsregelnZuEingaben(), PaarungenZuEingaben());
            await VorlageSetzenAsync(AktuelleVorlage);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
    private bool KannSpeichern() => HatVorlage && IsDirty && !HatRundenFehler;

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen() => await VorlageSetzenAsync(AktuelleVorlage);
    private bool KannAbbrechen() => HatVorlage && IsDirty;

    public bool HatUngespeicherteAenderungen => HatVorlage && IsDirty;
}
