import { Asterisk } from "lucide-react";
import { Link } from "react-router-dom";
export function Brand() {
  return (
    <Link to="/dashboard" className="brand" aria-label="Bantu-Bantu beranda">
      <Asterisk aria-hidden="true" strokeWidth={3} />
      <span>bantu-bantu.</span>
    </Link>
  );
}
