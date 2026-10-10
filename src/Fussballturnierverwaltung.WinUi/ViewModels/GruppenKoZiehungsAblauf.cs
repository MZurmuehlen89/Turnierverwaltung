using Fussballturnierverwaltung.Application.Auslosungen;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>TV-Ziehung einer Gruppenphase oder K.-o.-Runde (#408, Vorbild UEFA-Gruppenauslosung bzw. DFB-Pokal):
/// spielt die feststehenden Ziehungsschritte nacheinander auf der Anzeige des <see cref="ZiehungViewModel"/> ab.
/// Pro Schritt erscheint zunächst die gezogene Mannschaft (in der Topf-Anzeige als "gezogen" markiert — die Slots
/// bleiben zahlenmäßig fix, nur ihr Inhalt wechselt zu "…", damit sich das Layout nicht verschiebt, sobald ein Topf
/// leerer wird), dann das Ziel (Gruppe/Paarung), bei der Gruppenphase zusätzlich die Position. Gruppen und Paarungen
/// werden vorab mit der vollen Anzahl leerer Plätze ("…") aufgebaut und danach Platz für Platz befüllt. Gezeigt wird
/// die Fokus-Ansicht (aktueller Topf plus Zielgruppe bzw. aktuelle Paarung); nach jedem geleerten Topf bzw. nach je
/// <see cref="ZiehungsReihenfolge.PAARUNGEN_JE_ZWISCHENSTAND"/> Paarungen folgt als eigener Schritt die Gesamtübersicht. Das geladene
/// Endergebnis bleibt unberührt — es wird erst nach der Ziehung sichtbar, siehe
/// <see cref="AuslosungViewModel.ZeigeErgebnis"/>.</summary>
internal sealed class GruppenKoZiehungsAblauf(
    ZiehungViewModel anzeige, List<ZiehungsSchritt> schritte, IEnumerable<LostopfViewModel> lostoepfe)
{
    /// <summary>Eine Liga hat nur eine Gruppe: sie bleibt durchgehend im Fokus, der Schritt "Ziel" entfällt.</summary>
    private bool _eineGruppe;
    private AnimationsGruppe? _zielGruppe;
    private AnimationsGruppenSlot? _neuerSlot;

    public Task AbspielenAsync(bool manuell) =>
        schritte.Count == 0
            ? Task.CompletedTask
            : anzeige.AbspielenAsync(schritte.Count, manuell, SchritteAbspielenAsync);

    private async Task SchritteAbspielenAsync()
    {
        ListenAnzeigeAufbauen();
        for (int index = 0; index < schritte.Count; index++)
        {
            await SchrittAbspielenAsync(index);
            // Nach dem letzten Schritt folgt direkt die Ergebnisanzeige
            if (index < schritte.Count - 1) await UebergangAbspielenAsync(index);
        }
    }

    // ── Anzeige aufbauen ──────────────────────────────────────────────────────

    /// <summary>Töpfe und Ziele (Gruppen bzw. Paarungen) mit der vollen Anzahl noch leerer Plätze.</summary>
    private void ListenAnzeigeAufbauen()
    {
        anzeige.AnimationToepfe = new ObservableCollection<AnimationsTopf>(
            lostoepfe.OrderBy(l => l.Reihenfolge).Select(TopfAnzeige));

        var schritteJeZiel = schritte.GroupBy(s => s.ZielNummer).OrderBy(g => g.Key);
        anzeige.AnimationGruppen = schritte[0].IstGruppenmodus
            ? new ObservableCollection<AnimationsGruppe>(schritteJeZiel.Select(g => GruppenAnzeige(g.Key, g.Count())))
            : [];
        anzeige.AnimationPaarungen = schritte[0].IstGruppenmodus
            ? []
            : new ObservableCollection<AnimationsPaarung>(
                schritteJeZiel.Select(g => new AnimationsPaarung { PaarungsNr = g.Key, IstFreilos = g.First().IstFreilos }));

        _eineGruppe = anzeige.AnimationGruppen.Count == 1;
        if (_eineGruppe) anzeige.AnimationFokusGruppe = anzeige.AnimationGruppen[0];
    }

    private static AnimationsTopf TopfAnzeige(LostopfViewModel lostopf) => new()
    {
        TopfId = lostopf.Id,
        Name   = lostopf.Name,
        Slots  = new ObservableCollection<AnimationsTopfSlot>(lostopf.Teilnehmer.Select(
            t => new AnimationsTopfSlot { EintragId = t.EintragId, Name = t.Name, Wappen = t.Wappen }))
    };

    private static AnimationsGruppe GruppenAnzeige(int nummer, int plaetze) => new()
    {
        GruppenNummer = nummer,
        GruppenName   = $"Gruppe {GruppenHerkunft.Buchstabe(nummer)}",
        Kurzname      = GruppenHerkunft.Buchstabe(nummer),
        Slots         = new ObservableCollection<AnimationsGruppenSlot>(
            Enumerable.Range(1, plaetze).Select(p => new AnimationsGruppenSlot { Position = p }))
    };

    // ── Ein Schritt ───────────────────────────────────────────────────────────

    private async Task SchrittAbspielenAsync(int index)
    {
        var schritt = schritte[index];
        anzeige.AnimationSchrittIndex = index + 1;
        VorigenSchrittAbschliessen();

        var topf = anzeige.AnimationToepfe.FirstOrDefault(t => t.TopfId == schritt.TopfId);
        anzeige.AnimationFokusTopf = topf;

        if (schritt.IstGruppenmodus) await InGruppeZiehenAsync(schritt, topf, BeginntTopf(index));
        else await InPaarungZiehenAsync(schritt, topf);

        anzeige.BannerLeeren();
    }

    private void VorigenSchrittAbschliessen()
    {
        if (_zielGruppe is not null) _zielGruppe.IstZiel = false;
        if (_neuerSlot is not null) _neuerSlot.IstNeu = false;
        if (!_eineGruppe) anzeige.AnimationFokusGruppe = null;
        _zielGruppe = null;
        _neuerSlot  = null;
    }

    private bool BeginntTopf(int index) => index == 0 || schritte[index - 1].TopfId != schritte[index].TopfId;

    /// <summary>Gruppenphase: Topf vorstellen (nur vor seinem ersten Team), Team ziehen, Zielgruppe zeigen, Position
    /// besetzen — die Zielgruppe ist beim Ziehen noch offen.</summary>
    private async Task InGruppeZiehenAsync(ZiehungsSchritt schritt, AnimationsTopf? topf, bool beginntTopf)
    {
        if (beginntTopf) await TopfVorstellenAsync(schritt, topf);
        await TeamZiehenAsync(schritt, topf);
        await ZielGruppeZeigenAsync(schritt);
        await PositionBesetzenAsync(schritt);
    }

    /// <summary>K.-o.-Runde: die Paarung steht schon beim Ziehen fest (es wird Paarung für Paarung gezogen), das Team
    /// wird nach dem Ziehen sofort eingetragen.</summary>
    private async Task InPaarungZiehenAsync(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        var paarung = anzeige.AnimationPaarungen.First(p => p.PaarungsNr == schritt.ZielNummer);
        anzeige.AnimationFokusPaarung = paarung;

        await TeamZiehenAsync(schritt, topf);
        TeamInPaarungEintragen(schritt, paarung);
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
    }

    /// <summary>Erst den vollen Topf zeigen, dann daraus ziehen.</summary>
    private async Task TopfVorstellenAsync(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        anzeige.BannerTopfZeigen(schritt.TopfName, topf is null ? null : $"{topf.Slots.Count} Teams");
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Aufdecken);
    }

    /// <summary>Markiert das Team im Topf als gezogen und zeigt es im Banner.</summary>
    private async Task TeamZiehenAsync(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        var topfSlot = topf?.Slots.FirstOrDefault(s => s.EintragId == schritt.Eintrag.Id);
        if (topfSlot is not null) topfSlot.Gezogen = true;

        anzeige.BannerMannschaftZeigen(TeilnehmerAnzeige.AusEintrag(schritt.Eintrag).Name, schritt.TopfName);
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
    }

    private async Task ZielGruppeZeigenAsync(ZiehungsSchritt schritt)
    {
        _zielGruppe = anzeige.AnimationGruppen.First(g => g.GruppenNummer == schritt.ZielNummer);
        if (_eineGruppe) return;

        anzeige.AnimationBannerZiel  = _zielGruppe.GruppenName;
        _zielGruppe.IstZiel          = true;
        anzeige.AnimationFokusGruppe = _zielGruppe;
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
    }

    /// <summary>Das Team erscheint gleichzeitig mit der Position auf seinem Platz in der Gruppe.</summary>
    private async Task PositionBesetzenAsync(ZiehungsSchritt schritt)
    {
        anzeige.AnimationBannerPosition = $"Position {schritt.Position}";
        var team = TeilnehmerAnzeige.AusEintrag(schritt.Eintrag);
        _neuerSlot = _zielGruppe!.Slots[schritt.Position!.Value - 1];
        _neuerSlot.Anzeige = team.Name;
        _neuerSlot.Wappen  = team.Wappen;
        _neuerSlot.IstNeu  = true;
        _zielGruppe.FuellstandAktualisieren();
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
    }

    private void TeamInPaarungEintragen(ZiehungsSchritt schritt, AnimationsPaarung paarung)
    {
        bool heim = schritt.IstFreilos || schritt.IstHeim == true;
        anzeige.AnimationBannerZiel = $"Paarung {schritt.ZielNummer}";
        anzeige.AnimationBannerSlot = schritt.IstFreilos ? "Freilos" : (heim ? "Heim" : "Auswärts");

        var team = TeilnehmerAnzeige.AusEintrag(schritt.Eintrag);
        if (heim)
        {
            paarung.Heim       = team.Name;
            paarung.HeimWappen = team.Wappen;
        }
        else
        {
            paarung.Auswaerts       = team.Name;
            paarung.AuswaertsWappen = team.Wappen;
        }
    }

    // ── Zwischen zwei Schritten ───────────────────────────────────────────────

    /// <summary>Kurze Pause; ist nach dem Schritt ein Zwischenstand fällig, zusätzlich die Gesamtübersicht.</summary>
    private async Task UebergangAbspielenAsync(int index)
    {
        await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Pause);

        if (ZiehungsReihenfolge.ZwischenstandNach(schritte, index) is { } titel)
            await anzeige.ZwischenstandZeigenAsync(
                titel, schritte[index].IstGruppenmodus ? "Zwischenstand aller Gruppen" : "Zwischenstand aller Paarungen");
    }
}
