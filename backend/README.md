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
| Frontend:OriginPatterns | Frontend__OriginPatterns | Kosong untuk Firebase production; isi hanya jika preview channels dipakai |
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
| SeedAccounts:SharedPassword | SeedAccounts__SharedPassword | Satu password bersama Platform Admin, Agency Admin, dan Provider; minimal 14 karakter |
| SeedAccounts:Customer:GoogleSubject | SeedAccounts__Customer__GoogleSubject | Claim `sub` Google untuk Customer demo |

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

Seed adalah perintah eksplisit sekali jalan, bukan endpoint publik atau seed otomatis saat startup. Perintah `--seed-admin` menolak email yang sudah ada dan tidak mempromosikannya. Password disimpan memakai ASP.NET Identity PasswordHasher (PBKDF2 dengan salt individual), bukan plaintext.

Untuk membuat satu akun demo per role, isi `SeedAccounts:SharedPassword` dan `SeedAccounts:Customer:GoogleSubject` melalui user-secrets atau environment variables, lalu jalankan:

```sh
dotnet run --project BantuBantu.Api -- --seed-accounts
```

Seeder ini idempotent: akun yang sudah cocok dilewati dan konflik role/email dihentikan dengan error. Seeder memakai email dan nama demo statis yang berbeda untuk Platform Admin, Agency Admin, Provider, dan Customer. `SeedAccounts:SharedPassword` menjadi password yang sama untuk tiga role email/password; password tidak ditulis ke source code. Customer tetap login melalui Google OAuth; `SeedAccounts:Customer:GoogleSubject` harus berisi nilai `sub` Google dari akun yang dipakai.

Workflow `.github/workflows/migrate.yml` menyediakan input manual `seed_accounts`. Jika dicentang, workflow menjalankan migrasi dengan `DATABASE_URL_UNPOOLED` terlebih dahulu, lalu menjalankan seeder dengan secret `SEED_ACCOUNTS_SHARED_PASSWORD` dan `SEED_ACCOUNTS_CUSTOMER_GOOGLE_SUBJECT`. Seeder mode tidak membutuhkan JWT, Google Client ID, CORS, atau storage secret karena tidak menyalakan HTTP server; workflow tetap hanya berjalan melalui `workflow_dispatch`.

## REST API

Semua route autentikasi dibatasi 20 request/menit per alamat IP. Akses API memakai header `Authorization: Bearer <accessToken>`; refresh token dikirim di body JSON dan hanya disimpan di memori frontend. Untuk reverse proxy, konfigurasikan trusted proxy dan forwarded headers secara eksplisit sebelum memakai IP klien sebagai partition.

| Method | Path | Body / akses |
|---|---|---|
| POST | `/api/auth/google` | `{ "credential": "GOOGLE_ID_TOKEN" }` |
| POST | `/api/auth/admin/login` | `{ "email": "...", "password": "..." }` |
| POST | `/api/auth/provider/register` | `{ "email": "...", "password": "...", "confirmPassword": "..." }` |
| POST | `/api/auth/provider/login` | `{ "email": "...", "password": "..." }` |
| POST | `/api/auth/provider/change-password` | Provider Bearer token; `{ "currentPassword": "...", "newPassword": "..." }` |
| POST | `/api/auth/refresh` | `{ "refreshToken": "..." }` |
| POST | `/api/auth/logout` | `{ "refreshToken": "..." }` |
| GET | `/api/auth/me` | Bearer access token |
| GET | `/api/user/dashboard` | Role Customer |
| GET | `/api/admin/dashboard` | Role PlatformAdmin atau AgencyAdmin |
| GET | `/api/provider/profile` | Role Provider; profil Provider sendiri |
| GET | `/api/provider/application/status` | Role Provider; status dan tahap aplikasi sendiri |
| POST | `/api/provider/application/submit` | Role Provider; kirim aplikasi untuk moderasi |
| POST | `/api/provider/personal-info` | Role Provider; personal info sendiri |
| POST | `/api/provider/address` | Role Provider; alamat sendiri |
| POST | `/api/provider/profile` | Role Provider; layanan dan ketersediaan sendiri |
| POST | `/api/provider/documents` | Role Provider; KTP/KK sendiri |
| PUT | `/api/provider/availability` | Role Provider; ketersediaan Provider sendiri |
| GET | `/api/provider/orders` | Role Provider; order yang ditugaskan ke Provider sendiri |
| GET | `/api/admin/providers/applications` | Admin; aplikasi Provider sesuai scope |
| POST | `/api/admin/providers/{id}/approve` | Admin; setujui aplikasi |
| POST | `/api/admin/providers/{id}/reject` | Admin; tolak dengan alasan |
| POST | `/api/admin/providers/{id}/request-changes` | Admin; minta perbaikan |
| POST | `/api/admin/providers/{id}/suspend` | Admin; tangguhkan akun |
| PATCH | `/api/admin/providers/{id}/suspend` | Platform/owning Agency Admin; tangguhkan provider Verified (reason wajib) |
| PATCH | `/api/admin/providers/{id}/reactivate` | Platform/owning Agency Admin; pulihkan provider ke Approved/Verified |
| PATCH | `/api/admin/agencies/{id}/suspend` | Platform Admin; nonaktifkan agency dan seluruh roster publiknya |
| PATCH | `/api/admin/agencies/{id}/reactivate` | Platform Admin; aktifkan kembali agency |
| GET | `/api/admin/audit` | Platform Admin; 200 audit terbaru, newest first |
| GET | `/.well-known/jwks.json` | Public RSA key saja |
| GET | `/health` | Liveness, tidak memeriksa koneksi database |

Respons login/refresh: `{ accessToken, expiresAt, refreshToken, refreshExpiresAt, user: { id, email, fullName, pictureUrl, role, profileCompleted, profileStep } }`. Frontend menyimpan token hanya di memori dan mengirimkannya kembali di body refresh/logout. Database menyimpan SHA-256 hash; refresh lama ditolak setelah dipakai. Konsumsi refresh memakai update atomik PostgreSQL sehingga satu token tidak bisa dipakai dua kali bersamaan. Jika penerbitan sesi baru gagal setelah konsumsi, pengguna perlu login lagi.

JWT memuat `sub`, `email`, `role`, `profileCompleted`, `jti`, `exp`, `nbf`, `iss`, `aud`. Validator membatasi algoritma ke RS256 dan memeriksa public key, issuer, audience, expiry. Logout mencabut refresh token sesi ini; access token yang sudah terbit tetap valid hingga kedaluwarsa. Sesi perangkat lain tidak dicabut. Key rotation saat ini satu key aktif; penggantian key membatalkan access token lama.

Google verifier memvalidasi signature, audience, issuer, expiry melalui Google.Apis.Auth dan mewajibkan email terverifikasi. Akun dicocokkan dengan `sub`, role selalu Customer; kesamaan email dengan akun lain ditolak dan tidak otomatis ditautkan. Data identitas Google awal disimpan sebagai JSONB beserta email/nama/foto/sub. Tidak ada redirect/callback server karena GIS popup memberikan credential langsung ke frontend.

Provider dapat mendaftar melalui `POST /api/auth/provider/register`; akun baru berstatus `Draft`, otomatis menjadi direct talent tanpa agency, dan dapat melengkapi onboarding dari panel Provider. Admin tetap dapat membuat Provider langsung melalui `POST /api/admin/providers`, misalnya untuk talent internal. Aplikasi yang dikirim memiliki status `Submitted`, `NeedsChanges`, `Approved`, `Rejected`, atau `Suspended`; hanya Provider `Approved` dengan `VerificationStatus=Verified` yang tampil di marketplace. Platform Admin melihat semua aplikasi, sedangkan Agency Admin hanya melihat roster agency-nya. Email disimpan di `ProviderCredentials`, password memakai ASP.NET Identity PasswordHasher yang sama dengan admin. Provider login menghasilkan role `Provider` dan claim `providerId`; repository scope membatasi profil, ketersediaan, dan order ke Provider tersebut. Belum ada alur verifikasi email atau lupa password/email reset karena layanan email belum dikonfigurasi.

Private key dibaca dari path konfigurasi atau Base64 PEM; mount dari secret manager dengan permission terbatas.

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
