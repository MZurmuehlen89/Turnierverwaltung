using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Application.Stammdaten;
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

/// <summary>Ein Vorschlag für Gruppenanzahl/Gruppengröße (Issue #215), abgeleitet aus den Teilern der
/// Eingangsmannschaftsanzahl per <see cref="RundenKonfiguration.GruppenAnzahlVorschlaege"/>.</summary>
public record GruppenAnzahlVorschlag(int GruppenAnzahl, int Gruppengroesse)
{
    public string Anzeige => $"{GruppenAnzahl} × {Gruppengroesse}";
}

/// <summary>Eine Zeile innerhalb eines <see cref="UebergangsregelBlock"/> — je Gruppe (Gruppenphase-
/// Quellrunde), je Spiel (K.o.-Quellrunde) oder eine einzelne Zeile (Liga-Quellrunde, <see cref="GruppeIndex"/>
/// = null). <see cref="AnzahlBeste"/> (gruppenübergreifende Rangfolge-Regel, Issue #217, z.B. "2 beste
/// Gruppendritte") ist gesetzt, wenn <see cref="GruppeIndex"/> null bleibt UND die Zeile über
/// <see cref="RundenUebergaengeViewModel.NeueGruppenuebergreifendeZeileCommand"/> angelegt wurde.</summary>
public partial class UebergangsregelZeile : ObservableObject
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

    /// <summary>Nur für gruppenübergreifende Zeilen (<see cref="AnzahlBeste"/> gesetzt) relevant: eine
    /// einzelne Platzierung statt eines Von/Bis-Bereichs (z.B. "Platz 3") — setzt beide Felder synchron.</summary>
    public int Platzierung
    {
        get => PlatzierungVon;
        set { PlatzierungVon = value; PlatzierungBis = value; }
    }

    private void PlatzSetzen(bool sieger)
    {
        PlatzierungVon = sieger ? 1 : 2;
        PlatzierungBis = sieger ? 1 : 2;
    }
}

/// <summary>Eine Übergangsregel (Quelle→Ziel + Detailzeilen) — zentrale Datenhaltung in
/// <see cref="RundenViewModel"/>.AlleUebergaenge, gemeinsam genutzt von den Tabs "Runden" und
/// "Rundenübergänge" (<see cref="RundenUebergaengeViewModel"/>). Bearbeitet wird sie AUSSCHLIESSLICH
/// über das stabile Regelformular in <see cref="RundenUebergaengeViewModel"/> (Populier-/Zurückschreib-
/// Muster) — dieser Typ selbst enthält keine TwoWay-Bindungspunkte für Quelle/Ziel (siehe #284-Redesign-
/// Historie: eine frühere Fassung mit eingebetteter ComboBox pro Karte hat bei jedem Rundenfokus-Wechsel
/// wiederholt zu Datenkorruption geführt, weil die Karte selbst bei jedem Wechsel neu aufgebaut wurde). Nur
/// die einzelnen <see cref="Zeilen"/> (PlatzierungVon/Bis, Sieger/Verlierer) bleiben direkt TwoWay-editierbar —
/// das ist unproblematisch, da sie in TextBoxen/CheckBoxen ohne eigenen Selektions-State liegen.</summary>
public partial class UebergangsregelBlock : ObservableObject
{
    private bool _wirdInitialisiert;

    /// <summary>Feuert bei jeder inhaltlichen Änderung (Quelle/Ziel/Zeilen) — <see cref="RundenViewModel"/>
    /// aktualisiert daraufhin IsDirty, die Validierung und feuert <see cref="RundenViewModel.UebergaengeGeaendert"/>.</summary>
    public event Action? Geaendert;

    [ObservableProperty] private RundeZeile? _quellRunde;
    [ObservableProperty] private RundeZeile? _zielRunde;
    [ObservableProperty] private ObservableCollection<UebergangsregelZeile> _zeilen = [];

    /// <summary>Vom Übergänge-Tab gesetzt, wenn dieser Block gerade im Regelformular bearbeitet wird — reine
    /// Anzeige-Markierung für die Hervorhebung in den Eingehend-/Ausgehend-Listen, keine Nutzereingabe.</summary>
    [ObservableProperty] private bool _istAusgewaehlt;

    public static UebergangsregelBlock NeuLeer(RundeZeile? quelle, RundeZeile? ziel)
    {
        var block = new UebergangsregelBlock();
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

    public static UebergangsregelBlock AusBestehendenZeilenErstellen(
        RundeZeile quelle, RundeZeile ziel, IEnumerable<UebergangsregelZeile> zeilen)
    {
        var block = new UebergangsregelBlock();
        block._wirdInitialisiert = true;
        try
        {
            block.QuellRunde = quelle;
            block.ZielRunde = ziel;
            block.Zeilen = new ObservableCollection<UebergangsregelZeile>(zeilen);
        }
        finally
        {
            block._wirdInitialisiert = false;
        }
        foreach (var z in block.Zeilen)
            z.PropertyChanged += (_, __) => block.Geaendert?.Invoke();
        return block;
    }

    partial void OnQuellRundeChanged(RundeZeile? value)
    {
        if (!_wirdInitialisiert) Geaendert?.Invoke();
        if (_wirdInitialisiert || value is null || Zeilen.Count > 0) return;
        BefuelleStandardZeilen();
    }

    partial void OnZielRundeChanged(RundeZeile? value)
    {
        if (!_wirdInitialisiert) Geaendert?.Invoke();
    }

    private void BefuelleStandardZeilen()
    {
        var quelle = QuellRunde;
        if (quelle is null) return;

        void ZeileHinzufuegen(int? gruppeIndex, int von, int bis)
        {
            var zeile = new UebergangsregelZeile { GruppeIndex = gruppeIndex, PlatzierungVon = von, PlatzierungBis = bis };
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

    /// <summary>Fügt eine gruppenübergreifende Rangfolge-Regel-Zeile hinzu (z.B. "2 beste Gruppendritte",
    /// EM/WM-Klassiker) — nur bei Gruppenphase als Quellrunde sinnvoll. Anders als die per-Gruppe-Zeilen aus
    /// <see cref="BefuelleStandardZeilen"/> bleibt <see cref="UebergangsregelZeile.GruppeIndex"/> hier null,
    /// <see cref="UebergangsregelZeile.AnzahlBeste"/> markiert die Zeile als gruppenübergreifend (siehe
    /// <see cref="RundenUebergaengeViewModel.GruppenuebergreifendeZeilen"/>). Default "Platz 3, beste 1"
    /// analog WPF-Original.</summary>
    public void GruppenuebergreifendeZeileHinzufuegen()
    {
        var zeile = new UebergangsregelZeile { GruppeIndex = null, AnzahlBeste = 1, PlatzierungVon = 3, PlatzierungBis = 3 };
        zeile.PropertyChanged += (_, __) => Geaendert?.Invoke();
        Zeilen.Add(zeile);
        Geaendert?.Invoke();
    }

    /// <summary>Entfernt eine einzelne Zeile (aktuell nur für gruppenübergreifende Regeln genutzt — die
    /// per-Gruppe-Zeilen werden weiterhin automatisch aus der Quellrundenstruktur abgeleitet, nicht einzeln
    /// entfernbar).</summary>
    public void ZeileEntfernen(UebergangsregelZeile zeile)
    {
        Zeilen.Remove(zeile);
        Geaendert?.Invoke();
    }
}

/// <summary>ViewModel-Zeile für die Rundenliste — Spiegel einer <see cref="TurnierRunde"/>, reiner UI-Zustand
/// bis zum nächsten Speichern. Bewusst ein reiner Datenträger ohne eigene Änderungs-/Berechnungslogik — die
/// Formulare binden NICHT direkt auf diese Instanz, sondern nur die Rundenliste und die (nur lesenden)
/// Übergangsregel-Zeilen referenzieren sie per Objektidentität.</summary>
public partial class RundeZeile : ObservableObject
{
    public static readonly RegelwerkOption KeinRegelwerk = new("— Turnierstandard —", null);

    public int? Id { get; set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private Rundenart _rundenart = Rundenart.Liga;
    [ObservableProperty] private int _eingangsMannschaftsanzahl = 8;
    [ObservableProperty] private int _ausgangsMannschaftsanzahl = 4;
    [ObservableProperty] private int? _gruppenAnzahl;
    [ObservableProperty] private int? _anzahlSpieltage;
    [ObservableProperty] private bool _mitRueckspiel;
    [ObservableProperty] private bool _auswaertstorRegelAktiv;
    [ObservableProperty] private RegelwerkOption _regelwerk = KeinRegelwerk;
    [ObservableProperty] private bool _paarungFestVorgegeben;
    [ObservableProperty] private VorgegebeneAuslosungsartOption _vorgegebeneAuslosungsart = KeineAuslosungsartVorgabe;

    public static readonly VorgegebeneAuslosungsartOption KeineAuslosungsartVorgabe = new("— frei wählbar —", null);
}

/// <summary>Option der "Auslosungsart (Vorgabe)"-ComboBox im Rundenformular (Issue #347), <see cref="Wert"/> = null
/// steht für "frei wählbar". Instanzen nur über <c>RundenViewModel.AuslosungsartVorgabeOptionFuer</c> beziehen —
/// die ComboBox vergleicht <c>SelectedItem</c> per Referenz, Record-Gleichheit hilft dort nicht.</summary>
public record VorgegebeneAuslosungsartOption(string Anzeige, Auslosungsart? Wert);

/// <summary>"Runden"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297) — Rundenliste + Rundenformular
/// (Name/Rundenart/Eingang/Ausgang/GruppenAnzahl/MitRueckspiel/AuswaertstorRegelAktiv/Regelwerk/PaarungFest-
/// Vorgegeben). Zugleich alleiniger DATENHALTER für Runden UND Übergangsregeln (<see cref="AlleUebergaenge"/>)
/// — der separate Tab "Rundenübergänge" (<see cref="RundenUebergaengeViewModel"/>) referenziert dieses
/// ViewModel per DI (beide Scoped in derselben Navigations-Scope) und liest/schreibt die geteilten Daten über
/// die öffentlichen Methoden/Property unten, statt eigene Kopien zu halten — EIN "Speichern" committet Runden
/// UND Übergangsregeln gemeinsam über <see cref="TurnierInstanzService.RundenUndUebergaengeErsetzenAsync"/>,
/// unabhängig davon, welcher der beiden Tabs gerade aktiv ist. Feste Paarungen (Vorbelegung) werden seit
/// #297 unabhängig davon im <see cref="VorbelegungViewModel"/> direkt gegen Spiel/Auslosung verwaltet.</summary>
public partial class RundenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _service;
    private readonly RegelwerkService _regelwerkService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly TurnierValidator _turnierValidator;
    private readonly UebergangsregelValidator _uebergangsregelValidator;
    private int _populiertGerade;

    // Reentrancy-Guard: Instanz-Id, deren Ladevorgang gerade läuft — verhindert, dass ein schneller
    // Instanzwechsel (Klick A, sofort Klick B) den älteren, noch laufenden Ladevorgang anwendet.
    private int _ladevorgangLaufendFuer = -1;

    private readonly List<UebergangsregelBlock> _alleUebergaenge = [];

    /// <summary>Geteilte Übergangsregel-Datenhaltung, gelesen von <see cref="RundenUebergaengeViewModel"/>.</summary>
    public IReadOnlyList<UebergangsregelBlock> AlleUebergaenge => _alleUebergaenge;

    /// <summary>Feuert bei jeder strukturellen (hinzugefügt/entfernt) oder inhaltlichen (Quelle/Ziel/Zeilen)
    /// Änderung an <see cref="AlleUebergaenge"/> sowie bei jeder Rundendaten-Änderung, die die Kartenanzeige
    /// betrifft (Name/Eingang/Ausgang) — <see cref="RundenUebergaengeViewModel"/> abonniert dies.</summary>
    public event Action? UebergaengeGeaendert;

    private void UebergaengeGeaendertMelden()
    {
        OnPropertyChanged(nameof(PaarungFestVorgegebenVerfuegbar));
        UebergaengeGeaendert?.Invoke();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatInstanz))]
    [NotifyPropertyChangedFor(nameof(RundenBearbeitbar))]
    [NotifyCanExecuteChangedFor(nameof(NeueRundeCommand))]
    private TurnierInstanz? _aktuelleInstanz;

    public bool HatInstanz => AktuelleInstanz is not null;

    /// <summary>Status-Gating (Issue #297): Rundenstruktur/Übergänge sind nur bearbeitbar, solange das Turnier
    /// noch keine echten Teilnehmer/Ergebnisse hat (Status Geplant oder Vorlage) — analog WPF-Original
    /// <c>TurnierDefinitionViewModel.RundenTabAktiv</c>, um erweitert um den Vorlage-Status.</summary>
    public bool RundenBearbeitbar => AktuelleInstanz is null
        || AktuelleInstanz.Status is TurnierStatus.Geplant or TurnierStatus.Vorlage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RundeEntfernenCommand))]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    private RundeZeile? _ausgewaehlteRunde;

    partial void OnAusgewaehlteRundeChanged(RundeZeile? value)
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
        nameof(GruppenAnzahl), nameof(AnzahlSpieltage), nameof(MitRueckspiel), nameof(AuswaertstorRegelAktiv),
        nameof(Regelwerk), nameof(PaarungFestVorgegeben), nameof(VorgegebeneAuslosungsart)
    ];

    [ObservableProperty] private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRundenart))]
    [NotifyPropertyChangedFor(nameof(IstSchweizerSystem))]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    private Rundenart _rundenart = Rundenart.Liga;

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
    [ObservableProperty] private RegelwerkOption _regelwerk = RundeZeile.KeinRegelwerk;
    [ObservableProperty] private bool _paarungFestVorgegeben;
    [ObservableProperty] private VorgegebeneAuslosungsartOption _vorgegebeneAuslosungsart = RundeZeile.KeineAuslosungsartVorgabe;

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
            a => a, a => new VorgegebeneAuslosungsartOption(AuslosungViewModel.AuslosungsartAnzeigeName(a), a));

    // Manuell wird nicht angeboten — das regelt die Checkbox "Paarung fest vorgegeben".
    private static IReadOnlyList<VorgegebeneAuslosungsartOption> AuslosungsartVorgabeOptionenFuer(Rundenart rundenart) =>
    [
        RundeZeile.KeineAuslosungsartVorgabe,
        .. Enum.GetValues<Auslosungsart>()
            .Where(a => a != Auslosungsart.Manuell && AuslosungsKompatibilitaet.IstKompatibel(rundenart, a))
            .Select(a => _auslosungsartVorgabeOptionen[a])
    ];

    private static VorgegebeneAuslosungsartOption AuslosungsartVorgabeOptionFuer(Auslosungsart? art) =>
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

    /// <summary>Feste Paarungen (Vorbelegung, Issue #277/#297) ergeben nur für eine Runde mit mindestens
    /// einer eingehenden Übergangsregel Sinn — die erste Runde eines Turniers hat noch keine Platzhalter-
    /// Basis und durchläuft in jedem realen Turnier zwingend eine echte Auslosung.</summary>
    public bool PaarungFestVorgegebenVerfuegbar =>
        AusgewaehlteRunde is not null && AlleUebergaenge.Any(b => b.ZielRunde == AusgewaehlteRunde);

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
            Regelwerk = zeile?.Regelwerk ?? RundeZeile.KeinRegelwerk;
            PaarungFestVorgegeben = zeile?.PaarungFestVorgegeben ?? false;
            VorgegebeneAuslosungsart = zeile?.VorgegebeneAuslosungsart ?? RundeZeile.KeineAuslosungsartVorgabe;
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
        AusgewaehlteRunde.AnzahlSpieltage = AnzahlSpieltage;
        AusgewaehlteRunde.MitRueckspiel = MitRueckspiel;
        AusgewaehlteRunde.AuswaertstorRegelAktiv = AuswaertstorRegelAktiv;
        AusgewaehlteRunde.Regelwerk = Regelwerk;
        AusgewaehlteRunde.PaarungFestVorgegeben = PaarungFestVorgegeben;
        // null kommt nur transient aus der ComboBox (Auswahl beim ItemsSource-Wechsel nicht gefunden) — nie übernehmen.
        if (VorgegebeneAuslosungsart is not null)
            AusgewaehlteRunde.VorgegebeneAuslosungsart = VorgegebeneAuslosungsart;

        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden(); // Name/Eingang/Ausgang betreffen die Kartenanzeige im Übergänge-Tab
    }

    [ObservableProperty] private ObservableCollection<RundeZeile> _runden = [];

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
        [RundeZeile.KeinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();

    // ── IRibbonAware ──────────────────────────────────────────────────────────
    public string RibbonTabTitel => "Runden";
    public ICommand? NeuBefehl => NeueRundeCommand;
    public ICommand? SpeichernBefehl => SpeichernCommand;
    public ICommand? AbbrechenBefehl => AbbrechenCommand;
    public ICommand? LoeschenBefehl => RundeEntfernenCommand;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => [];

    public RundenViewModel(
        TurnierInstanzService service, RegelwerkService regelwerkService, UebergangsregelService uebergangsregelService,
        TurnierValidator turnierValidator, UebergangsregelValidator uebergangsregelValidator)
    {
        _service = service;
        _regelwerkService = regelwerkService;
        _uebergangsregelService = uebergangsregelService;
        _turnierValidator = turnierValidator;
        _uebergangsregelValidator = uebergangsregelValidator;
    }

    /// <summary>Vom Composite gesetzt, wenn sich die geteilte Instanzauswahl ändert — lädt Runden und
    /// Übergangsregeln der neuen Instanz in einem Rutsch. Wird auch von <see cref="Speichern"/>/
    /// <see cref="Abbrechen"/> für denselben Reload genutzt — die zuvor ausgewählte Runde wird danach
    /// wiederhergestellt (per Id, sonst per Listenposition für eine gerade erst angelegte, noch ungespeicherte
    /// Runde), statt das Formular kommentarlos zu leeren.</summary>
    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        var vorherAusgewaehlteId = AusgewaehlteRunde?.Id;
        var vorherAusgewaehlterIndex = AusgewaehlteRunde is not null ? Runden.IndexOf(AusgewaehlteRunde) : -1;

        AktuelleInstanz = instanz;
        var meineId = instanz?.Id ?? -1;
        _ladevorgangLaufendFuer = meineId;

        if (instanz is null)
        {
            RundenZuruecksetzen([]);
            return;
        }

        IstLadend = true;
        try
        {
            if (Regelwerke.Count == 0)
                Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());

            var mitRunden = await _service.FindenNachIdMitRundenAsync(instanz.Id);
            var alleRegeln = await _uebergangsregelService.AlleNachTurnierInstanzIdAsync(instanz.Id);
            if (_ladevorgangLaufendFuer != meineId) return;

            var runden = (mitRunden?.Runden ?? []).OrderBy(r => r.Reihenfolge).ToList();
            var zeilen = runden.Select(RundeZuZeile).ToList();
            var zeileNachId = runden.Zip(zeilen, (r, z) => (r.Id, z)).ToDictionary(p => p.Id, p => p.z);

            RundenZuruecksetzen(zeilen);

            _populiertGerade++;
            try
            {
                var regelnGueltig = alleRegeln.Where(r => zeileNachId.ContainsKey(r.QuellRundeId) && zeileNachId.ContainsKey(r.ZielRundeId));

                foreach (var gruppe in regelnGueltig.GroupBy(r => (r.QuellRundeId, r.ZielRundeId)))
                {
                    var quellZeile = zeileNachId[gruppe.Key.QuellRundeId];
                    var zielZeile = zeileNachId[gruppe.Key.ZielRundeId];
                    var blockZeilen = gruppe.OrderBy(r => r.GruppeIndex ?? 0).Select(r => new UebergangsregelZeile
                    {
                        GruppeIndex = r.GruppeIndex,
                        PlatzierungVon = r.PlatzierungVon,
                        PlatzierungBis = r.PlatzierungBis,
                        AnzahlBeste = r.AnzahlBeste
                    });
                    var block = UebergangsregelBlock.AusBestehendenZeilenErstellen(quellZeile, zielZeile, blockZeilen);
                    VerdrahteBlock(block);
                    _alleUebergaenge.Add(block);
                }
            }
            finally
            {
                _populiertGerade--;
            }

            // Id-Fallback greift nur bei Abbrechen (kein DB-Schreibzugriff, alte Ids bleiben gültig) — nach
            // Speichern hat RundenUndUebergaengeErsetzenAsync ALLE Runden per Löschen+Neuanlegen ersetzt,
            // auch unveränderte bekommen dabei eine neue Id. Der Id-Treffer schlägt dann für JEDE Runde
            // fehl — deshalb per Listenposition weitersuchen, statt nur beim Fehlen einer alten Id.
            RundeZeile? wiederhergestellt = vorherAusgewaehlteId is int id
                ? Runden.FirstOrDefault(z => z.Id == id)
                : null;
            if (wiederhergestellt is null && vorherAusgewaehlterIndex >= 0 && vorherAusgewaehlterIndex < Runden.Count)
                wiederhergestellt = Runden[vorherAusgewaehlterIndex];
            AusgewaehlteRunde = wiederhergestellt;

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

    private static RundeZeile RundeZuZeile(TurnierRunde r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Rundenart = r.Rundenart,
        EingangsMannschaftsanzahl = r.EingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl = r.AusgangsMannschaftsanzahl,
        GruppenAnzahl = r.GruppenAnzahl,
        AnzahlSpieltage = RundenKonfiguration.AusJson(r.KonfigurationJson).AnzahlSpieltage,
        MitRueckspiel = r.MitRueckspiel,
        AuswaertstorRegelAktiv = r.AuswaertstorRegelAktiv,
        Regelwerk = r.Regelwerk is not null ? new RegelwerkOption(r.Regelwerk.Name, r.Regelwerk) : RundeZeile.KeinRegelwerk,
        PaarungFestVorgegeben = r.PaarungFestVorgegeben,
        VorgegebeneAuslosungsart = AuslosungsartVorgabeOptionFuer(r.VorgegebeneAuslosungsart)
    };

    private void RundenZuruecksetzen(IEnumerable<RundeZeile> runden)
    {
        _populiertGerade++;
        try
        {
            Runden = new ObservableCollection<RundeZeile>(runden);
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
        var zeile = new RundeZeile { Name = $"Runde {Runden.Count + 1}", EingangsMannschaftsanzahl = eingang, AusgangsMannschaftsanzahl = eingang };
        Runden.Add(zeile);
        AusgewaehlteRunde = zeile;
        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }
    private bool KannNeueRunde() => HatInstanz;

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

    public void UebergangHinzufuegen(UebergangsregelBlock block)
    {
        VerdrahteBlock(block);
        _alleUebergaenge.Add(block);
        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }

    public void UebergangEntfernen(UebergangsregelBlock block)
    {
        _alleUebergaenge.Remove(block);
        IsDirty = true;
        AktualisiereRundenFehler();
        UebergaengeGeaendertMelden();
    }

    private void VerdrahteBlock(UebergangsregelBlock block)
    {
        block.Geaendert += () =>
        {
            if (_populiertGerade == 0) IsDirty = true;
            AktualisiereRundenFehler();
            UebergaengeGeaendertMelden();
        };
    }

    // ── Validierung ───────────────────────────────────────────────────────────

    /// <summary>Issue #297-Nachzug: der WinUi-Port hatte <c>TurnierRunde.EingangUeberUebergangsregeln</c>
    /// komplett vergessen (weder in <see cref="RundeZeile"/> noch beim Laden/Speichern berücksichtigt) —
    /// dadurch prüfte <see cref="TurnierValidator.RundenValidieren"/> stur die lineare Rundenkette
    /// (Ausgang[i] == Eingang[i+1]), was bei parallelen/zusammenlaufenden Pfaden (z.B. UEFA Champions
    /// League: Ligaphase speist Achtelfinale UND Playoff-Runde parallel) zu falschen Validierungsfehlern
    /// führte (Fund aus #297-Test). Statt das Flag als eigenes, manuell zu pflegendes Formularfeld
    /// nachzuziehen (wie im WPF-Original), wird es hier — analog zu dessen Auto-Set-Logik — direkt aus dem
    /// tatsächlichen Vorhandensein eingehender Übergangsregeln abgeleitet: hat eine Runde mindestens einen
    /// Übergangsregel-Block, der auf sie zielt, ist ihr Eingang per Definition regelbasiert und nicht mehr
    /// die unmittelbar vorangehende Runde.</summary>
    public List<TurnierRundeEingabe> RundenZuEingaben() => Runden.Select(z => new TurnierRundeEingabe(
        Name: z.Name,
        Rundenart: z.Rundenart,
        EingangsMannschaftsanzahl: z.EingangsMannschaftsanzahl,
        AusgangsMannschaftsanzahl: z.AusgangsMannschaftsanzahl,
        RegelwerkId: z.Regelwerk.Wert?.Id,
        MitRueckspiel: z.MitRueckspiel,
        AuswaertstorRegelAktiv: z.AuswaertstorRegelAktiv,
        KonfigurationJson: z.Rundenart == Rundenart.SchweizerSystem
            ? new RundenKonfiguration(z.AnzahlSpieltage).ZuJson() : null,
        GruppenAnzahl: z.Rundenart == Rundenart.Gruppenphase ? z.GruppenAnzahl : null,
        EingangUeberUebergangsregeln: AlleUebergaenge.Any(b => b.ZielRunde == z),
        PaarungFestVorgegeben: z.PaarungFestVorgegeben,
        VorgegebeneAuslosungsart: z.VorgegebeneAuslosungsart.Wert)).ToList();

    public List<UebergangsregelPositionsEingabe> UebergangsregelnZuEingaben() => _alleUebergaenge
        .Where(b => b.QuellRunde is not null && b.ZielRunde is not null && b.QuellRunde != b.ZielRunde)
        .SelectMany(b => b.Zeilen.Select(z => new UebergangsregelPositionsEingabe(
            Runden.IndexOf(b.QuellRunde!) + 1, Runden.IndexOf(b.ZielRunde!) + 1,
            z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste)))
        .ToList();

    private void AktualisiereRundenFehler()
    {
        if (Runden.Count == 0) { RundenFehler = []; return; }

        var rundenEingaben = RundenZuEingaben();
        var uebergangsregelEingaben = UebergangsregelnZuEingaben();
        var fehler = new List<string>(_turnierValidator.RundenValidieren(rundenEingaben).Fehler);

        // Nur für die Ganzgraph-Übergangsregel-Validierung nötig — bewusst rein transiente TurnierRunde-
        // Objekte (nie persistiert), da neue, noch ungespeicherte Runden hier ebenfalls mitgeprüft werden
        // müssen und der Validator echte TurnierRunde-Objekte (statt Eingaben) erwartet.
        var rundenFuerValidierung = Runden.Select(z => new TurnierRunde
        {
            Name = z.Name,
            Rundenart = z.Rundenart,
            EingangsMannschaftsanzahl = z.EingangsMannschaftsanzahl,
            AusgangsMannschaftsanzahl = z.AusgangsMannschaftsanzahl,
            GruppenAnzahl = z.GruppenAnzahl
        }).ToList();
        fehler.AddRange(_uebergangsregelValidator.Validieren(rundenFuerValidierung, uebergangsregelEingaben).Fehler);

        RundenFehler = fehler;
    }

    /// <summary>Feuert nach erfolgreichem Speichern — <see cref="VorbelegungViewModel"/> liest Runden
    /// (insbesondere <c>PaarungFestVorgegeben</c>) unabhängig frisch aus der DB und bekommt sonst nichts
    /// vom hier erfolgten Rundenersatz mit (Fund aus #297-Test: Vorbelegung-Tab blieb nach dem Setzen von
    /// "Paarung fest vorgegeben" + Speichern im Runden-Tab leer, weil niemand seinen Reload ausgelöst hat).</summary>
    public event Action<TurnierInstanz>? RundenGespeichert;

    // ── Ribbon-Befehle ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(KannSpeichern))]
    private async Task Speichern()
    {
        if (AktuelleInstanz is not { } instanz) return;
        Fehlermeldung = null;
        try
        {
            await _service.RundenUndUebergaengeErsetzenAsync(
                instanz.Id, RundenZuEingaben(), UebergangsregelnZuEingaben());
            await InstanzSetzenAsync(instanz);
            RundenGespeichert?.Invoke(instanz);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
    }
    private bool KannSpeichern() => HatInstanz && IsDirty && !HatRundenFehler;

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private async Task Abbrechen() => await InstanzSetzenAsync(AktuelleInstanz);
    private bool KannAbbrechen() => HatInstanz && IsDirty;

    public bool HatUngespeicherteAenderungen => HatInstanz && IsDirty;
}
