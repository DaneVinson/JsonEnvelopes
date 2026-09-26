# Packaging with `dotnet pack` — Design

**Date:** 2026-09-25
**Branch:** `dotnet-pack-packaging`
**Target version:** 3.0.0 (unchanged; not a release)
**Closes:** `status.md` finding 11, except coverage

## 1. Problem

The package is built from a hand-written `JsonEnvelopes.nuspec` with `nuget pack`.
The nuspec is correct for 3.0.0 today, but only because it was kept in step by hand:

- The package version is in four places across three files: `<AssemblyVersion>` in
  the csproj, `<version>` in the nuspec, and two times in `readme.md`.
- The `System.Text.Json` version is in two places: the csproj `<PackageReference>`
  and the nuspec `<dependency>`. A new `<PackageReference>` does not reach consumers
  unless someone also adds it to the nuspec.
- The `<file>` element hard-codes `bin\Release\netstandard2.0\`. It broke once
  already, at the retarget from `net6.0`.

The package also does not carry what consumers expect:

- No readme on the nuget.org page.
- No XML documentation file, so the `///` comments never reach IntelliSense.
- No symbol package and no SourceLink, so consumers cannot step into the source.
- No deterministic CI build, so a binary cannot be matched to a commit.

The pipeline has two more defects:

- `nuget push` has no `--skip-duplicate`. Every push to `main` that does not change
  the version fails the pipeline.
- It installs the .NET 6 SDK, which is out of support.

## 2. Decision

Move all package metadata into `JsonEnvelopes/JsonEnvelopes.csproj`, delete
`JsonEnvelopes.nuspec`, and build the package with `dotnet pack`.

**Rejected:** keep the nuspec and fill it from the csproj with `NuspecFile` and
`NuspecProperties` tokens. That removes the hard-coded version but keeps two files
that must agree on everything else, including the dependency group.

**Rejected:** put the package properties in `Directory.Build.props`. Only one project
is packable, so shared props would apply metadata to projects that never pack.

**Out of scope:**

- Coverage collection and publishing (the remainder of finding 11).
- Moving `[assembly: AssemblyKeyFile]` to `<SignAssembly>` (finding 13).
- Git tags. Dane tags releases by hand.
- A version bump. The version stays 3.0.0.

## 3. Changes

### 3.1 `JsonEnvelopes/JsonEnvelopes.csproj`

Replace `<AssemblyVersion>3.0.0</AssemblyVersion>` with `<Version>3.0.0</Version>`.
The SDK derives `AssemblyVersion` 3.0.0.0 and `FileVersion` from it, the same values
as today.

Add these properties:

| Property | Value | Replaces |
| --- | --- | --- |
| `PackageId` | `JsonEnvelopes` | nuspec `<id>` |
| `Title` | `JsonEnvelopes` | nuspec `<title>` |
| `Authors` | `Dane Vinson` | nuspec `<authors>` |
| `Description` | the current nuspec description | nuspec `<description>` |
| `PackageLicenseExpression` | `MIT` | nuspec `<license>` |
| `PackageProjectUrl` | `https://github.com/DaneVinson/JsonEnvelopes` | nuspec `<projectUrl>` |
| `RepositoryUrl` | `https://github.com/DaneVinson/JsonEnvelopes` | new |
| `RepositoryType` | `git` | new |
| `PackageTags` | `json;serialization;messaging;envelope;system.text.json` | new |
| `PackageReadmeFile` | `readme.md` | new |
| `GenerateDocumentationFile` | `true` | new |
| `IncludeSymbols` | `true` | new |
| `SymbolPackageFormat` | `snupkg` | new |
| `PublishRepositoryUrl` | `true` | new |
| `EmbedUntrackedSources` | `true` | new |
| `ContinuousIntegrationBuild` | `true`, only when `$(TF_BUILD)` is `true` | new |

Add `..\readme.md` as a `<None>` item with `Pack="true"` and `PackagePath="\"`.

The nuspec had `minClientVersion="5.0.0"` and `requireLicenseAcceptance="false"`.
`dotnet pack` omits `minClientVersion`, and `false` is the default for license
acceptance. Neither loss affects a consumer that can install a `netstandard2.0`
package with a license expression.

SourceLink is built into the .NET 8 SDK and later. No package reference is needed.
The local SDK is 10.0.401, and the pipeline moves to 10.x (section 3.3).

Update the two comments that point at the nuspec. The `<PackageReference>` comment
goes away, because the dependency now has one source. The version comment points
only at `readme.md`.

### 3.2 Delete `JsonEnvelopes.nuspec`

### 3.3 `azure-pipelines.yml`

- Replace the .NET 6 SDK install with SDK `10.x`.
- Add a `UseDotNet@2` step that installs runtime `6.x`. The tests and the example
  still target `net6.0`, and SDK 10 does not include the .NET 6 runtime. Remove this
  step when finding 3 retargets the tests.
- Remove the `NuGetToolInstaller@1` step.
- Pack with
  `dotnet pack JsonEnvelopes/JsonEnvelopes.csproj --configuration $(buildConfiguration) --no-build --output $(Build.ArtifactStagingDirectory)`.
- Push with
  `dotnet nuget push "$(Build.ArtifactStagingDirectory)/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key $(NuGet-ApiKey) --skip-duplicate`.
  `dotnet nuget push` also pushes the matching `.snupkg`.
- Keep the push condition: `main` only.

Azure Pipelines sets `TF_BUILD=true`, so section 3.1 turns on
`ContinuousIntegrationBuild` there and nowhere else. A local build keeps full local
paths in the PDB, which is what a debugger needs.

### 3.4 Documentation

- `status.md`: close finding 11 with evidence, except coverage. Update the
  invariants on the version and on the `System.Text.Json` version. Remove the
  invariant and the trap about the empty nuspec dependency group. Update the
  toolchain table (packaging, CI) and the repo state.
- `readme.md`: no change. It does not mention the nuspec, and all its links are
  absolute, so they resolve on nuget.org.

## 4. Verification

The pipeline cannot run locally, and its first run publishes 3.0.0. The local checks
must therefore prove that the package is correct before that push.

1. Build the solution in Release and run the tests. Expect 53 passing.
2. Before any change, pack the old nuspec and keep the result as the baseline.
3. After the change, run `dotnet pack` with `-p:ContinuousIntegrationBuild=true`.
4. Open the new `.nupkg` and confirm:
   - `lib/netstandard2.0/JsonEnvelopes.dll` and `lib/netstandard2.0/JsonEnvelopes.xml`
   - `readme.md` at the root
   - in the generated nuspec: id `JsonEnvelopes`, version `3.0.0`, license
     expression `MIT`, a `.NETStandard2.0` dependency group with
     `System.Text.Json` `6.0.10`, `<readme>`, `<tags>`, and a `<repository>`
     element with the URL and the commit
5. Confirm that a `.snupkg` exists and holds `JsonEnvelopes.pdb`.
6. Confirm that the DLL is strong-named with `PublicKeyToken=e6175e4a801927a0` and
   has `AssemblyVersion` 3.0.0.0.
7. Compare with the baseline. Every file and every metadata value in the baseline is
   also in the new package, except `minClientVersion`.
8. Confirm that no file under `JsonEnvelopes/` or the pipeline mentions the nuspec.

## 5. Risks

- **The YAML is untested until the release push.** A mistake fails the pipeline
  before or at the push step. It cannot publish a wrong package, because the push
  step runs only after pack succeeds. The recovery is a fix commit, and
  `--skip-duplicate` then makes a later rerun safe.
- **The .NET 6 runtime on the agent.** `UseDotNet@2` can still install an
  out-of-support runtime. If Microsoft removes 6.x from the feed, the test step fails
  and finding 3 becomes urgent.
