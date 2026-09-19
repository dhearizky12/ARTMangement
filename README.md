# Pembaruan marketplace

Arsitektur hybrid agency, role baru, browsing publik, dan wizard Provider kini menggantikan alur Customer lama. Panduan aktif: [MARKETPLACE.md](docs/MARKETPLACE.md). Bagian dokumentasi lama di bawah mungkin masih menjelaskan alur sebelum migrasi.

# Bantu-Bantu

Marketplace penyedia jasa dengan React + TypeScript dan ASP.NET Core 10 + EF Core + Neon/Lakebase Postgres. `frontend/` dan `backend/` adalah project independen yang berkomunikasi melalui REST API.

- Login pengguna via Google Identity Services, JWT RS256, dan refresh cookie httpOnly.
- Login Platform Admin dan Agency Admin memakai email/password terpisah; JWT Agency Admin membawa `agencyId`.
- Customer tidak diwajibkan mengikuti wizard. Provider didaftarkan dan diverifikasi oleh admin melalui personal → alamat → KTP/KK → layanan → checklist.
- Guest dapat menjelajah kategori, pencarian, dan detail provider. Booking dan riwayat pesanan memakai soft gate login.
- Kategori jasa, provider, konten trust, pesanan, dan ulasan berasal dari API/database.
- UI neo-brutalism mobile-first dengan tokens, komponen reusable, dan bottom navigation.

Jalankan dengan [Docker Compose](docs/DOCKER.md), atau ikuti [backend](backend/README.md) dan [frontend](frontend/README.md). Untuk deployment API ke Render dengan Cloudflare R2, ikuti [panduan Render](docs/RENDER.md). Setelah memperbarui source, `docker compose up --build -d` menerapkan migrasi ke branch Neon yang dikonfigurasi dan memasang volume dokumen privat.

Lihat [panduan marketplace](docs/MARKETPLACE.md) untuk model domain, scope agency, endpoint, migrasi, seeding wilayah, dan QA.
