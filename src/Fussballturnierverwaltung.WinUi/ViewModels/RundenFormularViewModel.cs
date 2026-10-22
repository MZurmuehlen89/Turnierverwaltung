using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Application.Tabelle;
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
    private readonly RanglisteService _ranglisteService;
    private int _populiertGerade;

    // Die gezeigte Runde und ob vor ihr eine Gruppenphase liegt (bestimmt, welche Auslosungsregeln wirken können).
    private RundeZeile? _zeile;
    private bool _folgtAufGruppenphase;

    /// <summary>Der Nutzer hat ein Feld der gezeigten Runde geändert; die Zeile trägt den neuen Wert bereits.</summary>
    public event Action? Geaendert;

    public RundenFormularViewModel(RegelwerkService regelwerkService, RanglisteService ranglisteService)
    {
        _regelwerkService = regelwerkService;
        _ranglisteService = ranglisteService;
        TabellenBereiche.Geaendert += () => Geaendert?.Invoke();
        VorgegebeneGruppenposition.Geaendert += GruppenpositionGeaendert;
        Gruppenkoepfe.Geaendert += GruppenkoepfeGeaendert;
    }

    /// <summary>Tabellenbereiche der gezeigten Runde (Issue #420), nur bei Runden mit Tabelle sichtbar.</summary>
    public TabellenBereicheViewModel TabellenBereiche { get; } = new();

    /// <summary>Vorgegebene Position in der Gruppe (Issue #475), nur bei Gruppenphase wählbar.</summary>
    public GruppenpositionAuswahl VorgegebeneGruppenposition { get; } = new();

    /// <summary>Feste Gruppenköpfe für Gastgeber und Titelverteidiger (Issue #487), nur bei Gruppenphase sichtbar.</summary>
    public GruppenkoepfeVorgabe Gruppenkoepfe { get; } = new();

    /// <summary>Zeigt die Runde im Formular (<c>null</c> leert es).</summary>
    public void Zeigen(RundeZeile? zeile, bool folgtAufGruppenphase)
    {
        _zeile = zeile;
        _folgtAufGruppenphase = folgtAufGruppenphase;
        OhneRueckschreiben(() => FelderAusZeile(zeile));
        TabellenBereiche.Zeigen(zeile);
        AuslosungOffen = AuslosungKopfTeile().Any(t => t is not null);
        OnPropertyChanged(nameof(AuslosungKopf));
    }

    // ── Aufklappbarer Bereich „Auslosung“ (Issue #476) ───────────────────────
    // Bleibt im Formular, weil er nur dessen Felder zusammenfasst — eine eigene Klasse müsste sie alle durchreichen.

    private static readonly HashSet<string> AuslosungFelder =
    [
        nameof(Rundenart), nameof(PaarungFestVorgegeben), nameof(VorgegebeneAuslosungsart),
        nameof(StandardRangliste), nameof(TitelverteidigerInTopf1)
    ];

    [ObservableProperty] private bool _auslosungOffen;

    public AbschnittKopf AuslosungKopf => AbschnittKopf.Aus("AUSLOSUNG", AuslosungKopfTeile(), "keine Vorgaben");

    private IEnumerable<string?> AuslosungKopfTeile()
    {
        int regeln = RegelVorgabeOptionen.Count(o => o.IstAktiv);
        return
        [
            PaarungFestVorgegeben ? "Paarung fest vorgegeben" : null,
            VorgegebeneAuslosungsart?.Wert is not null ? VorgegebeneAuslosungsart.Anzeige : null,
            StandardRangliste?.Wert is not null ? StandardRangliste.Anzeige : null,
            TitelverteidigerInTopf1 ? "Titelverteidiger in Topf 1" : null,
            IstGruppenphase && VorgegebeneGruppenposition.Wert != Gruppenposition.NachTopf
                ? VorgegebeneGruppenposition.Ausgewaehlt?.Anzeige : null,
            IstGruppenphase && (Gruppenkoepfe.GastgeberGruppen.Count > 0 || Gruppenkoepfe.TitelverteidigerGruppe is not null)
                ? "feste Gruppenköpfe" : null,
            regeln switch { 0 => null, 1 => "1 Auslosungsregel", _ => $"{regeln} Auslosungsregeln" }
        ];
    }

    /// <summary>Änderungen innerhalb von <paramref name="aktion"/> stammen nicht vom Nutzer: nichts davon wird in
    /// die Zeile geschrieben oder gemeldet, und die Kopplungen zwischen den Feldern bleiben aus.</summary>
    private void OhneRueckschreiben(Action aktion)
    {
        _populiertGerade++;
        try { aktion(); }
        finally { _populiertGerade--; }
    }

    // ── Felder ────────────────────────────────────────────────────────────────
    // Bewusst VM-Felder statt Direktbindung auf AusgewaehlteRunde.<Feld> (siehe #284-Umsetzungsnotiz für die
    // ausführliche Begründung: eine Direktbindung auf ein bei Fokuswechsel wechselndes Objekt hat wiederholt
    // zu Datenkorruption geführt). Dasselbe Muster gilt im Regelformular des Übergangsregel-Editors.
    // Ein neues Feld braucht hier drei Einträge — FormFelder, FelderAusZeile, FelderInZeile — und zwei im
    // Eltern-ViewModel (RundeZuZeile, RundenZuEingaben).
    private static readonly HashSet<string> FormFelder =
    [
        nameof(Name), nameof(Rundenart), nameof(EingangsMannschaftsanzahl), nameof(AusgangsMannschaftsanzahl),
        nameof(GruppenAnzahl), nameof(AnzahlSpieltage), nameof(MitRueckspiel), nameof(AuswaertstorRegelAktiv), nameof(NeutralerSpielort),
        nameof(Ueberraschungsfaktor),
        nameof(Regelwerk), nameof(PaarungFestVorgegeben), nameof(VorgegebeneAuslosungsart),
        nameof(StandardRangliste), nameof(TitelverteidigerInTopf1)
    ];

    [ObservableProperty] private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRundenart))]
    [NotifyPropertyChangedFor(nameof(IstSchweizerSystem))]
    [NotifyPropertyChangedFor(nameof(AuswaertstorAktivierbar))]
    [NotifyPropertyChangedFor(nameof(RundenartAuswahl))]
    [NotifyPropertyChangedFor(nameof(HatTabelle))]
    private Rundenart _rundenart = Rundenart.Liga;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzahlSpieltagePlatzhalter))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitelverteidigerInTopf1Aktivierbar))]
    private StandardRanglisteOption _standardRangliste = RundeZeile.KeineStandardRangliste;

    [ObservableProperty] private bool _titelverteidigerInTopf1;

    private void FelderAusZeile(RundeZeile? zeile)
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
        StandardRangliste = zeile?.StandardRangliste ?? RundeZeile.KeineStandardRangliste;
        TitelverteidigerInTopf1 = zeile?.TitelverteidigerInTopf1 ?? false;
        RegelVorgabenAusZeile(zeile);
        VorgegebeneGruppenposition.Zeigen(
            zeile?.VorgegebeneGruppenposition ?? Gruppenposition.NachTopf, Rundenart, AuslosungsartFuerGruppenposition);
        Gruppenkoepfe.Zeigen(zeile?.GastgeberGruppen ?? [], zeile?.TitelverteidigerGruppe, GruppenAnzahl);
    }

    private void FelderInZeile(RundeZeile zeile)
    {
        zeile.Name = Name;
        zeile.Rundenart = Rundenart;
        zeile.EingangsMannschaftsanzahl = EingangsMannschaftsanzahl;
        zeile.AusgangsMannschaftsanzahl = AusgangsMannschaftsanzahl;
        zeile.GruppenAnzahl = GruppenAnzahl;
        zeile.AnzahlSpieltage = AnzahlSpieltage;
        zeile.MitRueckspiel = MitRueckspiel;
        zeile.AuswaertstorRegelAktiv = AuswaertstorRegelAktiv;
        zeile.NeutralerSpielort = NeutralerSpielort;
        zeile.Ueberraschungsfaktor = Ueberraschungsfaktor;
        zeile.Regelwerk = Regelwerk;
        zeile.PaarungFestVorgegeben = PaarungFestVorgegeben;
        // null kommt nur transient aus der ComboBox (Auswahl beim ItemsSource-Wechsel nicht gefunden) — nie übernehmen.
        if (VorgegebeneAuslosungsart is not null)
            zeile.VorgegebeneAuslosungsart = VorgegebeneAuslosungsart;
        if (StandardRangliste is not null)
            zeile.StandardRangliste = StandardRangliste;
        zeile.TitelverteidigerInTopf1 = TitelverteidigerInTopf1;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not null && AuslosungFelder.Contains(e.PropertyName))
            OnPropertyChanged(nameof(AuslosungKopf));
        if (_populiertGerade > 0 || e.PropertyName is null || _zeile is null) return;
        if (!FormFelder.Contains(e.PropertyName)) return;

        FelderInZeile(_zeile);
        Geaendert?.Invoke();
    }

    // ── Abgeleitete Schalter und Vorschläge ───────────────────────────────────

    public bool IstGruppenphase => Rundenart == Rundenart.Gruppenphase;
    public bool IstSchweizerSystem => Rundenart == Rundenart.SchweizerSystem;
    public bool HatTabelle => TabellenZonen.HatTabelle(Rundenart);

    /// <summary>Die Auswärtstorregel ergibt nur bei Hin-/Rückspiel-Aggregaten in K.o.-Rundenarten Sinn
    /// (AD-001) — analog WPF-Original <c>RundenEditorViewModel.AuswaertstorAktivierbar</c>.</summary>
    public bool IstKoRundenart => Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
    public bool AuswaertstorAktivierbar => MitRueckspiel && IstKoRundenart;

    public bool TitelverteidigerInTopf1Aktivierbar => StandardRangliste?.Wert is not null;

    /// <summary>Fachlicher Standard-Vorschlag ceil(log2(n)) für <see cref="AnzahlSpieltage"/> (Issue #329 Teil D) —
    /// nur eine Anzeige-Hilfe, <see cref="AnzahlSpieltage"/> bleibt frei editierbar.</summary>
    public int AnzahlSpieltageVorschlag => RundenKonfiguration.VorschlagAnzahlSpieltage(EingangsMannschaftsanzahl);

    public string AnzahlSpieltagePlatzhalter => $"Vorschlag: {AnzahlSpieltageVorschlag}";

    /// <summary>Gruppenanzahl/Gruppengröße-Kombinationen zur Auswahl statt blinder Eingabe (Issue #215) —
    /// nur bei Gruppenphase relevant, aus den Teilern von <see cref="EingangsMannschaftsanzahl"/> abgeleitet.</summary>
    public IReadOnlyList<GruppenAnzahlVorschlag> GruppenAnzahlVorschlaege => RundenKonfiguration
        .GruppenAnzahlVorschlaege(EingangsMannschaftsanzahl)
        .Select(v => new GruppenAnzahlVorschlag(v.GruppenAnzahl, v.Gruppengroesse))
        .ToList();

    public bool HatGruppenAnzahlVorschlaege => GruppenAnzahlVorschlaege.Count > 0;

    [RelayCommand]
    private void GruppenAnzahlVorschlagWaehlen(GruppenAnzahlVorschlag vorschlag) => GruppenAnzahl = vorschlag.GruppenAnzahl;

    // ── Kopplungen zwischen den Feldern (nur bei Nutzeränderungen) ────────────

    partial void OnRundenartChanged(Rundenart value)
    {
        // Vor dem Populier-Guard: die Optionsliste muss auch beim Befüllen des Formulars zur Rundenart passen.
        AuslosungsartVorgabeAnRundenartPassen(value);
        GruppenpositionSperreAktualisieren();

        if (_populiertGerade > 0) return;
        RegelVorgabenAnRundePassen();
        AuswaertstorRegelBereinigen();
        AusgangVorschlagen();
        AnzahlSpieltageVorschlagen();
    }

    partial void OnEingangsMannschaftsanzahlChanged(int value)
    {
        if (_populiertGerade > 0) return;
        AusgangVorschlagen();
        AnzahlSpieltageVorschlagen();
    }

    partial void OnMitRueckspielChanged(bool value)
    {
        if (_populiertGerade > 0) return;
        AuswaertstorRegelBereinigen();
    }

    private void AuswaertstorRegelBereinigen()
    {
        if (!AuswaertstorAktivierbar) AuswaertstorRegelAktiv = false;
    }

    /// <summary>Nur ein Vorschlag (Normalfall: nur Sieger steigen auf) — bleibt frei editierbar, siehe Issue #334:
    /// der K.o.-Split-Sonderfall (Sieger UND Verlierer steigen in getrennte Zielrunden auf) braucht die volle
    /// Eingangsmannschaftsanzahl statt der Hälfte.</summary>
    private void AusgangVorschlagen()
    {
        if (IstKoRundenart) AusgangsMannschaftsanzahl = Math.Max(1, EingangsMannschaftsanzahl / 2);
    }

    private void AnzahlSpieltageVorschlagen()
    {
        if (IstSchweizerSystem && AnzahlSpieltage is null) AnzahlSpieltage = AnzahlSpieltageVorschlag;
    }

    // ── Auslosungsart als Vorgabe (Issue #347) ────────────────────────────────

    /// <summary>Nur zur Rundenart kompatible Auslosungsarten (Prävention statt Speicher-Validierung).
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

    // Erst den Zielwert, dann die ItemsSource setzen (siehe NeueAuslosungViewModel.OnRundeChanged).
    private void AuslosungsartVorgabeAnRundenartPassen(Rundenart rundenart)
    {
        if (VorgegebeneAuslosungsart?.Wert is { } vorgabe && !AuslosungsKompatibilitaet.IstKompatibel(rundenart, vorgabe))
            VorgegebeneAuslosungsart = RundeZeile.KeineAuslosungsartVorgabe;
        VorgegebeneAuslosungsartOptionen = AuslosungsartVorgabeOptionenFuer(rundenart);
        OnPropertyChanged(nameof(VorgegebeneAuslosungsart));
    }

    partial void OnPaarungFestVorgegebenChanged(bool value)
    {
        GruppenpositionSperreAktualisieren();
        if (_populiertGerade > 0) return;
        RegelVorgabenAnRundePassen();
    }

    partial void OnVorgegebeneAuslosungsartChanged(VorgegebeneAuslosungsartOption value)
    {
        GruppenpositionSperreAktualisieren();
        if (_populiertGerade > 0) return;

        // Siehe RundenartAuswahl: null ist nie eine Nutzerauswahl; die Rundenzeile hält weiter den richtigen Wert.
        if (value is null) AusZeileWiederherstellen(zeile => VorgegebeneAuslosungsart ??= zeile.VorgegebeneAuslosungsart);
        else RegelVorgabenAnRundePassen();
    }

    // ── Gruppenposition als Vorgabe (Issue #475) ─────────────────────────────

    // „Paarung fest vorgegeben“ heißt: die Runde wird manuell belegt, nicht ausgelost.
    private Auslosungsart? AuslosungsartFuerGruppenposition =>
        PaarungFestVorgegeben ? Auslosungsart.Manuell : VorgegebeneAuslosungsart?.Wert;

    private void GruppenpositionSperreAktualisieren() =>
        VorgegebeneGruppenposition.SperreAktualisieren(Rundenart, AuslosungsartFuerGruppenposition);

    partial void OnGruppenAnzahlChanged(int? value) => Gruppenkoepfe.GruppenAnzahlSetzen(value);

    private void GruppenkoepfeGeaendert()
    {
        OnPropertyChanged(nameof(AuslosungKopf));
        if (_populiertGerade > 0 || _zeile is null) return;
        _zeile.GastgeberGruppen       = Gruppenkoepfe.GastgeberGruppen;
        _zeile.TitelverteidigerGruppe = Gruppenkoepfe.TitelverteidigerGruppe;
        Geaendert?.Invoke();
    }

    private void GruppenpositionGeaendert()
    {
        OnPropertyChanged(nameof(AuslosungKopf));
        if (_populiertGerade > 0 || _zeile is null) return;
        _zeile.VorgegebeneGruppenposition = VorgegebeneGruppenposition.Wert;
        Geaendert?.Invoke();
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
                if (e.PropertyName != nameof(RegelVorgabeOption.IstAktiv)) return;
                RegelVorgabeGeaendert(option);
                OnPropertyChanged(nameof(AuslosungKopf));
            };
        return optionen;
    }

    private bool RegelVorgabeMoeglich(AuslosungsRegelArt regel) =>
        _zeile is not null && AuslosungsRegelKompatibilitaet.AlsVorgabeMoeglich(
            regel, Rundenart, VorgegebeneAuslosungsart?.Wert, _folgtAufGruppenphase, PaarungFestVorgegeben);

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
        OhneRueckschreiben(() =>
        {
            foreach (var option in RegelVorgabeOptionen)
            {
                option.IstVerfuegbar = RegelVorgabeMoeglich(option.Regelart);
                if (!option.IstVerfuegbar) option.IstAktiv = false;
            }
        });
        HatRegelVorgabeOptionen = RegelVorgabeOptionen.Any(o => o.IstVerfuegbar);
        RegelVorgabenInZeileSchreiben();
    }

    private void RegelVorgabeGeaendert(RegelVorgabeOption option)
    {
        if (_populiertGerade > 0) return;

        // Die Heimrecht-Regeln schließen sich aus: Anhaken der einen hakt die andere ab (wie in der Auslosung, #331).
        if (option.IstAktiv)
            OhneRueckschreiben(() =>
            {
                foreach (var andere in RegelVorgabeOptionen.Where(o =>
                             o.IstAktiv && AuslosungsRegelKompatibilitaet.SchliessenSichAus(option.Regelart, o.Regelart)))
                    andere.IstAktiv = false;
            });
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

    // ── Standard-Rangliste: Lostöpfe, Koeffizient, Spielklasse (Issue #349/#354/#393) ──

    partial void OnStandardRanglisteChanged(StandardRanglisteOption value)
    {
        if (_populiertGerade > 0) return;

        // Siehe RundenartAuswahl: null kommt nur transient aus der ComboBox und ist keine Nutzerauswahl.
        if (value is null) AusZeileWiederherstellen(zeile => StandardRangliste ??= zeile.StandardRangliste);
        // Ohne Standard-Rangliste gibt es keine automatische Topfbildung, in die der Titelverteidiger käme.
        else if (value.Wert is null) TitelverteidigerInTopf1 = false;
    }

    /// <summary>Genau eine Option je Rangliste (Referenzabgleich der ComboBox). Eine nicht (mehr) bekannte
    /// Rangliste erscheint als "keine".</summary>
    public StandardRanglisteOption StandardRanglisteOptionFuer(int? ranglisteId) =>
        StandardRanglistenOptionen.FirstOrDefault(o => o.Wert == ranglisteId && ranglisteId is not null) ?? RundeZeile.KeineStandardRangliste;

    // ── Auswahllisten aus den Stammdaten ──────────────────────────────────────

    public IEnumerable<Rundenart> RundenartOptionen => Enum.GetValues<Rundenart>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelwerkOptionen))]
    private ObservableCollection<Regelwerk> _regelwerke = [];

    public IReadOnlyList<RegelwerkOption> RegelwerkOptionen =>
        [RundeZeile.KeinRegelwerk, .. Regelwerke.Select(r => new RegelwerkOption(r.Name, r))];

    /// <summary>Einmal beim ersten Laden befüllt (analog <see cref="Regelwerke"/>), danach stabil — siehe
    /// <see cref="StandardRanglisteOptionFuer"/>.</summary>
    [ObservableProperty]
    private IReadOnlyList<StandardRanglisteOption> _standardRanglistenOptionen = [RundeZeile.KeineStandardRangliste];

    /// <summary>Lädt die Auswahllisten für Regelwerk und Standard-Rangliste, sofern noch nicht geschehen.</summary>
    public async Task StammdatenSicherstellenAsync()
    {
        if (Regelwerke.Count == 0)
            Regelwerke = new ObservableCollection<Regelwerk>(await _regelwerkService.AlleAbrufenAsync());
        if (StandardRanglistenOptionen.Count == 1)
            StandardRanglistenOptionen =
            [
                RundeZeile.KeineStandardRangliste,
                .. (await _ranglisteService.AlleOhneEintraegeAbrufenAsync())
                    .OrderBy(r => r.Name)
                    .ThenByDescending(r => r.Datum)
                    .Select(r => new StandardRanglisteOption($"{r.Name} ({r.Datum:dd.MM.yyyy})", r.Id))
            ];
    }

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
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => OhneRueckschreiben(wiederherstellen));

    /// <summary>Wie <see cref="NachComboBoxResetWiederherstellen"/> für Felder, deren richtigen Wert die gezeigte
    /// Zeile hält — sofern bis dahin noch dieselbe Runde gezeigt wird.</summary>
    private void AusZeileWiederherstellen(Action<RundeZeile> wiederherstellen)
    {
        if (_zeile is not { } zeile) return;
        NachComboBoxResetWiederherstellen(() =>
        {
            if (_zeile == zeile) wiederherstellen(zeile);
        });
    }
}
