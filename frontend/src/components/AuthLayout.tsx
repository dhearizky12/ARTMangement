import type { ReactNode } from "react";
import { ArrowUpRight, HandHeart, CarFront, House } from "lucide-react";
import { Brand } from "./Brand";
import { Badge } from "./ui";
export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main className="auth-layout">
      <section className="auth-story">
        <Brand />
        <div>
          <Badge tone="accent" sticker>
            BANTUAN TEPAT, HIDUP LEBIH RINGAN.
          </Badge>
          <h1>
            Urusan banyak?
            <br />
            Bagi bebannya.
          </h1>
          <p>
            Temukan beragam jasa untuk rumah, perjalanan, dan keseharianmu.
            Semuanya di Bantu-Bantu.
          </p>
          <div className="auth-art" aria-hidden="true">
            <House />
            <CarFront />
            <HandHeart />
            <ArrowUpRight />
          </div>
        </div>
        <p className="caption">Satu tempat. Banyak bantuan.</p>
      </section>
      <section className="auth-panel">
        {children}
        <p className="caption muted">
          © {new Date().getFullYear()} Bantu-Bantu
        </p>
      </section>
    </main>
  );
}
