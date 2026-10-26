import { expect, test } from "@playwright/test";

const origin = `http://127.0.0.1:${process.env.TOOLS_MOORKYZ_COM_PORTAL_PORT ?? process.env.PORTAL_TEST_PORT ?? "4174"}`;

test("integrated local service runs real exports, preserves downloads and stops view polling", async ({
  page,
}) => {
  test.setTimeout(120000);
  const requests = [];
  const errors = [];
  page.on("request", (request) => requests.push(request.url()));
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(`${origin}/tools/videocrunch`);
  expect(requests.filter((url) => url.includes("/api/local-tools/"))).toEqual(
    [],
  );
  await page
    .getByRole("button", { name: "Open Moorkyz Video Crunch workspace" })
    .click();
  const workspace = page.frameLocator(
    'iframe[title="Moorkyz Video Crunch local workspace"]',
  );
  await expect(workspace.locator("#submit-button")).toBeEnabled();
  await workspace
    .locator("#video-url")
    .fill("http://127.0.0.1:8877/sample.mp4");
  await workspace.getByRole("radio", { name: /Extreme/ }).check();
  await workspace.locator("#submit-button").click();
  await expect(workspace.locator("#active-job .job-status")).toHaveText(
    "Ready to watch",
    { timeout: 60000 },
  );
  const downloadEvent = page.waitForEvent("download");
  await workspace
    .locator("#active-job")
    .getByRole("link", { name: "Original", exact: true })
    .click();
  const download = await downloadEvent;
  expect(download.suggestedFilename()).toBe("source.mp4");
  expect(await download.failure()).toBeNull();
  const popupEvent = page.waitForEvent("popup");
  await workspace
    .locator("#active-job")
    .getByRole("link", { name: /Watch offline/ })
    .click();
  const player = await popupEvent;
  await expect(player.locator("video")).toBeVisible();
  await expect
    .poll(() => player.locator("video").evaluate((video) => video.readyState))
    .toBeGreaterThanOrEqual(2);
  await player.close();
  await page
    .getByRole("button", { name: "Close Moorkyz Video Crunch workspace" })
    .click();
  await expect(page.locator("iframe")).toHaveCount(0);
  const afterClose = requests.length;
  await page.waitForTimeout(2500);
  expect(requests.length).toBe(afterClose);
  await page
    .getByRole("button", { name: "Open Moorkyz Video Crunch workspace" })
    .click();
  await expect(workspace.locator("#submit-button")).toBeEnabled();
  expect(
    requests.filter((url) =>
      url.endsWith("/api/local-tools/videocrunch/index.html"),
    ),
  ).toHaveLength(2);
  await page
    .getByRole("button", { name: "Close Moorkyz Video Crunch workspace" })
    .click();
  expect(errors).toEqual([]);
});

test("unavailable service errors are visible and reconnectable", async ({
  page,
}) => {
  await page.route("**/api/local-tools/videocrunch/api/config", (route) =>
    route.fulfill({
      status: 502,
      contentType: "application/json",
      body: JSON.stringify({
        error: "Cannot reach Moorkyz Video Crunch. Start the local service.",
      }),
    }),
  );
  await page.goto(`${origin}/tools/videocrunch`);
  await page
    .getByRole("button", { name: "Open Moorkyz Video Crunch workspace" })
    .click();
  await expect(
    page.locator(".local-tool-workspace").getByRole("alert"),
  ).toContainText("Cannot reach Moorkyz Video Crunch");
  await page.unroute("**/api/local-tools/videocrunch/api/config");
  await page.getByRole("button", { name: "Retry connection" }).click();
  await expect(
    page
      .frameLocator('iframe[title="Moorkyz Video Crunch local workspace"]')
      .locator("#submit-button"),
  ).toBeEnabled();
  await page
    .getByRole("button", { name: "Close Moorkyz Video Crunch workspace" })
    .click();
});
