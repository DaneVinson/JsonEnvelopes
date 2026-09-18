namespace JsonEnvelopes;

/// <summary>
/// Abstract base class for <see cref="Envelope{TContent}"/> utilizing a custom JsonConverter.
/// </summary>
[JsonConverter(typeof(EnvelopeJsonConverter))]
public abstract class Envelope
{
    /// <summary>
    /// The CLR type of the wrapped content. To obtain the logical name this type carries on the
    /// wire, call <see cref="EnvelopeTypeRegistry.GetName"/>.
    /// </summary>
    public abstract Type ContentType { get; }

    /// <summary>
    /// The wrapped content.
    /// </summary>
    public abstract object GetContent();
}
