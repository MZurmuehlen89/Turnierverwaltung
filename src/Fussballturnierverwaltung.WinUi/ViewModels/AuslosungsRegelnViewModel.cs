using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Fussballturnierverwaltung.WinUi.Ribbon;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Regel-Schalter der geöffneten Auslosung (#423): Aufbau, Speichern samt gegenseitigem Ausschluss,
/// Sperren je Auslosungsart/Rundenart und die Schalterliste für das Ribbon-Flyout "Auslosungs-Regeln". Nicht an
/// XAML gebunden — das <see cref="AuslosungViewModel"/> baut auf <see cref="Geaendert"/> hin sein Ribbon neu.</summary>
public class AuslosungsRegelnViewModel
{
    private readonly AuslosungService _auslosungService;
    private readonly Func<DbContextReentrancySchutz> _dbContextSchutz;
    private readonly Action<string?> _fehlerMelden;

    private IReadOnlyList<RegelViewModel> _regeln = [];
    private Rundenart? _regelRundenart;
    private bool _regelFolgtAufGruppenphase;

    /// <summary>Schalter oder ihre Sperren haben sich geändert.</summary>
    public event Action? Geaendert;

    /// <param name="dbContextSchutz">Liefert den Schutz des Eltern-ViewModels bei jedem Betreten neu — der wird
    /// dort nach der Konstruktion noch ausgetauscht (#413).</param>
    public AuslosungsRegelnViewModel(
        AuslosungService auslosungService, Func<DbContextReentrancySchutz> dbContextSchutz, Action<string?> fehlerMelden)
    {
        _auslosungService = auslosungService;
        _dbContextSchutz = dbContextSchutz;
        _fehlerMelden = fehlerMelden;
    }

    public void Zeigen(Auslosung detail, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        RegelRundeMerken(detail, instanzRunden);
        _regeln = AuslosungsRegelKompatibilitaet.AngeboteneRegeln.Select(art => RegelSchalterErstellen(detail, art)).ToList();
        SperrenAktualisieren(detail.Auslosungsart);
    }

    /// <summary>Hält fest, was <see cref="SperrenAktualisieren"/> über die Runde wissen muss — auch dann,
    /// wenn später nur die Auslosungsart im Detail wechselt.</summary>
    private void RegelRundeMerken(Auslosung detail, IReadOnlyList<TurnierRunde> instanzRunden)
    {
        var regelRunde = instanzRunden.FirstOrDefault(r => r.Id == detail.TurnierRundeId) ?? detail.TurnierRunde;
        _regelRundenart = regelRunde?.Rundenart;
        _regelFolgtAufGruppenphase = regelRunde is not null && instanzRunden.Any(r =>
            r.Rundenart == Rundenart.Gruppenphase && r.Reihenfolge < regelRunde.Reihenfolge);
    }

    private RegelViewModel RegelSchalterErstellen(Auslosung detail, AuslosungsRegelArt art)
    {
        bool istAktiv = detail.Regeln.FirstOrDefault(r => r.Regelart == art)?.IstAktiv ?? false;
        return new RegelViewModel(
            art, AuslosungsRegelKompatibilitaet.AnzeigeName(art), AuslosungAnzeigeTexte.RegelBeschreibung(art), istAktiv,
            aktiv => RegelSpeichernAsync(detail.Id, art, aktiv));
    }

    /// <summary>Einstiegspunkt des Regel-Schalters — betritt deshalb selbst den Schutz (#413).</summary>
    private async Task RegelSpeichernAsync(int auslosungId, AuslosungsRegelArt art, bool aktiv)
    {
        using var schutz = await _dbContextSchutz().BetretenAsync();
        try
        {
            if (aktiv) await RegelAktivierenAsync(auslosungId, art);
            else await _auslosungService.RegelDeaktivierenAsync(auslosungId, art);
        }
        catch (Exception ex) { _fehlerMelden(ex.Message); }
    }

    private async Task RegelAktivierenAsync(int auslosungId, AuslosungsRegelArt art)
    {
        // Nacheinander statt parallel speichern — ein DbContext verträgt keine zwei gleichzeitigen Operationen.
        foreach (var andere in _regeln.Where(r =>
                     r.IstAktiv && AuslosungsRegelKompatibilitaet.SchliessenSichAus(art, r.Regelart)).ToList())
        {
            andere.OhneSpeichernSetzen(false);
            await _auslosungService.RegelDeaktivierenAsync(auslosungId, andere.Regelart);
        }
        await _auslosungService.RegelHinzufuegenOderAktivierenAsync(auslosungId, art, AuslosungsRegelKompatibilitaet.AnzeigeName(art));
    }

    // Unpassende Regeln bleiben sichtbar, werden aber ausgegraut und begründet (#331) — Quelle ist dieselbe Matrix,
    // nach der auch der AuslosungService entscheidet, welche Regeln er anwendet.
    public void SperrenAktualisieren(Auslosungsart art)
    {
        foreach (var regel in _regeln)
        {
            regel.ImmerAktivHinweis = AuslosungsRegelKompatibilitaet.ImmerAktivGrund(regel.Regelart, art);
            regel.SperrHinweis = AuslosungsRegelKompatibilitaet.SperrGrund(
                regel.Regelart, art, _regelRundenart, _regelFolgtAufGruppenphase);
        }
        Geaendert?.Invoke();
    }

    public IReadOnlyList<RibbonToggleAktion> RibbonSchalter() =>
        _regeln
            .Select(r => new RibbonToggleAktion(
                r.Anzeige, r.ImmerAktivHinweis is not null || r.IstAktiv, aktiv => r.IstAktiv = aktiv,
                r.Beschreibung,
                SperrHinweis: r.ImmerAktivHinweis ?? r.SperrHinweis,
                // Die Heimrecht-Regeln schließen sich aus: Anhaken der einen hakt die andere ab (#331).
                ExklusivGruppe: AuslosungsRegelKompatibilitaet.IstHeimrechtRegel(r.Regelart) ? "Heimrecht" : null,
                AutomationId: $"Befehlsleiste.Regel.{r.Regelart}"))
            .ToList();
}
