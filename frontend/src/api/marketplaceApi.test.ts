import { afterEach, describe, expect, it, vi } from "vitest";
import { adminApi } from "./marketplaceApi";
import { authApi } from "./authApi";
afterEach(() => vi.restoreAllMocks());
describe("adminApi.dashboardStats", () => {
  it("reads counts from the dedicated stats endpoint", async () => {
    const get = vi.spyOn(authApi, "get").mockResolvedValue({
      pendingVerification: 3,
      pendingOrders: 5,
      providers: 214,
      agencies: 2,
    });
    const stats = await adminApi.dashboardStats();
    expect(get).toHaveBeenCalledWith("/api/admin/dashboard/stats");
    expect(stats).toEqual({
      pendingVerification: 3,
      pendingOrders: 5,
      providers: 214,
      agencies: 2,
    });
  });
});
