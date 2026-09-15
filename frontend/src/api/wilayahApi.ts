import { authApi } from "./authApi";
export interface VillageResult {
  villageId: string;
  villageName: string;
  villageType: string;
  districtName: string;
  regencyName: string;
  provinceName: string;
  displayLabel: string;
}

export const wilayahApi = {
  search: (query: string) =>
    authApi.get<VillageResult[]>(
      `/api/wilayah/search?q=${encodeURIComponent(query.trim())}&limit=10`,
    ),
};
