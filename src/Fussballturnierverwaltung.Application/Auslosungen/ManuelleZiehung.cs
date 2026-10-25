using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Ein Team der Ziehung von Hand: sein Lostopf-Eintrag, der Topf (ab 0) und der Name, unter dem es in
/// Ansagen und Sperrgründen erscheint — die Mannschaft, auch hinter einem Platzhalter (#483).</summary>
public sealed record ZiehungsTeilnehmer(int Index, LostopfEintrag Eintrag, int TopfIndex, string Name);

public sealed record ZiehungsTopf(string Name, IReadOnlyList<int> Teilnehmer);

/// <summary>Was als Nächstes gezogen wird. Die Werte der Optionen: bei <see cref="Team"/> und <see cref="Gegner"/>
/// der Teilnehmer-Index, bei <see cref="Gruppe"/> die Gruppe ab 1, bei <see cref="Position"/> die Position ab 1.</summary>
public enum ZiehungsSchrittArt { Team, Gegner, Gruppe, Position, Fertig }

/// <summary>Eine wählbare oder gesperrte Möglichkeit des aktuellen Schritts; gesperrt mit Grund.</summary>
public sealed record ZiehungsOption(int Wert, string? SperrGrund)
{
    public bool IstFrei => SperrGrund is null;
}

/// <summary>Die Ansage des Moderators: was jetzt dran ist und woraus gewählt werden darf. Beim Positions-Zug nennt
/// <see cref="Gruppe"/> (ab 1) die Gruppe, deren Positionen die Optionen sind.</summary>
public sealed record ZiehungsAnsage(
    ZiehungsSchrittArt Art, string Text, IReadOnlyList<ZiehungsOption> Optionen, int? Gruppe = null)
{
    public static ZiehungsAnsage Fertig { get; } =
        new(ZiehungsSchrittArt.Fertig, "Alle Teams sind gezogen – die Auslosung kann abgeschlossen werden.", []);
}

/// <summary>Eine Gruppe (bei Liga und Schweizer System die eine Teilnehmerliste) mit dem Teilnehmer je Position;
/// <c>null</c> = Position noch frei.</summary>
public sealed record ZiehungsGruppe(int Nr, string Name, IReadOnlyList<int?> Plaetze);

/// <summary>Eine Paarung der K.-o.-Ziehung. Offen, solange der Gegner noch gezogen wird — dann steht das zuerst
/// gezogene Team als <see cref="Heim"/>; danach gilt das Heimrecht nach den Regeln.</summary>
public sealed record ZiehungsPaarung(int Nr, int Heim, int? Auswaerts, bool IstFreilos, bool IstOffen);

/// <summary>Ein Ablauf je Rundenform. Wendet Züge ohne Prüfung an — die übernimmt <see cref="ManuelleZiehung"/>.</summary>
internal interface IZiehungsAblauf
{
    ZiehungsAnsage AnsageErmitteln();
    void Anwenden(int wert);
    int? AktuellesTeam { get; }
    bool? IstLoesbar();
    IReadOnlyList<ZiehungsGruppe> Gruppen { get; }
    IReadOnlyList<ZiehungsPaarung> Paarungen { get; }
    IReadOnlyList<ManuellePaarungEingabe> Ergebnis();
}

/// <summary>Was alle Abläufe teilen: Teilnehmer, ihre Regel-Sicht und die Töpfe.</summary>
internal sealed record ZiehungsKontext(
    IReadOnlyList<ZiehungsTeilnehmer> Teilnehmer, IReadOnlyList<TeilnehmerSlot> Slots,
    IReadOnlyList<ZiehungsTopf> Toepfe, RegelKontext Regeln)
{
    /// <summary>Die Regel, gegen die zwei Teilnehmer zusammen verstoßen, als Anzeigename — oder <c>null</c>.</summary>
    public string? RegelVerstoss(int a, int b) =>
        PaarungsRegeln.VerletzteRegel(
            Regeln.KeinGleichesLand, Regeln.KeineGleicheGruppe,
            new(Slots[a].LandId, Slots[a].Gruppe), new(Slots[b].LandId, Slots[b].Gruppe)) is { } regel
            ? AuslosungsRegelKompatibilitaet.AnzeigeName(regel)
            : null;

    public ManuellePaarungEingabe Gruppenplatz(int gruppenNr, int teilnehmer) =>
        ManuellePaarungEingabe.FuerGruppenplatz(gruppenNr, Teilnehmer[teilnehmer].Eintrag.MannschaftId, Eintragsname(teilnehmer));

    public (int? MannschaftId, string Name) Seite(int teilnehmer) =>
        (Teilnehmer[teilnehmer].Eintrag.MannschaftId, Eintragsname(teilnehmer));

    // Gespeichert wird der Platzhalter, nicht die Mannschaft dahinter (#483: Kaskade und Gruppen-Herkunft hängen an ihm).
    private string Eintragsname(int teilnehmer) =>
        Teilnehmer[teilnehmer].Eintrag.PlatzhalterName ?? Teilnehmer[teilnehmer].Name;
}

/// <summary>
/// Die Ziehung von Hand (#482): das Programm moderiert, der Anwender ist die Losfee — etwa zum Nachstellen einer
/// realen Auslosung. Die Ansage nennt den nächsten Schritt; wählbar ist nur, womit die Auslosung regelkonform
/// vollständig aufgeht (wie die Auslosungssoftware der UEFA), alles andere ist mit Grund gesperrt. Züge lassen sich
/// einzeln zurücknehmen. Zustandsbehaftet, aber ohne Datenbankbezug — gespeichert wird das <see cref="Ergebnis"/>.
/// <list type="bullet">
/// <item>Gruppenphase: Topf für Topf je Gruppe ein Team, Gastgeber und Titelverteidiger stehen vorab fest (#487);
/// die Position folgt aus dem Topf oder wird nach der Gruppe gezogen (<see cref="Gruppenposition"/>).</item>
/// <item>K.-o.: Team, dann Gegner — Topf 1 gegen Topf 2, 3 gegen 4 …, ein Überhang danach untereinander
/// (DFB-Pokal); das zuerst gezogene Team spielt zu Hause, sofern keine Heimrecht-Regel anderes sagt.</item>
/// <item>Liga und Schweizer System: gezogen wird die Reihenfolge (Schlüsselzahl).</item>
/// </list>
/// </summary>
public sealed class ManuelleZiehung
{
    /// <summary>Sperrgrund eines Ziels, nach dem die Auslosung nicht mehr vollständig aufginge.</summary>
    public const string NICHT_LOESBAR = "Danach ginge die Auslosung nicht mehr regelkonform auf.";

    private readonly Func<IZiehungsAblauf> _neuerAblauf;
    private readonly List<int> _zuege = [];
    private IZiehungsAblauf _ablauf;
    private ZiehungsAnsage? _ansage;

    private ManuelleZiehung(
        IReadOnlyList<ZiehungsTeilnehmer> teilnehmer, IReadOnlyList<ZiehungsTopf> toepfe, bool istGruppenmodus,
        Func<IZiehungsAblauf> neuerAblauf)
    {
        Teilnehmer      = teilnehmer;
        Toepfe          = toepfe;
        IstGruppenmodus = istGruppenmodus;
        _neuerAblauf    = neuerAblauf;
        _ablauf         = neuerAblauf();
    }

    public IReadOnlyList<ZiehungsTeilnehmer> Teilnehmer { get; }
    public IReadOnlyList<ZiehungsTopf> Toepfe { get; }

    /// <summary>Gruppen (Gruppenphase, Liga, Schweizer System) statt Paarungen (K.-o.).</summary>
    public bool IstGruppenmodus { get; }

    public IReadOnlyList<ZiehungsGruppe> Gruppen => _ablauf.Gruppen;
    public IReadOnlyList<ZiehungsPaarung> Paarungen => _ablauf.Paarungen;

    /// <summary>Das gezogene Team, dessen Gruppe, Position oder Gegner noch aussteht.</summary>
    public int? AktuellesTeam => _ablauf.AktuellesTeam;
    public ZiehungsAnsage Ansage => _ansage ??= _ablauf.AnsageErmitteln();
    public bool IstFertig => Ansage.Art == ZiehungsSchrittArt.Fertig;
    public bool IstBegonnen => _zuege.Count > 0;

    /// <summary>Wählt die Option <paramref name="wert"/> des aktuellen Schritts.</summary>
    public void Waehlen(int wert)
    {
        var option = Ansage.Optionen.FirstOrDefault(o => o.Wert == wert)
            ?? throw new InvalidOperationException("Diese Wahl gehört nicht zum aktuellen Schritt der Ziehung.");
        if (option.SperrGrund is { } grund) throw new InvalidOperationException(grund);

        _ablauf.Anwenden(wert);
        _zuege.Add(wert);
        _ansage = null;
    }

    /// <summary>Nimmt den letzten Zug zurück — der Ablauf wird ohne ihn neu nachgespielt.</summary>
    public void Zuruecknehmen()
    {
        if (_zuege.Count == 0) return;

        _zuege.RemoveAt(_zuege.Count - 1);
        _ablauf = _neuerAblauf();
        foreach (int zug in _zuege) _ablauf.Anwenden(zug);
        _ansage = null;
    }

    public IReadOnlyList<ManuellePaarungEingabe> Ergebnis() => IstFertig
        ? _ablauf.Ergebnis()
        : throw new InvalidOperationException("Die Ziehung ist noch nicht abgeschlossen.");

    // ── Start ─────────────────────────────────────────────────────────────────

    /// <summary>Beginnt die Ziehung mit den Lostöpfen der Auslosung. Wirft, wenn die Regeln mit diesen Teams nicht
    /// erfüllbar sind oder bei ungerader Teamzahl ohne Freilos ein Team ohne Gegner bliebe.</summary>
    public static ManuelleZiehung Starten(Auslosung auslosung, LosVorbereitung vorbereitung, Qualifikanten qualifikanten)
    {
        var runde   = auslosung.TurnierRunde ?? throw new InvalidOperationException("Die Auslosung hat keine Runde.");
        var kontext = KontextAufbauen(auslosung, vorbereitung.Regeln, vorbereitung.GruppeJeMannschaft, qualifikanten);
        if (kontext.Teilnehmer.Count == 0)
            throw new InvalidOperationException("Die Lostöpfe sind leer — es gibt nichts zu ziehen.");

        Func<IZiehungsAblauf> neuerAblauf = runde.Rundenart switch
        {
            Rundenart.Gruppenphase => () => new GruppenZiehung(
                kontext, runde.GruppenAnzahl ?? throw new InvalidOperationException(
                    "Gruppenphase-Auslosung: Die Runde hat keine Gruppenanzahl definiert."),
                GruppenpositionKompatibilitaet.Wirksam(auslosung.Auslosungsart, auslosung.Gruppenposition),
                vorbereitung.Gruppenkoepfe),
            Rundenart.Liga or Rundenart.SchweizerSystem => () => new ReihenfolgeZiehung(kontext),
            Rundenart.TopfLiga => throw new InvalidOperationException(
                AuslosungsKompatibilitaet.InkompatibilitaetsGrund(runde.Rundenart, Auslosungsart.Manuell)),
            _ => () => new PaarungsZiehung(kontext)
        };

        var ziehung = new ManuelleZiehung(
            kontext.Teilnehmer, kontext.Toepfe, VorbelegungsRaster.HatGruppen(runde.Rundenart), neuerAblauf);
        if (ziehung._ablauf.IstLoesbar() == false)
            throw new InvalidOperationException(
                $"Keine gültige Auslosung möglich. Die Einschränkungen ({vorbereitung.Regeln.EinschraenkungenText}) " +
                "können mit den gewählten Mannschaften nicht erfüllt werden.");
        return ziehung;
    }

    private static ZiehungsKontext KontextAufbauen(
        Auslosung auslosung, RegelKontext regeln, IReadOnlyDictionary<int, string> gruppeJeMannschaft,
        Qualifikanten qualifikanten)
    {
        var teilnehmer = new List<ZiehungsTeilnehmer>();
        var slots      = new List<TeilnehmerSlot>();
        var toepfe     = new List<ZiehungsTopf>();
        foreach (var (lostopf, rang) in auslosung.Lostoepfe.OrderBy(l => l.Reihenfolge).Select((l, i) => (l, i)))
        {
            var indizes = new List<int>();
            foreach (var eintrag in lostopf.Eintraege.OrderBy(e => e.Id))
            {
                indizes.Add(teilnehmer.Count);
                string name = eintrag.Mannschaft?.Name ?? qualifikanten.Fuer(eintrag.PlatzhalterName)?.Name
                              ?? eintrag.PlatzhalterName ?? "?";
                teilnehmer.Add(new ZiehungsTeilnehmer(teilnehmer.Count, eintrag, rang, name));
                slots.Add(TeilnehmerSlot.AusEintrag(eintrag, rang, gruppeJeMannschaft, qualifikanten));
            }
            toepfe.Add(new ZiehungsTopf(lostopf.Name, indizes));
        }
        return new ZiehungsKontext(teilnehmer, slots, toepfe, regeln);
    }
}
