using Fussballturnierverwaltung.Application.Auslosungen;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>TV-Ziehung einer Gruppenphase oder K.-o.-Runde: spielt die feststehenden Ziehungsschritte auf der
/// Anzeige des <see cref="ZiehungViewModel"/> ab (#408).</summary>
internal sealed class GruppenKoZiehungsAblauf(ZiehungViewModel anzeige)
{
    private const int PAARUNGEN_JE_UEBERSICHT = 8;

    /// <summary>Spielt die Ziehungsschritte nacheinander im UI ab: pro Schritt wird zunächst die gezogene
    /// Mannschaft angezeigt (dabei in der Topf-Anzeige als "gezogen" markiert — die Slots bleiben
    /// zahlenmäßig fix, nur ihr Inhalt wechselt zu "…", damit sich das Layout nicht verschiebt, sobald ein
    /// Topf leerer wird), dann das Ziel (Gruppe/Paarung), bei Gruppenphase zusätzlich die Position — je nach
    /// Ziehungsart entweder automatisch mit Verzögerung oder wartend auf einen "Weiter"-Klick.
    /// AnimationGruppen/AnimationPaarungen werden vorab mit der vollen Anzahl leerer Plätze ("…") aufgebaut
    /// (nicht erst bei der ersten Zuweisung sichtbar) und danach Slot für Slot befüllt. Das bereits von
    /// LadeDetailAsync geladene Endergebnis wird während der Animation nicht verändert — es wird einfach
    /// erst nach Animationsende (bzw. bei "Überspringen" sofort) sichtbar, siehe <see cref="AuslosungViewModel.ZeigeErgebnis"/>.
    /// Gezeigt wird die Fokus-Ansicht (aktueller Topf plus Zielgruppe bzw. aktuelle Paarung); nach jedem
    /// geleerten Topf bzw. nach je <see cref="PAARUNGEN_JE_UEBERSICHT"/> Paarungen folgt als eigener Schritt
    /// die Gesamtübersicht (#408).</summary>
    public async Task AbspielenAsync(
        List<ZiehungsSchritt> schritte, IEnumerable<LostopfViewModel> lostoepfe, bool manuell)
    {
        if (schritte.Count == 0) return;

        anzeige.ZiehungStarten(schritte.Count, manuell);
        ListenAnzeigeAufbauen(schritte, lostoepfe);

        // Eine Liga hat nur eine Gruppe: sie bleibt durchgehend im Fokus, der Schritt "Ziel" entfällt.
        bool eineGruppe = anzeige.AnimationGruppen.Count == 1;
        if (eineGruppe) anzeige.AnimationFokusGruppe = anzeige.AnimationGruppen[0];

        AnimationsGruppe? zielGruppe = null;
        AnimationsGruppenSlot? neuerSlot = null;
        int fertigePaarungen = 0;

        try
        {
            for (int i = 0; i < schritte.Count; i++)
            {
                if (anzeige.Ueberspringen) break;
                var schritt = schritte[i];
                anzeige.AnimationSchrittIndex = i + 1;

                VorigenSchrittAbschliessen(zielGruppe, neuerSlot, eineGruppe);
                zielGruppe = null;
                neuerSlot  = null;

                var topf = anzeige.AnimationToepfe.FirstOrDefault(t => t.TopfId == schritt.TopfId);
                anzeige.AnimationFokusTopf = topf;

                // 0) Neuer Topf (Gruppenphase): erst den vollen Topf zeigen, dann daraus ziehen
                if (schritt.IstGruppenmodus && (i == 0 || schritte[i - 1].TopfId != schritt.TopfId))
                {
                    TopfVorstellen(schritt, topf);
                    await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Aufdeck);
                    anzeige.AnimationBannerKopf = ZiehungViewModel.BANNER_KOPF_MANNSCHAFT;
                    if (anzeige.Ueberspringen) break;
                }

                // 1) Team ziehen — Fokus auf seinen Topf; die Zielgruppe ist noch offen, die Paarung steht
                //    dagegen schon fest (es wird Paarung für Paarung gezogen)
                var paarung = TeamZiehen(schritt, topf);
                await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
                if (anzeige.Ueberspringen) break;

                // 2) Ziel — die Gruppe erscheint im Fokus, in der Paarung wird das Team sofort eingetragen
                if (schritt.IstGruppenmodus) zielGruppe = ZielGruppeZeigen(schritt, eineGruppe);
                else TeamInPaarungEintragen(schritt, paarung!);
                if (!eineGruppe)
                {
                    await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
                    if (anzeige.Ueberspringen) break;
                }

                // 3) Position — das Team erscheint gleichzeitig auf seinem Platz in der Gruppe
                if (schritt.IstGruppenmodus)
                {
                    neuerSlot = PositionBesetzen(schritt, zielGruppe!);
                    await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Schritt);
                    if (anzeige.Ueberspringen) break;
                }

                anzeige.BannerLeeren();
                if (i == schritte.Count - 1) break;

                await anzeige.WarteAufSchrittAsync(ZiehungsTempo.Pausen);
                if (anzeige.Ueberspringen) break;

                // 4) Gesamtübersicht: nach jedem geleerten Topf bzw. nach je acht fertigen Paarungen (nach dem
                //    letzten Schritt folgt direkt die Ergebnisanzeige)
                bool paarungFertig = !schritt.IstGruppenmodus && schritte[i + 1].ZielNummer != schritt.ZielNummer;
                if (paarungFertig) fertigePaarungen++;
                if (UebersichtTitel(schritt, schritte[i + 1], paarungFertig, fertigePaarungen) is { } titel)
                {
                    await anzeige.ZwischenstandZeigenAsync(
                        titel, schritt.IstGruppenmodus ? "Zwischenstand aller Gruppen" : "Zwischenstand aller Paarungen");
                    if (anzeige.Ueberspringen) break;
                }
            }
        }
        finally { anzeige.AnimationsZustandZuruecksetzen(); }
    }

    /// <summary>Töpfe und Ziele (Gruppen bzw. Paarungen) mit der vollen Anzahl noch leerer Plätze.</summary>
    private void ListenAnzeigeAufbauen(List<ZiehungsSchritt> schritte, IEnumerable<LostopfViewModel> lostoepfe)
    {
        anzeige.AnimationToepfe = new ObservableCollection<AnimationsTopf>(
            lostoepfe.OrderBy(l => l.Reihenfolge).Select(l => new AnimationsTopf
            {
                TopfId = l.Id,
                Name   = l.Name,
                Slots  = new ObservableCollection<AnimationsTopfSlot>(
                    l.Teilnehmer.Select(t => new AnimationsTopfSlot { EintragId = t.EintragId, Name = t.Name, Wappen = t.Wappen }))
            }));

        var schritteJeZiel = schritte.GroupBy(s => s.ZielNummer).OrderBy(g => g.Key);
        if (schritte[0].IstGruppenmodus)
        {
            anzeige.AnimationGruppen = new ObservableCollection<AnimationsGruppe>(schritteJeZiel.Select(g => new AnimationsGruppe
            {
                GruppenNummer = g.Key,
                GruppenName   = $"Gruppe {GruppenHerkunft.Buchstabe(g.Key)}",
                Kurzname      = GruppenHerkunft.Buchstabe(g.Key),
                Slots         = new ObservableCollection<AnimationsGruppenSlot>(
                    Enumerable.Range(1, g.Count()).Select(p => new AnimationsGruppenSlot { Position = p }))
            }));
            anzeige.AnimationPaarungen = [];
        }
        else
        {
            anzeige.AnimationPaarungen = new ObservableCollection<AnimationsPaarung>(schritteJeZiel.Select(g => new AnimationsPaarung
            {
                PaarungsNr = g.Key,
                IstFreilos = g.First().IstFreilos
            }));
            anzeige.AnimationGruppen = [];
        }
    }

    private void VorigenSchrittAbschliessen(AnimationsGruppe? zielGruppe, AnimationsGruppenSlot? neuerSlot, bool eineGruppe)
    {
        if (zielGruppe is not null) zielGruppe.IstZiel = false;
        if (neuerSlot is not null) neuerSlot.IstNeu = false;
        if (!eineGruppe) anzeige.AnimationFokusGruppe = null;
    }

    private void TopfVorstellen(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        anzeige.AnimationBannerKopf       = ZiehungViewModel.BANNER_KOPF_TOPF;
        anzeige.AnimationBannerMannschaft = schritt.TopfName;
        anzeige.AnimationBannerTopf       = topf is null ? null : $"{topf.Slots.Count} Teams";
    }

    /// <summary>Markiert das Team im Topf als gezogen und zeigt es im Banner. Liefert die Paarung, in die es
    /// kommt — bei der Gruppenphase <c>null</c>.</summary>
    private AnimationsPaarung? TeamZiehen(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        var topfSlot = topf?.Slots.FirstOrDefault(s => s.EintragId == schritt.Eintrag.Id);
        if (topfSlot is not null) topfSlot.Gezogen = true;

        var paarung = schritt.IstGruppenmodus
            ? null
            : anzeige.AnimationPaarungen.First(p => p.PaarungsNr == schritt.ZielNummer);
        if (paarung is not null) anzeige.AnimationFokusPaarung = paarung;

        anzeige.AnimationBannerMannschaft = TeilnehmerAnzeige.AusEintrag(schritt.Eintrag).Name;
        anzeige.AnimationBannerTopf       = schritt.TopfName;
        anzeige.AnimationBannerZiel       = null;
        anzeige.AnimationBannerSlot       = null;
        anzeige.AnimationBannerPosition   = null;
        return paarung;
    }

    private AnimationsGruppe ZielGruppeZeigen(ZiehungsSchritt schritt, bool eineGruppe)
    {
        var zielGruppe = anzeige.AnimationGruppen.First(g => g.GruppenNummer == schritt.ZielNummer);
        if (!eineGruppe)
        {
            anzeige.AnimationBannerZiel  = $"Gruppe {GruppenHerkunft.Buchstabe(schritt.ZielNummer)}";
            zielGruppe.IstZiel   = true;
            anzeige.AnimationFokusGruppe = zielGruppe;
        }
        return zielGruppe;
    }

    private void TeamInPaarungEintragen(ZiehungsSchritt schritt, AnimationsPaarung paarung)
    {
        anzeige.AnimationBannerZiel = $"Paarung {schritt.ZielNummer}";
        anzeige.AnimationBannerSlot = schritt.IstFreilos ? "Freilos" : (schritt.IstHeim == true ? "Heim" : "Auswärts");
        var team = TeilnehmerAnzeige.AusEintrag(schritt.Eintrag);
        if (schritt.IstFreilos || schritt.IstHeim == true)
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

    private AnimationsGruppenSlot PositionBesetzen(ZiehungsSchritt schritt, AnimationsGruppe zielGruppe)
    {
        anzeige.AnimationBannerPosition = $"Position {schritt.Position}";
        var slot = zielGruppe.Slots[schritt.Position!.Value - 1];
        var team = TeilnehmerAnzeige.AusEintrag(schritt.Eintrag);
        slot.Anzeige = team.Name;
        slot.Wappen  = team.Wappen;
        slot.IstNeu  = true;
        zielGruppe.FuellstandAktualisieren();
        return slot;
    }

    /// <summary>Titel der Gesamtübersicht nach <paramref name="schritt"/> — <c>null</c>, wenn keine fällig ist.</summary>
    private string? UebersichtTitel(ZiehungsSchritt schritt, ZiehungsSchritt naechster, bool paarungFertig, int fertigePaarungen)
    {
        if (schritt.IstGruppenmodus)
            return naechster.TopfId != schritt.TopfId ? $"{schritt.TopfName} komplett" : null;

        return paarungFertig && fertigePaarungen % PAARUNGEN_JE_UEBERSICHT == 0
            ? $"{fertigePaarungen} von {anzeige.AnimationPaarungen.Count} Paarungen gezogen"
            : null;
    }
}
