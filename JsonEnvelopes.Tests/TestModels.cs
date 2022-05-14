namespace JsonEnvelopes.Tests;

public class FooCommand<T> where T : class, new()
{
    public FooCommand()
    {
    }

    public FooCommand(T bar, string id = "") =>
        (Id, Bar) = (id, bar);

    public string Id { get; set; } = string.Empty;

    public T? Bar { get; set; }
}

public class BarEntity
{
    public BarEntity()
    {
    }

    public BarEntity(string id) =>
        Id = id;

    public string Id { get; set; } = string.Empty;
}
