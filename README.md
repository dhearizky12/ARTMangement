# Bantu-Bantu

Marketplace penyedia jasa dengan React + TypeScript dan ASP.NET Core 10 + EF Core + PostgreSQL. `frontend/` dan `backend/` adalah project independen yang berkomunikasi melalui REST API.

- Login pengguna via Google Identity Services, JWT RS256, dan refresh cookie httpOnly.
- Login admin email/password terpisah, dengan guard role di kedua sisi.
- Wizard profil personal → alamat → dokumen, dilanjutkan berdasarkan status database setiap reload.
- Guard server menolak akses layanan untuk User yang belum melengkapi profil.
- Kategori jasa diambil dari API/database, termasuk ART, Driver, dan kategori lain yang dapat ditambahkan tanpa perubahan frontend.
- UI neo-brutalism mobile-first dengan tokens, komponen reusable, dan bottom navigation.

Jalankan dengan [Docker Compose](docs/DOCKER.md), atau ikuti [backend](backend/README.md) dan [frontend](frontend/README.md). Setelah memperbarui source, `docker compose up --build -d` menerapkan migrasi dan memasang volume dokumen privat.

Lihat [kontrak wizard dan kategori](docs/PROFILE-AND-CATEGORIES.md). Kelengkapan profil berbeda dari verifikasi identitas: file yang diterima berstatus `Pending`. Direktori penyedia, pemesanan, dan proses review dokumen oleh admin belum termasuk fitur saat ini.
