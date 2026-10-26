import { expect, test } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import os from "node:os";

test("real Moorky Directory Cleaner preview, Keep priority, move and archive preserve fixture bytes", async ({
  page,
}) => {
  const root = await fs.mkdtemp(
    path.join(os.tmpdir(), "tools.moorkyz.com-cleaner-browser-"),
  );
  try {
    const source = path.join(root, "source"),
      destination = path.join(root, "moved");
    await fs.mkdir(source);
    await fs.writeFile(path.join(source, "keep.pdf"), "important");
    await fs.writeFile(path.join(source, "old.tmp"), "archive fixture");
    await fs.writeFile(path.join(source, "photo.jpg"), "move fixture");
    await page.goto("/tools/directory-cleaner");
    const workspace = page.locator(".directory-cleaner");
    await workspace.getByLabel("Source folder", { exact: true }).fill(source);
    await workspace.getByRole("button", { name: "Add rule" }).click();
    const rule = workspace.getByRole("group", { name: "Rule 3", exact: true });
    await rule
      .getByRole("combobox", { name: "Action", exact: true })
      .selectOption("move");
    await rule.getByLabel("Pattern", { exact: true }).fill("*.jpg");
    await rule
      .getByLabel("Move destination", { exact: true })
      .fill(destination);
    await workspace
      .getByRole("button", { name: "Connect local service" })
      .click();
    await expect(
      workspace
        .getByRole("status")
        .filter({ hasText: "Connected to the local service" }),
    ).toBeVisible();
    await workspace.getByRole("button", { name: "Preview files" }).click();
    await expect(
      workspace.getByRole("heading", { name: "Preview: 3 files, 2 changes" }),
    ).toBeVisible();
    expect((await fs.readdir(source)).sort()).toEqual([
      "keep.pdf",
      "old.tmp",
      "photo.jpg",
    ]);
    await expect(
      workspace.getByRole("button", {
        name: "Archive and move confirmed files",
      }),
    ).toBeDisabled();
    const previewText = await workspace
      .locator(".cleaner-preview")
      .textContent();
    const id = /APPLY ([0-9a-f-]{36})/.exec(previewText)[1];
    await workspace
      .getByLabel("Apply confirmation", { exact: true })
      .fill(`APPLY ${id}`);
    await workspace
      .getByRole("button", { name: "Archive and move confirmed files" })
      .click();
    await expect(
      workspace.getByRole("heading", { name: "Cleanup: completed" }),
    ).toBeVisible();
    expect(await fs.readFile(path.join(source, "keep.pdf"), "utf8")).toBe(
      "important",
    );
    expect(await fs.readFile(path.join(destination, "photo.jpg"), "utf8")).toBe(
      "move fixture",
    );
    expect(
      await fs.readFile(
        path.join(source, ".directory-cleaner-archive", id, "files", "old.tmp"),
        "utf8",
      ),
    ).toBe("archive fixture");
    await fs.access(
      path.join(source, ".directory-cleaner-archive", id, "receipt.jsonl"),
    );
    await workspace
      .getByRole("button", { name: "Disconnect local service" })
      .click();
    await expect(
      workspace.getByRole("button", { name: "Connect local service" }),
    ).toBeVisible();
  } finally {
    expect(path.basename(root)).toMatch(
      /^tools\.moorkyz\.com-cleaner-browser-/,
    );
    await fs.rm(root, { recursive: true, force: true });
  }
});

test("stale real preview fails closed and changing settings invalidates confirmation", async ({
  page,
}) => {
  const root = await fs.mkdtemp(
    path.join(os.tmpdir(), "tools.moorkyz.com-cleaner-browser-"),
  );
  try {
    await fs.writeFile(path.join(root, "a.tmp"), "original");
    await page.goto("/tools/directory-cleaner");
    const workspace = page.locator(".directory-cleaner");
    await workspace.getByLabel("Source folder", { exact: true }).fill(root);
    await workspace
      .getByRole("button", { name: "Connect local service" })
      .click();
    await workspace.getByRole("button", { name: "Preview files" }).click();
    await expect(
      workspace.getByRole("heading", { name: "Preview: 1 files, 1 changes" }),
    ).toBeVisible();
    const id = /APPLY ([0-9a-f-]{36})/.exec(
      await workspace.locator(".cleaner-preview").textContent(),
    )[1];
    await fs.writeFile(path.join(root, "a.tmp"), "changed after preview");
    await workspace
      .getByLabel("Apply confirmation", { exact: true })
      .fill(`APPLY ${id}`);
    await workspace
      .getByRole("button", { name: "Archive and move confirmed files" })
      .click();
    await expect(
      workspace.getByRole("heading", { name: "Cleanup: failed" }),
    ).toBeVisible();
    await expect(
      workspace.getByRole("alert").filter({ hasText: "stale" }),
    ).toBeVisible();
    expect(await fs.readFile(path.join(root, "a.tmp"), "utf8")).toBe(
      "changed after preview",
    );
    await expect(
      workspace.getByLabel("Apply confirmation", { exact: true }),
    ).toHaveCount(0);
  } finally {
    await fs.rm(root, { recursive: true, force: true });
  }
});
