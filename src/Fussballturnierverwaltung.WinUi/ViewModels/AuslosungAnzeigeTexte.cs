using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Anzeigetexte der Auslosung — Namen, Hinweise und Beschreibungen zu Auslosungsart, Status und
/// Regeln. Auch vom Runden-Editor und vom <c>AuslosungsartZuTextConverter</c> genutzt.</summary>
public static class AuslosungAnzeigeTexte
{
    public static string AuslosungsartAnzeigeName(Auslosungsart a) => a switch
    {
        Auslosungsart.Zufall                          => "Zufällig",
        Auslosungsart.NachSetzliste                   => "Nach Setzliste",
        Auslosungsart.NachToepfen                     => "Nach Töpfen",
        Auslosungsart.TopfZuordnung                   => "Topf-Zuordnung (UCL)",
        Auslosungsart.GruppensiegerGegenGruppenzweite => "Gruppensieger vs. Zweite",
        Auslosungsart.Benutzerdefiniert               => "Benutzerdefiniert",
        Auslosungsart.Manuell                         => "Manuell",
        _                                             => a.ToString()
    };

    public static string StatusAnzeigeName(AuslosungsStatus s) => s switch
    {
        AuslosungsStatus.Vorbereitung  => "Vorbereitung",
        AuslosungsStatus.Laufend       => "Laufend",
        AuslosungsStatus.Abgeschlossen => "Abgeschlossen",
        AuslosungsStatus.Abgebrochen   => "Abgebrochen",
        _                              => s.ToString()
    };

    public static string RegelBeschreibung(AuslosungsRegelArt art) => art switch
    {
        AuslosungsRegelArt.KeinGleichesLand =>
            "Teams aus demselben Land dürfen nicht in dieselbe Gruppe oder gegeneinander gelost werden. " +
            "Verhindert z. B. nationale Duelle in der Gruppenphase (typisch bei Nationenturnieren).",
        AuslosungsRegelArt.KeineGleicheGruppe =>
            "Teams, die in einer vorangegangenen Gruppenphase bereits in derselben Gruppe waren, " +
            "dürfen nicht erneut aufeinandertreffen. Typisch für UCL/UEL K.O.-Runden nach der Gruppenphase.",
        AuslosungsRegelArt.HeimrechtNachLos =>
            "Das Heimrecht wird je Paarung ausgelost — bei Hin- und Rückspiel entscheidet das Los, " +
            "wer zuerst zu Hause spielt.",
        AuslosungsRegelArt.HeimrechtNachSetzliste =>
            "Das besser eingestufte Team (höherer Topf, dann bessere Setzposition, dann besserer Gruppenplatz) " +
            "hat Heimrecht — bei Hin- und Rückspiel im Rückspiel. Typisch für UEFA-Wettbewerbe, " +
            "z. B. Gruppensieger im Nations-League-Viertelfinale.",
        AuslosungsRegelArt.FreilosMoeglich =>
            "Bei ungerader Teamanzahl erhält ein zufällig gezogenes Team ein Freilos und zieht " +
            "kampflos in die nächste Runde ein. Ohne diese Regel schlägt die Auslosung fehl, " +
            "wenn die Anzahl ungerade ist.",
        AuslosungsRegelArt.Konfoederationsverteilung =>
            "Die Teams eines Kontinentalverbands werden gleichmäßig auf die Gruppen verteilt: je Gruppe höchstens " +
            "aufgerundet und mindestens abgerundet (Teams des Verbands ÷ Gruppenanzahl). Bei der FIFA-WM ergibt " +
            "das höchstens ein Team je Konföderation und ein bis zwei aus Europa.",
        _ => ""
    };

    public static string AuslosungsartHinweisText(Auslosungsart art) => art switch
    {
        Auslosungsart.Zufall =>
            "Alle Teams werden zufällig gemischt und paarweise zugelost. Passt zu jeder Rundenart außer der TopfLiga. " +
            "Kennt keine Topf-Struktur — auf 1 Lostopf begrenzt.",
        Auslosungsart.NachSetzliste =>
            "Gesetzte Teams (Topf 1) werden gleichmäßig auf Gruppen oder Paare verteilt, " +
            "damit stärkere Teams erst in späteren Runden aufeinandertreffen. " +
            "Typisch für Nationenturniere und nationale Pokal-Auslosungen.",
        Auslosungsart.NachToepfen =>
            "Teams werden manuell auf Töpfe verteilt und dann topfübergreifend zugelost — " +
            "mindestens ein Lostopf ist erforderlich. " +
            "Typisch für UEFA Europa League und internationale Pokal-Wettbewerbe.",
        Auslosungsart.TopfZuordnung =>
            "UCL-Ligaphase (ab 2024/25): Jedes Team erhält aus jedem Topf zwei Gegner — einen zu Hause, " +
            "einen auswärts. Alle Töpfe müssen gleich groß sein (typisch 4 × 9 Teams). " +
            "Ist 'Kein gleiches Land' aktiv, gilt zusätzlich: höchstens 2 Gegner aus demselben Land.",
        Auslosungsart.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden den Zweitplatzierten einer anderen Gruppe zugelost, " +
            "wobei Teams aus derselben Gruppe nicht aufeinandertreffen dürfen. " +
            "Typisch für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale, UEFA-Playoff).",
        Auslosungsart.Benutzerdefiniert =>
            "Teams werden manuell den Losplätzen zugewiesen — keine automatische Verlosung. " +
            "Für besondere Anforderungen oder wenn keine andere Methode passt. " +
            "Kennt keine Topf-Struktur — auf 1 Lostopf begrenzt.",
        _ => ""
    };

    public static string? AuslosungsartWarnungText(Auslosungsart art, Rundenart? rundenart) =>
        (art, rundenart) switch
        {
            (Auslosungsart.NachSetzliste, Rundenart.SchweizerSystem) =>
                "⚠  Im Schweizer System wird die Paarung automatisch anhand der Tabelle berechnet. " +
                "Eine manuelle Setzlisten-Auslosung ist hier in der Regel nicht sinnvoll.",
            (Auslosungsart.NachToepfen, Rundenart.Liga) or
            (Auslosungsart.NachSetzliste, Rundenart.Liga) =>
                "⚠  Liga besteht aus nur einer Gruppe — die Topf-/Setzlisten-Reihenfolge hat hier " +
                "keine Auswirkung, alle Teams landen unabhängig davon in derselben Gruppe.",
            _ => null
        };
}
