namespace JsonEnvelopes.Tests;

/// <summary>
/// Regression tests for the arbitrary type loading fixed in 3.0.0. Before the registry, the
/// converter called Type.GetType on a value taken straight from the incoming JSON, so the sender
/// chose which type the receiving process loaded.
/// </summary>
public class SecurityTests
{
    [Theory]
    [InlineData("System.Net.WebClient, System.Net.WebClient")]
    [InlineData("System.IO.FileInfo, System.Private.CoreLib")]
    [InlineData("JsonEnvelopes.Tests.BarEntity, JsonEnvelopes.Tests")]
    public void An_Unregistered_Type_Name_Is_Refused_Rather_Than_Loaded(string contentTypeName)
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<FooCommand<BarEntity>>("Foo.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var json = $"{{\"ContentType\":\"{contentTypeName}\",\"Content\":{{}}}}";

        var exception = Assert.Throws<UnknownContentTypeException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, options));

        Assert.Equal(contentTypeName, exception.ContentTypeName);
    }

    [Fact]
    public void An_UnknownContentTypeException_Is_Catchable_As_A_JsonException()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var json = "{\"ContentType\":\"Nope.v1\",\"Content\":{}}";

        Assert.Throws<UnknownContentTypeException>(() =>
        {
            try
            {
                JsonSerializer.Deserialize<Envelope>(json, options);
            }
            catch (JsonException exception)
            {
                throw exception;
            }
        });
    }

    [Fact]
    public void A_Converter_Without_A_Registry_Refuses_To_Read()
    {
        var json = "{\"ContentType\":\"Bar.v1\",\"Content\":{}}";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Deserialize<Envelope>(json));

        Assert.Contains(nameof(EnvelopeTypeRegistry), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Converter_Without_A_Registry_Refuses_To_Write()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Serialize<Envelope>(envelope));

        Assert.Contains(nameof(EnvelopeTypeRegistry), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writing_An_Unregistered_Content_Type_Throws()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var envelope = new Envelope<FooCommand<BarEntity>>(new FooCommand<BarEntity>());

        Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Serialize<Envelope>(envelope, options));
    }
}
