import assert from "node:assert/strict";
import { test } from "node:test";
import {
  mkdtemp,
  mkdir,
  readFile,
  writeFile,
  rm,
  symlink,
  rename,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import {
  discoverTools,
  indexTools,
  readSourceFiles,
} from "../../scripts/index-tools.mjs";
import { prepareToolSources } from "../../scripts/prepare-tool-sources.mjs";
import { TOOLS } from "../../packages/portal/src/data/tools.ts";

const tool = (slug = "fixture") => ({
  slug,
  id: `moorkyz-tools-${slug}`,
  title: `Fixture ${slug}`,
  category: "FIXTURE",
  version: "0.1.0",
  releaseDate: null,
  updatedOn: "2026-10-26",
  summary: "Synthetic fixture.",
  description: "Isolated test project, never a real recording.",
  useCases: ["Test the catalog."],
  setup: [{ title: "Read", text: "Read the source." }],
});

async function fixture(run) {
  const root = await mkdtemp(path.join(tmpdir(), "tool-index-"));
  const toolsRoot = path.join(root, "tools");
  await mkdir(toolsRoot);
  const options = {
    toolsRoot,
    indexOutput: path.join(root, "tool-index.ts"),
    output: path.join(root, "downloads"),
  };
  const add = async (folder, metadata = { tool: tool(folder) }) => {
    const project = path.join(toolsRoot, folder);
    await mkdir(project);
    await writeFile(
      path.join(project, "portal.json"),
      JSON.stringify(metadata),
    );
    return project;
  };
  try {
    await run({ root, toolsRoot, options, add });
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

test("discovery exactly preserves the current registry, source allowlists and order", async () => {
  const { projects, unregistered } = await discoverTools();
  assert.deepEqual(
    projects.map((project) => project.tool),
    TOOLS,
  );
  assert.deepEqual(unregistered, []);
});

test("dropping a self-contained folder indexes metadata and exact source bytes without executing code", async () => {
  await fixture(async ({ root, options, add }) => {
    const project = await add("fixture", { tool: { ...tool(), source: {} } });
    const source = 'throw new Error("Project code must never execute");\r\n';
    await writeFile(path.join(project, "entry.mjs"), source);
    await writeFile(
      path.join(project, "source-files.json"),
      JSON.stringify(["entry.mjs", "portal.json", "source-files.json"]),
    );
    await prepareToolSources(options);
    const index = await readFile(options.indexOutput, "utf8");
    assert.match(index, /Fixture fixture/);
    assert.match(index, /bundle: "fixture"/);
    const downloads = JSON.parse(
      await readFile(path.join(options.output, "fixture.json"), "utf8"),
    );
    assert.equal(downloads["entry.mjs"], source);
    const zip = await readFile(path.join(options.output, "fixture.zip"));
    assert.ok(zip.includes(Buffer.from(source)));
    await indexTools({
      toolsRoot: options.toolsRoot,
      output: options.indexOutput,
      check: true,
    });
    assert.deepEqual(
      (await discoverTools(options.toolsRoot)).projects.map((p) => p.folder),
      ["fixture"],
    );
    assert.equal(
      await readFile(path.join(project, "entry.mjs"), "utf8"),
      source,
    );
    assert.ok(root);
  });
});

test("new tools are discovered without registry edits; removal retires old generated assets", async () => {
  await fixture(async ({ root, options, add }) => {
    const first = await add("first", {
      order: 20,
      tool: { ...tool("first"), source: {} },
    });
    await writeFile(path.join(first, "README.md"), "# First");
    await writeFile(path.join(first, "source-files.json"), '["README.md"]');
    await prepareToolSources(options);
    await add("second", { order: 10, tool: tool("second") });
    await assert.rejects(
      indexTools({
        toolsRoot: options.toolsRoot,
        output: options.indexOutput,
        check: true,
      }),
      /stale/,
    );
    await prepareToolSources(options);
    assert.deepEqual(
      (await discoverTools(options.toolsRoot)).projects.map((p) => p.tool.slug),
      ["second", "first"],
    );
    await rename(first, path.join(root, "removed-project"));
    await prepareToolSources(options);
    assert.deepEqual(
      (await discoverTools(options.toolsRoot)).projects.map((p) => p.tool.slug),
      ["second"],
    );
    await assert.rejects(readFile(path.join(options.output, "first.json")), {
      code: "ENOENT",
    });
    await assert.rejects(readFile(path.join(options.output, "first.zip")), {
      code: "ENOENT",
    });
  });
});

test("folders without metadata are explicitly reported, not guessed or published", async () => {
  await fixture(async ({ toolsRoot }) => {
    await mkdir(path.join(toolsRoot, "draft"));
    assert.deepEqual(await discoverTools(toolsRoot), {
      projects: [],
      unregistered: ["draft"],
    });
  });
});

test("native object-format allowlists remain usable without changing native tooling", async () => {
  await fixture(async ({ options, add }) => {
    const project = await add("fixture", {
      tool: { ...tool(), source: {} },
    });
    await writeFile(path.join(project, "README.md"), "# Native fixture\r\n");
    await writeFile(
      path.join(project, "source-files.json"),
      JSON.stringify({
        product: "Native fixture",
        files: ["README.md"],
      }),
    );
    await prepareToolSources(options);
    assert.equal(
      JSON.parse(
        await readFile(path.join(options.output, "fixture.json"), "utf8"),
      )["README.md"],
      "# Native fixture\r\n",
    );
  });
});

test("duplicate IDs/slugs, invalid metadata and unknown workspaces fail explicitly", async () => {
  await fixture(async ({ toolsRoot, add }) => {
    const first = await add("first");
    const second = await add("second", { tool: tool("first") });
    await assert.rejects(discoverTools(toolsRoot), /Duplicate/);
    await rm(second, { recursive: true });
    for (const invalid of [
      { ...tool(), slug: "../escape" },
      { ...tool(), updatedOn: "2026-02-30" },
      { ...tool(), source: { files: ["secret.txt"] } },
      { ...tool(), workspace: "arbitrary-script" },
      { ...tool(), usefulness: 11 },
      { ...tool(), setup: [] },
      { ...tool(), unexpected: true },
    ]) {
      await writeFile(
        path.join(first, "portal.json"),
        JSON.stringify({ tool: invalid }),
      );
      await assert.rejects(discoverTools(toolsRoot));
    }
    await writeFile(path.join(first, "portal.json"), "{");
    await assert.rejects(discoverTools(toolsRoot), SyntaxError);
  });
});

test("nested Git roots and linked tool folders/manifests are rejected", async () => {
  await fixture(async ({ root, toolsRoot, add }) => {
    const project = await add("fixture");
    await writeFile(path.join(project, ".git"), "gitdir: unrelated");
    await assert.rejects(discoverTools(toolsRoot), /Nested Git/);
    await rm(path.join(project, ".git"));
    await symlink(project, path.join(toolsRoot, "linked"), "junction");
    await assert.rejects(discoverTools(toolsRoot), /Linked/);
    await rm(path.join(toolsRoot, "linked"));
    const elsewhere = path.join(root, "metadata");
    await mkdir(elsewhere);
    await rename(
      path.join(project, "portal.json"),
      path.join(elsewhere, "portal.json"),
    );
    // Directory links cover manifest ancestors without requiring Windows file-symlink privileges.
    await symlink(elsewhere, path.join(project, "linked"), "junction");
    await writeFile(
      path.join(project, "source-files.json"),
      '["linked/portal.json"]',
    );
    await assert.rejects(readSourceFiles(project), /Linked/);
  });
});

test("source bundles reject traversal, secrets, runtime state, duplicate names and non-text bytes", async () => {
  await fixture(async ({ add }) => {
    const project = await add("fixture");
    for (const name of [
      "../secret",
      "/absolute",
      "a\\file",
      ".git/config",
      ".env",
      "state/private.json",
      "artifacts/app.exe",
      "recording.mp4",
      "private.pem",
    ]) {
      await writeFile(
        path.join(project, "source-files.json"),
        JSON.stringify([name]),
      );
      await assert.rejects(readSourceFiles(project), /Unsafe or invalid/);
    }
    await writeFile(path.join(project, "README.md"), "# Fixture");
    await writeFile(
      path.join(project, "source-files.json"),
      '["README.md","README.md"]',
    );
    await assert.rejects(readSourceFiles(project), /Unsafe or invalid/);
    await writeFile(path.join(project, "source-files.json"), '["README.md"]');
    await writeFile(path.join(project, "README.md"), Buffer.from([0, 255]));
    await assert.rejects(readSourceFiles(project), /not UTF-8/);
  });
});

test("unsafe projects cannot replace previously generated downloads", async () => {
  await fixture(async ({ options, add }) => {
    const first = await add("first", {
      tool: { ...tool("first"), source: {} },
    });
    await writeFile(path.join(first, "README.md"), "# First");
    await writeFile(path.join(first, "source-files.json"), '["README.md"]');
    await prepareToolSources(options);
    const before = await readFile(path.join(options.output, "first.zip"));
    await writeFile(path.join(first, "README.md"), "# Changed");
    const bad = await add("bad", { tool: { ...tool("bad"), source: {} } });
    await writeFile(path.join(bad, "source-files.json"), '["../secret"]');
    await assert.rejects(prepareToolSources(options), /Unsafe/);
    assert.deepEqual(
      await readFile(path.join(options.output, "first.zip")),
      before,
    );
  });
});

test("index/output ancestor junctions cannot redirect generated writes", async () => {
  await fixture(async ({ root, options, add }) => {
    await add("fixture");
    const target = path.join(root, "outside");
    await mkdir(target);
    const linked = path.join(root, "linked");
    await symlink(target, linked, "junction");
    await assert.rejects(
      indexTools({
        toolsRoot: options.toolsRoot,
        output: path.join(linked, "index.ts"),
      }),
      /Linked output/,
    );
    await assert.rejects(
      prepareToolSources({
        ...options,
        output: path.join(linked, "downloads"),
      }),
      /Linked output/,
    );
    await assert.rejects(readFile(path.join(target, "index.ts")), {
      code: "ENOENT",
    });
  });
});
