using JsonEnvelopes.Example.Handlers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonEnvelopes.Example.Services
{
    public class CommandDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public CommandDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task DispatchAsync(Envelope commandEnvelope)
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(Type.GetType(commandEnvelope.ContentType)!);
            ICommandHandler? handler = _serviceProvider.GetService(handlerType) as ICommandHandler;
            if (handler == null) { throw new InvalidOperationException($"{nameof(handler)} is not {nameof(ICommandHandler)}"); }

            await handler.HandleAsync(commandEnvelope.GetContent());
        }
    }
}
