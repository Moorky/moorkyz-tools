import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("catalog cards have compact icon-free headings and top-right version labels", async ({
  page,
}) => {
  await page.goto("/");
  await page.emulateMedia({ reducedMotion: "reduce" });
  const cards = page.locator(".tool-card");
  expect(await cards.count()).toBeGreaterThan(0);

  for (const theme of ["light", "dark"]) {
    if ((await page.locator("html").getAttribute("data-theme")) !== theme) {
      await page
        .getByRole("button", { name: `Switch to ${theme} theme` })
        .click();
    }
    for (const width of [320, 390, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      for (const card of await cards.all()) {
        await expect(
          card.locator(".tool-monogram, .tool-card-arrow, svg, img"),
        ).toHaveCount(0);
        await expect(card).not.toContainText(/[↗→]/);
        const version = card.locator(".tool-card-top .tool-version");
        await expect(version).toHaveText("v1.0.0");
        await expect(version).toHaveCSS("font-family", /DM Mono/);
        const metrics = await card.evaluate((element) => {
          const category = element.querySelector(".tool-category");
          const version = element.querySelector(".tool-version");
          const heading = element.querySelector("h3");
          const identifier = element.querySelector(".tool-id");
          if (!category || !version || !heading || !identifier) {
            throw new Error("Card is missing its metadata or heading.");
          }
          const cardBox = element.getBoundingClientRect();
          const categoryBox = category.getBoundingClientRect();
          const versionBox = version.getBoundingClientRect();
          const headingBox = heading.getBoundingClientRect();
          const idBox = identifier.getBoundingClientRect();
          const categoryStyle = getComputedStyle(category);
          const versionStyle = getComputedStyle(version);
          return {
            topInset: versionBox.top - cardBox.top,
            rightInset: cardBox.right - versionBox.right,
            metadataGap: versionBox.left - categoryBox.right,
            metadataAlignment: Math.abs(versionBox.top - categoryBox.top),
            headingInset: headingBox.top - cardBox.top,
            identifierInset: idBox.top - cardBox.top,
            identifierGap: idBox.top - headingBox.bottom,
            matchingStyle:
              categoryStyle.fontFamily === versionStyle.fontFamily &&
              categoryStyle.fontSize === versionStyle.fontSize &&
              categoryStyle.color === versionStyle.color,
            fits: element.scrollWidth <= element.clientWidth,
          };
        });
        expect(metrics.topInset).toBeLessThanOrEqual(22);
        expect(metrics.rightInset).toBeLessThanOrEqual(22);
        expect(metrics.metadataGap).toBeGreaterThanOrEqual(12);
        expect(metrics.metadataAlignment).toBeLessThanOrEqual(1);
        expect(metrics.headingInset).toBeLessThanOrEqual(72);
        expect(metrics.identifierInset).toBeLessThanOrEqual(180);
        expect(metrics.identifierGap).toBeGreaterThanOrEqual(0);
        expect(metrics.identifierGap).toBeLessThanOrEqual(8);
        expect(metrics.matchingStyle).toBe(true);
        expect(metrics.fits).toBe(true);
      }
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth,
        ),
      ).toBe(true);
    }
    expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  }

  const link = cards.first().getByRole("heading").getByRole("link");
  const target = await link.getAttribute("href");
  await page.keyboard.press("Tab");
  await link.focus();
  await expect(link).toBeFocused();
  await expect(cards.first()).toHaveCSS("outline-style", "solid");
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(new URL(target, page.url()).href);
  await expect(page.locator(".tool-detail-heading .tool-id")).toBeVisible();
});

test("card descriptions fill the main space and footers contain only useful facts", async ({
  page,
  request,
}) => {
  const requests = [];
  page.on("request", (request) => requests.push(request.url()));
  await page.goto("/");
  await page.setViewportSize({ width: 1440, height: 1000 });
  const cards = page.locator(".tool-card");

  for (const card of await cards.all()) {
    const words = (await card.locator(".tool-card-description").innerText())
      .trim()
      .split(/\s+/);
    expect(words.length).toBeGreaterThanOrEqual(50);
    expect(words.length).toBeLessThanOrEqual(100);
    const footer = card.locator(".tool-card-bottom");
    await expect(footer.locator("dt")).toHaveText([
      "Release date",
      "Interested in more?",
      "File size",
      "Usefulness (editorial)",
    ]);
    await expect(footer.locator("dd").first()).toHaveText(
      "Not publicly released",
    );
    await expect(footer).not.toContainText("Explore tool");
    await expect(footer.locator("dd").last()).toHaveText(/^(?:[1-9]|10)\/10$/);
    const contact = footer.getByRole("link", {
      name: /^Get in touch about/,
    });
    await expect(contact).toHaveAttribute(
      "href",
      "https://moorkyz.com/contact",
    );
    await page.keyboard.press("Tab");
    await contact.focus();
    await expect(contact).toBeFocused();
    await contact.click({ trial: true });

    const href = await card
      .getByRole("heading")
      .getByRole("link")
      .getAttribute("href");
    const slug = href.split("/").at(-1);
    const fileSize = footer.locator("dd").nth(2);
    if (slug === "toolbar") {
      await expect(fileSize).toHaveText("Up to 8 KiB HTML");
    } else {
      const archive = await request.get(`/tool-sources/${slug}.zip`);
      expect(archive.ok()).toBe(true);
      const bytes = (await archive.body()).length;
      await expect(fileSize).toHaveText(
        `${(bytes / 1024).toFixed(1)} KiB source ZIP`,
      );
      await expect(fileSize).toHaveAttribute(
        "title",
        `${bytes.toLocaleString("en")} bytes`,
      );
    }
  }
  expect(
    requests.filter(
      (url) => new URL(url).origin !== new URL(page.url()).origin,
    ),
  ).toEqual([]);
  await page.route("https://moorkyz.com/contact", (route) =>
    route.fulfill({ contentType: "text/html", body: "<h1>Contact</h1>" }),
  );
  await cards
    .first()
    .getByRole("link", { name: /^Get in touch about/ })
    .click();
  await expect(page).toHaveURL("https://moorkyz.com/contact");
});
