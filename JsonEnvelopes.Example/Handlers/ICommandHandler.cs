using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JsonEnvelopes.Example.Handlers
{
    public interface ICommandHandler
    {
        Task<bool> HandleAsync(object command);
    }

    public interface ICommandHandler<TCommand> : ICommandHandler
    {
        Task<bool> HandleAsync(TCommand command);
    }
}
