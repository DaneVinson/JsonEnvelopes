namespace JsonEnvelopes.Example;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var registry = EnvelopeTypeRegistry.Create(builder => builder
                .Add<CastFireball>("CastFireball.v1")
                .Add<CreateCharacter>("CreateCharacter.v1"));
            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new EnvelopeJsonConverter(registry));

            var envelopes = new List<Envelope>()
            {
                new Envelope<CastFireball>(Utility.NewCastFireballCommand()),
                registry.Wrap(Utility.NewCreateCharacterCommand())
            };

            var jsonEnvelopes = envelopes
                .Select(envelope => JsonSerializer.Serialize<Envelope>(envelope, jsonOptions))
                .ToList();

            envelopes = jsonEnvelopes
                .Select(json => JsonSerializer.Deserialize<Envelope>(json, jsonOptions)!)
                .ToList();

            await using var serviceProvider = NewServiceProvider(registry);
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var dispatcher = serviceProvider.GetRequiredService<CommandDispatcher>();

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

    private static ServiceProvider NewServiceProvider(EnvelopeTypeRegistry registry)
    {
        return new ServiceCollection()
            .AddSingleton(registry)
            .AddMediatR(typeof(Program))
            .AddSingleton<ICommandHandler<CastFireball>, CastFireballHandler>()
            .AddSingleton<ICommandHandler<CreateCharacter>, CreateCharacterHandler>()
            .AddSingleton<CommandDispatcher>()
            .BuildServiceProvider();
    }
}
