using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Microsoft.UI.Text;
using Windows.UI.Text;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public enum StatistikAuswertungsart
{
    Gesamt,
    Heim,
    Auswaerts,
    Form
}

/// <summary>Eintrag der Auswertungsliste im Statistik-Reiter (#327). Eine nicht verfügbare Auswertung bleibt
/// sichtbar und wählbar, ist aber ausgegraut und nennt den Grund.</summary>
public partial class StatistikAuswertung(StatistikAuswertungsart art, string titel, string? grundWennNichtVerfuegbar = null)
    : ObservableObject
{
    public StatistikAuswertungsart Art { get; } = art;
    public string Titel { get; } = titel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Deckkraft))]
    [NotifyPropertyChangedFor(nameof(Hinweis))]
    private bool _istVerfuegbar = true;

    public double Deckkraft => IstVerfuegbar ? 1.0 : 0.45;
    public string? Hinweis => IstVerfuegbar ? null : grundWennNichtVerfuegbar;
}

/// <summary>Eine Rangliste in der Form, in der die Ansicht sie zeichnet: jede Auswertung hat andere Spalten,
/// die Ansicht kennt nur Spaltenköpfe und Zellen fester Breite.</summary>
public record StatistikTabelle(
    IReadOnlyList<StatistikSpalte> Spalten,
    IReadOnlyList<StatistikZeile> Zeilen,
    double FormBreite = 0)
{
    public bool HatZeilen => Zeilen.Count > 0;
}

public record StatistikSpalte(string Titel, string Erlaeuterung, double Breite);

public record StatistikZelle(string Text, double Breite, bool Hervorgehoben = false)
{
    public FontWeight Schriftstaerke => Hervorgehoben ? FontWeights.Bold : FontWeights.Normal;
}

/// <param name="Mannschaft">Für das Wappen; <c>null</c> bei Zeilen, die keine einzelne Mannschaft zeigen.</param>
/// <param name="FormBreite">Feste Breite der Kästchenreihe, damit die Spalten auch bei weniger Spielen fluchten.</param>
public record StatistikZeile(
    int Rang,
    string Name,
    Mannschaft? Mannschaft,
    IReadOnlyList<StatistikZelle> Zellen,
    IReadOnlyList<FormKaestchen> Form,
    double FormBreite);

/// <summary>Ein Spiel der Formreihe. Grün/Rot bleiben für Erfolg/Fehler reserviert — die Ausgänge unterscheiden
/// sich über Buchstabe und Füllung: Sieg kräftig gefüllt, Unentschieden schwach, Niederlage nur umrandet.</summary>
public record FormKaestchen(string Buchstabe, double Fuellung);
