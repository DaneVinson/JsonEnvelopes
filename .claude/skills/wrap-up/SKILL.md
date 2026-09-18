---
name: wrap-up
description: Use when work on a feature branch in this repo is finished and should land on local main - "wrap up", "finish this branch", "land this", "merge this back". Runs to completion without asking unless a guard trips.
---

# Wrap Up a Feature Branch

Verify the branch builds and its tests pass, bring the docs up to date, commit
everything to the feature branch, merge it into local `main`, delete the branch.

Invoking this is permission to commit and merge into **local** `main`. It is not
permission to touch `origin`: no fetch, no pull, no push.

**Pushing `main` publishes a NuGet package.** `azure-pipelines.yml` packs and
runs `nuget push` to nuget.org on every `main` commit. That push has no
`--skip-duplicate`, so it fails the pipeline when the version is unchanged and
publishes irreversibly when it is changed. Releasing is a separate, deliberate
act by the user. This skill never pushes.

This repo has historically landed work through GitHub pull requests. This skill
is the local-merge path. If the change should be reviewed on GitHub, say so and
stop instead of merging.

When a guard trips, stop, change nothing further, and report which guard and why.

## 1. Guards before anything changes

```bash
git rev-parse --abbrev-ref HEAD
git status --porcelain --untracked-files=all
git worktree list
git log main..HEAD --oneline
git log HEAD..main --oneline
```

Stop if:

- **HEAD is `main` or detached.** There is no feature branch to wrap up.
- **The branch has no commits and the tree is clean.** There is nothing to land.
- **`main` is checked out in another worktree.** `git checkout main` will fail;
  tell the user to merge from that worktree.
- **A merge, rebase, or cherry-pick is in progress** (`.git/MERGE_HEAD`,
  `.git/rebase-merge`, `.git/rebase-apply`, or `.git/CHERRY_PICK_HEAD` exists).
- **Untracked files look like build output or secrets:** `bin/`, `obj/`,
  `TestResults/`, `*.nupkg`, `coverage.cobertura.xml`, `*.user`, `*.snk`, `.env`,
  or anything large and binary. That is a `.gitignore` gap to fix first, not work
  to commit.

`HEAD..main` not being empty is fine — `main` moved on since the branch was cut —
but note it, because it makes a conflict possible in step 7.

## 2. Build and test

The branch does not land red. Run both, from the repo root:

```bash
dotnet build JsonEnvelopes.sln --configuration Release
```

```bash
dotnet test JsonEnvelopes.Tests/JsonEnvelopes.Tests.csproj --configuration Release
```

Stop if either fails, or if the test count dropped without the diff explaining
why. Report the actual output — never the intent.

Warnings matter here: `TreatWarningsAsErrors` is not set, so a new nullability or
obsolete-API warning passes the build silently. Read the warning list and report
any the branch introduced.

## 3. Understand what the branch actually did

Read the real change, not the branch name or the session's intent:

```bash
git diff main...HEAD
git diff HEAD
```

Also list untracked files. The docs record what happened, and branches drift.

Two questions this library always has to answer:

- **Did the public API surface change?** `Envelope`, `Envelope<T>`,
  `EnvelopeJsonConverter`, and `Extensions` are a published package. A removal or
  signature change is breaking and needs a major version in step 4.
- **Did the serialized wire format change?** Property names, casing, ordering, or
  the `ContentType` string shape. No test pins the exact JSON, so a shape change
  passes the suite and silently breaks deployed consumers. Call it out explicitly
  in the report even when it was intended.

## 4. Check the version is consistent

The package version is duplicated in three files. If the branch changed the
public API, the wire format, or anything else a consumer would see, all four
occurrences must agree:

```bash
grep -rn "2\.0\.1" readme.md JsonEnvelopes.nuspec JsonEnvelopes/JsonEnvelopes.csproj
```

- `JsonEnvelopes/JsonEnvelopes.csproj` — `<AssemblyVersion>`
- `JsonEnvelopes.nuspec` — `<version>`
- `readme.md` — the prose version and the `dotnet add package` line

Bump them together or leave all of them alone. A partial bump is a guard trip:
stop and report it. Do not bump a version just because code changed — that is a
release decision for the user.

## 5. Update the docs

Update every file the branch made wrong or incomplete. Skip any the branch did
not affect. A typo fix or pure rename usually needs no doc change.

**`status.md`** — where the project is, how it got here, and what would break.
Read it first and follow its existing sections rather than inventing new ones.
Always:

- set **Last updated** to today's date (`YYYY-MM-DD`) if anything else in the
  file changes;
- fix any claim the branch made false: phase, the "Where we are" table, repo
  state, the toolchain table;
- record decisions that had real alternatives, including what was rejected and why;
- add or retire invariants, risks, and toolchain entries the branch touched;
- tick off or remove any review finding in Section 5 that the branch actually
  fixed, and say so in the report. Never mark one fixed without a test or a diff
  that proves it.
- add a trap that cost time as: the trap, its symptom, and the check that catches
  it next time.

The Repo row in "Where we are" goes stale on every merge. After this skill runs,
local `main` will be further ahead of `origin/main`, so say so.

**`readme.md`** — the package's public documentation and the only place usage is
explained. Correct it in place when the branch changed the API, the serialized
shape, the install version, or the usage pattern. It holds current truth only,
not history.

**`CLAUDE.md`** — conventions and project notes for the next session. Update it
when the branch changed the toolchain, the target framework, the build
properties, or a convention the file states.

**Anything else that carries state between sessions** — check whether each exists
and whether the branch affected it: `azure-pipelines.yml` (build, test, or
publish steps), `Directory.Build.props`, `.gitignore`, `.claude/skills/*` if the
branch changed a workflow a skill describes, and a `CHANGELOG.md` if one has been
added since.

Write full sentences, absolute dates (`YYYY-MM-DD`), and named files and symbols.
Never write a doc entry you cannot back with the diff.

## 6. Commit to the feature branch

Two commits, in this order, each only when there is something to commit:

1. **Implementation** — the uncommitted code and project changes.
2. **Docs** — the updates from step 5.

Stage paths explicitly (`git add <paths>`). Never use `git add -A` or `git add .`,
which would pick up anything the guard in step 1 missed.

Files in this repo use CRLF. When you append to or rewrite a file, match it, so
the diff shows the real change instead of every line.

Message format: imperative, sentence-case subject with no prefix tag; a body
explaining *why* and anything non-obvious; then the commit attribution this
session specifies.

```
Dispose the JsonDocument on the success path

Read leaked every successfully parsed document, so the pooled buffers it
rents were never returned and deserialization pushed avoidable pressure
onto the GC.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

Afterwards `git status --porcelain` must be empty. If it isn't, stop.

## 7. Merge into local `main`

```bash
git checkout main
git merge --no-ff <branch>
```

`--no-ff` always, so every feature lands as one merge commit.

**If the merge conflicts:** run `git merge --abort`, then `git checkout <branch>`,
and stop. Report the conflicting files. Don't resolve conflicts inside this
skill: a resolution is new work that should be looked at before it lands.

**Verify the merge actually took:**

```bash
git log main..<branch> --oneline
```

That must be empty.

## 8. Delete the branch

```bash
git branch -d <branch>
```

Use `-d`, never `-D`. `-d` refuses to delete an unmerged branch, which is the
last guard against losing work. If it refuses, stop and report. Never delete a
remote branch.

## 9. Report

- the branch that was merged, and the merge commit's hash;
- the commits it carried (`git log --oneline <merge>^1..<merge>^2`);
- the build and test result, with the test count;
- any public API or wire-format change, stated plainly;
- the package version, and whether it moved;
- any `status.md` review finding the branch closed, with the evidence;
- which docs changed, with one line each on what changed;
- that nothing was pushed, local `main` is now N commits ahead of `origin/main`,
  and that pushing would trigger a nuget.org publish.
