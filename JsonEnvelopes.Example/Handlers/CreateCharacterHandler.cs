using JsonEnvelopes.Example.Commands;
using JsonEnvelopes.Example.Services;
using System;
using System.Collections.Generic;
using System.Text;
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
}
