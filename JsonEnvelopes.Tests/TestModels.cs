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

public record TeleportCommand(string Destination, int Range);

/// <summary>
/// A content type whose payload is a collection, used to prove collections round-trip as envelope
/// content.
/// </summary>
public class BarList
{
    /// <summary>
    /// The wrapped entities.
    /// </summary>
    public List<BarEntity> Bars { get; set; } = new();
}

/// <summary>
/// A base content type used to prove that the wire name and the serialized body always agree with
/// the declared type, even when a derived instance is wrapped as the base.
/// </summary>
public class BaseCommand
{
    /// <summary>
    /// An identifier carried by every command.
    /// </summary>
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// A content type deriving from <see cref="BaseCommand"/>, adding a property that must not appear
/// on the wire when an instance is wrapped as an envelope of the base type.
/// </summary>
public class DerivedCommand : BaseCommand
{
    /// <summary>
    /// A property present only on the derived type.
    /// </summary>
    public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// An abstract content type used to prove that <see cref="EnvelopeTypeRegistryBuilder.Add{TContent}"/>
/// refuses registration at startup rather than failing on the first message.
/// </summary>
public abstract class AbstractCommand
{
}

/// <summary>
/// An interface content type used to prove that <see cref="EnvelopeTypeRegistryBuilder.Add{TContent}"/>
/// refuses registration at startup rather than failing on the first message.
/// </summary>
public interface ICommand
{
}
