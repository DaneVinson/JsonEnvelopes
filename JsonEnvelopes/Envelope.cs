namespace JsonEnvelopes;

/// <summary>
/// Abstract base class for <see cref="Envelope{TContent}"/> utilizing a custom JsonConverter.
/// </summary>
[JsonConverter(typeof(EnvelopeJsonConverter))]
public abstract class Envelope
{
    /// <summary>
    /// Wrap any content in an <see cref="Envelope{TContent}"/> and return as an <see cref="Envelope"/>.
    /// </summary>
    public static Envelope WrapContent(object content) =>
        (Activator.CreateInstance(
                        typeof(Envelope<>).MakeGenericType(content.GetType()),
                        content) as Envelope)!;

    public abstract object GetContent();

    public abstract string ContentType { get; }
}
