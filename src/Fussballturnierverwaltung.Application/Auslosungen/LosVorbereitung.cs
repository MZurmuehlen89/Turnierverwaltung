namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Was vor jedem Losen ermittelt wird (<see cref="AuslosungService"/>) und das Losen braucht — maschinell
/// (<see cref="Losverfahren"/>) wie von Hand (<see cref="ManuelleZiehung"/>).</summary>
/// <param name="Regeln">Die wirksamen Regeln der Auslosung.</param>
/// <param name="GruppeJeMannschaft">Gruppe („A“, „B“ …) je Mannschafts-Id aus der vorangegangenen Gruppenphase — nur
/// für „Keine gleiche Gruppe“ nötig, sonst leer.</param>
/// <param name="Gruppenkoepfe">Feste Gruppe (ab 0) je Mannschafts-Id — Gastgeber und Titelverteidiger, siehe
/// <see cref="Auslosungen.Gruppenkoepfe"/>. Wirkt nur in der Gruppenphase.</param>
public sealed record LosVorbereitung(
    RegelKontext Regeln, IReadOnlyDictionary<int, string> GruppeJeMannschaft, IReadOnlyDictionary<int, int> Gruppenkoepfe);
