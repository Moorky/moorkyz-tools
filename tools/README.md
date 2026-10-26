# Tool projects: drop them here

This is the canonical home for every Moorkyz Tools project. On this checkout it
is `C:\Users\marvi\source\repos\moorkyz-tools\tools`. The outer `repos` directory
only contains the portal checkout; individual tools do not get sibling or nested
repositories. Drag the **whole project folder** into this folder.

```text
tools/
  my-tool/
    portal.json          Portal metadata, no executable configuration
    source-files.json    Optional explicit list of downloadable text files
    README.md
    src/                 The actual project; its own layout stays intact
```

## Register a dragged-in project

Add `portal.json` inside the project. Existing projects provide complete examples,
including [Visory](visory-media-downloader/portal.json).

```json
{
  "order": 60,
  "tool": {
    "slug": "my-tool",
    "id": "moorkyz-tools-my-tool",
    "title": "Moorkyz My Tool",
    "category": "LOCAL TOOL",
    "version": "0.1.0",
    "releaseDate": null,
    "updatedOn": "2026-10-26",
    "summary": "A concise, honest description.",
    "catalogDescription": "Use this space to explain the real purpose of your tool in fifty to one hundred words. Describe the input it accepts, the useful output it produces and what stays on the user's own machine. Mention any prerequisites, development-preview limitations or manual steps honestly. Keep setup commands in the setup section rather than turning this description into an installation script.",
    "usefulness": 6,
    "description": "Explain what the project actually does and its limitations.",
    "useCases": ["Describe a practical use."],
    "setup": [
      { "title": "Read the guide", "text": "Follow the project's README." }
    ],
    "source": {}
  }
}
```

Use unique lowercase hyphenated IDs/slugs and valid `YYYY-MM-DD` dates.
`releaseDate: null` means unpublished. `order` is optional; equal/default orders
sort by folder name. Editorial `usefulness` is an integer 1-10, not a benchmark.
For the current catalog supply the 50-100-word `catalogDescription` and score;
the indexer also accepts older metadata retained in Git history.

For `"source": {}`, add `source-files.json`:

```json
["README.md", "src/main.mjs", "portal.json", "source-files.json"]
```

Every entry is an exact relative path to UTF-8 text; no globs, binaries, media,
secrets, runtime state, caches, dependencies, Git internals or linked paths.
Only include files you intend to serve in the public source viewer and ZIP.
Omit `source` entirely for an explanation-only project without downloads.
Do not add a `.git` file/directory; explicitly approved repository splits are
exceptions, not part of this workflow.

From the repository root:

```powershell
pnpm tools:index
pnpm build
```

Portal dev/build index automatically. Cards, search, route metadata/detail pages
and source downloads all use the same generated index. No edits to
[`tools.ts`](../packages/portal/src/data/tools.ts) or a central bundle array are
needed. `pnpm tools:index:check` verifies the generated index without changing it.
Additions/removals take effect on the next index/build; restart an already running
server. This is not a filesystem watcher.

Plain dragged-in folders without `portal.json` are reported as **not indexed**.
Their README can be used to prepare honest metadata, but arbitrary code is never
run to guess descriptions, install dependencies or enable services. Existing
Media Recover/Visory SHA-256 provenance must still be explicitly refreshed after
reviewing source or metadata changes. Failed manifests or unsafe source files
stop the build rather than producing an incomplete success-shaped catalog.

Only existing built-in `workspace` values are accepted. A new desktop/script tool
can use the shared explanation/source page immediately; a new interactive
workspace or local-service connection still needs explicit code and tests.
Indexing does not start apps, create accounts, publish, deploy or contact anything.
