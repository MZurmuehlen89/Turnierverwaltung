using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Dialoge;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Was nach einer Aktion auf mehreren Spielen mit der Spiel-Auswahl geschieht.</summary>
public enum AuswahlDanach { Aufheben, MitMehrfachauswahlAufheben, Wiederherstellen }

/// <summary>Rückkanal von <see cref="SpielmengenAktionen"/> zum <see cref="SpielplanViewModel"/>: was gerade
/// gewählt ist, und der gemeinsame Ablauf, der eine Aktion ausführt und danach den neuen Stand zeigt.</summary>
public interface ISpielmengenKontext
{
    DbContextReentrancySchutz DbContextSchutz { get; }

    TurnierRunde? AusgewaehlteRunde { get; }
    IReadOnlyList<SpielZeile> Spiele { get; }
    IReadOnlyList<SpielZeile> MehrfachAusgewaehlteSpiele { get; }

    /// <summary>Der gesetzte Gruppenfilter.</summary>
    GruppenTabelle? AusgewaehlteGruppe { get; }

    /// <summary>Der Gruppenfilter, sonst die Gruppe des gewählten Spiels.</summary>
    GruppenTabelle? EffektiveGruppe { get; }

    /// <summary>Der gezeigte Spieltag, sonst der des gewählten Spiels.</summary>
    int? EffektiverSpieltag { get; }

    bool HatHinUndRueckspiel { get; }
    bool IstKoMitRueckspiel { get; }
    bool IstSimulierend { get; }

    /// <summary>Hinweis für die Rückfrage, falls das Zurücksetzen Ergebnisse in Folgerunden mitnimmt.</summary>
    Task<string> KaskadenHinweisAsync(IReadOnlyList<int> spielIds);

    /// <summary>Führt <paramref name="aktion"/> aus und zeigt danach den neuen Stand der Runde. Läuft im bereits
    /// betretenen <see cref="DbContextSchutz"/>.</summary>
    Task SpielmengeBearbeitenAsync(
        string vorgangsName, Func<Task> aktion, AuswahlDanach auswahlDanach, bool finaleAbgleichen, bool baumNachziehen);
}

/// <summary>Simulieren und Zurücksetzen auf den Ebenen Runde, Hin-/Rückrunde, Gruppe, Spieltag und Auswahl
/// (Issue #14, #123, #124; #402 aus <see cref="SpielplanViewModel"/> herausgelöst). Jede Ebene benennt ihre
/// Spiele, ihren Dienstaufruf und ihre Rückfrage — den Ablauf dahinter führt der Spielplan.</summary>
public sealed class SpielmengenAktionen
{
    private const SimulationsModus SIMULATIONS_MODUS = SimulationsModus.AusfuehrlicheSimulation;

    private readonly ISpielmengenKontext _kontext;
    private readonly SimulationsService _simulationsService;
    private readonly SpielErgebnisService _spielService;
    private readonly IDialogService _dialogService;

    private readonly AsyncRelayCommand[] _befehle;

    public SpielmengenAktionen(
        ISpielmengenKontext kontext,
        SimulationsService simulationsService,
        SpielErgebnisService spielService,
        IDialogService dialogService)
    {
        _kontext = kontext;
        _simulationsService = simulationsService;
        _spielService = spielService;
        _dialogService = dialogService;

        _befehle =
        [
            new(RundeSimulierenAsync, CanRundeSimulieren),
            new(HinrundeSimulierenAsync, CanHinrundeSimulieren),
            new(RueckrundeSimulierenAsync, CanRueckrundeSimulieren),
            new(GruppeSimulierenAsync, CanGruppeSimulieren),
            new(SpieltagSimulierenAsync, CanSpieltagSimulieren),
            new(AuswahlSimulierenAsync, CanAuswahlSimulieren),
            new(RundeZuruecksetzenAsync, CanRundeZuruecksetzen),
            new(HinrundeZuruecksetzenAsync, CanHinrundeZuruecksetzen),
            new(RueckrundeZuruecksetzenAsync, CanRueckrundeZuruecksetzen),
            new(GruppeZuruecksetzenAsync, CanGruppeZuruecksetzen),
            new(SpieltagZuruecksetzenAsync, CanSpieltagZuruecksetzen),
        ];
    }

    public ICommand RundeSimulierenBefehl => _befehle[0];
    public ICommand HinrundeSimulierenBefehl => _befehle[1];
    public ICommand RueckrundeSimulierenBefehl => _befehle[2];
    public ICommand GruppeSimulierenBefehl => _befehle[3];
    public ICommand SpieltagSimulierenBefehl => _befehle[4];
    public ICommand AuswahlSimulierenBefehl => _befehle[5];
    public ICommand RundeZuruecksetzenBefehl => _befehle[6];
    public ICommand HinrundeZuruecksetzenBefehl => _befehle[7];
    public ICommand RueckrundeZuruecksetzenBefehl => _befehle[8];
    public ICommand GruppeZuruecksetzenBefehl => _befehle[9];
    public ICommand SpieltagZuruecksetzenBefehl => _befehle[10];

    public void BefehleMelden()
    {
        foreach (var befehl in _befehle) befehl.NotifyCanExecuteChanged();
    }

    public static bool IstSimulierbar(SpielZeile s) => s.Status != SpielStatus.Abgeschlossen && !s.IstPlatzhalterSpiel;

    // ── Spielmengen der Ebenen (Runde, Hin-/Rückrunde, Gruppe, Spieltag) ───────
    // null = die Ebene gibt es gerade nicht.

    private IEnumerable<SpielZeile>? RundenSpiele() => _kontext.AusgewaehlteRunde is not null ? _kontext.Spiele : null;

    private IEnumerable<SpielZeile>? RundenHaelfteSpiele(bool istRueckrunde) =>
        _kontext.HatHinUndRueckspiel ? _kontext.Spiele.Where(s => s.IstRueckspiel == istRueckrunde) : null;

    private IEnumerable<SpielZeile>? GruppenSpiele() =>
        _kontext.EffektiveGruppe is { } gruppe ? _kontext.Spiele.Where(s => s.GruppeNummer == gruppe.GruppenNummer) : null;

    private IEnumerable<SpielZeile>? SpieltagSpiele()
    {
        if (_kontext.EffektiverSpieltag is not { } spieltag) return null;
        return _kontext.AusgewaehlteGruppe is { } gruppe
            ? _kontext.Spiele.Where(s => s.GruppeNummer == gruppe.GruppenNummer && s.Spieltag == spieltag)
            : _kontext.Spiele.Where(s => s.Spieltag == spieltag);
    }

    private bool KannSimulieren(IEnumerable<SpielZeile>? spiele) =>
        !_kontext.IstSimulierend && spiele?.Any(IstSimulierbar) == true;

    private bool KannZuruecksetzen(IEnumerable<SpielZeile>? spiele) =>
        !_kontext.IstSimulierend && spiele?.Any(s => s.HatErgebnis) == true;

    // ── Simulation: CanExecute ─────────────────────────────────────────────────

    private bool CanRundeSimulieren() => KannSimulieren(RundenSpiele());
    private bool CanGruppeSimulieren() => KannSimulieren(GruppenSpiele());
    private bool CanSpieltagSimulieren() => KannSimulieren(SpieltagSpiele());
    private bool CanHinrundeSimulieren() => KannSimulieren(RundenHaelfteSpiele(istRueckrunde: false));

    // K.-o.-Rückspiele nur mit abgeschlossenem Hinspiel (SimulationsService überspringt die übrigen).
    private bool CanRueckrundeSimulieren() => KannSimulieren(
        RundenHaelfteSpiele(istRueckrunde: true)?.Where(s => !_kontext.IstKoMitRueckspiel || HinspielAbgeschlossen(s)));

    private bool CanAuswahlSimulieren() =>
        !_kontext.IstSimulierend && _kontext.MehrfachAusgewaehlteSpiele.Count > 1
        && _kontext.MehrfachAusgewaehlteSpiele.Any(IstSimulierbar);

    private bool HinspielAbgeschlossen(SpielZeile rueckspiel) =>
        rueckspiel.HinspielId is int hinId && _kontext.Spiele.Any(h => h.Id == hinId && h.HatErgebnis);

    private bool CanRundeZuruecksetzen() => KannZuruecksetzen(RundenSpiele());
    private bool CanGruppeZuruecksetzen() => KannZuruecksetzen(GruppenSpiele());
    private bool CanSpieltagZuruecksetzen() => KannZuruecksetzen(SpieltagSpiele());
    private bool CanHinrundeZuruecksetzen() => KannZuruecksetzen(RundenHaelfteSpiele(istRueckrunde: false));
    private bool CanRueckrundeZuruecksetzen() => KannZuruecksetzen(RundenHaelfteSpiele(istRueckrunde: true));

    // ── Simulation: Aktionen ───────────────────────────────────────────────────
    // Gruppe und Spieltag gleichen danach weder die Finale-Automatik ab noch ziehen sie den Turnierbaum nach —
    // so seit Issue #14, beim Zusammenführen (#402) unverändert übernommen.

    private async Task RundeSimulierenAsync()
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AusgewaehlteRunde is not { } runde) return;

        await _kontext.SpielmengeBearbeitenAsync(
            $"Spielplan: Runde simulieren (Runde {runde.Id})",
            () => _simulationsService.RundeSimulierenAsync(runde.Id, SIMULATIONS_MODUS),
            AuswahlDanach.Aufheben, finaleAbgleichen: true, baumNachziehen: true);
    }

    private Task HinrundeSimulierenAsync() => RundenHaelfteSimulierenAsync(istRueckrunde: false);
    private Task RueckrundeSimulierenAsync() => RundenHaelfteSimulierenAsync(istRueckrunde: true);

    private async Task RundenHaelfteSimulierenAsync(bool istRueckrunde)
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AusgewaehlteRunde is not { } runde) return;

        await _kontext.SpielmengeBearbeitenAsync(
            $"Spielplan: Rundenhälfte simulieren (Runde {runde.Id})",
            () => _simulationsService.RundenHaelfteSimulierenAsync(runde.Id, istRueckrunde, SIMULATIONS_MODUS),
            AuswahlDanach.Aufheben, finaleAbgleichen: true, baumNachziehen: true);
    }

    private async Task GruppeSimulierenAsync()
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.EffektiveGruppe is not { } gruppe || _kontext.AusgewaehlteRunde is not { } runde) return;
        var teamIds = gruppe.Tabelle.Select(t => t.Mannschaft.Id).ToList();

        await _kontext.SpielmengeBearbeitenAsync(
            $"Spielplan: Gruppe simulieren (Runde {runde.Id})",
            () => _simulationsService.GruppeSimulierenAsync(runde.Id, teamIds, SIMULATIONS_MODUS),
            AuswahlDanach.Wiederherstellen, finaleAbgleichen: false, baumNachziehen: false);
    }

    private async Task SpieltagSimulierenAsync()
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AusgewaehlteRunde is not { } runde || _kontext.EffektiverSpieltag is not { } spieltag) return;
        var teamIds = _kontext.AusgewaehlteGruppe?.Tabelle.Select(t => t.Mannschaft.Id).ToList();

        await _kontext.SpielmengeBearbeitenAsync(
            $"Spielplan: Spieltag simulieren (Runde {runde.Id}, Spieltag {spieltag})",
            () => teamIds is not null
                ? _simulationsService.SpieltagInGruppeSimulierenAsync(runde.Id, spieltag, teamIds, SIMULATIONS_MODUS)
                : _simulationsService.SpieltageSimulierenAsync(runde.Id, spieltag, SIMULATIONS_MODUS),
            AuswahlDanach.Wiederherstellen, finaleAbgleichen: false, baumNachziehen: false);
    }

    private async Task AuswahlSimulierenAsync()
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AusgewaehlteRunde is null) return;
        var spielIds = _kontext.MehrfachAusgewaehlteSpiele.Where(IstSimulierbar).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;

        await _kontext.SpielmengeBearbeitenAsync(
            $"Spielplan: Auswahl simulieren ({spielIds.Count} Spiele)",
            () => _simulationsService.SpieleSimulierenAsync(spielIds, SIMULATIONS_MODUS),
            AuswahlDanach.MitMehrfachauswahlAufheben, finaleAbgleichen: true, baumNachziehen: true);
    }

    // ── Ergebnis-Reset: Aktionen (Issue #123) ────────────────────────────────

    private Task RundeZuruecksetzenAsync() => ZuruecksetzenAsync(
        "Runde", RundenSpiele,
        ids => $"Alle {ids.Count} Ergebnis(se) der Runde '{_kontext.AusgewaehlteRunde?.Name}' zurücksetzen?",
        AuswahlDanach.Aufheben);

    private Task GruppeZuruecksetzenAsync() => ZuruecksetzenAsync(
        "Gruppe", GruppenSpiele,
        ids => $"Alle {ids.Count} Ergebnis(se) der Gruppe {_kontext.EffektiveGruppe?.GruppenNummer} zurücksetzen?",
        AuswahlDanach.Wiederherstellen);

    private Task SpieltagZuruecksetzenAsync() => ZuruecksetzenAsync(
        "Spieltag", SpieltagSpiele,
        ids =>
        {
            var gruppenHinweis = _kontext.AusgewaehlteGruppe is { } gruppe ? $" (Gruppe {gruppe.GruppenNummer})" : "";
            return $"Alle {ids.Count} Ergebnis(se) von Spieltag {_kontext.EffektiverSpieltag}{gruppenHinweis} zurücksetzen?";
        },
        AuswahlDanach.Wiederherstellen);

    /// <summary>Setzt die Ergebnisse der mehrfach ausgewählten Spiele zurück (Ribbon „Löschen“, Issue #124).</summary>
    public Task AuswahlZuruecksetzenAsync() => ZuruecksetzenAsync(
        "Auswahl", () => _kontext.MehrfachAusgewaehlteSpiele,
        ids => $"{ids.Count} Spielergebnis(se) zurücksetzen?",
        AuswahlDanach.MitMehrfachauswahlAufheben);

    private Task HinrundeZuruecksetzenAsync() => RundenHaelfteZuruecksetzenAsync(istRueckrunde: false);
    private Task RueckrundeZuruecksetzenAsync() => RundenHaelfteZuruecksetzenAsync(istRueckrunde: true);

    private Task RundenHaelfteZuruecksetzenAsync(bool istRueckrunde) => ZuruecksetzenAsync(
        "Rundenhälfte", () => RundenHaelfteSpiele(istRueckrunde),
        ids =>
        {
            var bezeichnung = _kontext.IstKoMitRueckspiel
                ? (istRueckrunde ? "Rückspiele" : "Hinspiele")
                : (istRueckrunde ? "Rückrunde" : "Hinrunde");
            // Issue #387: Ohne Hinspiel-Ergebnis ist die Gesamtwertung eines K.-o.-Rückspiels hinfällig — der
            // Ergebnis-Service setzt die zugehörigen Rückspiele mit zurück (Issue #346).
            var rueckspielHinweis = _kontext.IstKoMitRueckspiel && !istRueckrunde
                && _kontext.Spiele.Any(s => s.IstRueckspiel && s.HatErgebnis && s.HinspielId is int h && ids.Contains(h))
                ? "\n\nDie Ergebnisse der zugehörigen Rückspiele werden ebenfalls zurückgesetzt."
                : "";
            return $"Alle {ids.Count} Ergebnis(se) der {bezeichnung} zurücksetzen?{rueckspielHinweis}";
        },
        AuswahlDanach.Aufheben);

    /// <summary>Setzt nach Rückfrage die Ergebnisse einer Ebene zurück. <paramref name="frage"/> bekommt die IDs
    /// der betroffenen Spiele; der Hinweis auf Folgerunden wird angehängt.</summary>
    private async Task ZuruecksetzenAsync(
        string ebene, Func<IEnumerable<SpielZeile>?> spieleDerEbene, Func<IReadOnlyList<int>, string> frage,
        AuswahlDanach auswahlDanach)
    {
        using var schutz = await _kontext.DbContextSchutz.BetretenAsync();
        if (_kontext.AusgewaehlteRunde is null || spieleDerEbene() is not { } spiele) return;
        var spielIds = spiele.Where(s => s.HatErgebnis).Select(s => s.Id).ToList();
        if (spielIds.Count == 0) return;
        var kaskadenHinweis = await _kontext.KaskadenHinweisAsync(spielIds);

        var bestaetigt = await _dialogService.BestaetigenAsync(
            "Ergebnisse zurücksetzen", frage(spielIds) + kaskadenHinweis);
        if (!bestaetigt) return;

        await _kontext.SpielmengeBearbeitenAsync(
            $"Spielplan: {ebene} zurücksetzen ({spielIds.Count} Ergebnisse)",
            () => _spielService.MehrereErgebnisseLoeschenAsync(spielIds),
            auswahlDanach, finaleAbgleichen: false, baumNachziehen: true);
    }
}
