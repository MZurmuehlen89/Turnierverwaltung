using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere.Eingaben;

public record TurnierInstanzVollAktualisierenEingabe(
    string Name,
    string Beschreibung,
    string SaisonOderJahr,
    TurnierKategorie Kategorie,
    int? ZielMannschaftsanzahl,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    TurnierStatus Status,
    int? VerbandId = null,
    int? LandId = null,
    MannschaftsartFilter MannschaftsartFilter = MannschaftsartFilter.Alle,
    int? StandardRegelwerkId = null,
    byte[]? Logo = null,
    int? TitelverteidigerId = null,
    string Variante = "",
    IReadOnlyList<int>? GastgeberIds = null);

public record TurnierInstanzErstellenEingabe(
    string Name,
    string Beschreibung,
    string SaisonOderJahr,
    TurnierKategorie Kategorie,
    int? ZielMannschaftsanzahl,
    DateOnly? Startdatum,
    DateOnly? Enddatum,
    TurnierStatus Status = TurnierStatus.Geplant,
    int? VerbandId = null,
    int? LandId = null,
    MannschaftsartFilter MannschaftsartFilter = MannschaftsartFilter.Alle,
    int? StandardRegelwerkId = null,
    byte[]? Logo = null,
    int? TitelverteidigerId = null,
    string Variante = "",
    IReadOnlyList<int>? GastgeberIds = null);

public record TurnierAktualisierenEingabe(
    string Name,
    string SaisonOderJahr);

public record TurnierRundeAktualisierenEingabe(
    string Name,
    int? RegelwerkId,
    bool AuswaertstorRegelAktiv);

public record TurnierRundeEingabe(
    string Name,
    Rundenart Rundenart,
    int EingangsMannschaftsanzahl,
    int AusgangsMannschaftsanzahl,
    int? RegelwerkId,
    bool MitRueckspiel = false,
    bool AuswaertstorRegelAktiv = false,
    string? KonfigurationJson = null,
    int? GruppenAnzahl = null,
    bool EingangUeberUebergangsregeln = false,
    bool PaarungFestVorgegeben = false,
    Auslosungsart? VorgegebeneAuslosungsart = null,
    string? KombinationstabelleJson = null,
    int? StandardRanglisteId = null,
    bool TitelverteidigerInTopf1 = false,
    bool NeutralerSpielort = false,
    int Ueberraschungsfaktor = 0,
    IReadOnlyList<AuslosungsRegelArt>? VorgegebeneAuslosungsregeln = null,
    IReadOnlyList<TabellenBereichEingabe>? TabellenBereiche = null,
    Gruppenposition VorgegebeneGruppenposition = Gruppenposition.NachTopf,
    IReadOnlyList<int>? GastgeberGruppen = null,
    int? TitelverteidigerGruppe = null)
{
    /// <summary>Lässt weg, was bei dieser Runde nichts bewirkt, im Formular aber stehen geblieben sein kann:
    /// die Spieltag-Konfiguration außerhalb des Schweizer Systems, die Gruppenanzahl außerhalb der Gruppenphase,
    /// Tabellenbereiche bei Runden ohne Tabelle und „Titelverteidiger in Topf 1“ ohne Standard-Rangliste.</summary>
    public TurnierRundeEingabe OhneUnwirksameAngaben() => this with
    {
        KonfigurationJson = Rundenart == Rundenart.SchweizerSystem ? KonfigurationJson : null,
        GruppenAnzahl = Rundenart == Rundenart.Gruppenphase ? GruppenAnzahl : null,
        VorgegebeneGruppenposition = Rundenart == Rundenart.Gruppenphase ? VorgegebeneGruppenposition : Gruppenposition.NachTopf,
        GastgeberGruppen = Rundenart == Rundenart.Gruppenphase ? GastgeberGruppen : null,
        TitelverteidigerGruppe = Rundenart == Rundenart.Gruppenphase ? TitelverteidigerGruppe : null,
        TabellenBereiche = TabellenZonen.HatTabelle(Rundenart) ? TabellenBereiche : null,
        TitelverteidigerInTopf1 = StandardRanglisteId is not null && TitelverteidigerInTopf1
    };
}

public record RegelwerkErstellenEingabe(
    string Name,
    string Beschreibung,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis,
    int? RankingRegelSetId,
    int PunkteFuerSieg,
    int PunkteFuerUnentschieden,
    int PunkteFuerNiederlage,
    IReadOnlyList<VerfahrenArt> EntscheidungsVerfahrenReihenfolge);

public record RegelwerkAktualisierenEingabe(
    string Name,
    string Beschreibung,
    DateOnly? GueltigVon,
    DateOnly? GueltigBis,
    int? RankingRegelSetId,
    int PunkteFuerSieg,
    int PunkteFuerUnentschieden,
    int PunkteFuerNiederlage,
    IReadOnlyList<VerfahrenArt> EntscheidungsVerfahrenReihenfolge);

/// <summary>Ein Tabellenbereich einer Runde im Formular (Issue #420).</summary>
public record TabellenBereichEingabe(int PlatzVon, int PlatzBis, TabellenBereichArt Art, string Bezeichnung);
