namespace JsonEnvelopes.Example.Services;

/// <summary>
/// Resolves an <see cref="ICommandHandler{TCommand}"/> for an envelope's content type and hands the
/// content to it.
/// </summary>
public class CommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Create a dispatcher backed by <paramref name="serviceProvider"/>.
    /// </summary>
    public CommandDispatcher(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Dispatch the content of <paramref name="commandEnvelope"/> to its handler.
    /// </summary>
    public async Task DispatchAsync(Envelope commandEnvelope)
    {
        ArgumentNullException.ThrowIfNull(commandEnvelope);

        var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandEnvelope.ContentType);
        if (_serviceProvider.GetService(handlerType) is not ICommandHandler handler)
        {
            throw new InvalidOperationException($"No {nameof(ICommandHandler)} is registered for {handlerType}.");
        }

        await handler.HandleAsync(commandEnvelope.GetContent());
    }
}
