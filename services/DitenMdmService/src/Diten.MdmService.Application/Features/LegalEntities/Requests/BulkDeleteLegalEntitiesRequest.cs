using System;
using System.Collections.Generic;

namespace Diten.MdmService.Application.Features.LegalEntities.Requests;

public sealed record BulkDeleteLegalEntitiesRequest(List<Guid> Ids);
