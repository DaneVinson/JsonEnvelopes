using JsonEnvelopes.Example.Commands;
using JsonEnvelopes.Example.Services;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonEnvelopes.Example.Handlers
{
    public class CreateCharacterHandler : ICommandHandler<CreateCharacter>
    {
        public Task<bool> HandleAsync(CreateCharacter command)
        {
            Console.WriteLine($"{command}");

            return Task.FromResult(true);
        }

        public Task<bool> HandleAsync(object command) =>
            HandleAsync((CreateCharacter)command);
    }


    public class CreateCharacterMediatRHandler : IRequestHandler<CreateCharacter, bool>
    {
        public Task<bool> Handle(CreateCharacter command, CancellationToken cancellationToken)
        {
            Console.WriteLine($"{command}");

            return Task.FromResult(true);
        }
    }
}
