# Json Envelopes
Standardized messaging with System.Text.Json serialization.

## Purpose
Preform serialization/deserialization of messages such that receiving services can be agnostic with respect to message type.

## Get started
Install the [`JsonEnvelopes` NuGet Package](https://www.nuget.org/packages/JsonEnvelopes/) (e.g. version 1.1.0 from CLI).

    dotnet add package JsonEnvelopes --version 1.1.0

## Usage
Any class with a default constructor can be wrapped with `Envelope<T>`, e.g. the command `CastFireball`.

    var command = new CastFireball();
    var envelope = new Envelope<CastFireball>(command);

Serialize the envelope by calling `JsonSerializer.Serialize<Envelope>` (`System.Test.Json` namespace) and passing it an `Envelope<T>` instance. Note that the generic argument passed to `Serialize` is of type `Envelope` not of type `Envelope<T>`. This triggers the use of `EnvelopeJsonConverter`'s customized serialization operations (i.e. Read/Write);

    var json = JsonSerializer.Serialize<Envelope>(envelope);

Deserialize json for an `Envelope` by calling `JsonSerializer.Deserialize<Envelope>`. Again note the generic argument is of type `Envelope` which engages `EnvelopeJsonConverter`.

    var receivedEnvelope = JsonSerializer.Deserialize<Envelope>(json);

The envelope's `ContentType` provides the `AssemblyQualifiedName` string of the content's type (e.g. "JsonEnvelopes.Example.Commands.CastFireball, JsonEnvelopes.Example, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null").

    var assemblyQualifiedName = receivedEnvelope.ContentType;

The envelope's `GetContent` method returns an `object` which is of the type provided by the `ContentType` property. Continuing the example the variable `success` will be true in the following.

    var contentType = receivedEnvelope.GetContent().GetType();
    var success = contentType == typeof(CastFireball);

## Examples
See the project `JsonEnvelopes.Example` for examples of JsonEnvelopes message handling with Dependency Injection and additionally with [MediatR](https://github.com/jbogard/MediatR);

## Credits
The methodology used by `JsonEnvelopes` was heavily inspired by the work of the extraordinary developer and my former colleague, [Jonathan Berube](https://github.com/joncloud). My thanks to him.
