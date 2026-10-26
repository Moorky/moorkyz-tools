import { createHash } from "node:crypto";
import { lstat, mkdir, readFile, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
export const provenanceName = "SOURCE-PROVENANCE.json";
const repository = "Moorky/moorkyz-tools-mediarecover";
const digest = (bytes) => createHash("sha256").update(bytes).digest("hex");

function validateNames(names) {
  if (
    !Array.isArray(names) ||
    !names.includes(provenanceName) ||
    new Set(names).size !== names.length
  )
    throw new Error("Invalid Media Recover source allowlist.");
  for (const name of names) {
    if (
      typeof name !== "string" ||
      !/^[\w./-]+$/.test(name) ||
      name.startsWith("/") ||
      name.split("/").some((part) => !part || part === "." || part === "..") ||
      /(?:^|\/)(?:\.git|bin|obj|artifacts|node_modules|packages|state|staging|quarantine)(?:\/|$)/i.test(
        name,
      ) ||
      (!/\.(?:cs|xaml|csproj|sln|slnx|props|targets|md|ps1|json|ya?ml|manifest|txt)$/.test(
        name,
      ) &&
        !/(?:^|\/)(?:LICENSE|NOTICE|\.gitignore|\.editorconfig)$/.test(name))
    )
      throw new Error(`Unsafe Media Recover source path: ${name}`);
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

export async function checkSnapshotOutput(target) {
  let current = path.resolve(target);
  while (true) {
    try {
      if ((await lstat(current)).isSymbolicLink())
        throw new Error(`Linked snapshot output is not allowed: ${current}`);
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

export async function createMediaRecoverSnapshot(sourceRoot, names) {
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
    identity: "Moorkyz Media Recover",
    treeSha256: digest(JSON.stringify(hashes)),
    files: hashes,
    note: "Pinned UTF-8 source snapshot, not a release. Refresh only from the independent project using scripts/sync-mediarecover-source.mjs; do not edit snapshot files in the portal.",
  };
  return { files, provenance };
}

export async function verifyMediaRecoverSnapshot(sourceRoot, names) {
  const { provenance } = await createMediaRecoverSnapshot(sourceRoot, names);
  const recorded = JSON.parse(
    await readFile(path.join(sourceRoot, provenanceName), "utf8"),
  );
  if (JSON.stringify(recorded) !== JSON.stringify(provenance))
    throw new Error(
      "Media Recover snapshot provenance mismatch. Refresh the allowlisted snapshot from the independent project.",
    );
  return provenance;
}

async function sync(sourceRoot) {
  if (!sourceRoot)
    throw new Error(
      "Usage: node scripts\\sync-mediarecover-source.mjs <standalone-source-directory>",
    );
  const names = JSON.parse(
    await readFile(path.join(root, "tools", "mediarecover.files.json"), "utf8"),
  );
  const output = path.join(root, "tools", "mediarecover");
  if (path.resolve(sourceRoot).toLowerCase() === output.toLowerCase())
    throw new Error("Refresh from the independent project, not the snapshot.");
  const { files, provenance } = await createMediaRecoverSnapshot(
    sourceRoot,
    names,
  );
  for (const name of names) await checkSnapshotOutput(path.join(output, name));
  for (const { name, bytes } of files) {
    const target = path.join(output, name);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, bytes);
  }
  await writeFile(
    path.join(output, provenanceName),
    `${JSON.stringify(provenance, null, 2)}\n`,
  );
  console.log(
    `Pinned ${repository}: ${files.length} text files, tree SHA-256 ${provenance.treeSha256}. No release or remote operation performed.`,
  );
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  await sync(process.argv[2]);
}
