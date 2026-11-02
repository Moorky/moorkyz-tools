"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawnSync } = require("node:child_process");
const { test } = require("node:test");
const ts = require("typescript");
const storage = require("../../.moorkyz/scripts/helpers/storage.cjs");

const root = path.resolve(__dirname, "../../..");
function fixture(t) {
  const folder = fs.mkdtempSync(
    path.join(os.tmpdir(), "moorkyz-manual-storage-"),
  );
  t.after(() => fs.rmSync(folder, { recursive: true, force: true }));
  return folder;
}

test("ordinary log allocation and protection do not create archive or trash", (t) => {
  const folder = fixture(t);
  const log = storage.createLog(folder);
  const release = storage.registerLog(folder, log);
  try {
    assert.ok(fs.existsSync(log));
    assert.equal(storage.registeredLogPaths(folder).length, 1);
    for (const name of ["archive", "trash", "Input", "Output"])
      assert.equal(fs.existsSync(path.join(folder, name)), false, name);
  } finally {
    release();
  }
});

test("log creation never rotates, archives or prunes existing files", (t) => {
  const folder = fixture(t);
  for (const name of ["archive", "trash", "Input", "Output"])
    fs.mkdirSync(path.join(folder, name));
  const sentinels = [
    "archive/preserved.zip",
    "trash/preserved.txt",
    "Input/video.mp4",
    "Output/result.txt",
  ];
  for (const name of sentinels) fs.writeFileSync(path.join(folder, name), name);
  for (let index = 0; index < 52; index++) storage.createLog(folder);
  assert.equal(fs.readdirSync(path.join(folder, "logs")).length, 52);
  for (const name of sentinels)
    assert.equal(fs.readFileSync(path.join(folder, name), "utf8"), name);
});

test("service startup and worker code contain no implicit filesystem maintenance calls", () => {
  const text = fs.readFileSync(
    path.join(root, "src/.moorkyz/scripts/moorkyz.cjs"),
    "utf8",
  );
  const source = ts.createSourceFile(
    "moorkyz.cjs",
    text,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.JS,
  );
  const checked = [];
  for (const statement of source.statements) {
    if (
      !ts.isFunctionDeclaration(statement) ||
      !["startOne", "worker"].includes(statement.name?.text)
    )
      continue;
    checked.push(statement.name.text);
    function inspect(node) {
      if (ts.isCallExpression(node)) {
        const callee = node.expression.getText(source);
        assert.ok(
          ![
            "maintain",
            "storage.maintainStorage",
            "storage.ensureDirectories",
            "storage.archivePaths",
            "storage.pruneArchives",
          ].includes(callee),
          callee,
        );
      }
      ts.forEachChild(node, inspect);
    }
    inspect(statement.body);
  }
  assert.deepEqual(checked.sort(), ["startOne", "worker"]);
  assert.doesNotMatch(text, /\bmaintenanceTimer\b/);
});

test("the public Node entrypoint is read-only for help and list", (t) => {
  const folder = fixture(t);
  for (const command of ["help", "list"]) {
    const result = spawnSync(
      process.execPath,
      [path.join(root, "moorkyz"), command],
      {
        cwd: folder,
        encoding: "utf8",
        timeout: 10000,
      },
    );
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(fs.readdirSync(folder), []);
  }
});
