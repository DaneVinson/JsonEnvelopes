using JsonEnvelopes.Example.Commands;
using JsonEnvelopes.Example.Handlers;
using JsonEnvelopes.Example.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JsonEnvelopes.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var envelopes = new List<Envelope>()
                {
                    new Envelope<CastFireball>(Utility.NewCastFireballCommand()),
                    new Envelope<CreateCharacter>(Utility.NewCreateCharacterCommand())
                };

                var jsonEnvelopes = envelopes
                                        .Select(envelope => JsonSerializer.Serialize(envelope))
                                        .ToList();

                envelopes = jsonEnvelopes
                                        .Select(json => JsonSerializer.Deserialize<Envelope>(json))
                                        .ToList();

                using var sericeProvider = NewServiceProvider();
                var mediator = sericeProvider.GetService<IMediator>();
                var dispatcher = sericeProvider.GetService<CommandDispatcher>();

                Console.WriteLine("=== Command handling with MediatR ===");
                var mediatrTasks = envelopes.Select(envelope => mediator.Send(envelope.GetContent()));
                await Task.WhenAll(mediatrTasks);
                Console.WriteLine();

                Console.WriteLine($"=== Command handling with {typeof(CommandDispatcher).FullName} ===");
                var dispatcherTasks = envelopes.Select(envelope => dispatcher.DispatchAsync(envelope));
                await Task.WhenAll(dispatcherTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} - {1}", ex.GetType(), ex.Message);
                Console.WriteLine(ex.StackTrace ?? string.Empty);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("...");
                Console.ReadKey();
            }
        }

        private static ServiceProvider NewServiceProvider()
        {
            return new ServiceCollection()
                        .AddMediatR(typeof(Program))
                        .AddSingleton<ICommandHandler<CastFireball>, CastFireballHandler>()
                        .AddSingleton<ICommandHandler<CreateCharacter>, CreateCharacterHandler>()
                        .AddSingleton<CommandDispatcher>()
                        .BuildServiceProvider();
        }

        private static void TimingTests()
        {
            var stopwatch = new Stopwatch();
            var iterations = 100000;

            var castFireballEnvelope = new Envelope<CastFireball>(Utility.NewCastFireballCommand());
            var createCharacterEnvelope = new Envelope<CreateCharacter>(Utility.NewCreateCharacterCommand());

            var json = JsonSerializer.Serialize<Envelope>(createCharacterEnvelope);

            for (int j = 0; j < 3; j++)
            {
                stopwatch.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    //var jsonEnvelope = JsonSerializer.Serialize<Envelope>(createCharacterEnvelope);
                    var resultEnvelope = JsonSerializer.Deserialize<Envelope>(json);
                }
                stopwatch.Stop();
                //Console.WriteLine($"Serialize x {iterations}: {stopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"Deserialize x {iterations}: {stopwatch.ElapsedMilliseconds} ms");
            }

            //var resultCommand = resultEnvelope.GetContent();
        }
    }
}
