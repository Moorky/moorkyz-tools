import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { test } from "node:test";

const identity = "moorkyz-tools";
const launcherIdentity = "tools.moorkyz.com";
const previousBrand = ["moor", "kyz"].join("");
const retiredPackage = ["tool", "bar"].join("");
const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const previousIdentity = new RegExp(
  [
    `${previousBrand}-toolbar`,
    `toolbar\\.${previousBrand}\\.com`,
    `@${previousBrand}/`,
    `${previousBrand}-(?:tools-workspace|cleaner|portal-theme|mark\\.png)`,
    `packages[/\\\\]${retiredPackage}[/\\\\]`,
    `@${escape(identity)}/${retiredPackage}\\b`,
    `\\b${retiredPackage} (?:SDK|project|package|repository|website|monorepo|build|dependency)\\b`,
    `legacy ${retiredPackage} product`,
    `parent ${retiredPackage}\\b`,
  ].join("|"),
  "i",
);

test("retired identifier detection distinguishes the feature from project naming", () => {
  for (const text of [
    `${previousBrand}-${retiredPackage}`,
    `https://${retiredPackage}.${previousBrand}.com`,
    `${retiredPackage} package`,
    `${retiredPackage} SDK`,
    `${retiredPackage} build`,
  ]) {
    assert.equal(previousIdentity.test(text), true, text);
  }
  for (const text of [
    identity,
    "Moorkyz Tools",
    ".moorkyz/",
    "moorkyz.cjs",
    "moorkyz status",
    "Moorkyz Toolbar Builder",
    "tools/toolbar-builder/builder.mjs",
    "/tools/toolbar",
    "Toolbar settings",
  ]) {
    assert.equal(previousIdentity.test(text), false, text);
  }
});

test("workspace, portal package and command filters use the project identity", () => {
  const root = JSON.parse(readFileSync("package.json", "utf8"));
  const portal = JSON.parse(
    readFileSync("packages/portal/package.json", "utf8"),
  );
  assert.equal(root.name, identity);
  assert.equal(portal.name, `@${identity}/portal`);
  assert.ok(root.scripts.build.includes(`--filter @${identity}/portal`));
  assert.ok(root.scripts.typecheck.includes(`--filter @${identity}/portal`));
  for (const suffix of ["", ".cjs", ".cmd", ".config.json"]) {
    assert.ok(existsSync(`${launcherIdentity}${suffix}`));
  }
  assert.match(
    readFileSync(`${launcherIdentity}.cjs`, "utf8"),
    new RegExp(escape(`path.join(ROOT, ".${launcherIdentity}")`)),
  );
});

test("all repository text and paths, including historical docs, omit retired project identifiers", () => {
  const files = new Set(
    execFileSync(
      "git",
      ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
      { encoding: "utf8" },
    )
      .split("\0")
      .filter((file) => file && existsSync(file)),
  );
  const findings = [];
  for (const file of files) {
    if (previousIdentity.test(file)) findings.push(`${file}: filename`);
    const bytes = readFileSync(file);
    if (bytes.includes(0)) continue;
    const text = bytes.toString("utf8");
    if (!Buffer.from(text).equals(bytes)) continue;
    for (const [index, line] of text.split(/\r?\n/).entries()) {
      if (previousIdentity.test(line.replace(/\\+\./g, ".")))
        findings.push(`${file}:${index + 1}`);
    }
  }
  assert.deepEqual(findings, []);
});
