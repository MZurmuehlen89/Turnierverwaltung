using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Diagnose;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Navigation;
using Fussballturnierverwaltung.WinUi.Ribbon;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>
/// "Spielplan"-Bereich des #301-Composite (<see cref="SpielplanUndBaumViewModel"/>) — 1:1 aus
/// Wpf/ViewModels/SpielplanViewModel.cs portiert. Wichtigste WinUi-Anpassungen: <c>MessageBox.Show</c>
/// (3-Wege Speichern/Verwerfen/Abbrechen) → <see cref="IDialogService.UngespeicherteAenderungenAbfragenAsync"/>,
/// einfache Ja/Nein-Dialoge → <see cref="IDialogService.BestaetigenAsync"/>; Ribbon-Simulieren-Aktionen als
/// flache Einzel-Buttons statt SplitButtons/Unteraktionen (mit dem Nutzer abgestimmte Vereinfachung für #301,
/// siehe Plan) — "Runde/Gruppe/Spieltag zurücksetzen" stehen als eigene Buttons neben den Simulieren-Buttons.
/// </summary>
public partial class SpielplanViewModel : ViewModelBase, IRibbonAware, IHatUngespeicherteAenderungen,
    ISpielmengenKontext
{
    private readonly SpielErgebnisService _spielService;
    private readonly SpielplanService _spielplanService;
    private readonly PlatzhalterKaskadeService _kaskade;
    private readonly FinaleAutomatik _finaleAutomatik;
    private readonly NeuGenerierungsRueckfrage _neuGenerierung;
    private readonly TurnierBaumService _baumService;
    private readonly SpielplanRundenKontext _kontext;
    private readonly TurnierInstanzService _instanzService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<SpielplanViewModel> _logger;

    private readonly SpiellistenQuelle _quelle;

    private readonly WechselUnterdrueckung _spielWechsel = new();
    private readonly WechselUnterdrueckung _rundeWechsel = new();

    /// <summary>Reiht alle Datenbankzugriffe dieses Reiters hintereinander: Runde-/Spiel-Wechsel (#338), die
    /// Aktionen (Simulieren, Zurücksetzen, Speichern …) und — über den vom
    /// <see cref="TurnierdurchfuehrungViewModel"/> gesetzten gemeinsamen Schutz — auch Turnier-/Reiterwechsel
    /// (#415). Ein eigener Schutz je Reiter schloss das nicht aus: eine laufende Simulation kollidierte mit dem
    /// Laden nach einem Runden-, Reiter- oder Turnierwechsel und brach dabei selbst mittendrin ab. Nicht
    /// wiedereintrittsfähig — <see cref="InstanzSetzenAsync"/> läuft bereits unter dem Schutz und darf nichts
    /// abwarten, das ihn betritt (die selbstverursachte Reentrancy decken
    /// _rundeWechsel/_spielWechsel ab).</summary>
    public DbContextReentrancySchutz DbContextSchutz { get; set; } = new();

    IReadOnlyList<SpielZeile> ISpielmengenKontext.Spiele => Spiele;
    GruppenTabelle? ISpielmengenKontext.EffektiveGruppe => EffektiveGruppe;
    int? ISpielmengenKontext.EffektiverSpieltag => EffektiverSpieltag;
    bool ISpielmengenKontext.HatHinUndRueckspiel => HatHinUndRueckspiel;
    Task<string> ISpielmengenKontext.KaskadenHinweisAsync(IReadOnlyList<int> spielIds) => KaskadenHinweisAsync(spielIds);

    // ── linke Spalte ──────────────────────────────────────────────────────────

    [ObservableProperty] private TurnierInstanz? _ausgewaehlteInstanz;

    [ObservableProperty] private ObservableCollection<TurnierRunde> _runden = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunde))]
    [NotifyPropertyChangedFor(nameof(IstGruppenphase))]
    [NotifyPropertyChangedFor(nameof(IstKoRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaRunde))]
    [NotifyPropertyChangedFor(nameof(IstLigaMitRueckspiel))]
    [NotifyPropertyChangedFor(nameof(IstSpieltagRunde))]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusListe))]
    [NotifyPropertyChangedFor(nameof(ZeigtSchweizerRundenSteuerung))]
    [NotifyPropertyChangedFor(nameof(IstKoMitRueckspiel))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    [NotifyPropertyChangedFor(nameof(RundenHinweis))]
    [NotifyPropertyChangedFor(nameof(RundenlistenAuswahl))]
    [NotifyPropertyChangedFor(nameof(ZeigtSpielliste))]
    [NotifyPropertyChangedFor(nameof(ZeigtGesamtliste))]
    [NotifyPropertyChangedFor(nameof(KopfzeilenTitel))]
    [NotifyPropertyChangedFor(nameof(KopfzeilenHinweis))]
    private TurnierRunde? _ausgewaehlteRunde;

    /// <summary>Issue #352/#360: Zusatz in der Spiellisten-Kopfzeile und im Ergebnisformular.</summary>
    public string RundenHinweis =>
        (AusgewaehlteRunde?.NeutralerSpielort == true ? " · neutraler Platz" : "")
        + (AusgewaehlteRunde?.Ueberraschungsfaktor is int f and > 0 ? $" · Überraschungsfaktor {f} %" : "");

    public bool HatRunde => AusgewaehlteRunde is not null;
    public bool IstGruppenphase => AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase;
    public bool IstLigaRunde => AusgewaehlteRunde?.Rundenart == Rundenart.Liga;

    private bool IstTabellenRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.Gruppenphase or Rundenart.Liga or Rundenart.SchweizerSystem or Rundenart.TopfLiga;

    /// <summary>Rundenarten mit Spieltagen, deren Spielplan je Spieltag durchgeblättert wird (Issue #363) —
    /// K.-o.-Runden behalten die Paarungsansicht aus #340.</summary>
    public bool IstSpieltagRunde => IstTabellenRunde;

    public bool IstLigaMitRueckspiel => IstLigaRunde && (AusgewaehlteRunde?.MitRueckspiel ?? false);

    /// <summary>Issue #387: Die Menüpunkte Hin-/Rückrunde gelten in K.-o.-Runden für Hin- und Rückspiele.</summary>
    public bool IstKoMitRueckspiel => IstKoRunde && (AusgewaehlteRunde?.MitRueckspiel ?? false);
    private bool HatHinUndRueckspiel => IstLigaMitRueckspiel || IstKoMitRueckspiel;
    private string HinrundeMenuText => IstKoMitRueckspiel ? "Hinspiele" : "Hinrunde";
    private string RueckrundeMenuText => IstKoMitRueckspiel ? "Rückspiele" : "Rückrunde";

    // ── Gruppen-/Spieltag-Fokus ───────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstGefiltertAufGruppe))]
    [NotifyPropertyChangedFor(nameof(ZeigeFokusListe))]
    [NotifyPropertyChangedFor(nameof(ZeigtGesamtliste))]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private GruppenTabelle? _ausgewaehlteGruppe;

    public bool IstGefiltertAufGruppe => AusgewaehlteGruppe is not null;

    public bool ZeigeFokusListe => !IstMannschaftsansicht && (AusgewaehlteGruppe is not null || IstSpieltagRunde);

    // ── Schweizer-Rundensteuerung (Issue #329 Teil E) ────────────────────────────
    // "Nächste Runde generieren" ist bewusst ein eigener Inline-Button in der Spielliste-Kopfzeile statt im
    // Ribbon (laut #301-Fund oben bereits überfüllt) und bewusst getrennt vom Ribbon-"Neu"-Befehl
    // (SpielplanNeuGenerierenAsync), der den kompletten Spielplan löscht+aus der Auslosung neu generiert —
    // für eine laufende Schweizer-Rundenfolge wäre das destruktiv. Rundenabschluss nach dem letzten
    // Spieltag braucht keinen Sonderfall: sobald alle Ergebnisse eingetragen sind, lösen die bestehenden
    // Mechanismen (PlatzhalterAutomatischAufloesenAsync) die Übergangsregeln der Folgerunde ganz normal auf,
    // exakt wie bei Liga-Runden heute schon.

    public bool ZeigtSchweizerRundenSteuerung =>
        !IstMannschaftsansicht && AusgewaehlteRunde?.Rundenart == Rundenart.SchweizerSystem;

    private int SchweizerAktuellerSpieltag =>
        Spiele.Where(s => s.Spieltag.HasValue).Select(s => s.Spieltag!.Value).DefaultIfEmpty(0).Max();

    private int SchweizerZielSpieltage => AusgewaehlteRunde is { } r
        ? RundenKonfiguration.AusJson(r.KonfigurationJson).AnzahlSpieltage ?? RundenKonfiguration.VorschlagAnzahlSpieltage(r.EingangsMannschaftsanzahl)
        : 0;

    private bool SchweizerAktuellerSpieltagKomplett => SchweizerAktuellerSpieltag > 0 &&
        Spiele.Where(s => s.Spieltag == SchweizerAktuellerSpieltag).All(s => s.Status == SpielStatus.Abgeschlossen);

    public string SchweizerFortschrittsText => $"Spieltag {SchweizerAktuellerSpieltag} von {SchweizerZielSpieltage}";
    public string NaechsteSchweizerRundeLabel => $"Spieltag {SchweizerAktuellerSpieltag + 1} generieren";

    private void SchweizerAnzeigeAktualisieren()
    {
        OnPropertyChanged(nameof(SchweizerFortschrittsText));
        OnPropertyChanged(nameof(NaechsteSchweizerRundeLabel));
        _naechsteSchweizerRundeBefehl.NotifyCanExecuteChanged();
    }

    public SpieltagFokusViewModel Fokus { get; } = new();

    public string SpieltagSimulierenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} simulieren" : "Spieltag simulieren";
    public string SpieltagZuruecksetzenMenuText => EffektiverSpieltag is { } st ? $"Spieltag {st} zurücksetzen" : "Spieltag zurücksetzen";

    private readonly RelayCommand _zurueckZurUebersichtBefehl;

    public ICommand ZurueckZurUebersichtBefehl => _zurueckZurUebersichtBefehl;

    // ── Mannschaftsansicht (#479) ─────────────────────────────────────────────
    // Ist eine Mannschaft gewählt, zeigt die Liste ihre Spiele über alle Runden; Rundenauswahl und Spieltag-
    // Blättern treten zurück. AusgewaehlteRunde folgt dann still dem gewählten Spiel — Formular (Regelwerk,
    // Hinspiel), Tabelle und Turnierbaum arbeiten so unverändert mit der Runde des Spiels. Die Rundenliste zeigt
    // in dieser Zeit keine Auswahl (RundenlistenAuswahl): ein Klick auf eine Runde beendet den Filter.

    public MannschaftsfilterViewModel Mannschaftsfilter { get; } = new();

    public bool IstMannschaftsansicht => Mannschaftsfilter.Gewaehlt is not null;

    /// <summary>Die Runde, zu der „Alle Mannschaften“ zurückkehrt, wenn in der Mannschaftsansicht kein Spiel
    /// gewählt ist.</summary>
    private TurnierRunde? _rundeVorMannschaftsansicht;

    public bool ZeigtSpielliste => HatRunde || IstMannschaftsansicht;
    public bool ZeigtGesamtliste => !ZeigeFokusListe && !IstMannschaftsansicht;
    public bool ZeigtKeineMannschaftsSpiele => IstMannschaftsansicht && Spiele.Count == 0;
    public string KopfzeilenTitel => Mannschaftsfilter.Gewaehlt?.Name ?? AusgewaehlteRunde?.Name ?? "";
    public string KopfzeilenHinweis => IstMannschaftsansicht ? "" : RundenHinweis;

    /// <summary>Auswahl der Rundenliste — in der Mannschaftsansicht keine; eine dort angeklickte Runde beendet den
    /// Filter und zeigt die Runde.</summary>
    public TurnierRunde? RundenlistenAuswahl
    {
        get => IstMannschaftsansicht ? null : AusgewaehlteRunde;
        set
        {
            if (!IstMannschaftsansicht) AusgewaehlteRunde = value;
            else if (value is not null) _ = RundeZeigenAsync(value);
        }
    }

    /// <summary>Kontextmenü „Alle Spiele des Teams anzeigen“ (Spielliste, Gruppentabelle, Turnierbaum).</summary>
    public Task MannschaftsSpieleZeigenAsync(int mannschaftId) => Mannschaftsfilter.WaehlenNachIdAsync(mannschaftId);

    // ── Spiel zeigen (Kontextmenü, #62) ───────────────────────────────────────

    /// <summary>Die Ansicht soll das gewählte Spiel in der Liste zeigen und den Cursor ins erste Torfeld setzen.</summary>
    public event EventHandler? ErgebnisEingabeAngefordert;

    /// <summary>Wählt ein Spiel aus und bittet um den Cursor im Ergebnisformular („Ergebnis eingeben“ in der Spielliste,
    /// „Im Spielplan zeigen“ im Turnierbaum). Steht das Spiel nicht in der Liste, wechselt sie erst zu seiner Runde —
    /// aus der Mannschaftsansicht heraus endet dabei der Filter. Ungespeicherte Eingaben klärt die übliche Rückfrage.</summary>
    public async Task SpielZeigenAsync(int rundeId, int spielId)
    {
        if (AusgewaehlteSpiel?.Id != spielId && !await SpielWaehlenAsync(rundeId, spielId)) return;

        // Der Spielwechsel lädt das Formular im selben Schutz — erst danach gibt es ein Torfeld.
        using (await DbContextSchutz.BetretenAsync())
        {
            // nur warten, bis der Spielwechsel den Schutz wieder freigibt
        }
        ErgebnisEingabeAngefordert?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> SpielWaehlenAsync(int rundeId, int spielId)
    {
        if (!Spiele.Any(s => s.Id == spielId))
        {
            if (Runden.FirstOrDefault(r => r.Id == rundeId) is not { } runde) return false;
            await AnsichtWechselnAsync(null, runde);
            if (IstMannschaftsansicht || AusgewaehlteRunde?.Id != rundeId) return false;
        }

        if (!await UngespeichertesKlaerenAsync() || Spiele.FirstOrDefault(s => s.Id == spielId) is not { } zeile)
            return false;

        // Gruppenfilter und Spieltag geben der Fokus-Liste neue Zeilen, die deren Auswahl nullen (#345).
        using (_spielWechsel.Betreten())
        {
            if (AusgewaehlteGruppe is { } gruppe && gruppe.GruppenNummer != zeile.GruppeNummer) AusgewaehlteGruppe = null;
            Fokus.SpieltagZeigen(zeile.Spieltag);
            AusgewaehlteSpiel = zeile;
        }
        await DbContextSchutz.AusfuehrenAsync(BeiSpielAenderungAsync, ex => Fehlermeldung = ex.Message);
        return true;
    }

    /// <summary>Zeigt eine Runde — aus der Mannschaftsansicht heraus beendet das den Filter.</summary>
    public Task RundeZeigenAsync(TurnierRunde runde)
    {
        if (IstMannschaftsansicht) return AnsichtWechselnAsync(null, runde);

        AusgewaehlteRunde = runde;
        return Task.CompletedTask;
    }

    /// <summary>Setzt den Gruppenfilter — aus der Mannschaftsansicht heraus erst zurück in die Runde, die der
    /// Turnierbaum gerade zeigt (die des gewählten Spiels, sonst die vor dem Filter).</summary>
    public async Task GruppeZeigenAsync(int gruppenNummer)
    {
        if (IstMannschaftsansicht && (AusgewaehlteRunde ?? _rundeVorMannschaftsansicht) is { } runde)
            await AnsichtWechselnAsync(null, runde);
        if (IstMannschaftsansicht) return;

        if (_kontext.Gruppe(gruppenNummer) is { } gruppe)
            AusgewaehlteGruppe = gruppe;
    }

    /// <summary>Wechselt zwischen Runden- und Mannschaftsansicht bzw. zu einer anderen Mannschaft — oder, mit
    /// <paramref name="mannschaft"/> <c>null</c> aus der Rundenansicht, zu einer anderen Runde (#62) —, nach der
    /// Ungespeichert-Abfrage. <paramref name="zielRunde"/>: die Runde, die „Alle Mannschaften“ zeigt.</summary>
    private async Task AnsichtWechselnAsync(MannschaftsOption? mannschaft, TurnierRunde? zielRunde)
    {
        if (!await UngespeichertesKlaerenAsync())
        {
            Mannschaftsfilter.Anzeigen(Mannschaftsfilter.Gewaehlt);
            OnPropertyChanged(nameof(RundenlistenAuswahl));
            return;
        }

        if (!IstMannschaftsansicht && mannschaft is not null) _rundeVorMannschaftsansicht = AusgewaehlteRunde;
        Mannschaftsfilter.Anzeigen(mannschaft);
        MannschaftsansichtGeaendert();

        if (mannschaft is null)
        {
            _rundeVorMannschaftsansicht = null;
            RundeStillSetzen(zielRunde);
            OnPropertyChanged(nameof(RundenlistenAuswahl));
            await DbContextSchutz.AusfuehrenAsync(BeiRundeAenderungAsync, ex => Fehlermeldung = ex.Message);
        }
        else
        {
            await DbContextSchutz.AusfuehrenAsync(MannschaftsansichtLadenAsync, ex => Fehlermeldung = ex.Message);
        }
    }

    private void MannschaftsansichtGeaendert()
    {
        OnPropertyChanged(nameof(IstMannschaftsansicht));
        OnPropertyChanged(nameof(ZeigtSpielliste));
        OnPropertyChanged(nameof(ZeigeFokusListe));
        OnPropertyChanged(nameof(ZeigtGesamtliste));
        OnPropertyChanged(nameof(ZeigtKeineMannschaftsSpiele));
        OnPropertyChanged(nameof(ZeigtSchweizerRundenSteuerung));
        OnPropertyChanged(nameof(KopfzeilenTitel));
        OnPropertyChanged(nameof(KopfzeilenHinweis));
        OnPropertyChanged(nameof(RundenlistenAuswahl));
        OnPropertyChanged(nameof(RibbonZusatzGruppen));
        AktualisiereBefehle();
    }

    private async Task MannschaftsansichtLadenAsync()
    {
        using var vorgang = _logger.Vorgang(
            $"Spielplan: Spiele der Mannschaft laden (Mannschaft {Mannschaftsfilter.Gewaehlt?.Id})",
            "Spiele der Mannschaft werden geladen …");

        FormAktiv = false;
        Fehlermeldung = null;
        Formular.ErlaubteVerfahrenSetzen([]);
        AusgewaehlteGruppe = null;
        Fokus.AuswahlFreigeben();
        _kontext.Leeren();
        _quelle.VorschauVerwerfen();
        MehrfachAusgewaehlteSpiele = [];

        // Wie in SpiellisteAktualisierenAsync (#317): das Leeren der Liste kann SelectionChanged verzögert auslösen.
        using (_spielWechsel.Betreten())
        {
            AusgewaehlteSpiel = null;
            RundeStillSetzen(null);
            await ListeLadenAsync();
        }

        AktualisiereBefehle();
    }

    /// <summary>Mannschaftsansicht: die Runde des gewählten Spiels wird still zur ausgewählten Runde — mit deren
    /// Entscheidungsverfahren für das Formular, Tabellen und Turnierbaum.</summary>
    private async Task KontextRundeSetzenAsync(int rundeId)
    {
        if (AusgewaehlteRunde?.Id == rundeId) return;

        RundeStillSetzen(Runden.FirstOrDefault(r => r.Id == rundeId));
        _kontext.Leeren();
        if (AusgewaehlteRunde is not { } runde) return;

        await TabellenNeuLadenAsync(runde.Id);
        await ErlaubteVerfahrenLadenAsync(runde);
    }

    private async Task TeilnehmerLadenAsync()
    {
        Mannschaftsfilter.TeilnehmerSetzen(AusgewaehlteInstanz is { } instanz
            ? await _spielService.MannschaftenNachInstanzAsync(instanz.Id)
            : []);
    }

    // ── Spielliste ────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SpielZeile> _spiele = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpieltagSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(SpieltagZuruecksetzenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private SpielZeile? _ausgewaehlteSpiel;

    // ── Ergebnis-Formular ─────────────────────────────────────────────────────

    [ObservableProperty] private bool _formAktiv;
    [ObservableProperty] private string? _fehlermeldung;

    public bool HatUngespeicherteAenderungen => FormAktiv && Formular.IsDirty;
    public ErgebnisFormularViewModel Formular { get; } = new();

    public bool IstKoRunde => AusgewaehlteRunde?.Rundenart is
        Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    // ── Platzhalter-Vorschau (Issue #208, sichtbarer Toggle-Zustand #325) ────────────────────────────────
    // RibbonAktion.IstAktiv steuert das AppBarToggleButton-Rendering in MainWindow.xaml.cs
    // (siehe RibbonZusatzGruppen unten).

    public bool HatPlatzhalterSpiele => Spiele.Any(s => s.IstPlatzhalterSpiel);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private bool _vorschauAktiv;

    private readonly RelayCommand _vorschauUmschaltenBefehl;

    private async Task AktualisierePlatzhalterVorschauAsync()
    {
        if (IstMannschaftsansicht) return;

        if (VorschauAktiv && AusgewaehlteRunde is not null) await _quelle.VorschauLadenAsync(AusgewaehlteRunde.Id);
        else _quelle.VorschauVerwerfen();

        await ListeLadenAsync();
    }

    // ── Simulation ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _istSimulierend;

    /// <summary>Simulieren und Zurücksetzen auf Ebene Runde, Hin-/Rückrunde, Gruppe, Spieltag und Auswahl
    /// (Issue #14, #123).</summary>
    public SpielmengenAktionen Mengen { get; }

    // ── Manuelle Multi-Selektion (Issue #124) ────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnzahlMehrfachSimulierbar))]
    [NotifyPropertyChangedFor(nameof(AuswahlSimulierenMenuText))]
    [NotifyPropertyChangedFor(nameof(RibbonZusatzGruppen))]
    private IReadOnlyList<SpielZeile> _mehrfachAusgewaehlteSpiele = [];

    public int AnzahlMehrfachSimulierbar =>
        MehrfachAusgewaehlteSpiele.Count(SpielmengenAktionen.IstSimulierbar);
    public string AuswahlSimulierenMenuText => $"Auswahl ({AnzahlMehrfachSimulierbar}) simulieren";

    public void AktualisiereMehrfachAuswahl(IEnumerable<SpielZeile> ausgewaehlt)
    {
        MehrfachAusgewaehlteSpiele = ausgewaehlt.ToList();
        _loeschenBefehl.NotifyCanExecuteChanged();
        Mengen.BefehleMelden();
    }

    // ── IRibbonAware ──────────────────────────────────────────────────────────

    public string RibbonTabTitel => "Spielplan";
    public ICommand NeuBefehl => _neuBefehl;
    public ICommand SpeichernBefehl => _speichernBefehl;
    public ICommand AbbrechenBefehl => _abbrechenBefehl;
    public ICommand LoeschenBefehl => _loeschenBefehl;

    /// <summary>#302: Sprung in den Simulation-Tab derselben, bereits offenen Turnierdurchführung-Seite
    /// (abgefangen von <see cref="TurnierdurchfuehrungViewModel"/>, Precedent WPF-Original
    /// <c>TurnierBereichViewModel</c>/<c>TurnierDurchfuehrungViewModel</c>). "Ausführlich simulieren"
    /// startet dort eine neue Simulation (ggf. mit Überschreiben-Rückfrage), "Spielverlauf ansehen" zeigt
    /// nur das bereits vorhandene Protokoll (Issue #240, <see cref="VorausgewaehlteSimulation.NurAnzeigen"/>).</summary>
    public event EventHandler<VorausgewaehlteSimulation>? AusfuehrlicheSimulationAngefordert;

    private void AusfuehrlichSimulieren()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;
        AusfuehrlicheSimulationAngefordert?.Invoke(this, new VorausgewaehlteSimulation(
            AusgewaehlteInstanz.Id, AusgewaehlteRunde.Id, AusgewaehlteSpiel.Id));
    }

    private void SpielverlaufAnsehen()
    {
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;
        AusfuehrlicheSimulationAngefordert?.Invoke(this, new VorausgewaehlteSimulation(
            AusgewaehlteInstanz.Id, AusgewaehlteRunde.Id, AusgewaehlteSpiel.Id, NurAnzeigen: true));
    }

    private bool CanAusfuehrlichSimulieren() => AusgewaehlteSpiel is { IstPlatzhalterSpiel: false };
    private bool CanSpielverlaufAnsehen() => AusgewaehlteSpiel is { IstPlatzhalterSpiel: false, HatErgebnis: true };

    // Simulieren/Zurücksetzen als je EIN Flyout-Button statt elf Einzelbuttons (Nutzerfund #301-Test:
    // Ribbon-Leiste selbst mit Overflow unübersichtlich überfüllt) — die einzelnen Ebenen (Runde/Hinrunde/
    // Rückrunde/Gruppe/Spieltag/Auswahl) bleiben als Flyout-Menüpunkte erreichbar, nur die Andockstelle im
    // Ribbon selbst schrumpft von 11 auf 2 Buttons. Platzhalter-Vorschau bleibt eigener Button (Toggle,
    // kein Aktions-Flyout).
    public IReadOnlyList<RibbonGruppe> RibbonZusatzGruppen =>
    [
        new RibbonGruppe
        {
            Header = "Spielplan",
            Aktionen =
            [
                new RibbonAktion
                {
                    Header = "Simulieren",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        Ebene("Runde", Mengen.RundeSimulierenBefehl),
                        Ebene(HinrundeMenuText, Mengen.HinrundeSimulierenBefehl),
                        Ebene(RueckrundeMenuText, Mengen.RueckrundeSimulierenBefehl),
                        Ebene("Gruppe", Mengen.GruppeSimulierenBefehl),
                        Ebene(SpieltagSimulierenMenuText, Mengen.SpieltagSimulierenBefehl),
                        new RibbonUnteraktion(AuswahlSimulierenMenuText, Mengen.AuswahlSimulierenBefehl),
                    ],
                },
                new RibbonAktion
                {
                    Header = "Zurücksetzen",
                    IconGlyph = "",
                    Unteraktionen =
                    [
                        Ebene("Runde", Mengen.RundeZuruecksetzenBefehl),
                        Ebene(HinrundeMenuText, Mengen.HinrundeZuruecksetzenBefehl),
                        Ebene(RueckrundeMenuText, Mengen.RueckrundeZuruecksetzenBefehl),
                        Ebene("Gruppe", Mengen.GruppeZuruecksetzenBefehl),
                        Ebene(SpieltagZuruecksetzenMenuText, Mengen.SpieltagZuruecksetzenBefehl),
                    ],
                },
                new RibbonAktion
                {
                    Header = "Ausführlich simulieren",
                    Befehl = _ausfuehrlichSimulierenBefehl,
                    IconGlyph = "",
                },
                new RibbonAktion
                {
                    Header = "Spielverlauf ansehen",
                    Befehl = _spielverlaufAnsehenBefehl,
                    IconGlyph = "",
                },
                new RibbonAktion
                {
                    Header = "Platzhalter-Vorschau",
                    Befehl = _vorschauUmschaltenBefehl,
                    IstAktiv = VorschauAktiv,
                    IconGlyph = "",
                },
            ],
        },
    ];

    private readonly AsyncRelayCommand _neuBefehl;
    private readonly AsyncRelayCommand _speichernBefehl;
    private readonly RelayCommand _abbrechenBefehl;
    private readonly AsyncRelayCommand _loeschenBefehl;
    private readonly RelayCommand _ausfuehrlichSimulierenBefehl;
    private readonly RelayCommand _spielverlaufAnsehenBefehl;
    private readonly AsyncRelayCommand _naechsteSchweizerRundeBefehl;

    public ICommand NaechsteSchweizerRundeBefehl => _naechsteSchweizerRundeBefehl;
    public ICommand AusfuehrlichSimulierenBefehl => _ausfuehrlichSimulierenBefehl;
    public ICommand SpielverlaufAnsehenBefehl => _spielverlaufAnsehenBefehl;

    /// <summary>Menüpunkt einer Spielmengen-Ebene — in der Mannschaftsansicht gesperrt mit Begründung (#479).</summary>
    private RibbonUnteraktion Ebene(string header, ICommand befehl) => new(
        header, befehl,
        SperrHinweis: IstMannschaftsansicht ? SpielmengenAktionen.SPERR_HINWEIS_MANNSCHAFTSANSICHT : null);

    // ── Konstruktor ───────────────────────────────────────────────────────────

    public SpielplanViewModel(
        SpielErgebnisService spielService,
        SpielplanService spielplanService,
        SpiellistenQuelle quelle,
        PlatzhalterKaskadeService kaskade,
        FinaleAutomatik finaleAutomatik,
        NeuGenerierungsRueckfrage neuGenerierung,
        TurnierBaumService baumService,
        SpielplanRundenKontext kontext,
        SimulationsService simulationsService,
        TurnierInstanzService instanzService,
        IDialogService dialogService,
        ILogger<SpielplanViewModel> logger)
    {
        _spielService = spielService;
        _spielplanService = spielplanService;
        _quelle = quelle;
        _kaskade = kaskade;
        _finaleAutomatik = finaleAutomatik;
        _neuGenerierung = neuGenerierung;
        _baumService = baumService;
        _kontext = kontext;
        _instanzService = instanzService;
        _dialogService = dialogService;
        _logger = logger;

        _neuBefehl = new AsyncRelayCommand(SpielplanNeuGenerierenAsync, () => HatRunde && !IstMannschaftsansicht);
        _speichernBefehl = new AsyncRelayCommand(SpeichernAsync, () => FormAktiv && Formular.IsDirty);
        _abbrechenBefehl = new RelayCommand(FormZuruecksetzen, () => FormAktiv && Formular.IsDirty);
        _loeschenBefehl = new AsyncRelayCommand(ErgebnisLoeschenAsync, CanErgebnisLoeschen);

        _zurueckZurUebersichtBefehl = new RelayCommand(() => AusgewaehlteGruppe = null, () => AusgewaehlteGruppe is not null);

        Mengen = new SpielmengenAktionen(this, simulationsService, spielService, dialogService);

        // #315: Vorschau nur bei offenen Platzhalter-Spielen anbietbar — eine aktive Vorschau bleibt aber
        // abschaltbar, auch wenn die Runde inzwischen keine Platzhalter mehr hat.
        _vorschauUmschaltenBefehl = new RelayCommand(() =>
        {
            VorschauAktiv = !VorschauAktiv;
            _vorschauUmschaltenBefehl!.NotifyCanExecuteChanged();
            _ = DbContextSchutz.AusfuehrenAsync(AktualisierePlatzhalterVorschauAsync, ex => Fehlermeldung = ex.Message);
        }, () => !IstMannschaftsansicht && (HatPlatzhalterSpiele || VorschauAktiv));

        _ausfuehrlichSimulierenBefehl = new RelayCommand(AusfuehrlichSimulieren, CanAusfuehrlichSimulieren);
        _spielverlaufAnsehenBefehl = new RelayCommand(SpielverlaufAnsehen, CanSpielverlaufAnsehen);

        _naechsteSchweizerRundeBefehl = new AsyncRelayCommand(NaechsteSchweizerRundeGenerierenAsync, KannNaechsteSchweizerRundeGenerieren);
        Fokus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SpieltagFokusViewModel.GewaehlterSpieltag)) return;
            OnPropertyChanged(nameof(SpieltagSimulierenMenuText));
            OnPropertyChanged(nameof(SpieltagZuruecksetzenMenuText));
            OnPropertyChanged(nameof(RibbonZusatzGruppen));
            Mengen.BefehleMelden();
        };
        Formular.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ErgebnisFormularViewModel.IsDirty)) return;
            _speichernBefehl.NotifyCanExecuteChanged();
            _abbrechenBefehl.NotifyCanExecuteChanged();
        };
        Mannschaftsfilter.WahlAngefordert = mannschaft =>
            AnsichtWechselnAsync(mannschaft, AusgewaehlteRunde ?? _rundeVorMannschaftsansicht);
        Spiele.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(ZeigtKeineMannschaftsSpiele));
            SchweizerAnzeigeAktualisieren();
            _vorschauUmschaltenBefehl.NotifyCanExecuteChanged();
        };
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    public override async Task AktivierenAsync()
    {
        if (!ZeigtSpielliste || (FormAktiv && Formular.IsDirty)) return;
        await SpiellisteAktualisierenAsync();
    }

    private async Task SpiellisteAktualisierenAsync()
    {
        if (AusgewaehlteInstanz is not { } instanz || !ZeigtSpielliste) return;
        int? rundeId = AusgewaehlteRunde?.Id;
        int? spielId = AusgewaehlteSpiel?.Id;

        // #317: Guard umschließt Spiele.Clear()/Add() bis zum Ende der davon abhängigen Folge-Abfragen,
        // nicht nur die AusgewaehlteSpiel-Zuweisung — eine WinUI-ComboBox kann SelectionChanged bei
        // ItemsSource.Clear() über die DispatcherQueue VERZÖGERT statt synchron auslösen. Feuert der
        // Callback erst während der Turnierbaum-Aktualisierung/BeiSpielAenderungAsync, entsteht sonst ein
        // ungeschützter Parallelzugriff auf denselben DbContext ("second operation"-Exception, #317).
        using (_spielWechsel.Betreten())
        {
            await SpieleNeuLadenAsync(rundeId);

            AusgewaehlteSpiel = spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null;

            await _baumService.NachSpielplanAenderungAktualisierenAsync(instanz.Id);
        }

        await BeiSpielAenderungAsync();
        AktualisiereBefehle();
    }

    public async Task InstanzSetzenAsync(TurnierInstanz? instanz)
    {
        AusgewaehlteInstanz = instanz;
        await BeiInstanzAenderungAsync();
    }

    // ── Property-Reaktionen ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(AusgewaehlteRunde):
                _ = RundeWechselAsync();
                break;
            case nameof(AusgewaehlteSpiel):
                _ = SpielWechselAsync();
                break;
            case nameof(AusgewaehlteGruppe):
                BeiGruppeFokusAenderung();
                Fokus.SpieleMelden();
                break;
        }
    }

    // ── Selektions-Handler ────────────────────────────────────────────────────

    private int? _geladeneInstanzId;

    private async Task BeiInstanzAenderungAsync()
    {
        using var vorgang = _logger.Vorgang($"Spielplan: Turnier setzen (Instanz {AusgewaehlteInstanz?.Id})");

        FormAktiv = false;

        int? vorherigeRundeId = AusgewaehlteRunde?.Id;

        var instanz = AusgewaehlteInstanz is null
            ? null
            : await _instanzService.FindenNachIdMitRundenAsync(AusgewaehlteInstanz.Id);

        // #317: Guard umschließt Runden.Clear()/Add() bis zum Ende des kompletten Reloads (inkl. des
        // verschachtelten SpiellisteAktualisierenAsync-Aufrufs), nicht nur die AusgewaehlteRunde-
        // Zuweisung — eine WinUI-ComboBox kann SelectionChanged bei ItemsSource.Clear() über die
        // DispatcherQueue VERZÖGERT statt synchron auslösen. Feuert der Callback erst während einer der
        // nachfolgenden Abfragen, entsteht sonst ein ungeschützter Parallelzugriff auf denselben
        // DbContext ("second operation"-Exception, bislang nur per Retry im Composite abgefangen, #317).
        using var unterdrueckung = _rundeWechsel.Betreten();

        await TeilnehmerLadenAsync();

        // Mannschaftsansicht (#479): bleibt beim reinen Reiterwechsel, endet beim Turnierwechsel.
        bool istTurnierwechsel = instanz?.Id != _geladeneInstanzId;
        _geladeneInstanzId = instanz?.Id;
        if (IstMannschaftsansicht && istTurnierwechsel)
        {
            Mannschaftsfilter.Anzeigen(null);
            _rundeVorMannschaftsansicht = null;
            MannschaftsansichtGeaendert();
        }

        if (instanz is null)
        {
            Runden.Clear();
            await RundeUndSpielZuruecksetzenAsync();
            return;
        }

        Runden.Clear();
        foreach (var r in instanz.Runden.OrderBy(r => r.Reihenfolge))
            Runden.Add(r);

        if (IstMannschaftsansicht)
        {
            AusgewaehlteRunde = vorherigeRundeId.HasValue ? Runden.FirstOrDefault(r => r.Id == vorherigeRundeId) : null;
            _rundeVorMannschaftsansicht = _rundeVorMannschaftsansicht is { } vorher
                ? Runden.FirstOrDefault(r => r.Id == vorher.Id)
                : null;
            await SpiellisteAktualisierenAsync();
            return;
        }

        // Reiner Tab-Wechsel ohne echten Instanzwechsel (#307): dieselbe Runde existiert nach dem
        // Neuladen noch — Auswahl anhand der Id wiederherstellen statt sie zu verlieren. Bei echtem
        // Instanzwechsel findet sich die alte Runden-Id nicht mehr (Ids sind global eindeutig), dann
        // greift der volle Reset wie zuvor.
        var wiederhergestellteRunde = vorherigeRundeId.HasValue
            ? Runden.FirstOrDefault(r => r.Id == vorherigeRundeId)
            : null;

        if (wiederhergestellteRunde is null)
        {
            await RundeUndSpielZuruecksetzenAsync();
            return;
        }

        AusgewaehlteRunde = wiederhergestellteRunde;

        // Spiele/Tabellen frisch laden (externe Änderungen aus anderen Tabs sichtbar machen), aber
        // Spiel-Auswahl anhand ihrer Id wiederherstellen statt hart zurückzusetzen (Precedent:
        // SpiellisteAktualisierenAsync, bereits genau dieses Muster für AktivierenAsync).
        await SpiellisteAktualisierenAsync();
    }

    private async Task RundeUndSpielZuruecksetzenAsync()
    {
        Spiele.Clear();
        AusgewaehlteSpiel = null;
        RundeStillSetzen(null);

        await BeiRundeAenderungAsync();
    }

    /// <summary>Setzt die Rundenauswahl, ohne den Rundenwechsel (Abfrage, Laden) auszulösen.</summary>
    private void RundeStillSetzen(TurnierRunde? runde)
    {
        using (_rundeWechsel.Betreten())
            AusgewaehlteRunde = runde;
    }

    /// <summary>Setzt die Spielauswahl, ohne den Spielwechsel (Abfrage, Laden) auszulösen.</summary>
    private void SpielStillSetzen(SpielZeile? spiel)
    {
        using (_spielWechsel.Betreten())
            AusgewaehlteSpiel = spiel;
    }

    private async Task RundeWechselAsync()
    {
        if (_rundeWechsel.IstAktiv) return;

        var neueRundeId = AusgewaehlteRunde?.Id;

        if (Formular.RundeId is { } vorherigeRundeId && vorherigeRundeId != neueRundeId &&
            !await UngespeichertesKlaerenAsync())
        {
            RundeStillSetzen(Runden.FirstOrDefault(r => r.Id == vorherigeRundeId));
            return;
        }

        // #338: zwei echte, schnell aufeinanderfolgende Runde-Auswahländerungen (z.B. Pfeiltasten in der
        // ComboBox) können sonst überlappende Abfragen auf demselben DbContext auslösen ("second
        // operation"-Exception) — derselbe Root Cause wie #317, hier innerhalb des Tabs statt beim
        // Instanzwechsel. istNochAktuell verwirft veraltete Zwischenauswahlen aus der Lock-Warteschlange
        // (Ruckel-Fund aus #338-Test) — nur die zuletzt tatsächlich ausgewählte Runde führt die Abfrage aus.
        await DbContextSchutz.AusfuehrenAsync(
            BeiRundeAenderungAsync, ex => Fehlermeldung = ex.Message,
            istNochAktuell: () => AusgewaehlteRunde?.Id == neueRundeId);
    }

    /// <summary>Fragt bei ungespeicherter Eingabe im Formular nach, bevor die Liste wechselt (Runde, Mannschaft):
    /// <c>true</c> = weiter, die Eingabe ist gespeichert oder verworfen; ohne Eingabe ohne Rückfrage.</summary>
    private async Task<bool> UngespeichertesKlaerenAsync()
    {
        if (!Formular.IsDirty) return true;

        var spielId = Formular.SpielId;
        var rundeId = Formular.RundeId;
        var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();
        bool weiter = await AntwortUmsetzenAsync(antwort, async () =>
            spielId is not { } id ||
            Runden.FirstOrDefault(r => r.Id == rundeId) is { } runde &&
            await ErgebnisSpeichernAsync(id, runde, danachNeuLaden: false));

        if (weiter) Formular.AenderungenVergessen();
        return weiter;
    }

    /// <summary>Setzt die Antwort auf die Ungespeichert-Abfrage um und liefert, ob der Wechsel weitergeht:
    /// „Abbrechen“ nein, „Verwerfen“ ja, „Speichern“ je nach <paramref name="speichern"/> — das läuft im
    /// <see cref="DbContextSchutz"/>. Was bei „nein“ zurückgenommen und danach geladen wird, entscheidet der
    /// Aufrufer.</summary>
    private async Task<bool> AntwortUmsetzenAsync(UngespeichertAntwort antwort, Func<Task<bool>> speichern)
    {
        if (antwort == UngespeichertAntwort.Abbrechen) return false;
        if (antwort != UngespeichertAntwort.Speichern) return true;

        using (await DbContextSchutz.BetretenAsync())
            return await speichern();
    }

    private async Task BeiRundeAenderungAsync()
    {
        using var vorgang = _logger.Vorgang($"Spielplan: Runde laden (Runde {AusgewaehlteRunde?.Id})", "Runde wird geladen …");

        Spiele.Clear();
        AusgewaehlteSpiel = null;
        FormAktiv = false;
        Fehlermeldung = null;
        Formular.ErlaubteVerfahrenSetzen([]);
        AusgewaehlteGruppe = null;
        Fokus.AuswahlFreigeben();
        FokusSpieltageAbgleichen();
        _kontext.Leeren();
        _quelle.VorschauVerwerfen();
        AktualisiereBefehle();

        if (AusgewaehlteRunde is null)
        {
            if (RundeGeaendertAsync is not null) await RundeGeaendertAsync(null);
            return;
        }

        await TabellenNeuLadenAsync(AusgewaehlteRunde.Id);

        if (VorschauAktiv) await _quelle.VorschauLadenAsync(AusgewaehlteRunde.Id);
        await ListeLadenAsync();

        await ErlaubteVerfahrenLadenAsync(AusgewaehlteRunde);

        AktualisiereBefehle();
    }

    private async Task ErlaubteVerfahrenLadenAsync(TurnierRunde runde) =>
        Formular.ErlaubteVerfahrenSetzen(
            await _kontext.ErlaubteVerfahrenAsync(runde, AusgewaehlteInstanz?.StandardRegelwerkId));

    private async Task GruppenTabellenLadenAsync(int rundeId)
    {
        await _kontext.GruppenTabellenLadenAsync(rundeId);

        if (AusgewaehlteGruppe is { } aktuelleGruppe)
            AusgewaehlteGruppe = _kontext.Gruppe(aktuelleGruppe.GruppenNummer);
    }

    private async Task TabellenNeuLadenAsync(int rundeId)
    {
        if (AusgewaehlteRunde?.Rundenart == Rundenart.Gruppenphase)
            await GruppenTabellenLadenAsync(rundeId);

        if (SpielstandGeaendertAsync is not null)
            await SpielstandGeaendertAsync();

        if (RundeGeaendertAsync is not null)
            await RundeGeaendertAsync(AusgewaehlteRunde);
    }

    public Func<Task>? SpielstandGeaendertAsync { get; set; }
    public Func<TurnierRunde?, Task>? RundeGeaendertAsync { get; set; }

    private async Task SpielWechselAsync()
    {
        if (_spielWechsel.IstAktiv) return;

        var neuesSpielId = AusgewaehlteSpiel?.Id;

        if (Formular.IsDirty && Formular.SpielId is { } vorherigeSpielId &&
            Formular.RundeId == AusgewaehlteRunde?.Id && vorherigeSpielId != neuesSpielId)
        {
            var vorherigeRundeId = Formular.RundeId!.Value;

            var antwort = await _dialogService.UngespeicherteAenderungenAbfragenAsync();

            // Die Unterdrückung reicht vom Speichern bis zum Setzen der Auswahl: das Neuladen nach dem Speichern
            // baut die Liste neu auf und nullt dabei die Auswahl.
            using (_spielWechsel.Betreten())
            {
                // Ein gescheitertes Speichern hält den Spielwechsel auf wie beim Rundenwechsel: die Auswahl geht
                // zurück auf das vorherige Spiel, Eingabe und Fehlermeldung bleiben stehen (#461).
                bool wechseln = await AntwortUmsetzenAsync(antwort, async () =>
                    AusgewaehlteRunde is not { } runde || runde.Id != vorherigeRundeId ||
                    await ErgebnisSpeichernAsync(vorherigeSpielId, runde, danachNeuLaden: true));

                int? zielSpielId = wechseln ? neuesSpielId : vorherigeSpielId;
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == zielSpielId);
                if (!wechseln) return;
            }
        }

        // #338: siehe Kommentar in RundeWechselAsync — schützt gegen zwei echte, schnell
        // aufeinanderfolgende Spiel-Auswahländerungen; istNochAktuell verwirft veraltete Zwischenauswahlen.
        await DbContextSchutz.AusfuehrenAsync(
            BeiSpielAenderungAsync, ex => Fehlermeldung = ex.Message,
            istNochAktuell: () => AusgewaehlteSpiel?.Id == neuesSpielId);
    }

    private async Task BeiSpielAenderungAsync()
    {
        FormAktiv = false;
        Fehlermeldung = null;

        AktualisiereBefehle();

        if (AusgewaehlteSpiel is null) return;
        if (IstMannschaftsansicht) await KontextRundeSetzenAsync(AusgewaehlteSpiel.RundeId);

        var spiel = await _spielService.FindenNachIdAsync(AusgewaehlteSpiel.Id);
        if (spiel is null) return;

        Formular.AusFuellenMit(spiel, AusgewaehlteRunde, RundenHinweis, await HinspielToreAsync(spiel));
        FormAktiv = true;
        AktualisiereBefehle();
    }

    /// <summary>Tore des Hinspiels (aus dessen Sicht) zu einem K.-o.-Rückspiel, sofern das Hinspiel schon ein
    /// Ergebnis hat (Issue #346).</summary>
    private async Task<(int Heim, int Auswaerts)?> HinspielToreAsync(Spiel spiel)
    {
        if (!IstKoMitRueckspiel || !spiel.IstRueckspiel || spiel.HinspielId is not int hinspielId) return null;
        if ((await _spielService.FindenNachIdAsync(hinspielId))?.Ergebnis is not { } hinspiel) return null;

        return (HinRueckspielWertung.HinspielTore(hinspiel, heim: true),
                HinRueckspielWertung.HinspielTore(hinspiel, heim: false));
    }

    private async Task SpielAuswahlWiederherstellenAsync(int? spielId)
    {
        SpielStillSetzen(spielId.HasValue ? Spiele.FirstOrDefault(s => s.Id == spielId) : null);
        await BeiSpielAenderungAsync();
    }

    // ── Ribbon-Aktionen ───────────────────────────────────────────────────────

    private async Task SpielplanNeuGenerierenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is null || AusgewaehlteInstanz is null) return;

        var runde = AusgewaehlteRunde;
        Fehlermeldung = null;

        var auslosung = await _neuGenerierung.AuslosungFuerNeuGenerierungAsync(
            runde, AusgewaehlteInstanz.Id, Spiele.Count, fehler => Fehlermeldung = fehler);
        if (auslosung is null) return;

        using var vorgang = _logger.Vorgang($"Spielplan neu generieren (Runde {runde.Id})", "Spielplan wird neu generiert …");
        await AendernUndNeuLadenAsync(
            // Löschen, Generieren und die eine Kaskade danach (#320, #306) stehen im Dienst — derselbe Ablauf
            // wie beim Abschließen einer Auslosung.
            () => _kaskade.RundenSpielplanAusAuslosungErsetzenAsync(runde.Id, [.. auslosung.Ergebnisse]),
            async () =>
            {
                await SpieleNeuLadenAsync(runde.Id);
                AusgewaehlteSpiel = null;
                await _baumService.NachSpielplanAenderungAktualisierenAsync(runde.TurnierInstanzId);
                await TeilnehmerLadenAsync();
            });
    }

    private bool KannNaechsteSchweizerRundeGenerieren() =>
        ZeigtSchweizerRundenSteuerung && Spiele.Count > 0 &&
        SchweizerAktuellerSpieltagKomplett && SchweizerAktuellerSpieltag < SchweizerZielSpieltage;

    private async Task NaechsteSchweizerRundeGenerierenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteRunde is not { } runde) return;
        Fehlermeldung = null;

        using var vorgang = _logger.Vorgang($"Spielplan: nächste Schweizer Runde generieren (Runde {runde.Id})", "Nächste Runde wird generiert …");
        bool generiert = await AendernUndNeuLadenAsync(
            () => _spielplanService.SchweizerNaechsteRundeGenerierenAsync(runde.Id),
            async () =>
            {
                await SpieleNeuLadenAsync(runde.Id);
                await _baumService.NachSpielplanAenderungAktualisierenAsync(runde.TurnierInstanzId);
            });

        // Issue #363: direkt den gerade generierten Spieltag zeigen.
        if (generiert && AusgewaehlteGruppe is null) Fokus.LetztenSpieltagZeigen();
    }

    private async Task SpeichernAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteSpiel is null || AusgewaehlteRunde is null) return;
        var spielId = AusgewaehlteSpiel.Id;

        if (await ErgebnisSpeichernAsync(spielId, AusgewaehlteRunde, danachNeuLaden: true))
            AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
    }

    /// <summary>Speichert das Formular als Ergebnis des Spiels; läuft im bereits betretenen
    /// <see cref="DbContextSchutz"/>.</summary>
    /// <param name="danachNeuLaden">Tabellen und Spiele der Runde danach frisch laden — nur, wenn
    /// <paramref name="runde"/> die ausgewählte bleibt. Beim Rundenwechsel lädt die neue Runde ohnehin.</param>
    private async Task<bool> ErgebnisSpeichernAsync(int spielId, TurnierRunde runde, bool danachNeuLaden)
    {
        Fehlermeldung = null;
        try
        {
            using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis speichern (Spiel {spielId})", "Ergebnis wird gespeichert …");
            await _spielService.ErgebnisErfassenAsync(spielId, Formular.AlsEingabe());
            await _finaleAutomatik.AbgleichenAsync(runde.TurnierInstanzId);

            if (!danachNeuLaden) return true;

            // Das gesamte Neuladen läuft unterdrückt: sowohl das Neusetzen von AusgewaehlteGruppe in
            // GruppenTabellenLadenAsync als auch FokusSpiele geben der Fokus-Liste (Gruppenfilter/Spieltag) eine
            // neue ItemsSource, die deren SelectedItem nullt — das löste trotz gerade laufendem Speichern die
            // Ungespeichert-Abfrage aus (Issue #345).
            using (_spielWechsel.Betreten())
                await SpieleNeuLadenAsync(runde.Id);

            return true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            return false;
        }
    }

    private void FormZuruecksetzen() =>
        _ = DbContextSchutz.AusfuehrenAsync(BeiSpielAenderungAsync, ex => Fehlermeldung = ex.Message);

    private Task ErgebnisLoeschenAsync() =>
        MehrfachAusgewaehlteSpiele.Count > 1 ? Mengen.AuswahlZuruecksetzenAsync() : ErgebnisEinzelLoeschenAsync();

    private async Task ErgebnisEinzelLoeschenAsync()
    {
        using var schutz = await DbContextSchutz.BetretenAsync();
        if (AusgewaehlteSpiel is null || !Formular.HatErgebnis || AusgewaehlteRunde is null) return;

        var spielId = AusgewaehlteSpiel.Id;
        var rundeId = AusgewaehlteRunde.Id;
        var spielnummer = AusgewaehlteSpiel.Spielnummer;
        var kaskadenHinweis = await KaskadenHinweisAsync([spielId]);

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnis löschen", $"Ergebnis von Spiel #{spielnummer} wirklich löschen?{kaskadenHinweis}");
        if (!bestaetigt) return;

        var instanzId = AusgewaehlteRunde.TurnierInstanzId;
        Fehlermeldung = null;

        using var vorgang = _logger.Vorgang($"Spielplan: Ergebnis löschen (Spiel {spielId})", "Ergebnis wird gelöscht …");
        await AendernUndNeuLadenAsync(
            () => _spielService.ErgebnisLoeschenAsync(spielId),
            async () =>
            {
                await SpieleNeuLadenAsync(rundeId);
                AusgewaehlteSpiel = Spiele.FirstOrDefault(s => s.Id == spielId);
                await _baumService.NachSpielplanAenderungAktualisierenAsync(instanzId);
            });
    }

    private async Task<string> KaskadenHinweisAsync(IReadOnlyList<int> spielIds)
    {
        if (AusgewaehlteInstanz is null || spielIds.Count == 0) return "";
        using var vorgang = _logger.Vorgang($"Spielplan: Kaskaden-Vorschau ({spielIds.Count} Spiele)");
        var vorschau = await _kaskade.PlanenAsync(AusgewaehlteInstanz.Id, spielIds.ToHashSet());
        return vorschau.HatAuswirkungen
            ? $"\n\nZusätzlich werden dadurch {vorschau.GesamtBetroffeneSpiele} Ergebnis(se) in Folgerunden " +
              "zurückgesetzt, deren Paarungen davon abhängen."
            : "";
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private void AktualisiereBefehle()
    {
        _neuBefehl.NotifyCanExecuteChanged();
        _speichernBefehl.NotifyCanExecuteChanged();
        _abbrechenBefehl.NotifyCanExecuteChanged();
        _loeschenBefehl.NotifyCanExecuteChanged();
        Mengen.BefehleMelden();
        _ausfuehrlichSimulierenBefehl.NotifyCanExecuteChanged();
        _spielverlaufAnsehenBefehl.NotifyCanExecuteChanged();
        _vorschauUmschaltenBefehl.NotifyCanExecuteChanged();
        SchweizerAnzeigeAktualisieren();
    }

    private int? _vorherigeFokusGruppenNummer;

    private void BeiGruppeFokusAenderung()
    {
        bool istEchterGruppenwechsel = AusgewaehlteGruppe?.GruppenNummer != _vorherigeFokusGruppenNummer;
        _vorherigeFokusGruppenNummer = AusgewaehlteGruppe?.GruppenNummer;

        Mengen.BefehleMelden();
        _zurueckZurUebersichtBefehl.NotifyCanExecuteChanged();

        // Beim Gruppenwechsel bleibt der angezeigte Spieltag stehen, sofern die Gruppe ihn hat (Issue #363).
        if (istEchterGruppenwechsel)
            FokusSpieltageAbgleichen();
    }

    private IReadOnlyList<SpielZeile>? SpieltagRelevanteSpiele() =>
        IstMannschaftsansicht ? null
        : AusgewaehlteGruppe is { } gruppe ? Spiele.Where(s => s.GruppeNummer == gruppe.GruppenNummer).ToList()
        : IstSpieltagRunde ? Spiele.ToList()
        : null;

    /// <summary>Gibt dem Spieltag-Fokus die gerade betrachteten Spiele: die der gewählten Gruppe, sonst alle
    /// einer Runde mit Spieltagen (Issue #363).</summary>
    private void FokusSpieltageAbgleichen()
    {
        Fokus.Abgleichen(
            SpieltagRelevanteSpiele() ?? [], nachGruppeGegliedert: AusgewaehlteGruppe is null && IstGruppenphase);
        Mengen.BefehleMelden();
    }

    private GruppenTabelle? EffektiveGruppe =>
        AusgewaehlteGruppe ?? (AusgewaehlteSpiel?.GruppeNummer is { } gn ? _kontext.Gruppe(gn) : null);

    private int? EffektiverSpieltag =>
        ZeigeFokusListe && Fokus.GewaehlterSpieltag is { } spieltag
            ? spieltag
            : AusgewaehlteSpiel?.Spieltag;

    private bool CanErgebnisLoeschen() =>
        !IstSimulierend
        && (MehrfachAusgewaehlteSpiele.Count > 1 && MehrfachAusgewaehlteSpiele.Any(s => s.HatErgebnis)
            || FormAktiv && Formular.HatErgebnis);

    // ── Gemeinsamer Ablauf ────────────────────────────────────────────────────

    /// <summary>Führt eine Aktion auf mehreren Spielen der Runde aus und zeigt danach den neuen Stand. Läuft im
    /// bereits betretenen <see cref="DbContextSchutz"/>.</summary>
    async Task ISpielmengenKontext.SpielmengeBearbeitenAsync(
        string vorgangsName, string anzeige, Func<Task> aktion, AuswahlDanach auswahlDanach, bool finaleAbgleichen)
    {
        // Instanz statt Runde: die Auswahl der Mannschaftsansicht reicht über mehrere Runden (#479).
        if (AusgewaehlteInstanz is not { } instanz) return;
        int? rundeId = AusgewaehlteRunde?.Id;
        int? spielId = AusgewaehlteSpiel?.Id;
        Fehlermeldung = null;
        IstSimulierend = true;
        try
        {
            using var vorgang = _logger.Vorgang(vorgangsName, anzeige);
            await AendernUndNeuLadenAsync(
                async () =>
                {
                    await aktion();
                    if (finaleAbgleichen) await _finaleAutomatik.AbgleichenAsync(instanz.Id);
                },
                async () =>
                {
                    await SpieleNeuLadenAsync(rundeId);
                    await SpielAuswahlSetzenAsync(auswahlDanach, spielId);
                    await _baumService.NachSpielplanAenderungAktualisierenAsync(instanz.Id);
                });
        }
        finally
        {
            IstSimulierend = false;
            AktualisiereBefehle();
        }
    }

    /// <summary>Führt eine ändernde Aktion aus und zeigt danach in jedem Fall den Stand der Datenbank, auch wenn
    /// die Aktion wirft: Die Dienste speichern ohne gemeinsame Transaktion in mehreren Schritten (erst löschen,
    /// dann generieren; Ergebnisse je Spiel, dann die Kaskade). Ein Fehler mittendrin hinterlässt schon
    /// gespeicherte Änderungen — ohne Neuladen zeigte die Liste etwa Spiele, die es nicht mehr gibt (#460).
    /// Die Fehlermeldung der Aktion bleibt stehen.</summary>
    /// <returns>Ob die Aktion ohne Fehler durchlief.</returns>
    private async Task<bool> AendernUndNeuLadenAsync(Func<Task> aenderung, Func<Task> neuLaden)
    {
        bool erfolgreich = true;
        try
        {
            await aenderung();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
            erfolgreich = false;
        }

        try
        {
            await neuLaden();
        }
        catch (Exception ex)
        {
            Fehlermeldung ??= ex.Message;
        }

        return erfolgreich;
    }

    private async Task SpielAuswahlSetzenAsync(AuswahlDanach auswahlDanach, int? bisherigesSpielId)
    {
        if (auswahlDanach == AuswahlDanach.Wiederherstellen)
        {
            await SpielAuswahlWiederherstellenAsync(bisherigesSpielId);
            return;
        }

        AusgewaehlteSpiel = null;
        if (auswahlDanach == AuswahlDanach.MitMehrfachauswahlAufheben)
            MehrfachAusgewaehlteSpiele = [];
    }

    /// <summary>Tabellen und Spiele der Runde frisch aus der Datenbank — in der Mannschaftsansicht die Spiele der
    /// Mannschaft, <paramref name="rundeId"/> ist dort die Runde des gewählten Spiels (ohne Spiel <c>null</c>).
    /// Die Spiel-Auswahl setzt der Aufrufer.</summary>
    private async Task SpieleNeuLadenAsync(int? rundeId)
    {
        if (rundeId is int id) await TabellenNeuLadenAsync(id);
        else if (SpielstandGeaendertAsync is not null) await SpielstandGeaendertAsync();

        await ListeLadenAsync();
    }

    /// <summary>Füllt die Liste neu aus der <see cref="SpiellistenQuelle"/> — ohne Mannschaft und Runde bleibt sie,
    /// wie sie ist. Die Spiel-Auswahl setzt der Aufrufer.</summary>
    private async Task ListeLadenAsync()
    {
        if (AusgewaehlteInstanz is not { } instanz ||
            await _quelle.ZeilenAsync(instanz.Id, Mannschaftsfilter.Gewaehlt?.Id, AusgewaehlteRunde, _kontext.TeamGruppen)
                is not { } zeilen)
            return;

        Spiele.Clear();
        foreach (var zeile in zeilen)
            Spiele.Add(zeile);
        OnPropertyChanged(nameof(HatPlatzhalterSpiele));
        FokusSpieltageAbgleichen();
    }
}
