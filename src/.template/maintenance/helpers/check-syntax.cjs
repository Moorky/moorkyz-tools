"use strict";

const fs = require("node:fs");
const { spawnSync } = require("node:child_process");
const { scopedPath } = require("../../cli/helpers/storage.cjs");

function checkSyntax(root, files) {
  const base = fs.realpathSync(root);
  const failures = [];
  let checked = 0;
  for (const file of new Set(files)) {
    if (!/\.[cm]?js$/.test(file)) continue;
    let target;
    try {
      target = scopedPath(base, file);
    } catch (error) {
      if (error.code === "ENOENT") continue;
      failures.push(`${file}: ${error.message}`);
      continue;
    }
    if (!fs.lstatSync(target).isFile()) {
      failures.push(
        `${file}: expected a regular, repository-local source file`,
      );
      continue;
    }
    const result = spawnSync(process.execPath, ["--check", target], {
      cwd: base,
      encoding: "utf8",
      timeout: 10_000,
      windowsHide: true,
    });
    checked++;
    if (result.status !== 0) {
      failures.push(
        `${file}: ${result.error?.message || result.stderr.trim() || `node --check exited ${result.status}`}`,
      );
    }
  }
  return { checked, failures };
}

module.exports = { checkSyntax };
