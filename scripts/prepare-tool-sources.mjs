import { writeFile, mkdir, lstat, readdir, unlink } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { verifyMediaRecoverSnapshot } from "./sync-mediarecover-source.mjs";
import { verifyVisorySnapshot } from "./sync-visory-source.mjs";
import { assertNoLinks, indexTools, readSourceFiles } from "./index-tools.mjs";

const root = fileURLToPath(new URL("..", import.meta.url));

function crc32(bytes) {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++)
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return (crc ^ 0xffffffff) >>> 0;
}

// Stored ZIP entries keep original bytes and require no runtime compression dependency.
export function createSourceZip(files) {
  const local = [];
  const central = [];
  let offset = 0;
  if (files.length > 65535)
    throw new Error("Source bundle exceeds the ZIP entry limit.");
  for (const { name, bytes } of files) {
    if (
      !/^[\w./-]+$/.test(name) ||
      name.startsWith("/") ||
      name.split("/").some((part) => part === ".." || part === "." || !part)
    )
      throw new Error(`Unsafe source path: ${name}`);
    const filename = Buffer.from(name);
    if (filename.length > 65535 || bytes.length > 0xffffffff)
      throw new Error("Source entry exceeds the ZIP size limit.");
    const header = Buffer.alloc(30);
    header.writeUInt32LE(0x04034b50);
    header.writeUInt16LE(20, 4);
    header.writeUInt16LE(0x800, 6);
    header.writeUInt16LE(33, 12);
    header.writeUInt32LE(crc32(bytes), 14);
    header.writeUInt32LE(bytes.length, 18);
    header.writeUInt32LE(bytes.length, 22);
    header.writeUInt16LE(filename.length, 26);
    local.push(header, filename, bytes);
    const entry = Buffer.alloc(46);
    entry.writeUInt32LE(0x02014b50);
    entry.writeUInt16LE(20, 4);
    header.copy(entry, 6, 4, 30);
    entry.writeUInt32LE(offset, 42);
    central.push(entry, filename);
    offset += header.length + filename.length + bytes.length;
  }
  const directory = Buffer.concat(central);
  if (offset + directory.length > 0xffffffff)
    throw new Error("Source bundle requires ZIP64, which is not supported.");
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(directory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...local, directory, end]);
}

export async function prepareToolSources({
  toolsRoot,
  indexOutput,
  output = path.join(root, "packages", "portal", "public", "tool-sources"),
} = {}) {
  const { projects, unregistered } = await indexTools({
    toolsRoot,
    output: indexOutput,
  });
  for (const folder of unregistered)
    console.warn(
      `Not indexed: tools/${folder}. Add portal.json to register it.`,
    );
  await assertNoLinks(output);
  await mkdir(output, { recursive: true });
  if ((await lstat(output)).isSymbolicLink())
    throw new Error(`Linked source output is not allowed: ${output}`);
  const prepared = [];
  for (const { folder: bundle, sourceRoot, tool } of projects) {
    if (!tool.source) continue;
    const names = tool.source.files;
    if (bundle === "mediarecover")
      await verifyMediaRecoverSnapshot(sourceRoot, names);
    if (bundle === "visory-media-downloader")
      await verifyVisorySnapshot(sourceRoot, names);
    const files = await readSourceFiles(sourceRoot);
    const zip = createSourceZip(files);
    prepared.push({ bundle, files, zip });
  }
  // Validate every project before replacing any generated download.
  for (const { bundle } of prepared) {
    for (const extension of ["json", "zip"]) {
      const target = path.join(output, `${bundle}.${extension}`);
      try {
        if ((await lstat(target)).isSymbolicLink())
          throw new Error(`Linked source output is not allowed: ${target}`);
      } catch (error) {
        if (error.code !== "ENOENT") throw error;
      }
    }
  }
  for (const { bundle, files, zip } of prepared) {
    await writeFile(
      path.join(output, `${bundle}.json`),
      JSON.stringify(
        Object.fromEntries(
          files.map(({ name, bytes }) => [name, bytes.toString("utf8")]),
        ),
      ),
    );
    await writeFile(path.join(output, `${bundle}.zip`), zip);
    console.log(
      `Prepared ${bundle}: ${files.length} source files, ${zip.length} ZIP bytes.`,
    );
  }
  const bundles = new Set(prepared.map((entry) => entry.bundle));
  for (const entry of await readdir(output, { withFileTypes: true })) {
    const match = /^([a-z][a-z0-9-]*)\.(json|zip)$/.exec(entry.name);
    if (match && !bundles.has(match[1])) {
      if (!entry.isFile())
        throw new Error(`Unexpected generated source entry: ${entry.name}`);
      await unlink(path.join(output, entry.name));
      console.log(`Removed retired generated source: ${entry.name}`);
    }
  }
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  await prepareToolSources();
}
