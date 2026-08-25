using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;
using Fussballturnierverwaltung.Infrastructure.Persistenz;

namespace Fussballturnierverwaltung.Infrastructure.Repositories;

public class RanglistenQuelleRepository(TurnierverwaltungDbContext context)
    : GenericRepository<RanglistenQuelle>(context), IRanglistenQuelleRepository;
