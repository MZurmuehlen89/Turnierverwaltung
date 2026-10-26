using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>
/// Zentrale Matrix Auslosungsregel × Auslosungsart × Rundenart (Issue #331), analog zu
/// <see cref="AuslosungsKompatibilitaet"/> für Auslosungsart × Rundenart. Eine Quelle für beides: der
/// <see cref="AuslosungService"/> wendet nur Regeln an, die hier als wirksam gelten, und die Oberfläche graut
/// unpassende Regeln mit genau diesem Grund aus — statt sie wirkungslos anklickbar zu lassen.
/// </summary>
public static class AuslosungsRegelKompatibilitaet
{
    /// <summary>Die in der Oberfläche angebotenen Regeln. <c>GruppensiegerGegenGruppenzweite</c>,
    /// <c>GesetzteGegenUngesetzte</c> und <c>ManuelleEinschraenkung</c> sind mit #331 entfallen — sie hatten
    /// neben den gleichnamigen Auslosungsarten keinen eigenen fachlichen Sinn.</summary>
    public static IReadOnlyList<AuslosungsRegelArt> AngeboteneRegeln { get; } =
    [
        AuslosungsRegelArt.KeinGleichesLand,
        AuslosungsRegelArt.KeineGleicheGruppe,
        AuslosungsRegelArt.HeimrechtNachLos,
        AuslosungsRegelArt.HeimrechtNachSetzliste,
        AuslosungsRegelArt.HeimrechtFuerUnterklassige,
        AuslosungsRegelArt.FreilosMoeglich,
        AuslosungsRegelArt.Konfoederationsverteilung,
    ];

    public static string AnzeigeName(AuslosungsRegelArt regel) => regel switch
    {
        AuslosungsRegelArt.KeinGleichesLand          => "Kein gleiches Land",
        AuslosungsRegelArt.KeineGleicheGruppe        => "Keine gleiche Gruppe",
        AuslosungsRegelArt.HeimrechtNachLos          => "Heimrecht nach Los",
        AuslosungsRegelArt.HeimrechtNachSetzliste    => "Heimrecht nach Setzliste",
        AuslosungsRegelArt.HeimrechtFuerUnterklassige => "Heimrecht für Unterklassige",
        AuslosungsRegelArt.FreilosMoeglich           => "Freilos möglich",
        AuslosungsRegelArt.Konfoederationsverteilung => "Konföderationsverteilung",
        _                                            => regel.ToString()
    };

    /// <summary>Ob eine Vorlage die Regel für eine Runde vorgeben kann (Issue #356): bei der vorgegebenen
    /// Auslosungsart bzw. — ohne Vorgabe — bei mindestens einer zur Rundenart passenden Auslosungsart muss sie
    /// wirken können. Manuell zählt nicht mit (das regelt „Paarung fest vorgegeben“), ebenso wenig eine
    /// Auslosungsart, bei der die Regel ohnehin immer gilt.
    /// <paramref name="paarungFestVorgegeben"/>: Eine solche Runde wird nicht ausgelost, ihre Paarungen zwischen
    /// Platzhaltern stehen in der Vorbelegung fest — dort wirkt keine Regel, also gibt es nichts vorzugeben (#463).</summary>
    public static bool AlsVorgabeMoeglich(
        AuslosungsRegelArt regel, Rundenart rundenart, Auslosungsart? vorgegebeneArt, bool folgtAufGruppenphase,
        bool paarungFestVorgegeben)
    {
        if (paarungFestVorgegeben) return false;

        IEnumerable<Auslosungsart> arten = vorgegebeneArt is { } vorgabe
            ? [vorgabe]
            : Enum.GetValues<Auslosungsart>()
                .Where(a => a != Auslosungsart.Manuell && AuslosungsKompatibilitaet.IstKompatibel(rundenart, a));
        return arten.Any(art => ImmerAktivGrund(regel, art) is null &&
                                SperrGrund(regel, art, rundenart, folgtAufGruppenphase) is null);
    }

    /// <summary>Regeln, die sich gegenseitig ausschließen — beide legen das Heimrecht fest.</summary>
    public static bool SchliessenSichAus(AuslosungsRegelArt a, AuslosungsRegelArt b) =>
        a != b && IstHeimrechtRegel(a) && IstHeimrechtRegel(b);

    /// <summary>Ob die Regel das Heimrecht festlegt — von diesen Regeln wirkt höchstens eine.</summary>
    public static bool IstHeimrechtRegel(AuslosungsRegelArt art) =>
        art is AuslosungsRegelArt.HeimrechtNachLos or AuslosungsRegelArt.HeimrechtNachSetzliste
            or AuslosungsRegelArt.HeimrechtFuerUnterklassige;

    /// <summary>Liefert den Grund, warum die Regel bei der Auslosungsart <paramref name="art"/> in einer Runde der
    /// Rundenart <paramref name="rundenart"/> keine Wirkung hätte, oder <c>null</c>, wenn sie wirkt.
    /// <paramref name="folgtAufGruppenphase"/>: ob vor dieser Runde eine Gruppenphase liegt — nur dann gibt es
    /// frühere Gruppen, die „Keine gleiche Gruppe“ auswerten könnte.</summary>
    public static string? SperrGrund(
        AuslosungsRegelArt regel, Auslosungsart art, Rundenart? rundenart, bool folgtAufGruppenphase) =>
        RundenartSperrGrund(rundenart) ?? (ImmerAktivGrund(regel, art) is not null ? null : regel switch
        {
            AuslosungsRegelArt.KeinGleichesLand          => null,
            AuslosungsRegelArt.KeineGleicheGruppe        => GleicheGruppeSperrGrund(folgtAufGruppenphase),
            AuslosungsRegelArt.HeimrechtNachLos or
            AuslosungsRegelArt.HeimrechtNachSetzliste or
            AuslosungsRegelArt.HeimrechtFuerUnterklassige => HeimrechtSperrGrund(regel, art, rundenart),
            AuslosungsRegelArt.FreilosMoeglich           => FreilosSperrGrund(art, rundenart),
            AuslosungsRegelArt.Konfoederationsverteilung => KonfoederationSperrGrund(rundenart),
            _                                            => "Diese Regel wird nicht mehr unterstützt."
        });

    // Liga und Schweizer System sperren jede Regel — auch eine, die bei der Auslosungsart sonst immer gilt.
    private static string? RundenartSperrGrund(Rundenart? rundenart) => rundenart switch
    {
        Rundenart.Liga =>
            "In einer Liga spielt jeder gegen jeden — es gibt weder Gruppen noch ausgeloste Paarungen, " +
            "auf die diese Regel wirken könnte.",
        Rundenart.SchweizerSystem =>
            "Beim Schweizer System bildet der Spielplan-Generator die Paarungen selbst — die Auslosung legt " +
            "nur die Startreihenfolge fest.",
        _ => null
    };

    private static string? GleicheGruppeSperrGrund(bool folgtAufGruppenphase) => folgtAufGruppenphase
        ? null
        : "Diese Runde folgt auf keine Gruppenphase — es gibt keine früheren Gruppen, deren Teams sich erneut " +
          "begegnen könnten.";

    private static string? HeimrechtSperrGrund(AuslosungsRegelArt regel, Auslosungsart art, Rundenart? rundenart)
    {
        if (rundenart == Rundenart.Gruppenphase)
            return "In der Gruppenphase werden Teams nur Gruppen zugeordnet — das Heimrecht ergibt sich aus dem " +
                   "Spielplan der Gruppe.";
        if (art == Auslosungsart.TopfZuordnung)
            return "Die Topf-Zuordnung verteilt Heim- und Auswärtsspiele selbst (je Topf bzw. Topf-Paar ein Heim- " +
                   "und ein Auswärtsgegner).";
        // Bei der Ziehung von Hand spielt das zuerst gezogene Team zu Hause (DFB-Pokal, #482); die beiden anderen
        // Heimrecht-Regeln gehen dem vor.
        if (art == Auslosungsart.Manuell && regel == AuslosungsRegelArt.HeimrechtNachLos)
            return "Bei der Ziehung von Hand ist die Ziehung selbst das Los — das zuerst gezogene Team spielt zu Hause.";
        return null;
    }

    // „Nach Töpfen“ und „Nach Setzliste“ losen den Überhang ungleich großer Töpfe bzw. Seiten untereinander, bei
    // ungerader Gesamtzahl bleibt dort ein Team für das Freilos (#495).
    private static string? FreilosSperrGrund(Auslosungsart art, Rundenart? rundenart)
    {
        if (rundenart == Rundenart.Gruppenphase)
            return "In der Gruppenphase gibt es kein Freilos — alle Teams werden auf die Gruppen verteilt.";
        if (art == Auslosungsart.TopfZuordnung)
            return "Die Topf-Zuordnung erfordert gleich große Töpfe — ein Freilos ist nicht vorgesehen.";
        return null;
    }

    private static string? KonfoederationSperrGrund(Rundenart? rundenart) => rundenart == Rundenart.Gruppenphase
        ? null
        : "Die Konföderationsverteilung regelt die Verteilung auf Gruppen — in dieser Runde werden keine Gruppen " +
          "ausgelost.";

    /// <summary>Liefert den Grund, warum die Regel bei dieser Auslosungsart unabhängig vom Häkchen immer gilt,
    /// oder <c>null</c>. Beispiel: bei „Gruppensieger vs. Zweite“ trifft ein Gruppensieger real nie auf den
    /// Zweiten der eigenen Gruppe (u. a. Nations-League-Viertelfinale).</summary>
    public static string? ImmerAktivGrund(AuslosungsRegelArt regel, Auslosungsart art) => (regel, art) switch
    {
        (AuslosungsRegelArt.KeineGleicheGruppe, Auslosungsart.GruppensiegerGegenGruppenzweite) =>
            "Bei 'Gruppensieger vs. Zweite' immer aktiv — ein Gruppensieger trifft nie auf den Zweiten " +
            "der eigenen Gruppe.",
        _ => null
    };

    /// <summary>Ob die Regel bei der Auslosung tatsächlich angewendet wird: aktiv gesetzt und nicht gesperrt,
    /// oder durch die Auslosungsart ohnehin vorgegeben.</summary>
    public static bool Wirkt(
        AuslosungsRegelArt regel, bool istAktiv, Auslosungsart art, Rundenart? rundenart, bool folgtAufGruppenphase)
    {
        if (rundenart is Rundenart.Liga or Rundenart.SchweizerSystem) return false;
        if (ImmerAktivGrund(regel, art) is not null) return true;
        return istAktiv && SperrGrund(regel, art, rundenart, folgtAufGruppenphase) is null;
    }

    /// <summary>Ob die Regel bei dieser Auslosung angewendet wird — die eine Entscheidung für Losverfahren
    /// und Prüfung vor dem Losen (#443; die Vorbelegung prüft seit #463 keine Regeln mehr). Die Rundenart kommt vom Aufrufer, weil nicht jeder
    /// die Runde an der Auslosung mitgeladen hat.</summary>
    // folgtAufGruppenphase: true — ohne vorangegangene Gruppenphase fehlt ohnehin jede Gruppen-Herkunft,
    // „Keine gleiche Gruppe“ läuft dann ins Leere statt die Auslosung zu stören.
    public static bool Wirkt(AuslosungsRegelArt regel, Auslosung auslosung, Rundenart? rundenart) => Wirkt(
        regel, auslosung.Regeln.Any(r => r.IstAktiv && r.Regelart == regel), auslosung.Auslosungsart, rundenart,
        folgtAufGruppenphase: true);
}
