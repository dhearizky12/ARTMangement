import { Skeleton } from "./ui";

export function ProviderCardSkeleton() {
  return (
    <div className="card provider-card" aria-hidden="true">
      <div className="provider-heading">
        <Skeleton circle width={48} height={48} />
        <div className="provider-heading-text">
          <Skeleton width="65%" height={18} />
          <Skeleton width={96} height={22} style={{ marginTop: 6 }} />
        </div>
      </div>
      <div className="tags">
        <Skeleton width={84} height={24} />
        <Skeleton width={64} height={24} />
      </div>
      <Skeleton width="80%" height={14} style={{ marginTop: 8 }} />
      <Skeleton width="45%" height={14} style={{ marginTop: 8 }} />
      <Skeleton width="55%" height={16} style={{ marginTop: 8 }} />
    </div>
  );
}

export function ProviderGridSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="provider-grid" aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <ProviderCardSkeleton key={i} />
      ))}
    </div>
  );
}

export function CategoryGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="category-grid" aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <div className="card category-link" key={i}>
          <Skeleton circle width={40} height={40} />
          <Skeleton width="70%" height={14} />
        </div>
      ))}
    </div>
  );
}

export function OrderListSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div aria-hidden="true">
      {Array.from({ length: count }, (_, i) => (
        <div className="card order-card" key={i}>
          <Skeleton width="55%" height={20} />
          <Skeleton width="40%" height={14} style={{ marginTop: 10 }} />
          <Skeleton width="30%" height={16} style={{ marginTop: 10 }} />
          <Skeleton width="75%" height={14} style={{ marginTop: 10 }} />
        </div>
      ))}
    </div>
  );
}

export function ProviderDetailSkeleton() {
  return (
    <div aria-hidden="true">
      <section className="provider-detail-hero">
        <Skeleton circle width={72} height={72} />
        <div style={{ flex: 1 }}>
          <Skeleton width={110} height={24} />
          <Skeleton width="60%" height={26} style={{ marginTop: 10 }} />
          <Skeleton width="45%" height={16} style={{ marginTop: 10 }} />
          <Skeleton width="50%" height={16} style={{ marginTop: 8 }} />
        </div>
      </section>
      <div className="stats-grid">
        {Array.from({ length: 3 }, (_, i) => (
          <div className="card" key={i}>
            <Skeleton width="60%" height={22} />
            <Skeleton width="80%" height={14} style={{ marginTop: 8 }} />
          </div>
        ))}
      </div>
      <div className="card">
        <Skeleton width="40%" height={20} />
        <Skeleton width="95%" height={14} style={{ marginTop: 12 }} />
        <Skeleton width="85%" height={14} style={{ marginTop: 8 }} />
        <Skeleton width="70%" height={14} style={{ marginTop: 8 }} />
      </div>
    </div>
  );
}
