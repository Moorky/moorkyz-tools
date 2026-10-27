"use strict";

const { test, afterEach } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const zlib = require("node:zlib");
const { spawnSync } = require("node:child_process");
const storage = require("../../.moorkyz/launchers/moorkyz-storage.cjs");
const fixtures = [];
const policy = {
  ...storage.DEFAULTS,
  minFreeBytes: 0,
  targetFreeBytes: 0,
  minFreePercent: 0,
  targetFreePercent: 0,
};

function fixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-runtime-"));
  fixtures.push(root);
  storage.ensureDirectories(root);
  return root;
}

afterEach(() => {
  for (const root of fixtures.splice(0)) {
    assert.ok(
      path.resolve(root).startsWith(path.join(os.tmpdir(), "moorkyz-runtime-")),
    );
    fs.rmSync(root, { force: true, recursive: true });
  }
});

// Read central-directory records independently, then use Node's ZIP DEFLATE decoder.
function unzip(filename) {
  const bytes = fs.readFileSync(filename);
  const end = bytes.length - 22;
  assert.equal(bytes.readUInt32LE(end), 0x06054b50);
  const count = bytes.readUInt16LE(end + 10);
  let offset = bytes.readUInt32LE(end + 16);
  const files = new Map();
  for (let index = 0; index < count; index++) {
    assert.equal(bytes.readUInt32LE(offset), 0x02014b50);
    const compressed = bytes.readUInt32LE(offset + 20);
    const length = bytes.readUInt16LE(offset + 28);
    const extraLength = bytes.readUInt16LE(offset + 30);
    const commentLength = bytes.readUInt16LE(offset + 32);
    const local = bytes.readUInt32LE(offset + 42);
    const name = bytes.toString("utf8", offset + 46, offset + 46 + length);
    const start =
      local +
      30 +
      bytes.readUInt16LE(local + 26) +
      bytes.readUInt16LE(local + 28);
    const method = bytes.readUInt16LE(offset + 10);
    assert.ok(method === 0 || method === 8);
    if (bytes.readUInt32LE(offset + 24) === 0) {
      assert.equal(
        method,
        0,
        "Empty entries use stored mode without file data",
      );
      assert.equal(compressed, 0);
    }
    const data = bytes.subarray(start, start + compressed);
    const contents = method === 0 ? data : zlib.inflateRawSync(data);
    assert.equal(zlib.crc32(contents), bytes.readUInt32LE(offset + 16));
    assert.equal(contents.length, bytes.readUInt32LE(offset + 24));
    files.set(name, contents);
    offset += 46 + length + extraLength + commentLength;
  }
  return files;
}

test("uses UTC filenames with millisecond precision and validates calendar dates", () => {
  assert.equal(
    storage.timestamp(new Date("2026-10-23T12:34:56.789Z")),
    "log_2026-10-23-12-34-56-789",
  );
  assert.ok(storage.validName("log_2024-02-29-12-00-00-000.log", ".log"));
  for (const name of [
    "log_2025-02-29-12-00-00-000.log",
    "log_2026-13-01-12-00-00-000.log",
    "debug.log",
    "log_2026-10-23-24-00-00-000.log",
  ])
    assert.equal(storage.validName(name, ".log"), false);
});

test("creates collision-free canonical logs with restrictive permissions", () => {
  const root = fixture();
  const logs = Array.from({ length: 20 }, () => storage.createLog(root));
  assert.equal(new Set(logs).size, 20);
  assert.ok(
    logs.every((file) => storage.validName(path.basename(file), ".log")),
  );
});

test("compresses a single source and preserves it when remove is false", async () => {
  const root = fixture();
  fs.writeFileSync(
    path.join(root, "one.txt"),
    "repeated content ".repeat(1000),
  );
  const result = await storage.archivePaths(root, ["one.txt"], {
    policy,
    remove: false,
  });
  assert.ok(storage.validName(path.basename(result.filename), ".zip"));
  assert.equal(
    unzip(result.filename).get("one.txt").toString(),
    "repeated content ".repeat(1000),
  );
  assert.ok(fs.statSync(result.filename).size < 16000);
  assert.ok(fs.existsSync(path.join(root, "one.txt")));
});

test("archives one batch including binary bytes, Unicode paths and empty directories", async () => {
  const root = fixture();
  fs.mkdirSync(path.join(root, "folder", "empty"), { recursive: true });
  const binary = Buffer.from(Array.from({ length: 256 }, (_, index) => index));
  fs.writeFileSync(path.join(root, "folder", "ä.bin"), binary);
  fs.writeFileSync(path.join(root, "folder", "empty.txt"), "");
  fs.writeFileSync(path.join(root, "two.txt"), "second file");
  const result = await storage.archivePaths(
    root,
    ["folder", "folder/ä.bin", "two.txt"],
    { policy },
  );
  const files = unzip(result.filename);
  assert.deepEqual(files.get("folder/ä.bin"), binary);
  assert.equal(files.get("two.txt").toString(), "second file");
  assert.ok(files.has("folder/empty/"));
  assert.equal(files.get("folder/empty.txt").length, 0);
  assert.equal(fs.readdirSync(path.join(root, "archive")).length, 1);
  assert.equal(fs.existsSync(path.join(root, "folder")), false);
  assert.equal(fs.existsSync(path.join(root, "two.txt")), false);
});

test("refuses path escapes and protected root metadata without modifying sources", async () => {
  const root = fixture();
  const outside = fixture();
  fs.writeFileSync(path.join(outside, "keep.txt"), "keep");
  for (const input of [outside, "../outside", ".", ".moorkyz", "archive"]) {
    await assert.rejects(
      storage.archivePaths(root, [input], { policy }),
      /scope|Cannot archive/,
    );
  }
  assert.equal(fs.readFileSync(path.join(outside, "keep.txt"), "utf8"), "keep");
  assert.deepEqual(fs.readdirSync(path.join(root, "archive")), []);
});

test("preserves changed sources and removes incomplete output before deleting any input", async () => {
  const root = fixture();
  const source = path.join(root, "changing.txt");
  fs.writeFileSync(source, "original");
  const original = fs.createReadStream;
  fs.createReadStream = function (file, ...args) {
    const stream = original.call(this, file, ...args);
    if (file === source)
      stream.once("end", () => fs.appendFileSync(source, " updated"));
    return stream;
  };
  try {
    await assert.rejects(
      storage.archivePaths(root, [source], { policy }),
      /Source changed/,
    );
  } finally {
    fs.createReadStream = original;
  }
  assert.equal(fs.readFileSync(source, "utf8"), "original updated");
  assert.deepEqual(fs.readdirSync(path.join(root, "archive")), []);
});

test("preserves all original bytes when archive writing runs out of space", async () => {
  const root = fixture();
  const source = path.join(root, "keep.txt");
  fs.writeFileSync(source, "keep all original bytes");
  const original = fs.writeSync;
  fs.writeSync = () => {
    throw Object.assign(new Error("Simulated ENOSPC"), { code: "ENOSPC" });
  };
  try {
    await assert.rejects(
      storage.archivePaths(root, [source], { policy }),
      /ENOSPC/,
    );
  } finally {
    fs.writeSync = original;
  }
  assert.equal(fs.readFileSync(source, "utf8"), "keep all original bytes");
  assert.deepEqual(fs.readdirSync(path.join(root, "archive")), []);
});

for (const record of [
  "local header",
  "descriptor",
  "central directory",
  "end record",
]) {
  test(`preserves sources when the ZIP ${record} is corrupted before verification`, async () => {
    const root = fixture();
    const source = path.join(root, "keep.txt");
    fs.writeFileSync(source, "recovery bytes must remain readable");
    const original = fs.fsyncSync;
    fs.fsyncSync = (fd) => {
      original(fd);
      const temporary = fs
        .readdirSync(path.join(root, "archive"))
        .find((name) => name.endsWith(".tmp"));
      const bytes = fs.readFileSync(path.join(root, "archive", temporary));
      const signatures = {
        "local header": Buffer.from([0x50, 0x4b, 0x03, 0x04]),
        descriptor: Buffer.from([0x50, 0x4b, 0x07, 0x08]),
        "central directory": Buffer.from([0x50, 0x4b, 0x01, 0x02]),
        "end record": Buffer.from([0x50, 0x4b, 0x05, 0x06]),
      };
      const offset = bytes.indexOf(signatures[record]);
      assert.ok(offset >= 0);
      fs.writeSync(fd, Buffer.from([0]), 0, 1, offset);
    };
    try {
      await assert.rejects(
        storage.archivePaths(root, [source], { policy }),
        /record verification failed/,
      );
    } finally {
      fs.fsyncSync = original;
    }
    assert.equal(
      fs.readFileSync(source, "utf8"),
      "recovery bytes must remain readable",
    );
    assert.deepEqual(fs.readdirSync(path.join(root, "archive")), []);
  });
}

test("refuses internal and external junctions before removing any batch member", async () => {
  const root = fixture();
  const outside = fixture();
  fs.mkdirSync(path.join(root, "regular"));
  fs.writeFileSync(path.join(root, "keep.txt"), "keep");
  fs.symlinkSync(outside, path.join(root, "external"), "junction");
  fs.symlinkSync(
    path.join(root, "regular"),
    path.join(root, "internal"),
    "junction",
  );
  for (const input of ["external", "internal"]) {
    await assert.rejects(
      storage.archivePaths(root, ["keep.txt", input], { policy }),
      /symlink|junction/,
    );
  }
  assert.equal(fs.readFileSync(path.join(root, "keep.txt"), "utf8"), "keep");
});

test("rejects an archive-directory junction before writing outside the project", async () => {
  const root = fixture();
  const outside = fixture();
  fs.rmdirSync(path.join(root, "archive"));
  fs.symlinkSync(outside, path.join(root, "archive"), "junction");
  fs.writeFileSync(path.join(root, "keep.txt"), "keep");
  await assert.rejects(
    storage.archivePaths(root, ["keep.txt"], { policy }),
    /symlink|junction/,
  );
  assert.equal(fs.readFileSync(path.join(root, "keep.txt"), "utf8"), "keep");
  assert.deepEqual(fs.readdirSync(outside).sort(), [
    ".moorkyz",
    "archive",
    "logs",
    "trash",
  ]);
});

test("keeps the newest 50 logs and groups malformed/expired logs in one ZIP", async () => {
  const root = fixture();
  for (let index = 0; index < 53; index++) {
    fs.writeFileSync(
      path.join(
        root,
        "logs",
        `${storage.timestamp(new Date(Date.UTC(2026, 9, 23, 0, 0, index)))}.log`,
      ),
      String(index),
    );
  }
  fs.writeFileSync(path.join(root, "logs", "wrong.txt"), "wrong");
  fs.writeFileSync(
    path.join(root, "logs", "log_2026-02-30-00-00-00-000.log"),
    "invalid date",
  );
  const result = await storage.maintainStorage(root, { policy });
  assert.equal(result.archived.length, 1);
  assert.equal(fs.readdirSync(path.join(root, "logs")).length, 50);
  const files = unzip(result.archived[0].filename);
  assert.equal(files.size, 5);
  assert.equal(files.get("logs/wrong.txt").toString(), "wrong");
  assert.equal(
    files.get("logs/log_2026-10-23-00-00-00-000.log").toString(),
    "0",
  );
});

test("protects active logs from archival and rotation", async () => {
  const root = fixture();
  const active = path.join(root, "logs", "log_2026-01-01-00-00-00-000.log");
  const old = path.join(root, "logs", "log_2026-02-01-00-00-00-000.log");
  fs.writeFileSync(active, "active");
  fs.writeFileSync(old, "old");
  const options = {
    policy: { ...policy, maxLogs: 1 },
    protectedPaths: [active],
  };
  await assert.rejects(
    storage.archivePaths(root, [active], options),
    /active service log/,
  );
  await storage.maintainStorage(root, options);
  assert.equal(fs.readFileSync(active, "utf8"), "active");
  assert.equal(fs.existsSync(old), false);
});

test("reserves capacity for the next log even when external writers exceeded the cap", async () => {
  const root = fixture();
  for (let index = 0; index < 55; index++) {
    fs.writeFileSync(
      path.join(
        root,
        "logs",
        `${storage.timestamp(new Date(Date.UTC(2026, 9, 23, 0, 0, index)))}.log`,
      ),
      String(index),
    );
  }
  const result = await storage.maintainStorage(root, {
    policy,
    reserveLogs: 1,
  });
  assert.equal(fs.readdirSync(path.join(root, "logs")).length, 49);
  storage.createLog(root);
  assert.equal(fs.readdirSync(path.join(root, "logs")).length, 50);
  assert.equal(result.archived.length, 1);
  assert.equal(unzip(result.archived[0].filename).size, 6);
});

test("archives all staged trash as one batch", async () => {
  const root = fixture();
  fs.writeFileSync(path.join(root, "trash", "first"), "1");
  fs.writeFileSync(path.join(root, "trash", "second"), "2");
  const result = await storage.maintainStorage(root, {
    policy,
    includeTrash: true,
  });
  assert.equal(result.archived.length, 1);
  assert.equal(unzip(result.archived[0].filename).size, 2);
  assert.deepEqual(fs.readdirSync(path.join(root, "trash")), []);
});

test("archives malformed logs and staged trash from one maintenance call in one ZIP", async () => {
  const root = fixture();
  fs.writeFileSync(path.join(root, "logs", "wrong.log"), "log");
  fs.writeFileSync(path.join(root, "trash", "retired.txt"), "retired");
  const result = await storage.maintainStorage(root, {
    policy,
    includeTrash: true,
  });
  assert.equal(result.archived.length, 1);
  const files = unzip(result.archived[0].filename);
  assert.equal(files.get("logs/wrong.log").toString(), "log");
  assert.equal(files.get("trash/retired.txt").toString(), "retired");
  assert.deepEqual(fs.readdirSync(path.join(root, "logs")), []);
  assert.deepEqual(fs.readdirSync(path.join(root, "trash")), []);
});

test("a duplicate log registration cannot replace a live writer's protection", async () => {
  const root = fixture();
  const active = storage.createLog(root);
  const release = storage.registerLog(root, active);
  assert.throws(
    () => storage.registerLog(root, active),
    /Log already registered/,
  );
  assert.deepEqual(storage.registeredLogPaths(root), [active]);
  await assert.rejects(
    storage.archivePaths(root, [active], { policy }),
    /active service log/,
  );
  release();
  assert.deepEqual(storage.registeredLogPaths(root), []);
  const releaseAgain = storage.registerLog(root, active);
  releaseAgain();
});

test("all storage callers protect registered live logs and release dead leases", async () => {
  const root = fixture();
  const active = path.join(root, "logs", "log_2026-01-01-00-00-00-000.log");
  const inactive = path.join(root, "logs", "log_2026-02-01-00-00-00-000.log");
  fs.writeFileSync(active, "active");
  fs.writeFileSync(inactive, "inactive");
  const release = storage.registerLog(root, active);
  assert.deepEqual(storage.registeredLogPaths(root), [active]);
  await assert.rejects(
    storage.archivePaths(root, [active], { policy }),
    /active service log/,
  );
  await storage.maintainStorage(root, { policy: { ...policy, maxLogs: 1 } });
  assert.equal(fs.readFileSync(active, "utf8"), "active");
  assert.equal(fs.existsSync(inactive), false);
  release();
  assert.deepEqual(storage.registeredLogPaths(root), []);
  storage.registerLog(root, active, 2147483647);
  assert.deepEqual(storage.registeredLogPaths(root), []);
  assert.deepEqual(
    fs.readdirSync(path.join(root, ".moorkyz", "log-leases")),
    [],
  );
});

test("prunes oldest canonical ZIPs until the target and protects unrelated files/new archives", async () => {
  const root = fixture();
  const archives = [];
  for (let index = 1; index <= 3; index++) {
    const filename = path.join(
      root,
      "archive",
      `log_2026-01-0${index}-00-00-00-000.zip`,
    );
    fs.writeFileSync(filename, Buffer.from([0x50, 0x4b, 0x03, 0x04]));
    archives.push(filename);
  }
  fs.writeFileSync(path.join(root, "archive", "keep.zip"), "unrelated");
  fs.writeFileSync(
    path.join(root, "archive", "log_2025-01-01-00-00-00-000.zip"),
    "not a ZIP",
  );
  let calls = 0;
  const result = storage.pruneArchives(
    root,
    { ...policy, minFreeBytes: 100, targetFreeBytes: 200 },
    [archives[2]],
    () => ({ available: calls++ * 100, total: 1000 }),
  );
  assert.deepEqual(
    result.removed,
    archives.slice(0, 2).map((file) => path.relative(root, file)),
  );
  assert.ok(fs.existsSync(archives[2]));
  assert.ok(fs.existsSync(path.join(root, "archive", "keep.zip")));
  assert.ok(
    fs.existsSync(
      path.join(root, "archive", "log_2025-01-01-00-00-00-000.zip"),
    ),
  );
  assert.equal(result.available, 200);
});

test("validates retention settings", () => {
  for (const settings of [
    { maxLogs: 51 },
    { maxLogs: 0 },
    { maxLogs: 1.5 },
    { targetFreePercent: 101 },
    { minFreeBytes: 2, targetFreeBytes: 1 },
    { unexpected: 10 },
  ])
    assert.throws(() => storage.storagePolicy(settings));
});

test("concurrent retention leaves in-progress published archives intact", () => {
  const root = fixture();
  const name = "log_2026-01-01-00-00-00-000.zip";
  const archive = path.join(root, "archive", name);
  fs.writeFileSync(archive, Buffer.from([0x50, 0x4b, 0x03, 0x04]));
  const leases = path.join(root, ".moorkyz", "archive-leases");
  fs.mkdirSync(leases);
  fs.writeFileSync(
    path.join(leases, `${name}.json`),
    JSON.stringify({ pid: process.pid, token: "a".repeat(32), archive: name }),
  );
  const result = storage.pruneArchives(
    root,
    { ...policy, minFreeBytes: 100, targetFreeBytes: 200 },
    [],
    () => ({ available: 0, total: 1000 }),
  );
  assert.deepEqual(result.removed, []);
  assert.ok(fs.existsSync(archive));
});

test("CLI exposes archive and maintenance commands without requiring pnpm", () => {
  const root = fixture();
  fs.writeFileSync(path.join(root, "one.txt"), "one");
  fs.writeFileSync(path.join(root, "two.txt"), "two");
  const cli = path.resolve(__dirname, "../../../moorkyz.cjs");
  const archived = spawnSync(
    process.execPath,
    [cli, "archive", "one.txt", "two.txt"],
    { cwd: root, encoding: "utf8" },
  );
  assert.equal(archived.status, 0, archived.stderr);
  assert.match(archived.stdout, /Archived 2 entries/);
  const maintained = spawnSync(process.execPath, [cli, "maintain"], {
    cwd: root,
    encoding: "utf8",
  });
  assert.equal(maintained.status, 0, maintained.stderr);
  assert.match(maintained.stdout, /Storage maintained/);
});
