import { expect, test } from "@playwright/test";

const session = {
  accessToken: "provider-access-token",
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  refreshToken: "provider-refresh-token",
  refreshExpiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
  user: {
    id: "11111111-1111-1111-1111-111111111111",
    email: "provider@example.test",
    fullName: "Provider QA",
    pictureUrl: null,
    role: "Provider",
    profileCompleted: true,
    profileStep: "done",
  },
};

const profile = {
  id: session.user.id,
  agencyId: null,
  agencyName: null,
  fullName: "Provider QA",
  age: 31,
  bio: "Penyedia layanan untuk pengujian tampilan mobile.",
  yearsOfExperience: 7,
  jobsCompletedCount: 12,
  pricingType: "PerVisit",
  price: 125000,
  applicationStatus: "Draft",
  verificationStatus: "Verified",
  identityVerified: true,
  backgroundCheckPassed: true,
  contractSigned: true,
  location: "Sekaran, Gunungpati, Kota Semarang, Jawa Tengah",
  categories: [{ id: "cat-1", name: "Perawatan taman" }],
  skills: ["Pemangkasan tanaman"],
  languages: ["Indonesia"],
  availability: ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"].map(
    (day) => ({ dayOfWeek: day, isAvailable: day !== "Sunday" }),
  ),
  rating: 4.8,
  reviewCount: 9,
  reviews: [],
};

const application = {
  provider: {
    provider: profile,
    step: "profile",
    villageId: null,
    addressDetail: "",
    postalCode: "",
    documents: [],
  },
  status: "Draft",
  step: "profile",
  note: null,
  submittedAt: null,
  canEdit: true,
  sections: [
    { id: "personal", label: "Personal", status: "lengkapi" },
    { id: "address", label: "Alamat", status: "lengkapi" },
    { id: "documents", label: "Dokumen", status: "lengkapi" },
    { id: "profile", label: "Layanan", status: "sebagian" },
  ],
};

const orders = [
  {
    id: "order-1",
    providerId: profile.id,
    providerName: "Provider QA",
    status: "Confirmed",
    scheduledDate: "2026-10-01",
    price: 125000,
    pricingType: "PerVisit",
    addressDetail: "Jalan Melati nomor 12 RT 01 RW 02",
    villageId: "3374011003",
    reviewed: false,
  },
  {
    id: "order-2",
    providerId: profile.id,
    providerName: "Provider QA",
    status: "Completed",
    scheduledDate: "2026-09-01",
    price: 250000,
    pricingType: "PerVisit",
    addressDetail: "Jalan Mawar nomor 4",
    villageId: "3374011003",
    reviewed: true,
  },
];

async function mockApi(page: import("@playwright/test").Page) {
  await page.route("**/api/auth/provider/login", (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(session) }),
  );
  await page.route("**/api/provider/profile", (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(profile) }),
  );
  await page.route("**/api/provider/application/status", (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(application) }),
  );
  await page.route("**/api/provider/orders", (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(orders) }),
  );
}

async function login(page: import("@playwright/test").Page) {
  await page.goto("/provider/login");
  await page.getByLabel("Email Provider").fill("provider@example.test");
  await page.getByLabel("Kata sandi").fill("Provider-password-long-42!");
  await page.getByRole("button", { name: "Masuk sebagai Provider" }).click();
  await expect(page).toHaveURL(/\/provider\/dashboard/);
}

for (const width of [375, 393]) {
  test(`provider panel tabs fit at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await mockApi(page);
    await login(page);
    await expect(page.getByRole("heading", { name: "Provider QA" })).toBeVisible();

    const tabs = page.getByRole("navigation", { name: "Tab panel Provider" }).getByRole("link");
    await expect(tabs).toHaveCount(4);
    await expect(page.getByRole("link", { name: "Ringkasan" })).toHaveAttribute("aria-current", "page");

    await expect(page.getByText("Progres aplikasi")).toBeVisible();
    await expect(page.getByText("3/4", { exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: /Lanjutkan: Layanan/ })).toBeVisible();
    await expect(page.getByRole("link", { name: "Layanan Sebagian" })).toBeVisible();

    await expect(page.locator(".provider-grid")).toHaveCount(0);
    await expect(page.getByLabel("Kata sandi saat ini")).toBeHidden();

    await page.getByRole("link", { name: "Ketersediaan" }).click();
    await expect(page).toHaveURL(/tab=ketersediaan/);
    const chips = page.locator(".availability-chips .chip");
    await expect(chips).toHaveCount(7);
    const chipTexts = await chips.allTextContents();
    expect(chipTexts.map((t) => t.trim().split("\n")[0])).toEqual([
      "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu",
    ]);
    await expect(chips.first()).toHaveClass(/active/);
    await expect(chips.last()).not.toHaveClass(/active/);

    await page.getByRole("link", { name: "Pesanan" }).click();
    await expect(page).toHaveURL(/tab=pesanan/);
    await expect(page.locator(".provider-grid .card").first()).toBeVisible();
    const orderColumns = await page.evaluate(() => {
      const grid = document.querySelector(".provider-grid");
      return grid ? getComputedStyle(grid).gridTemplateColumns.split(" ").length : 0;
    });
    expect(orderColumns).toBe(1);

    await page.getByRole("link", { name: "Pengaturan" }).click();
    await expect(page).toHaveURL(/tab=pengaturan/);
    const current = page.getByLabel("Kata sandi saat ini");
    const next = page.getByLabel("Kata sandi baru");
    await expect(current).toHaveAttribute("type", "password");
    await expect(current).toHaveAttribute("autocomplete", "current-password");
    await expect(next).toHaveAttribute("type", "password");
    await expect(next).toHaveAttribute("autocomplete", "new-password");

    const scrollWidth = await page.evaluate(
      () => document.documentElement.scrollWidth,
    );
    expect(scrollWidth).toBe(width);
  });

  test(`provider deep links ?tab= and ?step= work at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await mockApi(page);
    await login(page);

    await page.getByRole("link", { name: "Pesanan" }).click();
    await expect(page).toHaveURL(/tab=pesanan/);
    await expect(page.locator(".provider-grid .card").first()).toBeVisible();
    await expect(page.getByRole("link", { name: "Pesanan" })).toHaveAttribute("aria-current", "page");

    await page.goBack();
    await expect(page.locator(".panel-tabs .active")).toHaveText("Ringkasan");

    await page.getByRole("link", { name: "Lanjutkan: Layanan" }).click();
    await expect(page).toHaveURL(/\/provider\/onboarding\?step=profile$/);
    await expect(page.getByRole("tab", { name: /Layanan/ })).toHaveAttribute("aria-selected", "true");

    await page.getByRole("tab", { name: /Dokumen/ }).click();
    await expect(page).toHaveURL(/step=documents/);
    await expect(page.getByRole("tab", { name: /Dokumen/ })).toHaveAttribute("aria-selected", "true");

    const scrollWidth = await page.evaluate(
      () => document.documentElement.scrollWidth,
    );
    expect(scrollWidth).toBe(width);
  });
}
