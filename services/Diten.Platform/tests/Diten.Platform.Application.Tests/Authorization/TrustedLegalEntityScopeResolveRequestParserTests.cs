using System.Text;
using Diten.Platform.API.Models.AccessGovernance;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class TrustedLegalEntityScopeResolveRequestParserTests
{
    [Fact]
    public async Task Exact_contract_is_accepted()
    {
        var request = Request("{\"module_code\":\"product-item-sku-master\",\"permission_key\":\"mdm.gskus.read\"}");
        var parsed = await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(request, CancellationToken.None);
        Assert.NotNull(parsed.Request); Assert.Null(parsed.Error);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"module_code\":\"Product-item\",\"permission_key\":\"mdm.gskus.read\"}")]
    [InlineData("{\"module_code\":\"product--item\",\"permission_key\":\"mdm.gskus.read\"}")]
    [InlineData("{\"module_code\":\"product-item\",\"permission_key\":\"mdm..read\"}")]
    [InlineData("{\"module_code\":\"product-item\",\"permission_key\":\"MDM.gskus.read\"}")]
    [InlineData("{\"module_code\":\"product-item\",\"permission_key\":\"mdm.gskus.read\",\"extra\":\"x\"}")]
    [InlineData("{\"module_code\":\"product-item\",\"module_code\":\"product-item\",\"permission_key\":\"mdm.gskus.read\"}")]
    public async Task Invalid_strict_contract_is_rejected(string json)
    {
        var parsed = await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(Request(json), CancellationToken.None);
        Assert.Null(parsed.Request); Assert.Equal("LEGAL_ENTITY_SCOPE_CONTRACT_INVALID", parsed.Error);
    }

    [Fact]
    public async Task Reads_at_most_1025_bytes_when_length_is_unknown()
    {
        var stream = new CountingStream(new byte[4096]);
        var request = new DefaultHttpContext().Request;
        request.ContentType = "application/json"; request.Body = stream; request.ContentLength = null;

        var parsed = await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(request, CancellationToken.None);

        Assert.Null(parsed.Request); Assert.Equal(1025, stream.BytesRead);
    }

    [Fact]
    public async Task Query_wrong_content_type_and_declared_overbound_are_rejected()
    {
        var query = Request("{}"); query.QueryString = new QueryString("?tenant=x");
        Assert.Null((await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(query, default)).Request);
        var content = Request("{}"); content.ContentType = "text/plain";
        Assert.Null((await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(content, default)).Request);
        var length = Request("{}"); length.ContentLength = 1025;
        Assert.Null((await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(length, default)).Request);
    }

    private static HttpRequest Request(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var request = new DefaultHttpContext().Request;
        request.ContentType = "application/json"; request.ContentLength = bytes.Length; request.Body = new MemoryStream(bytes);
        return request;
    }

    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int BytesRead { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return Core(buffer, cancellationToken);
        }
        private async ValueTask<int> Core(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var read = await base.ReadAsync(buffer, cancellationToken); BytesRead += read; return read;
        }
    }
}
