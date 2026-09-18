namespace JsonEnvelopes.Tests;

public class EnvelopeTests
{
    [Fact]
    public void WrapContent_Test()
    {
        var foo = new FooCommand<BarEntity>(new BarEntity("bar1"), "foo1");

        var envelope = Envelope.WrapContent(foo);
        var content = envelope.GetContent();

        Assert.Equal(foo.GetType(), content.GetType());
        var fooContent = content as FooCommand<BarEntity>;
        if (fooContent == null)
        {
            Assert.NotNull(fooContent);
            return;
        }

        Assert.Equal(foo.Id, fooContent.Id);
        Assert.Equal(foo.Bar!.Id, fooContent.Bar!.Id);
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
