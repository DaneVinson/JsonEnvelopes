# Json Envelopes
Standardized messaging with System.Text.Json serialization.

## Purpose
Preform serialization/deserialization of messages such that receiving services can be agnostic with respect to message type.

## Get started
Install the [`JsonEnvelopes` NuGet Package](https://www.nuget.org/packages/JsonEnvelopes/) (e.g. version 3.0.0).

    dotnet add package JsonEnvelopes --version 3.0.0

The package targets **.NET Standard 2.0**, so it runs on .NET Core 2.0+, .NET 5+, and
.NET Framework 4.6.1+. It depends on `System.Text.Json` 6.0.10 or later. On .NET 6 and
newer that dependency is already part of the shared framework; on .NET Framework it
arrives as a package along with its own transitive dependencies.

## Usage

Register every type you are willing to receive, then add a converter built from that registry to
your `JsonSerializerOptions`.

    var registry = EnvelopeTypeRegistry.Create(builder => builder
        .Add<CastFireball>("CastFireball.v1")
        .Add<CreateCharacter>("CreateCharacter.v1"));

    var jsonOptions = new JsonSerializerOptions();
    jsonOptions.Converters.Add(new EnvelopeJsonConverter(registry));

The logical name you pass to `Add` is what travels on the wire. It is an opaque string; this
library never parses it. The `.v1` suffix above is a suggestion, not a requirement.

Wrap content whose type you know at compile time with `Envelope<T>`, or let the registry pick the
type at run time with `Wrap`.

    var envelope = new Envelope<CastFireball>(new CastFireball());
    var alsoAnEnvelope = registry.Wrap(someCommand);

Serialize by calling `JsonSerializer.Serialize<Envelope>` with your options. Note that the generic
argument is `Envelope`, not `Envelope<T>` — that is what engages `EnvelopeJsonConverter`.

    var json = JsonSerializer.Serialize<Envelope>(envelope, jsonOptions);

The result carries the logical name, not a .NET type name:

    {"ContentType":"CastFireball.v1","Content":{ ... }}

Deserialize the same way.

    var receivedEnvelope = JsonSerializer.Deserialize<Envelope>(json, jsonOptions);

`ContentType` gives you the content's CLR type, ready for handler lookup.

    var contentType = receivedEnvelope.ContentType;   // typeof(CastFireball)
    var content = receivedEnvelope.GetContent();      // the CastFireball instance

### Unregistered types are refused

A name that is not in the registry throws `UnknownContentTypeException`, which derives from
`JsonException`. The library never resolves a type name supplied by the sender, so a message cannot
choose which type your process loads. Catch the narrow type to dead-letter an unrecognized message:

    try
    {
        var received = JsonSerializer.Deserialize<Envelope>(json, jsonOptions);
    }
    catch (UnknownContentTypeException exception)
    {
        DeadLetter(json, exception.ContentTypeName);
    }

## Migrating from 2.x

Version 3.0.0 is a breaking change and its wire format is not compatible with 2.x. Messages written
by 2.x cannot be read by 3.0.0, so drain any queue before upgrading.

| 2.x | 3.0.0 |
| --- | --- |
| No setup; the converter resolved any type name | Build an `EnvelopeTypeRegistry` and add `new EnvelopeJsonConverter(registry)` to your options |
| `ContentType` was a `string` holding an `AssemblyQualifiedName` | `ContentType` is a `Type`; the wire carries your logical name |
| `Envelope.WrapContent(content)` | `registry.Wrap(content)` |
| `Envelope<T>` required `T` to have a parameterless constructor | Any reference type, including positional records |
| `new Envelope<T>()` | Removed; use `new Envelope<T>(content)` |
| An unknown type threw `TypeLoadException` | An unregistered name throws `UnknownContentTypeException` |
| Serializing the concrete `Envelope<T>` worked the same as serializing `Envelope` | `ContentType` is now a `Type`, which System.Text.Json refuses to serialize by reflection; serializing `Envelope<T>` by its own type now throws `NotSupportedException` instead of silently producing different output. Always call `JsonSerializer.Serialize<Envelope>(envelope, options)` so the converter runs |

Call sites that relied on `Type.GetType(envelope.ContentType)` simplify to `envelope.ContentType`.

## Examples
See the project `JsonEnvelopes.Example` for examples of JsonEnvelopes message handling with Dependency Injection and additionally with [MediatR](https://github.com/jbogard/MediatR).

## Credits
The methodology used in `JsonEnvelopes` was heavily inspired by the work of the extraordinary developer [Jonathan Berube](https://github.com/joncloud). My thanks to him.
