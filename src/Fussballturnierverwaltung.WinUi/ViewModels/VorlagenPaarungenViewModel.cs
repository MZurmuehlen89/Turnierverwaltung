using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>"Paarungen"-Tab von <see cref="TurniervorlagenViewModel"/> (Issue #277) — vierter Tab, wie im
/// #284-Kommentar auf <see cref="TurniervorlagenViewModel"/> angekündigt. Zeigt nur Runden mit
/// <see cref="VorlagenRundeZeile.PaarungFestVorgegeben"/> = true; pro Runde eine aus Rundenart/Eingang/
/// GruppenAnzahl abgeleitete Slot-Liste (Ko-Paare oder Gruppenplätze, analog WPF-Original
/// <c>ManuellePaarungWorkspaceViewModel</c> auf Instanzebene), deren Platzhalter aus den eingehenden
/// Übergangsregeln der Runde stammen (<see cref="VorlagenPlatzhalterHilfsmittel"/>). Liest und schreibt die
/// geteilte Runden-Datenhaltung in <see cref="VorlagenRundenViewModel"/> (per DI referenziert, analog
/// <see cref="VorlagenRundenUebergaengeViewModel"/>) — kein eigener Datenbestand, kein eigenes Speichern.
///
/// Die Slot-Liste selbst ist rein lesend mit Klick-Selektion; die eigentliche Zuweisung passiert in einem
/// stabilen, separaten Formular (Populier-/Zurückschreib-Muster wie das Rundenformular) statt direkt über
/// TwoWay-Bindungen in der bei jedem Rundenwechsel neu aufgebauten Slot-Liste — konsistent mit der in #284
/// dokumentierten Lehre (TwoWay-Controls in einer neu aufgebauten Collection korrumpieren Daten bei
/// Fokuswechsel).</summary>
public partial class VorlagenPaarungenViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly VorlagenRundenViewModel _rundenVm;
    private int _populiertGerade;

    public VorlagenPaarungenViewModel(VorlagenRundenViewModel rundenVm)
    {
        _rundenVm = rundenVm;

        _rundenVm.PropertyChanged += OnRundenVmPropertyChanged;
        _rundenVm.UebergaengeGeaendert += () =>
        {
            AktualisierePaarungsRunden();
            AktualisiereVerfuegbarePlatzhalter();
        };

        AktualisierePaarungsRunden();
    }

    private void OnRundenVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VorlagenRundenViewModel.HatVorlage):
                OnPropertyChanged(nameof(HatVorlage));
                break;
            case nameof(VorlagenRundenViewModel.IstLadend):
                OnPropertyChanged(nameof(IstLadend));
                break;
            case nameof(VorlagenRundenViewModel.Fehlermeldung):
                OnPropertyChanged(nameof(Fehlermeldung));
                OnPropertyChanged(nameof(ZeigeMeldungsSpalte));
                break;
            case nameof(VorlagenRundenViewModel.RundenFehler):
                OnPropertyChanged(nameof(RundenFehler));
                OnPropertyChanged(nameof(HatRundenFehler));
                OnPropertyChanged(nameof(ZeigeMeldungsSpalte));
                break;
            case nameof(VorlagenRundenViewModel.RibbonTabTitel) or nameof(VorlagenRundenViewModel.NeuBefehl)
                or nameof(VorlagenRundenViewModel.SpeichernBefehl) or nameof(VorlagenRundenViewModel.AbbrechenBefehl)
                or nameof(VorlagenRundenViewModel.LoeschenBefehl) or nameof(VorlagenRundenViewModel.HatUngespeicherteAenderungen):
                OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
                break;
            case nameof(VorlagenRundenViewModel.Runden):
                AktualisierePaarungsRunden();
                break;
        }
    }

    // ── Geteilter Zustand (Pass-Through auf VorlagenRundenViewModel) ────────────
    public bool HatVorlage => _rundenVm.HatVorlage;
    public bool IstLadend => _rundenVm.IstLadend;
    public string? Fehlermeldung => _rundenVm.Fehlermeldung;
    public IReadOnlyList<string> RundenFehler => _rundenVm.RundenFehler;
    public bool HatRundenFehler => _rundenVm.HatRundenFehler;
    public bool ZeigeMeldungsSpalte => _rundenVm.ZeigeMeldungsSpalte;

    // ── Rundenliste (nur PaarungFestVorgegeben=true) ────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinePaarungsrundenVorhanden))]
    private ObservableCollection<VorlagenRundeZeile> _paarungsRunden = [];

    public bool KeinePaarungsrundenVorhanden => PaarungsRunden.Count == 0;

    private void AktualisierePaarungsRunden()
    {
        var vorherAusgewaehlt = AusgewaehlteRunde;
        PaarungsRunden = new ObservableCollection<VorlagenRundeZeile>(_rundenVm.Runden.Where(r => r.PaarungFestVorgegeben));
        AusgewaehlteRunde = vorherAusgewaehlt is not null && PaarungsRunden.Contains(vorherAusgewaehlt) ? vorherAusgewaehlt : null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    private VorlagenRundeZeile? _ausgewaehlteRunde;

    partial void OnAusgewaehlteRundeChanged(VorlagenRundeZeile? value)
    {
        AusgewaehlterSlot = null;
        AktualisiereSlots();
    }

    public bool FormularSichtbar => AusgewaehlteRunde is not null;

    // ── Slot-Liste der ausgewählten Runde (rein lesend, Klick wählt einen Slot) ─
    [ObservableProperty] private ObservableCollection<VorlagenPaarungSlot> _slots = [];

    private void AktualisiereSlots()
    {
        var runde = AusgewaehlteRunde;
        if (runde is null) { Slots = []; return; }

        var bestehende = runde.Paarungen.ToDictionary(p => (p.PaarungsNummer, p.SlotNr), p => p.PlatzhalterBezeichnung);
        var neue = new List<VorlagenPaarungSlot>();

        bool istGruppenModus = runde.Rundenart is Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem;
        if (istGruppenModus)
        {
            if (runde.Rundenart == Rundenart.Gruppenphase)
            {
                int gruppenAnzahl = Math.Max(1, runde.GruppenAnzahl ?? 1);
                int proGruppe = Math.Max(1, runde.EingangsMannschaftsanzahl / gruppenAnzahl);
                for (int g = 1; g <= gruppenAnzahl; g++)
                    for (int s = 1; s <= proGruppe; s++)
                        neue.Add(NeuerSlot(g, s, $"Gruppe {GruppenBuchstabe(g)} – Platz {s}", bestehende));
            }
            else
            {
                for (int s = 1; s <= Math.Max(1, runde.EingangsMannschaftsanzahl); s++)
                    neue.Add(NeuerSlot(1, s, $"Platz {s}", bestehende));
            }
        }
        else
        {
            int paare = Math.Max(1, (int)Math.Ceiling(runde.EingangsMannschaftsanzahl / 2.0));
            for (int p = 1; p <= paare; p++)
            {
                neue.Add(NeuerSlot(p, 1, $"Paarung {p} – Heim", bestehende));
                neue.Add(NeuerSlot(p, 2, $"Paarung {p} – Auswärts", bestehende));
            }
        }

        // Zurückschreiben: die neu aufgebaute Slot-Struktur wird die maßgebliche (Rundenart/Eingang können
        // sich seit dem Laden geändert haben) — alte, jetzt nicht mehr passende Slot-Positionen entfallen.
        runde.Paarungen = new ObservableCollection<VorlagenPaarungSlot>(neue);
        Slots = runde.Paarungen;
        AktualisiereVerfuegbarePlatzhalter();
    }

    private static VorlagenPaarungSlot NeuerSlot(int paarungsNr, int slotNr, string label, Dictionary<(int, int), string> bestehende)
    {
        bestehende.TryGetValue((paarungsNr, slotNr), out var bezeichnung);
        return new VorlagenPaarungSlot
        {
            PaarungsNummer = paarungsNr,
            SlotNr = slotNr,
            Label = label,
            PlatzhalterBezeichnung = string.IsNullOrEmpty(bezeichnung) ? null : bezeichnung
        };
    }

    private static string GruppenBuchstabe(int nr) => nr <= 26 ? ((char)('A' + nr - 1)).ToString() : $"G{nr}";

    // ── Verfügbare Platzhalter der ausgewählten Runde (aus eingehenden Übergangsregeln) ─
    [ObservableProperty] private ObservableCollection<string> _alleVerfuegbarenPlatzhalter = [];

    /// <summary>Für das Formular unten: alle generierten Platzhalter MINUS die von einem ANDEREN Slot bereits
    /// verwendeten, PLUS der aktuell zugewiesene des ausgewählten Slots selbst (bleibt sonst aus der Liste
    /// verschwunden, sobald er einmal zugewiesen ist).</summary>
    public IEnumerable<string> AuswaehlbarePlatzhalter
    {
        get
        {
            var slot = AusgewaehlterSlot;
            var belegt = Slots.Where(s => s != slot).Select(s => s.PlatzhalterBezeichnung).Where(b => b is not null).ToHashSet();
            return AlleVerfuegbarenPlatzhalter.Where(p => !belegt.Contains(p));
        }
    }

    private void AktualisiereVerfuegbarePlatzhalter()
    {
        var runde = AusgewaehlteRunde;
        if (runde is null) { AlleVerfuegbarenPlatzhalter = []; return; }

        var quellen = _rundenVm.AlleUebergaenge
            .Where(b => b.ZielRunde == runde && b.QuellRunde is not null)
            .SelectMany(b => b.Zeilen.Select(z => new VorlagenPlatzhalterQuelle(
                _rundenVm.Runden.IndexOf(b.QuellRunde!) + 1,
                b.QuellRunde!.Rundenart,
                b.QuellRunde!.Name,
                z.GruppeIndex, z.PlatzierungVon, z.PlatzierungBis, z.AnzahlBeste)))
            .ToList();

        AlleVerfuegbarenPlatzhalter = new ObservableCollection<string>(VorlagenPlatzhalterHilfsmittel.PlatzhalterGenerieren(quellen));
        OnPropertyChanged(nameof(AuswaehlbarePlatzhalter));
    }

    // ── Zuweisungsformular (stabil, Populier-/Zurückschreib-Muster) ─────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlotFormularSichtbar))]
    [NotifyPropertyChangedFor(nameof(AuswaehlbarePlatzhalter))]
    [NotifyCanExecuteChangedFor(nameof(ZuweisungEntfernenCommand))]
    private VorlagenPaarungSlot? _ausgewaehlterSlot;

    public bool SlotFormularSichtbar => AusgewaehlterSlot is not null;

    [ObservableProperty] private string? _formularPlatzhalter;

    partial void OnAusgewaehlterSlotChanged(VorlagenPaarungSlot? value)
    {
        _populiertGerade++;
        try { FormularPlatzhalter = value?.PlatzhalterBezeichnung; }
        finally { _populiertGerade--; }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade > 0 || e.PropertyName != nameof(FormularPlatzhalter) || AusgewaehlterSlot is null) return;

        AusgewaehlterSlot.PlatzhalterBezeichnung = FormularPlatzhalter;
        OnPropertyChanged(nameof(AuswaehlbarePlatzhalter));
        _rundenVm.PaarungenGeaendert();
    }

    [RelayCommand(CanExecute = nameof(KannZuweisungEntfernen))]
    private void ZuweisungEntfernen()
    {
        if (AusgewaehlterSlot is null) return;
        FormularPlatzhalter = null;
    }
    private bool KannZuweisungEntfernen() => AusgewaehlterSlot is not null;

    // ── IRibbonAware — reine Weiterleitung an den Runden-Tab (EIN gemeinsames Speichern für alle Tabs) ──
    public string RibbonTabTitel => "Paarungen";
    public ICommand? NeuBefehl => _rundenVm.NeuBefehl;
    public ICommand? SpeichernBefehl => _rundenVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => _rundenVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl => _rundenVm.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => _rundenVm.RibbonZusatzGruppen;
    public bool HatStandardAktionen => true;
    public bool HatLoeschenAktion => true;

    public bool HatUngespeicherteAenderungen => _rundenVm.HatUngespeicherteAenderungen;
}
