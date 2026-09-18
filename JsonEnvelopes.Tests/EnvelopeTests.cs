namespace JsonEnvelopes.Tests;

public class EnvelopeTests
{
    [Fact]
    public void ContentType_Is_The_Content_Clr_Type()
    {
        var envelope = new Envelope<FooCommand<BarEntity>>(new FooCommand<BarEntity>());

        Assert.Equal(typeof(FooCommand<BarEntity>), envelope.ContentType);
    }

    [Fact]
    public void Constructor_Throws_ArgumentNullException_When_Content_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new Envelope<BarEntity>(null!));
    }

    [Fact]
    public void A_Record_Without_A_Parameterless_Constructor_Can_Be_Content()
    {
        var command = new TeleportCommand("Waterdeep", 500);

        var envelope = new Envelope<TeleportCommand>(command);

        Assert.Same(command, envelope.GetContent());
    }

    [Fact]
    public void A_Record_Without_A_Parameterless_Constructor_Can_Be_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<TeleportCommand>("Teleport.v1"));

        var envelope = registry.Wrap(new TeleportCommand("Waterdeep", 500));

        Assert.IsType<Envelope<TeleportCommand>>(envelope);
    }
}
