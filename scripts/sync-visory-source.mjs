import { createHash } from "node:crypto";
import { lstat, mkdir, readFile, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { findToolManifest } from "./index-tools.mjs";

const root = fileURLToPath(new URL("..", import.meta.url));
export const provenanceName = "Data/Source/SOURCE-PROVENANCE.json";
const repository = "Moorky/moorkyz-tools";
const sourceDirectory = "tools/visory-media-downloader";
const digest = (bytes) => createHash("sha256").update(bytes).digest("hex");

function validateNames(names) {
  if (
    !Array.isArray(names) ||
    !names.includes(provenanceName) ||
    new Set(names).size !== names.length
  )
    throw new Error(
      "Invalid Moorksz Visory Media Downloader source allowlist.",
    );
  for (const name of names) {
    if (
      typeof name !== "string" ||
      !/^[\w./-]+$/.test(name) ||
      name.startsWith("/") ||
      name.split("/").some((part) => !part || part === "." || part === "..") ||
      /(?:^|\/)(?:\.git|bin|obj|artifacts|node_modules|\.fixtures|runtime|state|sessions?)(?:\/|$)/i.test(
        name,
      ) ||
      (!/\.(?:cs|xaml|csproj|sln|props|md|html|css|mjs|cmd|ps1|json|manifest|txt|b64)$/.test(
        name,
      ) &&
        !/(?:^|\/)(?:LICENSE|\.gitignore)$/.test(name))
    )
      throw new Error(
        `Unsafe Moorksz Visory Media Downloader source path: ${name}`,
      );
  }
}

async function readSource(sourceRoot, name) {
  let file = sourceRoot;
  for (const part of name.split("/")) {
    file = path.join(file, part);
    if ((await lstat(file)).isSymbolicLink())
      throw new Error(`Linked source is not distributable: ${name}`);
  }
  const bytes = await readFile(file);
  if (
    bytes.includes(0) ||
    !Buffer.from(bytes.toString("utf8"), "utf8").equals(bytes)
  )
    throw new Error(`Source is not UTF-8 text: ${name}`);
  return bytes;
}

async function checkOutput(target) {
  let current = path.resolve(target);
  while (true) {
    try {
      if ((await lstat(current)).isSymbolicLink())
        throw new Error(`Linked provenance output is not allowed: ${current}`);
    } catch (cause) {
      if (
        !(cause instanceof Error) ||
        !("code" in cause) ||
        cause.code !== "ENOENT"
      )
        throw cause;
    }
    const parent = path.dirname(current);
    if (parent === current) return;
    current = parent;
  }
}

export async function createVisorySnapshot(sourceRoot, names) {
  validateNames(names);
  const source = await realpath(sourceRoot);
  const files = [];
  for (const name of names.filter((name) => name !== provenanceName))
    files.push({ name, bytes: await readSource(source, name) });
  const hashes = files.map(({ name, bytes }) => ({
    path: name,
    sha256: digest(bytes),
  }));
  const provenance = {
    format: 1,
    repository,
    sourceDirectory,
    identity: "Moorksz Visory Media Downloader",
    treeSha256: digest(JSON.stringify(hashes)),
    files: hashes,
    note: "Canonical UTF-8 source maintained in tools/visory-media-downloader in this repository, not a release. After local edits, refresh this manifest using node scripts/sync-visory-source.mjs.",
  };
  return { files, provenance };
}

export async function verifyVisorySnapshot(sourceRoot, names) {
  const { provenance } = await createVisorySnapshot(sourceRoot, names);
  const recorded = JSON.parse(
    (await readSource(sourceRoot, provenanceName)).toString("utf8"),
  );
  if (JSON.stringify(recorded) !== JSON.stringify(provenance))
    throw new Error(
      "Moorksz Visory Media Downloader source provenance mismatch. After reviewing local edits in tools/visory-media-downloader, run node scripts\\sync-visory-source.mjs.",
    );
  return provenance;
}

export async function updateVisoryProvenance(sourceRoot, names) {
  const target = path.join(sourceRoot, provenanceName);
  await checkOutput(target);
  const { provenance } = await createVisorySnapshot(sourceRoot, names);
  await mkdir(path.dirname(target), { recursive: true });
  await writeFile(target, `${JSON.stringify(provenance, null, 2)}\n`);
  return provenance;
}

async function sync() {
  if (process.argv.length > 2)
    throw new Error(
      "Arguments are not supported. Edit tools\\visory-media-downloader in this repository, then run node scripts\\sync-visory-source.mjs.",
    );
  const sourceRoot = path.join(root, "tools", "visory-media-downloader");
  const manifest = await findToolManifest(sourceRoot, "source-files.json");
  if (!manifest)
    throw new Error(
      "Missing Moorksz Visory Media Downloader source-files.json.",
    );
  const names = JSON.parse(await readFile(manifest, "utf8"));
  const provenance = await updateVisoryProvenance(sourceRoot, names);
  console.log(
    `Updated ${repository} ${sourceDirectory}: ${provenance.files.length} local text files, tree SHA-256 ${provenance.treeSha256}. No release or remote operation performed.`,
  );
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  await sync();
}
