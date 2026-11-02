import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

for (const width of [320, 390, 1280]) {
  test(`crossed-tools branding stays large, black and accessible at ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.goto("/");

    for (const theme of ["light", "dark"]) {
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      const brand = page.getByRole("link", {
        name: "Moorkyz Tools",
        exact: true,
      });
      await expect(brand).toHaveAttribute("href", "/#home");
      const icon = brand.locator("img");
      await expect(icon).toHaveAttribute("src", "/moorkyz-tools.svg");
      await expect(icon).toHaveAttribute("alt", "");
      await expect(icon).toHaveCSS("width", "40px");
      await expect(icon).toHaveCSS("height", "40px");
      await expect(icon).toHaveCSS("background-color", "rgb(255, 255, 255)");
      await expect
        .poll(() =>
          icon.evaluate((image) => image.complete && image.naturalWidth > 0),
        )
        .toBe(true);
      const iconBox = await icon.boundingBox();
      const titleBox = await brand.locator("strong").boundingBox();
      expect(iconBox).not.toBeNull();
      expect(titleBox).not.toBeNull();
      expect(titleBox.x).toBeGreaterThanOrEqual(iconBox.x + iconBox.width);
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      ).toBe(true);
      expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
      await brand.focus();
      await expect(brand).toBeFocused();
      if (theme === "light") {
        await page
          .getByRole("button", { name: /Switch to dark theme/ })
          .click();
      }
    }

    await page
      .getByRole("link", { name: "Moorkyz Tools", exact: true })
      .focus();
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(/\/#home$/);
  });
}

test("favicon, home-screen and social images all use the supplied black tools artwork", async ({
  page,
  request,
}) => {
  await page.goto("/");
  await expect(
    page.locator('link[rel="icon"][type="image/svg+xml"]'),
  ).toHaveAttribute("href", "/moorkyz-tools.svg");
  await expect(page.locator('link[rel="manifest"]')).toHaveAttribute(
    "href",
    "/site.webmanifest",
  );
  const source = await request.get("/moorkyz-tools.svg");
  expect(source.ok()).toBe(true);
  expect(source.headers()["content-type"]).toContain("image/svg+xml");
  const svg = await source.text();
  expect(svg).toContain('stroke="#000"');
  expect(svg).toContain('stroke-width="1.5"');

  for (const [path, size] of [
    ["/favicon-16x16.png", 16],
    ["/favicon-32x32.png", 32],
    ["/apple-touch-icon.png", 180],
    ["/android-chrome-192x192.png", 192],
    ["/android-chrome-512x512.png", 512],
    ["/tools.moorkyz.com-mark.png", 512],
  ]) {
    const image = await request.get(path);
    expect(image.ok()).toBe(true);
    expect(image.headers()["content-type"]).toBe("image/png");
    const result = await page.evaluate(
      async ({ path, size }) => {
        const load = async (url) => {
          const image = new Image();
          image.src = url;
          await image.decode();
          return image;
        };
        const [raster, vector] = await Promise.all([
          load(path),
          load("/moorkyz-tools.svg"),
        ]);
        const pixels = (image) => {
          const canvas = document.createElement("canvas");
          canvas.width = size;
          canvas.height = size;
          const context = canvas.getContext("2d");
          if (!context) throw new Error("Canvas context is unavailable");
          context.drawImage(image, 0, 0, size, size);
          return context.getImageData(0, 0, size, size).data;
        };
        const actual = pixels(raster);
        const expected = pixels(vector);
        let difference = 0;
        let blackPixels = 0;
        for (let index = 0; index < actual.length; index += 4) {
          difference += Math.abs(actual[index] - expected[index]);
          if (actual[index] < 64) blackPixels++;
        }
        return {
          width: raster.naturalWidth,
          height: raster.naturalHeight,
          difference: difference / (size * size * 255),
          blackPixels,
        };
      },
      { path, size },
    );
    expect(result.width).toBe(size);
    expect(result.height).toBe(size);
    expect(result.blackPixels).toBeGreaterThan(size * size * 0.1);
    expect(result.difference).toBeLessThan(0.05);
  }

  const ico = await request.get("/favicon.ico");
  expect(ico.ok()).toBe(true);
  const bytes = await ico.body();
  expect(bytes.readUInt16LE(2)).toBe(1);
  expect(bytes.readUInt16LE(4)).toBe(1);
  expect(bytes[6]).toBe(32);
  expect(bytes[7]).toBe(32);
  const png = await request.get("/favicon-32x32.png");
  expect(bytes.subarray(bytes.readUInt32LE(18))).toEqual(await png.body());
});
