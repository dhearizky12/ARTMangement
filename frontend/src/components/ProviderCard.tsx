import { Link } from "react-router-dom";
import { Badge } from "./ui";
import { type Provider, money, priceUnit } from "../api/marketplaceApi";
import { MapPin, Star } from "lucide-react";

export function ProviderCard({ provider: p }: { provider: Provider }) {
  const initials = p.fullName
    .split(" ")
    .slice(0, 2)
    .map((n) => n[0])
    .join("");
  const experience =
    p.yearsOfExperience > 0
      ? `${p.yearsOfExperience} tahun pengalaman`
      : "Baru bergabung";
  const age = p.age > 0 ? `Usia ${p.age} tahun` : null;
  return (
    <Link className="card provider-card" to={`/providers/${p.id}`}>
      <div className="provider-heading">
        <span className="avatar" aria-hidden="true">
          {initials}
        </span>
        <div className="provider-heading-text">
          <h3>{p.fullName}</h3>
          <Badge tone="success">Terverifikasi</Badge>
        </div>
      </div>
      {p.categories.length > 0 && (
        <div className="tags">
          {p.categories.map((c) => (
            <Badge key={c.id}>{c.name}</Badge>
          ))}
        </div>
      )}
      <p className="provider-meta">
        <span>{experience}</span>
        {age && <span>{age}</span>}
        {p.location && (
          <span>
            <MapPin size={16} aria-hidden="true" />
            {p.location}
          </span>
        )}
      </p>
      <p className="provider-rating">
        <Star size={18} aria-hidden="true" />
        {p.rating === null ? (
          <span className="muted">Belum ada ulasan</span>
        ) : (
          <span>
            <strong>{p.rating.toFixed(1)}</strong> ({p.reviewCount} ulasan)
          </span>
        )}
      </p>
      <p className="provider-price">
        {p.price > 0 ? (
          <>
            Mulai dari <strong>{money(p.price)}</strong>
            <small>/{priceUnit(p.pricingType)}</small>
          </>
        ) : (
          <span className="muted">Tarif belum diatur</span>
        )}
      </p>
    </Link>
  );
}
