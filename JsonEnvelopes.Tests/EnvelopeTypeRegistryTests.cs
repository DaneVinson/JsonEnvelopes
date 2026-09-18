namespace JsonEnvelopes.Tests;

public class EnvelopeTypeRegistryTests
{
    [Fact]
    public void Create_Throws_ArgumentNullException_When_Configure_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => EnvelopeTypeRegistry.Create(null!));
    }

    [Fact]
    public void GetName_Returns_The_Registered_Name()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.Equal("Bar.v1", registry.GetName(typeof(BarEntity)));
    }

    [Fact]
    public void GetName_Throws_InvalidOperationException_When_Type_Is_Not_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.GetName(typeof(FooCommand<BarEntity>)));

        Assert.Contains("FooCommand", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetName_Returns_False_When_Type_Is_Not_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.False(registry.TryGetName(typeof(FooCommand<BarEntity>), out var name));
        Assert.Null(name);
    }

    [Fact]
    public void TryGetName_Returns_True_And_The_Name_When_Type_Is_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.True(registry.TryGetName(typeof(BarEntity), out var name));
        Assert.Equal("Bar.v1", name);
    }

    [Fact]
    public void Wrap_Produces_An_Envelope_Of_The_Content_Type()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));
        var bar = new BarEntity("bar1");

        var envelope = registry.Wrap(bar);

        Assert.IsType<Envelope<BarEntity>>(envelope);
        Assert.Same(bar, envelope.GetContent());
    }

    [Fact]
    public void Wrap_Throws_ArgumentNullException_When_Content_Is_Null()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.Throws<ArgumentNullException>(() => registry.Wrap(null!));
    }

    [Fact]
    public void Wrap_Throws_InvalidOperationException_When_Type_Is_Not_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Wrap(new FooCommand<BarEntity>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_Throws_ArgumentException_When_Name_Is_Blank(string? name)
    {
        Assert.Throws<ArgumentException>(() => EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>(name!)));
    }

    [Fact]
    public void Add_Throws_ArgumentException_When_Name_Is_Already_Registered()
    {
        var exception = Assert.Throws<ArgumentException>(() => EnvelopeTypeRegistry.Create(builder =>
            builder
                .Add<BarEntity>("Duplicate.v1")
                .Add<FooCommand<BarEntity>>("Duplicate.v1")));

        Assert.Contains("Duplicate.v1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_Throws_ArgumentException_When_Type_Is_Already_Registered()
    {
        var exception = Assert.Throws<ArgumentException>(() => EnvelopeTypeRegistry.Create(builder =>
            builder
                .Add<BarEntity>("Bar.v1")
                .Add<BarEntity>("Bar.v2")));

        Assert.Contains("BarEntity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_Is_Chainable_And_Registers_Every_Type()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder
            .Add<BarEntity>("Bar.v1")
            .Add<FooCommand<BarEntity>>("Foo.v1"));

        Assert.Equal("Bar.v1", registry.GetName(typeof(BarEntity)));
        Assert.Equal("Foo.v1", registry.GetName(typeof(FooCommand<BarEntity>)));
    }

    [Fact]
    public void Registry_Is_Unaffected_By_Later_Mutation_Of_The_Captured_Builder()
    {
        EnvelopeTypeRegistryBuilder? capturedBuilder = null;
        var registry = EnvelopeTypeRegistry.Create(builder =>
        {
            capturedBuilder = builder;
            builder.Add<BarEntity>("Bar.v1");
        });

        capturedBuilder!.Add<FooCommand<BarEntity>>("Foo.v1");

        Assert.False(registry.TryGetName(typeof(FooCommand<BarEntity>), out var name));
        Assert.Null(name);
    }
}
