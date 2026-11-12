using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turnierbaum;
using Microsoft.UI.Text;
using System.Globalization;
using Windows.UI.Text;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

// ── Hilfsklassen für die Karten des Turnierbaums ──────────────────────────

public class BracketKnotenAnzeige
{
    public int Id { get; init; }
    /// <summary>Zeile im rundenweisen Baum (<see cref="RundenbaumLayoutErgebnis.ZeileJeKnoten"/>) — ordnet die
    /// Karten einer Runde von oben nach unten; die Pixelposition im Fenster ist <see cref="FensterY"/>.</summary>
    public double Zeile { get; init; }
    public double FensterY { get; set; }
    public int RundenIndex { get; init; }
    public int? TurnierRundeId { get; init; }
    public int? LinkerKindKnotenId { get; init; }
    public int? RechterKindKnotenId { get; init; }
    public bool IstFreilos { get; init; }
    public string Label { get; init; } = "";
    public bool HatLabel => !string.IsNullOrEmpty(Label);
    public string HeimName { get; init; } = "";
    public string AuswaertsName { get; init; } = "";
    public byte[]? HeimWappen { get; init; }
    public byte[]? AuswaertsWappen { get; init; }
    /// <summary>Für das Kontextmenü „Alle Spiele des Teams anzeigen“ (#479); <c>null</c> bei offenem Platzhalter.</summary>
    public int? HeimMannschaftId { get; init; }
    public int? AuswaertsMannschaftId { get; init; }
    /// <summary>Die Spiele der Karte für ihr Kontextmenü (#62): bei Hin-/Rückspiel beide, beim Freilos keins.</summary>
    public int? SpielId { get; init; }
    public int? RueckspielId { get; init; }
    public bool SpielHatErgebnis { get; init; }
    public bool RueckspielHatErgebnis { get; init; }
    /// <summary>Mindestens ein Gegner steht noch nicht fest — simulieren lässt sich das Spiel dann nicht.</summary>
    public bool IstPlatzhalterSpiel { get; init; }
    public bool HeimIstSieger { get; init; }
    public bool AuswaertsIstSieger { get; init; }
    public FontWeight HeimSchrift => HeimIstSieger ? FontWeights.Bold : FontWeights.Normal;
    public FontWeight AuswaertsSchrift => AuswaertsIstSieger ? FontWeights.Bold : FontWeights.Normal;
    public bool HatErgebnis { get; init; }
    public string HeimScore { get; init; } = "";
    public string AuswaertsScore { get; init; } = "";
    public string ErgebnisDetail { get; init; } = "";
    /// <summary>Fußzeile bei Hin-/Rückspiel-Paarungen (Issue #340), z.B. "Hin 1:0 · Rück 3:2".</summary>
    public string PaarungsText { get; init; } = "";
    public bool HatPaarungsText => !string.IsNullOrEmpty(PaarungsText);
    public string Datum { get; init; } = "";
    public bool HatDatum => !string.IsNullOrEmpty(Datum);
    /// <summary>Kurzes Entscheidungskürzel für die kompakte Karte der Gesamtübersicht (Issue #238), z.B. "n.V."</summary>
    public string EntscheidungKurz { get; init; } = "";
    // Oberkante der kompakten Karte in der Gesamtübersicht (Issue #238), gesetzt in BerechneGesamtbaum.
    public double GesamtX { get; set; }
    public double GesamtY { get; set; }
    public bool IstInGesamtbaum { get; set; }

    public static BracketKnotenAnzeige Aus(
        TurnierBaumKnoten k, Spiel? rueckspiel, double zeile, int rundenIndex, string label, int? turnierRundeId)
    {
        if (k.Spiel is not { } s)
            return new BracketKnotenAnzeige
            {
                Id = k.Id,
                Zeile = zeile,
                RundenIndex = rundenIndex,
                TurnierRundeId = turnierRundeId,
                LinkerKindKnotenId = k.LinkerKindKnotenId,
                RechterKindKnotenId = k.RechterKindKnotenId,
                IstFreilos = true,
                Label = label,
                HeimName = "–",
                AuswaertsName = "–"
            };

        var ergebnis = KnotenErgebnis.Fuer(s, rueckspiel);
        return new BracketKnotenAnzeige
        {
            Id = k.Id,
            Zeile = zeile,
            RundenIndex = rundenIndex,
            TurnierRundeId = turnierRundeId ?? s.TurnierRundeId,
            LinkerKindKnotenId = k.LinkerKindKnotenId,
            RechterKindKnotenId = k.RechterKindKnotenId,
            IstFreilos = false,
            Label = label,
            HeimName = s.HeimMannschaft?.Name ?? s.HeimPlatzhalterName ?? "—",
            AuswaertsName = s.AuswaertsMannschaft?.Name ?? s.AuswaertsPlatzhalterName ?? "—",
            HeimWappen = MannschaftsIcon.Fuer(s.HeimMannschaft),
            AuswaertsWappen = MannschaftsIcon.Fuer(s.AuswaertsMannschaft),
            HeimMannschaftId = s.HeimMannschaftId,
            AuswaertsMannschaftId = s.AuswaertsMannschaftId,
            SpielId = s.Id,
            RueckspielId = rueckspiel?.Id,
            SpielHatErgebnis = s.Ergebnis is not null,
            RueckspielHatErgebnis = rueckspiel?.Ergebnis is not null,
            IstPlatzhalterSpiel = s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null,
            HatErgebnis = ergebnis.HatErgebnis,
            HeimScore = ergebnis.HeimScore,
            AuswaertsScore = ergebnis.AuswaertsScore,
            ErgebnisDetail = ergebnis.Detail,
            PaarungsText = ergebnis.PaarungsText,
            HeimIstSieger = ergebnis.IstSieger(s.HeimMannschaftId),
            AuswaertsIstSieger = ergebnis.IstSieger(s.AuswaertsMannschaftId),
            Datum = s.Datum?.ToString("ddd, dd.MM.yy", CultureInfo.GetCultureInfo("de-DE")) ?? "",
            EntscheidungKurz = ergebnis.EntscheidungKurz
        };
    }
}

/// <summary>Spaltenkopf bzw. Kartenbeschriftung der Gesamtübersicht (Issue #238), Pixelposition der Oberkante.
/// <paramref name="GehoertZuKarte"/>: Beschriftung direkt über einer Karte der Mitte (Finale, Spiel um Platz 3) —
/// wandert beim vertikalen Strecken mit der Karte, Spaltenköpfe bleiben oben.</summary>
public record GesamtbaumBeschriftungAnzeige(double X, double Y, double Breite, string Name, bool GehoertZuKarte);

/// <summary>Inhalt des Siegerfelds (#527, <c>Views/Siegerfeld</c>): der Turniersieger, solange er offen ist ohne Namen.</summary>
public record SiegerfeldAnzeige(string? SiegerName, byte[]? Wappen)
{
    public bool IstEntschieden => SiegerName is not null;
    public string Untertitel => IstEntschieden ? "Turniersieger" : "Turniersieger noch offen";
    public double PokalDeckkraft => IstEntschieden ? 1.0 : 0.35;

    /// <summary>Der Sieger steht schon auf der Finalkarte (<see cref="KnotenErgebnis"/>, bei Hin- und Rückspiel aus
    /// dem Rückspiel).</summary>
    public static SiegerfeldAnzeige Aus(BracketKnotenAnzeige finale) =>
        finale.HeimIstSieger      ? new SiegerfeldAnzeige(finale.HeimName, finale.HeimWappen)
        : finale.AuswaertsIstSieger ? new SiegerfeldAnzeige(finale.AuswaertsName, finale.AuswaertsWappen)
        : new SiegerfeldAnzeige(null, null);
}
