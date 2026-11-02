"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const scanner = path.resolve(__dirname, "../../scripts/verify-source.mjs");

function removeFixture(root) {
  assert.equal(path.dirname(path.resolve(root)), path.resolve(os.tmpdir()));
  assert.match(path.basename(root), /^moorkyz-source-(?:scan|target)-/);
  fs.rmSync(root, { recursive: true, force: true });
}

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-source-scan-"));
  t.after(() => removeFixture(root));
  const git = (...args) => {
    const result = spawnSync("git", args, {
      cwd: root,
      encoding: "utf8",
      windowsHide: true,
    });
    assert.equal(result.status, 0, result.stderr);
  };
  git("init", "--quiet");
  return {
    root,
    git,
    run: () =>
      spawnSync(process.execPath, [scanner], {
        cwd: root,
        encoding: "utf8",
        windowsHide: true,
        timeout: 10000,
      }),
  };
}

test("source scan inspects working files after tracked deletions and file-to-directory moves", (t) => {
  const { root, git, run } = fixture(t);
  fs.writeFileSync(path.join(root, "deleted.md"), "Old location.");
  fs.writeFileSync(path.join(root, "moved.md"), "Old file.");
  git("add", "--", "deleted.md", "moved.md");
  fs.unlinkSync(path.join(root, "deleted.md"));
  fs.unlinkSync(path.join(root, "moved.md"));
  fs.mkdirSync(path.join(root, "moved.md"));
  fs.writeFileSync(
    path.join(root, "moved.md", "current.md"),
    "Current content.",
  );
  fs.writeFileSync(
    path.join(root, "current.cjs"),
    'throw new Error("do not execute");',
  );
  const clean = run();
  assert.equal(clean.status, 0, clean.stdout + clean.stderr);
  fs.writeFileSync(
    path.join(root, "moved.md", "current.md"),
    "-----BEGIN " + "PRIVATE KEY-----",
  );
  const flagged = run();
  assert.equal(flagged.status, 1, flagged.stdout + flagged.stderr);
  assert.match(
    flagged.stderr,
    /moved\.md\/current\.md:1: possible credential \(value redacted\)/,
  );
  assert.doesNotMatch(flagged.stderr, /BEGIN/);
});

test("source scan refuses a linked source without reading or disclosing its target", (t) => {
  const { root, run } = fixture(t);
  const outside = fs.mkdtempSync(
    path.join(os.tmpdir(), "moorkyz-source-target-"),
  );
  t.after(() => removeFixture(outside));
  const target = path.join(outside, "target.json");
  fs.writeFileSync(target, "PRIVATE FIXTURE CONTENT");
  try {
    fs.symlinkSync(target, path.join(root, "linked.json"));
  } catch (error) {
    if (error.code !== "EPERM" && error.code !== "EACCES") throw error;
    t.skip("Creating file symlinks is unavailable for this Windows account.");
    return;
  }
  const result = run();
  assert.equal(result.status, 1, result.stdout + result.stderr);
  assert.match(
    result.stderr,
    /linked\.json: source is not a regular file; inspection refused/,
  );
  assert.doesNotMatch(result.stdout + result.stderr, /PRIVATE FIXTURE CONTENT/);
  assert.equal(fs.readFileSync(target, "utf8"), "PRIVATE FIXTURE CONTENT");
});
