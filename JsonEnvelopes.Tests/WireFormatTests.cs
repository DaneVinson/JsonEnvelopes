namespace JsonEnvelopes.Tests;

/// <summary>
/// Golden tests that pin the exact serialized bytes. The JSON is a public contract: deployed
/// consumers parse it, and no other test would catch a change to its shape.
/// </summary>
public class WireFormatTests
{
    private static JsonSerializerOptions NewOptions(JsonNamingPolicy? namingPolicy = null)
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = namingPolicy };
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        return options;
    }

    [Fact]
    public void Default_Options_Produce_Pascal_Case_Envelope_Properties()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var json = JsonSerializer.Serialize<Envelope>(envelope, NewOptions());

        Assert.Equal("{\"ContentType\":\"Bar.v1\",\"Content\":{\"Id\":\"bar1\"}}", json);
    }

    [Fact]
    public void CamelCase_Policy_Applies_To_Envelope_And_Content_Alike()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var json = JsonSerializer.Serialize<Envelope>(envelope, NewOptions(JsonNamingPolicy.CamelCase));

        Assert.Equal("{\"contentType\":\"Bar.v1\",\"content\":{\"id\":\"bar1\"}}", json);
    }

    [Fact]
    public void A_Custom_Naming_Policy_Applies_To_The_Envelope_Properties()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var json = JsonSerializer.Serialize<Envelope>(envelope, NewOptions(new UpperCasePolicy()));

        Assert.Equal("{\"CONTENTTYPE\":\"Bar.v1\",\"CONTENT\":{\"ID\":\"bar1\"}}", json);
    }

    [Fact]
    public void Serializing_By_The_Static_Envelope_Of_T_Type_Bypasses_The_Converter_And_Throws()
    {
        // The [JsonConverter] attribute on the abstract Envelope base does not apply when the
        // compile-time static type passed to Serialize is the derived Envelope<TContent>, so
        // System.Text.Json falls back to reflecting over Envelope<TContent> itself. That
        // reflection hits the ContentType property, whose type is System.Type - a type
        // System.Text.Json refuses to serialize. The resulting NotSupportedException is a loud,
        // deliberate guard rail: consumers must serialize through the Envelope base type (for
        // example JsonSerializer.Serialize<Envelope>(envelope, options)) for the converter, and
        // therefore the allow-list, to run at all.
        Envelope<BarEntity> envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(envelope, NewOptions()));
    }

    private sealed class UpperCasePolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) =>
            name.ToUpperInvariant();
    }
}
