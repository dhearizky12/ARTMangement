import { test, expect } from "@playwright/test";
import fs from "node:fs";
const fixturesPath = process.env.QA_FIXTURES;
const fixtures = fixturesPath
  ? JSON.parse(fs.readFileSync(fixturesPath, "utf8"))
  : null;
test.describe("admin separation", () => {
  test.skip(
    !fixtures,
    "Set QA_FIXTURES to the isolated test environment fixture file; never run against production.",
  );
  for (const width of [375, 393]) {
    test(`admin area separated from customer app at ${width}px`, async ({
      browser,
    }) => {
      const context = await browser.newContext({
        baseURL: process.env.QA_FRONTEND_URL || "http://localhost:5191",
        viewport: { width, height: 852 },
      });
      const page = await context.newPage();
      const errors: string[] = [];
      page.on("pageerror", (e) => errors.push(e.message));
      async function audit(name: string) {
        await expect
          .poll(() =>
            page.evaluate(
              () => document.documentElement.scrollWidth <= innerWidth,
            ),
          )
          .toBe(true);
        await page.screenshot({
          path: `../docs/qa/admin-separation-${name}-${width}.png`,
          fullPage: true,
        });
      }
      // Unknown admin URL while signed out must land on the admin login page,
      // never a customer page.
      await page.goto("/admin/dashboard");
      await expect(page).toHaveURL(/\/admin\/login\?returnTo=/);
      await audit("login");
      // returnTo is only honoured for /admin/ paths; evil input must bounce to go dashboard.
      await page.goto(
        "/admin/login?returnTo=https%3A%2F%2Fevil.example%2Fcallback",
      );
      await page.getByLabel("Email", { exact: true }).fill(fixtures.adminEmail);
      await page
        .getByLabel("Kata sandi", { exact: true })
        .fill(fixtures.adminPassword);
      await page
        .getByRole("button", { name: "Masuk sebagai admin", exact: true })
        .click();
      await expect(page).toHaveURL(/\/admin\/dashboard$/);
      await expect(
        page.getByRole("link", { name: "Dashboard" }),
      ).toBeVisible();
      await audit("dashboard");
      // /admin itself redirects to /admin/dashboard.
      await page.goto("/admin");
      await expect(page).toHaveURL(/\/admin\/dashboard$/);
      // A valid /admin/ returnTo is honoured: already authenticated admins
      // visiting /admin/login bounce straight to the requested admin page.
      await page.goto("/admin/login?returnTo=%2Fadmin%2Forders");
      await expect(page).toHaveURL(/\/admin\/orders$/);
      await expect(
        page.getByRole("heading", { name: "Pesanan layanan" }),
      ).toBeVisible();
      await audit("orders");
      // Unknown admin route renders the admin 404 inside the admin shell.
      await page.goto("/admin/tidak-ada");
      await expect(
        page.getByRole("heading", { name: "Halaman tidak ditemukan" }),
      ).toBeVisible();
      await audit("notfound");
      // Admin shell links only inside /admin/*: logout via the header returns to the admin login.
      await page
        .locator(".site-header")
        .getByRole("button", { name: "Keluar" })
        .click();
      await expect(page).toHaveURL(/\/admin\/login/);
      expect(errors).toEqual([]);
      await context.close();
    });
  }
});