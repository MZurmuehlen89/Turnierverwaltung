using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Spielplan &amp; Baum"-Tab der Turnierdurchführung (#301, ersetzt den Platzhalter aus #299) —
/// Composite aus <see cref="SpielplanViewModel"/> (links) und <see cref="TurnierBaumViewModel"/>
/// (rechts), portiert aus Wpf/ViewModels/TurnierBereichViewModel.cs. Anders als das WPF-Original hält
/// dieses ViewModel KEINE eigene Instanzliste/-auswahl — die geteilte Instanzauswahl kommt von
/// <see cref="TurnierdurchfuehrungViewModel"/> über <see cref="InstanzSetzenAsync"/> (Precedent:
/// <see cref="AuslosungViewModel.InstanzSetzenAsync"/>, gleiches Muster aus #300).
/// </summary>
public partial class SpielplanUndBaumViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen
{
    private readonly TurnierInstanzService _instanzService;

    public SpielplanViewModel SpielplanVm { get; }
    public TurnierBaumViewModel TurnierBaumVm { get; }

    private TurnierInstanz? _instanz;

    /// <summary>#302: nur durchgereicht — <see cref="TurnierdurchfuehrungViewModel"/> fängt es ab und
    /// wechselt auf den Simulation-Tab (Precedent WPF-Original <c>TurnierBereichViewModel</c>).</summary>
    public event EventHandler<VorausgewaehlteSimulation>? AusfuehrlicheSimulationAngefordert;

    /// <summary>Ein K.-o.-Turnierbaum ergibt nur bei mehreren aufeinanderfolgenden Runden Sinn — eine
    /// Gruppenphase-/Liga-Tabelle ist aber auch bei genau einer Runde sinnvoll (Precedent aus dem
    /// WPF-Original, Nutzer-Feedback 2026-08-21).</summary>
    public bool ZeigeSplitAnsicht =>
        SpielplanVm.Runden.Count > 1 ||
        SpielplanVm.AusgewaehlteRunde?.Rundenart is
            Fussballturnierverwaltung.Domain.Enums.Rundenart.Gruppenphase or
            Fussballturnierverwaltung.Domain.Enums.Rundenart.Liga or
            Fussballturnierverwaltung.Domain.Enums.Rundenart.TopfLiga or
            Fussballturnierverwaltung.Domain.Enums.Rundenart.SchweizerSystem;

    /// <summary>Issue #238: Die Gesamtübersicht des Turnierbaums nimmt die ganze Seite ein — Rundenliste und
    /// Spielplan treten dafür zurück.</summary>
    public bool ZeigtBaumGesamtansicht => ZeigeSplitAnsicht && TurnierBaumVm.IstGesamtansicht;
    public bool ZeigtSpielplan => !ZeigtBaumGesamtansicht;
    public bool ZeigtTrenner => ZeigeSplitAnsicht && !TurnierBaumVm.IstGesamtansicht;

    private void AnsichtGeaendert()
    {
        OnPropertyChanged(nameof(ZeigeSplitAnsicht));
        OnPropertyChanged(nameof(ZeigtBaumGesamtansicht));
        OnPropertyChanged(nameof(ZeigtSpielplan));
        OnPropertyChanged(nameof(ZeigtTrenner));
    }

    public string? FortschrittAnzeige { get; private set; }

    public SpielplanUndBaumViewModel(
        TurnierInstanzService instanzService,
        SpielplanViewModel spielplanVm,
        TurnierBaumViewModel turnierBaumVm)
    {
        _instanzService = instanzService;
        SpielplanVm = spielplanVm;
        TurnierBaumVm = turnierBaumVm;

        TurnierBaumVm.RundeAngeklickt += (_, rundeId) =>
        {
            var runde = SpielplanVm.Runden.FirstOrDefault(r => r.Id == rundeId);
            if (runde is not null)
                SpielplanVm.AusgewaehlteRunde = runde;
        };

        TurnierBaumVm.GruppeAngeklickt += (_, gruppenNummer) =>
        {
            var gruppe = SpielplanVm.GruppenTabellen.FirstOrDefault(g => g.GruppenNummer == gruppenNummer);
            if (gruppe is not null)
                SpielplanVm.AusgewaehlteGruppe = gruppe;
        };

        TurnierBaumVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TurnierBaumViewModel.IstGesamtansicht))
                AnsichtGeaendert();
        };

        SpielplanVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpielplanViewModel.AusgewaehlteGruppe) ||
                e.PropertyName == nameof(SpielplanViewModel.AusgewaehlteSpiel))
            {
                AktualisiereGruppenFokus();
            }

            if (e.PropertyName == nameof(SpielplanViewModel.RibbonZusatzGruppen))
                OnPropertyChanged(nameof(RibbonZusatzGruppen));

            if (e.PropertyName == nameof(SpielplanViewModel.AusgewaehlteRunde))
                AnsichtGeaendert();
        };

        SpielplanVm.AusfuehrlicheSimulationAngefordert += (sender, vorauswahl) =>
            AusfuehrlicheSimulationAngefordert?.Invoke(sender, vorauswahl);

        SpielplanVm.SpieltagNavigiert = TurnierBaumVm.Tabellen.ZeigeAktuellenStand;
        SpielplanVm.SpielstandGeaendertAsync = FortschrittAktualisierenAsync;
        SpielplanVm.RundeGeaendertAsync = TurnierBaumVm.AktualisiereFokusRundeAsync;
    }

    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        _instanz = instanz;
        await SpielplanVm.InstanzSetzenAsync(instanz);
        AnsichtGeaendert();
        await TurnierBaumVm.InstanzSetzenAsync(instanz);
        await FortschrittAktualisierenAsync();
    }

    private void AktualisiereGruppenFokus()
    {
        int? ziel = SpielplanVm.AusgewaehlteGruppe?.GruppenNummer ?? SpielplanVm.AusgewaehlteSpiel?.GruppeNummer;
        TurnierBaumVm.Tabellen.FokussiereGruppe(ziel);
    }

    private async Task FortschrittAktualisierenAsync()
    {
        if (_instanz is not { } instanz)
        {
            FortschrittAnzeige = null;
            OnPropertyChanged(nameof(FortschrittAnzeige));
            return;
        }

        var fortschritt = await _instanzService.FortschrittAbrufenAsync(instanz.Id);
        FortschrittAnzeige = fortschritt.RundenGesamt == 0
            ? null
            : $"Runde {Math.Min(fortschritt.RundenAbgeschlossen + 1, fortschritt.RundenGesamt)} von {fortschritt.RundenGesamt} · " +
              $"{fortschritt.SpieleGespielt} / {fortschritt.SpieleGesamt} Spiele gespielt";
        OnPropertyChanged(nameof(FortschrittAnzeige));
    }

    // ── IRibbonAware — immer an SpielplanVm delegiert ────────────────────────
    public string RibbonTabTitel => SpielplanVm.RibbonTabTitel;
    public ICommand? NeuBefehl => SpielplanVm.NeuBefehl;
    public ICommand? SpeichernBefehl => SpielplanVm.SpeichernBefehl;
    public ICommand? AbbrechenBefehl => SpielplanVm.AbbrechenBefehl;
    public ICommand? LoeschenBefehl => SpielplanVm.LoeschenBefehl;

    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
        [.. SpielplanVm.RibbonZusatzGruppen, .. TurnierBaumVm.RibbonZusatzGruppen];

    public bool HatUngespeicherteAenderungen => SpielplanVm.HatUngespeicherteAenderungen;
}
