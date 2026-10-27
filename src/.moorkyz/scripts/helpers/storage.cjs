"use strict";

// Shared local storage policy. Node core only; safe to require from project code.
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const zlib = require("node:zlib");
const { Transform } = require("node:stream");
const { pipeline } = require("node:stream/promises");

const LOG_RE = /^log_\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d{3}\.log$/;
const ARCHIVE_RE = /^log_\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d{3}\.zip$/;
const DEFAULTS = Object.freeze({
  maxLogs: 50,
  minFreeBytes: 1024 ** 3,
  targetFreeBytes: 2 * 1024 ** 3,
  minFreePercent: 5,
  targetFreePercent: 10,
});
const ZIP_LIMIT = 0xffffffff;

function storagePolicy(value = {}) {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("storage must be a JSON object.");
  for (const key of Object.keys(value)) {
    if (!Object.hasOwn(DEFAULTS, key))
      throw new Error(`Unknown storage setting '${key}'.`);
  }
  const settings = { ...DEFAULTS, ...value };
  for (const [key, number] of Object.entries(settings)) {
    if (!Number.isFinite(number) || number < 0)
      throw new Error(`storage.${key} must be a non-negative number.`);
  }
  if (
    !Number.isInteger(settings.maxLogs) ||
    settings.maxLogs < 1 ||
    settings.maxLogs > 50
  )
    throw new Error("storage.maxLogs must be an integer from 1 to 50.");
  if (
    settings.minFreePercent > 100 ||
    settings.targetFreePercent > 100 ||
    settings.targetFreePercent < settings.minFreePercent ||
    settings.targetFreeBytes < settings.minFreeBytes
  )
    throw new Error(
      "Storage target thresholds must be at least the minimum thresholds; percentages cannot exceed 100.",
    );
  return settings;
}

function inside(root, target) {
  const relative = path.relative(root, target);
  return (
    relative !== ".." &&
    !relative.startsWith(`..${path.sep}`) &&
    !path.isAbsolute(relative)
  );
}

/** Reject symlinks/junctions at every path component, including internal links. */
function scopedPath(root, input, allowMissing = false) {
  const base = fs.realpathSync(root);
  const target = path.resolve(base, input);
  if (!inside(base, target))
    throw new Error(`Path escapes project scope: ${input}`);
  let current = base;
  for (const part of path
    .relative(base, target)
    .split(path.sep)
    .filter(Boolean)) {
    current = path.join(current, part);
    let stat;
    try {
      stat = fs.lstatSync(current);
    } catch (error) {
      if (allowMissing && error.code === "ENOENT") continue;
      throw error;
    }
    if (stat.isSymbolicLink())
      throw new Error(
        `Refusing symlink or junction: ${path.relative(base, current)}`,
      );
  }
  return target;
}

function ensureDirectories(
  root,
  names = [".moorkyz", "logs", "archive", "trash"],
) {
  for (const name of names) {
    const folder = scopedPath(root, name, true);
    fs.mkdirSync(folder, { recursive: true });
    if (!fs.lstatSync(folder).isDirectory())
      throw new Error(`${name} must be a directory.`);
  }
}

function processAlive(pid) {
  if (!Number.isSafeInteger(pid) || pid < 1) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === "EPERM";
  }
}

/** All CLI/library writers cooperate through atomic PID leases for their open logs. */
function registerLog(root, filename, pid = process.pid) {
  ensureDirectories(root, [".moorkyz", "logs"]);
  const file = scopedPath(root, filename);
  const logFolder = scopedPath(root, "logs");
  if (
    path.dirname(file) !== logFolder ||
    !validName(path.basename(file), ".log") ||
    !fs.lstatSync(file).isFile()
  )
    throw new Error(
      "Only canonical regular files in the project's logs/ directory can be registered.",
    );
  if (!Number.isSafeInteger(pid) || pid < 1)
    throw new Error("A log lease requires a valid process PID.");
  const directory = scopedPath(root, path.join(".moorkyz", "log-leases"), true);
  fs.mkdirSync(directory, { recursive: true });
  const lease = scopedPath(
    root,
    path.join(directory, `${path.basename(file)}.json`),
    true,
  );
  const token = crypto.randomBytes(16).toString("hex");
  const temporary = scopedPath(root, `${lease}.${token}.tmp`, true);
  try {
    fs.writeFileSync(
      temporary,
      JSON.stringify({ pid, token, log: path.basename(file) }),
      { flag: "wx", mode: 0o600 },
    );
    // Publish without replacing an existing writer's protection. A second
    // registrant must not release the first writer's lease when it closes.
    try {
      fs.linkSync(temporary, lease);
    } catch (error) {
      if (error.code === "EEXIST")
        throw new Error(
          `Log already registered: ${path.relative(root, file)}. Close its writer or run maintenance to clear a dead lease.`,
        );
      throw error;
    }
  } finally {
    if (fs.existsSync(temporary)) fs.unlinkSync(temporary);
  }
  return () => {
    if (!fs.existsSync(lease)) return;
    const current = JSON.parse(
      fs.readFileSync(scopedPath(root, lease), "utf8"),
    );
    if (current.token === token) fs.unlinkSync(lease);
  };
}

function registeredLogPaths(root) {
  const folder = scopedPath(root, path.join(".moorkyz", "log-leases"), true);
  if (!fs.existsSync(folder)) return [];
  const live = [];
  for (const name of fs
    .readdirSync(folder)
    .filter((value) => value.endsWith(".json"))) {
    const file = scopedPath(root, path.join(folder, name));
    const entry = JSON.parse(fs.readFileSync(file, "utf8"));
    if (
      !entry ||
      !validName(entry.log || "", ".log") ||
      name !== `${entry.log}.json` ||
      !Number.isSafeInteger(entry.pid) ||
      entry.pid < 1 ||
      !/^[a-f0-9]{32}$/.test(entry.token || "")
    )
      throw new Error(
        `Invalid active log lease: ${path.relative(root, file)}. Inspect it before running maintenance.`,
      );
    if (processAlive(entry.pid))
      live.push(scopedPath(root, path.join("logs", entry.log), true));
    else fs.unlinkSync(file);
  }
  return live;
}

function archiveLease(root, filename) {
  const directory = scopedPath(
    root,
    path.join(".moorkyz", "archive-leases"),
    true,
  );
  fs.mkdirSync(directory, { recursive: true });
  const file = scopedPath(
    root,
    path.join(directory, `${path.basename(filename)}.json`),
    true,
  );
  const token = crypto.randomBytes(16).toString("hex");
  const temporary = scopedPath(root, `${file}.${token}.tmp`, true);
  try {
    fs.writeFileSync(
      temporary,
      JSON.stringify({
        pid: process.pid,
        token,
        archive: path.basename(filename),
      }),
      { flag: "wx", mode: 0o600 },
    );
    fs.renameSync(temporary, file);
  } finally {
    if (fs.existsSync(temporary)) fs.unlinkSync(temporary);
  }
  return () => {
    if (fs.existsSync(file)) fs.unlinkSync(scopedPath(root, file));
  };
}

function protectedArchives(root) {
  const folder = scopedPath(
    root,
    path.join(".moorkyz", "archive-leases"),
    true,
  );
  if (!fs.existsSync(folder)) return [];
  const live = [];
  for (const name of fs
    .readdirSync(folder)
    .filter((value) => value.endsWith(".json"))) {
    const file = scopedPath(root, path.join(folder, name));
    const entry = JSON.parse(fs.readFileSync(file, "utf8"));
    if (
      !entry ||
      !validName(entry.archive || "", ".zip") ||
      name !== `${entry.archive}.json` ||
      !Number.isSafeInteger(entry.pid) ||
      entry.pid < 1
    )
      throw new Error(
        `Invalid in-progress archive lease: ${path.relative(root, file)}. Inspect it before maintenance.`,
      );
    if (processAlive(entry.pid))
      live.push(path.join(root, "archive", entry.archive));
    else fs.unlinkSync(file);
  }
  return live;
}

function timestamp(date = new Date()) {
  return `log_${date.toISOString().replace("T", "-").replace(/[:.]/g, "-").replace(/Z$/, "")}`;
}

function validName(name, extension) {
  const expression = extension === ".log" ? LOG_RE : ARCHIVE_RE;
  if (!expression.test(name)) return false;
  const pieces = name.slice(4, -extension.length).split("-").map(Number);
  const [year, month, day, hour, minute, second, millisecond] = pieces;
  const date = new Date(
    Date.UTC(year, month - 1, day, hour, minute, second, millisecond),
  );
  return timestamp(date) + extension === name;
}

function reserveName(root, folder, extension) {
  for (let increment = 0; increment < 10000; increment++) {
    const filename = scopedPath(
      root,
      path.join(
        folder,
        timestamp(new Date(Date.now() + increment)) + extension,
      ),
      true,
    );
    try {
      return { filename, fd: fs.openSync(filename, "wx", 0o600) };
    } catch (error) {
      if (error.code !== "EEXIST") throw error;
    }
  }
  throw new Error(
    `Cannot allocate a unique filename in ${folder}. Retry after other writers finish.`,
  );
}

function snapshot(file) {
  const stat = fs.lstatSync(file);
  if (stat.isSymbolicLink() || (!stat.isFile() && !stat.isDirectory()))
    throw new Error(`Only regular files/directories can be archived: ${file}`);
  return stat;
}

function unchanged(before, after) {
  return (
    before.dev === after.dev &&
    before.ino === after.ino &&
    before.size === after.size &&
    before.mtimeMs === after.mtimeMs &&
    before.ctimeMs === after.ctimeMs
  );
}

async function hashFile(file) {
  const hash = crypto.createHash("sha256");
  for await (const chunk of fs.createReadStream(file)) hash.update(chunk);
  return hash.digest("hex");
}

function gather(root, inputs) {
  if (!Array.isArray(inputs) || !inputs.length)
    throw new Error("Choose at least one project-relative path to archive.");
  const base = fs.realpathSync(root);
  const selections = [
    ...new Set(
      inputs.map((input) => {
        if (typeof input !== "string" || !input)
          throw new Error("Archive paths must be non-empty strings.");
        const file = scopedPath(base, input);
        const relative = path.relative(base, file);
        const first = relative.split(path.sep)[0].toLowerCase();
        if (!relative || [".git", ".moorkyz", "archive"].includes(first))
          throw new Error(
            `Cannot archive the project root, Git metadata, runtime state or archives: ${input}`,
          );
        return file;
      }),
    ),
  ].sort((a, b) => a.length - b.length);
  const roots = selections.filter(
    (file, index) =>
      !selections.slice(0, index).some((parent) => inside(parent, file)),
  );
  const entries = [];
  function visit(file) {
    scopedPath(base, file);
    if (
      path
        .relative(base, file)
        .split(path.sep)
        .some((part) => part.toLowerCase() === ".git")
    )
      throw new Error(
        `Cannot archive Git metadata: ${path.relative(base, file)}`,
      );
    const stat = snapshot(file);
    if (stat.isFile() && stat.size >= ZIP_LIMIT)
      throw new Error(
        `File exceeds the 4 GiB classic ZIP limit: ${path.relative(base, file)}. Split this file before archiving.`,
      );
    const directory = stat.isDirectory();
    entries.push({
      file,
      stat,
      directory,
      name:
        path.relative(base, file).split(path.sep).join("/") +
        (directory ? "/" : ""),
    });
    if (entries.length > 65534)
      throw new Error(
        "Archive exceeds 65534 entries; split the operation into smaller batches.",
      );
    if (directory)
      for (const name of fs.readdirSync(file).sort())
        visit(path.join(file, name));
  }
  roots.forEach(visit);
  return entries;
}

function writeAll(fd, bytes) {
  let written = 0;
  while (written < bytes.length)
    written += fs.writeSync(fd, bytes, written, bytes.length - written);
}

function dosTime(date) {
  const year = Math.min(2107, Math.max(1980, date.getUTCFullYear()));
  return {
    time:
      (date.getUTCHours() << 11) |
      (date.getUTCMinutes() << 5) |
      (date.getUTCSeconds() >> 1),
    date:
      ((year - 1980) << 9) |
      ((date.getUTCMonth() + 1) << 5) |
      date.getUTCDate(),
  };
}

/** Nonempty ZIP entries use DEFLATE; UTF-8 paths and classic ZIP limits are enforced. */
async function writeZip(fd, entries) {
  let offset = 0;
  const append = (bytes) => {
    if (offset + bytes.length >= ZIP_LIMIT)
      throw new Error(
        "Archive exceeds the 4 GiB classic ZIP limit; split this batch.",
      );
    writeAll(fd, bytes);
    offset += bytes.length;
  };
  for (const entry of entries) {
    const name = Buffer.from(entry.name, "utf8");
    if (name.length > 65535)
      throw new Error(`Archive entry path is too long: ${entry.name}`);
    entry.offset = offset;
    entry.dos = dosTime(entry.stat.mtime);
    entry.crc = 0;
    entry.size = 0;
    // Empty files and directories have no file data in the ZIP format.
    entry.empty = entry.directory || entry.stat.size === 0;
    entry.method = entry.empty ? 0 : 8;
    entry.flags = entry.empty ? 0x800 : 0x808;
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(entry.flags, 6);
    local.writeUInt16LE(entry.method, 8);
    local.writeUInt16LE(entry.dos.time, 10);
    local.writeUInt16LE(entry.dos.date, 12);
    local.writeUInt16LE(name.length, 26);
    append(local);
    append(name);
    entry.dataOffset = offset;
    const hash = crypto.createHash("sha256");
    const measure = new Transform({
      transform(chunk, encoding, callback) {
        entry.size += chunk.length;
        if (entry.size >= ZIP_LIMIT)
          return callback(new Error(`File exceeds ZIP limit: ${entry.name}`));
        entry.crc = zlib.crc32(chunk, entry.crc);
        hash.update(chunk);
        callback(null, chunk);
      },
    });
    if (!entry.empty)
      await pipeline(
        fs.createReadStream(entry.file),
        measure,
        zlib.createDeflateRaw({ level: 6 }),
        async (chunks) => {
          for await (const chunk of chunks) append(chunk);
        },
      );
    entry.hash = hash.digest("hex");
    entry.compressedSize = offset - entry.dataOffset;
    if (!entry.empty) {
      const descriptor = Buffer.alloc(16);
      descriptor.writeUInt32LE(0x08074b50, 0);
      descriptor.writeUInt32LE(entry.crc, 4);
      descriptor.writeUInt32LE(entry.compressedSize, 8);
      descriptor.writeUInt32LE(entry.size, 12);
      append(descriptor);
    }
  }
  const centralOffset = offset;
  for (const entry of entries) {
    const name = Buffer.from(entry.name, "utf8");
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE((3 << 8) | 20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(entry.flags, 8);
    central.writeUInt16LE(entry.method, 10);
    central.writeUInt16LE(entry.dos.time, 12);
    central.writeUInt16LE(entry.dos.date, 14);
    central.writeUInt32LE(entry.crc, 16);
    central.writeUInt32LE(entry.compressedSize, 20);
    central.writeUInt32LE(entry.size, 24);
    central.writeUInt16LE(name.length, 28);
    central.writeUInt32LE(
      ((entry.stat.mode & 0xffff) * 65536 + (entry.directory ? 0x10 : 0)) >>> 0,
      38,
    );
    central.writeUInt32LE(entry.offset, 42);
    append(central);
    append(name);
  }
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(offset - centralOffset, 12);
  end.writeUInt32LE(centralOffset, 16);
  append(end);
  fs.fsyncSync(fd);
  return {
    centralOffset,
    centralSize: offset - centralOffset - end.length,
    size: offset,
  };
}

function verifyZipRecords(filename, entries, layout) {
  const fd = fs.openSync(filename, "r");
  const invalid = () => {
    throw new Error(
      "Archive record verification failed; source files were preserved.",
    );
  };
  const read = (offset, length) => {
    const bytes = Buffer.alloc(length);
    if (fs.readSync(fd, bytes, 0, length, offset) !== length) invalid();
    return bytes;
  };
  try {
    if (fs.fstatSync(fd).size !== layout.size) invalid();
    let centralOffset = layout.centralOffset;
    for (const entry of entries) {
      const expectedName = Buffer.from(entry.name, "utf8");
      const local = read(entry.offset, 30);
      if (
        local.readUInt32LE(0) !== 0x04034b50 ||
        local.readUInt16LE(4) !== 20 ||
        local.readUInt16LE(6) !== entry.flags ||
        local.readUInt16LE(8) !== entry.method ||
        local.readUInt16LE(26) !== expectedName.length ||
        local.readUInt16LE(28) !== 0 ||
        !read(entry.offset + 30, expectedName.length).equals(expectedName)
      )
        invalid();
      if (!entry.empty) {
        const descriptor = read(entry.dataOffset + entry.compressedSize, 16);
        if (
          descriptor.readUInt32LE(0) !== 0x08074b50 ||
          descriptor.readUInt32LE(4) !== entry.crc ||
          descriptor.readUInt32LE(8) !== entry.compressedSize ||
          descriptor.readUInt32LE(12) !== entry.size
        )
          invalid();
      }
      const central = read(centralOffset, 46);
      if (
        central.readUInt32LE(0) !== 0x02014b50 ||
        central.readUInt16LE(6) !== 20 ||
        central.readUInt16LE(8) !== entry.flags ||
        central.readUInt16LE(10) !== entry.method ||
        central.readUInt32LE(16) !== entry.crc ||
        central.readUInt32LE(20) !== entry.compressedSize ||
        central.readUInt32LE(24) !== entry.size ||
        central.readUInt16LE(28) !== expectedName.length ||
        central.readUInt16LE(30) !== 0 ||
        central.readUInt16LE(32) !== 0 ||
        central.readUInt16LE(34) !== 0 ||
        central.readUInt32LE(42) !== entry.offset ||
        !read(centralOffset + 46, expectedName.length).equals(expectedName)
      )
        invalid();
      centralOffset += 46 + expectedName.length;
    }
    const end = read(centralOffset, 22);
    if (
      centralOffset !== layout.centralOffset + layout.centralSize ||
      end.readUInt32LE(0) !== 0x06054b50 ||
      end.readUInt16LE(4) !== 0 ||
      end.readUInt16LE(6) !== 0 ||
      end.readUInt16LE(8) !== entries.length ||
      end.readUInt16LE(10) !== entries.length ||
      end.readUInt32LE(12) !== layout.centralSize ||
      end.readUInt32LE(16) !== layout.centralOffset ||
      end.readUInt16LE(20) !== 0
    )
      invalid();
  } finally {
    fs.closeSync(fd);
  }
}

async function verifyZip(filename, entries, layout) {
  // Payload integrity alone does not prove that ZIP readers can locate it.
  verifyZipRecords(filename, entries, layout);
  for (const entry of entries) {
    if (entry.empty) continue;
    let crc = 0;
    let size = 0;
    const hash = crypto.createHash("sha256");
    await pipeline(
      fs.createReadStream(filename, {
        start: entry.dataOffset,
        end: entry.dataOffset + entry.compressedSize - 1,
      }),
      zlib.createInflateRaw(),
      async (chunks) => {
        for await (const chunk of chunks) {
          crc = zlib.crc32(chunk, crc);
          size += chunk.length;
          hash.update(chunk);
        }
      },
    );
    if (
      crc !== entry.crc ||
      size !== entry.size ||
      hash.digest("hex") !== entry.hash
    )
      throw new Error(
        `Archive verification failed for ${entry.name}; source files were preserved.`,
      );
  }
}

function freeSpace(root) {
  const stat = fs.statfsSync(root, { bigint: true });
  return {
    available: Number(stat.bavail * stat.bsize),
    total: Number(stat.blocks * stat.bsize),
  };
}

/** Only canonical regular ZIPs in this project's archive are eligible for pruning. */
function pruneArchives(
  root,
  policy = DEFAULTS,
  protectedPaths = [],
  measure = freeSpace,
) {
  const settings = storagePolicy(policy);
  const folder = scopedPath(root, "archive");
  const protectedFiles = new Set(
    [...protectedPaths, ...protectedArchives(root)].map((file) =>
      path.resolve(file),
    ),
  );
  const entries = fs
    .readdirSync(folder)
    .filter((name) => validName(name, ".zip"))
    .map((name) => path.join(folder, name))
    .filter((file) => !protectedFiles.has(file) && fs.lstatSync(file).isFile())
    .sort((a, b) => path.basename(a).localeCompare(path.basename(b)));
  const space = measure(folder);
  const minimum = Math.max(
    settings.minFreeBytes,
    (space.total * settings.minFreePercent) / 100,
  );
  const target = Math.max(
    settings.targetFreeBytes,
    (space.total * settings.targetFreePercent) / 100,
  );
  const removed = [];
  let available = space.available;
  if (available < minimum) {
    for (const file of entries) {
      if (available >= target) break;
      scopedPath(root, file);
      // Do not discard a user file that merely has a canonical .zip filename.
      const fd = fs.openSync(file, "r");
      const magic = Buffer.alloc(4);
      try {
        fs.readSync(fd, magic, 0, 4, 0);
      } finally {
        fs.closeSync(fd);
      }
      if (magic.readUInt32LE() !== 0x04034b50) continue;
      if (
        protectedArchives(root).some(
          (active) => path.resolve(active) === path.resolve(file),
        )
      )
        continue;
      fs.unlinkSync(file);
      removed.push(path.relative(root, file));
      available = measure(folder).available;
    }
  }
  return { removed, available, target, lowSpace: available < minimum };
}

/** Compress all selected paths in one verified ZIP, then remove unchanged sources. */
async function archivePaths(root, inputs, options = {}) {
  ensureDirectories(root);
  const settings = storagePolicy(options.policy);
  const entries = gather(root, inputs);
  const protectedFiles = new Set(
    [...(options.protectedPaths || []), ...registeredLogPaths(root)].map(
      (file) => path.resolve(file),
    ),
  );
  if (entries.some((entry) => protectedFiles.has(entry.file)))
    throw new Error(
      "Cannot archive an active service log. Stop its service first.",
    );
  const pruned = pruneArchives(root, settings, options.protectedPaths);
  // Reserve the final filename exclusively; publish compressed content only after verification.
  const reserved = reserveName(root, "archive", ".zip");
  const temporary = scopedPath(
    root,
    `${reserved.filename}.${process.pid}.tmp`,
    true,
  );
  fs.closeSync(reserved.fd);
  let releaseLease;
  let fd;
  let published = false;
  try {
    // A concurrent maintainer must not prune this ZIP before source removal finishes.
    releaseLease = archiveLease(root, reserved.filename);
    fd = fs.openSync(temporary, "wx", 0o600);
    const layout = await writeZip(fd, entries);
    fs.closeSync(fd);
    fd = undefined;
    await verifyZip(temporary, entries, layout);
    for (const entry of entries) {
      scopedPath(root, entry.file);
      if (
        !unchanged(entry.stat, snapshot(entry.file)) ||
        (!entry.directory && (await hashFile(entry.file)) !== entry.hash)
      )
        throw new Error(
          `Source changed while archiving: ${entry.name}. No source files were removed.`,
        );
    }
    fs.renameSync(temporary, reserved.filename);
    published = true;
    // Recheck each file immediately before unlinking. Never recursively delete a directory.
    if (options.remove !== false) {
      for (const entry of entries.filter((item) => !item.directory)) {
        scopedPath(root, entry.file);
        if (
          !unchanged(entry.stat, snapshot(entry.file)) ||
          (await hashFile(entry.file)) !== entry.hash
        )
          throw new Error(
            `Source changed before deletion: ${entry.name}. Verified ZIP retained at ${reserved.filename}.`,
          );
        fs.unlinkSync(entry.file);
      }
      for (const entry of entries.filter((item) => item.directory).reverse()) {
        scopedPath(root, entry.file);
        fs.rmdirSync(entry.file);
      }
    }
    return {
      filename: reserved.filename,
      entries: entries.length,
      pruned: pruned.removed,
    };
  } finally {
    if (fd !== undefined) fs.closeSync(fd);
    if (fs.existsSync(temporary)) fs.unlinkSync(temporary);
    if (!published && fs.existsSync(reserved.filename))
      fs.unlinkSync(reserved.filename);
    if (releaseLease) releaseLease();
  }
}

/** Invoke at start and periodically; a filesystem folder itself cannot reject writes. */
async function maintainStorage(root, options = {}) {
  ensureDirectories(root);
  const policy = storagePolicy(options.policy);
  const reserveLogs = options.reserveLogs ?? 0;
  if (
    !Number.isInteger(reserveLogs) ||
    reserveLogs < 0 ||
    reserveLogs > policy.maxLogs
  )
    throw new Error(
      "reserveLogs must be an integer between zero and the log cap.",
    );
  const protectedFiles = new Set(
    [...(options.protectedPaths || []), ...registeredLogPaths(root)].map(
      (file) => path.resolve(file),
    ),
  );
  const logs = scopedPath(root, "logs");
  const files = fs
    .readdirSync(logs)
    .map((name) => ({ name, file: path.join(logs, name) }));
  const valid = files
    .filter(
      ({ name, file }) =>
        validName(name, ".log") && fs.lstatSync(file).isFile(),
    )
    .sort((a, b) => a.name.localeCompare(b.name));
  const discard = files.filter(
    ({ name, file }) =>
      !validName(name, ".log") || !fs.lstatSync(file).isFile(),
  );
  let excess = Math.max(0, valid.length - (policy.maxLogs - reserveLogs));
  for (const entry of valid) {
    if (excess && !protectedFiles.has(entry.file)) {
      discard.push(entry);
      excess--;
    }
  }
  if (excess)
    throw new Error(
      "All excess logs are active; stop a service before exceeding the log limit.",
    );
  const selected = discard.map(({ file }) => file);
  if (options.includeTrash) {
    const trash = scopedPath(root, "trash");
    selected.push(
      ...fs.readdirSync(trash).map((name) => path.join(trash, name)),
    );
  }
  // Everything selected by one maintenance operation shares one verified ZIP.
  const archived = selected.length
    ? [await archivePaths(root, selected, { ...options, policy })]
    : [];
  const pruned = pruneArchives(
    root,
    policy,
    archived.map(({ filename }) => filename),
  );
  return { archived, pruned };
}

function createLog(root) {
  ensureDirectories(root, ["logs"]);
  const reserved = reserveName(root, "logs", ".log");
  fs.closeSync(reserved.fd);
  return reserved.filename;
}

module.exports = {
  DEFAULTS,
  LOG_RE,
  ARCHIVE_RE,
  storagePolicy,
  scopedPath,
  ensureDirectories,
  timestamp,
  validName,
  createLog,
  registerLog,
  registeredLogPaths,
  archivePaths,
  maintainStorage,
  pruneArchives,
};
