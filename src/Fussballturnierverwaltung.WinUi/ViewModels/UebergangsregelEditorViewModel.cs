using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Übergangsregel-Bereich im Runden-Detail (Issue #348, vorher eigener Reiter "Übergänge" aus #297) —
/// Eingehende/Ausgehende Übergangsregeln der fokussierten Runde (rein lesende Zusammenfassung, Klick wählt eine
/// Regel aus) + ein Regelformular. Kind-ViewModel von <see cref="RundenViewModel"/>, das alleiniger Datenhalter
/// bleibt — kein eigener Datenbestand, kein eigenes Speichern. Bewusst ausgelagert statt direkt in
/// <see cref="RundenViewModel"/>, um dessen Umfang nicht weiter zu vergrößern.</summary>
public partial class UebergangsregelEditorViewModel : ObservableObject
{
    private readonly RundenViewModel _rundenVm;

    public UebergangsregelEditorViewModel(RundenViewModel rundenVm)
    {
        _rundenVm = rundenVm;
        _rundenVm.PropertyChanged += OnRundenVmPropertyChanged;
        _rundenVm.UebergaengeGeaendert += AktualisiereRegelListen;
    }

    private void OnRundenVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RundenViewModel.AusgewaehlteRunde):
                AusgewaehlteRegel = null; // schließt ein offenes Regelformular bei Rundenwechsel
                NeueEingehendeRegelCommand.NotifyCanExecuteChanged();
                NeueAusgehendeRegelCommand.NotifyCanExecuteChanged();
                AktualisiereRegelListen();
                Offen = EingehendeRegeln.Count + AusgehendeRegeln.Count > 0;
                break;
            case nameof(RundenViewModel.Runden):
                // RundenZuruecksetzen weist bei jedem Instanz-/Vorlagenwechsel eine NEUE Collection-Instanz zu —
                // ohne Weiterleitung bleiben die Quelle/Ziel-ComboBoxen im Regelformular an der zuerst geladenen
                // Rundenliste hängen. Regelformular VORHER schließen (nicht erst im nachfolgenden
                // AusgewaehlteRunde-Case): Runden wird bereits VOR AusgewaehlteRunde=null neu zugewiesen, ein
                // offenes Formular würde sonst die neue ItemsSource sehen — die ComboBox schreibt ihr (jetzt
                // fremdes) Element dabei per TwoWay auf RegelQuelle/RegelZiel zurück, was in einer noch offenen,
                // aber schon ungültigen Regel der ALTEN Vorlage landet (NullReferenceException-Fund aus #297).
                AusgewaehlteRegel = null;
                OnPropertyChanged(nameof(Runden));
                break;
        }
    }

    public ObservableCollection<RundeZeile> Runden => _rundenVm.Runden;

    // ── Eingehende/Ausgehende Übergangsregeln der fokussierten Runde (rein lesend) ──
    public ObservableCollection<UebergangsregelBlock> EingehendeRegeln { get; } = [];
    public ObservableCollection<UebergangsregelBlock> AusgehendeRegeln { get; } = [];

    private void AktualisiereRegelListen()
    {
        EingehendeRegeln.Clear();
        AusgehendeRegeln.Clear();

        var ausgewaehlt = _rundenVm.AusgewaehlteRunde;
        if (ausgewaehlt is not null)
        {
            foreach (var b in _rundenVm.AlleUebergaenge.Where(b => b.ZielRunde == ausgewaehlt))
                EingehendeRegeln.Add(b);
            foreach (var b in _rundenVm.AlleUebergaenge.Where(b => b.QuellRunde == ausgewaehlt))
                AusgehendeRegeln.Add(b);
        }
        OnPropertyChanged(nameof(Kopf));
    }

    // ── Aufklappbarer Bereich (Issue #476) ────────────────────────────────────
    [ObservableProperty] private bool _offen;

    public AbschnittKopf Kopf => AbschnittKopf.Aus("ÜBERGÄNGE",
    [
        KopfTeil("von", EingehendeRegeln.Select(b => b.QuellRunde?.Name)),
        KopfTeil("nach", AusgehendeRegeln.Select(b => b.ZielRunde?.Name))
    ], "keine");

    // Bis zu drei Runden stehen mit Namen in der Kopfzeile, darüber nur ihre Anzahl.
    private static string? KopfTeil(string richtung, IEnumerable<string?> rundenNamen)
    {
        var namen = rundenNamen.Select(n => string.IsNullOrWhiteSpace(n) ? "?" : n).ToList();
        return namen.Count switch
        {
            0    => null,
            <= 3 => $"{richtung} {string.Join(", ", namen)}",
            _    => $"{richtung} {namen.Count} Runden"
        };
    }

    // ── Regelformular ─────────────────────────────────────────────────────────
    // Populier-/Zurückschreib-Muster wie das Rundenformular. Quelle/Ziel-ComboBoxen binden auf die komplette
    // Rundenliste (Runden-Property oben) — bewusst KEINE gefilterte Optionsliste.
    private static readonly HashSet<string> RegelFormFelder = [nameof(RegelQuelle), nameof(RegelZiel)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelIstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(RegelIstKoSonderfall))]
    [NotifyPropertyChangedFor(nameof(RegelIstLiga))]
    [NotifyCanExecuteChangedFor(nameof(NeueGruppenuebergreifendeZeileCommand))]
    private RundeZeile? _regelQuelle;

    [ObservableProperty] private RundeZeile? _regelZiel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NormaleZeilen))]
    [NotifyPropertyChangedFor(nameof(GruppenuebergreifendeZeilen))]
    private ObservableCollection<UebergangsregelZeile> _regelZeilen = [];

    public bool RegelIstGruppenphase => RegelQuelle?.Rundenart == Rundenart.Gruppenphase;
    public bool RegelIstKoSonderfall => RegelQuelle?.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
    public bool RegelIstLiga => RegelQuelle is not null && !RegelIstGruppenphase && !RegelIstKoSonderfall;

    // ── Gruppenübergreifende Rangfolge-Regeln (z.B. "2 beste Gruppendritte") ────
    // Gefilterte Sicht auf RegelZeilen (AnzahlBeste gesetzt = gruppenübergreifend) statt eigener Collection —
    // eine zweite parallele Collection müsste in der geteilten Datenhaltung zusätzlich synchron gehalten werden.
    public IEnumerable<UebergangsregelZeile> NormaleZeilen => RegelZeilen.Where(z => z.AnzahlBeste is null);
    public IEnumerable<UebergangsregelZeile> GruppenuebergreifendeZeilen => RegelZeilen.Where(z => z.AnzahlBeste is not null);

    [RelayCommand(CanExecute = nameof(RegelIstGruppenphase))]
    private void NeueGruppenuebergreifendeZeile()
    {
        AusgewaehlteRegel?.GruppenuebergreifendeZeileHinzufuegen();
        OnPropertyChanged(nameof(NormaleZeilen));
        OnPropertyChanged(nameof(GruppenuebergreifendeZeilen));
    }

    [RelayCommand]
    private void GruppenuebergreifendeZeileEntfernen(UebergangsregelZeile zeile)
    {
        AusgewaehlteRegel?.ZeileEntfernen(zeile);
        OnPropertyChanged(nameof(NormaleZeilen));
        OnPropertyChanged(nameof(GruppenuebergreifendeZeilen));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeRegelformular))]
    private UebergangsregelBlock? _ausgewaehlteRegel;

    public bool ZeigeRegelformular => AusgewaehlteRegel is not null;

    private int _populiertGerade;

    partial void OnAusgewaehlteRegelChanged(UebergangsregelBlock? oldValue, UebergangsregelBlock? newValue)
    {
        if (oldValue is not null) oldValue.IstAusgewaehlt = false;
        if (newValue is not null) newValue.IstAusgewaehlt = true;
        FormularAusFuellenMitRegel(newValue);
        RegelLoeschenCommand.NotifyCanExecuteChanged();
    }

    private void FormularAusFuellenMitRegel(UebergangsregelBlock? regel)
    {
        _populiertGerade++;
        try
        {
            RegelQuelle = regel?.QuellRunde;
            RegelZiel = regel?.ZielRunde;
            RegelZeilen = regel?.Zeilen ?? [];
        }
        finally
        {
            _populiertGerade--;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_populiertGerade > 0 || e.PropertyName is null) return;
        if (!RegelFormFelder.Contains(e.PropertyName)) return;

        // Einmal lokal lesen statt AusgewaehlteRegel mehrfach über den Getter abzufragen: base.OnPropertyChanged
        // kann UI-Bindings synchron benachrichtigen, die ihrerseits AusgewaehlteRegel zurücksetzen (siehe
        // Runden-Case in OnRundenVmPropertyChanged).
        var regel = AusgewaehlteRegel;
        if (regel is null) return;

        // IsDirty/Validierung/Listen-Refresh laufen über den Geaendert-Handler in RundenViewModel.VerdrahteBlock.
        regel.QuellRunde = RegelQuelle;
        regel.ZielRunde = RegelZiel;
    }

    [RelayCommand]
    private void RegelAuswaehlen(UebergangsregelBlock regel) => AusgewaehlteRegel = regel;

    [RelayCommand]
    private void RegelFormularSchliessen() => AusgewaehlteRegel = null;

    [RelayCommand(CanExecute = nameof(KannNeueRegel))]
    private void NeueEingehendeRegel()
    {
        if (_rundenVm.AusgewaehlteRunde is not { } runde) return;
        var block = UebergangsregelBlock.NeuLeer(quelle: null, ziel: runde);
        _rundenVm.UebergangHinzufuegen(block);
        AusgewaehlteRegel = block;
    }

    [RelayCommand(CanExecute = nameof(KannNeueRegel))]
    private void NeueAusgehendeRegel()
    {
        if (_rundenVm.AusgewaehlteRunde is not { } runde) return;
        var block = UebergangsregelBlock.NeuLeer(quelle: runde, ziel: null);
        _rundenVm.UebergangHinzufuegen(block);
        AusgewaehlteRegel = block;
    }
    private bool KannNeueRegel() => _rundenVm.AusgewaehlteRunde is not null;

    [RelayCommand(CanExecute = nameof(KannRegelLoeschen))]
    private void RegelLoeschen()
    {
        if (AusgewaehlteRegel is null) return;
        _rundenVm.UebergangEntfernen(AusgewaehlteRegel);
        AusgewaehlteRegel = null;
    }
    private bool KannRegelLoeschen() => AusgewaehlteRegel is not null;
}
