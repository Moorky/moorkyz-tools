import { test, expect } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

let fixture, url, emptyUrl;
test.beforeAll(async () => {
  fixture = await mkdtemp(path.join(tmpdir(), "visory-report-ui-"));
  execFileSync(
    "dotnet",
    [
      "run",
      "--project",
      "tools\\visory-media-downloader\\src\\tests\\MoorkszVisoryMediaDownloader.Tests",
      "-c",
      "Debug",
      "--no-build",
      "--",
      "--report-browser-fixture",
      fixture,
    ],
    { encoding: "utf8", timeout: 30000 },
  );
  url = pathToFileURL(
    path.join(fixture, "backup", "reports", "report.html"),
  ).href;
  const emptyFolder = path.join(fixture, "empty");
  execFileSync(
    "dotnet",
    [
      "run",
      "--project",
      "tools\\visory-media-downloader\\src\\tests\\MoorkszVisoryMediaDownloader.Tests",
      "-c",
      "Debug",
      "--no-build",
      "--",
      "--report-browser-fixture",
      emptyFolder,
      "--empty",
    ],
    { encoding: "utf8", timeout: 30000 },
  );
  emptyUrl = pathToFileURL(
    path.join(emptyFolder, "backup", "reports", "report.html"),
  ).href;
});
test.afterAll(async () => {
  if (fixture) await rm(fixture, { recursive: true, force: true });
});

test("local report batches scroll rows, searches all tasks and stays offline", async ({
  page,
}) => {
  const errors = [],
    external = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  page.on("request", (request) => {
    if (/^https?:/.test(request.url())) external.push(request.url());
  });
  await page.goto(url);
  await expect(page.locator("#report-controls")).toBeVisible();
  await expect(page.locator("#report-table tbody tr")).toHaveCount(50);
  await expect(page.locator("#report-count")).toHaveText(
    "Showing 50 of 165 tasks.",
  );
  await page.locator("#report-scroll").evaluate((el) => {
    el.scrollTop = el.scrollHeight;
  });
  await expect(page.locator("#report-table tbody tr")).toHaveCount(100);
  await page.getByRole("button", { name: "Load more results" }).click();
  await expect(page.locator("#report-table tbody tr")).toHaveCount(150);
  await page
    .getByRole("searchbox", { name: "Search all tasks" })
    .fill("fixture-25-54");
  await expect(page.locator("#report-table tbody tr")).toHaveCount(1);
  await expect(page.locator("#report-table")).toContainText(
    "<img src=x onerror=alert(1)>",
  );
  await expect(page.locator("#report-table img")).toHaveCount(0);
  await expect(page.locator("#report-table tbody a")).toHaveCount(2);
  await page.getByRole("searchbox").fill("does-not-exist");
  await expect(page.locator("#report-count")).toHaveText("No matching tasks.");
  await expect(page.locator("#report-more")).toBeHidden();
  expect(errors).toEqual([]);
  expect(external).toEqual([]);
});

test("statistics calendar bounds recorded dates, validates ranges and searches aggregates", async ({
  page,
}) => {
  await page.goto(url);
  await page.getByRole("radio", { name: "Full Statistics" }).check();
  await expect(page.locator("#report-table tbody tr")).toHaveCount(3);
  await expect(page.locator("#report-totals")).toContainText("SUCCESS: 120");
  const from = page.getByLabel("From", { exact: true }),
    to = page.getByLabel("To", { exact: true });
  await expect(from).toHaveAttribute("min", "2026-09-25");
  await expect(to).toHaveAttribute("max", "2026-10-27");
  await page.getByRole("button", { name: "Choose From date" }).click();
  const calendar = page.getByRole("dialog");
  await expect(
    calendar.getByRole("button", { name: "2026-09-24", exact: true }),
  ).toBeDisabled();
  await expect(
    calendar.getByRole("button", { name: "Previous month" }),
  ).toBeDisabled();
  await expect(
    calendar.getByRole("button", { name: "Next month" }),
  ).toBeEnabled();
  await calendar.getByRole("button", { name: "Next month" }).click();
  await expect(
    calendar.getByRole("button", { name: "2026-10-28", exact: true }),
  ).toBeDisabled();
  await expect(
    calendar.getByRole("button", { name: "Next month" }),
  ).toBeDisabled();
  await expect(
    calendar.getByRole("button", { name: "Previous month" }),
  ).toBeEnabled();
  await calendar
    .getByRole("button", { name: "2026-10-26", exact: true })
    .click();
  await expect(calendar).toBeHidden();
  await expect(from).toHaveValue("2026-10-26");
  await from.fill("2026-10-26");
  await to.fill("2026-10-26");
  await expect(page.locator("#report-table tbody tr")).toHaveCount(1);
  await expect(page.locator("#report-totals")).toContainText("SUCCESS: 40");
  await to.fill("2026-10-25");
  await expect(page.getByRole("alert")).toContainText(
    "From must be on or before To",
  );
  await expect(page.locator("#report-table tbody tr")).toHaveCount(0);
  await from.fill("2026-09-24");
  await expect(page.getByRole("alert")).toContainText(
    "between 2026-09-25 and 2026-10-27",
  );
  expect(await from.evaluate((el) => el.checkValidity())).toBe(false);
  await page.getByRole("button", { name: "All recorded dates" }).click();
  await expect(page.getByRole("alert")).toBeEmpty();
  await expect(page.locator("#report-table tbody tr")).toHaveCount(3);
  await page.getByRole("searchbox").fill("ERROR");
  await expect(page.locator("#report-totals")).toContainText("ERROR: 15");
  await expect(page.locator("#report-totals")).toContainText("SUCCESS: 0");
});

test("empty reports disable calendar selection and no-script reports retain readable exports", async ({
  page,
  browser,
}) => {
  await page.goto(emptyUrl);
  await page.getByRole("radio", { name: "Full Statistics" }).check();
  await expect(page.getByLabel("From", { exact: true })).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Choose From date" }),
  ).toBeDisabled();
  await expect(page.locator("#report-count")).toHaveText("No matching days.");
  await page.getByRole("button", { name: "Compare performance" }).click();
  await expect(page.locator("#report-baseline")).toContainText("unknown");
  const context = await browser.newContext({ javaScriptEnabled: false });
  try {
    const preview = await context.newPage();
    await preview.goto(url);
    await expect(preview.locator("#report-table tbody tr")).toHaveCount(50);
    await expect(preview.locator("#report-controls")).toBeHidden();
    await expect(preview.locator("#report-no-script")).toBeVisible();
    await expect(
      preview.getByRole("link", { name: "CSV", exact: true }),
    ).toBeVisible();
  } finally {
    await context.close();
  }
});

test("performance swaps columns, retains the all-time baseline and uses local DM Mono and status colors", async ({
  page,
}) => {
  await page.goto(url);
  const search = page.getByRole("searchbox");
  await search.fill("fixture-27-0.mp4");
  await page.getByRole("button", { name: "Compare performance" }).click();
  await expect(
    page.getByRole("columnheader", { name: "Seconds / MiB", exact: true }),
  ).toBeVisible();
  await expect(page.locator("#report-baseline")).toContainText("2.000 s/MiB");
  await expect(page.locator("#report-table tbody tr").first()).toContainText(
    "50.00% faster",
  );
  await expect(page.locator("#report-table tbody .metric-better")).toHaveCount(
    3,
  );
  await expect(page.locator("#report-table tbody td").nth(6)).toHaveText(
    "1.000",
  );
  await expect(page.locator("#report-table tbody td").nth(7)).toHaveText(
    "1.000",
  );
  await search.fill("fixture-27-30.mp4");
  await expect(page.locator("#report-table tbody .metric-worse")).toHaveCount(
    3,
  );
  await expect(page.locator("#report-table tbody td").nth(6)).toHaveText(
    "3.000",
  );
  await expect(page.locator("#report-table tbody td").nth(7)).toHaveText(
    "0.333",
  );
  await expect(page.locator("#report-baseline")).toContainText("2.000 s/MiB");
  await page.getByRole("button", { name: "Compare performance" }).click();
  await expect(
    page.getByRole("columnheader", { name: "File Size (bytes)", exact: true }),
  ).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  expect(
    await page
      .locator("#report-table tbody td")
      .first()
      .evaluate((el) => getComputedStyle(el).fontFamily),
  ).toContain("DM Mono");
  expect(
    await page.evaluate(() => document.fonts.check('16px "DM Mono"')),
  ).toBe(true);
  await search.fill("");
  const colors = [];
  for (const status of ["SUCCESS", "FAIL", "ERROR"]) {
    await search.fill(status);
    colors.push(
      await page
        .locator(`.status-${status.toLowerCase()}`)
        .first()
        .evaluate((el) => getComputedStyle(el).color),
    );
  }
  expect(colors).toEqual([
    "rgb(33, 98, 52)",
    "rgb(180, 35, 35)",
    "rgb(113, 27, 36)",
  ]);
  expect(
    await page
      .locator("#report-table .error-message")
      .first()
      .evaluate((el) => getComputedStyle(el).color),
  ).toBe("rgb(180, 35, 35)");
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});
