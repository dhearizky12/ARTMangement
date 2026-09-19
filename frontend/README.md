# Pembaruan marketplace

Arsitektur hybrid agency, role baru, browsing publik, dan wizard Provider kini menggantikan alur Customer lama. Panduan aktif: [MARKETPLACE.md](../docs/MARKETPLACE.md). Bagian dokumentasi lama di bawah mungkin masih menjelaskan alur sebelum migrasi.

# Bantu-Bantu frontend

React + TypeScript + Vite. Node.js 22.12+ atau 24. Dependency dan build berdiri sendiri dari backend. Halaman publik dapat dibuka tanpa login; area admin dan aksi booking memakai guard yang sesuai role.

```sh
npm ci
cp .env.example .env
```

Isi `VITE_API_BASE_URL` dengan origin API (contoh lokal `http://localhost:5080`) dan `VITE_GOOGLE_CLIENT_ID` dengan Web Client ID yang sama dengan `Google:ClientId` di backend. Lalu:

```sh
npm run dev -- --host localhost --port 5173 --strictPort
```

Konfigurasikan OAuth consent screen di Google Cloud, buat OAuth client tipe Web application, dan tambahkan `http://localhost:5173` sebagai **Authorized JavaScript origin**. Jika aplikasi Google masih Testing, tambahkan akun penguji. Alur popup credential ini tidak memerlukan Client Secret maupun redirect callback backend. Jangan campur `localhost` dan `127.0.0.1` antara FE/BE karena cookie dan origin berbeda.

Frontend menyimpan access token hanya di memori; fetch memakai `credentials: include`. Refresh pada startup, sebelum expiry, dan setelah HTTP 401. Request refresh bersamaan dalam satu tab digabung menjadi satu promise. Kegagalan refresh menghapus sesi. Tidak ada JWT/refresh token di localStorage/sessionStorage. Guard route mengikuti role dari respons server; backend tetap menjadi sumber otorisasi yang sebenarnya.

## Demo marketplace

1. Jalankan migrasi, seed wilayah, API, dan frontend.
2. Guest dapat membuka `/`, `/search`, `/providers/:id`, dan `/trust`; kategori, provider, rating, harga, dan konten berasal dari API.
3. Klik `Masuk & Pesan` dari detail provider. Setelah Google login, Customer kembali ke detail lalu mengisi tanggal serta lokasi layanan.
4. `/admin/login` membuka area admin. Platform Admin membuat agency/provider; Agency Admin hanya melihat roster agency yang ada di claim `agencyId`.
5. `/admin/providers/:id` menjalankan personal, alamat, KTP/KK, layanan, dan checklist verifikasi. Status tahap disimpan server dan aman saat reload.
6. Refresh token hanya muncul sebagai cookie httpOnly; JWT memakai RS256 dan public key tersedia di JWKS API.

Bottom navigation membuka Beranda, Cari, Pesanan, dan Akun. Customer tidak memiliki wizard onboarding wajib; alamat layanan tersimpan pada Order.

## Build dan test

```sh
npm run build
npm test
```

Deploy folder `dist` ke static host dengan SPA fallback ke `index.html`. Variabel `VITE_` dibaca saat build; lakukan rebuild setelah mengubah konfigurasi. Frontend membutuhkan HTTPS di production. Konfigurasikan origin frontend yang sama persis di CORS backend.

## Docker

Dockerfile tersedia di folder ini. Untuk menjalankan frontend, API, dan Neon bersama, lihat [panduan Docker Compose](../docs/DOCKER.md).

## Design system

Tokens ada di `src/styles/tokens.css`; komponen Button, Card, Input, Select, Textarea, dan Badge ada di `src/components/ui`. Ikon menggunakan Lucide. Base style ditujukan untuk layar 360px, dengan breakpoint 420px dan 768px. Shadow kategori/CTA memakai offset tanpa blur; daftar layanan pilihan memakai card border tanpa shadow berulang.
