using System.Reflection;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemScopeIntegrationTests
{
    [Theory]
    [InlineData(typeof(GetProductAbbreviationWorkItemsHandler))]
    [InlineData(typeof(DispatchProductAbbreviationWorkItemActionHandler))]
    public void Work_item_handlers_consume_the_single_FU03_guard_without_copying_scope_logic(Type handlerType)
    {
        var field = Assert.Single(
            handlerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            x => x.FieldType.Name == "ProductLegalEntityScopeConsumerGuard");
        Assert.Equal("_scopeGuard", field.Name);
    }
}
