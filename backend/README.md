# Pembaruan marketplace

Arsitektur hybrid agency, role baru, browsing publik, dan wizard Provider kini menggantikan alur Customer lama. Panduan aktif: [MARKETPLACE.md](../docs/MARKETPLACE.md). Bagian dokumentasi lama di bawah mungkin masih menjelaskan alur sebelum migrasi.

# Bantu-Bantu API

.NET SDK 10, Neon/Lakebase Postgres, OpenSSL. Jalankan seluruh perintah dari folder `backend`.

## Struktur

- Domain: entity dan enum role.
- Application: kontrak repository, DTO, dan AuthService.
- Infrastructure: EF Core, repository, verifier Google, password hasher, RSA/JWT.
- API: controllers, DI, validasi konfigurasi, CORS, rate limit, dan middleware.
- Tests: integrasi HTTP dengan PostgreSQL nyata, migrasi akun lama, scope agency, booking, review, dan pemeriksaan token.

## Konfigurasi lokal

```sh
dotnet restore
dotnet tool restore
mkdir -p secrets
chmod 700 secrets
# Jalankan satu kali; jangan menimpa key yang sudah dipakai.
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out secrets/private.pem
openssl pkey -in secrets/private.pem -pubout -out secrets/public.pem
chmod 600 secrets/private.pem
```

Isi konfigurasi via `dotnet user-secrets set 'Key' 'value' --project BantuBantu.Api`, atau environment variables menggunakan `__` sebagai pengganti `:`. Gunakan path RSA absolut. Nilai di bawah merupakan contoh lokal, bukan konfigurasi bawaan aplikasi.

| Key user-secrets | Environment variable | Isi / contoh lokal |
|---|---|---|
| ConnectionStrings:Default | ConnectionStrings__Default | Pooled Neon `postgresql://...` URL untuk API |
| — | DATABASE_URL | Pooled Neon connection URL (fallback di luar Compose) |
| — | DATABASE_URL_UNPOOLED | Direct Neon URL untuk migrasi/seed (diprioritaskan EF CLI) |
| Google:ClientId | Google__ClientId | Web Client ID dari Google Cloud |
| Google:ClientSecret | Google__ClientSecret | Reserved untuk OAuth callback server; flow GIS saat ini tidak membacanya |
| Jwt:PrivateKeyPath | Jwt__PrivateKeyPath | Path absolut `secrets/private.pem` |
| Jwt:PublicKeyPath | Jwt__PublicKeyPath | Path absolut `secrets/public.pem` |
| Jwt:PrivateKeyBase64 | Jwt__PrivateKeyBase64 | Base64 dari PEM private key; gunakan ini pada Render |
| Jwt:PublicKeyBase64 | Jwt__PublicKeyBase64 | Base64 dari PEM public key; gunakan ini pada Render |
| Jwt:KeyId | Jwt__KeyId | Identitas key, misalnya `local-v1` |
| Jwt:Issuer | Jwt__Issuer | Misalnya `http://localhost:5080` |
| Jwt:Audience | Jwt__Audience | Misalnya `bantu-bantu-web` |
| Jwt:AccessMinutes | Jwt__AccessMinutes | `15` |
| Jwt:RefreshDays | Jwt__RefreshDays | `7` |
| Frontend:Origin | Frontend__Origin | `http://localhost:5173` tanpa trailing slash |
| Frontend:Origins | Frontend__Origins | Origin tambahan exact, dipisahkan koma/semicolon |
| Frontend:OriginPatterns | Frontend__OriginPatterns | Preview HTTPS pattern, misalnya `https://*.pages.dev` |
| Auth:CookieSecure | Auth__CookieSecure | `false` hanya Development HTTP; `true` di production |
| Auth:CookieSameSite | Auth__CookieSameSite | `Lax` untuk localhost/same-site; `None` + Secure untuk cross-site |
| Storage:RootPath | Storage__RootPath | Path absolut direktori privat dokumen, di luar web root |
| Storage:Provider | Storage__Provider | `Local` untuk development atau `S3` untuk Cloudflare R2 |
| Storage:S3:ServiceUrl | Storage__S3__ServiceUrl | Endpoint R2 S3 API (`https://<account>.r2.cloudflarestorage.com`) |
| Storage:S3:Region | Storage__S3__Region | Region S3, biasanya `auto` untuk R2 |
| Storage:S3:AccessKey | Storage__S3__AccessKey | R2 API token access key |
| Storage:S3:SecretKey | Storage__S3__SecretKey | R2 API token secret key |
| Storage:S3:Bucket | Storage__S3__Bucket | Nama bucket R2 privat |
| Storage:S3:KeyPrefix | Storage__S3__KeyPrefix | Prefix object, misalnya `provider-documents` |
| AllowedHosts | AllowedHosts | Host API yang diizinkan; misalnya `localhost` |
| — | ASPNETCORE_ENVIRONMENT | `Development` untuk membaca user-secrets |
| — | ASPNETCORE_URLS | Misalnya `http://localhost:5080` |
| AdminSeed:Email | AdminSeed__Email | Hanya untuk perintah seed admin |
| AdminSeed:Password | AdminSeed__Password | Hanya seed; minimal 14 karakter |

Template key kosong: `BantuBantu.Api/appsettings.Example.json`. File ini tidak otomatis dimuat. Alternatif lokal: salin menjadi `appsettings.Development.json` yang sudah diabaikan git. Jangan gunakan `VITE_` untuk secret; nilai tersebut terlihat di browser.

`RsaKeys` menerima key sebagai path file PEM, raw PEM, atau base64 PEM. Gunakan `Jwt__PrivateKeyBase64` dan `Jwt__PublicKeyBase64` pada Render karena filesystem container bersifat ephemeral; key harus merupakan pasangan yang sama dan minimal 2048 bit.

## Database dan migrasi

Neon menyediakan dua URL untuk branch yang sama. Gunakan URL pooled (`DATABASE_URL`) untuk API dan URL direct/unpooled (`DATABASE_URL_UNPOOLED`) untuk EF migrations. `DatabaseConnectionStringResolver` mengubah URI `postgres://`/`postgresql://` menjadi format Npgsql dan mempertahankan `sslmode=require`. Lihat [panduan Neon](../docs/NEON.md).

```sh
# Isi connection string lewat environment lokal/secret manager.
export DATABASE_URL='postgresql://USER:PASSWORD@ep-example-pooler.REGION.aws.neon.tech/DB?sslmode=require&channel_binding=require'
export DATABASE_URL_UNPOOLED='postgresql://USER:PASSWORD@ep-example.REGION.aws.neon.tech/DB?sslmode=require&channel_binding=require'
dotnet ef database update --project BantuBantu.Infrastructure --startup-project BantuBantu.Api
```

Migrations memakai direct URL karena koneksi pooled Neon berjalan melalui PgBouncer dan tidak cocok untuk operasi yang membutuhkan session state. Untuk branch production, uji migration di branch terpisah terlebih dahulu bila tersedia.

Migrasi `InitialAuth`, `FixUserProfileRelationship`, dan `ProfileWizardAndServiceCategories` disertakan. Jangan gunakan `EnsureCreated`. Untuk perubahan entity berikutnya:

```sh
dotnet ef migrations add NamaPerubahan --project BantuBantu.Infrastructure --startup-project BantuBantu.Api
```

## Admin dan menjalankan API

Isi `AdminSeed:Email` dan `AdminSeed:Password` via user-secrets, lalu:

```sh
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5080
dotnet run --project BantuBantu.Api -- --seed-admin
dotnet user-secrets remove 'AdminSeed:Password' --project BantuBantu.Api
dotnet run --project BantuBantu.Api
```

Seed adalah perintah eksplisit sekali jalan, bukan endpoint publik atau seed otomatis saat startup. Email yang sudah ada ditolak, tidak dipromosikan. Password disimpan memakai ASP.NET Identity PasswordHasher (PBKDF2 dengan salt individual), bukan plaintext.

## REST API

Semua POST `/api/auth/*` wajib membawa `Origin` yang persis sama dengan `Frontend:Origin`, termasuk curl/Postman. Ini melindungi login/refresh/logout dari CSRF. Browser mengirim header Origin otomatis. Semua route autentikasi dibatasi 20 request/menit per alamat IP. Untuk reverse proxy, konfigurasikan trusted proxy dan forwarded headers secara eksplisit sebelum memakai IP klien sebagai partition.

| Method | Path | Body / akses |
|---|---|---|
| POST | `/api/auth/google` | `{ "credential": "GOOGLE_ID_TOKEN" }` |
| POST | `/api/auth/admin/login` | `{ "email": "...", "password": "..." }` |
| POST | `/api/auth/refresh` | `{}` + cookie refresh |
| POST | `/api/auth/logout` | `{}` + cookie refresh |
| GET | `/api/auth/me` | Bearer access token |
| GET | `/api/user/dashboard` | Role User + profil lengkap |
| GET | `/api/admin/dashboard` | Role Admin |
| GET | `/.well-known/jwks.json` | Public RSA key saja |
| GET | `/health` | Liveness, tidak memeriksa koneksi database |

Respons login/refresh: `{ accessToken, expiresAt, user: { id, email, fullName, pictureUrl, role, profileCompleted, profileStep } }`. Refresh token hanya di cookie httpOnly, path `/api/auth`. Database menyimpan SHA-256 hash; refresh lama ditolak setelah dipakai. Konsumsi refresh memakai update atomik PostgreSQL sehingga satu token tidak bisa dipakai dua kali bersamaan. Jika penerbitan sesi baru gagal setelah konsumsi, pengguna perlu login lagi.

JWT memuat `sub`, `email`, `role`, `profileCompleted`, `jti`, `exp`, `nbf`, `iss`, `aud`. Validator membatasi algoritma ke RS256 dan memeriksa public key, issuer, audience, expiry. Logout mencabut refresh token sesi ini; access token yang sudah terbit tetap valid hingga kedaluwarsa. Sesi perangkat lain tidak dicabut. Key rotation saat ini satu key aktif; penggantian key membatalkan access token lama.

Google verifier memvalidasi signature, audience, issuer, expiry melalui Google.Apis.Auth dan mewajibkan email terverifikasi. Akun dicocokkan dengan `sub`, role selalu User; kesamaan email dengan akun lain ditolak dan tidak otomatis ditautkan. Data identitas Google awal disimpan sebagai JSONB beserta email/nama/foto/sub. Tidak ada redirect/callback server karena GIS popup memberikan credential langsung ke frontend.

Gunakan HTTPS untuk frontend dan API di production. Cookie cross-site bergantung kebijakan third-party cookie browser; deployment pada subdomain same-site lebih andal. Private key dibaca dari path konfigurasi; mount dari secret manager dengan permission terbatas.

## Wizard dan kategori

Endpoint profil, format upload, status, dan cara menambahkan kategori dijelaskan di [kontrak wizard dan kategori](../docs/PROFILE-AND-CATEGORIES.md). Untuk local non-Docker, isi `Storage:RootPath` via user-secrets dengan path absolut di luar direktori publik. Compose mengaturnya otomatis dan memasang volume `document_data`.

## Pengujian

Gunakan database **test kosong yang terpisah** untuk setiap run; test menerapkan migrasi dan menambah fixture admin/user. Jangan menunjuk database development/production.

```sh
export BANTUBANTU_TEST_DB='Host=YOUR_HOST;Database=YOUR_EMPTY_TEST_DB;Username=YOUR_USER;Password=YOUR_PASSWORD'
dotnet test BantuBantu.sln
```

Tes integrasi mencakup login admin, password salah, JWT RS256, audience/issuer salah, token rusak, batas role, refresh rotation/replay, logout, origin asing, registrasi Google, login ulang tanpa duplikasi, dan penolakan penggabungan email admin. Lifecycle dilanjutkan dengan wizard, validasi DTO, konflik submit bersamaan, guard berbasis database, validasi/re-encoding upload, penyimpanan privat, dan kategori dinamis. Google verifier diganti hanya dalam test HTTP; tes lain memanggil verifier asli untuk memastikan token palsu ditolak. Pengujian ini tidak mengklaim login interaktif Google nyata sudah berhasil.

## Docker

Dockerfile tersedia di folder ini. Untuk menjalankan frontend dan API dengan Neon, lihat [panduan Docker Compose](../docs/DOCKER.md).

Referensi wilayah dan langkah pembaruan/seed: lihat [WILAYAH.md](../docs/WILAYAH.md). Docker menyediakan seed sebagai job eksplisit profile `tools`.
