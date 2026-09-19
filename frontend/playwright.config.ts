import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e",
  workers: 1,
  timeout: 90000,
  use: {
    baseURL: process.env.QA_FRONTEND_URL || "http://localhost:5191",
    headless: true,
  },
  reporter: "list",
});
