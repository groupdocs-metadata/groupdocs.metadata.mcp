# Prompt — Clone GroupDocs.Metadata MCP solution to a new GroupDocs product

Run this prompt in a fresh Claude Code session. Paste it verbatim and follow
the interactive steps. The prompt collects every path it needs from you up
front — no assumptions about local layout.

---

## Goal

Clone the GroupDocs.Metadata MCP solution to another GroupDocs product. Two
output repositories are produced:

1. **Main MCP repo** — `GroupDocs.{Product}.Mcp` (the publishable MCP server,
   mirrors the Metadata MCP repo's structure exactly).
2. **Tests repo** — `GroupDocs.{Product}.Mcp.Tests` (mirrors the Metadata
   Tests repo exactly).

## Conventions baked in (do not ask the user to confirm)

- **NuGet package id**: `GroupDocs.{Product}.Mcp`
- **dotnet tool command**: `groupdocs-{product-lowercase}-mcp`
- **GHCR image**: `ghcr.io/groupdocs-{product-lowercase}/{product-lowercase}-net-mcp`
- **Docker Hub image**: `docker.io/groupdocs/{product-lowercase}-net-mcp`
- **GitHub repo URLs**: `github.com/groupdocs-{product-lowercase}/GroupDocs.{Product}.Mcp` and `…/GroupDocs.{Product}.Mcp.Tests`
- **MCP `name` field**: `io.github.groupdocs-{product-lowercase}/groupdocs-{product-lowercase}-mcp`
- **Initial `<GroupDocs{Product}Mcp>` version**: `26.5.1`
- **`<GroupDocsMcpCore>` version**: copy from the source-of-truth Metadata repo's `build/dependencies.props` (currently `26.4.1` — re-read at clone time)
- **`<GroupDocs{Product}>` (underlying engine) version**: resolve from NuGet (latest stable) — confirm with user before writing
- **Target framework**: `net10.0` only
- **License expression**: `MIT`
- **Authors / Company / Copyright**: copy from the Metadata repo's `build/dependencies.props`
- **Package type**: `<PackageType>McpServer</PackageType>` (singular — the plural form is silently ignored by NuGet)
- **CalVer in lockstep** between `build/dependencies.props` → `<GroupDocs{Product}Mcp>` and `src/GroupDocs.{Product}.Mcp/.mcp/server.json` (top-level `version` and `packages[0].version`). `build.ps1` enforces this — do not skip.

---

## Step 0 — Collect required paths

Ask the user for the **six** absolute paths below. Validate each one exists
(or for the two target paths, that the parent exists and the path is empty
or contains only the gh-init placeholders). Halt with a clear message if any
path is missing.

> Before I start, I need six absolute paths from you (most are read-only inputs):
>
> 1. **SOURCE_TEMPLATE_PATH** — the source-of-truth Metadata MCP repo
>    (the repo this prompt was authored from; contains `src/GroupDocs.Metadata.Mcp/`,
>    `build/dependencies.props`, `docker/Dockerfile`, etc.)
> 2. **SOURCE_TESTS_PATH** — the source-of-truth Metadata Tests repo
>    (contains `src/GroupDocs.Metadata.Mcp.Tests/`, `how-to/`, `examples/`,
>    `docker-scripts/`, `sample-docs/`, `.github/workflows/integration.yml`)
> 3. **FRAMEWORK_PATH** — the `groupdocs-mcp-framework` monorepo
>    (contains `src/GroupDocs.{Product}.Mcp/` subfolders for every product —
>    this is where the per-product `Tools/*.cs` files live that we'll port)
> 4. **TARGET_MAIN_PATH** — the empty new repo where I'll write `GroupDocs.{Product}.Mcp/`
>    plus all siblings (assets, build, changelog, docker, src, …)
> 5. **TARGET_TESTS_PATH** — the empty new repo where I'll write `GroupDocs.{Product}.Mcp.Tests/`
>    plus all siblings (Files, sample-docs, examples, how-to, docker-scripts, src, …)
> 6. **PRODUCT_DOTNET_PATH** — the original `GroupDocs.{Product}` for .NET source folder
>    (the one that produces the `GroupDocs.{Product}` NuGet package; needs a
>    `.nuspec` somewhere under it; needs `src/` with the `License` class)

For each TARGET path: verify the folder exists and is empty (or contains
only `.git/`, `.gitignore`, `LICENSE`, `README.md` placeholders from
`gh repo create --add-readme --license MIT --gitignore VisualStudio`). Halt
if either is non-empty in an unexpected way; show the user what's there
before deciding to overwrite.

If Claude Code reports "path is outside the workspace" when writing, ask
the user to add both target paths via Claude Code's *additional working
directories* mechanism, then resume.

For the rest of this prompt, all six paths are referenced as the variables
above. Do not hardcode any user-specific path.

## Step 1 — Ask the product name

Ask the user:

> What GroupDocs product are we cloning to? (e.g. `GroupDocs.Conversion`,
> `GroupDocs.Annotation`, `GroupDocs.Viewer`, …)

Validate that a folder exists at `{FRAMEWORK_PATH}/src/GroupDocs.{Product}.Mcp/`.
If not, list the available product subfolders under `{FRAMEWORK_PATH}/src/`
and ask again.

Derive the lowercase short form `{product}` (e.g. `conversion`) — used for tool
command name, image names, and registry slugs.

## Step 2 — Enumerate the framework's tools and suggest descriptions

Read `{FRAMEWORK_PATH}/src/GroupDocs.{Product}.Mcp/Tools/`. For each `*.cs`
file, extract:
- The class name (filename minus `.cs`)
- The `[McpServerTool]` method name (this is the **wire name**)
- The `[Description("…")]` string

**Sanity-check**: the file name should match the method name's `*Tool`
convention. If a class file's name doesn't match its method name (e.g.
`GetSupportedFormatsTool.cs` containing a `GetDocumentInfo` method — a real
case observed during the Conversion clone), flag it to the user and propose
the correct rename.

From the tool inventory, draft and show the user:

1. A one-line **GitHub repo description** (max ~140 chars, mention "MCP server",
   the underlying engine, and the primary verbs the tools expose).
2. A short **NuGet `<Description>`** (~1–2 sentences) for the csproj.
3. A **canonical description sentence** (see Step 8 — this single sentence
   gets used verbatim across csproj, server.json, Dockerfile OCI label, and
   the leading line of llms.txt for AI consistency).

Wait for the user to confirm or revise these before continuing.

## Step 3 — Inspect the original .NET product (Skia / System.Drawing / License class)

Using `{PRODUCT_DOTNET_PATH}`, perform three inspections in parallel:

1. **`SkiaSharp.NativeAssets.Linux.NoDependencies` decision** — Glob for
   `**/*.nuspec` under `{PRODUCT_DOTNET_PATH}` and grep for `SkiaSharp`. Also
   grep `{FRAMEWORK_PATH}/src/GroupDocs.{Product}.Mcp/` for `SkiaSharp`.
   **Include** the package reference if either source mentions it (mirror the
   Metadata csproj comment — note that even when the upstream nuspec already
   declares the native asset packages, pinning explicitly keeps transitive
   resolution deterministic).
   **Skip and document the omission in `changelog/001`** if neither mentions it.

2. **`System.Drawing.EnableUnixSupport` decision** — grep
   `{PRODUCT_DOTNET_PATH}/src/` and the framework subproject for `System.Drawing`.
   **Include** the `<RuntimeHostConfigurationOption>` in the csproj AND the
   matching `apt-get install -y --no-install-recommends libgdiplus libfontconfig1`
   line in `docker/Dockerfile` if either source uses `System.Drawing`. **Skip
   both** if neither does — and rewrite the Dockerfile comment to remove the
   System.Drawing rationale.
   When writing the Dockerfile comment, name the **specific subsystems** of
   the new product that drive the dependency (e.g. "Cells (Excel) and image-
   format paths" for Comparison; "JPEG/PNG/image-bearing PDFs" for Metadata).
   Do **not** carry over Metadata's exact wording verbatim.

3. **License-class availability decision** — grep
   `{PRODUCT_DOTNET_PATH}/src/` for `class License` and
   `public void SetLicense(string`. If both are present (and the class is
   `public`, not `internal`), **use the Metadata pattern**:
   `new GroupDocs.{Product}.License().SetLicense(licensePath);`
   If the License class is private/internal or absent, fall back to the
   framework subproject's `{Product}LicenseManager.cs` body verbatim (likely
   sets an env var). **Do not blindly copy the framework's pattern** — the
   framework `*LicenseManager.cs` files were observed to claim the License
   class wasn't exposed when in fact it was (Conversion clone learning).

Confirm the three decisions back to the user with the evidence (file paths +
matched lines) before writing.

## Step 4 — Resolve the underlying NuGet version

Fetch `https://www.nuget.org/packages/GroupDocs.{Product}` (or
`https://api.nuget.org/v3-flatcontainer/groupdocs.{product}/index.json` for a
stable JSON response) and extract the latest **stable** version. Show it to
the user:

> Latest `GroupDocs.{Product}` on NuGet is `X.Y.Z`. Use this as
> `<GroupDocs{Product}>` in `build/dependencies.props`?

Wait for confirmation (or override) before writing.

## Step 5 — Mirror the Metadata MCP repo into TARGET_MAIN_PATH

Read `{SOURCE_TEMPLATE_PATH}` and mirror every file/folder into
`{TARGET_MAIN_PATH}`. The most efficient approach is:

1. **Bulk-copy verbatim files via Bash** (`cp -r`) — the files in the table
   below that need NO substitution.
2. **Run a comprehensive Python token-substitution pass** over the cp'd
   files using the eight token forms in Step 5b.
3. **Write product-specific files via the Write tool** — the files that
   have no useful Metadata starting point (Tools, csproj, Program.cs,
   LicenseManager, .mcp/server.json, sln, README sections, AGENTS sections,
   llms.txt, changelog/001, RELEASE.md, build.ps1, publish-prod.ps1).
4. **Apply post-substitution prose rewrites** for files where blind
   substitution produces nonsense (see Step 5d).

### Step 5a — Bulk-copy verbatim/semi-verbatim files

```bash
SRC="{SOURCE_TEMPLATE_PATH}"
DEST="{TARGET_MAIN_PATH}"

# Root files
cp "$SRC/LICENSE" "$SRC/global.json" "$SRC/nuget.config" "$SRC/.gitignore" \
   "$SRC/.editorconfig" "$SRC/.gitattributes" "$DEST/"

# Bulk dirs (will overwrite individual files in next step)
cp -r "$SRC/build" "$SRC/changelog" "$SRC/docker" "$SRC/.github" "$SRC/.vscode" "$DEST/"

# Drop Metadata-era changelog entries (we write a fresh 001)
rm -f "$DEST/changelog/"*-*.md  # only delete entries — keeps changelog/README.md

# Assets — only icon
mkdir -p "$DEST/assets"
cp "$SRC/assets/icon.png" "$DEST/assets/"

# src scaffold
mkdir -p "$DEST/src/GroupDocs.{Product}.Mcp/.mcp"
mkdir -p "$DEST/src/GroupDocs.{Product}.Mcp/Tools"
mkdir -p "$DEST/src/GroupDocs.{Product}.Mcp.Tests"
cp "$SRC/src/Directory.Build.props" "$DEST/src/"

# Delete dest README (gh-auto stub) so we can write a fresh one
rm -f "$DEST/README.md"
```

### Step 5b — Comprehensive token-substitution pass

Run this Python script over every text file under `{TARGET_MAIN_PATH}`
(skip `.git/`, `bin/`, `obj/`, `build_out/`, and binary extensions). Token
list MUST cover all eight casing forms — order matters (longest/most
specific first):

```python
REPLS = [
    # PascalCase + dot (Mcp first so it doesn't get clobbered)
    ('GroupDocs.Metadata.Mcp', 'GroupDocs.{Product}.Mcp'),
    ('GroupDocs.Metadata',     'GroupDocs.{Product}'),
    # MSBuild props + test-method substrings (no dot)
    ('GroupDocsMetadataMcp',   'GroupDocs{Product}Mcp'),
    ('GroupDocsMetadata',      'GroupDocs{Product}'),
    # kebab forms (longer first)
    ('groupdocs-metadata-mcp', 'groupdocs-{product}-mcp'),
    ('groupdocs-metadata',     'groupdocs-{product}'),
    # lowercase-dot (NuGet cache path, package id in lowercase)
    ('groupdocs.metadata.mcp', 'groupdocs.{product}.mcp'),
    ('groupdocs.metadata',     'groupdocs.{product}'),
    # Docker repo
    ('metadata-net-mcp',       '{product}-net-mcp'),
    # MetadataLicenseManager class
    ('MetadataLicenseManager', '{Product}LicenseManager'),
    # Versions
    # Metadata template version → new clone version. Identity by default
    # (next clone starts at whatever Metadata is on). Bump the right-hand
    # side here BEFORE running the substitution if you want the new clone
    # to start at a different version than the current Metadata template.
    ('26.5.1',                 '26.5.1'),
    # NOTE: Do NOT add ('26.1.0', '<resolved>') here — that's the Metadata
    # engine version, only used in build/dependencies.props which we rewrite
    # entirely in Step 5c. A blind 26.1.0 substitution would also touch other
    # version refs by accident.
]
```

After this pass, run `grep -rni "metadata" {TARGET_MAIN_PATH}` and confirm
every hit is either intentional historical context (`changelog/001` lines like
"after Metadata and Conversion") or a generic concept use ("server.json
metadata", "AssemblyMetadataAttribute"). **Every other hit is a bug**.

### Step 5c — Write product-specific files

Use the Write tool (with substituted content) for these files. Skip token
substitution since you're authoring them fresh:

**Root:**
- `LICENSE` — copy from Metadata, then change ONLY the copyright line:
  `Copyright (c) <year> GroupDocs.Metadata Product Family` →
  `Copyright (c) <year> GroupDocs.{Product} Product Family`.
- `README.md` — full rewrite using the new product's tools table; include the
  Example prompts section (see Step 8).
- `RELEASE.md` — substitute names + URLs; checklist content stays.
- `AGENTS.md` — full rewrite of the *MCP tools exposed* table and the *Folder
  layout* tree; the rest can be substituted.
- `llms.txt` — full rewrite of the MCP-tools section and the example-prompts
  section.
- `build.ps1` — substituted from Metadata's; keep the
  `Assert-ServerJsonVersionMatchesDependencies` enforcement intact.
- `publish-prod.ps1` — substituted; product-agnostic content.

**`changelog/`:**
- `README.md` — substitute the first sentence (`Per-change notes for
  GroupDocs.Metadata.Mcp, …` → `… GroupDocs.{Product}.Mcp, …`). The rest is
  product-agnostic.
- Write a fresh `001-initial-commit.md` with frontmatter
  `id: 001`, `date: <today YYYY-MM-DD>`, `version: 26.5.1`, `type: feature` —
  body lists the package id, every tool with what it does, the install
  command, the Docker image refs, and the env vars. Note the SkiaSharp /
  System.Drawing decisions made in Step 3.

**`build/dependencies.props`:**
- Full rewrite. Set `<GroupDocs{Product}Mcp>` = `26.5.1`,
  `<GroupDocsMcpCore>` = the version from Metadata's current props,
  `<GroupDocs{Product}>` = the version from Step 4.
- Keep `<MicrosoftExtensionsHosting>`, `<ModelContextProtocol>`,
  `<MicrosoftSourceLinkGithub>` versions identical to Metadata's.
- **Remove** the `<SkiaSharp>` line if Step 3 decided to skip the SkiaSharp
  package; otherwise pin the same version Metadata uses (currently `3.119.0`).

**`docker/Dockerfile`:**
- Substituted from Metadata. If Step 3 said skip System.Drawing, remove the
  `apt-get install libgdiplus libfontconfig1` block AND its comment block.
  Otherwise, rewrite the System.Drawing comment to name the new product's
  specific subsystems that drive the dependency (Step 3 also requires this).
- OCI label `image.description` lists every tool the server exposes by name
  (e.g. `(Convert, GetSupportedFormats, GetDocumentInfo)`), not just the
  primary one.

**`docker/docker-compose.yml`:** substituted only.

**`.vscode/mcp.json`:**
- Substitute the server entry name `groupdocs-{product}-dev` and the project
  path `src/GroupDocs.{Product}.Mcp`.

**`src/GroupDocs.{Product}.Mcp.sln`:**
- Substitute names; **regenerate the project GUIDs** to keep them unique
  across all product MCP repos. (Do not reuse Metadata's GUIDs verbatim.)

**`src/GroupDocs.{Product}.Mcp/`:**
- `GroupDocs.{Product}.Mcp.csproj` — substitute; respect Step 3 decisions on
  SkiaSharp itemgroup and System.Drawing flag. **MUST include the
  `StripNativeRuntimePdbs` MSBuild target** (defense against NuGet.org's
  250 MB hard limit — see Pitfall #12). Drop in verbatim outside the main
  `<PropertyGroup>`:

  ```xml
  <Target Name="StripNativeRuntimePdbs" AfterTargets="Publish">
    <ItemGroup>
      <_NativeRuntimePdbs Include="$(PublishDir)runtimes/**/*.pdb" />
    </ItemGroup>
    <Delete Files="@(_NativeRuntimePdbs)" />
    <Message
      Text="StripNativeRuntimePdbs: removed @(_NativeRuntimePdbs->Count()) native PDB file(s) from publish output to keep the nupkg under NuGet.org's 250 MB limit."
      Importance="high"
      Condition="@(_NativeRuntimePdbs->Count()) > 0" />
  </Target>
  ```
- `Program.cs` — substitute namespace + service registration line for the
  `{Product}LicenseManager`.
- `{Product}LicenseManager.cs` — body per Step 3 license-class decision.
- `.mcp/server.json` — substitute; set both `version` fields to `26.5.1`,
  update `name`, `description`, `identifier`, `repository.url`.
  **CRITICAL — schema length limit**: the top-level `description` field has
  `maxLength: 100` per the MCP server.json schema (`definitions/ServerDetail/
  properties/description`). Anything longer fails the `validate-mcp-manifest`
  job in `run_tests.yml` with `'<description>' is too long`. Pick a short
  canonical sentence (≤ 100 chars) here. The csproj `<Description>`,
  `llms.txt` lead, and Dockerfile OCI label have NO such limit and can carry
  the longer marketing copy. The `name` field has `maxLength: 200` and pattern
  `^[a-zA-Z0-9.-]+/[a-zA-Z0-9._-]+$` (well within reach for our naming) —
  keep that in mind if you ever rename namespaces.
- `Tools/` — copy every `*.cs` file from
  `{FRAMEWORK_PATH}/src/GroupDocs.{Product}.Mcp/Tools/` verbatim (or with
  fixes per Step 2's sanity-check). These already use the correct namespace
  and `using` imports.

**`.github/workflows/`:** substitute names, image refs, repo URLs in all 4
files (`build_packages.yml`, `run_tests.yml`, `publish_prod.yml`,
`publish_docker.yml`).

**Branch-trigger note**: `build_packages.yml` and `run_tests.yml` MUST trigger
on **both** `master` AND `main`:

```yaml
on:
  push:
    branches: [master, main]   # NOT just [master]
  pull_request:
    branches: [master, main]
```

**Why**: GitHub repos created via `gh repo create --add-readme` use `main` as
the default branch (modern GitHub default since 2020), but the Metadata
template was authored on `master`. If the new repo's default is `main` and
the workflows only listen for `master` pushes, **CI silently never runs** —
the user pushes commits and nothing happens. This exact bug bit the
Conversion clone. Specifying both branches works regardless of which default
the new repo uses. `publish_prod.yml` and `publish_docker.yml` trigger on
tags (`'[0-9]+.[0-9]+.[0-9]+'`), not branches, so they're not affected.

### Step 5d — Build verify the main repo

```bash
cd {TARGET_MAIN_PATH}
dotnet restore
dotnet build src/GroupDocs.{Product}.Mcp.sln -c Release
```

Fix any compile errors before moving on. Common issues observed:
- A tool file calls a property that exists on Metadata's API but not the new
  product's API — e.g. `FileType.Format` (Metadata) vs `FileType.FileFormat`
  (Conversion). Read the .NET product source to find the correct property
  name; do **not** paper over with `dynamic` or reflection.
- Tool returns a generic `IDocumentInfo` interface — serialize against the
  runtime type to capture all subtype-specific properties (use
  `JsonSerializer.Serialize(info, info.GetType(), opts)` rather than the
  default interface-typed overload).

## Step 6 — Mirror the Tests repo into TARGET_TESTS_PATH

Same approach as Step 5: bulk-copy + Python substitution + product-specific
writes. The Tests repo has different content: integration-test fixtures,
how-to guides, docker-scripts.

### Step 6a — Bulk-copy + substitute

```bash
SRC_TESTS="{SOURCE_TESTS_PATH}"
DEST_TESTS="{TARGET_TESTS_PATH}"

cp "$SRC_TESTS/LICENSE" "$SRC_TESTS/global.json" \
   "$SRC_TESTS/.editorconfig" "$SRC_TESTS/.gitattributes" "$DEST_TESTS/"

# Bulk dirs
cp -r "$SRC_TESTS/Files" "$SRC_TESTS/sample-docs" "$SRC_TESTS/changelog" \
      "$SRC_TESTS/docker-scripts" "$SRC_TESTS/examples" "$SRC_TESTS/how-to" \
      "$SRC_TESTS/.github" "$DEST_TESTS/"

# Drop Metadata-era changelog entries
rm -f "$DEST_TESTS/changelog/"*-*.md

# Scaffold src
mkdir -p "$DEST_TESTS/src/GroupDocs.{Product}.Mcp.Tests/Fixtures"

# Delete dest README (gh-auto stub)
rm -f "$DEST_TESTS/README.md"
```

Then run the same Python token-substitution pass from Step 5b over
`{TARGET_TESTS_PATH}`. **Plus** these additional Tests-repo-only mappings:

```python
EXTRA_REPLS = [
    # Metadata-era tool-name references in scripts/docs need to map to the
    # new product's primary tool. WARNING: this is a semantic mapping, not
    # textual — pick the analog in the new product, then handle prose-level
    # nonsense in Step 6c.
    ('ReadMetadataTool',   '{NewPrimaryTool}Tool'),
    ('RemoveMetadataTool', '{NewPrimaryTool}Tool'),
    ('ReadMetadataTests',  '{NewPrimaryTool}Tests'),
    ('RemoveMetadataTests','{NewPrimaryTool}Tests'),
    ('ReadMetadata',       '{NewPrimaryTool}'),
    ('RemoveMetadata',     '{NewPrimaryTool}'),
    ('read_metadata',      '{newPrimaryTool}'),
    ('remove_metadata',    '{newPrimaryTool}'),
]
```

For Conversion this was `Convert`; for Comparison this was `Compare`. Pick
the most common verb in your tool list.

### Step 6b — Write product-specific Tests files

- `LICENSE` — substitute copyright line as in Step 5c.
- `README.md`, `AGENTS.md`, `llms.txt` — full rewrite for the new product.
- `Directory.Build.props` — substitute; bump `<McpPackageVersion>` to `26.5.1`.
- `GroupDocs.{Product}.Mcp.Tests.sln` — substitute; regenerate GUIDs.
- `changelog/README.md` — substitute the first sentence.
- `changelog/001-initial-commit.md` — fresh write; lists the test classes,
  the synthetic-fixture strategy, the real-sample coverage.
- `.github/workflows/integration.yml` — **MUST exist** in the new Tests repo
  (matrix × 3 OS, nightly cron, release-smoke `repository_dispatch`).
  Substitute package id, default `package_version`, the echo line. Forgetting
  this leaves the cloned Tests repo with NO CI.
- `examples/{claude-desktop.json, vscode-mcp.json, docker-compose.yml}` —
  substitute names + image refs + version strings (`26.5.1`).

**`src/GroupDocs.{Product}.Mcp.Tests/`:**
- `GroupDocs.{Product}.Mcp.Tests.csproj` — substitute; keep
  `RootNamespace = GroupDocs.{Product}.Mcp.IntegrationTests`.
- `Fixtures/CommandResolver.cs`, `Fixtures/PackageVersion.cs`,
  `Fixtures/ToolResponse.cs` — copy with namespace substitution.
- `Fixtures/ToolCatalog.cs` — rewrite the keyword-resolver properties for
  the new product's tools (e.g. `public McpClientTool Compare => Resolve("compare");`).
- `Fixtures/SampleDocuments.cs` — see Step 9 for the test-correctness
  requirement on synthetic fixtures.
- `Fixtures/McpServerFixture.cs` — substitute the temp-folder prefix
  (`gdmeta-` → e.g. `gdcomp-`), the package id, the transport name.
- `ToolDiscoveryTests.cs` — assert exactly the new tool count and resolve
  every tool by keyword.
- `ErrorHandlingTests.cs` — adapt assertions to the new product's primary
  tool.
- One `{ToolName}Tests.cs` per tool. Start from the closest analog in
  Metadata (`ReadMetadataTests.cs` ≈ "extract" tools, `RemoveMetadataTests.cs`
  ≈ "modify" tools). If the framework subproject already has integration
  tests for this product, prefer those over speculative ones.

### Step 6c — Prose rewrites (rewrite, don't substitute)

The bulk substitution leaves prose-level nonsense in any file that talks
*about* the tools rather than just naming them. **Open each of these files
and rewrite the affected sections from scratch using the new product's tool
semantics**:

- `how-to/01-install-from-nuget.md`:
  - Cache-path lines (e.g. `groupdocs.metadata.mcp` → `groupdocs.{product}.mcp`)
    — already handled by the lowercase-dot token, but verify.
  - Smoke-test "you should see two JSON-RPC responses containing X and Y"
    line — list the actual tool names of the new product.
  - **License section**: rewrite to match the new product's actual eval-mode
    behaviour. Metadata's `Save()` throws (`Could not save the file. Evaluation
    only.`). Conversion's `Convert()` produces watermarked output. Comparison's
    `Compare()` does the same. Do not carry over Metadata's exact exception
    message — it is Metadata-specific.
  - Configuration table row for `GROUPDOCS_LICENSE_PATH` — rephrase the
    "Purpose" cell to match the new eval-mode behaviour.
  - Troubleshooting rows — replace any `Could not save the file. Evaluation
    only.` row with the actual symptom of eval mode for this product.
- `how-to/02-run-via-docker.md`:
  - Same JSON-RPC tool-listing line.
  - Eval-mode license note (line ~150 in Metadata).
- `how-to/03-verify-mcp-registry.md`:
  - The MCP-registry **search keyword** suggestion in §VS Code — change
    `"or 'metadata'"` to `"or '{product}'"`.
  - The quoted **server.json description** in §VS Code — paste the canonical
    description verbatim (must match `server.json` exactly).
  - Test method names (`ServerInfo_AdvertisesGroupDocsMetadataMcp`,
    `ListTools_ExposesReadAndRemoveMetadata`) → the new test method names.
- `how-to/04-use-with-claude-desktop.md`:
  - Page intro — describe what a user can ask Claude to *do* with this product.
  - "Verify the connection" step — list the actual tool names that should
    appear in Claude's tools icon.
  - **Example prompts block** — Metadata's "Read the metadata from
    report.pdf", "Remove all metadata…" become nonsensical. Rewrite from
    scratch using the new product's tool semantics.
  - License note + troubleshooting.
- `how-to/05-use-with-vscode-copilot.md`:
  - "Server listed but tools empty" troubleshooting list of expected tools.
  - Example prompts block.
  - License note.
- `how-to/06-run-integration-tests.md`:
  - `--filter` example uses the new test-class name.
  - "Expected output" block lists the actual test method names.
- `how-to/README.md`:
  - The wire-name listing ("Tools exposed on the wire are X and Y") — list
    the new tool names with correct PascalCase.
  - License caveat.
- `docker-scripts/README.md`:
  - "Overview" bullet list — describe each tool with its actual semantics.
    Blind `ReadMetadata→{NewPrimaryTool}` substitution yields nonsense like
    "Compare — PDF/JPEG metadata extraction".
  - Service names like `metadata-server` in compose snippets → `{product}-server`.
  - Test method names in the "Expected output" sample.
- `docker-scripts/02_test-all-scenarios.sh`:
  - Header `# Scenarios:` block — list the new test classes.
- `docker-scripts/04_run-server-with-samples.sh`:
  - `--smoke` description — reflect the actual count and names of tools
    advertised. Watch for "both `compare` and `compare`" duplicate-from-
    substitution wording.

A useful approach: after substitution, grep for all of these patterns and
read the surrounding context for each hit:
- `metadata-server`
- `\`{NewPrimaryTool}\` and \`{NewPrimaryTool}\`` (duplicates)
- `Could not save\|Evaluation only\|GroupDocsMetadataException`
- `read-metadata\|read_metadata\|remove_metadata`
- The lowercase tool-name backtick form `\`{newprimarytool}\`` where the
  PascalCase form was meant.

### Step 6d — Build verify the Tests repo

```bash
cd {TARGET_TESTS_PATH}
dotnet restore
dotnet build src/GroupDocs.{Product}.Mcp.Tests/GroupDocs.{Product}.Mcp.Tests.csproj -c Release
# Skip `dotnet test` here — it's an integration suite that launches the
# published NuGet via dnx; the NuGet doesn't exist yet at clone time.
```

## Step 7 — POST-CLONE CODE-REVIEW AUDIT

After both repos build cleanly, run a structured audit. **Each finding must
be fixed before declaring the clone done.**

### Step 7a — Metadata-footprint grep

```bash
grep -rni "metadata" {TARGET_MAIN_PATH} {TARGET_TESTS_PATH} \
  --include='*.md' --include='*.cs' --include='*.csproj' --include='*.json' \
  --include='*.yml' --include='*.props' --include='*.ps1' --include='*.sh' \
  --include='*.txt' --include='LICENSE' --include='Dockerfile*' \
  --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj
```

Filter out legitimate generic uses. Hits that ARE bugs:
- `Copyright (c) … GroupDocs.Metadata Product Family` in LICENSE
- `Per-change notes for GroupDocs.Metadata.Mcp` in `changelog/README.md`
- Any tool name `ReadMetadata`/`RemoveMetadata` (or snake_case forms)
- Any prose about "metadata extraction", "metadata removal" in product docs

Hits that are NOT bugs (legitimate, leave alone):
- `AssemblyMetadataAttribute` (.NET API name)
- "assembly metadata" in PackageVersion comments (generic concept)
- `server.json metadata` (generic concept)
- `Author/Title metadata` in synthetic-PDF-builder comments (generic concept)
- `wrong metadata` in MCP-registry troubleshooting prose (generic concept)
- One historical-context line in `changelog/001` like "third product MCP
  server in the GroupDocs MCP framework (after Metadata and Conversion)"

### Step 7b — Multi-tool wording duplicates

After substituting `ReadMetadata`+`RemoveMetadata` → single new-product
primary-tool name, prose like "containing `Convert` and `Convert`" appears in
several places. Grep:

```bash
grep -rn '`{NewPrimaryTool}` and `{NewPrimaryTool}`' {TARGET_TESTS_PATH}
```

Each hit must be rewritten. If the new product has additional tools (e.g.
`GetDocumentInfo` from the optional Step 11 enhancement), the duplicate
becomes the natural place to mention them: rewrite to `Compare and
GetDocumentInfo`. Otherwise rewrite to "the `{NewPrimaryTool}` tool" (singular).

### Step 7c — Eval-mode prose check

```bash
grep -rn 'Could not save\|Evaluation only\|GroupDocsMetadataException\|GroupDocsException' \
  {TARGET_MAIN_PATH} {TARGET_TESTS_PATH}
```

Any hit is Metadata-specific eval-mode prose that survived substitution.
Rewrite to match the new product's actual eval-mode behaviour confirmed in
Step 3.

### Step 7d — Tool-name casing in prose

Tool wire names from `[McpServerTool]` are PascalCase verbatim (e.g.
`Compare`, not `compare`). Grep for backtick-quoted lowercase forms:

```bash
grep -rn '`{newprimarytool}`' {TARGET_MAIN_PATH} {TARGET_TESTS_PATH}
```

Each hit that refers to **the tool name as displayed in MCP clients** must
become PascalCase. Verb usage in English sentences ("compare two documents")
stays lowercase. Distinguishing factor: backtick-quoted = identifier =
PascalCase; unquoted in a sentence = verb = lowercase.

## Step 8 — AI-FRIENDLINESS CHECKLIST

Before declaring done, verify each item below. AI agents read multiple
surfaces (NuGet listing, MCP registry, server tool list, README) — drift
between them produces a fuzzy mental model.

1. **Canonical description (two-tier — short for server.json, long for the rest)**.
   The MCP `server.json` schema enforces `description.maxLength = 100`, so the
   description there must be short. The other AI-facing surfaces (csproj
   `<Description>`, `llms.txt` lead, Dockerfile OCI `image.description`) have
   no such limit and benefit from a longer form that lists supported formats
   and enumerates tools.

   **Short canonical** (≤ 100 chars — used verbatim in `server.json`):
   > "MCP server for GroupDocs.{Product} — {primary-verb-phrase} via AI agents."

   Examples (chars in parens):
   - Metadata: `"MCP server for GroupDocs.Metadata — read and remove document metadata via AI agents."` (84)
   - Conversion: `"MCP server for GroupDocs.Conversion — convert, inspect, and list supported formats via AI agents."` (97)
   - Comparison: `"MCP server for GroupDocs.Comparison — compare documents and inspect source info via AI agents."` (94)
   - Viewer: `"MCP server for GroupDocs.Viewer — render pages as PNG and inspect view info via AI agents."` (90)

   **Long canonical** (any length — start with the short canonical, then
   extend with format list + tool list). Used in csproj `<Description>`,
   `llms.txt` lead, Dockerfile OCI `image.description`:
   > "{Short canonical}. Supports {top-5 formats}, and 30+ more. Exposes {Tool1, Tool2, …} tools to AI agents."

   The Dockerfile OCI label can additionally enumerate the tool list in
   parentheses, e.g. `(Compare, GetDocumentInfo)` after the product name.

   **Verify length before commit**:
   ```bash
   python -c "import json; d=json.load(open('src/GroupDocs.{Product}.Mcp/.mcp/server.json',encoding='utf-8')); n=len(d['description']); print(f'{n} chars'); assert n<=100, 'TOO LONG'"
   ```

2. **Tool `[Description]` enumerates supported formats.** Each tool's
   `[McpServerTool, Description("…")]` should include a sentence like
   "Supports PDF, DOCX, XLSX, PPTX, … and 30+ more document formats." This
   helps AI clients pick the right tool against alternatives.

3. **Tool `[Description]` mentions the response format.** AI agents
   post-process responses; they need to know whether to expect JSON, a
   sentence, or a saved file path. Add one sentence describing the format
   (e.g. "Returns a JSON object with fields `fileName`, `fileType`, …" or
   "Returns either `<N> change(s) detected` or `No changes detected`,
   followed by the saved path…").

4. **README has an "Example prompts for AI agents" section.** Five 1-line
   prompts the user can copy into Claude Desktop / Cursor / Copilot. Place
   between the tools table and the Configuration table. The same prompts
   appear in `llms.txt` under "Example prompts for AI agents".

5. **AGENTS.md tools table and folder layout are accurate.** Every tool the
   server actually advertises is named in the table. Folder layout tree
   includes every `Tools/*.cs` file by name.

6. **`llms.txt` MCP-tools section lists every tool**, with a one-line
   description for each.

## Step 9 — TEST-CORRECTNESS SANITY CHECK

Run this check on every synthetic fixture in
`{TARGET_TESTS_PATH}/src/GroupDocs.{Product}.Mcp.Tests/Fixtures/SampleDocuments.cs`:

> **For each fixture: does the data the fixture provides actually exercise
> the assertion that depends on it?**

Real bug observed during the Comparison clone:
- `BuildAuthoredPdf("Original Document", …)` and `BuildAuthoredPdf("Modified
  Document", …)` produced two synthetic PDFs that differed only in the
  `/Title` of the Info dictionary.
- `Compare_DifferentSyntheticPdfs_…` asserted `change(s) detected`.
- `GroupDocs.Comparison.Compare()` compares document content (text,
  structure), **not** the Info dictionary.
- → Test would fail in CI: zero changes detected, assertion fails.

The fix was to add a `/Contents` stream with visible text drawn via
`BT /F1 24 Tf … (text) Tj ET` operators, and pass distinct text lines for
source vs target.

**General rule**: synthetic fixtures must vary in whatever the tool under
test actually processes. For:
- a content comparer → vary visible text/structure, not just metadata
- a metadata reader → vary the metadata dictionary (current Metadata pattern)
- a converter → vary input format (different file extensions)
- a redactor → embed the actual sensitive strings the redactor will look for

Read each test that uses synthetic fixtures and confirm the chain
fixture-data → tool-processing → assertion is sound. **If a test would
silently pass even when the tool is broken, it's a bug.**

## Step 10 — Final verification & summary

Print a summary to the user:

- Files written to `{TARGET_MAIN_PATH}` (count by folder)
- Files written to `{TARGET_TESTS_PATH}` (count by folder)
- Decisions made: SkiaSharp included? System.Drawing flag included?
  License-class pattern (Metadata-style vs framework-fallback)? Tool list?
- Versions: `<GroupDocs{Product}Mcp> = 26.5.1`,
  `<GroupDocsMcpCore> = X.Y.Z` (from Metadata's props),
  `<GroupDocs{Product}> = X.Y.Z` (from Step 4)
- Build status of both solutions (must be 0W/0E)
- Unit-test status for the main repo (must be all-pass)
- Step 7 audit results (must show "clean — only legitimate generic uses")
- Step 8 AI-friendliness checklist (each box ticked or with explicit note)
- Step 9 test-correctness review (any synthetic fixture where the data
  doesn't actually exercise the assertion — flag for the user)

**Next steps for the user**:
1. Review the diff in both repos.
2. Run `pwsh ./build.ps1` in the main repo to validate `server.json` ↔
   `dependencies.props` lockstep.
3. Push initial commits. Both repos were created by the
   `scripts/create-mcp-repos.py` helper with auto-README/LICENSE/.gitignore,
   so the first push is a force-push (`git push -u origin main --force`)
   to overwrite the gh-init commit.
4. Configure secrets in the new GitHub org (same names as Metadata):
   `NUGET_API_KEY_PROD`, `ES_USERNAME`/`ES_PASSWORD`/`ES_TOTP_SECRET`/
   `CODE_SIGN_CLIENT_ID`, `DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN`.
5. Trigger publish workflows with `version=26.5.1`.
6. **After the first successful `publish_docker.yml` run, flip the GHCR
   container package's visibility to PUBLIC via the web UI.** The package
   defaults to private even though the source repo is public, and the REST
   API does not expose visibility-change for container packages — only the
   UI works (see Pitfall #14). One-time per product:

   > Open `https://github.com/orgs/groupdocs-{product}/packages/container/{product}-net-mcp/package-settings`
   > → "Danger Zone" → "Change visibility" → *Public* → type the package
   > name to confirm.

   Then verify from a logged-out shell:

   ```bash
   docker logout ghcr.io
   docker pull ghcr.io/groupdocs-{product}/{product}-net-mcp:<version>
   ```

   Must succeed without `401 Unauthorized`.

Do **not** create commits or push — that's the user's call.

## Step 11 — OPTIONAL: Additional MCP tool enhancements

The framework subproject often only wires up the most obvious tool. The
underlying GroupDocs API typically supports more verbs that make excellent
additional MCP tools. After the basic clone is done, ask the user whether to
add any of these:

- **`GetDocumentInfo`** (cross-product common addition) — return source-doc
  file type, page count, size, per-page dimensions as JSON without performing
  the primary operation. Implemented via `comparer.Source.GetDocumentInfo()`,
  `converter.GetDocumentInfo()`, etc.
- **Domain-specific `Get*` queries** — e.g. for Conversion: `GetSupportedFormats`
  (list possible target formats); for Comparison: `GetChanges` (return change
  list as JSON without writing the marked-up file); for Signature:
  `GetSignatures` (list signatures without verification).
- **Multi-target / batch variants** — e.g. compare 1 source against N targets
  in a single call, convert N inputs to the same target format.

If the user opts in, add the new tool(s) per the same pattern: source file in
`Tools/`, unit test in main repo's `*Tests/`, integration test in Tests repo,
and propagate the count change through ToolDiscoveryTests + ToolCatalog +
README + AGENTS + llms.txt + changelog/001 + Dockerfile OCI label tool list.

---

## Things to ASK before guessing

If at any point you cannot determine something from the inputs, **ask the
user** rather than make it up. In particular:
- The exact MCP tool surface of the new product if the framework subproject
  is empty / out of date / has a misnamed file (e.g. a `*Tool.cs` file whose
  `[McpServerTool]` method name doesn't match the file's `*Tool` stem).
- Whether to include a tool from the framework subproject that looks
  experimental or is `[Description("…")]`-less.
- Whether the chosen sample-docs are appropriate for the new product's tools.
- Any third-party native dep beyond SkiaSharp / System.Drawing that surfaces
  during the build.
- Whether to add any of the optional Step 11 enhancement tools.

## Substitution pitfalls (learned the hard way)

These are the bugs that token-substitution alone tends to introduce. Each
clone we've done so far has hit at least three of them. Audit all of them
after each clone and fix before declaring done.

1. **Token list must cover every casing form.** The eight forms in Step 5b's
   token list. Easy to miss: `groupdocs.metadata.mcp` (lowercase-dot, NuGet
   cache path) and `GroupDocsMetadataMcp` (no dot, MSBuild prop name and
   test method substring). Forgetting these leaves real bugs that pass
   sloppy audits.
2. **Tool-name substitution is semantic, not textual.** Do not blindly map
   `ReadMetadata → ConvertTool` etc. — that turns documentation prose like
   "Convert — PDF/JPEG metadata extraction" into nonsense. The `Tools/*.cs`
   source files come from the framework subproject (so the C# code has the
   right identifiers); the **prose docs** (how-to guides, docker-scripts
   README, AGENTS.md tools tables) need rewriting based on the new product's
   actual tool semantics.
3. **Eval-mode behaviour differs per product.** Metadata's `Save()` throws
   in eval mode; Conversion's `Convert()` produces watermarked output;
   Comparison's `Compare()` does the same; Viewer may have a per-page cap.
   The "License" section of `how-to/01-install-from-nuget.md` and the
   troubleshooting tables in `04-use-with-claude-desktop.md` and
   `05-use-with-vscode-copilot.md` must be rewritten to reflect the new
   product's actual eval-mode behaviour. Do not carry over Metadata's
   "Could not save the file. Evaluation only." error message — it is
   Metadata-specific.
4. **License-class availability differs per product, and the framework
   `*LicenseManager.cs` is not authoritative.** Always verify the License
   class exists in `{PRODUCT_DOTNET_PATH}/src/Licensing/` (or similar — grep
   for `class License` and `public void SetLicense(string)`). If the License
   class IS public, use the Metadata pattern. The Conversion clone observed
   that the framework's `ConversionLicenseManager.cs` had a comment saying
   "License class is not directly exposed in v26" when in fact the class WAS
   public — using the framework's env-var fallback would have been wrong.
5. **Docs files that LOOK verbatim aren't.** Files I would have called
   "verbatim" but contain product-specific tokens:
   - `LICENSE` (copyright line)
   - `changelog/README.md` (first sentence)
   - All how-to guides (example prompts + license-section + cache paths)
6. **Files that are easy to forget to clone:**
   - Main repo: `.editorconfig`, `.gitattributes`, `.vscode/mcp.json`
   - Tests repo: `.editorconfig`, `.gitattributes`,
     `.github/workflows/integration.yml`
   The Tests repo's `.github/workflows/integration.yml` is **the entire CI**
   for the integration suite — without it, every push silently goes
   un-tested.
7. **Synthetic fixtures must exercise what the tool actually processes.**
   See Step 9. A test that asserts X but uses fixture data that can never
   produce X will fail at first CI run (best case) or pass while masking
   broken code (worst case).
8. **Description drift across surfaces is an AI-friendliness anti-pattern.**
   See Step 8. Pick one canonical sentence and use it verbatim across csproj,
   server.json, Dockerfile OCI label, llms.txt lead.
9. **Tool `[Description]` is the AI-facing surface.** It MUST enumerate
   supported formats and describe the response format — not just say what
   the tool does conceptually. AI agents pick tools based on this string.
10. **Workflow branch trigger must list both `master` AND `main`.** New
    GitHub repos created via `gh repo create --add-readme` default to `main`,
    but the Metadata template was authored on `master`. Workflows that only
    listen for `branches: [master]` silently never run on a `main`-default
    repo. Use `branches: [master, main]` in `build_packages.yml` and
    `run_tests.yml`. The Conversion clone hit this and went a full release
    cycle with zero CI runs before anyone noticed.
11. **`server.json` `description` has a hard 100-char schema limit.** The MCP
    server.json schema enforces `description.maxLength = 100`. Pasting the
    long canonical sentence used in csproj `<Description>` / `llms.txt` lead
    breaks `run_tests.yml`'s `validate-mcp-manifest` job with
    `'<description>' is too long`. Use the **short canonical** (≤ 100 chars)
    in `server.json`; use the **long canonical** elsewhere. See Step 8 for
    the two-tier pattern. The Conversion clone failed CI on this exact issue
    after first push — caught only because GitHub Actions surfaced the
    schema-validation error. (Schema also caps `name` at 200 chars and
    `title` at 100 chars, but those have not been a problem in practice.)
12. **NuGet.org has a hard 250 MB nupkg size limit; strip native PDBs.**
    SkiaSharp's NuGet packages ship Windows native PDB files
    (`runtimes/win-{x86,x64,arm64}/native/libSkiaSharp.pdb`, ≈ 80 MB each).
    With `<PackAsTool>true</PackAsTool>`, the entire publish output is packed
    into the nupkg under `tools/<TFM>/any/` — including those PDBs. For
    products with a large engine DLL (`GroupDocs.Conversion.dll` is 268 MB
    by itself), the PDBs push the nupkg from ~230 MB to ~291 MB and the
    `dotnet nuget push` step fails with `HTTP 413 RequestEntityTooLarge`
    (NuGet.org's `publish_prod` workflow then fails). The fix is the
    `StripNativeRuntimePdbs` MSBuild target documented in Step 5c — runs
    `AfterTargets="Publish"` to delete `$(PublishDir)runtimes/**/*.pdb`
    before pack collects the output. **`<CopyDebugSymbolFilesFromPackages>
    false</...>` does NOT cover this** — runtime-asset PDBs reach the
    publish output via a separate code path that property doesn't intercept.
    Apply the target to **every** product csproj as defense in depth, even
    those whose current package size is comfortably under the limit — a
    future SkiaSharp / Aspose / engine bump can add tens of MB without
    warning, and the target is a no-op when no native PDBs are present.
13. **NuGet.org's V3 flat-container index can lag `dotnet nuget push` by
    >10 minutes for large packages.** The `publish_prod.yml →
    publish_mcp_registry` job polls
    `https://api.nuget.org/v3-flatcontainer/<package-id-lowercase>/index.json`
    to confirm ownership before submitting `server.json` to the MCP Registry.
    For 200+ MB packages, malware scan + signature validation + CDN propagation
    routinely take 10–15 min, exceeding the original 10 min window. Use
    **20 attempts × 60 s = 20 min** (`seq 1 20`, `sleep 60`) — see the
    `Wait for NuGet.org to index the new version` step in `publish_prod.yml`.
    20 min comfortably covers Conversion's 230 MB nupkg (indexed in ~12 min)
    with headroom for any package up to NuGet's 250 MB hard limit. The
    Conversion clone hit this with its first successful publish — `publish_prod`
    ran clean, the package landed on NuGet.org, but the registry job timed
    out at the original 10-min window. The fix is purely in the workflow;
    no re-publish needed (the failed registry job is independently re-runnable
    from the Actions UI once the `index.json` URL returns 200).
14. **GHCR container packages default to PRIVATE — even when the source repo
    is public, and there is NO REST API to flip visibility for containers.**
    The `gh repo create --public` flag controls source-repo visibility only;
    per-package visibility for container images is a separate setting that
    GitHub creates on first `docker push ghcr.io/…` and defaults to *Private*.
    There is no `gh repo create` flag, no template setting, and no
    pre-creation route to set the default — the package record only exists
    once the first push lands. Result: the first successful
    `publish_docker.yml` run produces an image at
    `ghcr.io/groupdocs-{product}/{product}-net-mcp` that anonymous `docker
    pull` can't access (`401 Unauthorized`), even though the source repo and
    the Docker Hub mirror are both public.

    **The REST API does NOT expose visibility-change for container packages.**
    `PATCH /orgs/{org}/packages/container/{name}/visibility` returns HTTP 404
    (the endpoint exists for *some* package types but not containers — verified
    against the live API). The only working path is the **org Packages UI**:

    > `https://github.com/orgs/groupdocs-{product}/packages/container/{product}-net-mcp/package-settings`
    > → "Danger Zone" → "Change visibility" → *Public* → confirm by typing
    > the package name.

    **Verify** from a logged-out shell after flipping:

    ```bash
    docker logout ghcr.io
    docker pull ghcr.io/groupdocs-{product}/{product}-net-mcp:<version>
    ```

    Must succeed without `401 Unauthorized`. Optionally inspect the API state:

    ```bash
    gh api orgs/groupdocs-{product}/packages/container/{product}-net-mcp \
      -q '.visibility'   # should print "public"
    ```

    Note: the **list** endpoint
    `gh api orgs/{org}/packages?package_type=container` only returns *public*
    packages by default, so seeing an empty list does NOT mean the package
    doesn't exist — it just means no public packages exist. Use the direct
    GET above to inspect a private package's visibility.

    The Conversion clone hit this: post-26.5.0 release, `docker pull
    ghcr.io/groupdocs-conversion/conversion-net-mcp:26.5.0` returned 401 for
    anonymous users until the visibility was flipped manually via the UI.
    The Metadata package (older — created before this trap was understood)
    happens to already be public; it's the one outlier in the family.
15. **`ToolCatalog` keyword resolver must use the SNAKE_CASE wire name, not
    PascalCase or smashed-together lowercase.** ModelContextProtocol's
    `[McpServerTool]` attribute generates wire names by taking the C# method
    name and converting PascalCase → snake_case (`GetDocumentInfo` →
    `get_document_info`, `GetViewInfo` → `get_view_info`). The substring
    resolver in [Tests/Fixtures/ToolCatalog.cs](#) does literal `Contains`
    matching:

    ```csharp
    public McpClientTool DocumentInfo => Resolve("documentinfo");  // ❌ never matches
    public McpClientTool DocumentInfo => Resolve("document_info"); // ✓
    ```

    `"get_document_info".Contains("documentinfo")` is **false** because of
    the underscore. The Conversion clone shipped with `Resolve("documentinfo")`
    and 7 of 12 integration tests failed with `No tool with name containing
    'documentinfo'. Found: get_supported_formats, get_document_info, convert`.

    **Audit rule for new clones**: every `Resolve("…")` keyword must be a
    snake_case substring of an actual `[McpServerTool]` method's snake_case
    wire name. Run the integration suite once after clone — a single failed
    assertion of this form catches it immediately.

    Same trap also bit the Comparison clone (`documentinfo`) and the Viewer
    clone (`viewinfo` for `get_view_info`). Audit the resolver line whenever
    the new product has a multi-word tool name.
16. **Do NOT pass JSON through `OutputHelper.TruncateText` — its truncation
    marker is plain text and breaks JSON parsing.** `GroupDocs.Mcp.Core`'s
    `TruncateText(string)` appends `[Output truncated — showing first X of Y
    characters. ...]` after the content when length exceeds
    `McpConfig.MaxOutputCharacters` (default **5000**). For tools that emit
    structured JSON (`GetDocumentInfoTool`, `GetSupportedFormatsTool`,
    `ReadMetadataTool`, `GetViewInfoTool`, similar), wrapping the JSON
    response in `TruncateText` produces an invalid JSON document — the
    suffix line trips `JsonSerializer.Deserialize`/`JsonDocument.Parse`,
    and integration tests fail with `Tool response was not valid JSON. Body:
    {…}\n\n[Output truncated — …]`.

    **Rule**: tools that return JSON should `return JsonSerializer.Serialize(…)`
    directly. Only text-output tools (e.g. `ConvertTool` returning a
    human-readable description) may pass through `TruncateText`. The JSON
    is bounded in practice by API realities (a few KB to ~30 KB per response);
    if a future tool genuinely needs to truncate inside a JSON response, do
    it structurally — cap an inner array and add a `truncated: true` field —
    not by appending a non-JSON marker.

    The Conversion clone hit this with `GetSupportedFormats` on a JPEG source
    (60+ target formats → ~6 KB JSON → marker appended → JSON parser fails
    in the integration test). Fixed by removing the `output.TruncateText(json)`
    call from the four affected tools across all four products. Audit any
    new tool that returns JSON for this pattern.
17. **Linux integration runners need `libgdiplus` + `libfontconfig1` —
    Docker has them, the bare GitHub runner does not.** The main repo's
    `docker/Dockerfile` installs these because `System.Drawing.EnableUnixSupport
    = true` requires the managed System.Drawing implementation
    (`libgdiplus`) and a font discovery library (`libfontconfig1`). The Tests
    repo's `.github/workflows/integration.yml` runs on a fresh `ubuntu-24.04`
    GitHub-hosted runner with neither installed — so any tool that internally
    touches System.Drawing (PDF rendering, image conversion, image-bearing
    metadata reads) crashes with a generic
    `An error occurred invoking '<tool>'` wrapping `PlatformNotSupportedException`
    or `DllNotFoundException`.

    **Fix**: add an apt-get step to `integration.yml` BEFORE the test step:

    ```yaml
    - name: Install Linux native deps
      if: runner.os == 'Linux'
      run: |
        sudo apt-get update
        sudo apt-get install -y --no-install-recommends libgdiplus libfontconfig1
    ```

    Apply to **every** product Tests repo whose csproj has
    `<RuntimeHostConfigurationOption Include="System.Drawing.EnableUnixSupport"
    Value="true" />` — currently all four (Metadata, Conversion, Comparison,
    Viewer). The Conversion clone hit this: PDF→HTML conversion failed only
    on the Linux matrix leg of the integration suite while DOCX→HTML and
    PDF→PDF passed. macOS runners may need a parallel `brew install
    mono-libgdiplus` step — confirm by running the suite once before adding
    speculatively.

## Things explicitly NOT to do

- Do not invent new env vars beyond `GROUPDOCS_MCP_STORAGE_PATH`,
  `GROUPDOCS_MCP_OUTPUT_PATH`, `GROUPDOCS_LICENSE_PATH`.
- Do not change the target framework from `net10.0`.
- Do not edit `obj/` or `build_out/` (build artifacts).
- Do not hardcode versions in `.csproj` — they flow from
  `build/dependencies.props`.
- Do not remove `<PackageType>McpServer</PackageType>` or
  `<ToolCommandName>` from the csproj (NuGet.org discoverability + `dnx` need
  them).
- Do not commit, push, or open PRs without explicit user instruction.
- Do not reuse Metadata's solution-file project GUIDs verbatim — regenerate
  to keep GUIDs unique across the GroupDocs MCP repo family.
- Do not rely on hardcoded local paths anywhere in the clone process —
  every path in this prompt is one of the six variables collected in Step 0.
