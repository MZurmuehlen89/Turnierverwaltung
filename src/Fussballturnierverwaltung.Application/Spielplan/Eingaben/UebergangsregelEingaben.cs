namespace Fussballturnierverwaltung.Application.Spielplan.Eingaben;

public record UebergangsregelEingabe(
    int QuellRundeId,
    int ZielRundeId,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis,
    int? AnzahlBeste = null);

/// <summary>Eine Zeile innerhalb eines Übergangsregel-Blocks (siehe <see cref="Fussballturnierverwaltung.Application.Spielplan.UebergangsregelService.BlockSpeichernAsync"/>).
/// <see cref="Id"/> ist <c>null</c> für eine noch nicht gespeicherte Zeile.</summary>
public record UebergangsregelZeileEingabe(
    int? Id,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis,
    int? AnzahlBeste = null);

/// <summary>Übergangsregel für den Vollersatz-Fall (siehe <see cref="Fussballturnierverwaltung.Application.Spielplan.UebergangsregelService.AlleErsetzenAsync"/>,
/// Issue #294-Nachfolger von <c>VorlagenUebergangsRegelEingabe</c>) — referenziert Quell-/Zielrunde
/// über die 1-basierte Listenposition (Reihenfolge) statt über eine reale <c>TurnierRunde</c>-Id, da
/// beim Vollersatz auch neu angelegte Runden ohne bereits bekannte Id mitgemeint sein können.</summary>
public record UebergangsregelPositionsEingabe(
    int QuellReihenfolge,
    int ZielReihenfolge,
    int? GruppeIndex,
    int PlatzierungVon,
    int PlatzierungBis,
    int? AnzahlBeste = null);
