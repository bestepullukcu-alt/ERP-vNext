using Diten.MdmService.Persistence.Configurations;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>WP-MDM-BL576-01 / BL-569: this assembly's initializer ran and registered every document map with the flag.</summary>
public sealed class MdmApiTestSerializersTests
{
    [Fact]
    public void The_module_initializer_registered_every_document_map_before_the_first_test()
    {
        Assert.True(MdmApiTestSerializers.Ran);
        Assert.Null(MdmApiTestSerializers.RegistrationFailure);
        Assert.All(MdmDocumentClassMaps.CoveredTypes, type =>
            Assert.True(BsonClassMap.LookupClassMap(type).IgnoreExtraElements, type.Name));
    }
}
