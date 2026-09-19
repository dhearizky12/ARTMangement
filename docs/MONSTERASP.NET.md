# MonsterASP.NET deployment runbook

This runbook prepares the ASP.NET Core API for IIS hosting on MonsterASP.NET while keeping Neon as the PostgreSQL provider. It does not replace the existing Google OAuth, RS256, CORS, storage, or wilayah implementations.

## Go/no-go gate: outbound Neon connectivity

This workspace cannot reach a MonsterASP.NET site or contact its support team, so outbound connectivity is **not verified**. The migration is not viable until Monster confirms that the selected plan permits outbound TCP connections from the ASP.NET worker to the Neon endpoint on port `5432`.

After a site is provisioned, test from the hosting environment (or ask support to run the equivalent test):

```powershell
Test-NetConnection ep-example.eu-central-1.aws.neon.tech -Port 5432
```

Then validate the actual credentials with the direct connection string from a machine that has `psql`:

```powershell
$env:DATABASE_URL_UNPOOLED = "postgresql://USER:PASSWORD@ep-example.eu-central-1.aws.neon.tech/DB?sslmode=require&channel_binding=require"
psql $env:DATABASE_URL_UNPOOLED -v ON_ERROR_STOP=1 -c "select 1"
```

A successful `TcpTestSucceeded: True` and `select 1` are required. If port `5432` is blocked, stop here and choose a hosting plan or architecture that can reach Neon; do not continue with publish configuration and assume it will work.

## Publish and upload

Publish from the repository root or from CI. The SDK generates an IIS `web.config` for the ASP.NET Core Module:

```sh
dotnet publish backend/BantuBantu.Api/BantuBantu.Api.csproj \
  --configuration Release \
  --output ./artifacts/monsteraspnet
```

Upload the contents of `artifacts/monsteraspnet` to the site. If the Monster plan exposes an App Settings/environment-variable panel, configure the variables there. If it only supports IIS configuration, copy [`deploy/monsteraspnet/web.config.template`](../deploy/monsteraspnet/web.config.template) to the published directory as `web.config`, fill it on the server, and keep the filled file out of source control and backups that are shared with the repository. Never put secrets in the tracked template.

The template uses IIS `environmentVariables`; it is not Docker-style `-e` injection. The exact mechanism available on the selected Monster plan must be confirmed in its control panel or documentation before deployment.

## Configuration checklist

Use these existing configuration keys. Do not introduce `JWT_SECRET`, `ALLOWED_ORIGINS`, or another parallel naming scheme.

| Key | What it contains | Where to get it |
|---|---|---|
| `DATABASE_URL` | Neon pooled application URL, including `sslmode=require` | Neon Console/CLI, production branch pooled endpoint |
| `Jwt__PrivateKeyBase64` | Base64-encoded PEM RSA private key used to sign tokens | The operator-managed RS256 key pair |
| `Jwt__PublicKeyBase64` | Base64-encoded matching PEM public key | Derived from the same RS256 key pair |
| `Jwt__KeyId` | Active signing key identifier | Operator-managed key id; keep stable while tokens are valid |
| `Jwt__Issuer` | Production JWT issuer URL | The deployed API URL |
| `Jwt__Audience` | JWT audience accepted by the frontend/API | The production application audience |
| `Jwt__AccessMinutes` / `Jwt__RefreshDays` | Token lifetime policy | Production security policy |
| `Google__ClientId` | Google Identity Services web client id | Google Cloud OAuth credentials |
| `Google__ClientSecret` | Google OAuth client secret reserved for server-side OAuth use | Google Cloud OAuth credentials; keep configured if required by the host |
| `Frontend__Origin` | Primary exact Cloudflare Pages origin | Production frontend URL, without a trailing slash |
| `Frontend__Origins` | Additional exact origins | Comma/semicolon-separated allowed origins |
| `Frontend__OriginPatterns` | Preview origin patterns | For example `https://*.pages.dev` |
| `AllowedHosts` | Hostnames accepted by ASP.NET Core | API hostname(s), comma-separated as appropriate |
| `Auth__CookieSecure` | Secure refresh-cookie flag | Set `true` after HTTPS is active |
| `Auth__CookieSameSite` | Cross-origin cookie policy | Set `None` for Pages → API; requires HTTPS |
| `Storage__Provider` | File storage implementation | `S3` in production |
| `Storage__S3__ServiceUrl` | S3-compatible endpoint | Cloudflare R2 S3 API endpoint |
| `Storage__S3__Region` | S3 signing region | `auto` for R2 unless the bucket requires another value |
| `Storage__S3__AccessKey` / `Storage__S3__SecretKey` | Object storage credentials | Cloudflare R2 API token with the required bucket permissions |
| `Storage__S3__Bucket` / `Storage__S3__KeyPrefix` | Private document location | R2 bucket and an optional object prefix |

The runtime API needs the pooled `DATABASE_URL`. Keep `DATABASE_URL_UNPOOLED` in a local/CI secret store for migrations and seeding instead of exposing it to the web process whenever possible.

## Migrations: direct Neon URL from outside Monster

Use the Neon **direct/unpooled** URL for every long-lived EF Core operation. Run this from a local machine or CI, never as an IIS startup action and never with the pooled URL:

```sh
export DATABASE_URL_UNPOOLED='postgresql://USER:PASSWORD@ep-example.eu-central-1.aws.neon.tech/DB?sslmode=require&channel_binding=require'
dotnet ef database update \
  --project backend/BantuBantu.Infrastructure \
  --startup-project backend/BantuBantu.Api
```

`AppDbContextFactory` and `DatabaseConnectionStringResolver` already prioritize `DATABASE_URL_UNPOOLED` for EF design-time commands and preserve Neon TLS settings. Apply migrations once before uploading an API build that depends on the new schema.

## Existing CORS and authentication cookie settings

The API already reads `Frontend__Origin`, `Frontend__Origins`, and `Frontend__OriginPatterns` through `AllowedOriginPolicy`. Configure those same keys for the Monster deployment; do not add an `ALLOWED_ORIGINS` variable or a second CORS pipeline.

Before testing Google OAuth end to end, confirm that an SSL certificate is actually issued and active for the exact Monster API hostname:

```sh
curl -fsSI https://api.example.com/health
```

Only after HTTPS works should `Auth__CookieSecure=true` and `Auth__CookieSameSite=None` be tested with Cloudflare Pages. Browsers reject a `SameSite=None` cookie delivered without HTTPS, which makes a healthy OAuth callback appear to be an authentication failure.

## Wilayah seed data

The wilayah reference seed is independent of the hosting provider. It runs once from a local machine or CI directly against Neon using the direct URL, not inside the Monster server and not on every API boot:

```sh
docker compose --profile tools run --rm wilayah-seed
```

The wrapper chooses `DATABASE_URL_UNPOOLED`, checks whether `Villages` already contains rows, and exits without inserting when the seed is already present. Keep both Neon URLs in the local `.env` used by Compose; do not add a Monster startup hook for this job.

## Health check

Configure the Monster health check to request `GET /health`. It is a lightweight liveness endpoint that returns HTTP 200 without querying the database, so it remains useful while diagnosing a database or secret configuration issue.

## Deployment checklist

1. Get an explicit yes from MonsterASP.NET support (or a successful test on the provisioned site) for outbound Neon TCP `5432`.
2. Create/verify the production SSL certificate and confirm `https://<api-host>/health`.
3. Run EF migrations locally/CI with `DATABASE_URL_UNPOOLED`.
4. Run the wilayah seed once with `docker compose --profile tools run --rm wilayah-seed` if the reference tables are empty.
5. Publish and upload the API, then inject the keys above through Monster App Settings or a server-only `web.config`.
6. Verify CORS preflight from the actual Cloudflare Pages origin.
7. Test Google login and refresh-cookie behavior only after steps 1–2 pass.
