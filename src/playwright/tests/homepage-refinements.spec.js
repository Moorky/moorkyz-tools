import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test("homepage has a compact introduction without a domain label in the hero", async ({
  page,
}, testInfo) => {
  await page.goto("/");
  await page.emulateMedia({ reducedMotion: "reduce" });
  const skipLink = page.getByRole("link", { name: "Skip to content" });
  await skipLink.focus();
  await expect(skipLink).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("#main-content")).toBeFocused();

  const hero = page.locator(".catalog-hero");
  await expect(hero.getByRole("heading", { level: 1 })).toHaveText(
    "A little more useful.",
  );
  await expect(hero.locator(".hero-lede")).toHaveText(
    "A library of tools I never planned to build — until they became essential.",
  );
  await expect(hero.locator(".eyebrow")).toHaveCount(0);
  await expect(
    hero.getByText("tools.moorkyz.com", { exact: true }),
  ).toHaveCount(0);
  await expect(page.getByText("WORKBENCH", { exact: true })).toHaveCount(0);
  await expect(
    page.getByRole("link", { name: "Explore the tools" }),
  ).toHaveCount(0);
  await expect(page.locator("#tools-heading")).toHaveText(
    "Small tools. Real lifesavers.",
  );
  await expect(page.locator(".brand")).toHaveAccessibleName("Moorkyz Tools");
  await expect(page.locator(".brand-accent")).toHaveText("Tools");

  for (const theme of ["light", "dark"]) {
    if ((await page.locator("html").getAttribute("data-theme")) !== theme) {
      await page
        .getByRole("button", { name: `Switch to ${theme} theme` })
        .click();
    }
    for (const width of [320, 390, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect.poll(() => page.evaluate(() => innerWidth)).toBe(width);
      await expect(hero.locator("h1")).toHaveCSS(
        "font-size",
        width < 700 ? "32px" : "64px",
      );
      const metrics = await page.evaluate(() => {
        const hero = document.querySelector(".catalog-hero");
        const title = hero.querySelector("h1");
        const lede = hero.querySelector(".hero-lede");
        const logo = document.querySelector(".header-logo");
        const brand = document.querySelector(".brand strong");
        const accent = document.querySelector(".brand-accent");
        return {
          titleSize: parseFloat(getComputedStyle(title).fontSize),
          ledeSize: parseFloat(getComputedStyle(lede).fontSize),
          brandSize: parseFloat(getComputedStyle(brand).fontSize),
          logoWidth: logo.getBoundingClientRect().width,
          logoHeight: logo.getBoundingClientRect().height,
          heroHeight: hero.getBoundingClientRect().height,
          accentColor: getComputedStyle(accent).color,
          fits: document.documentElement.scrollWidth <= innerWidth,
        };
      });
      expect(metrics.titleSize).toBeLessThanOrEqual(width < 700 ? 36 : 64);
      expect(metrics.ledeSize).toBeLessThanOrEqual(16);
      expect(metrics.brandSize).toBeLessThanOrEqual(16);
      expect(metrics.logoWidth).toBe(40);
      expect(metrics.logoHeight).toBe(40);
      expect(metrics.heroHeight).toBeLessThanOrEqual(320);
      expect(metrics.accentColor).toBe(
        theme === "dark" ? "rgb(216, 180, 255)" : "rgb(83, 37, 131)",
      );
      expect(metrics.fits).toBe(true);
      if (width !== 390) {
        await page.screenshot({
          path: testInfo.outputPath(`homepage-${theme}-${width}.png`),
          fullPage: true,
          animations: "disabled",
        });
      }
    }
    expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  }
});

test("catalog displays the requested editorial scores without changing tool IDs", async ({
  page,
}) => {
  await page.goto("/");
  for (const [slug, identifier, score] of [
    ["mediarecover", "moorkyz-tools-mediarecover", "9/10"],
    ["directory-cleaner", "moorkyz-tools-directory-cleaner", "7/10"],
    ["videocrunch", "moorkyz-tools-video-crunch", "8/10"],
    ["toolbar", "moorkyz-tools-toolbar-builder", "6/10"],
  ]) {
    const card = page.locator(".tool-card").filter({
      has: page.locator(`h3 a[href="/tools/${slug}"]`),
    });
    await expect(card.locator(".tool-id")).toHaveText(identifier);
    await expect(
      card.locator(".tool-card-bottom > div").filter({
        has: page.getByText("Usefulness (editorial)", { exact: true }),
      }),
    ).toHaveText(`Usefulness (editorial)${score}`);
  }
});

for (const slug of [
  "toolbar",
  "videocrunch",
  "mediarecover",
  "directory-cleaner",
]) {
  test(`${slug} detail version is exactly 1.0.0 without implying a release`, async ({
    page,
  }) => {
    await page.goto(`/tools/${slug}`);
    const metadata = page.locator(".tool-metadata");
    await expect(
      metadata
        .locator("div")
        .filter({
          has: page.getByText("Version", { exact: true }),
        })
        .locator("dd"),
    ).toHaveText("1.0.0");
    await expect(
      metadata
        .locator("div")
        .filter({
          has: page.getByText("Release Date", { exact: true }),
        })
        .locator("dd"),
    ).toHaveText("Not publicly released");
  });
}
