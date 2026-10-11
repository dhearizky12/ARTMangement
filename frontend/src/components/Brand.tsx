import { Asterisk } from "lucide-react";
import { Link } from "react-router-dom";
export function Brand({
  to = "/dashboard",
}: {
  to?: string;
}) {
  return (
    <Link to={to} className="brand" aria-label="Bantu-Bantu beranda">
      <Asterisk aria-hidden="true" strokeWidth={3} />
      <span>bantu-bantu.</span>
    </Link>
  );
}