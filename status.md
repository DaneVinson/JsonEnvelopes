# JsonEnvelopes — Project Status

**Last updated:** 2026-09-18
**Phase:** 3.0.0 is built, tested and merged to local `main`, but **not published**.
Version 2.0.1 remains the latest on nuget.org.

Companion to [readme.md](readme.md) and [CLAUDE.md](CLAUDE.md). The readme records
*how to use the package*. CLAUDE.md records *the conventions to write code by*.
This file records *where we are, how we got here, and what would break if we
changed our minds* — the things neither of the other two carries.

---

## 1. Where we are

| | |
| --- | --- |
| Repo | on `main`; [GitHub](https://github.com/DaneVinson/JsonEnvelopes) `origin/main` is at the .gitignore commit `0d2816c`, and local `main` is **1 commit ahead** (the documentation commit, unpushed) |
| Published package | **2.0.1** on nuget.org, `net6.0` only — unchanged |
| Version in the tree | **3.0.0**, a breaking change, **unpublished** |
| Library code | 7 files. `Envelope`, `Envelope<TContent>`, `EnvelopeJsonConverter`, `EnvelopeTypeRegistry`, `EnvelopeTypeRegistryBuilder`, `UnknownContentTypeException`, `Extensions` |
| Tests | **53 passing** as of 2026-09-18, up from 12 |
| Target framework | **`net6.0`, out of support since November 2024** — build emits `NETSDK1138` |
| Review backlog | 15 findings from 2026-09-17; **8 closed by 3.0.0**, 7 open. See Section 5 |
| Docs | `readme.md` rewritten for 3.0.0 with a migration guide; `CLAUDE.md`, this file, and the `wrap-up` skill added 2026-09-17 |

## 2. Repo state

Three projects in `JsonEnvelopes.sln`:

- **`JsonEnvelopes/`** — the shipped library. `Envelope` (abstract base, carries the
  `[JsonConverter]` attribute), `Envelope<TContent>`, `EnvelopeJsonConverter`,
  `EnvelopeTypeRegistry`, `EnvelopeTypeRegistryBuilder`,
  `UnknownContentTypeException`, and the now-`internal` `Extensions`.
- **`JsonEnvelopes.Tests/`** — xunit. 53 tests across `EnvelopeTests`,
  `EnvelopeJsonConverterTests`, `EnvelopeTypeRegistryTests`, `SecurityTests`, and
  `WireFormatTests`.
- **`JsonEnvelopes.Example/`** — console app demonstrating dispatch through both
  MediatR and a hand-rolled `CommandDispatcher`. Not packable.

Local `main` carries the merged 3.0.0 work and is **well ahead of `origin/main`**,
which sits at the documentation commit `95c6d62`. Earlier documentation commits were
pushed on 2026-09-17 from outside the session that wrote them; version 2.0.1 was
unchanged at the time, so the pipeline's `nuget push` step is expected to have failed
on a duplicate — see finding 11.

**Pushing `main` now would publish 3.0.0 to nuget.org irreversibly**, because the
version in the tree did change. That is a release decision, not a side effect of
landing work — see Section 4.

No git tags exist, so published version 2.0.1 cannot be mapped to a commit.

History shows work landing through GitHub pull requests (`#1`, `#2`, `#3`). The
`wrap-up` skill added on 2026-09-17 is the local-merge path and says so; it is not
a replacement for review on GitHub when a change warrants it.

## 3. Decisions made, and what we rejected

**`.gitignore` scope for Claude Code state (2026-09-17).** Ignored only
per-developer files: `.claude/settings.local.json`, `.claude/*.local.json`,
`.claude/scratchpad/`, `.claude/todos/`, `.claude/shell-snapshots/`,
`.claude/statsig/`, and `CLAUDE.local.md`. Deliberately **not** ignored: the whole
`.claude/` directory, because `.claude/settings.json`, `.claude/commands/`, and
`.claude/skills/` are meant to be committed and shared. Also left tracked:
`.mcp.json`, which Anthropic intends to be checked in. A comment in `.gitignore`
records this so the pattern does not get widened to `.claude/` later by mistake.

**`CLAUDE.md` content (2026-09-17).** The C# conventions section is a verbatim copy
of the shared source at `E:\My Drive\Development\Claude\claude_md\csharp-conventions.md`.
Deliberately **not** done: inventing repo-specific conventions. A short "Project
Notes" section was added for the three facts that override the generic rules here —
`Directory.Build.props` already exists with `LangVersion` 10.0, the global usings
files already exist, and some code predates the conventions.

**Scope of the 2026-09-17 review.** The review was advisory. **No code was
changed.** Every finding in Section 5 is still open. This matters because the
findings are ranked by consequence, not by effort, and the top one is a security
issue.

**Allow-list type resolution, shipped as 3.0.0 (2026-09-18).** Findings 1 and 2 were
fixed together, because both came from one cause: letting the wire decide what a .NET
type is. Design in
[the spec](docs/superpowers/specs/2026-09-17-allow-list-type-resolution-design.md),
execution in [the plan](docs/superpowers/plans/2026-09-17-allow-list-type-resolution.md).
Deliberately **rejected**: a compatibility mode that still read 2.x envelopes, because
gating `Type.GetType` behind a flag leaves the unsafe path in the product; and a static
global registry, which would have kept the old zero-setup call site but cannot host two
message vocabularies in one process and leaks registrations between tests. Consequence:
**3.0.0 cannot read messages written by 2.x**, so a queue has to be drained before an
upgrade. That is stated in the readme's migration section.

**Logical names are opaque (2026-09-18).** The library never parses, derives, or
validates the name a type is registered under. The `.v1` suffix in the docs is a
suggestion. Rejected: deriving names from `Type.Name` or `Type.FullName`, which would
have reintroduced a silent coupling to namespaces; and an attribute on the message
type, which would force contract assemblies to reference this package.

**No reflection at all, rather than cached reflection (2026-09-18).** The spec only
asked for a cached factory. Because `Add<TContent>` is generic, the factory closes over
the type directly as `content => new Envelope<TContent>((TContent)content)`, so
`MakeGenericType`, `Activator`, and compiled expressions are all unnecessary. This is
why finding 8 is fully closed and why finding 9 is now achievable.

**`status.md` was seeded deliberately, not bootstrapped (2026-09-17).** The
alternative was letting the `wrap-up` skill create this file on its first run.
Rejected because the first wrap-up would have shaped the structure around whatever
that branch happened to touch. The skill maintains this file; it does not define it.

## 4. Load-bearing invariants

- **Pushing `main` publishes a NuGet package.** `azure-pipelines.yml` packs and runs
  `nuget push` to nuget.org on every `main` commit. The push has no
  `--skip-duplicate`, so it **fails the pipeline** when the version is unchanged and
  **publishes irreversibly** when it is changed. Releasing is a deliberate act, never
  a side effect of landing work.
- **The package version lives in four places across three files.** `<AssemblyVersion>`
  in `JsonEnvelopes/JsonEnvelopes.csproj`, `<version>` in `JsonEnvelopes.nuspec`, and
  twice in `readme.md` (the prose version and the `dotnet add package` line). They
  move together or not at all.
- **The serialized JSON is a public contract.** Property names, casing, ordering, and
  the `ContentType` value are what deployed consumers parse. `WireFormatTests` now pins
  the exact bytes in **both directions** — golden writes and golden reads from
  hard-coded JSON. The read tests exist because write-only goldens would let a
  symmetric change to both the writer and the reader pass green while breaking senders
  already deployed.
- **Nothing resolves a type name supplied by the sender.** `Type.GetType` appears
  nowhere under `JsonEnvelopes/`. The only route from wire data to a type is a lookup
  in `EnvelopeTypeRegistry`, whose entries are `typeof(TContent)` captured at
  registration. Reintroducing any name-to-type resolution reopens finding 1.
- **Registered names match ordinally.** `PropertyNameCaseInsensitive` affects property
  names only, never content type names. A test pins this.
- **The name on the wire comes from the DECLARED content type.** `Write` serializes the
  payload against `value.ContentType`, not the runtime type, so an `Envelope<TBase>`
  holding a derived instance emits the base's registered name and the base's properties.
  Serializing against the runtime type instead would emit one type's name beside another
  type's body, and the reader would silently drop the extra fields.
- **Serialize as `Envelope`, never as `Envelope<T>`.** The `[JsonConverter]` attribute
  on the abstract base does not apply to the derived generic, so the concrete static
  type bypasses the converter entirely. Since `ContentType` is a `Type`, that path now
  throws `NotSupportedException` rather than emitting a different shape. A test pins it.
- **`Nullable`, `ImplicitUsings`, and `LangVersion` come from `Directory.Build.props`.**
  Never set them in an individual `.csproj`.
- **`TreatWarningsAsErrors` is not set.** A new nullability or obsolete-API warning
  passes the build. Warnings have to be read, not assumed absent.
- **Every project uses a `_GlobalUsings.cs`.** New namespaces go there, never as a
  local `using` at the top of a `.cs` file.

## 5. Known risks and review backlog

From the review of 2026-09-17, verified by building the library, running the suite,
and probing runtime behavior with a scratch project. Ranked by consequence.

**Findings 1, 2, 4, 5, 6, 7, 8 and 10 were CLOSED by 3.0.0** on 2026-09-18 and are
struck through below, with the evidence that closed each one. Findings 3, 9, 11, 12,
13 and 15 remain open, and 14 is partly closed. They stay here because each is still
true of the tree.

**1. ~~Arbitrary type loading from untrusted JSON~~ — CLOSED in 3.0.0.**
*Evidence: `Type.GetType` is absent from `JsonEnvelopes/`; the original `System.Net.WebClient` payload is refused with `UnknownContentTypeException`; `SecurityTests` pins it. Original finding below.*

`Type.GetType(contentTypeString)` in `EnvelopeJsonConverter.Read` resolves any type
name a sender puts on the wire, with no allow-list. Verified: the input
`{"ContentType":"System.Net.WebClient, System.Net.WebClient","Content":{}}`
loaded `System.Net.WebClient`. This is the same design as Newtonsoft's
`TypeNameHandling.All`, which Microsoft documents as unsafe and which
`System.Text.Json` deliberately omits. `System.Text.Json` limits the blast radius
versus `BinaryFormatter` — no arbitrary constructor calls, only property setters —
but static constructors run and setter-based gadget chains exist. The readme does
not warn about it. **Fix:** make type resolution a consumer-supplied policy that
defaults to an empty allow-list. Registering short logical names
(`"CastFireball"` → `typeof(CastFireball)`) fixes finding 2 at the same time.

**2. ~~The `AssemblyQualifiedName` wire contract is brittle~~ — CLOSED in 3.0.0.**
*Evidence: the wire now carries the logical name you registered, e.g. `{"ContentType":"CastFireball.v1",...}`, with no assembly, version, or namespace in it.*
 A rename, a namespace
move, or a version bump makes messages already in a queue undeliverable, and it
couples two services through a .NET implementation detail — the opposite of the
"agnostic with respect to message type" goal stated in the readme. **Fix:** the
registered-logical-name resolver above, plus a schema-version field.

**3. `net6.0` is out of support.** Out of support since November 2024; the build
emits `NETSDK1138`. There is no `netstandard2.0` target, so .NET Framework consumers
cannot use the package at all. **Fix:** multi-target `net8.0;net10.0;netstandard2.0`.

**4. ~~`PropertyNameCaseInsensitive` is silently ignored~~ — CLOSED in 3.0.0.**
*Evidence: `Extensions` now calls `PropertyNamingPolicy.ConvertName`, and `Read` falls back to an ordinal-ignore-case scan when the option is set. Cross-configuration round-trip tests cover it.*
 `Extensions` compares the
naming policy by reference against `JsonNamingPolicy.CamelCase` and hardcodes two
strings; `Read` then uses case-sensitive `TryGetProperty`. Verified: JSON written
with camelCase and read with `PropertyNameCaseInsensitive = true` throws
`JsonException`. Any policy other than `CamelCase` is also ignored, producing a
document where the envelope keys stay PascalCase while the payload is transformed.
The existing tests miss this because every test uses the same options object for
both directions. **Fix:** call `PropertyNamingPolicy.ConvertName`, and match
case-insensitively in `Read` when the option is set.

**5. ~~`JsonDocument` is never disposed on the success path~~ — CLOSED in 3.0.0.**
*Evidence: `Read` wraps the document in `using`, covering every throw branch and the deserialize call.*
 `Read` disposes it in
each throw branch but not on success, so pooled `ArrayPool` buffers are never
returned — on the library's hot path. **Fix:** `using var document`.

**6. ~~Null handling is wrong in both directions~~ — CLOSED in 3.0.0.**
*Evidence: `Wrap` and the `Envelope<TContent>` constructor both use `ArgumentNullException.ThrowIfNull`; the silent `new TContent()` substitution is gone with the parameterless constructor.*
 Verified: `Envelope.WrapContent(null)`
throws `NullReferenceException` from `content.GetType()`, and `new Envelope<T>(null)`
silently substitutes an empty `new TContent()`, turning a caller bug into a
valid-looking message carrying empty data. **Fix:** `ArgumentNullException.ThrowIfNull`
in both.

**7. ~~The `class, new()` constraint excludes modern message types~~ — CLOSED in 3.0.0.**
*Evidence: the constraint is `where TContent : class`; a positional record is registered and round-tripped in the suite. Structs remain out of scope by design. Abstract classes and interfaces are now rejected at registration rather than failing on the first message.*
 Positional
records, immutable classes with `[JsonConstructor]`, and structs are all supported by
`System.Text.Json` but rejected here. Via `WrapContent`, the constraint is enforced by
`MakeGenericType` at runtime, so an incompatible record fails with an opaque
`ArgumentException` rather than a compile error. **Fix:** drop `new()` — a breaking
change, so 3.0.

**8. ~~Reflection on every message with no caching~~ — CLOSED in 3.0.0.**
*Evidence: reflection was removed rather than cached — see Section 3. `TimingTests` was deleted.*
 `Read` calls `Type.GetType`,
`MakeGenericType`, and `Activator.CreateInstance` per deserialize. **Fix:** cache a
factory delegate per type. Note `TimingTests` in `JsonEnvelopes.Example/Program.cs`
is dead code with commented-out lines and no caller, so its numbers were never
published — replace it with BenchmarkDotNet.

**9. No trimming or NativeAOT support.** The reflection approach is incompatible with
source-generated serialization, trimming, and NativeAOT, and there are no
`RequiresUnreferencedCode` or `DynamicallyAccessedMembers` annotations, so consumers
get no warning — their app fails at runtime after publishing trimmed.

**10. ~~`Extensions` is public API that should not be~~ — CLOSED in 3.0.0.**
*Evidence: the class is `internal`.*
 `GetContentPropertyName` and
`GetContentTypePropertyName` appear in IntelliSense on every `JsonSerializerOptions`
in any consuming project. **Fix:** make the class `internal`.

**11. Packaging is hand-rolled and incomplete.** A `.nuspec` with a hardcoded
`bin\Release\net6.0\` path instead of `dotnet pack`; no `<Version>`, no
`<PackageReadmeFile>`, no `<GenerateDocumentationFile>` (so the XML doc comments never
reach consumers), no symbol package, no SourceLink, no deterministic build, no
`--skip-duplicate`, no tags. `coverlet.collector` is referenced but no coverage is
collected or published.

**12. The LICENSE has no copyright notice.** It is the MIT body text with the
`MIT License` heading and the `Copyright (c) <year> <holder>` line removed — while the
text itself requires that notice be included in all copies. This makes the grant
ambiguous.

**13. The private signing key is committed.** `JsonEnvelopes/JsonEnvelopes.snk` is
tracked and the `*.snk` rule at `.gitignore:228` is commented out. Signing does work
(the built assembly carries `PublicKeyToken=e6175e4a801927a0`), but anyone can now
produce an assembly with this exact strong-name identity. Strong names are not a
security boundary, so this is minor, but it defeats the identity purpose. Removing the
file would not remove it from history. The readme example still shows
`PublicKeyToken=null`, which is stale. Also, the legacy `[assembly: AssemblyKeyFile]`
sits in `Extensions.cs` rather than in the `.csproj` as `<SignAssembly>`.

**14. Test coverage — mostly closed, partly open.** Closed in 3.0.0: golden
write AND read tests on the exact bytes, cross-configuration round-trips, collections
as content, null arguments, direct `Envelope<T>` serialization, custom naming policies,
and concurrent reads. 53 tests, up from 12. Still open: no concurrent-`Write` test,
and test dependencies are from 2022 (xunit 2.4.1, Test.Sdk 17.2.0).

**15. Repository hygiene.** No CHANGELOG, CONTRIBUTING, issue or PR templates, CI
badge, Dependabot, analyzer enforcement, or security policy. `Console.ReadKey()` in
`JsonEnvelopes.Example/Program.cs` still makes the example hang in CI. Partly closed:
`CommandDispatcher.cs` was rewritten with a file-scoped namespace and no redundant
local `using` directives. A CHANGELOG matters more now that a breaking version exists.

## 6. Working agreements

- Implementation work happens on a feature branch and lands on local `main` through
  the `wrap-up` skill. **Pushing to `origin` happens only when Dane asks**, because
  pushing `main` publishes to nuget.org.
- The C# conventions in `CLAUDE.md` apply to new and edited code. Untouched files are
  not reformatted as a side effect of an unrelated change.
- A version bump is a release decision for Dane, never an automatic consequence of a
  code change.
- Findings in Section 5 are advisory until Dane picks one up. Do not start fixing them
  opportunistically; the open ones are not blocking anything.
- **3.0.0 is built but unpublished.** It reaches nuget.org only when Dane decides to
  push. Do not push `main` to make CI green or to "finish" the release.

## 7. Toolchain decisions

| | Decision |
| --- | --- |
| Target framework | **`net6.0`** — out of support; see finding 3 |
| Language version | **C# 10**, pinned in `Directory.Build.props` as `<LangVersion>10.0</LangVersion>` |
| Nullable / ImplicitUsings | **enabled** in `Directory.Build.props`, never per-project |
| Local SDK | 10.0.401 is the only SDK installed; it builds `net6.0` fine but warns `NETSDK1138` |
| Tests | **xunit** 2.4.1, with `coverlet.collector` referenced but unused; 53 tests |
| Type resolution | **consumer-supplied allow-list** via `EnvelopeTypeRegistry`; no reflection, no `Type.GetType` |
| Packaging | **hand-written `JsonEnvelopes.nuspec`** with `nuget pack`, not `dotnet pack`; see finding 11 |
| CI | **Azure Pipelines** (repo stays on GitHub), `windows-latest`: restore, build, test, pack, push to nuget.org on `main` |
| Signing | strong-named via `[assembly: AssemblyKeyFile]` in `Extensions.cs`; key committed; see finding 13 |
| Line endings | **CRLF** throughout the repo |

## 8. Traps

Each cost time on 2026-09-17. Recorded as the trap, its symptom, and the check that
catches it next time.

- **Appending LF lines to a CRLF file.** Symptom: git reports
  `LF will be replaced by CRLF the next time Git touches it`, and a later normalization
  shows the whole file as changed. Check: after writing, confirm
  `count(b"\n") - count(b"\r\n") == 0`.
- **PowerShell here-string syntax passed to the Bash tool.** Symptom: the `@'` and `'@`
  delimiters land in the commit message as literal `@` lines. Check:
  `git log -1 --format=%B | cat -A` before moving on.
- **Long prose through a bash heredoc.** Symptom:
  `unexpected EOF while looking for matching quote`. Check: write prose files with the
  Write tool rather than through the shell.

Added 2026-09-18:

- **Handing a builder's own collections to the immutable object it builds.** Symptom:
  none at runtime until a caller captures the builder out of the `Create` callback and
  mutates it afterward, silently changing a registry documented as immutable. Check: a
  test that captures the builder, calls `Add` after `Create` returns, and asserts the
  registry did not change.
- **Writing a type name from the declared type while serializing the payload as
  `object`.** Symptom: `System.Text.Json` uses the runtime type for the body, so the
  emitted name and body describe different types and the reader silently drops fields.
  Check: register only a base type, wrap a derived instance, and assert the derived-only
  property is absent from the JSON.
- **Windows Smart App Control blocking freshly built unsigned DLLs.** Symptom: an
  intermittent `FileLoadException` saying "Application Control policy has blocked this
  file" on a build that just succeeded. It is a non-deterministic reputation check on
  locally built binaries, not a code defect. Check: rebuild. Change no security setting.
