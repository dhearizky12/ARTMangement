import { authApi } from "./authApi";
export interface ServiceCategory {
  id: string;
  slug: string;
  name: string;
  description: string;
  iconKey: string;
  isFeatured: boolean;
}
export const categoryApi = {
  list: () => authApi.publicGet<ServiceCategory[]>("/api/service-categories"),
};
