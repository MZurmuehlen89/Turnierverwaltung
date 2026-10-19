using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public record RegelwerkOption(string Anzeige, Regelwerk? Wert);

/// <summary>Ein Vorschlag für Gruppenanzahl/Gruppengröße (Issue #215), abgeleitet aus den Teilern der
/// Eingangsmannschaftsanzahl per <see cref="RundenKonfiguration.GruppenAnzahlVorschlaege"/>.</summary>
public record GruppenAnzahlVorschlag(int GruppenAnzahl, int Gruppengroesse)
{
    public string Anzeige => $"{GruppenAnzahl} × {Gruppengroesse}";
}

/// <summary>Option der "Standard-Rangliste"-ComboBox im Rundenformular (Issue #349), <see cref="Wert"/> = Id der
/// Rangliste oder null (seit #393; vorher Id einer Ranglisten-Quelle). Wie <see cref="VorgegebeneAuslosungsartOption"/> nur über
/// <c>RundenFormularViewModel.StandardRanglisteOptionFuer</c> beziehen (Referenzabgleich der ComboBox).</summary>
public record StandardRanglisteOption(string Anzeige, int? Wert);

/// <summary>Ankreuzbare Auslosungsregel im Rundenformular (Issue #356). Die Liste dieser Optionen bleibt über alle
/// Runden dieselbe — je Runde wechseln nur <see cref="IstAktiv"/> und <see cref="IstVerfuegbar"/> (eine pro Auswahl
/// neu zugewiesene ItemsSource ist ein bekannter WinUI3-Fallstrick).</summary>
public partial class RegelVorgabeOption(AuslosungsRegelArt regelart, string anzeige, string beschreibung) : ObservableObject
{
    public AuslosungsRegelArt Regelart { get; } = regelart;
    public string Anzeige { get; } = anzeige;
    public string Beschreibung { get; } = beschreibung;

    [ObservableProperty] private bool _istAktiv;

    /// <summary>Ob die Regel bei Rundenart und vorgegebener Auslosungsart dieser Runde überhaupt wirken kann —
    /// sonst wird sie gar nicht erst angeboten.</summary>
    [ObservableProperty] private bool _istVerfuegbar;
}

/// <summary>Option der "Auslosungsart (Vorgabe)"-ComboBox im Rundenformular (Issue #347), <see cref="Wert"/> = null
/// steht für "frei wählbar". Instanzen nur über <c>RundenFormularViewModel.AuslosungsartVorgabeOptionFuer</c> beziehen —
/// die ComboBox vergleicht <c>SelectedItem</c> per Referenz, Record-Gleichheit hilft dort nicht.</summary>
public record VorgegebeneAuslosungsartOption(string Anzeige, Auslosungsart? Wert);
