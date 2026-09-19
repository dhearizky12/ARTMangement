#!/usr/bin/env bash

set -euo pipefail

# ============================================================
# BantuBantu Backend -> MonsterASP.NET
#
# Flow:
#
# .env
#   ↓
# map local env -> ASP.NET hierarchical config
#   ↓
# ensure persistent staging RSA keys
#   ↓
# dotnet publish
#   ↓
# inject config -> web.config
#   ↓
# app_offline.htm
#   ↓
# clean /wwwroot
#   ↓
# upload fresh publish
#   ↓
# remove app_offline.htm
#   ↓
# health check
# ============================================================


# ============================================================
# Configuration
# ============================================================

BACKEND_PROJECT="backend/BantuBantu.Api/BantuBantu.Api.csproj"

PUBLISH_DIR="./publish/backend"

ENV_FILE=".env"


# ------------------------------------------------------------
# MonsterASP
# ------------------------------------------------------------

FTP_HOST="site92439.siteasp.net"
FTP_PORT="22"
FTP_USER="site92439"

REMOTE_DIR="/wwwroot"


# ------------------------------------------------------------
# Hosted URLs
# ------------------------------------------------------------

STAGING_API_URL="http://bantu-bantu.runasp.net"

#
# Bisa override:
#
# STAGING_FRONTEND_ORIGIN="https://art-mngmnt.web.app" ./deploy-be.sh
#
# Kalau belum diberikan, script menggunakan FRONTEND_ORIGIN
# dari .env (default production: Firebase Hosting).
#

STAGING_FRONTEND_ORIGIN="${STAGING_FRONTEND_ORIGIN:-}"


# ------------------------------------------------------------
# JWT staging identity
# ------------------------------------------------------------

STAGING_JWT_KEY_ID="${STAGING_JWT_KEY_ID:-monsterasp-staging-v1}"


# ------------------------------------------------------------
# Persistent local RSA deployment keys
#
# JANGAN generate ulang setiap deploy.
# Kalau berubah, JWT lama menjadi invalid.
# ------------------------------------------------------------

DEPLOY_SECRET_DIR=".deploy/monsterasp"

JWT_PRIVATE_KEY_FILE="$DEPLOY_SECRET_DIR/private.pem"
JWT_PUBLIC_KEY_FILE="$DEPLOY_SECRET_DIR/public.pem"


# ------------------------------------------------------------
# Storage
#
# Backend defaults to Local and requires Storage:RootPath.
#
# NOTE:
# Karena deploy kita clean /wwwroot, isi directory ini juga
# akan hilang setiap deploy. Acceptable untuk staging.
# ------------------------------------------------------------

STAGING_STORAGE_PROVIDER="Local"
STAGING_STORAGE_ROOT="App_Data/documents"


# ------------------------------------------------------------
# Expected publish output
# ------------------------------------------------------------

EXPECTED_DLL="BantuBantu.Api.dll"
EXPECTED_RUNTIME_CONFIG="BantuBantu.Api.runtimeconfig.json"
EXPECTED_WEB_CONFIG="web.config"


# ============================================================
# Helpers
# ============================================================

log() {
    echo
    echo "==> $1"
}

fail() {
    echo
    echo "ERROR: $1" >&2
    exit 1
}


# ============================================================
# 1. Check dependencies
# ============================================================

log "Checking dependencies..."

if ! command -v dotnet >/dev/null 2>&1; then
    fail ".NET SDK tidak ditemukan."
fi

if ! command -v python3 >/dev/null 2>&1; then
    fail "python3 tidak ditemukan."
fi

if ! command -v openssl >/dev/null 2>&1; then
    fail "openssl tidak ditemukan."
fi

if ! command -v lftp >/dev/null 2>&1; then

    echo "lftp belum tersedia."

    if [[ "${OSTYPE:-}" == darwin* ]]; then

        if ! command -v brew >/dev/null 2>&1; then
            fail "Homebrew belum tersedia."
        fi

        echo "Installing lftp..."
        brew install lftp

    elif [[ -f /etc/debian_version ]]; then

        sudo apt-get update
        sudo apt-get install -y lftp

    elif [[ -f /etc/redhat-release ]]; then

        sudo yum install -y lftp

    else

        fail "Install lftp secara manual terlebih dahulu."

    fi

fi


echo "dotnet  : $(dotnet --version)"
echo "python3 : $(python3 --version)"
echo "openssl : $(openssl version)"
echo "lftp    : available"


# ============================================================
# 2. Validate local files
# ============================================================

log "Checking project files..."

[[ -f "$ENV_FILE" ]] \
    || fail "$ENV_FILE tidak ditemukan."

[[ -f "$BACKEND_PROJECT" ]] \
    || fail "$BACKEND_PROJECT tidak ditemukan."


echo "Environment:"
echo "  $ENV_FILE"

echo "Backend:"
echo "  $BACKEND_PROJECT"


# ============================================================
# 3. Load .env
# ============================================================

log "Loading $ENV_FILE..."

set -a

# shellcheck disable=SC1090
source "$ENV_FILE"

set +a

echo "$ENV_FILE loaded."

FTP_PASSWORD="${MONSTERASP_FTP_PASSWORD:-}"
[[ -n "$FTP_PASSWORD" ]] || fail "MONSTERASP_FTP_PASSWORD kosong."


# ============================================================
# 4. Resolve frontend origin
# ============================================================

if [[ -z "$STAGING_FRONTEND_ORIGIN" ]]; then
    STAGING_FRONTEND_ORIGIN="${FRONTEND_ORIGIN:-https://art-mngmnt.web.app}"
fi


[[ -n "$STAGING_FRONTEND_ORIGIN" ]] \
    || fail "Frontend origin tidak tersedia."


# ============================================================
# 5. Ensure persistent RSA keys
# ============================================================

log "Checking JWT RSA keys..."

mkdir -p "$DEPLOY_SECRET_DIR"


if [[ ! -f "$JWT_PRIVATE_KEY_FILE" || ! -f "$JWT_PUBLIC_KEY_FILE" ]]; then

    echo "RSA staging keys belum ada."
    echo "Generating persistent RSA 3072-bit key pair..."

    rm -f \
        "$JWT_PRIVATE_KEY_FILE" \
        "$JWT_PUBLIC_KEY_FILE"

    openssl genpkey \
        -algorithm RSA \
        -pkeyopt rsa_keygen_bits:3072 \
        -out "$JWT_PRIVATE_KEY_FILE"

    openssl pkey \
        -in "$JWT_PRIVATE_KEY_FILE" \
        -pubout \
        -out "$JWT_PUBLIC_KEY_FILE"

    chmod 600 "$JWT_PRIVATE_KEY_FILE"
    chmod 644 "$JWT_PUBLIC_KEY_FILE"

    echo "RSA key pair generated."

else

    echo "Existing RSA staging keys found."

fi


# ============================================================
# 6. Verify RSA key pair
# ============================================================

log "Verifying RSA key pair..."

TEMP_PUBLIC_KEY="$(mktemp)"

openssl pkey \
    -in "$JWT_PRIVATE_KEY_FILE" \
    -pubout \
    -out "$TEMP_PUBLIC_KEY" \
    >/dev/null 2>&1


if ! cmp -s "$TEMP_PUBLIC_KEY" "$JWT_PUBLIC_KEY_FILE"; then

    rm -f "$TEMP_PUBLIC_KEY"

    fail "RSA private/public key tidak cocok."

fi


rm -f "$TEMP_PUBLIC_KEY"

echo "RSA key pair valid."


# ============================================================
# 7. Convert RSA PEM -> Base64
# ============================================================

log "Encoding RSA keys..."

JWT_PRIVATE_KEY_BASE64="$(
    base64 < "$JWT_PRIVATE_KEY_FILE" \
        | tr -d '\r\n'
)"

JWT_PUBLIC_KEY_BASE64="$(
    base64 < "$JWT_PUBLIC_KEY_FILE" \
        | tr -d '\r\n'
)"


[[ -n "$JWT_PRIVATE_KEY_BASE64" ]] \
    || fail "Private key Base64 kosong."

[[ -n "$JWT_PUBLIC_KEY_BASE64" ]] \
    || fail "Public key Base64 kosong."


export JWT_PRIVATE_KEY_BASE64
export JWT_PUBLIC_KEY_BASE64


echo "RSA keys encoded."


# ============================================================
# 8. Map .env -> actual ASP.NET configuration
# ============================================================

log "Building staging ASP.NET configuration..."


# ------------------------------------------------------------
# Runtime
# ------------------------------------------------------------

export ASPNETCORE_ENVIRONMENT="Staging"


# ------------------------------------------------------------
# Database
#
# DatabaseConnectionStringResolver explicitly supports:
#
# DATABASE_URL
# DATABASE_URL_UNPOOLED
#
# Jadi nama existing .env sudah benar.
# ------------------------------------------------------------

export DATABASE_URL="${DATABASE_URL:-}"

export DATABASE_URL_UNPOOLED="${DATABASE_URL_UNPOOLED:-}"


# ------------------------------------------------------------
# Frontend:Origin
# ------------------------------------------------------------

export Frontend__Origin="$STAGING_FRONTEND_ORIGIN"


# ------------------------------------------------------------
# Google:ClientId
# ------------------------------------------------------------

export Google__ClientId="${GOOGLE_CLIENT_ID:-}"


# ------------------------------------------------------------
# Jwt:*
# ------------------------------------------------------------

export Jwt__KeyId="$STAGING_JWT_KEY_ID"

export Jwt__Issuer="$STAGING_API_URL"

export Jwt__Audience="${JWT_AUDIENCE:-}"

export Jwt__AccessMinutes="${JWT_ACCESS_MINUTES:-15}"

export Jwt__RefreshDays="${JWT_REFRESH_DAYS:-7}"


# ------------------------------------------------------------
# JWT RSA key material
# ------------------------------------------------------------

export Jwt__PrivateKeyBase64="$JWT_PRIVATE_KEY_BASE64"

export Jwt__PublicKeyBase64="$JWT_PUBLIC_KEY_BASE64"


# ------------------------------------------------------------
# Storage
#
# Program defaults to Local but requires RootPath
# when Provider=Local.
# ------------------------------------------------------------

export Storage__Provider="$STAGING_STORAGE_PROVIDER"

export Storage__RootPath="$STAGING_STORAGE_ROOT"


# ------------------------------------------------------------
# ASP.NET Host Filtering
# ------------------------------------------------------------

export AllowedHosts="bantu-bantu.runasp.net"


# ------------------------------------------------------------
# Optional Admin Seed
#
# Program.cs only reads these when --seed-admin is used.
# ------------------------------------------------------------

export AdminSeed__Email="${ADMIN_EMAIL:-}"

export AdminSeed__Password="${ADMIN_PASSWORD:-}"


# ============================================================
# 9. Validate ALL configuration required by Program.cs
# ============================================================

log "Validating required application configuration..."


[[ -n "${DATABASE_URL:-}" ]] \
    || fail "DATABASE_URL kosong."


[[ -n "${Google__ClientId:-}" ]] \
    || fail "Google__ClientId kosong."


[[ -n "${Jwt__KeyId:-}" ]] \
    || fail "Jwt__KeyId kosong."


[[ -n "${Jwt__Issuer:-}" ]] \
    || fail "Jwt__Issuer kosong."


[[ -n "${Jwt__Audience:-}" ]] \
    || fail "Jwt__Audience kosong."


[[ -n "${Jwt__AccessMinutes:-}" ]] \
    || fail "Jwt__AccessMinutes kosong."


[[ -n "${Jwt__RefreshDays:-}" ]] \
    || fail "Jwt__RefreshDays kosong."


[[ -n "${Frontend__Origin:-}" ]] \
    || fail "Frontend__Origin kosong."


[[ -n "${Jwt__PrivateKeyBase64:-}" ]] \
    || fail "Jwt__PrivateKeyBase64 kosong."


[[ -n "${Jwt__PublicKeyBase64:-}" ]] \
    || fail "Jwt__PublicKeyBase64 kosong."


[[ -n "${Storage__Provider:-}" ]] \
    || fail "Storage__Provider kosong."


[[ -n "${Storage__RootPath:-}" ]] \
    || fail "Storage__RootPath kosong."


# ------------------------------------------------------------
# Validate numeric JWT settings
# ------------------------------------------------------------

if ! [[ "$Jwt__AccessMinutes" =~ ^[1-9][0-9]*$ ]]; then
    fail "Jwt__AccessMinutes harus integer >= 1."
fi

if ! [[ "$Jwt__RefreshDays" =~ ^[1-9][0-9]*$ ]]; then
    fail "Jwt__RefreshDays harus integer >= 1."
fi


echo
echo "Required application configuration OK."

echo
echo "Staging config:"
echo "  ASPNETCORE_ENVIRONMENT : $ASPNETCORE_ENVIRONMENT"
echo "  API                    : $STAGING_API_URL"
echo "  Frontend:Origin        : $Frontend__Origin"
echo "  Google:ClientId        : configured"
echo "  Jwt:KeyId              : $Jwt__KeyId"
echo "  Jwt:Issuer             : $Jwt__Issuer"
echo "  Jwt:Audience           : $Jwt__Audience"
echo "  Jwt:AccessMinutes      : $Jwt__AccessMinutes"
echo "  Jwt:RefreshDays        : $Jwt__RefreshDays"
echo "  JWT RSA keys           : configured"
echo "  Storage:Provider       : $Storage__Provider"
echo "  Storage:RootPath       : $Storage__RootPath"
echo "  Database               : configured"


# ============================================================
# 10. Clean local publish
# ============================================================

log "Cleaning local publish directory..."

rm -rf "$PUBLISH_DIR"

mkdir -p "$PUBLISH_DIR"


# ============================================================
# 11. Publish backend
# ============================================================

log "Publishing backend..."

dotnet publish "$BACKEND_PROJECT" \
    -c Release \
    -o "$PUBLISH_DIR"


# ============================================================
# 12. Validate publish output
# ============================================================

log "Validating publish output..."


[[ -f "$PUBLISH_DIR/$EXPECTED_DLL" ]] \
    || fail "$EXPECTED_DLL tidak ditemukan."


[[ -f "$PUBLISH_DIR/$EXPECTED_RUNTIME_CONFIG" ]] \
    || fail "$EXPECTED_RUNTIME_CONFIG tidak ditemukan."


[[ -f "$PUBLISH_DIR/$EXPECTED_WEB_CONFIG" ]] \
    || fail "$EXPECTED_WEB_CONFIG tidak ditemukan."


echo "Publish output valid."


# ============================================================
# 13. Create local storage directory
# ============================================================

log "Preparing local storage directory..."

mkdir -p "$PUBLISH_DIR/$STAGING_STORAGE_ROOT"

echo "Storage directory:"
echo "  $PUBLISH_DIR/$STAGING_STORAGE_ROOT"


# ============================================================
# 14. Inject ASP.NET environment into web.config
# ============================================================

log "Injecting application configuration into web.config..."

export WEB_CONFIG_PATH="$PUBLISH_DIR/web.config"


python3 <<'PY'
import os
import sys
import xml.etree.ElementTree as ET


path = os.environ["WEB_CONFIG_PATH"]


try:
    tree = ET.parse(path)

except Exception as exc:
    print(
        f"ERROR: gagal membaca web.config: {exc}",
        file=sys.stderr
    )
    sys.exit(1)


root = tree.getroot()

aspnet_core = root.find(".//aspNetCore")


if aspnet_core is None:

    print(
        "ERROR: <aspNetCore> tidak ditemukan di web.config.",
        file=sys.stderr
    )

    sys.exit(1)


# ------------------------------------------------------------
# Remove previously generated env block
# ------------------------------------------------------------

existing = aspnet_core.find("environmentVariables")


if existing is not None:
    aspnet_core.remove(existing)


env_node = ET.SubElement(
    aspnet_core,
    "environmentVariables"
)


# ------------------------------------------------------------
# EXACT hosted configuration
# ------------------------------------------------------------

variable_names = [

    # Runtime
    "ASPNETCORE_ENVIRONMENT",

    # Database resolver
    "DATABASE_URL",
    "DATABASE_URL_UNPOOLED",

    # Frontend
    "Frontend__Origin",

    # Google
    "Google__ClientId",

    # JWT
    "Jwt__KeyId",
    "Jwt__Issuer",
    "Jwt__Audience",
    "Jwt__AccessMinutes",
    "Jwt__RefreshDays",

    # JWT RSA
    "Jwt__PrivateKeyBase64",
    "Jwt__PublicKeyBase64",

    # Storage
    "Storage__Provider",
    "Storage__RootPath",

    # ASP.NET
    "AllowedHosts",

    # Optional admin seed
    "AdminSeed__Email",
    "AdminSeed__Password",
]


for name in variable_names:

    value = os.environ.get(name)

    # Skip optional empty variables.
    if value is None or value == "":
        continue

    ET.SubElement(
        env_node,
        "environmentVariable",
        {
            "name": name,
            "value": value,
        }
    )


try:
    ET.indent(tree, space="  ")
except AttributeError:
    pass


#
# ElementTree handles XML escaping automatically.
#
# DATABASE_URL containing:
#
#   &sslmode=require
#
# becomes:
#
#   &amp;sslmode=require
#
# inside XML.
#

tree.write(
    path,
    encoding="utf-8",
    xml_declaration=True
)


print("web.config updated successfully.")
PY


# ============================================================
# 15. Validate generated web.config
# ============================================================

log "Validating injected web.config..."


python3 <<'PY'
import os
import sys
import xml.etree.ElementTree as ET


path = os.environ["WEB_CONFIG_PATH"]

tree = ET.parse(path)

aspnet_core = tree.getroot().find(".//aspNetCore")


if aspnet_core is None:

    print(
        "ERROR: aspNetCore node missing.",
        file=sys.stderr
    )

    sys.exit(1)


env_node = aspnet_core.find("environmentVariables")


if env_node is None:

    print(
        "ERROR: environmentVariables node missing.",
        file=sys.stderr
    )

    sys.exit(1)


values = {}


for item in env_node.findall("environmentVariable"):

    name = item.attrib.get("name")
    value = item.attrib.get("value")

    if name:
        values[name] = value


required = {

    "ASPNETCORE_ENVIRONMENT",

    "DATABASE_URL",

    "Frontend__Origin",

    "Google__ClientId",

    "Jwt__KeyId",

    "Jwt__Issuer",

    "Jwt__Audience",

    "Jwt__AccessMinutes",

    "Jwt__RefreshDays",

    "Jwt__PrivateKeyBase64",

    "Jwt__PublicKeyBase64",

    "Storage__Provider",

    "Storage__RootPath",
}


missing = [

    name
    for name in required
    if not values.get(name)

]


if missing:

    print(
        "ERROR: required configuration tidak terinject:",
        ", ".join(sorted(missing)),
        file=sys.stderr
    )

    sys.exit(1)


print("All required hosted configuration found in web.config.")

print()
print("Non-secret configuration:")


safe_keys = [

    "ASPNETCORE_ENVIRONMENT",

    "Frontend__Origin",

    "Google__ClientId",

    "Jwt__KeyId",

    "Jwt__Issuer",

    "Jwt__Audience",

    "Jwt__AccessMinutes",

    "Jwt__RefreshDays",

    "Storage__Provider",

    "Storage__RootPath",

    "AllowedHosts",
]


for key in safe_keys:

    value = values.get(key)

    if value:
        print(f"  {key}={value}")


print()
print("Secret configuration:")

print(
    "  DATABASE_URL="
    + ("configured" if values.get("DATABASE_URL") else "MISSING")
)

print(
    "  Jwt__PrivateKeyBase64="
    + (
        "configured"
        if values.get("Jwt__PrivateKeyBase64")
        else "MISSING"
    )
)

print(
    "  Jwt__PublicKeyBase64="
    + (
        "configured"
        if values.get("Jwt__PublicKeyBase64")
        else "MISSING"
    )
)
PY


# ============================================================
# 16. Prepare app_offline.htm
# ============================================================

log "Preparing app_offline.htm..."


CLEAN_DIR="$(mktemp -d)"


cleanup_local() {

    rm -rf "$CLEAN_DIR"

}


trap cleanup_local EXIT


cat > "$CLEAN_DIR/app_offline.htm" <<'EOF'
<!doctype html>

<html lang="en">

<head>

    <meta charset="utf-8">

    <meta
        name="viewport"
        content="width=device-width, initial-scale=1"
    >

    <title>Deployment</title>

</head>

<body>

    <h1>Application is being deployed...</h1>

</body>

</html>
EOF


# ============================================================
# 17. Put remote app offline
# ============================================================

log "Putting remote application offline..."


lftp -p "$FTP_PORT" <<EOF
set cmd:fail-exit yes
set sftp:auto-confirm yes
set mirror:set-permissions no

open -u "$FTP_USER","$FTP_PASSWORD" sftp://$FTP_HOST

put "$CLEAN_DIR/app_offline.htm" \
    -o "$REMOTE_DIR/app_offline.htm"

bye
EOF


echo "Waiting for IIS to release application files..."

sleep 3


# ============================================================
# 18. Clean remote /wwwroot
# ============================================================

log "Cleaning $REMOTE_DIR completely..."


#
# CLEAN_DIR contains ONLY app_offline.htm.
#
# After this mirror:
#
# /wwwroot/
# └── app_offline.htm
#
# Everything from the old deployment is removed.
#

lftp -p "$FTP_PORT" <<EOF
set cmd:fail-exit yes
set sftp:auto-confirm yes
set mirror:set-permissions no

open -u "$FTP_USER","$FTP_PASSWORD" sftp://$FTP_HOST

mirror \
    -R \
    --delete \
    --delete-first \
    --no-perms \
    --verbose \
    "$CLEAN_DIR" \
    "$REMOTE_DIR"

bye
EOF


echo "Remote $REMOTE_DIR cleaned."


# ============================================================
# 19. Upload fresh publish
# ============================================================

log "Uploading fresh backend publish..."


lftp -p "$FTP_PORT" <<EOF
set cmd:fail-exit yes
set sftp:auto-confirm yes
set mirror:set-permissions no

open -u "$FTP_USER","$FTP_PASSWORD" sftp://$FTP_HOST

mirror \
    -R \
    --no-perms \
    --transfer-all \
    --parallel=4 \
    --verbose \
    "$PUBLISH_DIR" \
    "$REMOTE_DIR"

bye
EOF


echo "Fresh publish uploaded."


# ============================================================
# 20. Bring app online
# ============================================================

log "Bringing application online..."


lftp -p "$FTP_PORT" <<EOF
set cmd:fail-exit yes
set sftp:auto-confirm yes

open -u "$FTP_USER","$FTP_PASSWORD" sftp://$FTP_HOST

rm "$REMOTE_DIR/app_offline.htm"

bye
EOF


# ============================================================
# 21. Wait for ASP.NET startup
# ============================================================

log "Waiting for ASP.NET Core startup..."

sleep 7


# ============================================================
# 22. Health check
# ============================================================

if command -v curl >/dev/null 2>&1; then

    log "Checking health endpoint..."

    HEALTH_TEMP="$(mktemp)"


    HTTP_STATUS="$(
        curl \
            --silent \
            --show-error \
            --output "$HEALTH_TEMP" \
            --write-out "%{http_code}" \
            --max-time 30 \
            "$STAGING_API_URL/health" \
            || true
    )"


    echo "HTTP status: $HTTP_STATUS"

    echo
    echo "Response:"
    echo

    cat "$HEALTH_TEMP" || true

    echo

    rm -f "$HEALTH_TEMP"


    if [[ "$HTTP_STATUS" =~ ^2 ]]; then

        echo
        echo "Health check PASSED."

    else

        fail "Health check gagal dengan HTTP $HTTP_STATUS. Cek MonsterASP.NET logs."

    fi

else

    fail "curl tidak tersedia; health check wajib dijalankan."

fi


# ============================================================
# 23. Finished
# ============================================================

echo
echo "============================================================"
echo " DEPLOYMENT FINISHED"
echo "============================================================"

echo
echo "Backend:"
echo "  $STAGING_API_URL"

echo
echo "Health:"
echo "  $STAGING_API_URL/health"

echo
echo "Environment:"
echo "  $ASPNETCORE_ENVIRONMENT"

echo
echo "Frontend Origin:"
echo "  $Frontend__Origin"

echo
echo "JWT Key ID:"
echo "  $Jwt__KeyId"

echo
echo "Storage:"
echo "  $Storage__Provider -> $Storage__RootPath"

echo
