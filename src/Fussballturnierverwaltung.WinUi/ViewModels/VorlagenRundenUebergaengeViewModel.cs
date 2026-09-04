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
/// Ausgangsmannschaftszahl passen. War ursprünglich eine grafische, positionierte "Perlenkette" mit
/// Verbindungslinien (Graphtiefe-basiertes Canvas-Layout, analog WPF-Original <c>RundenUebergangKarte</c>) —
/// nach Nutzer-Rückmeldung auf eine einfache Liste reduziert: der Informationsgehalt war für eine grafische
/// Darstellung zu gering, während das Canvas-Layout in WinUI3 mehrfach eigene Stolperfallen aufgeworfen hat
/// (zwei gescheiterte deklarative Positionierungsversuche, siehe Git-Historie dieser Datei).</summary>
public partial class VorlagenRundenKarte : ObservableObject
{
    public required VorlagenRundeZeile Runde { get; init; }
    [ObservableProperty] private string _rundenartText = "";
    [ObservableProperty] private string _einAusgangText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatUnstimmigkeit))]
    private string? _unstimmigkeitText;
    public bool HatUnstimmigkeit => UnstimmigkeitText is not null;
}

/// <summary>"Rundenübergänge"-Tab von <see cref="TurniervorlagenViewModel"/> (#284) — Rundenliste (rein
/// lesend, Fokus per Klick) links, daneben Eingehende/Ausgehende Übergangsregeln der fokussierten Runde (rein
/// lesende Zusammenfassung, Klick wählt eine Regel aus) + ein Regelformular. Liest und schreibt die geteilte
/// Runden-/Übergangsregel-Datenhaltung in <see cref="VorlagenRundenViewModel"/> (per DI referenziert, beide
/// Scoped in derselben Navigations-Scope) — kein eigener Datenbestand, kein eigenes Speichern (delegiert 1:1
/// an den Runden-Tab). Redesign-Historie: die ursprüngliche Fassung bettete bearbeitbare ComboBoxen in die bei
/// jedem Rundenfokus-Wechsel neu aufgebauten Eingehend-/Ausgehend-Karten ein — das hat wiederholt zu
/// Datenkorruption geführt. Jetzt gilt strikt: bearbeitbare TwoWay-Controls leben nur noch im stabilen
/// Regelformular, alles andere ist reine Anzeige mit Klick-Commands. Die Regelformular-ComboBoxen (Quelle/
/// Ziel) binden immer auf die komplette, unveränderliche Rundenliste — keine pro Auswahl neu berechnete
/// Optionsliste mehr (das hatte in einer Zwischenfassung erneut zu leerem Formular bei Regelauswahl geführt,
/// da eine Neuberechnung der Optionsliste denselben ItemsSource-Tausch-Fallstrick auslöst wie die
/// ComboBox-Probleme davor).</summary>
public partial class VorlagenRundenUebergaengeViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly VorlagenRundenViewModel _rundenVm;

    public VorlagenRundenUebergaengeViewModel(VorlagenRundenViewModel rundenVm)
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
            case nameof(VorlagenRundenViewModel.AusgewaehlteRunde):
                // AktualisiereRundenliste() bewusst NICHT hier aufrufen: Karten wird jetzt per SelectedValue
                // an eine native ListView gebunden (siehe AusgewaehlteRunde-Property unten) — eine
                // Neuzuweisung der Collection bei jedem Fokuswechsel würde die ListView-Auswahl transient
                // zurücksetzen und über die TwoWay-Bindung wieder auf null schreiben (derselbe Fallstrick,
                // der bei der Perlenkette und den Regel-Quelle/Ziel-ComboBoxen bereits aufgetreten ist).
                // AktualisiereRundenliste() läuft nur noch bei strukturellen Änderungen (UebergaengeGeaendert).
                AusgewaehlteRegel = null; // schließt ein offenes Regelformular bei Rundenwechsel
                OnPropertyChanged(nameof(AusgewaehlteRunde));
                OnPropertyChanged(nameof(FormularSichtbar));
                NeueEingehendeRegelCommand.NotifyCanExecuteChanged();
                NeueAusgehendeRegelCommand.NotifyCanExecuteChanged();
                AktualisiereRegelListen();
                break;
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
        }
    }

    // ── Geteilter Zustand (Pass-Through auf VorlagenRundenViewModel) ────────────
    public bool HatVorlage => _rundenVm.HatVorlage;
    public bool IstLadend => _rundenVm.IstLadend;
    public string? Fehlermeldung => _rundenVm.Fehlermeldung;
    public IReadOnlyList<string> RundenFehler => _rundenVm.RundenFehler;
    public bool HatRundenFehler => _rundenVm.HatRundenFehler;
    public bool ZeigeMeldungsSpalte => _rundenVm.ZeigeMeldungsSpalte;
    public ObservableCollection<VorlagenRundeZeile> Runden => _rundenVm.Runden;

    /// <summary>Echte Get/Set-Passthrough-Property (nicht nur lesend) — ermöglicht eine native
    /// ListView.SelectedValue-TwoWay-Bindung in der Rundenliste, konsistent mit dem "Runden"-Tab (dort
    /// SelectedItem direkt auf die dortige, eigene AusgewaehlteRunde-Property). Schreibt bei Auswahl direkt
    /// in <see cref="VorlagenRundenViewModel.AusgewaehlteRunde"/> zurück, PropertyChanged kommt über
    /// <see cref="OnRundenVmPropertyChanged"/> zurück (kein eigenes Feld nötig, kein Duplikat-Zustand).</summary>
    public VorlagenRundeZeile? AusgewaehlteRunde
    {
        get => _rundenVm.AusgewaehlteRunde;
        set => _rundenVm.AusgewaehlteRunde = value;
    }

    public bool FormularSichtbar => AusgewaehlteRunde is not null;

    // ── Eingehende/Ausgehende Übergangsregeln der fokussierten Runde (rein lesend) ──
    public ObservableCollection<VorlagenUebergangsregelBlock> EingehendeRegeln { get; } = [];
    public ObservableCollection<VorlagenUebergangsregelBlock> AusgehendeRegeln { get; } = [];

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
    // Rundenliste (Runden-Property oben) — bewusst KEINE gefilterte Optionsliste (siehe Klassendokumentation).
    private static readonly HashSet<string> RegelFormFelder = [nameof(RegelQuelle), nameof(RegelZiel)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegelIstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(RegelIstKoSonderfall))]
    [NotifyPropertyChangedFor(nameof(RegelIstLiga))]
    private VorlagenRundeZeile? _regelQuelle;

    [ObservableProperty] private VorlagenRundeZeile? _regelZiel;
    [ObservableProperty] private ObservableCollection<VorlagenUebergangsregelZeile> _regelZeilen = [];

    public bool RegelIstGruppenphase => RegelQuelle?.Rundenart == Rundenart.Gruppenphase;
    public bool RegelIstKoSonderfall => RegelQuelle?.Rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;
    public bool RegelIstLiga => RegelQuelle is not null && !RegelIstGruppenphase && !RegelIstKoSonderfall;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigeRegelformular))]
    private VorlagenUebergangsregelBlock? _ausgewaehlteRegel;

    public bool ZeigeRegelformular => AusgewaehlteRegel is not null;

    private int _populiertGerade;

    partial void OnAusgewaehlteRegelChanged(VorlagenUebergangsregelBlock? oldValue, VorlagenUebergangsregelBlock? newValue)
    {
        if (oldValue is not null) oldValue.IstAusgewaehlt = false;
        if (newValue is not null) newValue.IstAusgewaehlt = true;
        FormularAusFuellenMitRegel(newValue);
        RegelLoeschenCommand.NotifyCanExecuteChanged();
    }

    private void FormularAusFuellenMitRegel(VorlagenUebergangsregelBlock? regel)
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

        // IsDirty/Validierung/Listen-Refresh laufen über den Geaendert-Handler in VorlagenRundenViewModel
        // (VerdrahteBlock), der durch die folgenden Zuweisungen ausgelöst wird
        // (OnQuellRundeChanged/OnZielRundeChanged auf dem Block).
        AusgewaehlteRegel.QuellRunde = RegelQuelle;
        AusgewaehlteRegel.ZielRunde = RegelZiel;
    }

    [RelayCommand]
    private void RegelAuswaehlen(VorlagenUebergangsregelBlock regel) => AusgewaehlteRegel = regel;

    [RelayCommand]
    private void RegelFormularSchliessen() => AusgewaehlteRegel = null;

    [RelayCommand(CanExecute = nameof(KannNeueRegel))]
    private void NeueEingehendeRegel()
    {
        if (AusgewaehlteRunde is null) return;
        var block = VorlagenUebergangsregelBlock.NeuLeer(quelle: null, ziel: AusgewaehlteRunde);
        _rundenVm.UebergangHinzufuegen(block);
        AusgewaehlteRegel = block;
    }

    [RelayCommand(CanExecute = nameof(KannNeueRegel))]
    private void NeueAusgehendeRegel()
    {
        if (AusgewaehlteRunde is null) return;
        var block = VorlagenUebergangsregelBlock.NeuLeer(quelle: AusgewaehlteRunde, ziel: null);
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
    [ObservableProperty] private ObservableCollection<VorlagenRundenKarte> _karten = [];

    /// <summary>Aktualisiert die Kartenliste. Feuert bei JEDER Rundenformular-Änderung (nicht nur strukturellen
    /// wie Hinzufügen/Entfernen einer Runde), da Name/Eingang/Ausgang die Kartenanzeige betreffen — <see cref="Karten"/>
    /// darf daher im unstrukturierten Fall NICHT neu instanziiert werden: die ListView bindet ihre Auswahl per
    /// SelectedValue TwoWay auf die geteilte <see cref="AusgewaehlteRunde"/>, und ein ItemsSource-Austausch
    /// setzt diese Auswahl transient zurück — schreibt über TwoWay sofort null in die geteilte Property und
    /// löscht damit auch die Auswahl im "Runden"-Tab (derselbe Fallstrick wie in <see cref="OnRundenVmPropertyChanged"/>
    /// dokumentiert, hier nur für Feldänderungen statt Rundenwechsel). Nur bei tatsächlicher Struktur-Änderung
    /// (Runde hinzugefügt/entfernt/umsortiert) wird die Collection ersetzt — dabei wird die aktuelle Auswahl
    /// vorab gesichert und danach wiederhergestellt (analog <see cref="VorlagenPaarungenViewModel.AktualisierePaarungsRunden"/>),
    /// da der Collection-Tausch sie sonst transient auf null zurücksetzt (Löschen-Fall: die Rundenliste
    /// selektiert bewusst die Nachbarrunde VOR diesem Aufruf, siehe <see cref="VorlagenRundenViewModel.RundeEntfernen"/>).</summary>
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
        Karten = new ObservableCollection<VorlagenRundenKarte>(Runden.Select(r => new VorlagenRundenKarte
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
        Rundenart.TopfLiga => "Liga (Topf)",
        Rundenart.SchweizerSystem => "Schweizer System",
        Rundenart.KoRunde => "K.o.-Runde",
        Rundenart.Finale => "Finale",
        Rundenart.SpielUmPlatzDrei => "Spiel um Platz 3",
        Rundenart.Sonderrunde => "Sonderrunde",
        _ => art.ToString()
    };

    /// <summary>Prüft, ob die Summe ALLER ausgehenden Übergangsregeln einer Runde (über ggf. mehrere
    /// unabhängige Regeln zu verschiedenen Zielrunden hinweg) tatsächlich deren Ausgangsanzahl ergibt —
    /// analog WPF-Original <c>RundenUebergaengeViewModel.UnstimmigkeitErmitteln</c> (Issue #187), aber ohne
    /// den dortigen K.o.-Sonderfall (Ausgang wird für K.o.-Runden bereits automatisch auf Eingang/2
    /// gehalten). Nur relevant, wenn überhaupt ausgehende Regeln existieren — eine Runde ganz ohne (z.B. das
    /// Finale) ist kein Fehlerfall.</summary>
    private string? UnstimmigkeitErmitteln(VorlagenRundeZeile runde)
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
