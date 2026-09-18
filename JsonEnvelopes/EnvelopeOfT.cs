namespace JsonEnvelopes;

/// <summary>
/// Concrete implementation of <see cref="Envelope"/> with generic Content.
/// </summary>
public class Envelope<TContent> : Envelope where TContent : class
{
    /// <summary>
    /// Wrap <paramref name="content"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public Envelope(TContent content)
    {
        if (content is null) { throw new ArgumentNullException(nameof(content)); }

        Content = content;
    }

    /// <summary>
    /// The wrapped content. Assigned at construction and never afterward.
    /// </summary>
    public TContent Content { get; }

    /// <inheritdoc />
    public override Type ContentType =>
        typeof(TContent);

    /// <inheritdoc />
    public override object GetContent() =>
        Content;
}
