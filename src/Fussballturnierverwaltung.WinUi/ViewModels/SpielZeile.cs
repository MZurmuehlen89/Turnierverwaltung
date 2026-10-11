using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Hilfsklasse — 1:1 aus Wpf/ViewModels/SpielplanViewModel.cs portiert ──────────────────────────

public partial class SpielZeile : ObservableObject
{
    public int Id { get; init; }
    public int Spielnummer { get; init; }
    public int? Spieltag { get; init; }
    public string? KoLabel { get; init; }
    public string SpieltagAnzeige => KoLabel ?? (Spieltag.HasValue ? $"ST {Spieltag}" : "-");
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public byte[]? HeimWappen { get; init; }
    public byte[]? AuswaertsWappen { get; init; }
    public SpielStatus Status { get; init; }
    public string StatusAnzeige => Status switch
    {
        SpielStatus.Geplant => "Geplant",
        SpielStatus.Laufend => "Läuft",
        SpielStatus.Abgeschlossen => "Fertig",
        SpielStatus.Verschoben => "Verschoben",
        SpielStatus.Abgebrochen => "Abgebrochen",
        _ => Status.ToString()
    };
    public bool HatErgebnis { get; init; }
    public string ErgebnisText { get; init; } = "-:-";
    public string ErgebnisDetail { get; init; } = "";
    /// <summary>Gesamtergebnis-Zeile unter einem K.-o.-Rückspiel (Issue #340), z.B. "Gesamt 3:3 · i.E. 2:4".</summary>
    public string GesamtText { get; init; } = "";
    public bool HatGesamtText => !string.IsNullOrEmpty(GesamtText);
    public bool IstRueckspiel { get; init; }
    public int? HinspielId { get; init; }
    public bool IstPlatzhalterSpiel { get; init; }
    public bool HeimIstVorschau { get; init; }
    public bool AuswaertsIstVorschau { get; init; }
    public int? GruppeNummer { get; init; }
    /// <summary>Erste Zeile einer Hin-/Rückspiel-Paarung nach der ersten (Issue #340) — bekommt Abstand nach oben,
    /// damit Paarungen wie in der kicker-Paarungsansicht als Block erkennbar sind.</summary>
    public bool IstPaarungsStart { get; init; }
    public Microsoft.UI.Xaml.Thickness PaarungsAbstand => IstPaarungsStart ? new(0, 10, 0, 0) : new(0);
    public string GruppeAnzeige => GruppeNummer.HasValue ? $"Gr.{GruppeNummer}" : "";

    /// <summary>Zwischenüberschrift über der ersten Zeile einer Gruppe in der Spieltag-Ansicht der Gruppenphase
    /// (Issue #363) — hängt von der gerade angezeigten Auswahl ab, deshalb veränderlich statt init.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGruppenKopf))]
    private string? _gruppenKopf;

    public bool HatGruppenKopf => GruppenKopf is not null;
}
