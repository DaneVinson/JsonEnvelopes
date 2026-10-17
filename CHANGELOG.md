# Changelog

All notable changes to the `JsonEnvelopes` package are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses
[Semantic Versioning](https://semver.org/). Dates are the nuget.org publish dates.

## [3.0.0] - 2026-10-16

A breaking change. The wire format is not compatible with 2.x: 3.0.0 cannot read messages
that 2.x wrote, so drain any queue before you upgrade. The readme's
[Migrating from 2.x](readme.md#migrating-from-2x) section maps each old call to its replacement.

### Security

- The converter no longer loads a .NET type from a name that the sender puts on the wire. It
  resolves content types only through an `EnvelopeTypeRegistry` allow-list that you build. An
  unregistered name throws `UnknownContentTypeException`.

### Added

- `EnvelopeTypeRegistry` and `EnvelopeTypeRegistryBuilder`. Register each content type under a
  logical name with `EnvelopeTypeRegistry.Create(builder => builder.Add<T>("Name.v1"))`.
- `EnvelopeTypeRegistry.Wrap`, `GetName`, and `TryGetName`.
- `UnknownContentTypeException`, which derives from `JsonException` and carries the
  `ContentTypeName` that was refused.
- A `net10.0` build that uses the in-box `System.Text.Json` and has no package dependency.
- XML documentation, the readme, and a symbol package with SourceLink in the package.

### Changed

- The wire carries the logical name you registered, not an `AssemblyQualifiedName`.
- `Envelope.ContentType` is a `Type`, not a `string`.
- `EnvelopeJsonConverter` needs a registry: add `new EnvelopeJsonConverter(registry)` to
  `JsonSerializerOptions.Converters`.
- `Envelope<TContent>` accepts any reference type, including positional records. The `new()`
  constraint is gone. Abstract classes and interfaces are refused at registration.
- The `netstandard2.0` build depends on `System.Text.Json` 10.0.0, up from 4.7.2 in 1.x. The
  package supports .NET Framework 4.7.2 and later, and .NET Core 2.0 and later.
- Serializing an envelope as its concrete `Envelope<T>` type now throws
  `NotSupportedException`. Serialize it as `Envelope`.

### Removed

- `Envelope.WrapContent`. Use `EnvelopeTypeRegistry.Wrap`.
- The parameterless `Envelope<T>` constructor. Use `new Envelope<T>(content)`.
- The public `Extensions` class. It is now `internal`.

### Fixed

- `PropertyNameCaseInsensitive` and every `PropertyNamingPolicy` now apply to the envelope's
  own property names, not only `JsonNamingPolicy.CamelCase`.
- A null content argument throws `ArgumentNullException`. Before, `WrapContent(null)` threw
  `NullReferenceException`, and `new Envelope<T>(null)` silently used an empty `T`.
- `Read` now disposes its `JsonDocument` on the success path, so pooled buffers are returned.
- The `LICENSE` file has the MIT copyright notice.

## [2.0.1] - 2022-05-14

### Fixed

- The package declares its `net6.0` dependency group.

## [2.0.0] - 2022-05-14

### Changed

- The library targets `net6.0` and uses its in-box `System.Text.Json`. It no longer targets
  `netstandard2.0`.

## [1.3.1] - 2021-05-08

### Changed

- `Envelope.WrapContent` returns `Envelope`, not `Envelope?`.

## [1.3.0] - 2021-05-08

### Added

- `Envelope.WrapContent`, which wraps an object in the `Envelope<T>` of its runtime type.

## [1.2.0] - 2021-02-04

### Changed

- An unknown content type throws `TypeLoadException` during deserialization.
- The license is MIT. Earlier versions were GPL-3.0.

## [1.1.0] - 2020-11-11

### Changed

- The assembly is strong-named with `PublicKeyToken=e6175e4a801927a0`. Version 1.0.0 was the
  first signed build and is unlisted on nuget.org.

## [0.5.0 – 0.6.3] - 2020-10-24 to 2020-11-01

Initial releases for `netstandard2.0` with `System.Text.Json` 4.7.2.
