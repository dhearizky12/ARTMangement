import {
  BriefcaseBusiness,
  CarFront,
  House,
  Sparkles,
  Sprout,
  type LucideIcon,
} from "lucide-react";
const icons: Record<string, LucideIcon> = {
  house: House,
  car: CarFront,
  sparkles: Sparkles,
  sprout: Sprout,
  briefcase: BriefcaseBusiness,
};
export function CategoryIcon({ name }: { name: string }) {
  const Icon = icons[name] || BriefcaseBusiness;
  return <Icon aria-hidden="true" />;
}
