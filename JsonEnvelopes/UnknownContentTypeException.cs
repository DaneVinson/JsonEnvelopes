namespace JsonEnvelopes;

/// <summary>
/// Thrown when incoming JSON names a content type that is not in the
/// <see cref="EnvelopeTypeRegistry"/>. Derives from <see cref="JsonException"/> so that existing
/// handlers keep working, while a message pump can catch this type alone to dead-letter an
/// unrecognized message without swallowing genuine parse failures.
/// </summary>
public sealed class UnknownContentTypeException : JsonException
{
    /// <summary>
    /// Create the exception for <paramref name="contentTypeName"/>.
    /// </summary>
    public UnknownContentTypeException(string contentTypeName)
        : base($"'{contentTypeName}' is not a registered content type name.") =>
        ContentTypeName = contentTypeName;

    /// <summary>
    /// The unrecognized name that appeared on the wire.
    /// </summary>
    public string ContentTypeName { get; }
}
