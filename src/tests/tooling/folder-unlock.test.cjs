"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const path = require("node:path");
const { test } = require("node:test");

for (const host of ["powershell.exe", "pwsh.exe"]) {
  test(
    `Folder Unlock safety and behavior under ${host}`,
    {
      skip: process.platform !== "win32",
    },
    () => {
      const result = spawnSync(
        host,
        [
          "-NoProfile",
          "-NonInteractive",
          "-File",
          path.resolve(
            __dirname,
            "../../../tools/folder-unlock/tests/Verify.ps1",
          ),
        ],
        { encoding: "utf8", timeout: 60000 },
      );
      assert.ifError(result.error);
      assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
      assert.match(result.stdout, /PASS: \d+ folder-unlock assertions/);
    },
  );
}
