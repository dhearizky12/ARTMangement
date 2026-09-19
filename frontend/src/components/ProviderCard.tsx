import { Link } from "react-router-dom";
import { Badge, Card } from "./ui";
import { type Provider, money, priceUnit } from "../api/marketplaceApi";
import { MapPin, Star, ArrowUpRight } from "lucide-react";
export function ProviderCard({ provider: p }: { provider: Provider }) {
  return (
    <Card className="provider-card">
      <div className="provider-heading">
        <span className="avatar" aria-hidden="true">
          {p.fullName
            .split(" ")
            .slice(0, 2)
            .map((n) => n[0])
            .join("")}
        </span>
        <div>
          <h3>
            <Link to={`/providers/${p.id}`}>{p.fullName}</Link>
          </h3>
          <p className="muted">{p.categories.map((c) => c.name).join(" · ")}</p>
        </div>
      </div>
      <Badge tone="success">Terverifikasi</Badge>
      <p>
        <Star size={16} aria-hidden="true" />{" "}
        {p.rating === null
          ? "Belum ada ulasan"
          : `${p.rating.toFixed(1)} (${p.reviewCount} ulasan)`}
      </p>
      <p>
        {p.yearsOfExperience} tahun pengalaman · {p.age} tahun
      </p>
      <p className="muted">
        <MapPin size={16} aria-hidden="true" /> {p.location}
      </p>
      <div className="card-footer">
        <strong>
          {money(p.price)}
          <small> /{priceUnit(p.pricingType)}</small>
        </strong>
        <Link
          className="icon-link"
          aria-label={`Lihat ${p.fullName}`}
          to={`/providers/${p.id}`}
        >
          <ArrowUpRight />
        </Link>
      </div>
    </Card>
  );
}
