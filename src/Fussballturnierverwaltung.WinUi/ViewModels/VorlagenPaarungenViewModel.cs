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

/// <summary>Ein KO-Paar (Heim/Auswärts nebeneinander mit "vs" dazwischen) für die Anzeige im "Paarungen"-Tab
/// (Issue #277-Nachbesserung nach Nutzer-Rückmeldung: Slots waren als flache Liste nicht als "Mannschaft 1
/// gegen Mannschaft 2" erkennbar) — analog WPF-Original <c>ManuelleKoPaarungAnzeige</c>.</summary>
public record VorlagenPaarungKoPaar(int PaarungsNummer, VorlagenPaarungSlot HeimSlot, VorlagenPaarungSlot AuswaertsSlot);

/// <summary>Eine Gruppe (Gruppenphase) bzw. die einzelne "Teilnehmer"-Gruppe (Liga/SchweizerSystem) für die
/// Anzeige im "Paarungen"-Tab — analog WPF-Original <c>ManuelleGruppe</c>.</summary>
public class VorlagenPaarungGruppe
{
    public required int Nummer { get; init; }
    public required string Name { get; init; }
    public required ObservableCollection<VorlagenPaarungSlot> Slots { get; init; }
}

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

    /// <summary>Nach Speichern/Abbrechen (<see cref="VorlagenRundenViewModel.VorlageSetzenAsync"/>) besteht
    /// <see cref="VorlagenRundenViewModel.Runden"/> aus komplett neuen Objektinstanzen (RundeZuZeile erzeugt
    /// je Ladevorgang frische Zeilen) — ein Vergleich per Objektreferenz (wie zuvor) findet die vorherige
    /// Auswahl dort nie wieder. Analog zum selben Fund/Fix in <see cref="VorlagenRundenViewModel.VorlageSetzenAsync"/>:
    /// per Id wiederherstellen, sonst per Listenposition. Der Id-Treffer schlägt nach Speichern (anders als bei
    /// Abbrechen) für JEDE Runde fehl, da TurnierVorlageService.RundenSynchronisierenAsync alle Runden per
    /// Löschen+Neuanlegen ersetzt — deshalb bei fehlgeschlagenem Id-Treffer IMMER per Position weitersuchen,
    /// nicht nur wenn keine alte Id vorhanden war (Nutzer-Rückmeldung: Fix wirkte nur bei Abbrechen).</summary>
    private void AktualisierePaarungsRunden()
    {
        var vorherAusgewaehlteId = AusgewaehlteRunde?.Id;
        var vorherIndex = AusgewaehlteRunde is not null ? PaarungsRunden.IndexOf(AusgewaehlteRunde) : -1;

        PaarungsRunden = new ObservableCollection<VorlagenRundeZeile>(_rundenVm.Runden.Where(r => r.PaarungFestVorgegeben));

        VorlagenRundeZeile? wiederhergestellt = vorherAusgewaehlteId is int id
            ? PaarungsRunden.FirstOrDefault(r => r.Id == id)
            : null;
        if (wiederhergestellt is null && vorherIndex >= 0 && vorherIndex < PaarungsRunden.Count)
            wiederhergestellt = PaarungsRunden[vorherIndex];
        AusgewaehlteRunde = wiederhergestellt;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormularSichtbar))]
    [NotifyPropertyChangedFor(nameof(IstKoModus))]
    [NotifyPropertyChangedFor(nameof(IstGruppenModus))]
    private VorlagenRundeZeile? _ausgewaehlteRunde;

    partial void OnAusgewaehlteRundeChanged(VorlagenRundeZeile? value)
    {
        AusgewaehlterSlot = null;
        AktualisiereSlots();
    }

    public bool FormularSichtbar => AusgewaehlteRunde is not null;

    /// <summary>Für die paarweise (KO) bzw. gruppierte (Gruppenphase/Liga/SchweizerSystem) Anzeige (Nutzer-
    /// Rückmeldung nach #277-Test: Slots waren als flache Liste nicht als "Mannschaft 1 gegen Mannschaft 2"
    /// erkennbar) — analog WPF-Original <see cref="ManuellePaarungWorkspaceViewModel.IstKoModus"/>.</summary>
    public bool IstKoModus => AusgewaehlteRunde?.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
    public bool IstGruppenModus => AusgewaehlteRunde is not null && !IstKoModus;

    // ── Slot-Liste der ausgewählten Runde (rein lesend, Drop-Ziel für Drag&Drop aus "Verfügbar") ─
    [ObservableProperty] private ObservableCollection<VorlagenPaarungSlot> _slots = [];

    /// <summary>Paarweise Anzeige für KO-Rundenarten (Heim/Auswärts nebeneinander mit "vs" dazwischen) —
    /// analog WPF-Original <see cref="ManuellePaarungWorkspaceViewModel.KoPaarungen"/>.</summary>
    [ObservableProperty] private ObservableCollection<VorlagenPaarungKoPaar> _koPaare = [];

    /// <summary>Gruppierte Anzeige für Gruppenphase (echte Gruppen) bzw. Liga/SchweizerSystem (eine einzelne
    /// "Teilnehmer"-Gruppe) — analog WPF-Original <see cref="ManuellePaarungWorkspaceViewModel.Gruppen"/>.</summary>
    [ObservableProperty] private ObservableCollection<VorlagenPaarungGruppe> _paarungsGruppen = [];

    private void AktualisiereSlots()
    {
        var runde = AusgewaehlteRunde;
        if (runde is null) { Slots = []; KoPaare = []; PaarungsGruppen = []; return; }

        var bestehende = runde.Paarungen.ToDictionary(p => (p.PaarungsNummer, p.SlotNr), p => p.PlatzhalterBezeichnung);
        var neue = new List<VorlagenPaarungSlot>();

        if (IstGruppenModus)
        {
            if (runde.Rundenart == Rundenart.Gruppenphase)
            {
                int gruppenAnzahl = Math.Max(1, runde.GruppenAnzahl ?? 1);
                int proGruppe = Math.Max(1, runde.EingangsMannschaftsanzahl / gruppenAnzahl);
                for (int g = 1; g <= gruppenAnzahl; g++)
                    for (int s = 1; s <= proGruppe; s++)
                        neue.Add(NeuerSlot(g, s, $"Platz {s}", bestehende));
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
                neue.Add(NeuerSlot(p, 1, "Heim", bestehende));
                neue.Add(NeuerSlot(p, 2, "Auswärts", bestehende));
            }
        }

        // Zurückschreiben: die neu aufgebaute Slot-Struktur wird die maßgebliche (Rundenart/Eingang können
        // sich seit dem Laden geändert haben) — alte, jetzt nicht mehr passende Slot-Positionen entfallen.
        runde.Paarungen = new ObservableCollection<VorlagenPaarungSlot>(neue);
        Slots = runde.Paarungen;

        if (IstGruppenModus)
        {
            KoPaare = [];
            PaarungsGruppen = new ObservableCollection<VorlagenPaarungGruppe>(Slots
                .GroupBy(s => s.PaarungsNummer)
                .Select(g => new VorlagenPaarungGruppe
                {
                    Nummer = g.Key,
                    Name = runde.Rundenart == Rundenart.Gruppenphase ? $"Gruppe {GruppenBuchstabe(g.Key)}" : "Teilnehmer",
                    Slots = new ObservableCollection<VorlagenPaarungSlot>(g.OrderBy(s => s.SlotNr))
                }));
        }
        else
        {
            PaarungsGruppen = [];
            KoPaare = new ObservableCollection<VorlagenPaarungKoPaar>(Slots
                .GroupBy(s => s.PaarungsNummer)
                .Select(g => new VorlagenPaarungKoPaar(g.Key, g.First(s => s.SlotNr == 1), g.First(s => s.SlotNr == 2))));
        }

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
        OnPropertyChanged(nameof(VerfuegbarePlatzhalterPool));
    }

    /// <summary>Alle generierten Platzhalter, die noch KEINEM Slot zugewiesen sind — Drag-Quelle "Verfügbar"
    /// (Issue #277-Nachbesserung: Drag&amp;Drop auf die Slots, analog WPF-Original
    /// <see cref="ManuellePaarungWorkspaceViewModel.VerfuegbareTeilnehmer"/>).</summary>
    public IEnumerable<string> VerfuegbarePlatzhalterPool =>
        AlleVerfuegbarenPlatzhalter.Where(p => !Slots.Any(s => s.PlatzhalterBezeichnung == p));

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
        OnPropertyChanged(nameof(VerfuegbarePlatzhalterPool));
        _rundenVm.PaarungenGeaendert();
    }

    [RelayCommand(CanExecute = nameof(KannZuweisungEntfernen))]
    private void ZuweisungEntfernen()
    {
        if (AusgewaehlterSlot is null) return;
        FormularPlatzhalter = null;
    }
    private bool KannZuweisungEntfernen() => AusgewaehlterSlot is not null;

    // ── Drag&Drop-Zuweisung (Issue #277-Nachbesserung, Code-behind-Aufrufer: VorlagenPaarungenPage) ──────

    /// <summary>Weist einen Platzhalter direkt einem Slot zu — unabhängig von der aktuellen Formular-Auswahl
    /// (das Drop-Ziel kann ein beliebiger Slot sein). Hält <see cref="FormularPlatzhalter"/> synchron, falls
    /// der Ziel-Slot zufällig der gerade ausgewählte ist.</summary>
    public void PlatzhalterZuweisen(VorlagenPaarungSlot slot, string platzhalter)
    {
        if (slot.PlatzhalterBezeichnung == platzhalter) return;
        slot.PlatzhalterBezeichnung = platzhalter;
        if (AusgewaehlterSlot == slot)
        {
            _populiertGerade++;
            try { FormularPlatzhalter = platzhalter; }
            finally { _populiertGerade--; }
        }
        OnPropertyChanged(nameof(AuswaehlbarePlatzhalter));
        OnPropertyChanged(nameof(VerfuegbarePlatzhalterPool));
        _rundenVm.PaarungenGeaendert();
    }

    public void PlatzhalterAusSlotEntfernen(VorlagenPaarungSlot slot)
    {
        if (slot.PlatzhalterBezeichnung is null) return;
        slot.PlatzhalterBezeichnung = null;
        if (AusgewaehlterSlot == slot)
        {
            _populiertGerade++;
            try { FormularPlatzhalter = null; }
            finally { _populiertGerade--; }
        }
        OnPropertyChanged(nameof(AuswaehlbarePlatzhalter));
        OnPropertyChanged(nameof(VerfuegbarePlatzhalterPool));
        _rundenVm.PaarungenGeaendert();
    }

    public VorlagenPaarungSlot? NaechsterFreierSlot() => Slots.FirstOrDefault(s => !s.IstBesetzt);

    // ── IRibbonAware — reine Weiterleitung an den Runden-Tab (EIN gemeinsames Speichern für alle Tabs) ──
    public string RibbonTabTitel => "Vorbelegung";
    public ICommand? NeuBefehl => _rundenVm.NeuBefehl;
    public ICommand? SpeichernBefehl => _rundenVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => _rundenVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl => _rundenVm.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => _rundenVm.RibbonZusatzGruppen;
    public bool HatStandardAktionen => true;
    public bool HatLoeschenAktion => true;

    public bool HatUngespeicherteAenderungen => _rundenVm.HatUngespeicherteAenderungen;
}
