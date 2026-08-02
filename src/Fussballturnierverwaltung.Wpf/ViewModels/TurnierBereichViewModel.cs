using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Wpf.Navigation;
using Fussballturnierverwaltung.Wpf.Ribbon;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

public partial class TurnierBereichViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    public SpielplanViewModel   SpielplanVm   { get; }
    public TurnierBaumViewModel TurnierBaumVm { get; }

    [ObservableProperty] private ObservableCollection<TurnierInstanz> _instanzen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatInstanz))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool HatInstanz => AusgewaehlteInstanz is not null;

    public event EventHandler<VorausgewaehlteSimulation>? AusfuehrlicheSimulationAngefordert;

    // Ein Turnierbaum ergibt nur bei mehreren aufeinanderfolgenden Runden Sinn — bei genau einer
    // Runde (oder keiner) entfaellt die Split-Ansicht komplett zugunsten des vollen Spielplans.
    public bool ZeigeSplitAnsicht => SpielplanVm.Runden.Count > 1;

    [ObservableProperty] private string? _fortschrittAnzeige;

    public TurnierBereichViewModel(
        TurnierInstanzService instanzService,
        SpielplanViewModel   spielplanVm,
        TurnierBaumViewModel turnierBaumVm,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _instanzService = instanzService;
        SpielplanVm     = spielplanVm;
        TurnierBaumVm   = turnierBaumVm;
        _turnierAuswahlService = turnierAuswahlService;

        TurnierBaumVm.RundeAngeklickt += (_, rundeId) =>
        {
            var runde = SpielplanVm.Runden.FirstOrDefault(r => r.Id == rundeId);
            if (runde is not null)
                SpielplanVm.AusgewaehlteRunde = runde;
        };

        // TurnierBereichViewModel kennt selbst keine Tabs (Split-Ansicht, kein Tab-Umschalter) —
        // das Event wird daher nur durchgereicht, TurnierEditorViewModel fängt es ab und
        // wechselt dort auf den Simulation-Tab (siehe Nachbesserung zu #163).
        SpielplanVm.AusfuehrlicheSimulationAngefordert += (sender, vorauswahl) =>
            AusfuehrlicheSimulationAngefordert?.Invoke(sender, vorauswahl);

        // Issue #138/#141, Richtung "Canvas → Spielplan": Klick auf eine Gruppen-Karte im rechten
        // Panel filtert die Spielliste links direkt auf diese Gruppe (es gibt seit #141 nur noch eine
        // Listenansicht, kein separater Gruppenansicht-Umschalter mehr). Löst über den
        // AusgewaehlteGruppe-PropertyChanged-Handler unten automatisch auch den rechten
        // Eigenfokus aus (Bug 1 aus dem manuellen Testdurchlauf).
        TurnierBaumVm.GruppeAngeklickt += (_, gruppenNummer) =>
        {
            var gruppe = SpielplanVm.GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppenNummer);
            if (gruppe is not null)
                SpielplanVm.AusgewaehlteGruppe = gruppe;
        };

        // Issue #138, überarbeitetes Kopplungsmodell (nach manuellem Testdurchlauf, Bug 1/3/4):
        // der rechte Gruppen-Fokus folgt primär SpielplanVm.AusgewaehlteGruppe (bewusster,
        // klebriger Filter) — nur wenn dort kein Filter aktiv ist, folgt er sekundär-automatisch
        // der volatilen AusgewaehlteSpiel-Selektion. Verhindert, dass ein reiner Listen-Rebuild
        // (z.B. Spieltag-Wechsel, der AusgewaehlteSpiel auf null zurückfallen lässt, siehe Bug 4)
        // einen aktiven Gruppenfilter rechts zerstört. Rein synchron/clientseitig (keine DB-Arbeit
        // in FokussiereGruppe), daher als direkter Property-Changed-Handler statt sequenzielles
        // Delegate sicher (siehe [[feedback-dbcontext-concurrency]]).
        SpielplanVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpielplanViewModel.AusgewaehlteGruppe) ||
                e.PropertyName == nameof(SpielplanViewModel.AusgewaehlteSpiel))
            {
                AktualisiereGruppenFokus();
            }

            if (e.PropertyName == nameof(SpielplanViewModel.RibbonZusatzGruppen))
                OnPropertyChanged(nameof(RibbonZusatzGruppen));
        };

        // Issue #141: Spieltag-Navigation in der Spielliste (links) setzt den Tabellen-Snapshot
        // rechts zurück auf "Aktuell" — verhindert, dass eine veraltete historische Tabelle
        // unbemerkt stehen bleibt, während in der Liste weitergeblättert wird. Synchron, kein
        // DB-Zugriff auf der Empfängerseite.
        SpielplanVm.SpieltagNavigiert = TurnierBaumVm.ZeigeAktuellenStand;

        SpielplanVm.SpielstandGeaendertAsync = FortschrittAktualisierenAsync;
        SpielplanVm.RundeGeaendertAsync      = TurnierBaumVm.AktualisiereFokusRundeAsync;
    }

    public override async Task AktivierenAsync()
    {
        var liste = await _instanzService.AlleAbrufenAsync();
        Instanzen.Clear();
        foreach (var i in liste) Instanzen.Add(i);

        if (AusgewaehlteInstanz is not null && !Instanzen.Contains(AusgewaehlteInstanz))
        {
            AusgewaehlteInstanz = null;
        }
        else if (AusgewaehlteInstanz is null)
        {
            AusgewaehlteInstanz = Instanzen.FirstOrDefault(
                i => i.Id == _turnierAuswahlService.TurnierInstanzId);
        }
        else
        {
            // Issue-Nachbesserung zu #163: Instanz unverändert, hier also nur eine reine
            // Reaktivierung (z.B. Rückkehr vom Simulation-Tab) — OnPropertyChanged(AusgewaehlteInstanz)
            // feuert in diesem Fall NICHT, SpielplanVm.AktivierenAsync() muss daher explizit
            // aufgerufen werden (der Tab-Wechsel-Mechanismus in TurnierEditorViewModel ruft nur
            // AktivierenAsync() auf diesem Composite auf, nicht direkt auf SpielplanVm). In den
            // beiden Zweigen oben übernimmt das bereits BeiInstanzAenderungAsync (fire-and-forget) —
            // hier zusätzlich awaiten würde sich mit dessen DB-Zugriff überschneiden.
            await SpielplanVm.AktivierenAsync();
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(AusgewaehlteInstanz))
        {
            _turnierAuswahlService.TurnierInstanzId = AusgewaehlteInstanz?.Id;
            _ = BeiInstanzAenderungAsync();
        }
    }

    private async Task BeiInstanzAenderungAsync()
    {
        var instanz = AusgewaehlteInstanz;
        await SpielplanVm.LadeInstanzAsync(instanz);
        OnPropertyChanged(nameof(ZeigeSplitAnsicht));
        await TurnierBaumVm.LadeInstanzAsync(instanz);
        await FortschrittAktualisierenAsync();
    }

    /// <summary>Berechnet den rechten Gruppen-Fokus neu (Issue #138, überarbeitetes
    /// Kopplungsmodell): SpielplanVm.AusgewaehlteGruppe hat Vorrang; ohne aktiven Gruppenfilter
    /// folgt der Fokus automatisch der aktuellen SpielplanVm.AusgewaehlteSpiel-Selektion.</summary>
    private void AktualisiereGruppenFokus()
    {
        int? ziel = SpielplanVm.AusgewaehlteGruppe?.GruppenNummer ?? SpielplanVm.AusgewaehlteSpiel?.GruppeNummer;
        TurnierBaumVm.FokussiereGruppe(ziel);
    }

    private async Task FortschrittAktualisierenAsync()
    {
        if (AusgewaehlteInstanz is not { } instanz)
        {
            FortschrittAnzeige = null;
            return;
        }

        var fortschritt = await _instanzService.FortschrittAbrufenAsync(instanz.Id);
        FortschrittAnzeige = fortschritt.RundenGesamt == 0
            ? null
            : $"Runde {Math.Min(fortschritt.RundenAbgeschlossen + 1, fortschritt.RundenGesamt)} von {fortschritt.RundenGesamt} · " +
              $"{fortschritt.SpieleGespielt} / {fortschritt.SpieleGesamt} Spiele gespielt";
    }

    // ── IRibbonAware — immer an SpielplanVm delegiert ────────────────────────
    public string   RibbonTabTitel  => SpielplanVm.RibbonTabTitel;
    public ICommand? NeuBefehl      => SpielplanVm.NeuBefehl;
    public ICommand? SpeichernBefehl => SpielplanVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => SpielplanVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl  => SpielplanVm.LoeschenBefehl;

    // Issue #163: kombiniert die Ribbon-Zusatzgruppen aus Spielplan (Runde/Gruppe-Aktionen) und
    // Turnierbaum (Baum-Aktionen) — beide Kind-VMs sind gleichzeitig sichtbar (Split-Ansicht,
    // kein Tab-Umschalter), daher hier zusammengeführt statt wie bei den 4 CRUD-Slots exklusiv
    // an ein Kind delegiert.
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
        [.. SpielplanVm.RibbonZusatzGruppen, .. TurnierBaumVm.RibbonZusatzGruppen];

    public bool HatUngespeicherteAenderungen => SpielplanVm.HatUngespeicherteAenderungen;
}
