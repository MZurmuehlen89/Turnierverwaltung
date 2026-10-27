using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Anzeigetexte der Auslosung — Namen, Hinweise und Beschreibungen zu Auslosungsart, Status und
/// Regeln. Auch vom Runden-Editor und vom <c>AuslosungsartZuTextConverter</c> genutzt.</summary>
public static class AuslosungAnzeigeTexte
{
    /// <summary>ComboBox-Optionen der zur Rundenart passenden Auslosungsarten (siehe <see cref="AuslosungsartVorgabe"/>).</summary>
    public static IReadOnlyList<AuslosungsartOption> AuslosungsartOptionen(Rundenart? rundenart) =>
        AuslosungsartVorgabe.KompatibleArten(rundenart)
            .Select(a => new AuslosungsartOption(AuslosungsartAnzeigeName(a), a))
            .ToList();

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
        AuslosungsRegelArt.HeimrechtFuerUnterklassige =>
            "Das Team der tieferen Spielklasse hat Heimrecht — bei Hin- und Rückspiel im Rückspiel. Die Spielklasse " +
            "kommt aus den Abschnitten der Standard-Rangliste der Runde (Ligapyramide); hat sie eine Profigrenze, " +
            "gilt das nur gegen Teams bis zu dieser Klasse (DFB-Pokal), sonst strikt Klasse gegen Klasse " +
            "(Copa del Rey). Gleiche Klasse oder Team ohne Klasse: Das Los entscheidet.",
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
            "Topf 1 ist die Setzliste, seine Reihenfolge die Setzreihenfolge. In der Gruppenphase führen die Gesetzten " +
            "der Reihe nach Gruppe A, B, C … an (Gastgeber und Titelverteidiger behalten ihre Gruppe), die übrigen " +
            "Töpfe werden gelost. In K.-o.-Runden trifft jeder Gesetzte auf ein Team aus den übrigen Töpfen; gibt es " +
            "mehr Gesetzte als Ungesetzte (oder umgekehrt), werden die übrigen untereinander gelost. " +
            "Typisch für Nationenturniere und nationale Pokal-Auslosungen.",
        Auslosungsart.NachToepfen =>
            "Teams werden manuell auf Töpfe verteilt und dann topfübergreifend zugelost — " +
            "mindestens ein Lostopf ist erforderlich. In K.-o.-Runden wird Topf 1 gegen Topf 2 gelost (3 gegen 4 …), " +
            "bis der kleinere Topf leer ist; die übrigen Teams werden untereinander gelost (wie im DFB-Pokal). " +
            "Typisch für UEFA Europa League und internationale Pokal-Wettbewerbe.",
        Auslosungsart.TopfZuordnung =>
            "Ligaphase der UEFA-Klubwettbewerbe (ab 2024/25): Jedes Team erhält aus jedem Topf zwei Gegner — " +
            "einen zu Hause, einen auswärts (Champions/Europa League, typisch 4 × 9 Teams). Mit „Ein Gegner je " +
            "Topf“ in der Runde nur einen Gegner je Topf; die Töpfe 1/2, 3/4, … bilden Paare mit je einem Heim- " +
            "und einem Auswärtsgegner (Conference League, 6 × 6 Teams). Alle Töpfe müssen gleich groß sein. " +
            "Ist 'Kein gleiches Land' aktiv, gilt zusätzlich: höchstens 2 Gegner aus demselben Land.",
        Auslosungsart.GruppensiegerGegenGruppenzweite =>
            "Gruppensieger werden den Zweitplatzierten einer anderen Gruppe zugelost, " +
            "wobei Teams aus derselben Gruppe nicht aufeinandertreffen dürfen. " +
            "Typisch für K.O.-Runden nach einer Gruppenphase (z. B. WM-Achtelfinale, UEFA-Playoff).",
        Auslosungsart.Benutzerdefiniert =>
            "Teams werden manuell den Losplätzen zugewiesen — keine automatische Verlosung. " +
            "Für besondere Anforderungen oder wenn keine andere Methode passt. " +
            "Kennt keine Topf-Struktur — auf 1 Lostopf begrenzt.",
        Auslosungsart.Manuell =>
            "Ziehung von Hand: Du ziehst Team für Team selbst, das Programm sagt den nächsten Schritt an und lässt " +
            "nur Ziele zu, mit denen die Auslosung regelkonform aufgeht — z. B. zum Nachstellen einer realen " +
            "Auslosung. Das zuerst gezogene Team einer Paarung spielt zu Hause.",
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
