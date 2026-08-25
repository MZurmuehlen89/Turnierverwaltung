using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

public record RanglistenImportZeile(RanglistenRohZeile Quelle, Mannschaft Mannschaft);

public record RanglistenImportVorschau(
    IReadOnlyList<RanglistenImportZeile> Aufgeloest,
    IReadOnlyList<RanglistenRohZeile> Unaufgeloest);
