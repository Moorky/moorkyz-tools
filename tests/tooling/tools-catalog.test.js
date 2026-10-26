import assert from "node:assert/strict";
import { test } from "node:test";
import { readFile } from "node:fs/promises";
import {
  TOOLS,
  findTool,
  formatToolDate,
} from "../../packages/portal/src/data/tools.ts";
import { createSourceZip } from "../../scripts/prepare-tool-sources.mjs";

test("tool registry has unique routable slugs and complete, honest detail metadata", () => {
  assert.equal(new Set(TOOLS.map((tool) => tool.slug)).size, TOOLS.length);
  assert.equal(findTool("missing"), undefined);
  assert.equal(findTool("__proto__"), undefined);
  for (const tool of TOOLS) {
    assert.match(tool.slug, /^[a-z][a-z0-9-]*$/);
    assert.equal(findTool(tool.slug), tool);
    for (const key of ["title", "summary", "description", "version"])
      assert.ok(tool[key].length > 0);
    assert.match(tool.updatedOn, /^\d{4}-\d{2}-\d{2}$/);
    assert.ok(tool.useCases.length > 0);
    assert.ok(tool.setup.length > 0);
    assert.equal(tool.releaseDate, null);
  }
  assert.equal(formatToolDate("2026-10-23"), "October 23, 2026");
});

for (const tool of TOOLS.filter((entry) => entry.source))
  test(`${tool.title} downloads contain every allowlisted file and exact original ZIP bytes`, async () => {
    const source = JSON.parse(
      await readFile(
        new URL(
          `../../packages/portal/public/tool-sources/${tool.source.bundle}.json`,
          import.meta.url,
        ),
        "utf8",
      ),
    );
    assert.deepEqual(Object.keys(source), tool.source.files);
    const archive = await readFile(
      new URL(
        `../../packages/portal/public/tool-sources/${tool.source.bundle}.zip`,
        import.meta.url,
      ),
    );
    let offset = 0;
    const names = [];
    while (archive.readUInt32LE(offset) === 0x04034b50) {
      const size = archive.readUInt32LE(offset + 18);
      const nameSize = archive.readUInt16LE(offset + 26);
      const name = archive
        .subarray(offset + 30, offset + 30 + nameSize)
        .toString();
      names.push(name);
      assert.ok(!/node_modules|history\.json|test-results|\.git\//.test(name));
      const original = await readFile(
        new URL(`../../tools/${tool.source.bundle}/${name}`, import.meta.url),
      );
      const bytes = archive.subarray(
        offset + 30 + nameSize,
        offset + 30 + nameSize + size,
      );
      assert.deepEqual(bytes, original, name);
      assert.equal(source[name], original.toString("utf8"), name);
      offset += 30 + nameSize + size;
    }
    assert.deepEqual(names, tool.source.files);
    assert.equal(archive.readUInt32LE(offset), 0x02014b50);
    assert.equal(archive.readUInt32LE(archive.length - 22), 0x06054b50);
    assert.equal(archive.readUInt16LE(archive.length - 12), names.length);
  });

test("source ZIP rejects escaping and malformed paths", () => {
  for (const name of [
    "../secret",
    "/absolute",
    "a/../../secret",
    "a\\secret",
    "a//b",
    "./file",
  ]) {
    assert.throws(
      () => createSourceZip([{ name, bytes: Buffer.from("source") }]),
      /Unsafe source path/,
    );
  }
});

test("Media Recover is an independent unreleased desktop tool with explanation-first metadata", () => {
  const tool = findTool("mediarecover");
  assert.equal(tool.title, "Moorkyz Media Recover");
  assert.equal(tool.id, "moorkyz-tools-mediarecover");
  assert.equal(tool.workspace, undefined);
  assert.equal(tool.releaseDate, null);
  assert.match(tool.downloadNotice, /Not yet released/);
  assert.match(tool.description, /cannot scan your computer or phone/);
  assert.deepEqual(
    tool.howItWorks.map((step) => step.title),
    [
      "Choose source",
      "Scan",
      "Review",
      "Recover",
      "Verify",
      "Open saved folder",
    ],
  );
  assert.equal(tool.features.length, 5);
  assert.match(tool.limitations.join(" "), /does not mean undeleting/);
  assert.match(
    tool.setup.map((step) => step.text).join(" "),
    /tools\/mediarecover within this repository/,
  );
  assert.match(tool.source.notice, /Canonical source maintained/);
  assert.doesNotMatch(tool.source.notice, /independent.*project|not a second/);
});
