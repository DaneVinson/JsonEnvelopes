# Allow-List Type Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace attacker-controlled `Type.GetType` resolution in `EnvelopeJsonConverter` with a consumer-supplied allow-list, so the wire carries a logical name the consumer chose rather than an `AssemblyQualifiedName`.

**Architecture:** An immutable `EnvelopeTypeRegistry`, built once through a short-lived builder, maps logical names to content types and back. Each registration captures a closure `content => new Envelope<TContent>((TContent)content)`, so no reflection remains on the per-message path. `EnvelopeJsonConverter` takes the registry as a constructor argument; the parameterless constructor that the `[JsonConverter]` attribute needs throws a directive error instead of falling back to anything unsafe.

**Tech Stack:** C# 10, .NET 6, `System.Text.Json`, xunit. No new dependencies.

**Spec:** [docs/superpowers/specs/2026-09-17-allow-list-type-resolution-design.md](../specs/2026-09-17-allow-list-type-resolution-design.md)

## Global Constraints

Every task's requirements implicitly include this section.

- **Target:** `net6.0`, `LangVersion` 10.0. Never add `<Nullable>`, `<ImplicitUsings>`, or `<LangVersion>` to a `.csproj` — they come from `Directory.Build.props`.
- **No new NuGet dependencies** in the `JsonEnvelopes` project.
- **Line endings are CRLF.** Match them when writing or appending to any file, or the diff shows every line as changed.
- **Never delete `[assembly: AssemblyKeyFile("JsonEnvelopes.snk")]` from the top of `JsonEnvelopes/Extensions.cs`.** That single line is what strong-names the assembly. Task 4 rewrites this file; the attribute must survive.
- **Do not change the package version** except in Task 6. Do not push to `origin` at any point — pushing `main` publishes to nuget.org.
- **Conventions** (from `CLAUDE.md`), applied to all new and edited code:
  - Always use `var` for locals.
  - Private fields are `_camelCase`. Never abbreviate: `exception` not `ex`, `parameter` not `param`.
  - Member order within a type: constants, private backing fields, constructors, properties, methods — each group sorted alphabetically by member name.
  - XML doc comments (`/// <summary>`) on every type and every non-private member. Private members need none.
  - Always use braces for control flow, even single-line bodies.
  - Always write explicit access modifiers.
  - Never add a `using` directive to a `.cs` file. Add `global using` to that project's `_GlobalUsings.cs` instead.
- **Test command:** `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`. Narrow with `--filter "FullyQualifiedName~TestName"`.
- **Build command:** `dotnet build JsonEnvelopes.sln`. The solution must build at the end of every task.

---

### Task 1: `EnvelopeTypeRegistry` and its builder

Purely additive. Nothing existing changes, so the 12 current tests must still pass at the end.

**Files:**
- Create: `JsonEnvelopes/EnvelopeTypeRegistry.cs`
- Create: `JsonEnvelopes/EnvelopeTypeRegistryBuilder.cs`
- Test: `JsonEnvelopes.Tests/EnvelopeTypeRegistryTests.cs` (create)

**Interfaces:**
- Consumes: `Envelope`, `Envelope<TContent>` as they exist today.
- Produces:
  - `EnvelopeTypeRegistry.Create(Action<EnvelopeTypeRegistryBuilder> configure)` → `EnvelopeTypeRegistry`
  - `EnvelopeTypeRegistry.Wrap(object content)` → `Envelope`
  - `EnvelopeTypeRegistry.GetName(Type contentType)` → `string`
  - `EnvelopeTypeRegistry.TryGetName(Type contentType, out string? name)` → `bool`
  - `EnvelopeTypeRegistry.TryGetRegistration(string name, out EnvelopeRegistration? registration)` → `bool` (internal, used by Task 3)
  - `EnvelopeTypeRegistryBuilder.Add<TContent>(string name)` → `EnvelopeTypeRegistryBuilder`
  - `EnvelopeRegistration.ContentType` → `Type`, `EnvelopeRegistration.Factory` → `Func<object, Envelope>` (internal)

Note on `TryGetName`: the spec writes it as `out string name`. Nullable reference types are enabled, so the real signature is `out string? name`.

Note on the `where TContent : class, new()` constraint: `Envelope<TContent>` still carries `new()` until Task 2, so `Add<TContent>` must declare `where TContent : class, new()` in this task. Task 2 loosens both.

- [ ] **Step 1: Write the failing tests**

Create `JsonEnvelopes.Tests/EnvelopeTypeRegistryTests.cs`:

```csharp
namespace JsonEnvelopes.Tests;

public class EnvelopeTypeRegistryTests
{
    [Fact]
    public void Create_Throws_ArgumentNullException_When_Configure_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => EnvelopeTypeRegistry.Create(null!));
    }

    [Fact]
    public void GetName_Returns_The_Registered_Name()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.Equal("Bar.v1", registry.GetName(typeof(BarEntity)));
    }

    [Fact]
    public void GetName_Throws_InvalidOperationException_When_Type_Is_Not_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.GetName(typeof(FooCommand<BarEntity>)));

        Assert.Contains("FooCommand", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetName_Returns_False_When_Type_Is_Not_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.False(registry.TryGetName(typeof(FooCommand<BarEntity>), out var name));
        Assert.Null(name);
    }

    [Fact]
    public void TryGetName_Returns_True_And_The_Name_When_Type_Is_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.True(registry.TryGetName(typeof(BarEntity), out var name));
        Assert.Equal("Bar.v1", name);
    }

    [Fact]
    public void Wrap_Produces_An_Envelope_Of_The_Content_Type()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));
        var bar = new BarEntity("bar1");

        var envelope = registry.Wrap(bar);

        Assert.IsType<Envelope<BarEntity>>(envelope);
        Assert.Same(bar, envelope.GetContent());
    }

    [Fact]
    public void Wrap_Throws_ArgumentNullException_When_Content_Is_Null()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.Throws<ArgumentNullException>(() => registry.Wrap(null!));
    }

    [Fact]
    public void Wrap_Throws_InvalidOperationException_When_Type_Is_Not_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>("Bar.v1"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Wrap(new FooCommand<BarEntity>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_Throws_ArgumentException_When_Name_Is_Blank(string? name)
    {
        Assert.Throws<ArgumentException>(() => EnvelopeTypeRegistry.Create(builder =>
            builder.Add<BarEntity>(name!)));
    }

    [Fact]
    public void Add_Throws_ArgumentException_When_Name_Is_Already_Registered()
    {
        var exception = Assert.Throws<ArgumentException>(() => EnvelopeTypeRegistry.Create(builder =>
            builder
                .Add<BarEntity>("Duplicate.v1")
                .Add<FooCommand<BarEntity>>("Duplicate.v1")));

        Assert.Contains("Duplicate.v1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_Throws_ArgumentException_When_Type_Is_Already_Registered()
    {
        var exception = Assert.Throws<ArgumentException>(() => EnvelopeTypeRegistry.Create(builder =>
            builder
                .Add<BarEntity>("Bar.v1")
                .Add<BarEntity>("Bar.v2")));

        Assert.Contains("BarEntity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_Is_Chainable_And_Registers_Every_Type()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder
            .Add<BarEntity>("Bar.v1")
            .Add<FooCommand<BarEntity>>("Foo.v1"));

        Assert.Equal("Bar.v1", registry.GetName(typeof(BarEntity)));
        Assert.Equal("Foo.v1", registry.GetName(typeof(FooCommand<BarEntity>)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj --filter "FullyQualifiedName~EnvelopeTypeRegistryTests"`

Expected: compile failure — `EnvelopeTypeRegistry` does not exist.

- [ ] **Step 3: Create the registry**

Create `JsonEnvelopes/EnvelopeTypeRegistry.cs`:

```csharp
namespace JsonEnvelopes;

/// <summary>
/// An immutable allow-list of the content types an <see cref="Envelope"/> may carry, keyed by the
/// logical name each type is given on the wire. Build one with <see cref="Create"/>.
/// </summary>
public sealed class EnvelopeTypeRegistry
{
    private readonly Dictionary<string, EnvelopeRegistration> _registrationsByName;
    private readonly Dictionary<Type, string> _namesByType;

    internal EnvelopeTypeRegistry(
        Dictionary<string, EnvelopeRegistration> registrationsByName,
        Dictionary<Type, string> namesByType) =>
        (_registrationsByName, _namesByType) = (registrationsByName, namesByType);

    /// <summary>
    /// Build a registry. The builder is only valid inside <paramref name="configure"/>; the
    /// resulting registry is immutable and safe for concurrent use.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    public static EnvelopeTypeRegistry Create(Action<EnvelopeTypeRegistryBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new EnvelopeTypeRegistryBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Return the logical wire name registered for <paramref name="contentType"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="contentType"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="contentType"/> is not registered.</exception>
    public string GetName(Type contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        if (!_namesByType.TryGetValue(contentType, out var name))
        {
            throw new InvalidOperationException(
                $"The content type {contentType} is not registered. Add it when building the registry.");
        }

        return name;
    }

    /// <summary>
    /// Return the logical wire name registered for <paramref name="contentType"/> without throwing
    /// when it is absent. Use this on a path that must not throw, such as dead-lettering.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="contentType"/> is null.</exception>
    public bool TryGetName(Type contentType, out string? name)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        return _namesByType.TryGetValue(contentType, out name);
    }

    /// <summary>
    /// Wrap <paramref name="content"/> in an <see cref="Envelope{TContent}"/> for its runtime type.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The content's type is not registered.</exception>
    public Envelope Wrap(object content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var name = GetName(content.GetType());
        return _registrationsByName[name].Factory(content);
    }

    internal bool TryGetRegistration(string name, out EnvelopeRegistration? registration) =>
        _registrationsByName.TryGetValue(name, out registration);
}

/// <summary>
/// One entry in an <see cref="EnvelopeTypeRegistry"/>: the registered content type and a factory
/// that wraps an instance of it without reflection.
/// </summary>
internal sealed class EnvelopeRegistration
{
    internal EnvelopeRegistration(Type contentType, Func<object, Envelope> factory) =>
        (ContentType, Factory) = (contentType, factory);

    internal Type ContentType { get; }

    internal Func<object, Envelope> Factory { get; }
}
```

- [ ] **Step 4: Create the builder**

Create `JsonEnvelopes/EnvelopeTypeRegistryBuilder.cs`:

```csharp
namespace JsonEnvelopes;

/// <summary>
/// Collects registrations for an <see cref="EnvelopeTypeRegistry"/>. Obtained inside the callback
/// passed to <see cref="EnvelopeTypeRegistry.Create"/>.
/// </summary>
public sealed class EnvelopeTypeRegistryBuilder
{
    private readonly Dictionary<Type, string> _namesByType = new();
    private readonly Dictionary<string, EnvelopeRegistration> _registrationsByName = new(StringComparer.Ordinal);

    internal EnvelopeTypeRegistryBuilder()
    { }

    /// <summary>
    /// Allow <typeparamref name="TContent"/> as envelope content, carried on the wire as
    /// <paramref name="name"/>. Names are opaque strings; this library never parses them.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or whitespace; or <paramref name="name"/> or
    /// <typeparamref name="TContent"/> is already registered.
    /// </exception>
    public EnvelopeTypeRegistryBuilder Add<TContent>(string name) where TContent : class, new()
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A logical content type name is required.", nameof(name));
        }
        if (_registrationsByName.ContainsKey(name))
        {
            throw new ArgumentException($"The name '{name}' is already registered.", nameof(name));
        }
        if (_namesByType.TryGetValue(typeof(TContent), out var existingName))
        {
            throw new ArgumentException(
                $"The content type {typeof(TContent)} is already registered as '{existingName}'.",
                nameof(name));
        }

        _registrationsByName.Add(
            name,
            new EnvelopeRegistration(typeof(TContent), content => new Envelope<TContent>((TContent)content)));
        _namesByType.Add(typeof(TContent), name);
        return this;
    }

    internal EnvelopeTypeRegistry Build() =>
        new(_registrationsByName, _namesByType);
}
```

The factory is a closure over `TContent`, so wrapping needs no `MakeGenericType` and no `Activator`. This is what removes reflection from the per-message path.

- [ ] **Step 5: Run the new tests to verify they pass**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj --filter "FullyQualifiedName~EnvelopeTypeRegistryTests"`

Expected: PASS, 13 tests.

- [ ] **Step 6: Run the whole suite to verify nothing regressed**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`

Expected: PASS, 25 tests (12 existing + 13 new).

- [ ] **Step 7: Commit**

```bash
git add JsonEnvelopes/EnvelopeTypeRegistry.cs JsonEnvelopes/EnvelopeTypeRegistryBuilder.cs JsonEnvelopes.Tests/EnvelopeTypeRegistryTests.cs
git commit -m "Add the envelope type registry and its builder"
```

---

### Task 2: Loosen `Envelope<TContent>`

Drops the `new()` constraint so records and immutable types can be registered, removes the parameterless constructor that only existed to support it, and makes `Content` get-only.

**Files:**
- Modify: `JsonEnvelopes/EnvelopeOfT.cs`
- Modify: `JsonEnvelopes/EnvelopeTypeRegistryBuilder.cs` (drop `new()` from `Add<TContent>`)
- Modify: `JsonEnvelopes.Tests/TestModels.cs`
- Test: `JsonEnvelopes.Tests/EnvelopeTests.cs`

**Interfaces:**
- Consumes: `EnvelopeTypeRegistryBuilder.Add<TContent>(string)` from Task 1.
- Produces: `Envelope<TContent> where TContent : class` with a single constructor `Envelope(TContent content)` and a get-only `TContent Content { get; }`.

- [ ] **Step 1: Add a record test model**

Append to `JsonEnvelopes.Tests/TestModels.cs`:

```csharp
public record TeleportCommand(string Destination, int Range);
```

A positional record has no parameterless constructor, which is exactly what the old `new()` constraint rejected.

- [ ] **Step 2: Write the failing tests**

Append to `JsonEnvelopes.Tests/EnvelopeTests.cs`, inside `EnvelopeTests`:

```csharp
    [Fact]
    public void Constructor_Throws_ArgumentNullException_When_Content_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new Envelope<BarEntity>(null!));
    }

    [Fact]
    public void A_Record_Without_A_Parameterless_Constructor_Can_Be_Content()
    {
        var command = new TeleportCommand("Waterdeep", 500);

        var envelope = new Envelope<TeleportCommand>(command);

        Assert.Same(command, envelope.GetContent());
    }

    [Fact]
    public void A_Record_Without_A_Parameterless_Constructor_Can_Be_Registered()
    {
        var registry = EnvelopeTypeRegistry.Create(builder =>
            builder.Add<TeleportCommand>("Teleport.v1"));

        var envelope = registry.Wrap(new TeleportCommand("Waterdeep", 500));

        Assert.IsType<Envelope<TeleportCommand>>(envelope);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj --filter "FullyQualifiedName~EnvelopeTests"`

Expected: compile failure — `TeleportCommand` does not satisfy the `new()` constraint on `Envelope<TContent>`.

- [ ] **Step 4: Rewrite `EnvelopeOfT.cs`**

Replace the whole file with:

```csharp
namespace JsonEnvelopes;

/// <summary>
/// Concrete implementation of <see cref="Envelope"/> with generic Content.
/// </summary>
public class Envelope<TContent> : Envelope where TContent : class
{
    /// <summary>
    /// Wrap <paramref name="content"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public Envelope(TContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
    }

    /// <summary>
    /// The wrapped content. Assigned at construction and never afterward.
    /// </summary>
    public TContent Content { get; }

    /// <inheritdoc />
    public override string ContentType =>
        typeof(TContent).AssemblyQualifiedName ?? string.Empty;

    /// <inheritdoc />
    public override object GetContent() =>
        Content;
}
```

`ContentType` still returns the assembly-qualified name here. Task 5 changes it to a `Type`; keeping it unchanged now keeps this task's diff to one concern.

- [ ] **Step 5: Drop `new()` from the builder constraint**

In `JsonEnvelopes/EnvelopeTypeRegistryBuilder.cs`, change the signature:

```csharp
    public EnvelopeTypeRegistryBuilder Add<TContent>(string name) where TContent : class
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`

Expected: PASS, 28 tests. If `EnvelopeJsonConverterTests` or `EnvelopeTests` fail to compile because something used the removed parameterless constructor, fix the call to pass content.

- [ ] **Step 7: Commit**

```bash
git add JsonEnvelopes/EnvelopeOfT.cs JsonEnvelopes/EnvelopeTypeRegistryBuilder.cs JsonEnvelopes.Tests/TestModels.cs JsonEnvelopes.Tests/EnvelopeTests.cs
git commit -m "Allow records as envelope content"
```

---

### Task 3: Rewrite `EnvelopeJsonConverter` on the registry

The core of the change. `Type.GetType` is deleted, the wire carries logical names, and the three bugs inside the rewritten method are fixed at the same time.

**Files:**
- Create: `JsonEnvelopes/UnknownContentTypeException.cs`
- Modify: `JsonEnvelopes/EnvelopeJsonConverter.cs` (full rewrite)
- Modify: `JsonEnvelopes/Extensions.cs`
- Modify: `JsonEnvelopes.Tests/EnvelopeJsonConverterTests.cs` (full rewrite)
- Create: `JsonEnvelopes.Tests/SecurityTests.cs`
- Create: `JsonEnvelopes.Tests/WireFormatTests.cs`

**Interfaces:**
- Consumes: `EnvelopeTypeRegistry.TryGetRegistration(string, out EnvelopeRegistration?)`, `EnvelopeTypeRegistry.GetName(Type)`, `EnvelopeRegistration.Factory` from Task 1.
- Produces:
  - `EnvelopeJsonConverter(EnvelopeTypeRegistry registry)`
  - `EnvelopeJsonConverter()` — throws `InvalidOperationException` on use
  - `UnknownContentTypeException : JsonException` with `string ContentTypeName { get; }`

- [ ] **Step 1: Write the failing security test**

Create `JsonEnvelopes.Tests/SecurityTests.cs`:

```csharp
namespace JsonEnvelopes.Tests;

/// <summary>
/// Regression tests for the arbitrary type loading fixed in 3.0.0. Before the registry, the
/// converter called Type.GetType on a value taken straight from the incoming JSON, so the sender
/// chose which type the receiving process loaded.
/// </summary>
public class SecurityTests
{
    [Theory]
    [InlineData("System.Net.WebClient, System.Net.WebClient")]
    [InlineData("System.IO.FileInfo, System.Private.CoreLib")]
    [InlineData("JsonEnvelopes.Tests.BarEntity, JsonEnvelopes.Tests")]
    public void An_Unregistered_Type_Name_Is_Refused_Rather_Than_Loaded(string contentTypeName)
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<FooCommand<BarEntity>>("Foo.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var json = $"{{\"ContentType\":\"{contentTypeName}\",\"Content\":{{}}}}";

        var exception = Assert.Throws<UnknownContentTypeException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, options));

        Assert.Equal(contentTypeName, exception.ContentTypeName);
    }

    [Fact]
    public void An_UnknownContentTypeException_Is_Catchable_As_A_JsonException()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var json = "{\"ContentType\":\"Nope.v1\",\"Content\":{}}";

        Assert.Throws<UnknownContentTypeException>(() =>
        {
            try
            {
                JsonSerializer.Deserialize<Envelope>(json, options);
            }
            catch (JsonException exception)
            {
                throw exception;
            }
        });
    }

    [Fact]
    public void A_Converter_Without_A_Registry_Refuses_To_Read()
    {
        var json = "{\"ContentType\":\"Bar.v1\",\"Content\":{}}";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Deserialize<Envelope>(json));

        Assert.Contains(nameof(EnvelopeTypeRegistry), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Converter_Without_A_Registry_Refuses_To_Write()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Serialize<Envelope>(envelope));

        Assert.Contains(nameof(EnvelopeTypeRegistry), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writing_An_Unregistered_Content_Type_Throws()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var envelope = new Envelope<FooCommand<BarEntity>>(new FooCommand<BarEntity>());

        Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Serialize<Envelope>(envelope, options));
    }
}
```

The third `InlineData` case matters: `BarEntity` is a real, loadable type in the test assembly that is deliberately *not* registered. It proves the allow-list is doing the work, not the type loader failing.

- [ ] **Step 2: Write the failing wire-format test**

Create `JsonEnvelopes.Tests/WireFormatTests.cs`:

```csharp
namespace JsonEnvelopes.Tests;

/// <summary>
/// Golden tests that pin the exact serialized bytes. The JSON is a public contract: deployed
/// consumers parse it, and no other test would catch a change to its shape.
/// </summary>
public class WireFormatTests
{
    private static JsonSerializerOptions NewOptions(JsonNamingPolicy? namingPolicy = null)
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = namingPolicy };
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        return options;
    }

    [Fact]
    public void Default_Options_Produce_Pascal_Case_Envelope_Properties()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var json = JsonSerializer.Serialize<Envelope>(envelope, NewOptions());

        Assert.Equal("{\"ContentType\":\"Bar.v1\",\"Content\":{\"Id\":\"bar1\"}}", json);
    }

    [Fact]
    public void CamelCase_Policy_Applies_To_Envelope_And_Content_Alike()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var json = JsonSerializer.Serialize<Envelope>(envelope, NewOptions(JsonNamingPolicy.CamelCase));

        Assert.Equal("{\"contentType\":\"Bar.v1\",\"content\":{\"id\":\"bar1\"}}", json);
    }

    [Fact]
    public void A_Custom_Naming_Policy_Applies_To_The_Envelope_Properties()
    {
        var envelope = new Envelope<BarEntity>(new BarEntity("bar1"));

        var json = JsonSerializer.Serialize<Envelope>(envelope, NewOptions(new UpperCasePolicy()));

        Assert.Equal("{\"CONTENTTYPE\":\"Bar.v1\",\"CONTENT\":{\"ID\":\"bar1\"}}", json);
    }

    private sealed class UpperCasePolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) =>
            name.ToUpperInvariant();
    }
}
```

The third test is the one that fails today: `Extensions` compares the policy by reference against `JsonNamingPolicy.CamelCase`, so any other policy leaves the envelope properties PascalCase while the content is transformed.

- [ ] **Step 3: Rewrite the converter tests**

Replace `JsonEnvelopes.Tests/EnvelopeJsonConverterTests.cs` entirely:

```csharp
namespace JsonEnvelopes.Tests;

public class EnvelopeJsonConverterTests
{
    private static JsonSerializerOptions NewOptions(bool useCamelCase = false, bool caseInsensitive = false)
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder
            .Add<FooCommand<BarEntity>>("Foo.v1")
            .Add<BarEntity>("Bar.v1"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = caseInsensitive };
        if (useCamelCase)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        return options;
    }

    [Fact]
    public void Constructor_Throws_ArgumentNullException_When_Registry_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new EnvelopeJsonConverter(null!));
    }

    [Fact]
    public void Deserialization_Throws_JsonException_When_Json_Is_Invalid()
    {
        var json = "{\"ContentType\":\"Foo.v1\",\"Content\":\"test\"";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Envelope>(json, NewOptions()));
    }

    [Fact]
    public void Deserialization_Throws_JsonException_Naming_The_Missing_Content_Property()
    {
        var json = "{\"contentType\":\"Foo.v1\",\"data\":\"test\"}";

        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, NewOptions(useCamelCase: true)));

        Assert.Contains("content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialization_Throws_JsonException_Naming_The_Missing_ContentType_Property()
    {
        var json = "{\"content\":\"test\",\"data\":\"test\"}";

        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, NewOptions(useCamelCase: true)));

        Assert.Contains("contentType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialization_Throws_JsonException_When_ContentType_Is_Empty()
    {
        var json = "{\"content\":\"test\",\"contentType\":\"\"}";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Envelope>(json, NewOptions(useCamelCase: true)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Serialization_Then_Deserialization_Produces_An_Equal_Object(bool useCamelCase, bool caseInsensitive)
    {
        var options = NewOptions(useCamelCase, caseInsensitive);
        var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
        var envelope = new Envelope<FooCommand<BarEntity>>(foo);

        var json = JsonSerializer.Serialize<Envelope>(envelope, options);
        var result = JsonSerializer.Deserialize<Envelope>(json, options)?.GetContent() as FooCommand<BarEntity>;

        Assert.NotNull(result);
        Assert.Equal(foo.Id, result!.Id);
        Assert.Equal(foo.Bar!.Id, result.Bar?.Id);
    }

    [Fact]
    public void CamelCase_Json_Is_Readable_When_Case_Insensitivity_Is_Enabled()
    {
        var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
        var json = JsonSerializer.Serialize<Envelope>(
            new Envelope<FooCommand<BarEntity>>(foo),
            NewOptions(useCamelCase: true));

        var result = JsonSerializer.Deserialize<Envelope>(json, NewOptions(caseInsensitive: true));

        Assert.IsType<Envelope<FooCommand<BarEntity>>>(result);
    }

    [Fact]
    public void PascalCase_Json_Is_Readable_When_Case_Insensitivity_Is_Enabled()
    {
        var foo = new FooCommand<BarEntity>(new BarEntity("pdq"), "abc");
        var json = JsonSerializer.Serialize<Envelope>(new Envelope<FooCommand<BarEntity>>(foo), NewOptions());

        var result = JsonSerializer.Deserialize<Envelope>(
            json,
            NewOptions(useCamelCase: true, caseInsensitive: true));

        Assert.IsType<Envelope<FooCommand<BarEntity>>>(result);
    }

    [Fact]
    public void A_Collection_Round_Trips_As_Content()
    {
        var registry = EnvelopeTypeRegistry.Create(builder => builder.Add<BarList>("BarList.v1"));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnvelopeJsonConverter(registry));
        var content = new BarList { Bars = { new BarEntity("one"), new BarEntity("two") } };

        var json = JsonSerializer.Serialize<Envelope>(new Envelope<BarList>(content), options);
        var result = JsonSerializer.Deserialize<Envelope>(json, options)?.GetContent() as BarList;

        Assert.NotNull(result);
        Assert.Equal(2, result!.Bars.Count);
        Assert.Equal("two", result.Bars[1].Id);
    }

    [Fact]
    public void One_Registry_Serves_Concurrent_Deserialization()
    {
        var options = NewOptions();
        var json = JsonSerializer.Serialize<Envelope>(
            new Envelope<FooCommand<BarEntity>>(new FooCommand<BarEntity>(new BarEntity("pdq"), "abc")),
            options);

        Parallel.For(0, 1000, _ =>
        {
            var result = JsonSerializer.Deserialize<Envelope>(json, options);
            Assert.IsType<Envelope<FooCommand<BarEntity>>>(result);
        });
    }
}
```

`BarList` does not exist yet. Append it to `JsonEnvelopes.Tests/TestModels.cs`:

```csharp
public class BarList
{
    public List<BarEntity> Bars { get; set; } = new();
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`

Expected: compile failure — `EnvelopeJsonConverter` has no registry constructor and `UnknownContentTypeException` does not exist.

- [ ] **Step 5: Create the exception**

Create `JsonEnvelopes/UnknownContentTypeException.cs`:

```csharp
namespace JsonEnvelopes;

/// <summary>
/// Thrown when incoming JSON names a content type that is not in the
/// <see cref="EnvelopeTypeRegistry"/>. Derives from <see cref="JsonException"/> so that existing
/// handlers keep working, while a message pump can catch this type alone to dead-letter an
/// unrecognized message without swallowing genuine parse failures.
/// </summary>
public sealed class UnknownContentTypeException : JsonException
{
    /// <summary>
    /// Create the exception for <paramref name="contentTypeName"/>.
    /// </summary>
    public UnknownContentTypeException(string contentTypeName)
        : base($"'{contentTypeName}' is not a registered content type name.") =>
        ContentTypeName = contentTypeName;

    /// <summary>
    /// The unrecognized name that appeared on the wire.
    /// </summary>
    public string ContentTypeName { get; }
}
```

- [ ] **Step 6: Rewrite the converter**

Replace `JsonEnvelopes/EnvelopeJsonConverter.cs` entirely:

```csharp
namespace JsonEnvelopes;

/// <summary>
/// Reads and writes <see cref="Envelope"/> instances, resolving content types through an
/// <see cref="EnvelopeTypeRegistry"/> allow-list. Add a configured instance to
/// <see cref="JsonSerializerOptions.Converters"/>; a converter built without a registry throws.
/// </summary>
public class EnvelopeJsonConverter : JsonConverter<Envelope>
{
    private readonly EnvelopeTypeRegistry? _registry;

    /// <summary>
    /// Create a converter with no registry. This constructor exists only because the
    /// <see cref="JsonConverterAttribute"/> on <see cref="Envelope"/> requires one; every read and
    /// write on the resulting instance throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public EnvelopeJsonConverter()
    { }

    /// <summary>
    /// Create a converter that resolves content types through <paramref name="registry"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public EnvelopeJsonConverter(EnvelopeTypeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    /// <inheritdoc />
    /// <exception cref="UnknownContentTypeException">
    /// The JSON names a content type that is not registered.
    /// </exception>
    public override Envelope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var registry = RequireRegistry();
        var contentTypePropertyName = options.GetContentTypePropertyName();
        var contentPropertyName = options.GetContentPropertyName();

        if (!JsonDocument.TryParseValue(ref reader, out var document))
        {
            throw new JsonException("The envelope JSON could not be parsed.");
        }

        using (document)
        {
            if (!TryGetProperty(document.RootElement, contentTypePropertyName, options, out var contentTypeElement))
            {
                throw new JsonException($"The envelope JSON has no '{contentTypePropertyName}' property.");
            }
            if (!TryGetProperty(document.RootElement, contentPropertyName, options, out var contentElement))
            {
                throw new JsonException($"The envelope JSON has no '{contentPropertyName}' property.");
            }

            var contentTypeName = contentTypeElement.GetString();
            if (string.IsNullOrEmpty(contentTypeName))
            {
                throw new JsonException($"The envelope JSON has an empty '{contentTypePropertyName}' property.");
            }
            if (!registry.TryGetRegistration(contentTypeName, out var registration))
            {
                throw new UnknownContentTypeException(contentTypeName);
            }

            var content = contentElement.Deserialize(registration!.ContentType, options) ??
                          throw new JsonException($"The '{contentPropertyName}' property deserialized to null.");

            return registration.Factory(content);
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The content's type is not registered.</exception>
    public override void Write(Utf8JsonWriter writer, Envelope value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        var registry = RequireRegistry();
        var content = value.GetContent();

        writer.WriteStartObject();
        writer.WriteString(options.GetContentTypePropertyName(), registry.GetName(content.GetType()));
        writer.WritePropertyName(options.GetContentPropertyName());
        JsonSerializer.Serialize(writer, content, options);
        writer.WriteEndObject();
    }

    private EnvelopeTypeRegistry RequireRegistry() =>
        _registry ??
        throw new InvalidOperationException(
            $"This {nameof(EnvelopeJsonConverter)} has no {nameof(EnvelopeTypeRegistry)}. Build a " +
            $"registry and add new {nameof(EnvelopeJsonConverter)}(registry) to " +
            $"{nameof(JsonSerializerOptions)}.{nameof(JsonSerializerOptions.Converters)}.");

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        JsonSerializerOptions options,
        out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }
        if (!options.PropertyNameCaseInsensitive || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
```

`Write` resolves the name from `content.GetType()`. Task 5 changes that line to `value.ContentType` once `ContentType` is a `Type`.

- [ ] **Step 7: Fix and hide `Extensions`**

Replace `JsonEnvelopes/Extensions.cs` entirely. **The assembly attribute on line 3 must survive** — it strong-names the assembly:

```csharp
using System.Reflection;

[assembly: AssemblyKeyFile("JsonEnvelopes.snk")]

namespace JsonEnvelopes;

internal static class Extensions
{
    internal static string GetContentPropertyName(this JsonSerializerOptions jsonOptions) =>
        ConvertName(jsonOptions, nameof(Envelope<object>.Content));

    internal static string GetContentTypePropertyName(this JsonSerializerOptions jsonOptions) =>
        ConvertName(jsonOptions, nameof(Envelope<object>.ContentType));

    private static string ConvertName(JsonSerializerOptions jsonOptions, string propertyName) =>
        jsonOptions.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
}
```

`ConvertName` replaces the reference comparison against `JsonNamingPolicy.CamelCase`, so every naming policy now applies to the envelope properties.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`

Expected: PASS. The solution will not build until Task 4 fixes `JsonEnvelopes.Example`, so run the test project directly in this step:

Run: `dotnet build JsonEnvelopes/JsonEnvelopes.csproj` — expected: 0 errors.

- [ ] **Step 9: Verify the assembly is still strong-named**

Run: `dotnet build JsonEnvelopes/JsonEnvelopes.csproj --configuration Release`

Then:

```bash
pwsh -NoProfile -Command "[System.Reflection.AssemblyName]::GetAssemblyName('JsonEnvelopes/bin/Release/net6.0/JsonEnvelopes.dll').FullName"
```

Expected: the output contains `PublicKeyToken=e6175e4a801927a0`. If it says `PublicKeyToken=null`, the `AssemblyKeyFile` attribute was lost in Step 7 — restore it.

- [ ] **Step 10: Commit**

```bash
git add JsonEnvelopes/UnknownContentTypeException.cs JsonEnvelopes/EnvelopeJsonConverter.cs JsonEnvelopes/Extensions.cs JsonEnvelopes.Tests/EnvelopeJsonConverterTests.cs JsonEnvelopes.Tests/SecurityTests.cs JsonEnvelopes.Tests/WireFormatTests.cs JsonEnvelopes.Tests/TestModels.cs
git commit -m "Resolve envelope content types through an allow-list"
```

---

### Task 4: `ContentType` becomes a `Type`, and `WrapContent` moves

Removes the last two pieces of the old design from the object model, and repairs `JsonEnvelopes.Example` so the solution builds again.

**Files:**
- Modify: `JsonEnvelopes/Envelope.cs`
- Modify: `JsonEnvelopes/EnvelopeOfT.cs`
- Modify: `JsonEnvelopes/EnvelopeJsonConverter.cs:Write` (one line)
- Modify: `JsonEnvelopes.Tests/EnvelopeTests.cs`
- Modify: `JsonEnvelopes.Example/Program.cs`
- Modify: `JsonEnvelopes.Example/Services/CommandDispatcher.cs`
- Modify: `JsonEnvelopes.Example/_GlobalUsings.cs`

**Interfaces:**
- Consumes: `EnvelopeTypeRegistry.Wrap(object)` from Task 1, `EnvelopeJsonConverter(EnvelopeTypeRegistry)` from Task 3.
- Produces: `Envelope.ContentType` → `Type`. `Envelope.WrapContent` no longer exists.

- [ ] **Step 1: Write the failing test**

In `JsonEnvelopes.Tests/EnvelopeTests.cs`, delete `WrapContent_Test` (the registry now owns wrapping, and Task 1 already covers `Wrap`) and add:

```csharp
    [Fact]
    public void ContentType_Is_The_Content_Clr_Type()
    {
        var envelope = new Envelope<FooCommand<BarEntity>>(new FooCommand<BarEntity>());

        Assert.Equal(typeof(FooCommand<BarEntity>), envelope.ContentType);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj --filter "FullyQualifiedName~ContentType_Is_The_Content_Clr_Type"`

Expected: compile failure — cannot compare `string` to `Type`.

- [ ] **Step 3: Change `Envelope`**

Replace `JsonEnvelopes/Envelope.cs` entirely:

```csharp
namespace JsonEnvelopes;

/// <summary>
/// Abstract base class for <see cref="Envelope{TContent}"/> utilizing a custom JsonConverter.
/// </summary>
[JsonConverter(typeof(EnvelopeJsonConverter))]
public abstract class Envelope
{
    /// <summary>
    /// The CLR type of the wrapped content. To obtain the logical name this type carries on the
    /// wire, call <see cref="EnvelopeTypeRegistry.GetName"/>.
    /// </summary>
    public abstract Type ContentType { get; }

    /// <summary>
    /// The wrapped content.
    /// </summary>
    public abstract object GetContent();
}
```

`WrapContent` is gone. Consumers call `registry.Wrap(content)`.

- [ ] **Step 4: Change `Envelope<TContent>.ContentType`**

In `JsonEnvelopes/EnvelopeOfT.cs`, replace the `ContentType` property:

```csharp
    /// <inheritdoc />
    public override Type ContentType =>
        typeof(TContent);
```

- [ ] **Step 5: Point `Write` at the declared type**

In `JsonEnvelopes/EnvelopeJsonConverter.cs`, inside `Write`, change:

```csharp
        writer.WriteString(options.GetContentTypePropertyName(), registry.GetName(content.GetType()));
```

to:

```csharp
        writer.WriteString(options.GetContentTypePropertyName(), registry.GetName(value.ContentType));
```

The declared content type is what was registered, so this is the deterministic choice: an `Envelope<TBase>` holding a derived instance serializes under the base's registered name rather than failing on an unregistered derived type.

- [ ] **Step 6: Fix `CommandDispatcher`**

Replace `JsonEnvelopes.Example/Services/CommandDispatcher.cs` entirely. Note the file-scoped namespace and the removal of the local `using` directives, which `_GlobalUsings.cs` already covers:

```csharp
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
```

`Type.GetType` and the null-forgiving operator are both gone.

`JsonEnvelopes.Example/_GlobalUsings.cs` already declares `Microsoft.Extensions.DependencyInjection` and `JsonEnvelopes.Example.Handlers`, so no global using needs adding for this file.

- [ ] **Step 7: Fix `Program`**

In `JsonEnvelopes.Example/Program.cs`, replace the body of `Main` down to the `await using var serviceProvider` line with:

```csharp
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
```

Then change the two dispatch lines to drop the now-unneeded null handling:

```csharp
            var mediatrTasks = envelopes.Select(envelope => mediator.Send(envelope.GetContent()));
```

```csharp
            var dispatcherTasks = envelopes.Select(envelope => dispatcher.DispatchAsync(envelope));
```

Then register the registry as a singleton, so a handler could take a dependency on it. Change the signature of `NewServiceProvider` and its first line:

```csharp
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
```

and pass the registry at the call site:

```csharp
            await using var serviceProvider = NewServiceProvider(registry);
```

Also delete the unused `TimingTests` method. It has no caller, its measurement lines are commented out, and the reflection it was timing no longer exists. Deleting it leaves `global using System.Diagnostics;` in `JsonEnvelopes.Example/_GlobalUsings.cs` unused — remove that line too, since `Stopwatch` was its only consumer.

- [ ] **Step 8: Build the whole solution**

Run: `dotnet build JsonEnvelopes.sln`

Expected: 0 errors. Warnings about `net6.0` being out of support are pre-existing and expected.

- [ ] **Step 9: Run the whole suite**

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`

Expected: PASS, roughly 36 tests.

- [ ] **Step 10: Run the example end to end**

Run: `dotnet run --project JsonEnvelopes.Example/JsonEnvelopes.Example.csproj`

Expected: both handler sections print. The program waits on `Console.ReadKey()` at the end, so send it a keypress or terminate it once the output appears.

- [ ] **Step 11: Commit**

```bash
git add JsonEnvelopes/Envelope.cs JsonEnvelopes/EnvelopeOfT.cs JsonEnvelopes/EnvelopeJsonConverter.cs JsonEnvelopes.Tests/EnvelopeTests.cs JsonEnvelopes.Example/Program.cs JsonEnvelopes.Example/Services/CommandDispatcher.cs
git commit -m "Expose the content CLR type and move wrapping to the registry"
```

---

### Task 5: Version 3.0.0, readme, and migration guide

Documentation and packaging metadata. No behavior changes.

**Files:**
- Modify: `JsonEnvelopes/JsonEnvelopes.csproj`
- Modify: `JsonEnvelopes.nuspec`
- Modify: `readme.md`

**Interfaces:**
- Consumes: the complete public surface from Tasks 1-4.
- Produces: nothing code-facing.

- [ ] **Step 1: Confirm the four version occurrences**

Run: `grep -rn "2\.0\.1" readme.md JsonEnvelopes.nuspec JsonEnvelopes/JsonEnvelopes.csproj`

Expected: four lines — `<AssemblyVersion>`, `<version>`, and two in `readme.md`.

- [ ] **Step 2: Bump all four to 3.0.0**

- `JsonEnvelopes/JsonEnvelopes.csproj`: `<AssemblyVersion>3.0.0</AssemblyVersion>`
- `JsonEnvelopes.nuspec`: `<version>3.0.0</version>`
- `readme.md`: `(e.g. version 3.0.0)` and `dotnet add package JsonEnvelopes --version 3.0.0`

- [ ] **Step 3: Rewrite the readme usage section**

Replace everything between the `## Usage` heading and the `## Examples` heading with:

````markdown
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

Call sites that relied on `Type.GetType(envelope.ContentType)` simplify to `envelope.ContentType`.
````

- [ ] **Step 4: Verify the readme's examples compile**

Read the rewritten section and check every type and member name against the code from Tasks 1-4: `EnvelopeTypeRegistry.Create`, `EnvelopeTypeRegistryBuilder.Add<T>`, `EnvelopeJsonConverter(registry)`, `registry.Wrap`, `Envelope.ContentType`, `Envelope.GetContent`, `UnknownContentTypeException.ContentTypeName`. Fix any mismatch.

- [ ] **Step 5: Build and test once more**

Run: `dotnet build JsonEnvelopes.sln`

Run: `dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj`

Expected: 0 errors, all tests pass.

- [ ] **Step 6: Commit**

```bash
git add readme.md JsonEnvelopes.nuspec JsonEnvelopes/JsonEnvelopes.csproj
git commit -m "Document the registry and bump to 3.0.0"
```

---

## Finishing

Do not push. Pushing `main` runs `nuget push` to nuget.org, and this branch changes the version, so a push would publish 3.0.0 for real.

Land the branch with the `wrap-up` skill, which updates `status.md` — it should close findings 1, 2, 4, 5, 6, 7, 8, and 10, record the new wire format as an invariant, and note that findings 3, 9, 11, 12, 13, and 15 remain open.
