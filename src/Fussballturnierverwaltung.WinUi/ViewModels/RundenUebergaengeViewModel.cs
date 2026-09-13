using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Rein lesende Anzeige-Zeile für die Rundenliste — Name/Rundenart/Eingang→Ausgang + optionaler
/// Unstimmigkeits-Hinweis, wenn die ausgehenden Übergangsregeln dieser Runde nicht zu ihrer
/// Ausgangsmannschaftszahl passen.</summary>
public partial class RundenKarte : ObservableObject
{
    public required RundeZeile Runde { get; init; }
    [ObservableProperty] private string _rundenartText = "";
    [ObservableProperty] private string _einAusgangText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUnstimmigkeit))]
    private string? _unstimmigkeitText;
    public bool HatUnstimmigkeit => UnstimmigkeitText is not null;
}

/// <summary>"Rundenübergänge"-Tab von <see cref="TurnierlisteViewModel"/> (Issue #297) — Rundenliste (rein
/// lesend, Fokus per Klick) links, daneben Eingehende/Ausgehende Übergangsregeln der fokussierten Runde (rein
/// lesende Zusammenfassung, Klick wählt eine Regel aus) + ein Regelformular. Liest und schreibt die geteilte
/// Runden-/Übergangsregel-Datenhaltung in <see cref="RundenViewModel"/> (per DI referenziert, beide Scoped in
/// derselben Navigations-Scope) — kein eigener Datenbestand, kein eigenes Speichern (delegiert 1:1 an den
/// Runden-Tab).</summary>
public partial class RundenUebergaengeViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly RundenViewModel _rundenVm;

    public RundenUebergaengeViewModel(RundenViewModel rundenVm)
    {
        _rundenVm = rundenVm;

        _rundenVm.PropertyChanged += OnRundenVmPropertyChanged;
        _rundenVm.UebergaengeGeaendert += () =>
        {
            AktualisiereRundenliste();
            AktualisiereRegelListen();
        };

        AktualisiereRundenliste();
        AktualisiereRegelListen();
    }

    private void OnRundenVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RundenViewModel.AusgewaehlteRunde):
                // AktualisiereRundenliste() bewusst NICHT hier aufrufen: Karten wird per SelectedValue an eine
                // native ListView gebunden — eine Neuzuweisung der Collection bei jedem Fokuswechsel würde die
                // ListView-Auswahl transient zurücksetzen und über die TwoWay-Bindung wieder auf null schreiben.
                AusgewaehlteRegel = null; // schließt ein offenes Regelformular bei Rundenwechsel
                OnPropertyChanged(nameof(AusgewaehlteRunde));
                OnPropertyChanged(nameof(FormularSichtbar));
                NeueEingehendeRegelCommand.NotifyCanExecuteChanged();
                NeueAusgehendeRegelCommand.NotifyCanExecuteChanged();
                AktualisiereRegelListen();
                break;
            case nameof(RundenViewModel.HatInstanz):
                OnPropertyChanged(nameof(HatInstanz));
                break;
            case nameof(RundenViewModel.RundenBearbeitbar):
                OnPropertyChanged(nameof(RundenBearbeitbar));
                break;
            case nameof(RundenViewModel.IstLadend):
                OnPropertyChanged(nameof(IstLadend));
                break;
            case nameof(RundenViewModel.Fehlermeldung):
                OnPropertyChanged(nameof(Fehlermeldung));
                OnPropertyChanged(nameof(ZeigeMeldungsSpalte));
                break;
            case nameof(RundenViewModel.RundenFehler):
                OnPropertyChanged(nameof(RundenFehler));
                OnPropertyChanged(nameof(HatRundenFehler));
                OnPropertyChanged(nameof(ZeigeMeldungsSpalte));
                break;
            case nameof(RundenViewModel.RibbonTabTitel) or nameof(RundenViewModel.NeuBefehl)
                or nameof(RundenViewModel.SpeichernBefehl) or nameof(RundenViewModel.AbbrechenBefehl)
                or nameof(RundenViewModel.LoeschenBefehl) or nameof(RundenViewModel.HatUngespeicherteAenderungen):
                OnPropertyChanged(nameof(HatUngespeicherteAenderungen));
                break;
        }
    }

    // ── Geteilter Zustand (Pass-Through auf RundenViewModel) ────────────────────
    public bool HatInstanz => _rundenVm.HatInstanz;
    public bool RundenBearbeitbar => _rundenVm.RundenBearbeitbar;
    public bool IstLadend => _rundenVm.IstLadend;
    public string? Fehlermeldung => _rundenVm.Fehlermeldung;
    public IReadOnlyList<string> RundenFehler => _rundenVm.RundenFehler;
    public bool HatRundenFehler => _rundenVm.HatRundenFehler;
    public bool ZeigeMeldungsSpalte => _rundenVm.ZeigeMeldungsSpalte;
    public ObservableCollection<RundeZeile> Runden => _rundenVm.Runden;

    /// <summary>Echte Get/Set-Passthrough-Property (nicht nur lesend) — ermöglicht eine native
    /// ListView.SelectedValue-TwoWay-Bindung in der Rundenliste, konsistent mit dem "Runden"-Tab.</summary>
    public RundeZeile? AusgewaehlteRunde
    {
        get => _rundenVm.AusgewaehlteRunde;
        set => _rundenVm.AusgewaehlteRunde = value;
    }

    public bool FormularSichtbar => AusgewaehlteRunde is not null;

    // ── Eingehende/Ausgehende Übergangsregeln der fokussierten Runde (rein lesend) ──
    public ObservableCollection<UebergangsregelBlock> EingehendeRegeln { get; } = [];
    public ObservableCollection<UebergangsregelBlock> AusgehendeRegeln { get; } = [];

    private void AktualisiereRegelListen()
    {
        EingehendeRegeln.Clear();
        AusgehendeRegeln.Clear();

        var ausgewaehlt = AusgewaehlteRunde;
        if (ausgewaehlt is null) return;

        foreach (var b in _rundenVm.AlleUebergaenge.Where(b => b.ZielRunde == ausgewaehlt))
            EingehendeRegeln.Add(b);
        foreach (var b in _rundenVm.AlleUebergaenge.Where(b => b.QuellRunde == ausgewaehlt))
            AusgehendeRegeln.Add(b);
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
    // Bewusst KEINE eigene Collection wie im WPF-Original, sondern eine gefilterte Sicht auf RegelZeilen
    // (AnzahlBeste gesetzt = gruppenübergreifend) — die WinUi-Architektur hält Runden UND Übergangsregeln in
    // EINER geteilten Datenhaltung (RundenViewModel), eine zweite parallele Collection müsste dort zusätzlich
    // mitgeführt und synchron gehalten werden, ohne fachlichen Mehrwert.
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
        if (_populiertGerade > 0 || e.PropertyName is null || AusgewaehlteRegel is null) return;
        if (!RegelFormFelder.Contains(e.PropertyName)) return;

        // IsDirty/Validierung/Listen-Refresh laufen über den Geaendert-Handler in RundenViewModel
        // (VerdrahteBlock), der durch die folgenden Zuweisungen ausgelöst wird.
        AusgewaehlteRegel.QuellRunde = RegelQuelle;
        AusgewaehlteRegel.ZielRunde = RegelZiel;
    }

    [RelayCommand]
    private void RegelAuswaehlen(UebergangsregelBlock regel) => AusgewaehlteRegel = regel;

    [RelayCommand]
    private void RegelFormularSchliessen() => AusgewaehlteRegel = null;

    [RelayCommand(CanExecute = nameof(KannNeueRegel))]
    private void NeueEingehendeRegel()
    {
        if (AusgewaehlteRunde is null) return;
        var block = UebergangsregelBlock.NeuLeer(quelle: null, ziel: AusgewaehlteRunde);
        _rundenVm.UebergangHinzufuegen(block);
        AusgewaehlteRegel = block;
    }

    [RelayCommand(CanExecute = nameof(KannNeueRegel))]
    private void NeueAusgehendeRegel()
    {
        if (AusgewaehlteRunde is null) return;
        var block = UebergangsregelBlock.NeuLeer(quelle: AusgewaehlteRunde, ziel: null);
        _rundenVm.UebergangHinzufuegen(block);
        AusgewaehlteRegel = block;
    }
    private bool KannNeueRegel() => AusgewaehlteRunde is not null;

    [RelayCommand(CanExecute = nameof(KannRegelLoeschen))]
    private void RegelLoeschen()
    {
        if (AusgewaehlteRegel is null) return;
        _rundenVm.UebergangEntfernen(AusgewaehlteRegel);
        AusgewaehlteRegel = null;
    }
    private bool KannRegelLoeschen() => AusgewaehlteRegel is not null;

    // ── Rundenliste (rein lesend) ─────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<RundenKarte> _karten = [];

    /// <summary>Aktualisiert die Kartenliste. Feuert bei JEDER Rundenformular-Änderung (nicht nur strukturellen
    /// wie Hinzufügen/Entfernen einer Runde), da Name/Eingang/Ausgang die Kartenanzeige betreffen — <see cref="Karten"/>
    /// darf daher im unstrukturierten Fall NICHT neu instanziiert werden: die ListView bindet ihre Auswahl per
    /// SelectedValue TwoWay auf die geteilte <see cref="AusgewaehlteRunde"/>, und ein ItemsSource-Austausch
    /// setzt diese Auswahl transient zurück. Nur bei tatsächlicher Struktur-Änderung (Runde hinzugefügt/
    /// entfernt/umsortiert) wird die Collection ersetzt — dabei wird die aktuelle Auswahl vorab gesichert und
    /// danach wiederhergestellt.</summary>
    private void AktualisiereRundenliste()
    {
        if (Karten.Count == Runden.Count && Karten.Select(k => k.Runde).SequenceEqual(Runden))
        {
            foreach (var karte in Karten)
            {
                karte.RundenartText = RundenartAnzeigetext(karte.Runde.Rundenart, karte.Runde.GruppenAnzahl);
                karte.EinAusgangText = $"{karte.Runde.EingangsMannschaftsanzahl} → {karte.Runde.AusgangsMannschaftsanzahl}";
                karte.UnstimmigkeitText = UnstimmigkeitErmitteln(karte.Runde);
            }
            return;
        }

        var vorherAusgewaehlt = AusgewaehlteRunde;
        Karten = new ObservableCollection<RundenKarte>(Runden.Select(r => new RundenKarte
        {
            Runde = r,
            RundenartText = RundenartAnzeigetext(r.Rundenart, r.GruppenAnzahl),
            EinAusgangText = $"{r.EingangsMannschaftsanzahl} → {r.AusgangsMannschaftsanzahl}",
            UnstimmigkeitText = UnstimmigkeitErmitteln(r)
        }));
        if (vorherAusgewaehlt is not null && Runden.Contains(vorherAusgewaehlt))
            AusgewaehlteRunde = vorherAusgewaehlt;
    }

    private static string RundenartAnzeigetext(Rundenart art, int? gruppenAnzahl) => art switch
    {
        Rundenart.Gruppenphase => gruppenAnzahl is > 0 ? $"Gruppenphase ({gruppenAnzahl} Gruppen)" : "Gruppenphase",
        Rundenart.Liga => "Liga",
        Rundenart.TopfLiga => "Topf-Liga (Swiss-Modell)",
        Rundenart.SchweizerSystem => "Schweizer System",
        Rundenart.KoRunde => "K.o.-Runde",
        Rundenart.Finale => "Finale",
        Rundenart.SpielUmPlatzDrei => "Spiel um Platz 3",
        Rundenart.Sonderrunde => "Sonderrunde",
        _ => art.ToString()
    };

    /// <summary>Prüft, ob die Summe ALLER ausgehenden Übergangsregeln einer Runde (über ggf. mehrere
    /// unabhängige Regeln zu verschiedenen Zielrunden hinweg) tatsächlich deren Ausgangsanzahl ergibt (Issue
    /// #187). Nur relevant, wenn überhaupt ausgehende Regeln existieren.</summary>
    private string? UnstimmigkeitErmitteln(RundeZeile runde)
    {
        var ausgehende = _rundenVm.AlleUebergaenge.Where(b => b.QuellRunde == runde).ToList();
        if (ausgehende.Count == 0) return null;

        var summe = ausgehende.SelectMany(b => b.Zeilen).Sum(z => z.AnzahlBeste ?? (z.PlatzierungBis - z.PlatzierungVon + 1));
        var erwartet = runde.AusgangsMannschaftsanzahl;

        if (summe == erwartet) return null;
        return summe < erwartet
            ? $"Übergangsregeln decken nur {summe} von {erwartet} Plätzen ab."
            : $"Übergangsregeln decken {summe} von {erwartet} Plätzen ab — mehr als erwartet.";
    }

    // ── IRibbonAware — reine Weiterleitung an den Runden-Tab (EIN gemeinsames Speichern für beide Tabs) ──
    public string RibbonTabTitel => "Übergänge";
    public ICommand? NeuBefehl => _rundenVm.NeuBefehl;
    public ICommand? SpeichernBefehl => _rundenVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => _rundenVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl => _rundenVm.LoeschenBefehl;
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen => _rundenVm.RibbonZusatzGruppen;
    public bool HatStandardAktionen => true;
    public bool HatLoeschenAktion => true;

    public bool HatUngespeicherteAenderungen => _rundenVm.HatUngespeicherteAenderungen;
}
