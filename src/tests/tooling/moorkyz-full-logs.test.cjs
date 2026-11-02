"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const root = path.resolve(__dirname, "..", "..", "..");
for (const name of ["moorkyz", "tools.moorkyz.com"]) {
  test(`${name} streams the full current service log without tail truncation`, (t) => {
    const folder = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-full-logs-"));
    t.after(() => fs.rmSync(folder, { recursive: true, force: true }));
    const state = path.join(folder, `.${name}`);
    fs.mkdirSync(state);
    const file = path.join(state, "portal.log");
    const text = `BEGIN full history\n${"complete log line\n".repeat(40000)}END full history\n`;
    fs.writeFileSync(file, text);
    fs.writeFileSync(
      path.join(folder, `${name}.config.json`),
      JSON.stringify({
        defaultProject: "portal",
        projects: { portal: { cwd: ".", url: "http://127.0.0.1:4320" } },
      }),
    );
    const run = (...args) =>
      spawnSync(
        process.execPath,
        [
          path.join(
            root,
            name === "moorkyz"
              ? "moorkyz"
              : "src/.moorkyz/scripts/tools.moorkyz.com.cjs",
          ),
          "logs",
          "portal",
          ...args,
        ],
        {
          cwd: folder,
          encoding: "utf8",
          timeout: 10000,
          maxBuffer: 2 * 1024 * 1024,
        },
      );
    const full = run("--all");
    assert.equal(full.status, 0, full.stderr);
    assert.equal(full.stdout, text, "including more than the old 512 KiB cap");
    const tail = run("--lines", "2");
    assert.equal(tail.status, 0, tail.stderr);
    assert.equal(tail.stdout, "complete log line\nEND full history\n");
    for (const args of [
      ["--all", "--lines", "2"],
      ["--all", "--follow"],
    ]) {
      const invalid = run(...args);
      assert.equal(invalid.status, 1);
      assert.match(invalid.stderr, /do not combine/);
    }
    assert.equal(
      fs.readFileSync(file, "utf8"),
      text,
      "log reads never mutate history",
    );
  });
}
