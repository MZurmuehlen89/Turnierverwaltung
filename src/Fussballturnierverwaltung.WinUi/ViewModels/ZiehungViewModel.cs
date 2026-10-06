using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Auslosungen;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>TV-Modus: schrittweise Live-Ziehung einer bereits feststehenden Auslosung (#303, Issue #5) — Banner,
/// Fokus-Ansicht und Gesamtübersicht für Gruppenphase/K.-o.-Runde (#408) sowie Gegner-Matrix der Ligaphase (#364).
/// Reine Anzeige ohne Datenbankzugriff: Auslosung und Spielplan stehen vor dem Start fest, die Ziehung enthüllt nur
/// noch und darf jederzeit enden. Kind-ViewModel von <see cref="AuslosungViewModel"/>.</summary>
public partial class ZiehungViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationLaeuftNicht))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenFokus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenUebersicht))]
    [NotifyCanExecuteChangedFor(nameof(AnimationUeberspringenCommand))]
    private bool _animationLaeuft;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnimationWeiterCommand))]
    private bool _animationWartetAufWeiter;
    [ObservableProperty] private string? _animationBannerMannschaft;
    [ObservableProperty] private string? _animationBannerTopf;
    [ObservableProperty] private string? _animationBannerZiel;
    [ObservableProperty] private string? _animationBannerSlot;
    [ObservableProperty] private string? _animationBannerPosition;
    [ObservableProperty] private ObservableCollection<AnimationsTopf> _animationToepfe = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstGruppenmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeGruppenLeiste))]
    private ObservableCollection<AnimationsGruppe> _animationGruppen = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstPaarungsmodus))]
    private ObservableCollection<AnimationsPaarung> _animationPaarungen = [];

    public bool AnimationIstGruppenmodus  => AnimationGruppen.Count > 0;
    public bool AnimationIstPaarungsmodus => AnimationPaarungen.Count > 0;

    // Ligaphase (#364): eigene Matrix-Instanzen für die Animation (Ergebnis-Matrizen bleiben unberührt).
    // Fokus = nur die Matrix des gerade gezogenen Topfs; Übersicht = alle Matrizen nach Abschluss eines Topfs.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationIstMatrixmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenmodus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenFokus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenUebersicht))]
    private ObservableCollection<GegnerMatrixTopf> _animationMatrizen = [];

    [ObservableProperty] private GegnerMatrixTopf? _animationFokusMatrix;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeFokusMatrix))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenFokus))]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeListenUebersicht))]
    private bool _animationZeigeUebersicht;

    // Gruppenphase/K.-o.-Runde (#408, Vorbild UEFA-Gruppenauslosung bzw. DFB-Pokal): Fokus = aktueller Topf plus
    // Zielgruppe bzw. aktuelle Paarung; Übersicht = alle Gruppen/Paarungen mit dem bisherigen Stand.
    [ObservableProperty] private AnimationsTopf? _animationFokusTopf;
    [ObservableProperty] private AnimationsGruppe? _animationFokusGruppe;
    [ObservableProperty] private AnimationsPaarung? _animationFokusPaarung;

    public bool AnimationZeigeListenFokus      => AnimationZeigeListenmodus && !AnimationZeigeUebersicht;
    public bool AnimationZeigeListenUebersicht => AnimationZeigeListenmodus && AnimationZeigeUebersicht;
    /// <summary>Mini-Leiste aller Gruppen nur bei mehreren Gruppen — eine Liga hat nur eine.</summary>
    public bool AnimationZeigeGruppenLeiste => AnimationGruppen.Count > 1;
    /// <summary>Beschriftung der linken Banner-Pille — während der Übersicht und der Topf-Vorstellung steht dort
    /// kein Mannschaftsname.</summary>
    [ObservableProperty] private string _animationBannerKopf = BANNER_KOPF_MANNSCHAFT;
    private const string BANNER_KOPF_MANNSCHAFT    = "MANNSCHAFT";
    private const string BANNER_KOPF_ZWISCHENSTAND = "ZWISCHENSTAND";
    private const string BANNER_KOPF_TOPF          = "NÄCHSTER TOPF";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationZeigeBannerGegner))]
    private ObservableCollection<AnimationsBannerGegner> _animationBannerGegner = [];

    public bool AnimationZeigeBannerGegner => AnimationBannerGegner.Count > 0;

    public bool AnimationIstMatrixmodus   => AnimationMatrizen.Count > 0;
    public bool AnimationZeigeFokusMatrix => !AnimationZeigeUebersicht;
    /// <summary>Lostöpfe + Gruppen/Paarungen nur außerhalb der Ligaphase — dort zeigt die Matrix den Topf-Inhalt
    /// selbst (abgeblendete Zeilen = noch im Topf).</summary>
    public bool AnimationZeigeListenmodus => AnimationLaeuft && !AnimationIstMatrixmodus;
    private const int PAARUNGEN_JE_UEBERSICHT = 8;

    [ObservableProperty] private string? _animationFokusKopf;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationFortschrittText))]
    private int _animationSchrittIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationFortschrittText))]
    private int _animationSchrittGesamt;

    public bool AnimationLaeuftNicht => !AnimationLaeuft;
    public string AnimationFortschrittText => $"{AnimationSchrittIndex} / {AnimationSchrittGesamt}";

    private bool _animationUeberspringen;
    private TaskCompletionSource _weiterSignal = new();
    private static readonly TimeSpan _tvModusSchrittVerzoegerung = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan _tvModusPausenVerzoegerung  = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _tvModusAufdeckVerzoegerung = TimeSpan.FromMilliseconds(1600);
    private static readonly TimeSpan _tvModusGegnerVerzoegerung  = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan _tvModusUebersichtVerzoegerung = TimeSpan.FromMilliseconds(3000);

    /// <summary>Manuelle Ziehung: jeder Schritt wartet auf "Weiter" statt auf eine Verzögerung.</summary>
    private bool _manuell;

    /// <summary>Beendet eine laufende TV-Ziehung wie "Rest sofort anzeigen" — die Ziehung ist reine Anzeige,
    /// Auslosung und Spielplan stehen schon vorher fest. Die Nutzerwege fragen vorher nach (siehe
    /// <see cref="AuslosungViewModel.ZiehungBeendenNachRueckfrageAsync"/>); die Aufrufe ohne Rückfrage sind das Netz darunter,
    /// damit die Reste einer wartenden Ziehung nie ein danach geöffnetes Detail überlagern (#406). Der Zustand
    /// wird hier sofort zurückgesetzt statt erst im finally der Ziehung, damit das nicht davon abhängt, wann
    /// deren Fortsetzung läuft.</summary>
    public void Beenden()
    {
        if (!AnimationLaeuft) return;
        _animationUeberspringen = true;
        _weiterSignal.TrySetResult();
        AnimationsZustandZuruecksetzen();
    }

    private void AnimationsZustandZuruecksetzen()
    {
        AnimationLaeuft           = false;
        AnimationWartetAufWeiter  = false;
        AnimationBannerKopf       = BANNER_KOPF_MANNSCHAFT;
        BannerLeeren();
        AnimationBannerGegner     = [];
        AnimationToepfe           = [];
        AnimationGruppen          = [];
        AnimationPaarungen        = [];
        AnimationMatrizen         = [];
        AnimationFokusKopf        = null;
        AnimationFokusMatrix      = null;
        AnimationFokusTopf        = null;
        AnimationFokusGruppe      = null;
        AnimationFokusPaarung     = null;
        AnimationZeigeUebersicht  = false;
    }

    [RelayCommand(CanExecute = nameof(AnimationWartetAufWeiter))]
    private void AnimationWeiter() => _weiterSignal.TrySetResult();

    [RelayCommand(CanExecute = nameof(AnimationLaeuft))]
    private void AnimationUeberspringen()
    {
        _animationUeberspringen = true;
        _weiterSignal.TrySetResult();
    }

    // ── Gruppenphase und K.-o.-Runde ──────────────────────────────────────────

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
    public async Task ZiehungAnimierenAsync(
        List<ZiehungsSchritt> schritte, IEnumerable<LostopfViewModel> lostoepfe, bool manuell)
    {
        if (schritte.Count == 0) return;

        ZiehungStarten(schritte.Count, manuell);
        ListenAnzeigeAufbauen(schritte, lostoepfe);

        // Eine Liga hat nur eine Gruppe: sie bleibt durchgehend im Fokus, der Schritt "Ziel" entfällt.
        bool eineGruppe = AnimationGruppen.Count == 1;
        if (eineGruppe) AnimationFokusGruppe = AnimationGruppen[0];

        AnimationsGruppe? zielGruppe = null;
        AnimationsGruppenSlot? neuerSlot = null;
        int fertigePaarungen = 0;

        try
        {
            for (int i = 0; i < schritte.Count; i++)
            {
                if (_animationUeberspringen) break;
                var schritt = schritte[i];
                AnimationSchrittIndex = i + 1;

                VorigenSchrittAbschliessen(zielGruppe, neuerSlot, eineGruppe);
                zielGruppe = null;
                neuerSlot  = null;

                var topf = AnimationToepfe.FirstOrDefault(t => t.TopfId == schritt.TopfId);
                AnimationFokusTopf = topf;

                // 0) Neuer Topf (Gruppenphase): erst den vollen Topf zeigen, dann daraus ziehen
                if (schritt.IstGruppenmodus && (i == 0 || schritte[i - 1].TopfId != schritt.TopfId))
                {
                    TopfVorstellen(schritt, topf);
                    await WarteAufSchrittAsync(_tvModusAufdeckVerzoegerung);
                    AnimationBannerKopf = BANNER_KOPF_MANNSCHAFT;
                    if (_animationUeberspringen) break;
                }

                // 1) Team ziehen — Fokus auf seinen Topf; die Zielgruppe ist noch offen, die Paarung steht
                //    dagegen schon fest (es wird Paarung für Paarung gezogen)
                var paarung = TeamZiehen(schritt, topf);
                await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                if (_animationUeberspringen) break;

                // 2) Ziel — die Gruppe erscheint im Fokus, in der Paarung wird das Team sofort eingetragen
                if (schritt.IstGruppenmodus) zielGruppe = ZielGruppeZeigen(schritt, eineGruppe);
                else TeamInPaarungEintragen(schritt, paarung!);
                if (!eineGruppe)
                {
                    await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                    if (_animationUeberspringen) break;
                }

                // 3) Position — das Team erscheint gleichzeitig auf seinem Platz in der Gruppe
                if (schritt.IstGruppenmodus)
                {
                    neuerSlot = PositionBesetzen(schritt, zielGruppe!);
                    await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                    if (_animationUeberspringen) break;
                }

                BannerLeeren();
                if (i == schritte.Count - 1) break;

                await WarteAufSchrittAsync(_tvModusPausenVerzoegerung);
                if (_animationUeberspringen) break;

                // 4) Gesamtübersicht: nach jedem geleerten Topf bzw. nach je acht fertigen Paarungen (nach dem
                //    letzten Schritt folgt direkt die Ergebnisanzeige)
                bool paarungFertig = !schritt.IstGruppenmodus && schritte[i + 1].ZielNummer != schritt.ZielNummer;
                if (paarungFertig) fertigePaarungen++;
                if (UebersichtTitel(schritt, schritte[i + 1], paarungFertig, fertigePaarungen) is { } titel)
                {
                    await ZwischenstandZeigenAsync(
                        titel, schritt.IstGruppenmodus ? "Zwischenstand aller Gruppen" : "Zwischenstand aller Paarungen");
                    if (_animationUeberspringen) break;
                }
            }
        }
        finally { AnimationsZustandZuruecksetzen(); }
    }

    /// <summary>Töpfe und Ziele (Gruppen bzw. Paarungen) mit der vollen Anzahl noch leerer Plätze.</summary>
    private void ListenAnzeigeAufbauen(List<ZiehungsSchritt> schritte, IEnumerable<LostopfViewModel> lostoepfe)
    {
        AnimationToepfe = new ObservableCollection<AnimationsTopf>(
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
            AnimationGruppen = new ObservableCollection<AnimationsGruppe>(schritteJeZiel.Select(g => new AnimationsGruppe
            {
                GruppenNummer = g.Key,
                GruppenName   = $"Gruppe {GruppenHerkunft.Buchstabe(g.Key)}",
                Kurzname      = GruppenHerkunft.Buchstabe(g.Key),
                Slots         = new ObservableCollection<AnimationsGruppenSlot>(
                    Enumerable.Range(1, g.Count()).Select(p => new AnimationsGruppenSlot { Position = p }))
            }));
            AnimationPaarungen = [];
        }
        else
        {
            AnimationPaarungen = new ObservableCollection<AnimationsPaarung>(schritteJeZiel.Select(g => new AnimationsPaarung
            {
                PaarungsNr = g.Key,
                IstFreilos = g.First().IstFreilos
            }));
            AnimationGruppen = [];
        }
    }

    private void VorigenSchrittAbschliessen(AnimationsGruppe? zielGruppe, AnimationsGruppenSlot? neuerSlot, bool eineGruppe)
    {
        if (zielGruppe is not null) zielGruppe.IstZiel = false;
        if (neuerSlot is not null) neuerSlot.IstNeu = false;
        if (!eineGruppe) AnimationFokusGruppe = null;
    }

    private void TopfVorstellen(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        AnimationBannerKopf       = BANNER_KOPF_TOPF;
        AnimationBannerMannschaft = schritt.TopfName;
        AnimationBannerTopf       = topf is null ? null : $"{topf.Slots.Count} Teams";
    }

    /// <summary>Markiert das Team im Topf als gezogen und zeigt es im Banner. Liefert die Paarung, in die es
    /// kommt — bei der Gruppenphase <c>null</c>.</summary>
    private AnimationsPaarung? TeamZiehen(ZiehungsSchritt schritt, AnimationsTopf? topf)
    {
        var topfSlot = topf?.Slots.FirstOrDefault(s => s.EintragId == schritt.Team.EintragId);
        if (topfSlot is not null) topfSlot.Gezogen = true;

        var paarung = schritt.IstGruppenmodus
            ? null
            : AnimationPaarungen.First(p => p.PaarungsNr == schritt.ZielNummer);
        if (paarung is not null) AnimationFokusPaarung = paarung;

        AnimationBannerMannschaft = schritt.Team.Name;
        AnimationBannerTopf       = schritt.TopfName;
        AnimationBannerZiel       = null;
        AnimationBannerSlot       = null;
        AnimationBannerPosition   = null;
        return paarung;
    }

    private AnimationsGruppe ZielGruppeZeigen(ZiehungsSchritt schritt, bool eineGruppe)
    {
        var zielGruppe = AnimationGruppen.First(g => g.GruppenNummer == schritt.ZielNummer);
        if (!eineGruppe)
        {
            AnimationBannerZiel  = $"Gruppe {GruppenHerkunft.Buchstabe(schritt.ZielNummer)}";
            zielGruppe.IstZiel   = true;
            AnimationFokusGruppe = zielGruppe;
        }
        return zielGruppe;
    }

    private void TeamInPaarungEintragen(ZiehungsSchritt schritt, AnimationsPaarung paarung)
    {
        AnimationBannerZiel = $"Paarung {schritt.ZielNummer}";
        AnimationBannerSlot = schritt.IstFreilos ? "Freilos" : (schritt.IstHeim == true ? "Heim" : "Auswärts");
        if (schritt.IstFreilos || schritt.IstHeim == true)
        {
            paarung.Heim       = schritt.Team.Name;
            paarung.HeimWappen = schritt.Team.Wappen;
        }
        else
        {
            paarung.Auswaerts       = schritt.Team.Name;
            paarung.AuswaertsWappen = schritt.Team.Wappen;
        }
    }

    private AnimationsGruppenSlot PositionBesetzen(ZiehungsSchritt schritt, AnimationsGruppe zielGruppe)
    {
        AnimationBannerPosition = $"Position {schritt.Position}";
        var slot = zielGruppe.Slots[schritt.Position!.Value - 1];
        slot.Anzeige = schritt.Team.Name;
        slot.Wappen  = schritt.Team.Wappen;
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
            ? $"{fertigePaarungen} von {AnimationPaarungen.Count} Paarungen gezogen"
            : null;
    }

    // ── Ligaphase ─────────────────────────────────────────────────────────────

    /// <summary>TV-Modus der Ligaphase (#364, Vorbild UEFA-Live-Show): Topf für Topf wird Team für Team gezogen
    /// (Reihenfolge innerhalb des Topfs nur für die Anzeige zufällig). Pro Team deckt sich seine Zeile in der
    /// Gegner-Matrix auf — gleichzeitig die gespiegelten Zellen bei den Gegnern (Heim gegen X ⇒ bei X "Auswärts"),
    /// sodass später gezogene Teams bereits vorbelegte Zellen haben. Während eines Topfs ist nur dessen Matrix
    /// sichtbar; nach jedem Topf (außer dem letzten) folgt einmal die Übersicht aller Matrizen.</summary>
    public async Task LigaphasenZiehungAnimierenAsync(ObservableCollection<GegnerMatrixTopf> matrizen, bool manuell)
    {
        if (matrizen.Count == 0) return;

        // Zeile je Team-Schlüssel, um die gespiegelte Zelle beim Gegner zu finden
        var zeileJeTeam = matrizen.SelectMany(m => m.Zeilen)
            .Where(z => z.Team.Schluessel is not null)
            .ToDictionary(z => z.Team.Schluessel!);
        MatrizenVerdecken(zeileJeTeam.Values);

        ZiehungStarten(matrizen.Sum(m => m.Zeilen.Count), manuell);
        AnimationMatrizen = matrizen;

        var markiert = new List<ObservableObject>();
        try
        {
            for (int topfIndex = 0; topfIndex < matrizen.Count; topfIndex++)
            {
                var matrix = matrizen[topfIndex];
                AnimationFokusMatrix     = matrix;
                AnimationZeigeUebersicht = false;
                int nochImTopf = matrix.Zeilen.Count;
                AnimationFokusKopf = TopfKopf(matrix, nochImTopf);

                foreach (var zeile in matrix.Zeilen.OrderBy(_ => Random.Shared.Next()).ToList())
                {
                    if (_animationUeberspringen) break;
                    AnimationSchrittIndex++;

                    // 1) Team ziehen
                    nochImTopf--;
                    TeamAusMatrixZiehen(zeile, matrix, nochImTopf, markiert);
                    await WarteAufSchrittAsync(_tvModusSchrittVerzoegerung);
                    if (_animationUeberspringen) break;

                    // 2) Gegner aufdecken — im Banner namentlich nebeneinander, bereits feststehende sofort, neue
                    //    nacheinander (auch im manuellen Modus ohne eigenen Klick); dabei jeweils eigene Zeile +
                    //    Spiegelzelle beim Gegner aufdecken
                    AnimationBannerGegner = BannerGegner(zeile, matrix);
                    foreach (var zelle in AnimationBannerGegner.Where(g => g.IstNeu).Select(g => g.Zelle))
                    {
                        GegnerAufdecken(zelle, zeile, zeileJeTeam, markiert);
                        await Task.WhenAny(Task.Delay(_tvModusGegnerVerzoegerung), _weiterSignal.Task);
                        if (_animationUeberspringen) break;
                    }
                    if (_animationUeberspringen) break;

                    await WarteAufSchrittAsync(_tvModusAufdeckVerzoegerung);
                    if (_animationUeberspringen) break;

                    MarkierungAufheben(markiert);
                    AnimationBannerGegner = [];
                    BannerLeeren();
                    // Im manuellen Modus keine eigene "Weiter"-Pause — zwei Klicks je Team (ziehen, aufdecken) genügen.
                    if (!_manuell)
                    {
                        await WarteAufSchrittAsync(_tvModusPausenVerzoegerung);
                        if (_animationUeberspringen) break;
                    }
                }
                if (_animationUeberspringen) break;

                // 3) Zwischen-Übersicht nach jedem Topf (nach dem letzten folgt direkt die Ergebnisanzeige)
                if (topfIndex < matrizen.Count - 1)
                    await ZwischenstandZeigenAsync($"{matrix.Name} komplett", "Zwischenstand aller Töpfe");
            }
        }
        finally { AnimationsZustandZuruecksetzen(); }
    }

    /// <summary>Ausgangslage der Live-Ziehung: kein Team gezogen, kein Gegner aufgedeckt.</summary>
    private static void MatrizenVerdecken(IEnumerable<GegnerMatrixZeile> zeilen)
    {
        foreach (var zeile in zeilen)
        {
            zeile.IstGezogen = false;
            foreach (var zelle in zeile.Gegner)
                if (zelle.Schluessel is not null) zelle.IstAufgedeckt = false;
        }
    }

    private static string TopfKopf(GegnerMatrixTopf matrix, int nochImTopf) =>
        $"Ziehung {matrix.Name} · noch {nochImTopf} von {matrix.Zeilen.Count} Teams im Topf";

    private void TeamAusMatrixZiehen(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix, int nochImTopf, List<ObservableObject> markiert)
    {
        zeile.IstGezogen        = true;
        zeile.ZeigeGezogenHaken = true;
        AnimationFokusKopf      = TopfKopf(matrix, nochImTopf);
        zeile.IstHervorgehoben  = true;
        markiert.Add(zeile);

        AnimationBannerMannschaft = zeile.Team.Name;
        AnimationBannerTopf       = matrix.Name;
        AnimationBannerZiel       = null;
        AnimationBannerSlot       = null;
    }

    /// <summary>Die Gegner der Zeile fürs Banner — neu ist ein Gegner, dessen Zelle noch verdeckt ist.</summary>
    private static ObservableCollection<AnimationsBannerGegner> BannerGegner(GegnerMatrixZeile zeile, GegnerMatrixTopf matrix) =>
        new(zeile.Gegner
            .Select((zelle, index) => (zelle, index))
            .Where(x => x.zelle.Schluessel is not null)
            .Select(x => new AnimationsBannerGegner(
                $"{matrix.Spalten[x.index].TopfName} · {matrix.Spalten[x.index].Seite}", x.zelle, !x.zelle.IstAufgedeckt)));

    /// <summary>Deckt <paramref name="zelle"/> in der Zeile des gezogenen Teams auf und zugleich die gespiegelte
    /// Zelle in der Zeile des Gegners.</summary>
    private static void GegnerAufdecken(
        GegnerMatrixZelle zelle, GegnerMatrixZeile gezogen,
        Dictionary<string, GegnerMatrixZeile> zeileJeTeam, List<ObservableObject> markiert)
    {
        zelle.IstAufgedeckt    = true;
        zelle.IstHervorgehoben = true;
        markiert.Add(zelle);

        var spiegel = zeileJeTeam.GetValueOrDefault(zelle.Schluessel!)?.Gegner
            .FirstOrDefault(g => g.Schluessel == gezogen.Team.Schluessel);
        if (spiegel is null) return;

        spiegel.IstAufgedeckt    = true;
        spiegel.IstHervorgehoben = true;
        markiert.Add(spiegel);
    }

    private static void MarkierungAufheben(List<ObservableObject> markiert)
    {
        foreach (var o in markiert)
        {
            if (o is GegnerMatrixZelle zelle) zelle.IstHervorgehoben = false;
            else if (o is GegnerMatrixZeile zeile) zeile.IstHervorgehoben = false;
        }
        markiert.Clear();
    }

    // ── Gemeinsamer Ablauf ────────────────────────────────────────────────────

    private void ZiehungStarten(int schrittGesamt, bool manuell)
    {
        _manuell                = manuell;
        _animationUeberspringen = false;
        _weiterSignal           = new TaskCompletionSource();
        AnimationLaeuft         = true;
        AnimationSchrittGesamt  = schrittGesamt;
        AnimationSchrittIndex   = 0;
    }

    private void BannerLeeren()
    {
        AnimationBannerMannschaft = null;
        AnimationBannerTopf       = null;
        AnimationBannerZiel       = null;
        AnimationBannerSlot       = null;
        AnimationBannerPosition   = null;
    }

    /// <summary>Blendet die Gesamtübersicht als eigenen Schritt ein und danach wieder aus.</summary>
    private async Task ZwischenstandZeigenAsync(string titel, string untertitel)
    {
        AnimationZeigeUebersicht  = true;
        AnimationBannerKopf       = BANNER_KOPF_ZWISCHENSTAND;
        AnimationBannerMannschaft = titel;
        AnimationBannerTopf       = untertitel;
        await WarteAufSchrittAsync(_tvModusUebersichtVerzoegerung);
        AnimationBannerKopf       = BANNER_KOPF_MANNSCHAFT;
        AnimationBannerMannschaft = null;
        AnimationBannerTopf       = null;
        AnimationZeigeUebersicht  = false;
    }

    private async Task WarteAufSchrittAsync(TimeSpan verzoegerung)
    {
        if (_animationUeberspringen) return;

        if (_manuell)
        {
            AnimationWartetAufWeiter = true;
            await _weiterSignal.Task;
        }
        else
        {
            await Task.WhenAny(Task.Delay(verzoegerung), _weiterSignal.Task);
        }
        AnimationWartetAufWeiter = false;
        _weiterSignal = new TaskCompletionSource();
    }
}
