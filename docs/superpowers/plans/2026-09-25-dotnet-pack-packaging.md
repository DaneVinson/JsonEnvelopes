# Packaging with `dotnet pack` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the JsonEnvelopes package with `dotnet pack` from the csproj, with a readme, XML docs, a SourceLink symbol package and a deterministic CI build, and make the pipeline push with `--skip-duplicate` on SDK 10.

**Architecture:** All package metadata moves into `JsonEnvelopes/JsonEnvelopes.csproj` and `JsonEnvelopes.nuspec` is deleted, so `dotnet pack` generates the nuspec and its dependency group from the project. Two PowerShell scripts outside the repo prove the package: one checks the new package against the spec, and one compares it with a baseline built from the old nuspec. The pipeline change cannot run locally, so it is checked by review and by text checks.

**Tech Stack:** .NET SDK 10.0.401 (local), `netstandard2.0` library, MSBuild pack properties, SourceLink built into the SDK, Azure Pipelines `UseDotNet@2`, PowerShell 7.

**Spec:** [docs/superpowers/specs/2026-09-25-dotnet-pack-packaging-design.md](../specs/2026-09-25-dotnet-pack-packaging-design.md)

## Global Constraints

Every task's requirements implicitly include this section.

- **Branch:** `dotnet-pack-packaging`. Never push to `origin`. Pushing `main` publishes to nuget.org.
- **The package version stays `3.0.0`.** Do not change it anywhere.
- **Never set `<Nullable>`, `<ImplicitUsings>` or `<LangVersion>` in a `.csproj`.** They come from `Directory.Build.props`.
- **No new `<PackageReference>`.** SourceLink is built into the .NET 8+ SDK.
- **Do not touch `[assembly: AssemblyKeyFile("JsonEnvelopes.snk")]` in `JsonEnvelopes/Extensions.cs`.** It strong-names the assembly. Moving it is finding 13, which is out of scope.
- **Out of scope:** coverage, git tags, finding 13, retargeting the tests (finding 3).
- **Line endings are CRLF** in every file you write or edit. `JsonEnvelopes/JsonEnvelopes.csproj` also starts with a UTF-8 BOM (`EF BB BF`); keep it. After each write, confirm the LF-only count is 0:
  `python -c "b=open(r'<path>','rb').read();print(b.count(b'\n')-b.count(b'\r\n'))"`
- **Work directory for scripts and packages:** `$env:TEMP\jsonenvelopes-pack` (outside the repo). Nothing in it is committed.
- **Commit messages** are one imperative sentence in sentence case, like the history (`Target netstandard2.0 with a System.Text.Json 6.0.10 floor`), and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
  Write the message with a bash heredoc (`git commit -F - <<'EOF'`), never a PowerShell here-string through the Bash tool. Then run `git log -1 --format=%B | cat -A` and confirm no stray `@` lines.
- **Smart App Control** can block a freshly built DLL with `FileLoadException` ("Application Control policy has blocked this file"). This is not a defect. Rebuild. Change no security setting.

---

### Task 1: Move package metadata into the csproj and delete the nuspec

**Files:**
- Modify: `JsonEnvelopes/JsonEnvelopes.csproj` (whole file)
- Delete: `JsonEnvelopes.nuspec`
- Scratch (not committed): `$env:TEMP\jsonenvelopes-pack\check-package.ps1`, `$env:TEMP\jsonenvelopes-pack\compare-packages.ps1`, `$env:TEMP\jsonenvelopes-pack\baseline\`, `$env:TEMP\jsonenvelopes-pack\new\`

**Interfaces:**
- Consumes: nothing.
- Produces: `dotnet pack JsonEnvelopes/JsonEnvelopes.csproj --configuration Release --no-build --output <dir>` writes `JsonEnvelopes.3.0.0.nupkg` and `JsonEnvelopes.3.0.0.snupkg` to `<dir>`. Task 2's pipeline runs exactly this command. With the environment variable `TF_BUILD=true`, the property `ContinuousIntegrationBuild` is `true`; without it, the property is empty.

- [ ] **Step 1: Write the package check script**

Create `$env:TEMP\jsonenvelopes-pack\check-package.ps1` with this content:

```powershell
param(
    [Parameter(Mandatory)] [string] $PackageDirectory,
    [Parameter(Mandatory)] [string] $ExpectedCommit
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$failures = 0

function Check([string] $name, [bool] $condition) {
    if ($condition) { Write-Host "PASS  $name" }
    else { Write-Host "FAIL  $name"; $script:failures++ }
}

function Get-Entries([string] $path) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try { return @($zip.Entries | ForEach-Object { $_.FullName }) } finally { $zip.Dispose() }
}

function Read-Entry([string] $path, [string] $entryName) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.GetEntry($entryName)
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
}

$package = Join-Path $PackageDirectory 'JsonEnvelopes.3.0.0.nupkg'
$symbols = Join-Path $PackageDirectory 'JsonEnvelopes.3.0.0.snupkg'
Check 'nupkg exists' (Test-Path $package)
if (-not (Test-Path $package)) { exit 1 }

$entries = Get-Entries $package
Check 'lib/netstandard2.0/JsonEnvelopes.dll' ($entries -contains 'lib/netstandard2.0/JsonEnvelopes.dll')
Check 'lib/netstandard2.0/JsonEnvelopes.xml' ($entries -contains 'lib/netstandard2.0/JsonEnvelopes.xml')
Check 'readme.md at package root' ($entries -contains 'readme.md')

[xml] $nuspec = Read-Entry $package 'JsonEnvelopes.nuspec'
$metadata = $nuspec.package.metadata
Check 'id is JsonEnvelopes' ($metadata.id -eq 'JsonEnvelopes')
Check 'version is 3.0.0' ($metadata.version -eq '3.0.0')
Check 'license expression is MIT' ($metadata.license.type -eq 'expression' -and $metadata.license.'#text' -eq 'MIT')
$groups = @($metadata.dependencies.group)
$dependencies = @($groups | Where-Object { $_.targetFramework -eq '.NETStandard2.0' } | ForEach-Object { $_.dependency })
Check 'one dependency group, .NETStandard2.0' ($groups.Count -eq 1 -and $groups[0].targetFramework -eq '.NETStandard2.0')
Check 'System.Text.Json 6.0.10 is the only dependency' ($dependencies.Count -eq 1 -and $dependencies[0].id -eq 'System.Text.Json' -and $dependencies[0].version -eq '6.0.10')
Check 'readme element is readme.md' ($metadata.readme -eq 'readme.md')
Check 'tags' ($metadata.tags -eq 'json serialization messaging envelope system.text.json')
Check 'repository url' ($metadata.repository.url -eq 'https://github.com/DaneVinson/JsonEnvelopes')
Check "repository commit is $ExpectedCommit" ($metadata.repository.commit -eq $ExpectedCommit)

Check 'snupkg exists' (Test-Path $symbols)
if (Test-Path $symbols) {
    Check 'snupkg holds lib/netstandard2.0/JsonEnvelopes.pdb' ((Get-Entries $symbols) -contains 'lib/netstandard2.0/JsonEnvelopes.pdb')
}

$extract = Join-Path ([System.IO.Path]::GetTempPath()) ("check-package-" + [guid]::NewGuid())
[System.IO.Compression.ZipFile]::ExtractToDirectory($package, $extract)
$assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $extract 'lib/netstandard2.0/JsonEnvelopes.dll'))
$token = -join ($assemblyName.GetPublicKeyToken() | ForEach-Object { $_.ToString('x2') })
Check 'PublicKeyToken is e6175e4a801927a0' ($token -eq 'e6175e4a801927a0')
Check 'AssemblyVersion is 3.0.0.0' ($assemblyName.Version.ToString() -eq '3.0.0.0')
Remove-Item $extract -Recurse -Force

Write-Host "$failures failure(s)"
exit $failures
```

- [ ] **Step 2: Write the baseline comparison script**

Create `$env:TEMP\jsonenvelopes-pack\compare-packages.ps1` with this content:

```powershell
param(
    [Parameter(Mandatory)] [string] $BaselinePackage,
    [Parameter(Mandatory)] [string] $NewPackage
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$failures = 0

function Get-Entries([string] $path) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try { return @($zip.Entries | ForEach-Object { $_.FullName }) } finally { $zip.Dispose() }
}

function Read-Metadata([string] $path) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.GetEntry('JsonEnvelopes.nuspec')
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return $nuspec.package.metadata
    } finally { $zip.Dispose() }
}

# Files: every baseline file except the per-pack OPC bookkeeping must be in the new package.
$ignored = @('_rels/.rels', '[Content_Types].xml')
$newEntries = Get-Entries $NewPackage
foreach ($entry in (Get-Entries $BaselinePackage)) {
    if ($ignored -contains $entry -or $entry -like 'package/services/metadata/core-properties/*') { continue }
    if ($newEntries -contains $entry) { Write-Host "SAME  file $entry" }
    else { Write-Host "LOST  file $entry"; $failures++ }
}

# Metadata: every baseline element must be in the new nuspec with the same text.
$baseline = Read-Metadata $BaselinePackage
$new = Read-Metadata $NewPackage
foreach ($element in $baseline.ChildNodes) {
    if ($element.NodeType -ne 'Element') { continue }
    $name = $element.LocalName
    if ($name -eq 'dependencies') { continue }
    $match = $new.ChildNodes | Where-Object { $_.LocalName -eq $name } | Select-Object -First 1
    if ($null -ne $match -and $match.InnerText -eq $element.InnerText) { Write-Host "SAME  <$name> $($element.InnerText)" }
    elseif ($name -eq 'requireLicenseAcceptance' -and $element.InnerText -eq 'false' -and ($null -eq $match -or $match.InnerText -eq 'false')) { Write-Host "SAME  <$name> false (default)" }
    else { Write-Host "DIFF  <$name> baseline '$($element.InnerText)' new '$($match.InnerText)'"; $failures++ }
}

# Dependencies: compare group, id and version; ignore the include/exclude attributes dotnet pack adds.
function Format-Dependencies($metadata) {
    return @($metadata.dependencies.group | ForEach-Object {
        $group = $_.targetFramework
        $_.dependency | ForEach-Object { "$group|$($_.id)|$($_.version)" }
    }) -join ';'
}
$baselineDependencies = Format-Dependencies $baseline
$newDependencies = Format-Dependencies $new
if ($baselineDependencies -eq $newDependencies) { Write-Host "SAME  dependencies $baselineDependencies" }
else { Write-Host "DIFF  dependencies baseline '$baselineDependencies' new '$newDependencies'"; $failures++ }

if ($baseline.minClientVersion) { Write-Host "NOTE  minClientVersion $($baseline.minClientVersion) is dropped (accepted in the spec)" }

Write-Host "$failures difference(s)"
exit $failures
```

- [ ] **Step 3: Build the baseline package from the old nuspec**

`nuget.exe` is not installed locally. `dotnet pack` can pack the old nuspec through `NuspecFile`, which gives the same package the pipeline's `nuget pack` step builds today. Run in PowerShell from the repo root:

```powershell
$work = "$env:TEMP\jsonenvelopes-pack"
Remove-Item "$work\baseline" -Recurse -Force -ErrorAction SilentlyContinue
dotnet build JsonEnvelopes/JsonEnvelopes.csproj --configuration Release --nologo -v q
dotnet pack JsonEnvelopes/JsonEnvelopes.csproj --configuration Release --no-build --nologo -p:NuspecFile=../JsonEnvelopes.nuspec -p:NuspecBasePath=.. --output "$work\baseline"
```

Expected: `Successfully created package '...\baseline\JsonEnvelopes.3.0.0.nupkg'`, and the warning `The package JsonEnvelopes.3.0.0 is missing a readme`.

- [ ] **Step 4: Run the check script on the baseline and confirm it fails**

```powershell
$work = "$env:TEMP\jsonenvelopes-pack"
& "$work\check-package.ps1" -PackageDirectory "$work\baseline" -ExpectedCommit (git rev-parse HEAD)
```

Expected: exit code 7, with exactly these seven `FAIL` lines. Every other line is `PASS`.

```
FAIL  lib/netstandard2.0/JsonEnvelopes.xml
FAIL  readme.md at package root
FAIL  readme element is readme.md
FAIL  tags
FAIL  repository url
FAIL  repository commit is <HEAD sha>
FAIL  snupkg exists
```

If a different line fails (for example the PublicKeyToken), stop. The baseline is wrong, and the later comparison would prove nothing.

- [ ] **Step 5: Replace the csproj**

Overwrite `JsonEnvelopes/JsonEnvelopes.csproj` with this content. Keep the UTF-8 BOM and CRLF line endings.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <!--Match the version in readme.md (two places)-->
    <Version>3.0.0</Version>
  </PropertyGroup>

  <PropertyGroup>
    <PackageId>JsonEnvelopes</PackageId>
    <Title>JsonEnvelopes</Title>
    <Authors>Dane Vinson</Authors>
    <Description>Standardized messaging with System.Text.Json serialization. Targets .NET Standard 2.0.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/DaneVinson/JsonEnvelopes</PackageProjectUrl>
    <RepositoryUrl>https://github.com/DaneVinson/JsonEnvelopes</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageTags>json;serialization;messaging;envelope;system.text.json</PackageTags>
    <PackageReadmeFile>readme.md</PackageReadmeFile>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>

  <!--Azure Pipelines sets TF_BUILD; local builds keep full paths for the debugger-->
  <PropertyGroup Condition="'$(TF_BUILD)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\readme.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="6.0.10" />
  </ItemGroup>

</Project>
```

Then check the encoding:

```bash
cd /c/repos/GitHub/JsonEnvelopes && head -c 3 JsonEnvelopes/JsonEnvelopes.csproj | od -An -tx1 && python -c "b=open(r'JsonEnvelopes/JsonEnvelopes.csproj','rb').read();print(b.count(b'\n')-b.count(b'\r\n'))"
```

Expected: ` ef bb bf` and `0`.

- [ ] **Step 6: Delete the nuspec**

```bash
cd /c/repos/GitHub/JsonEnvelopes && git rm -q JsonEnvelopes.nuspec
```

- [ ] **Step 7: Confirm the CI-build condition**

```powershell
dotnet msbuild JsonEnvelopes/JsonEnvelopes.csproj -getProperty:ContinuousIntegrationBuild
$env:TF_BUILD = 'true'; dotnet msbuild JsonEnvelopes/JsonEnvelopes.csproj -getProperty:ContinuousIntegrationBuild; Remove-Item Env:TF_BUILD
```

Expected: an empty line first, then `true`.

- [ ] **Step 8: Build, test and pack as the pipeline does**

Run in one PowerShell call, so `TF_BUILD` applies to the build and the pack:

```powershell
$work = "$env:TEMP\jsonenvelopes-pack"
Remove-Item "$work\new" -Recurse -Force -ErrorAction SilentlyContinue
$env:TF_BUILD = 'true'
dotnet build JsonEnvelopes.sln --configuration Release --nologo
dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj --configuration Release --no-build --nologo
dotnet pack JsonEnvelopes/JsonEnvelopes.csproj --configuration Release --no-build --nologo --output "$work\new"
Remove-Item Env:TF_BUILD
```

Expected:
- The build succeeds. Read the warnings. The only allowed ones are `NETSDK1138` for the `net6.0` test and example projects. There must be no `CS1591` (missing XML comment) and no `NU5xxx` pack warning.
- `Passed!  - Failed: 0, Passed: 53`.
- `Successfully created package '...\new\JsonEnvelopes.3.0.0.nupkg'` and `'...\new\JsonEnvelopes.3.0.0.snupkg'`, with no "missing a readme" warning.

- [ ] **Step 9: Run the check script on the new package and confirm it passes**

```powershell
$work = "$env:TEMP\jsonenvelopes-pack"
& "$work\check-package.ps1" -PackageDirectory "$work\new" -ExpectedCommit (git rev-parse HEAD)
```

Expected: every line is `PASS`, the last line is `0 failure(s)`, and the exit code is 0.

- [ ] **Step 10: Compare the new package with the baseline**

```powershell
$work = "$env:TEMP\jsonenvelopes-pack"
& "$work\compare-packages.ps1" -BaselinePackage "$work\baseline\JsonEnvelopes.3.0.0.nupkg" -NewPackage "$work\new\JsonEnvelopes.3.0.0.nupkg"
```

Expected: only `SAME` lines and one `NOTE  minClientVersion 5.0.0 is dropped` line, then `0 difference(s)`, and exit code 0. The `SAME` lines cover the files `JsonEnvelopes.nuspec` and `lib/netstandard2.0/JsonEnvelopes.dll`, the elements `id`, `version`, `title`, `authors`, `requireLicenseAcceptance`, `license`, `licenseUrl`, `projectUrl` and `description`, and the dependencies `.NETStandard2.0|System.Text.Json|6.0.10`.

- [ ] **Step 11: Confirm nothing still refers to the nuspec**

```bash
cd /c/repos/GitHub/JsonEnvelopes && grep -rn -i "nuspec" --include=*.csproj --include=*.cs --include=*.props --include=*.sln . ; echo "exit $?"
```

Expected: no matches, `exit 1`. (`azure-pipelines.yml` still refers to it until Task 2. `status.md` and the docs refer to it until Task 3.)

- [ ] **Step 12: Commit**

```bash
cd /c/repos/GitHub/JsonEnvelopes && git add JsonEnvelopes/JsonEnvelopes.csproj && git commit -q -F - <<'EOF'
Build the package with dotnet pack from the csproj

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
git log -1 --format=%B | cat -A && git status --short
```

Expected: the message has no stray `@` lines, and `git status --short` is empty.

---

### Task 2: Move the pipeline to SDK 10, `dotnet pack` and `--skip-duplicate`

**Files:**
- Modify: `azure-pipelines.yml` (whole file)

**Interfaces:**
- Consumes: Task 1's pack command and the `TF_BUILD` condition in the csproj.
- Produces: nothing that later tasks call. Task 3 describes this pipeline in `status.md`.

This file cannot run locally, and its first real run is the push that publishes 3.0.0. The checks in this task are text checks and review. No YAML parser is installed, so do not install one to check the file.

- [ ] **Step 1: Replace the pipeline**

Overwrite `azure-pipelines.yml` with this content, with CRLF line endings. Restore, build and test are unchanged from the current file.

```yaml
trigger:
- main

variables:
  buildConfiguration: 'Release'
  isMainBranch: $[eq(variables['Build.SourceBranch'], 'refs/heads/main')]
  
jobs:
  - job: Build_Test_Publish
    displayName: 'Windows: build, test and publish to nuget.org'
    pool:
        vmImage: 'windows-latest'

    steps:
    - task: UseDotNet@2
      displayName: Install .NET 10 SDK
      inputs:
        packageType: 'sdk'
        version: 10.x

    # The tests and the example still target net6.0 (status.md finding 3), and SDK 10 does not include that runtime
    - task: UseDotNet@2
      displayName: Install .NET 6 runtime
      inputs:
        packageType: 'runtime'
        version: 6.x

    - task: DotNetCoreCLI@2
      displayName: Restore
      inputs:
        command: restore
        projects: '**/*.csproj'

    - task: DotNetCoreCLI@2
      displayName: Build
      inputs:
        command: build
        projects: 'JsonEnvelopes.sln'
        arguments: --configuration $(buildConfiguration)

    - task: DotNetCoreCLI@2
      displayName: Test
      inputs:
        command: test
        projects: '**/JsonEnvelopes.Tests.csproj'
        arguments: '--configuration $(buildConfiguration) --no-restore --no-build -- xunit.parallelizeAssembly=true xunit.parallelizeTestCollections=true'

    - script: dotnet pack JsonEnvelopes/JsonEnvelopes.csproj --configuration $(buildConfiguration) --no-build --output "$(Build.ArtifactStagingDirectory)"
      displayName: dotnet pack

    # Also pushes the matching .snupkg; --skip-duplicate keeps an unchanged version from failing the pipeline
    - script: dotnet nuget push "$(Build.ArtifactStagingDirectory)\*.nupkg" --source https://api.nuget.org/v3/index.json --api-key $(NuGet-ApiKey) --skip-duplicate
      displayName: 'dotnet nuget push -> nuget.org'
      condition: and(succeeded(), eq(variables.isMainBranch, 'True'))
```

The current file has no newline after its last line. Keep it that way.

Then check the line endings:

```bash
cd /c/repos/GitHub/JsonEnvelopes && python -c "b=open(r'azure-pipelines.yml','rb').read();print(b.count(b'\n')-b.count(b'\r\n'), b.count(b'\t'))"
```

Expected: `0 0` (no LF-only lines, no tabs; YAML forbids tabs for indentation).

- [ ] **Step 2: Review the diff against the current pipeline**

```bash
cd /c/repos/GitHub/JsonEnvelopes && git diff azure-pipelines.yml
```

Expected: the only changes are the SDK install (`6.x` to `10.x`), the new runtime step, the removal of `NuGetToolInstaller@1` and of both `nuget` script steps, and the two new `dotnet` script steps. Restore, build, test, `variables` and the push `condition` are unchanged.

- [ ] **Step 3: Text-check the pipeline**

```bash
cd /c/repos/GitHub/JsonEnvelopes && grep -n "nuspec\|NuGetToolInstaller\|nuget pack\|- script: nuget" azure-pipelines.yml; echo "stale refs exit $?"; grep -c "skip-duplicate" azure-pipelines.yml; grep -n "condition:" azure-pipelines.yml
```

Expected: `stale refs exit 1` (no matches), `1`, and exactly one `condition:` line, on the push step.

- [ ] **Step 4: Confirm the push glob does not match the symbol package**

`dotnet nuget push` pushes the `.snupkg` next to each `.nupkg` by itself. If the glob also matched the `.snupkg`, the symbols would be pushed twice.

```powershell
$work = "$env:TEMP\jsonenvelopes-pack"
Get-ChildItem "$work\new\*.nupkg" | ForEach-Object Name
```

Expected: only `JsonEnvelopes.3.0.0.nupkg`.

- [ ] **Step 5: Commit**

```bash
cd /c/repos/GitHub/JsonEnvelopes && git add azure-pipelines.yml && git commit -q -F - <<'EOF'
Pack and push with the .NET 10 SDK and skip duplicate versions

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
git log -1 --format=%B | cat -A && git status --short
```

---

### Task 3: Record the change in `status.md`

**Files:**
- Modify: `status.md`

**Interfaces:**
- Consumes: the results of Tasks 1 and 2: the check-script and comparison output, the test count, and the pipeline steps.
- Produces: nothing.

Use the Edit tool for each change, so the CRLF line endings stay. Match the existing voice: short declarative sentences, **bold** for the key fact, dates as `2026-09-25`.

- [ ] **Step 1: Update the header**

Change `**Last updated:** 2026-09-18` to `**Last updated:** 2026-09-25`.

- [ ] **Step 2: Update the review backlog row in Section 1**

Replace:

```
| Review backlog | 15 findings from 2026-09-17; **8 closed by 3.0.0**, finding 3 partly closed, 6 open. See Section 5 |
```

with:

```
| Review backlog | 15 findings from 2026-09-17; **8 closed by 3.0.0**; finding 11 closed on 2026-09-25 except coverage; findings 3 and 14 partly closed; 9, 12, 13 and 15 open. See Section 5 |
```

- [ ] **Step 3: Add a decision to Section 3**

Add this paragraph after the paragraph that starts `**No reflection at all, rather than cached reflection (2026-09-18).**`:

```
**Packaging with `dotnet pack` (2026-09-25).** The hand-written nuspec was deleted
and all package metadata moved into `JsonEnvelopes/JsonEnvelopes.csproj`, so
`dotnet pack` generates the nuspec and its dependency group. Design in
[the spec](docs/superpowers/specs/2026-09-25-dotnet-pack-packaging-design.md).
Deliberately **rejected**: keeping the nuspec and filling it from the csproj with
`NuspecProperties` tokens, because two files would still have to agree on the
dependency group; and package properties in `Directory.Build.props`, because only one
project packs. Accepted loss: `minClientVersion="5.0.0"`, which `dotnet pack` does not
write. `ContinuousIntegrationBuild` is on only when `TF_BUILD` is set, so local PDBs
keep full paths for the debugger.
```

- [ ] **Step 4: Update the invariants in Section 4**

Replace the first bullet (it starts `- **Pushing \`main\` publishes a NuGet package.**` and ends `a side effect of landing work.`) with:

```
- **Pushing `main` publishes a NuGet package.** `azure-pipelines.yml` runs
  `dotnet pack` and `dotnet nuget push --skip-duplicate` to nuget.org on every `main`
  commit. An unchanged version is skipped and the pipeline stays green. A changed
  version **publishes irreversibly**, with its symbol package. Releasing is a
  deliberate act, never a side effect of landing work.
```

Replace the two bullets that start `- **The \`System.Text.Json\` version lives in two places.**` and `- **The \`.nuspec\` dependency group is not generated.**` with this one bullet:

```
- **Package metadata lives only in the csproj.** `dotnet pack` generates the nuspec,
  and every `<PackageReference>` in `JsonEnvelopes/JsonEnvelopes.csproj` becomes a
  dependency that consumers get. A build-only reference needs `PrivateAssets="all"`,
  or it ships as a dependency.
```

Replace the bullet that starts `- **The package version lives in four places across three files.**` with:

```
- **The package version lives in three places across two files.** `<Version>` in
  `JsonEnvelopes/JsonEnvelopes.csproj`, and twice in `readme.md` (the prose version
  and the `dotnet add package` line). They move together or not at all.
```

- [ ] **Step 5: Mark finding 11 in Section 5**

In the paragraph that starts `**Findings 1, 2, 4, 5, 6, 7, 8 and 10 were CLOSED by 3.0.0**`, change the sentences after the first one to:

```
Finding 11 was closed on 2026-09-25 except for coverage. Findings 3 and 14 are partly
closed, and 9, 12, 13 and 15 remain open. They stay here because each is still true
of the tree.
```

Replace the heading line of finding 11:

```
**11. Packaging is hand-rolled and incomplete.** A `.nuspec` with a hardcoded
```

with these lines, and keep the original finding text after them:

```
**11. ~~Packaging is hand-rolled and incomplete~~ — CLOSED 2026-09-25, except coverage.**
*Evidence: `JsonEnvelopes.nuspec` is deleted and `dotnet pack` builds from the csproj. A local pack with `TF_BUILD=true` produced a `.nupkg` with the XML docs, `readme.md`, tags and a `<repository>` commit, plus a `.snupkg` with the PDB; the DLL is still strong-named `e6175e4a801927a0`; a comparison with a package built from the old nuspec lost nothing except `minClientVersion`. The pipeline uses SDK 10.x and `--skip-duplicate`. Still open: coverage is not collected, and there are no tags — Dane tags releases by hand. The pipeline change is unverified until its first run on `main`. Original finding below.*

A `.nuspec` with a hardcoded
```

- [ ] **Step 6: Update the toolchain table in Section 7**

Replace the `| Packaging |` row with:

```
| Packaging | **`dotnet pack`** from `JsonEnvelopes/JsonEnvelopes.csproj`; `.snupkg` symbols with SourceLink; readme and XML docs in the package |
```

Replace the `| CI |` row with:

```
| CI | **Azure Pipelines** (repo stays on GitHub), `windows-latest`, SDK 10.x plus the .NET 6 runtime for the `net6.0` tests: restore, build, test, `dotnet pack`, `dotnet nuget push --skip-duplicate` to nuget.org on `main` |
```

- [ ] **Step 7: Remove the obsolete trap in Section 8**

Delete the bullet that starts `- **A hand-written .nuspec whose dependency group is empty.**` and ends `` `dotnet pack` generates for the same project. `` The trap cannot happen now, and the new invariant in Step 4 covers the remaining risk.

- [ ] **Step 8: Check the file**

```bash
cd /c/repos/GitHub/JsonEnvelopes && python -c "b=open(r'status.md','rb').read();print(b.count(b'\n')-b.count(b'\r\n'))" && grep -n -e 'four places' -e 'lives in two places' -e 'is not generated' -e 'hand-written `JsonEnvelopes.nuspec`' -e 'has no `--skip-duplicate`' status.md; echo "stale exit $?"
```

Expected: `0`, then `stale exit 1`.

Read Sections 1, 4, 5 (finding 11) and 7 once more. Confirm that no sentence still says the nuspec exists or that the push has no `--skip-duplicate`, except in the struck-through original text of finding 11.

- [ ] **Step 9: Commit**

```bash
cd /c/repos/GitHub/JsonEnvelopes && git add status.md && git commit -q -F - <<'EOF'
Record the dotnet pack packaging change in status.md

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
git log -1 --format=%B | cat -A && git status --short
```

After this task, the branch is ready for the `wrap-up` skill. Do not push.
