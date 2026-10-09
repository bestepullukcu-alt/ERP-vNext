using Xunit;

namespace Diten.MdmService.Application.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProductLegalEntityScopeMongoCollection
{
    public const string Name = "ProductLegalEntityScopeMongo";
    public const string DatabaseName = "diten_mdm_product_scope_itest";
}
