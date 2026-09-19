import { test, expect } from "@playwright/test";
import fs from "node:fs";
const fixturesPath = process.env.QA_FIXTURES;
const fixtures = fixturesPath
  ? JSON.parse(fs.readFileSync(fixturesPath, "utf8"))
  : null;
test.describe("live marketplace", () => {
  test.skip(
    !fixtures,
    "Set QA_FIXTURES to the isolated test environment fixture file; never run against production.",
  );
  for (const width of [360, 420]) {
    test(`live marketplace + admin wizard at ${width}px`, async ({
      browser,
    }) => {
      const context = await browser.newContext({
        baseURL: process.env.QA_FRONTEND_URL || "http://localhost:5191",
        viewport: { width, height: 880 },
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
          path: `../docs/qa/marketplace-${name}-${width}.png`,
          fullPage: true,
        });
      }
      await page.goto("/");
      await expect(
        page.getByRole("heading", { name: "Bantuan untuk hari-hari Anda." }),
      ).toBeVisible();
      // Finish the anonymous refresh before navigating to login. This avoids a
      // late Set-Cookie deletion racing with the fixture refresh cookie below.
      await page.waitForLoadState("networkidle");
      await expect(page.locator(".provider-card").first()).toBeVisible();
      await audit("home");
      await page
        .getByRole("navigation", { name: "Navigasi utama" })
        .getByRole("link", { name: "Cari", exact: true })
        .click();
      await expect(
        page.getByRole("heading", { name: "Temukan bantuan Anda." }),
      ).toBeVisible();
      await audit("search");
      await page.goto(`/providers/${fixtures.providerId}`);
      await expect(
        page.getByRole("link", { name: "Masuk & Pesan" }),
      ).toBeVisible();
      await audit("detail");
      await page.getByRole("link", { name: "Masuk & Pesan" }).click();
      await expect(page).toHaveURL(/login\?returnTo=/);
      // Restore a real refresh session belonging to a fixture Customer in the isolated PostgreSQL database.
      await context.addCookies([
        {
          name: "bantubantu_refresh",
          value: fixtures.customers[String(width)],
          domain: "localhost",
          path: "/api/auth",
          httpOnly: true,
          sameSite: "Lax",
        },
      ]);
      await page.reload();
      await expect(page).toHaveURL(
        new RegExp(`/providers/${fixtures.providerId}$`),
      );
      await page
        .getByRole("button", { name: "Pesan layanan", exact: true })
        .click();
      await page
        .getByLabel("Tanggal mulai layanan")
        .fill(new Date(Date.now() + 2 * 86400000).toISOString().slice(0, 10));
      const location = page.getByRole("combobox");
      await location.fill("Sekaran");
      await expect(page.getByRole("option").first()).toBeVisible();
      await location.press("ArrowDown");
      await location.press("Enter");
      await page
        .getByLabel("Detail alamat layanan")
        .fill("Jalan QA nomor 12 RT 01 RW 02");
      await page
        .getByRole("button", { name: "Konfirmasi pesanan", exact: true })
        .click();
      await expect(page).toHaveURL(/\/orders$/);
      await expect(
        page.getByText("Jalan QA nomor 12 RT 01 RW 02").first(),
      ).toBeVisible();
      await audit("orders");
      await page.goto("/account");
      await page.getByRole("button", { name: "Keluar", exact: true }).click();
      await expect(
        page.getByRole("link", { name: "Masuk dengan Google" }),
      ).toBeVisible();
      await page.goto("/admin/login");
      await page.getByLabel("Email", { exact: true }).fill(fixtures.adminEmail);
      await page
        .getByLabel("Kata sandi", { exact: true })
        .fill(fixtures.adminPassword);
      await page
        .getByRole("button", { name: "Masuk sebagai admin", exact: true })
        .click();
      await expect(page).toHaveURL(/\/admin$/);
      await page.getByRole("button", { name: "Buat draft penyedia" }).click();
      await expect(page).toHaveURL(/\/admin\/providers\//);
      await page
        .getByLabel("Nama lengkap", { exact: true })
        .fill(`Penyedia QA ${width}`);
      await page.getByLabel("Usia", { exact: true }).fill("30");
      await page
        .getByLabel("Tentang penyedia")
        .fill(
          "Profil penyedia untuk verifikasi browser di database pengujian.",
        );
      await page.getByLabel("Pengalaman (tahun)").fill("5");
      await page
        .getByRole("button", { name: "Simpan informasi personal" })
        .click();
      await expect(
        page.getByRole("heading", { name: "Alamat", exact: true }),
      ).toBeVisible();
      const adminLocation = page.getByRole("combobox");
      await adminLocation.fill("Sekaran");
      await expect(page.getByRole("option").first()).toBeVisible();
      const lastLabel = await page.getByRole("option").last().innerText();
      await adminLocation.press("ArrowUp");
      await adminLocation.press("Enter");
      await expect(adminLocation).toHaveValue(lastLabel);
      await adminLocation.fill("TidakAdaWilayahQAXYZ");
      await expect(
        page.getByText("Alamat tidak ditemukan, coba kata kunci lain"),
      ).toBeVisible();
      await adminLocation.fill("Sekaran");
      await expect(page.getByRole("option").first()).toBeVisible();
      await audit("address");
      await adminLocation.press("ArrowDown");
      await adminLocation.press("Enter");
      await page
        .getByLabel("Detail alamat domisili")
        .fill("Jalan Domisili QA nomor 15 RT 01 RW 01");
      await page.getByLabel("Kode pos").fill("50229");
      await page.getByRole("button", { name: "Simpan alamat" }).click();
      await expect(
        page.getByRole("heading", { name: "Dokumen", exact: true }),
      ).toBeVisible();
      for (const type of ["KTP", "KK"]) {
        await page.getByLabel("Jenis dokumen").selectOption(type);
        await page
          .getByLabel("Foto dokumen")
          .setInputFiles(fixtures.documentPath);
        await page
          .getByRole("button", { name: "Unggah dokumen", exact: true })
          .click();
        if (type === "KTP")
          await expect(
            page.getByRole("button", { name: "Unduh KTP" }),
          ).toBeVisible();
      }
      await expect(
        page.getByRole("heading", { name: "Layanan", exact: true }),
      ).toBeVisible();
      await page.getByLabel("Asisten rumah tangga", { exact: true }).check();
      await page
        .getByLabel("Keahlian (pisahkan dengan koma)")
        .fill("Membersihkan rumah, Memasak");
      await page.getByLabel("Bahasa (pisahkan dengan koma)").fill("Indonesia");
      await page.getByLabel("Tarif (Rp)").fill("120000");
      for (const day of [
        "Minggu",
        "Senin",
        "Selasa",
        "Rabu",
        "Kamis",
        "Jumat",
        "Sabtu",
      ])
        await page.getByLabel(day, { exact: true }).check();
      await page.getByRole("button", { name: "Simpan profil layanan" }).click();
      await expect(
        page.getByRole("heading", { name: "Verifikasi", exact: true }),
      ).toBeVisible();
      for (const label of [
        "Identitas telah diperiksa",
        "Pemeriksaan latar belakang lulus",
        "Kontrak telah ditandatangani",
      ])
        await page.getByLabel(label).check();
      await page.getByLabel("Status verifikasi").selectOption("Verified");
      await page
        .getByRole("button", { name: "Simpan hasil pemeriksaan" })
        .click();
      await expect(
        page.locator(".badge").filter({ hasText: /^Verified$/ }),
      ).toBeVisible();
      await audit("verification");
      await page.reload();
      await expect(
        page.getByRole("heading", { name: "Verifikasi", exact: true }),
      ).toBeVisible();
      await page.goto("/admin/agencies");
      await expect(
        page.getByRole("heading", { name: "Agency & administrator" }),
      ).toBeVisible();
      await audit("agencies");
      expect(errors).toEqual([]);
      await context.close();
    });
  }
});
