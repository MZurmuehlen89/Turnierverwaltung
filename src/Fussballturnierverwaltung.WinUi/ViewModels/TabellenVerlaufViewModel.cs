using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eintrag der Gruppenleiste im Tabellenverlauf.</summary>
public sealed record VerlaufGruppe(int Nummer)
{
    public string Name => GruppenHerkunft.Buchstabe(Nummer);
}

/// <summary>Bereich „Tabellenverlauf“ im Statistik-Reiter (#480): Runde mit Tabelle wählen, bei einer Gruppenphase
/// die Gruppe; darunter der Verlauf des Tabellenplatzes über die Spieltage und die Tabellen der Runde zu einem
/// wählbaren Stand (<see cref="Tabellen"/>). Lädt erst, wenn der Bereich sichtbar ist (<see cref="Anzeigen"/>).</summary>
public partial class TabellenVerlaufViewModel : ObservableObject
{
    private const string HINWEIS_KEINE_ERGEBNISSE =
        "Noch keine Ergebnisse in dieser Runde. Der Verlauf erscheint nach dem ersten gespielten Spieltag.";

    private readonly TabellenService _tabellenService;
    private bool _veraltet = true;
    private bool _geladen;

    public RundenTabellenViewModel Tabellen { get; }

    /// <summary>Neue Fehlermeldung beim Laden; <c>null</c> räumt die bisherige ab.</summary>
    public event Action<string?>? FehlerGemeldet;

    private DbContextReentrancySchutz _dbContextSchutz = new();

    /// <summary>Gemeinsamer Schutz der Turnierdurchführung (#415) — gilt auch für die Tabellen.</summary>
    public DbContextReentrancySchutz DbContextSchutz
    {
        get => _dbContextSchutz;
        set
        {
            _dbContextSchutz = value;
            Tabellen.DbContextSchutz = value;
        }
    }

    public TabellenVerlaufViewModel(TabellenService tabellenService, RundenTabellenViewModel tabellen)
    {
        _tabellenService = tabellenService;
        Tabellen = tabellen;
        Tabellen.FehlerGemeldet += meldung => FehlerGemeldet?.Invoke(meldung);
        Tabellen.GruppeAngeklickt += GruppeWaehlen;
    }

    // ── Runde ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatRunden))]
    [NotifyPropertyChangedFor(nameof(HatMehrereRunden))]
    private IReadOnlyList<TurnierRunde> _runden = [];

    public bool HatRunden => Runden.Count > 0;
    public bool HatMehrereRunden => Runden.Count > 1;

    private TurnierRunde? _ausgewaehlteRunde;

    // Die ComboBox schreibt beim Austausch ihrer Einträge vorübergehend null zurück — die Auswahl bleibt dann und
    // wird ihr einen Takt später wieder gemeldet (siehe WinUi-CLAUDE.md, #377).
    public TurnierRunde? AusgewaehlteRunde
    {
        get => _ausgewaehlteRunde;
        set
        {
            if (value is null)
            {
                AuswahlSpaeterMelden(nameof(AusgewaehlteRunde));
                return;
            }
            if (!SetProperty(ref _ausgewaehlteRunde, value)) return;
            _veraltet = true;
            Anzeigen();
        }
    }

    // ── Gruppe und Verlauf ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Gruppen))]
    [NotifyPropertyChangedFor(nameof(HatGruppen))]
    [NotifyPropertyChangedFor(nameof(Verlauf))]
    [NotifyPropertyChangedFor(nameof(Hinweis))]
    private IReadOnlyList<TabellenVerlauf> _verlaeufe = [];

    public IReadOnlyList<VerlaufGruppe> Gruppen =>
        [.. Verlaeufe.Where(v => v.GruppenNummer is not null).Select(v => new VerlaufGruppe(v.GruppenNummer!.Value))];

    public bool HatGruppen => Gruppen.Count > 0;

    private VerlaufGruppe? _ausgewaehlteGruppe;

    // Die Gruppenleiste (ListView) schreibt beim Austausch ihrer Einträge vorübergehend null zurück.
    public VerlaufGruppe? AusgewaehlteGruppe
    {
        get => _ausgewaehlteGruppe;
        set
        {
            if (value is null)
            {
                AuswahlSpaeterMelden(nameof(AusgewaehlteGruppe));
                return;
            }
            if (!SetProperty(ref _ausgewaehlteGruppe, value)) return;
            OnPropertyChanged(nameof(Verlauf));
            OnPropertyChanged(nameof(Hinweis));
        }
    }

    /// <summary>Der Verlauf der gewählten Gruppe bzw. der Runde.</summary>
    public TabellenVerlauf? Verlauf =>
        Verlaeufe.FirstOrDefault(v => v.GruppenNummer == AusgewaehlteGruppe?.Nummer) ?? Verlaeufe.FirstOrDefault();

    public string? Hinweis => _geladen && Verlauf is not { HatSpieltage: true } ? HINWEIS_KEINE_ERGEBNISSE : null;

    private void AuswahlSpaeterMelden(string eigenschaft) =>
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => OnPropertyChanged(eigenschaft));

    private void GruppeWaehlen(int gruppenNummer) =>
        AusgewaehlteGruppe = Gruppen.FirstOrDefault(g => g.Nummer == gruppenNummer) ?? AusgewaehlteGruppe;

    // ── Laden ─────────────────────────────────────────────────────────────

    /// <summary>Neues Turnier bzw. neuer Datenstand (ohne Datenbankzugriff): Runden mit Tabelle übernehmen, die
    /// gewählte Runde bleibt, solange es sie noch gibt. Geladen wird beim nächsten <see cref="AnzeigenAsync"/>.</summary>
    public void InstanzSetzen(TurnierInstanz? instanz)
    {
        Runden = instanz is null
            ? []
            : [.. instanz.Runden.Where(r => TabellenZonen.HatTabelle(r.Rundenart)).OrderBy(r => r.Reihenfolge)];
        _ausgewaehlteRunde = Runden.FirstOrDefault(r => r.Id == _ausgewaehlteRunde?.Id) ?? Runden.FirstOrDefault();
        OnPropertyChanged(nameof(AusgewaehlteRunde));
        _veraltet = true;
    }

    /// <summary>Lädt, falls sich seit dem letzten Laden etwas geändert hat. Der Aufrufer hält den
    /// <see cref="DbContextSchutz"/> bereits.</summary>
    public async Task AnzeigenAsync()
    {
        if (!_veraltet) return;
        _veraltet = false;
        await LadenAsync();
    }

    /// <summary>Wie <see cref="AnzeigenAsync"/>, aus einer Nutzeraktion heraus — unter dem
    /// <see cref="DbContextSchutz"/>.</summary>
    public void Anzeigen() =>
        _ = DbContextSchutz.AusfuehrenAsync(AnzeigenAsync, ex => FehlerGemeldet?.Invoke(ex.Message));

    private async Task LadenAsync()
    {
        FehlerGemeldet?.Invoke(null);
        try
        {
            var runde = AusgewaehlteRunde;
            Verlaeufe = runde is null ? [] : await _tabellenService.VerlaeufeBerechnenAsync(runde.Id);
            _ausgewaehlteGruppe = Gruppen.FirstOrDefault(g => g.Nummer == _ausgewaehlteGruppe?.Nummer) ?? Gruppen.FirstOrDefault();
            OnPropertyChanged(nameof(AusgewaehlteGruppe));
            OnPropertyChanged(nameof(Verlauf));
            await Tabellen.FokusRundeSetzenAsync(runde);
        }
        catch (Exception ex)
        {
            FehlerGemeldet?.Invoke(ex.Message);
        }
        finally
        {
            _geladen = true;
            OnPropertyChanged(nameof(Hinweis));
        }
    }
}
