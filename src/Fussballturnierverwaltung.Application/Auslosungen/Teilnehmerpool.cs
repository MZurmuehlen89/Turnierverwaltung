namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Wie ein Teilnehmerpool die Quoten erfüllt (#576): je Verband und Land der Stand, jeweils dahinter die freien
/// Plätze, dazu die Gesamtzahl und was fehlt. Erst ein erfüllter Pool darf automatisch in die Töpfe.</summary>
public sealed record TeilnehmerpoolStand(
    IReadOnlyList<QuotenStand> Verbandsquoten,
    IReadOnlyList<QuotenStand> Landesquoten,
    QuotenStand Gesamt,
    IReadOnlyList<string> Maengel)
{
    public bool Erfuellt => Maengel.Count == 0;
}

/// <summary>Alles, woraus der Teilnehmerpool einer ersten Runde mit Quoten entsteht (#576): die Quoten, die zugelassenen
/// Mannschaften, die Mannschaftszahl der Runde, die Rangliste der Automatik und die Teams, die vorab in Topf 1 kommen
/// (Titelverteidiger, Gruppenköpfe). Zustandslos — der Pool selbst lebt nicht in der Datenbank, sondern wird aus der
/// Topfbelegung und dem Vorschlag der <see cref="Teilnehmerauswahl"/> aufgebaut.</summary>
public sealed record Teilnehmerpool(
    Kontingentvorgabe Quoten,
    IReadOnlyList<AuswahlKandidat> Kandidaten,
    int Eingang,
    IReadOnlyDictionary<int, int> Rang,
    IReadOnlyList<int> VorabInTopf1)
{
    /// <summary>Der Pool, den das Programm vorschlägt: die schon eingelegten Mannschaften, die vorab für Topf 1 und
    /// dahinter die <see cref="Teilnehmerauswahl"/>. Derselbe Aufruf baut den Pool beim erneuten Öffnen wieder auf.</summary>
    public IReadOnlySet<int> Vorschlagen(IReadOnlyCollection<int> eingelegt)
    {
        var zugelassen = Kandidaten.Select(k => k.MannschaftId).ToHashSet();
        var vorab = VorabInTopf1.Where(zugelassen.Contains);
        var gesetzt = new HashSet<int>([.. eingelegt.Where(zugelassen.Contains), .. vorab]);
        var auswahl = Teilnehmerauswahl.Waehlen(Quoten, Kandidaten, gesetzt, Eingang, Rang);
        return new HashSet<int>([.. gesetzt, .. auswahl.Mannschaften]);
    }

    /// <summary>Wie <paramref name="pool"/> die Quoten erfüllt. Mannschaften, die nicht zugelassen sind, zählen nicht.</summary>
    public TeilnehmerpoolStand Stand(IReadOnlyCollection<int> pool)
    {
        var imPool = Kandidaten.Where(k => pool.Contains(k.MannschaftId)).ToList();
        var verbandsquoten = MitFreienPlaetzen(QuotenBelegung.JeVerband(Quoten.Verbandsquoten, imPool), imPool.Count);
        var landesquoten = MitFreienPlaetzen(QuotenBelegung.JeLand(Quoten.Landesquoten, imPool), imPool.Count);
        var gesamt = new QuotenStand("Gesamt", imPool.Count, Eingang);
        return new TeilnehmerpoolStand(verbandsquoten, landesquoten, gesamt, Maengel([.. verbandsquoten, .. landesquoten], gesamt));
    }

    /// <summary>Platz in der Rangliste der Automatik, sonst <c>null</c>.</summary>
    public int? RangVon(int mannschaftId) => Rang.TryGetValue(mannschaftId, out var rang) ? rang : null;

    private IReadOnlyList<QuotenStand> MitFreienPlaetzen(IReadOnlyList<QuotenStand> staende, int poolGroesse) =>
        staende.Count > 0 && Eingang > 0
            ? [.. staende, QuotenBelegung.FreiePlaetze(staende, poolGroesse, Eingang)]
            : staende;

    /// <summary>Je verletzter Quote eine Zeile; die Gesamtzahl nur, wenn sonst alles stimmt — sonst sagte sie dasselbe
    /// noch einmal.</summary>
    private static IReadOnlyList<string> Maengel(IReadOnlyList<QuotenStand> staende, QuotenStand gesamt)
    {
        var maengel = staende.Where(s => s.Verletzt).Select(Mangel).ToList();
        if (maengel.Count == 0 && gesamt.Anzahl > 0 && gesamt.Aktuell != gesamt.Anzahl)
            maengel.Add(gesamt.Aktuell < gesamt.Anzahl
                ? Fehlen(gesamt.Name, gesamt.Anzahl - gesamt.Aktuell)
                : ZuViel(gesamt.Name, gesamt.Aktuell - gesamt.Anzahl));
        return maengel;
    }

    private static string Mangel(QuotenStand s) => s.IstFreiePlaetze
        ? ZuViel(s.Name, s.Aktuell - s.Anzahl)
        : Fehlen(s.Name, s.Anzahl - s.Aktuell);

    private static string Fehlen(string name, int anzahl) =>
        anzahl == 1 ? $"{name}: 1 Mannschaft fehlt" : $"{name}: {anzahl} Mannschaften fehlen";

    private static string ZuViel(string name, int anzahl) =>
        anzahl == 1 ? $"{name}: 1 Mannschaft zu viel" : $"{name}: {anzahl} Mannschaften zu viel";
}
