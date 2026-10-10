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

    /// <summary>Neue Fehlermeldung des Tabellenladens; <c>null</c> räumt die bisherige ab.</summary>
    public event Action<string?>? FehlerGemeldet;

    // ── Gruppenphase-/Liga-Darstellung ─────────────────────────────────────────

    [ObservableProperty]
    private bool _zeigtGruppenphase;

    [ObservableProperty]
    private bool _zeigtLigaTabelle;

    [ObservableProperty]
    private bool _zeigtBuchholzSpalte;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGruppenTabellenAnzeige))]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private ObservableCollection<GruppenTabelle> _gruppenTabellenAnzeige = [];
    public bool HatGruppenTabellenAnzeige => GruppenTabellenAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatLigaTabelleAnzeige))]
    private ObservableCollection<TabellenEintrag> _ligaTabelleAnzeige = [];
    public bool HatLigaTabelleAnzeige => LigaTabelleAnzeige.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUebergreifendeRangfolgenAnzeige))]
    private ObservableCollection<UebergreifendeRangfolgeAnzeige> _uebergreifendeRangfolgenAnzeige = [];
    public bool HatUebergreifendeRangfolgenAnzeige => UebergreifendeRangfolgenAnzeige.Count > 0;

    /// <summary>Legende der Tabellenzonen der fokussierten Runde (Issue #358) — leer, wenn die Runde keine
    /// ausgehenden Übergangsregeln hat oder eine Teilansicht (Heim, Hinrunde …) gezeigt wird.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatZonenLegende))]
    private IReadOnlyList<ZonenLegendenEintrag> _zonenLegende = [];
    public bool HatZonenLegende => ZonenLegende.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzeigeGruppenTabellen))]
    private int? _fokussierteGruppenNummer;

    public IReadOnlyList<GruppenTabelle> AnzeigeGruppenTabellen =>
        FokussierteGruppenNummer is { } nr
            ? GruppenTabellenAnzeige.Where(g => g.GruppenNummer == nr).ToList()
            : GruppenTabellenAnzeige;

    public void FokussiereGruppe(int? gruppenNummer) => FokussierteGruppenNummer = gruppenNummer;

    // ── Tabellenstand-Snapshot "Bis Spieltag" ────────────────────────────────

    private TurnierRunde? _fokusRunde;

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
        if (_fokusRunde is not null) TabelleNeuLadenMitSchutz();
    }

    public void ZeigeAktuellenStand() => TabellenBisSpieltag = null;

    [ObservableProperty] private TabellenAnsicht _ausgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
    public bool ZeigtHinRueckrundenTabs => _fokusRunde?.MitRueckspiel == true;
    public ICommand LigaAnsichtWaehlenBefehl => _ligaAnsichtWaehlenBefehl;
    private readonly RelayCommand<TabellenAnsicht?> _ligaAnsichtWaehlenBefehl;

    partial void OnAusgewaehlteLigaAnsichtChanged(TabellenAnsicht value)
    {
        if (_fokusRunde is not null) TabelleNeuLadenMitSchutz();
    }

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

    /// <summary>Stellt die Anzeige auf die Runde um und lädt ihre Tabellen; bei einer K.-o.-Runde (oder
    /// <c>null</c>) zeigt dieses ViewModel nichts.</summary>
    public async Task FokusRundeSetzenAsync(TurnierRunde? runde)
    {
        bool istEchterRundenwechsel = runde?.Id != _fokusRunde?.Id;
        if (istEchterRundenwechsel)
            FokussierteGruppenNummer = null;

        _fokusRunde = null;
        TabellenBisSpieltag = null;
        AusgewaehlteLigaAnsicht = TabellenAnsicht.Gesamt;
        VerfuegbareSpieltage = [];
        _fokusRunde = runde;
        OnPropertyChanged(nameof(ZeigtHinRueckrundenTabs));

        var rundenart = runde?.Rundenart;
        bool istGruppenphase = rundenart == Rundenart.Gruppenphase;
        bool istLigaArtig = rundenart is Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem;

        ZeigtGruppenphase = istGruppenphase;
        ZeigtLigaTabelle = istLigaArtig;
        ZeigtBuchholzSpalte = rundenart == Rundenart.SchweizerSystem;

        if (!istGruppenphase && !istLigaArtig) return;

        FehlerGemeldet?.Invoke(null);
        try
        {
            var verfuegbareSpieltage = await _tabellenService.VerfuegbareSpieltageAsync(runde!.Id);
            VerfuegbareSpieltage = new ObservableCollection<int>(verfuegbareSpieltage);

            await TabelleNeuLadenAsync();
        }
        catch (Exception ex)
        {
            FehlerGemeldet?.Invoke(ex.Message);
        }
    }

    private void TabelleNeuLadenMitSchutz() =>
        _ = DbContextSchutz.AusfuehrenAsync(TabelleNeuLadenAsync, ex => FehlerGemeldet?.Invoke(ex.Message));

    private async Task TabelleNeuLadenAsync()
    {
        if (_fokusRunde is null) return;
        FehlerGemeldet?.Invoke(null);
        try
        {
            // Issue #358: Zonen allein aus den ausgehenden Übergangsregeln. Der Sicher-Punkt gilt nur für den
            // aktuellen Stand — bei einem historischen Spieltag passen die möglichen Ränge nicht zur Tabelle.
            var regeln = await _uebergangsregelService.AlleNachQuellRundeIdAsync(_fokusRunde.Id);
            var zonen = TabellenZonen.AusRegeln(regeln);
            bool mitSicherheit = TabellenBisSpieltag is null;

            if (ZeigtGruppenphase)
            {
                var tabellen = await _tabellenService.GruppenTabellenBerechnenAsync(_fokusRunde.Id, TabellenBisSpieltag);
                foreach (var gruppe in tabellen)
                    TabellenZonen.Zuweisen(gruppe.Tabelle, zonen, gruppe.GruppenNummer, mitSicherheit);
                GruppenTabellenAnzeige = new ObservableCollection<GruppenTabelle>(tabellen);
                await UebergreifendeRangfolgenLadenAsync(regeln, zonen);
                ZonenLegende = TabellenZonen.Legende(zonen);
            }
            else if (ZeigtLigaTabelle)
            {
                // Heim-/Auswärts- und Halbserien-Tabellen sind keine Wertung für das Weiterkommen - ohne Zonen.
                if (AusgewaehlteLigaAnsicht != TabellenAnsicht.Gesamt) zonen = [];

                var tabelle = await _tabellenService.TabelleBerechnenAsync(_fokusRunde.Id, AusgewaehlteLigaAnsicht, TabellenBisSpieltag);
                TabellenZonen.Zuweisen(tabelle, zonen, gruppenNummer: null, mitSicherheit);
                LigaTabelleAnzeige = new ObservableCollection<TabellenEintrag>(tabelle);
                ZonenLegende = TabellenZonen.Legende(zonen);
            }
        }
        catch (Exception ex)
        {
            FehlerGemeldet?.Invoke(ex.Message);
        }
    }

    private async Task UebergreifendeRangfolgenLadenAsync(
        IReadOnlyList<Uebergangsregel> ausgehendeRegeln, IReadOnlyList<TabellenZone> zonen)
    {
        var anzeigen = new List<UebergreifendeRangfolgeAnzeige>();
        foreach (var regel in ausgehendeRegeln.Where(r => r.AnzahlBeste is not null))
        {
            var rangfolge = await _tabellenService.UebergreifendeRangfolgeBerechnenAsync(regel.QuellRundeId, regel.PlatzierungVon);
            var qualifikanten = await _uebergangsregelService.QualifikantenErmittelnAsync(regel.Id);

            // Issue #358: Hier fällt die Entscheidung über das Weiterkommen — die Plätze der Besten tragen die
            // Farbe der Zielrunde, der Punkt folgt, sobald die Qualifikanten feststehen.
            var zielFarbe = TabellenZonen.ZielFarbe(zonen, regel.ZielRundeId);
            foreach (var eintrag in rangfolge)
                eintrag.ZonenFarbe = eintrag.Rang <= regel.AnzahlBeste ? zielFarbe : null;

            if (qualifikanten.Values.All(v => v.HasValue))
            {
                var qualifizierteIds = qualifikanten.Values.Select(v => v!.Value).ToHashSet();
                foreach (var eintrag in rangfolge)
                {
                    bool qualifiziert = qualifizierteIds.Contains(eintrag.Mannschaft.Id);
                    eintrag.Qualifikationsstatus = qualifiziert
                        ? Qualifikationsstatus.SicherQualifiziert
                        : Qualifikationsstatus.SicherAusgeschieden;
                    eintrag.SicherPositioniert = true;
                    eintrag.PunktFarbe = qualifiziert ? zielFarbe : ZonenFarbe.Rot;
                }
            }

            anzeigen.Add(new UebergreifendeRangfolgeAnzeige
            {
                Platzierung = regel.PlatzierungVon,
                AnzahlBeste = regel.AnzahlBeste!.Value,
                ZielRundeName = regel.ZielRunde.Name,
                Rangfolge = rangfolge
            });
        }

        UebergreifendeRangfolgenAnzeige = new ObservableCollection<UebergreifendeRangfolgeAnzeige>(anzeigen);
    }
}
