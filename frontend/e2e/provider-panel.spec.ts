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

for (const width of [375, 400]) {
  test(`provider login and panel fit at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.route("**/api/auth/provider/login", (route) =>
      route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(session) }),
    );
    await page.route("**/api/provider/profile", (route) =>
      route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(profile) }),
    );
    await page.route("**/api/provider/orders", (route) =>
      route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(orders) }),
    );

    await page.goto("/provider/login");
    await page.getByLabel("Email Provider").fill("provider@example.test");
    await page.getByLabel("Kata sandi").fill("Provider-password-long-42!");
    await page.getByRole("button", { name: "Masuk sebagai Provider" }).click();
    await expect(page).toHaveURL(/\/provider\/dashboard$/);
    await expect(page.getByRole("heading", { name: "Provider QA" })).toBeVisible();

    const metrics = await page.evaluate(() => {
      const availability = document.querySelector(".availability");
      const orderGrid = document.querySelector(".provider-grid");
      return {
        scrollWidth: document.documentElement.scrollWidth,
        viewportWidth: window.innerWidth,
        availabilityColumns: availability
          ? getComputedStyle(availability).gridTemplateColumns.split(" ").length
          : 0,
        orderColumns: orderGrid
          ? getComputedStyle(orderGrid).gridTemplateColumns.split(" ").length
          : 0,
      };
    });

    expect(metrics.scrollWidth).toBe(metrics.viewportWidth);
    expect(metrics.availabilityColumns).toBe(2);
    expect(metrics.orderColumns).toBe(1);
  });
}
