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
}
