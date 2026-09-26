namespace JsonEnvelopes.Tests;

public class EnvelopeJsonConverterTests
{
    private static JsonSerializerOptions NewOptions(bool useCamelCase = false, bool caseInsensitive = false)
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder
            .Add<FooCommand<BarEntity>>("Foo.v1")
            .Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = caseInsensitive };
        if (useCamelCase)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        return options;
    }

    [Fact]
    public void Constructor_Throws_ArgumentNullException_When_Registry_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new EnvelopeJsonConverter(null!));
    }

    [Fact]
    public void Deserialization_Throws_JsonException_When_Json_Is_Invalid()
    {
        var json = "{\"ContentType\":\"Foo.v1\",\"Content\":\"test\"";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Envelope>(json, NewOptions()));
    }

    [Fact]
    public void Deserialization_Throws_JsonException_Naming_The_Missing_Content_Property()
    {
        var json = "{\"contentType\":\"Foo.v1\",\"data\":\"test\"}";

        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, NewOptions(useCamelCase: true)));

        Assert.Contains("content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialization_Throws_JsonException_Naming_The_Missing_ContentType_Property()
    {
        var json = "{\"content\":\"test\",\"data\":\"test\"}";

        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, NewOptions(useCamelCase: true)));

        Assert.Contains("contentType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialization_Throws_JsonException_When_ContentType_Is_Empty()
    {
        var json = "{\"content\":\"test\",\"contentType\":\"\"}";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, NewOptions(useCamelCase: true)));
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"a string\"")]
    public void Deserialization_Throws_JsonException_When_The_Root_Is_Not_An_Object(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Envelope>(json, NewOptions()));
    }

    [Fact]
    public void Deserialization_Throws_JsonException_When_ContentType_Is_Not_A_String()
    {
        var json = "{\"ContentType\":123,\"Content\":{}}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Envelope>(json, NewOptions()));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Serialization_Then_Deserialization_Produces_An_Equal_Object(bool useCamelCase, bool caseInsensitive)
    {
        var options = NewOptions(useCamelCase, caseInsensitive);
        var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
        var envelope = new Envelope<FooCommand<BarEntity>>(foo);

        var json = JsonSerializer.Serialize<Envelope>(envelope, options);
        var result = JsonSerializer.Deserialize<Envelope>(json, options)?.GetContent() as FooCommand<BarEntity>;

        Assert.NotNull(result);
        Assert.Equal(foo.Id, result!.Id);
        Assert.Equal(foo.Bar!.Id, result.Bar?.Id);
    }

    [Fact]
    public void CamelCase_Json_Is_Readable_When_Case_Insensitivity_Is_Enabled()
    {
        var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
        var json = JsonSerializer.Serialize<Envelope>(
            new Envelope<FooCommand<BarEntity>>(foo),
            NewOptions(useCamelCase: true));

        var result = JsonSerializer.Deserialize<Envelope>(json, NewOptions(caseInsensitive: true));

        Assert.IsType<Envelope<FooCommand<BarEntity>>>(result);
    }

    [Fact]
    public void PascalCase_Json_Is_Readable_When_Case_Insensitivity_Is_Enabled()
    {
        var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
        var json = JsonSerializer.Serialize<Envelope>(new Envelope<FooCommand<BarEntity>>(foo), NewOptions());

        var result = JsonSerializer.Deserialize<Envelope>(
            json,
            NewOptions(useCamelCase: true, caseInsensitive: true));

        Assert.IsType<Envelope<FooCommand<BarEntity>>>(result);
    }

    [Fact]
    public void A_Collection_Round_Trips_As_Content()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarList>("BarList.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var content = new BarList { Bars = { new BarEntity("one"), new BarEntity("two") } };

        var json = JsonSerializer.Serialize<Envelope>(new Envelope<BarList>(content), options);
        var result = JsonSerializer.Deserialize<Envelope>(json, options)?.GetContent() as BarList;

        Assert.NotNull(result);
        Assert.Equal(2, result!.Bars.Count);
        Assert.Equal("two", result.Bars[1].Id);
    }

    [Fact]
    public void Write_Serializes_Against_The_Declared_Type_Not_The_Runtime_Type()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BaseCommand>("Base.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var derived = new DerivedCommand { Id = "base-id", Detail = "derived-detail" };
        var envelope = new Envelope<BaseCommand>(derived);

        var json = JsonSerializer.Serialize<Envelope>(envelope, options);
        var result = JsonSerializer.Deserialize<Envelope>(json, options);

        Assert.Contains("\"Id\":\"base-id\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Detail", json, StringComparison.Ordinal);
        Assert.IsType<Envelope<BaseCommand>>(result);
        Assert.IsType<BaseCommand>(result!.GetContent());
    }

    [Fact]
    public void One_Registry_Serves_Concurrent_Deserialization()
    {
        var options = NewOptions();
        var json = JsonSerializer.Serialize<Envelope>(
            new Envelope<FooCommand<BarEntity>>(new FooCommand<BarEntity>(new BarEntity("pdq"), "abc")),
            options);

        Parallel.For(0, 1000, _ =>
        {
            var result = JsonSerializer.Deserialize<Envelope>(json, options);
            Assert.IsType<Envelope<FooCommand<BarEntity>>>(result);
        });
    }

    [Fact]
    public void One_Registry_Serves_Concurrent_Serialization()
    {
        var options = NewOptions();
        var envelopes = Enumerable.Range(0, 1000)
            .Select(index => index % 2 == 0
                ? (Envelope)new Envelope<FooCommand<BarEntity>>(
                    new FooCommand<BarEntity>(new BarEntity($"bar-{index}"), $"foo-{index}"))
                : new Envelope<BarEntity>(new BarEntity($"bar-{index}")))
            .ToArray();
        var expected = envelopes
            .Select(envelope => JsonSerializer.Serialize(envelope, options))
            .ToArray();
        var actual = new string[envelopes.Length];

        Parallel.For(0, envelopes.Length, index =>
        {
            actual[index] = JsonSerializer.Serialize(envelopes[index], options);
        });

        Assert.Equal(expected, actual);
        Assert.Contains("\"ContentType\":\"Foo.v1\"", actual[0], StringComparison.Ordinal);
        Assert.Contains("\"ContentType\":\"Bar.v1\"", actual[1], StringComparison.Ordinal);
    }
}
