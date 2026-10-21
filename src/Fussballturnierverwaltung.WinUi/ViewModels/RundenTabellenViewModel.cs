using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public class UebergreifendeRangfolgeAnzeige
{
    public int Platzierung { get; init; }
    public int AnzahlBeste { get; init; }
    public string ZielRundeName { get; init; } = "";
    public IReadOnlyList<TabellenEintrag> Rangfolge { get; init; } = [];
}

/// <summary>Tabellen der fokussierten Runde im „Turnierbaum“-Bereich (#438): Gruppen- und Ligatabellen,
/// Tabellenzonen, Stand „bis Spieltag“, Heim-/Auswärts-/Halbserienansicht und die gruppenübergreifende Rangfolge
/// der Besten. Kind-ViewModel von <see cref="TurnierBaumViewModel"/> — Einstieg ist
/// <see cref="FokusRundeSetzenAsync"/>; Fehler meldet es über <see cref="FehlerGemeldet"/>, die Anzeige liegt
/// beim Eltern-ViewModel.</summary>
public partial class RundenTabellenViewModel : ObservableObject
{
    private readonly TabellenService _tabellenService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly RelayCommand<TabellenAnsicht?> _ligaAnsichtWaehlenBefehl;
    private TurnierRunde? _fokusRunde;

    /// <summary>Neue Fehlermeldung des Tabellenladens; <c>null</c> räumt die bisherige ab.</summary>
    public event Action<string?>? FehlerGemeldet;

    /// <summary>Gemeinsamer Schutz der Turnierdurchführung (#415): Das Umschalten von Tabellen-Spieltag und
    /// Liga-Ansicht lädt die Tabelle neu und darf dabei weder eine laufende Spielplan-Aktion noch einen
    /// Turnier-/Reiterwechsel treffen.</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    public RundenTabellenViewModel(TabellenService tabellenService, UebergangsregelService uebergangsregelService)
    {
        _tabellenService = tabellenService;
        _uebergangsregelService = uebergangsregelService;

        _ligaAnsichtWaehlenBefehl = new RelayCommand<TabellenAnsicht?>(a => AusgewaehlteLigaAnsicht = a ?? TabellenAnsicht.Gesamt);
    }

    // ── Fokussierte Runde ─────────────────────────────────────────────────────

    /// <summary>Stellt die Anzeige auf die Runde um und lädt ihre Tabellen; bei einer K.-o.-Runde (oder
    /// <c>null</c>) zeigt dieses ViewModel nichts.</summary>
    public async Task FokusRundeSetzenAsync(TurnierRunde? runde)
    {
        FokusWechseln(runde);
        AnzeigeartSetzen(runde?.Rundenart);
        if (ZeigtTabelle) await SpieltageUndTabelleLadenAsync(runde!.Id);
    }

    // Spieltag und Ansicht gehören zur bisherigen Runde und fallen auf den Standard zurück. Solange das läuft,
    // ist keine Runde fokussiert — sonst lüde jeder der beiden Änderungs-Haken die Tabelle der alten Runde neu.
    private void FokusWechseln(TurnierRunde? runde)
    {
        if (runde?.Id != _fokusRunde?.Id) FokussierteGruppenNummer = null;

        _fokusRunde = null;
        TabellenBisSpieltag = null;
        AusgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
        VerfuegbareSpieltage = [];
        Zonen = [];
        _fokusRunde = runde;
        OnPropertyChanged(nameof(ZeigtHinRueckrundenTabs));
    }

    private void AnzeigeartSetzen(Rundenart? rundenart)
    {
        ZeigtGruppenphase = rundenart == Rundenart.Gruppenphase;
        ZeigtLigaTabelle = rundenart is Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem;
        ZeigtBuchholzSpalte = rundenart == Rundenart.SchweizerSystem;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtTabelle))]
    private bool _zeigtGruppenphase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtTabelle))]
    private bool _zeigtLigaTabelle;

    /// <summary>Die fokussierte Runde hat Tabellen — der Turnierbaum tritt dafür zurück.</summary>
    public bool ZeigtTabelle => ZeigtGruppenphase || ZeigtLigaTabelle;

    [ObservableProperty]
    private bool _zeigtBuchholzSpalte;

    // ── Tabellen ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGruppenTabellenAnzeige))]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private ObservableCollection<GruppenTabelle> _gruppenTabellenAnzeige = [];
    public bool HatGruppenTabellenAnzeige => GruppenTabellenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private int? _fokussierteGruppenNummer;

    public IReadOnlyList<GruppenTabelle> AnzeigeGruppenTabellen =>
        FokussierteGruppenNummer is { } nr
            ? GruppenTabellenAnzeige.Where(g => g.GruppenNummer == nr).ToList()
            : GruppenTabellenAnzeige;

    public void FokussiereGruppe(int? gruppenNummer) => FokussierteGruppenNummer = gruppenNummer;

    /// <summary>Klick auf eine Gruppenkarte (Gruppennummer) — was daraus folgt, entscheidet der Besitzer.</summary>
    public event Action<int>? GruppeAngeklickt;

    [RelayCommand]
    private void GruppeAnklicken(GruppenTabelle? gruppe)
    {
        if (gruppe is not null) GruppeAngeklickt?.Invoke(gruppe.GruppenNummer);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUebergreifendeRangfolgenAnzeige))]
    private ObservableCollection<UebergreifendeRangfolgeAnzeige> _uebergreifendeRangfolgenAnzeige = [];
    public bool HatUebergreifendeRangfolgenAnzeige => UebergreifendeRangfolgenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatLigaTabelleAnzeige))]
    private ObservableCollection<TabellenEintrag> _ligaTabelleAnzeige = [];
    public bool HatLigaTabelleAnzeige => LigaTabelleAnzeige.Count > 0;

    /// <summary>Legende der Tabellenzonen der fokussierten Runde (Issue #358) — leer, wenn die Runde keine
    /// ausgehenden Übergangsregeln hat oder eine Teilansicht (Heim, Hinrunde …) gezeigt wird.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatZonenLegende))]
    private IReadOnlyList<ZonenLegendenEintrag> _zonenLegende = [];
    public bool HatZonenLegende => ZonenLegende.Count > 0;

    /// <summary>Alle Zonen der fokussierten Runde — auch dann, wenn eine Teilansicht sie in der Tabelle ausblendet
    /// (für die Bänder im Tabellenverlauf, #480).</summary>
    [ObservableProperty] private IReadOnlyList<TabellenZone> _zonen = [];

    // ── Stand „bis Spieltag“ und Liga-Ansicht ─────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabellenSpieltagOptionen))]
    [NotifyPropertyChangedFor(nameof(HatVerfuegbareSpieltage))]
    private ObservableCollection<int> _verfuegbareSpieltage = [];

    public IReadOnlyList<int> TabellenSpieltagOptionen => new[] { 0 }.Concat(VerfuegbareSpieltage).ToList();
    public bool HatVerfuegbareSpieltage => VerfuegbareSpieltage.Count > 0;

    [ObservableProperty] private int? _tabellenBisSpieltag;

    public int TabellenBisSpieltagAuswahl
    {
        get => TabellenBisSpieltag ?? 0;
        set => TabellenBisSpieltag = value == 0 ? null : value;
    }

    partial void OnTabellenBisSpieltagChanged(int? value)
    {
        OnPropertyChanged(nameof(TabellenBisSpieltagAuswahl));
        TabelleNeuLadenMitSchutz();
    }

    public void ZeigeAktuellenStand() => TabellenBisSpieltag = null;

    [ObservableProperty] private TabellenAnsicht _ausgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
    public bool ZeigtHinRueckrundenTabs => _fokusRunde?.MitRueckspiel == true;
    public ICommand LigaAnsichtWaehlenBefehl => _ligaAnsichtWaehlenBefehl;

    partial void OnAusgewaehlteLigaAnsichtChanged(TabellenAnsicht value) => TabelleNeuLadenMitSchutz();

    // ── Laden ─────────────────────────────────────────────────────────────────

    private async Task SpieltageUndTabelleLadenAsync(int rundeId)
    {
        FehlerGemeldet?.Invoke(null);
        try
        {
            var verfuegbareSpieltage = await _tabellenService.VerfuegbareSpieltageAsync(rundeId);
            VerfuegbareSpieltage = new ObservableCollection<int>(verfuegbareSpieltage);

            await TabelleNeuLadenAsync();
        }
        catch (Exception ex)
        {
            FehlerGemeldet?.Invoke(ex.Message);
        }
    }

    private void TabelleNeuLadenMitSchutz()
    {
        if (_fokusRunde is null) return;
        _ = DbContextSchutz.AusfuehrenAsync(TabelleNeuLadenAsync, ex => FehlerGemeldet?.Invoke(ex.Message));
    }

    private async Task TabelleNeuLadenAsync()
    {
        if (_fokusRunde is not { } runde) return;
        FehlerGemeldet?.Invoke(null);
        try
        {
            // Issue #358/#420: Zonen aus den ausgehenden Übergangsregeln und den Tabellenbereichen der Runde.
            var regeln = await _uebergangsregelService.AlleNachQuellRundeIdAsync(runde.Id);
            var zonen = await _tabellenService.ZonenErmittelnAsync(runde.Id, regeln);
            Zonen = zonen;

            if (ZeigtGruppenphase)
                await GruppenTabellenLadenAsync(runde.Id, regeln, zonen);
            else if (ZeigtLigaTabelle)
                await LigaTabelleLadenAsync(runde.Id, zonen);
        }
        catch (Exception ex)
        {
            FehlerGemeldet?.Invoke(ex.Message);
        }
    }

    // Issue #358: Der Sicher-Punkt gilt nur für den aktuellen Stand — bei einem historischen Spieltag passen die
    // möglichen Ränge nicht zur Tabelle.
    private bool MitSicherPunkt => TabellenBisSpieltag is null;

    private async Task GruppenTabellenLadenAsync(
        int rundeId, IReadOnlyList<Uebergangsregel> regeln, IReadOnlyList<TabellenZone> zonen)
    {
        var tabellen = await _tabellenService.GruppenTabellenBerechnenAsync(rundeId, TabellenBisSpieltag);
        foreach (var gruppe in tabellen)
            TabellenZonen.Zuweisen(gruppe.Tabelle, zonen, gruppe.GruppenNummer, MitSicherPunkt);
        GruppenTabellenAnzeige = new ObservableCollection<GruppenTabelle>(tabellen);
        await UebergreifendeRangfolgenLadenAsync(regeln, zonen);
        ZonenLegende = TabellenZonen.Legende(zonen);
    }

    private async Task LigaTabelleLadenAsync(int rundeId, IReadOnlyList<TabellenZone> zonen)
    {
        // Heim-/Auswärts- und Halbserien-Tabellen sind keine Wertung für das Weiterkommen - ohne Zonen.
        if (AusgewaehlteLigaAnsicht != TabellenAnsicht.Gesamt) zonen = [];

        var tabelle = await _tabellenService.TabelleBerechnenAsync(rundeId, AusgewaehlteLigaAnsicht, TabellenBisSpieltag);
        TabellenZonen.Zuweisen(tabelle, zonen, gruppenNummer: null, MitSicherPunkt);
        LigaTabelleAnzeige = new ObservableCollection<TabellenEintrag>(tabelle);
        ZonenLegende = TabellenZonen.Legende(zonen);
    }

    private async Task UebergreifendeRangfolgenLadenAsync(
        IReadOnlyList<Uebergangsregel> ausgehendeRegeln, IReadOnlyList<TabellenZone> zonen)
    {
        var anzeigen = new List<UebergreifendeRangfolgeAnzeige>();
        foreach (var regel in ausgehendeRegeln)
        {
            if (regel.AnzahlBeste is { } anzahlBeste)
                anzeigen.Add(await RangfolgeDerBestenLadenAsync(regel, anzahlBeste, zonen));
        }

        UebergreifendeRangfolgenAnzeige = new ObservableCollection<UebergreifendeRangfolgeAnzeige>(anzeigen);
    }

    private async Task<UebergreifendeRangfolgeAnzeige> RangfolgeDerBestenLadenAsync(
        Uebergangsregel regel, int anzahlBeste, IReadOnlyList<TabellenZone> zonen)
    {
        var rangfolge = await _tabellenService.UebergreifendeRangfolgeBerechnenAsync(regel.QuellRundeId, regel.PlatzierungVon);
        var qualifikanten = await _uebergangsregelService.QualifikantenErmittelnAsync(regel.Id);
        TabellenZonen.BesteZuweisen(rangfolge, zonen, regel.ZielRundeId, anzahlBeste, qualifikanten.Values);

        return new UebergreifendeRangfolgeAnzeige
        {
            Platzierung = regel.PlatzierungVon,
            AnzahlBeste = anzahlBeste,
            ZielRundeName = regel.ZielRunde.Name,
            Rangfolge = rangfolge
        };
    }
}
