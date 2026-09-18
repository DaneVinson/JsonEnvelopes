# Allow-List Type Resolution — Design

**Date:** 2026-09-17
**Branch:** `allow-list-type-resolution`
**Target version:** 3.0.0 (breaking)
**Closes:** `status.md` findings 1, 2, 4, 5, 6, 7, 8, 10

## 1. Problem

`EnvelopeJsonConverter.Read` calls `Type.GetType(contentTypeString)` on a value
taken straight from the incoming JSON, with no allow-list. The sender therefore
chooses which type the receiving process loads and instantiates. Verified on
2026-09-17: the payload

```json
{"ContentType":"System.Net.WebClient, System.Net.WebClient","Content":{}}
```

loaded `System.Net.WebClient`. This is the design Microsoft documents as unsafe in
`Newtonsoft.Json` (`TypeNameHandling.All`) and which `System.Text.Json`
deliberately omits. `System.Text.Json` limits the blast radius compared with
`BinaryFormatter` — it calls property setters, not arbitrary constructors — but
static constructors still run and setter-based gadget chains exist. For a library
whose stated purpose is messages arriving from other services, this is the
headline risk.

The same line causes a second, independent problem. `ContentType` carries an
`AssemblyQualifiedName`, so the receiver must host an assembly with a matching
identity and an identically named type in an identically named namespace:

```
"Msg, probe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
```

Rename the project, move a class, or split the contracts assembly, and every
message already in a queue becomes undeliverable. That couples two services
through a .NET implementation detail — the opposite of the "agnostic with respect
to message type" goal in `readme.md`.

Fixing the first problem properly fixes the second, because both come from letting
the wire decide what a .NET type is.

## 2. Approach

Type resolution becomes a consumer-supplied allow-list. There is no unsafe
default and no fallback: `Type.GetType` is deleted outright.

Three alternatives were considered.

**A static global registry** would have kept today's zero-setup call site working
— `JsonSerializer.Serialize<Envelope>(envelope)` with no options — because the
`[JsonConverter]` attribute could still construct the converter. Rejected for
global mutable state: tests leak registrations into each other, and a process
cannot host two message vocabularies.

**An instance registry passed to the converter** avoids that. Chosen, with two
additions below.

**An immutable instance registry with cached factories (chosen).** The registry is
frozen after construction, so the read path needs no locking and can cache a
`Func<object, Envelope>` per registered type. That removes `Type.GetType`,
`MakeGenericType`, and `Activator.CreateInstance` from the per-message path in the
same change. The `[JsonConverter]` attribute stays on `Envelope`, pointing at a
converter that throws a directive error when it holds no registry, so the call
pattern documented in today's readme fails loudly rather than obscurely.

Verified on 2026-09-17 that this works: a converter in `options.Converters` takes
precedence over a `[JsonConverter]` attribute on the type, so a configured
converter always wins over the attribute-constructed one.

## 3. Public surface

Three types, all in the `JsonEnvelopes` namespace.

### `EnvelopeTypeRegistry`

Sealed and immutable. The allow-list. Holds two lookups built at construction:

- logical name → content `Type` plus a cached `Func<object, Envelope>` factory,
  for reads;
- content `Type` → logical name, for writes.

Both are plain `Dictionary` instances, populated during `Create` and never mutated
afterward, which is safe for concurrent reads. `FrozenDictionary` would be
preferable but is .NET 8+, and this change must not couple itself to the
multi-targeting work (finding 3).

Members:

- `static EnvelopeTypeRegistry Create(Action<EnvelopeTypeRegistryBuilder> configure)`
- `Envelope Wrap(object content)` — uses the cached factory. Throws
  `ArgumentNullException` for null, and `InvalidOperationException` naming the type
  when it is not registered.
- `string GetName(Type contentType)` — the wire name, for logging and
  dead-lettering. Throws `InvalidOperationException` naming the type when it is not
  registered, matching `Wrap`. Callers who need a non-throwing check use
  `TryGetName(Type, out string)`.
- `bool TryGetName(Type contentType, out string name)` — the non-throwing form, for
  a dead-letter path that must not itself throw.

### `EnvelopeTypeRegistryBuilder`

Sealed and mutable, short-lived, reachable only inside the `Create` callback. One
method:

- `EnvelopeTypeRegistryBuilder Add<TContent>(string name) where TContent : class`

Chainable. It validates at registration time, so collisions fail at startup rather
than on the first message: `name` must not be null or whitespace, must not already
be registered, and `TContent` must not already be registered.

No builder interface. Consumers never implement their own builder, so a concrete
sealed class is enough.

### `EnvelopeJsonConverter`

Gains `EnvelopeJsonConverter(EnvelopeTypeRegistry registry)`. The parameterless
constructor stays, because the `[JsonConverter]` attribute on `Envelope` needs one;
that instance holds no registry and throws on first use.

### Usage

```csharp
var registry = EnvelopeTypeRegistry.Create(builder => builder
    .Add<CastFireball>("CastFireball.v1")
    .Add<CreateCharacter>("CreateCharacter.v1"));

var options = new JsonSerializerOptions();
options.Converters.Add(new EnvelopeJsonConverter(registry));

var json = JsonSerializer.Serialize<Envelope>(envelope, options);
var received = JsonSerializer.Deserialize<Envelope>(json, options);
```

A registry is genuinely required, not optional. `Envelope` no longer carries enough
information to serialize itself, which is the point: there is no unsafe path to
fall back to.

## 4. Changes to `Envelope`

**`ContentType` changes from `string` to `Type`.** It returns `typeof(TContent)`.
Today it returns an `AssemblyQualifiedName` that
`JsonEnvelopes.Example/Services/CommandDispatcher.cs:22` immediately feeds back
through `Type.GetType` — a string round-trip that exists only because the wire
format leaked into the object model. Consumers dispatch on a `Type`. Anyone who
wants the wire name calls `registry.GetName(type)`.

**`Envelope.WrapContent(object)` is removed**, replaced by
`EnvelopeTypeRegistry.Wrap(object)`. It has to move, because wrapping now requires
knowing whether the type is registered. It gains the cached factory and an
`ArgumentNullException`, replacing today's `NullReferenceException`.

**The constraint loosens from `class, new()` to `class`.** The `new()` half exists
only to support `Content = content ?? new TContent()`, which becomes a throw.
Dropping it admits positional records and immutable types with `[JsonConstructor]`
— the types people actually register. Structs stay out of scope; they would box
through the object-based factory for no benefit.

**`Envelope<TContent>` loses its parameterless constructor, and `Content` becomes
get-only.** Both exist today only to support the `new TContent()` default. Without
`new()` a parameterless constructor would leave `Content` null, which contradicts
its non-nullable declaration and would let an invalid envelope reach the
serializer. `Content` is therefore assigned once in `Envelope(TContent content)`
and never afterward. The converter does not need a parameterless constructor: it
builds envelopes through the registry's cached factory, which calls the
content-taking constructor.

`Envelope.GetContent()` is unchanged.

## 5. Wire format

Both property names and the naming-policy behavior are unchanged. Only the *value*
of `contentType` changes, from an `AssemblyQualifiedName` to the registered logical
name:

```json
{"contentType":"CastFireball.v1","content":{"caster":"Mordenkainen"}}
```

Logical names are opaque strings chosen by the consumer. The library does not
derive, parse, or interpret them, and imposes no versioning convention — `".v1"`
in the examples is a suggestion, not a rule.

This format is what the golden tests in Section 7 pin.

## 6. Error handling

| Case | Behavior |
| --- | --- |
| Unknown logical name on read | `UnknownContentTypeException : JsonException`, carrying the name |
| Unregistered type on write, on `Wrap`, or on `GetName` | `InvalidOperationException` naming the type |
| Converter constructed by the attribute, holding no registry | `InvalidOperationException` naming the fix |
| `Wrap(null)`, or `new Envelope<T>(null)` | `ArgumentNullException` |
| Missing or empty `content` / `contentType` property | `JsonException` naming the actual property |
| Duplicate name, duplicate type, or blank name at registration | `ArgumentException` |

`UnknownContentTypeException` subclasses `JsonException` deliberately. Existing
`catch (JsonException)` keeps working, and a message pump can catch the narrow type
to dead-letter an unrecognized message without swallowing genuine parse failures.
It replaces today's `TypeLoadException`, which no longer describes what happened —
nothing tries to load a type any more.

Today three distinct failures all throw `new JsonException("Failed to parse JSON")`.
The rewritten `Read` names the missing property and the expected property name,
which varies with the naming policy.

Two bugs inside the method being rewritten are fixed at the same time, because
leaving them in freshly written code is worse than the small scope increase:

- `JsonDocument` is disposed on the success path (`using var document`), returning
  its pooled `ArrayPool` buffers.
- Property lookup honors `JsonSerializerOptions.PropertyNameCaseInsensitive`, and
  uses `PropertyNamingPolicy.ConvertName` rather than a reference comparison
  against `JsonNamingPolicy.CamelCase`, so `SnakeCaseLower`, `KebabCaseLower`, and
  custom policies work.

`Extensions` becomes `internal`. Its two methods are now purely an implementation
detail of the converter, and as public extension methods on
`JsonSerializerOptions` they appear in IntelliSense in every consuming project.

## 7. Testing

Tests come first, per TDD. The suite goes from 12 to roughly 30. Four groups carry
the weight.

**Security regression.** The `System.Net.WebClient` payload from Section 1 must
throw `UnknownContentTypeException` rather than load a type. This test proves the
change did its job and should read that way, naming the finding it closes.

**Golden wire format.** Assert the exact serialized string for a known envelope,
under both default and camelCase options. This is the most valuable missing test
today, and it must exist *before* 3.0 publishes a new format, or the silent-drift
problem starts over.

**Cross-configuration round-trips.** Write with one `JsonSerializerOptions` and read
with a different one, across the camelCase and case-insensitive matrix. Every
current test uses the same options object for both directions, which is why the
case-insensitivity bug went unnoticed.

**Registry behavior.** Duplicate name, duplicate type, null and whitespace names,
unregistered type on write, unregistered name on read, `Wrap(null)`, a positional
record as content, a collection as content, and parallel deserialization against one
shared registry.

## 8. Out of scope

Deliberately excluded, because none of them interact with this change and bundling
them would make the diff unreviewable: the EOL `net6.0` target framework (finding
3), AOT and trimming annotations (9), packaging (11), the missing LICENSE copyright
notice (12), the committed `.snk` (13), and repo hygiene (15).

This design does make finding 9 achievable later — explicit registration is what
roots types for a trimmer — but the annotations are their own pass.

## 9. Collateral changes

`JsonEnvelopes.Example` is in the solution and must build. The registry is
constructed in `Program.NewServiceProvider` and registered as a singleton;
`CommandDispatcher` loses its `Type.GetType` call and its null-forgiving operator,
becoming a direct `MakeGenericType(commandEnvelope.ContentType)`. Redundant local
`using` directives in `CommandDispatcher.cs` are removed, since the file is being
edited and `_GlobalUsings.cs` already covers them. `Console.ReadKey()` stays; it is
unrelated to this change.

Version becomes **3.0.0** in all four places that duplicate it:
`JsonEnvelopes/JsonEnvelopes.csproj` (`<AssemblyVersion>`),
`JsonEnvelopes.nuspec` (`<version>`), and twice in `readme.md`.

`readme.md` needs a rewritten usage section and a migration section covering the
2.0.1 → 3.0.0 move: register your types, add the converter to your options, switch
`ContentType` from `string` to `Type`, and replace `Envelope.WrapContent` with
`registry.Wrap`. It also needs the security rationale stated plainly — that
unregistered types are refused by design.
