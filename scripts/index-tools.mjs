import { lstat, readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { format } from "prettier";

const root = fileURLToPath(new URL("..", import.meta.url));
const slugPattern = /^[a-z][a-z0-9-]*$/;
const fields = [
  "slug",
  "id",
  "title",
  "category",
  "monogram",
  "version",
  "releaseDate",
  "updatedOn",
  "summary",
  "catalogDescription",
  "usefulness",
  "description",
  "howItWorks",
  "features",
  "limitations",
  "downloadNotice",
  "useCases",
  "setup",
  "workspace",
  "source",
];
const unsafeSource =
  /(?:^|\/)(?:\.git|node_modules|bin|obj|artifacts|\.fixtures|state|sessions?|history|cache|logs?)(?:\/|$)|(?:^|\/)\.env(?:\.|$)|\.(?:exe|dll|db|sqlite|zip|mp4|webm|part|pfx|pem|key)$/i;

function requireValue(condition, message) {
  if (!condition) throw new Error(message);
}

function object(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function text(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function date(value) {
  return (
    typeof value === "string" &&
    /^\d{4}-\d{2}-\d{2}$/.test(value) &&
    new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value
  );
}

async function plainPath(file, directory = false) {
  const info = await lstat(file);
  requireValue(
    !info.isSymbolicLink(),
    `Linked project/source path is not allowed: ${file}`,
  );
  requireValue(
    directory ? info.isDirectory() : info.isFile(),
    `Expected ${directory ? "directory" : "file"}: ${file}`,
  );
}

export async function assertNoLinks(file) {
  let current = path.resolve(file);
  while (true) {
    try {
      requireValue(
        !(await lstat(current)).isSymbolicLink(),
        `Linked output path is not allowed: ${current}`,
      );
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }
    const parent = path.dirname(current);
    if (parent === current) return;
    current = parent;
  }
}

async function exists(file) {
  try {
    await lstat(file);
    return true;
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
    return false;
  }
}

function validateTool(tool, folder) {
  requireValue(object(tool), `Invalid portal metadata: ${folder}`);
  requireValue(
    Object.keys(tool).every((key) => fields.includes(key)),
    `Unknown portal metadata field: ${folder}`,
  );
  for (const key of [
    "slug",
    "id",
    "title",
    "category",
    "version",
    "summary",
    "description",
  ])
    requireValue(text(tool[key]), `Missing ${key}: ${folder}`);
  requireValue(
    slugPattern.test(tool.slug) && slugPattern.test(tool.id),
    `Invalid slug/id: ${folder}`,
  );
  requireValue(
    date(tool.updatedOn) &&
      (tool.releaseDate === null || date(tool.releaseDate)),
    `Invalid dates: ${folder}`,
  );
  for (const key of ["catalogDescription", "monogram", "downloadNotice"])
    if (tool[key] !== undefined)
      requireValue(text(tool[key]), `Invalid ${key}: ${folder}`);
  if (tool.catalogDescription !== undefined) {
    const count = tool.catalogDescription.trim().split(/\s+/).length;
    requireValue(
      count >= 50 && count <= 100,
      `catalogDescription must contain 50-100 words: ${folder}`,
    );
  }
  if (tool.usefulness !== undefined)
    requireValue(
      Number.isInteger(tool.usefulness) &&
        tool.usefulness >= 1 &&
        tool.usefulness <= 10,
      `Invalid editorial usefulness: ${folder}`,
    );
  requireValue(
    Array.isArray(tool.useCases) &&
      tool.useCases.length > 0 &&
      tool.useCases.every(text),
    `Invalid useCases: ${folder}`,
  );
  if (tool.limitations !== undefined)
    requireValue(
      Array.isArray(tool.limitations) && tool.limitations.every(text),
      `Invalid limitations: ${folder}`,
    );
  for (const key of ["setup", "features", "howItWorks"]) {
    if (key !== "setup" && tool[key] === undefined) continue;
    requireValue(
      Array.isArray(tool[key]) &&
        tool[key].length > 0 &&
        tool[key].every(
          (step) =>
            object(step) &&
            text(step.title) &&
            text(step.text) &&
            Object.keys(step).every((field) =>
              [
                "title",
                "text",
                ...(key === "setup" ? ["command"] : []),
              ].includes(field),
            ) &&
            (step.command === undefined || text(step.command)),
        ),
      `Invalid ${key}: ${folder}`,
    );
  }
  if (tool.workspace !== undefined)
    requireValue(
      ["toolbar", "videocrunch", "directory-cleaner"].includes(tool.workspace),
      `Unknown workspace (requires explicit implementation): ${folder}`,
    );
  if (tool.source !== undefined)
    requireValue(
      object(tool.source) &&
        Object.keys(tool.source).every((key) => key === "notice") &&
        (tool.source.notice === undefined || text(tool.source.notice)),
      `Source bundle/files are derived from the project folder, not metadata: ${folder}`,
    );
}

export async function readSourceFiles(sourceRoot) {
  await plainPath(sourceRoot, true);
  const manifest = path.join(sourceRoot, "source-files.json");
  await plainPath(manifest);
  const definition = JSON.parse(await readFile(manifest, "utf8"));
  const names = Array.isArray(definition) ? definition : definition?.files;
  requireValue(
    Array.isArray(names) &&
      names.length > 0 &&
      names.every(
        (name) =>
          typeof name === "string" &&
          /^[\w./-]+$/.test(name) &&
          !name.startsWith("/") &&
          name
            .split("/")
            .every((part) => part && part !== "." && part !== "..") &&
          !unsafeSource.test(name),
      ) &&
      new Set(names).size === names.length,
    `Unsafe or invalid source-files.json: ${sourceRoot}`,
  );
  const files = [];
  for (const name of names) {
    let file = sourceRoot;
    const parts = name.split("/");
    for (const [index, part] of parts.entries()) {
      file = path.join(file, part);
      await plainPath(file, index < parts.length - 1);
    }
    const bytes = await readFile(file);
    requireValue(
      !bytes.includes(0) &&
        Buffer.from(bytes.toString("utf8"), "utf8").equals(bytes),
      `Source is not UTF-8 text: ${file}`,
    );
    files.push({ name, bytes });
  }
  return files;
}

export async function discoverTools(toolsRoot = path.join(root, "tools")) {
  await plainPath(toolsRoot, true);
  const projects = [];
  const unregistered = [];
  const slugs = new Set();
  const ids = new Set();
  for (const entry of await readdir(toolsRoot, { withFileTypes: true })) {
    if (!entry.isDirectory() && !entry.isSymbolicLink()) continue;
    const sourceRoot = path.join(toolsRoot, entry.name);
    await plainPath(sourceRoot, true);
    requireValue(
      !(await exists(path.join(sourceRoot, ".git"))),
      `Nested Git repository is not allowed: ${sourceRoot}`,
    );
    const manifest = path.join(sourceRoot, "portal.json");
    if (!(await exists(manifest))) {
      unregistered.push(entry.name);
      continue;
    }
    requireValue(
      slugPattern.test(entry.name),
      `Invalid project folder name: ${entry.name}`,
    );
    await plainPath(manifest);
    const metadata = JSON.parse(await readFile(manifest, "utf8"));
    requireValue(
      object(metadata) &&
        Object.keys(metadata).every((key) => ["order", "tool"].includes(key)) &&
        (metadata.order === undefined || Number.isSafeInteger(metadata.order)),
      `Invalid portal manifest: ${manifest}`,
    );
    validateTool(metadata.tool, entry.name);
    const tool = { ...metadata.tool };
    requireValue(
      !slugs.has(tool.slug) && !ids.has(tool.id),
      `Duplicate tool slug/id: ${tool.slug}`,
    );
    slugs.add(tool.slug);
    ids.add(tool.id);
    if (tool.source !== undefined) {
      const files = await readSourceFiles(sourceRoot);
      tool.source = {
        bundle: entry.name,
        files: files.map((file) => file.name),
        ...tool.source,
      };
    }
    projects.push({
      folder: entry.name,
      sourceRoot,
      order: metadata.order ?? 1000,
      tool,
    });
  }
  projects.sort(
    (a, b) =>
      a.order - b.order ||
      (a.folder < b.folder ? -1 : a.folder > b.folder ? 1 : 0),
  );
  return { projects, unregistered: unregistered.sort() };
}

export async function indexTools({
  toolsRoot,
  output = path.join(
    root,
    "packages",
    "portal",
    "src",
    "data",
    "tool-index.ts",
  ),
  check = false,
} = {}) {
  const { projects, unregistered } = await discoverTools(toolsRoot);
  const content = await format(
    `// Generated by pnpm tools:index. Edit tools/<project>/portal.json instead.
export const TOOL_INDEX = ${JSON.stringify(projects.map((project) => project.tool))} as const;
`,
    { parser: "typescript" },
  );
  await assertNoLinks(output);
  await plainPath(path.dirname(output), true);
  const present = await exists(output);
  if (present) await plainPath(output);
  const previous = present ? await readFile(output, "utf8") : null;
  if (check)
    requireValue(
      previous?.replaceAll("\r\n", "\n") === content,
      "Tool index is stale. Run pnpm tools:index.",
    );
  else if (previous?.replaceAll("\r\n", "\n") !== content)
    await writeFile(output, content);
  return { projects, unregistered };
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  requireValue(
    process.argv.slice(2).every((arg) => arg === "--check") &&
      process.argv.length <= 3,
    "Usage: node scripts\\index-tools.mjs [--check]",
  );
  const { projects, unregistered } = await indexTools({
    check: process.argv.includes("--check"),
  });
  console.log(
    `Indexed ${projects.length} tool projects from tools/. No project code executed.`,
  );
  for (const folder of unregistered)
    console.warn(
      `Not indexed: tools/${folder}. Add portal.json to register it.`,
    );
}
