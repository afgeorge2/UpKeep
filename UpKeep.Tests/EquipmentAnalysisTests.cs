using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UpKeep.Services;
using Xunit;

namespace UpKeep.Tests;

public sealed class EquipmentAnalysisTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("https://example.com/photo.jpg")]
    [InlineData("data:image/jpeg;base64,bm90IGEgcGhvdG8=")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz4=")]
    [InlineData("data:image/jpeg;base64,invalid!")]
    public void RejectsRemoteUrlsAndInvalidImages(string photo) => Assert.False(EquipmentAnalysis.ValidPhoto(photo));

    [Fact]
    public async Task MissingKeyReturnsUnavailableWithoutCallingProvider()
    {
        await using var factory = new UpKeepApiFactory();
        using var client = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "", ["OPENAI_API_KEY"] = "" }))).CreateClient();
        var result = await client.PostAsJsonAsync("/api/equipment/analyze", new { model = "CVPVA2414XMC" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/equipment/analyze", new { model = "", photo = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task AnalysisUsesServerKeyAndRemovesInventedSourcesAndUnsupportedFilterSpec()
    {
        const string source = "https://manufacturer.example/manual.pdf";
        var draft = new EquipmentAnalysisResult("CVPVA2414XMC", "Evaporator coil", "Review the label", "16x20x1", "", "Optional housing photo",
            [new("Check filter", "Check condition", 1, 15, "", "Follow filter instructions", "https://invented.example/manual")],
            [new(source, "Manual"), new("https://invented.example/manual", "Invented")]);
        var handler = new StubHandler(async request =>
        {
            Assert.Equal("test-key", request.Headers.Authorization?.Parameter);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.False(payload.RootElement.GetProperty("store").GetBoolean());
            Assert.Equal("web_search", payload.RootElement.GetProperty("tools")[0].GetProperty("type").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                status = "completed", output = new object[] {
                    new { type = "web_search_call", action = new { sources = new[] { new { url = source } } } },
                    new { type = "message", content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(draft, JsonOptions) } } }
                }
            }) };
        });
        var service = new EquipmentAnalysis(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "test-key" }).Build());
        var result = await service.Analyze(new() { Model = "CVPVA2414XMC" }, CancellationToken.None);
        Assert.Single(result.Sources);
        Assert.Equal(source, result.Sources[0].Url);
        Assert.Equal("", result.Tasks[0].SourceUrl);
        Assert.Equal("", result.FilterSpecification);
        Assert.Equal(1, result.Tasks[0].RepeatEveryMonths);
    }

    [Fact]
    public async Task RefusedOrIncompleteOutputCannotBecomeTasks()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { status = "incomplete", output = Array.Empty<object>() }) }));
        var service = new EquipmentAnalysis(new HttpClient(handler), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<JsonException>(() => service.Analyze(new() { Model = "test" }, CancellationToken.None));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
