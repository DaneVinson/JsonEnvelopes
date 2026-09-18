# CLAUDE.md

Guidance for Claude Code when working in this repository.

## C# Conventions

### Variable Declarations
- Always use `var` for local variable declarations.

### Naming
- Follow Microsoft naming conventions for all identifiers.
- Private fields use `_camelCase` (e.g. `_myField`).
- Acronyms of 3 or more letters are Pascal-cased (first letter upper, rest lower): `Dto`, `Http`, `Xml`. Two-letter acronyms remain all-caps: `IO`, `UI`. Examples: `userDto` not `userDTO`, `httpClient` not `hTTPClient`.
- Never use abbreviations in names. Use the full word: `exception` not `ex`, `service` not `svc`, `request` not `req`, `response` not `res`, `parameter` not `param`, `message` not `msg`.

### Member Ordering
Order members within a type as follows, with each group sorted alphabetically by member name:
1. Constants
2. Private backing fields
3. Constructors
4. Properties
5. Methods

### XML Documentation Comments
- Always add XML documentation comments (`/// <summary>`) to all types (classes, interfaces, enums, structs, records) and all non-private members (methods, properties, constructors, events, fields).
- Private members do not require documentation comments.

### Braces
- Always use braces for control flow (`if`, `for`, `foreach`, `while`, etc.), even for single-line bodies.

### Access Modifiers
- Always write explicit access modifiers (`public`, `private`, `protected`, etc.). Never rely on defaults.

### Async
- Always use `async`/`await`. Never block on async code with `.Result` or `.Wait()`.

### Nullable Reference Types
- Use nullable patterns (`?`, `?.`, `??`) appropriately. Avoid `null` where possible. Treat nullability warnings as errors.

### Using Directives / Global Usings
- Never add `using {namespace};` at the top of individual `.cs` files.
- When creating a new project, add a `_GlobalUsings.cs` file at the project root and declare all namespaces there with `global using {namespace};`.
- When a new namespace is needed in any file, add `global using {namespace};` to `_GlobalUsings.cs` instead of adding a local using to the file.

### New Solutions / Projects
- When creating a new solution, always add a `Directory.Build.props` file at the solution root containing at minimum:
  ```xml
  <Project>
    <PropertyGroup>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <LangVersion><!-- ask user which version --></LangVersion>
    </PropertyGroup>
  </Project>
  ```
- Ask the user which C# `LangVersion` to use before creating the file.
- Do not add `<Nullable>`, `<ImplicitUsings>`, or `<LangVersion>` elements to individual `.csproj` files — they are inherited from `Directory.Build.props`.

## Project Notes

- Read [status.md](status.md) first. It records where the project is, the decisions behind it, load-bearing invariants, and an open review backlog. The `wrap-up` skill keeps it current.
- `Directory.Build.props` already exists at the solution root and sets `Nullable`, `ImplicitUsings`, and `LangVersion` (10.0). Do not duplicate those properties in any `.csproj`.
- Each project already has a `_GlobalUsings.cs` at its root. Add new namespaces there.
- Some existing code predates these conventions (for example `catch (Exception ex)` in `JsonEnvelopes.Example/Program.cs`, and member ordering that is not alphabetical). Apply the conventions to new and edited code. Do not reformat untouched files as a side effect of an unrelated change.
