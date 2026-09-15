# Bantu-Bantu frontend

React + TypeScript + Vite. Node.js 22.12+ atau 24. Dependency dan build berdiri sendiri dari backend.

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

## Demo tahap 1–6

1. Jalankan migrasi, seed admin, API, dan frontend.
2. `/login` → klik Google → pilih akun penguji → pengguna baru masuk wizard. Selesaikan personal, alamat, dan upload JPG/PNG untuk masuk homepage yang terlindungi.
3. Periksa Network: POST Google mengembalikan access token; refresh token hanya muncul sebagai Set-Cookie httpOnly. Decode header JWT untuk melihat `alg: RS256`; public key tersedia di JWKS API.
4. Reload halaman: sesi dipulihkan via refresh cookie tanpa login Google lagi.
5. Keluar → `/admin/login` → masukkan akun hasil seed → dashboard admin.
6. Buka route berbeda role: frontend mengarahkan kembali; request API langsung dengan token berbeda role menghasilkan 403. Tanpa token menghasilkan 401.
7. Logout mencabut refresh; refresh berikutnya menghasilkan 401.

Pengguna dengan `profileCompleted=false` diarahkan ke wizard sesuai `GET /api/profile/status`. Setiap submit berhasil diikuti GET status, sehingga reload melanjutkan tahap terakhir. User yang sudah lengkap diarahkan ke homepage. Bottom navigation membuka Beranda, Layanan, dan Akun; kategori dan layanan pilihan diambil dari `/api/service-categories`.

## Build dan test

```sh
npm run build
npm test
```

Deploy folder `dist` ke static host dengan SPA fallback ke `index.html`. Variabel `VITE_` dibaca saat build; lakukan rebuild setelah mengubah konfigurasi. Frontend membutuhkan HTTPS di production. Konfigurasikan origin frontend yang sama persis di CORS backend.

## Docker

Dockerfile tersedia di folder ini. Untuk menjalankan frontend, API, dan PostgreSQL bersama, lihat [panduan Docker Compose](../docs/DOCKER.md).

## Design system

Tokens ada di `src/styles/tokens.css`; komponen Button, Card, Input, Select, Textarea, dan Badge ada di `src/components/ui`. Ikon menggunakan Lucide. Base style ditujukan untuk layar 360px, dengan breakpoint 420px dan 768px. Shadow kategori/CTA memakai offset tanpa blur; daftar layanan pilihan memakai card border tanpa shadow berulang.
