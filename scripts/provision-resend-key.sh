#!/usr/bin/env bash
#
# Provision a least-privilege Resend sending key for BantuBantu.
#
# Least privilege:
#   * RESEND_MASTER_API_KEY (full access) lives ONLY in the local root .env and
#     is read directly from that file. It is never `source`d, never exported to
#     a child process, never passed through a process argument (it reaches the
#     python3 helper through its environment only), and never printed.
#   * The backend gets a separate sending-only key created below.
#
# Resend returns a key's token exactly ONCE (creation time), so the token is
# persisted to the deploy-secrets directory next to the RSA keys:
#
#   .deploy/monsterasp/resend_api_key      (token, chmod 600)
#   .deploy/monsterasp/resend_api_key.id   (key id, chmod 600)
#
# Usage:
#   scripts/provision-resend-key.sh            create if missing, then verify
#   scripts/provision-resend-key.sh --verify   only run the least-privilege check
#   scripts/provision-resend-key.sh --rotate   create new, delete old, replace files
#   scripts/provision-resend-key.sh --help
#
# Reads from .env: RESEND_MASTER_API_KEY (required), RESEND_DOMAIN_ID (optional,
# restricts the key to a single verified sending domain).
#

set -euo pipefail

ENV_FILE="${RESEND_ENV_FILE:-.env}"
SECRET_DIR="${RESEND_SECRET_DIR:-.deploy/monsterasp}"
KEY_FILE="$SECRET_DIR/resend_api_key"
ID_FILE="$SECRET_DIR/resend_api_key.id"
API_BASE="${RESEND_API_BASE_URL:-https://api.resend.com}"
KEY_NAME="${RESEND_KEY_NAME:-bantubantu-staging-sending}"

fail() {
    echo "ERROR: $1" >&2
    exit 1
}

usage() {
    sed -n '3,26p' "$0" | sed 's/^# \{0,1\}//'
}

# ------------------------------------------------------------
# .env access
#
# The file is parsed, NOT sourced: sourcing under `set -a` would export every
# value (including RESEND_MASTER_API_KEY) into this shell and therefore into
# every child process of this script.
# ------------------------------------------------------------

read_env_var() {
    local name="$1" line

    [[ -f "$ENV_FILE" ]] || return 1

    line="$(grep -E "^[[:space:]]*(export[[:space:]]+)?${name}=" "$ENV_FILE" | head -n 1 || true)"
    [[ -n "$line" ]] || return 1

    line="${line#*=}"
    line="${line%$'\r'}"

    if [[ "$line" =~ ^\"(.*)\"$ ]]; then
        line="${BASH_REMATCH[1]}"
    elif [[ "$line" =~ ^\'(.*)\'$ ]]; then
        line="${BASH_REMATCH[1]}"
    fi

    printf '%s' "$line"
}

# ------------------------------------------------------------
# Resend API helper
#
# argv carries only: method, path, body. The bearer token travels through the
# environment (readable by this user only), never through argv, so it cannot
# show up in `ps` output or a shell trace.
#
# Prints the HTTP status code on stdout; the response body goes to $RESEND_HTTP_OUT.
# ------------------------------------------------------------

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

resend_http() {
    local method="$1" path="$2" body="${3:-}"

    RESEND_API_BASE="$API_BASE" \
    RESEND_HTTP_OUT="$WORK_DIR/response.json" \
    python3 - "$method" "$path" "$body" <<'PY'
import json
import os
import ssl
import sys
import urllib.error
import urllib.request

method, path, body = sys.argv[1], sys.argv[2], sys.argv[3]
url = os.environ["RESEND_API_BASE"].rstrip("/") + path
token = os.environ["RESEND_API_TOKEN"]
out = os.environ["RESEND_HTTP_OUT"]


def make_context():
    # python.org builds on macOS ship without the system root certificates, so
    # fall back to certifi and finally the OS CA bundle instead of failing with
    # CERTIFICATE_VERIFY_FAILED.
    candidates = []
    try:
        import certifi

        candidates.append(certifi.where())
    except Exception:
        pass
    candidates.extend(
        p
        for p in ("/etc/ssl/cert.pem", "/etc/ssl/certs/ca-certificates.crt")
        if os.path.exists(p)
    )
    for candidate in candidates:
        try:
            return ssl.create_default_context(cafile=candidate)
        except Exception:
            continue
    return ssl.create_default_context()


request = urllib.request.Request(
    url,
    data=body.encode("utf-8") if body else None,
    method=method,
    headers={
        "Authorization": "Bearer " + token,
        "Content-Type": "application/json",
        "Accept": "application/json",
        "User-Agent": "bantubantu-provision-resend/1.0",
    },
)

try:
    with urllib.request.urlopen(request, timeout=30, context=make_context()) as response:
        status, text = response.status, response.read().decode("utf-8", "replace")
except urllib.error.HTTPError as error:
    status, text = error.code, error.read().decode("utf-8", "replace")
except Exception as exc:  # noqa: BLE001 - report any transport failure clearly
    print("network error: " + str(exc), file=sys.stderr)
    sys.exit(1)

with open(out, "w", encoding="utf-8") as handle:
    handle.write(text)

print(status)
PY
}

# Call helper with the token supplied through RESEND_API_TOKEN.
api() {
    local method="$1" path="$2" body="${3:-}" token="$4"
    local status

    if ! status="$(RESEND_API_TOKEN="$token" resend_http "$method" "$path" "$body")"; then
        fail "Tidak dapat menghubungi Resend API ($API_BASE)."
    fi

    printf '%s' "$status"
}

json_field() {
    python3 - "$1" "$2" <<'PY'
import json
import sys

try:
    with open(sys.argv[1], encoding="utf-8") as handle:
        data = json.load(handle)
except Exception:
    data = {}

value = data.get(sys.argv[2])
print("" if value is None else value)
PY
}

# Never print the token: only the id and its last 4 characters.
describe_token() {
    local token="$1"
    printf '%s' "…$(printf '%s' "$token" | tail -c 4)"
}

write_key_files() {
    local token="$1" id="$2" stage

    mkdir -p "$SECRET_DIR"

    # Stage inside the target directory so the final rename stays on the same
    # filesystem and each file is replaced atomically.
    stage="$(mktemp -d "$SECRET_DIR/.resend.XXXXXX")"

    printf '%s' "$id" >"$stage/resend_api_key.id"
    printf '%s' "$token" >"$stage/resend_api_key"
    chmod 600 "$stage/resend_api_key.id" "$stage/resend_api_key"

    mv -f "$stage/resend_api_key.id" "$ID_FILE"
    mv -f "$stage/resend_api_key" "$KEY_FILE"
    rmdir "$stage"
}

# ------------------------------------------------------------
# Least-privilege verification
#
# 1. management endpoint (GET /domains) with the NEW key  -> must be rejected
#    (real API answers 401 restricted_api_key, some setups 403)
# 2. sending endpoint (POST /emails, deliberately invalid payload so nothing is
#    actually delivered) with the NEW key                  -> must NOT be 401/403
# 3. same management endpoint with the master key          -> control (expected 200);
#    skipped when no separate full-access master key is available.
#
# All observed status codes are reported.
# ------------------------------------------------------------

verify_least_privilege() {
    local new_token="$1" master_token="${2:-}"
    local code_manage code_send code_control="skipped (master key not available)"

    log_section "Verifying least privilege of the new key"

    code_manage="$(api GET /domains "" "$new_token")"
    echo "  management probe : GET /domains with sending key -> HTTP $code_manage (expected 401/403: rejected)"

    code_send="$(api POST /emails '{"subject":"","html":"","from":"","to":[]}' "$new_token")"
    echo "  sending probe    : POST /emails with sending key  -> HTTP $code_send (expected 400 validation, must not be 401/403)"

    if [[ -n "$master_token" ]]; then
        code_control="$(api GET /domains "" "$master_token")"
        echo "  control probe    : GET /domains with master key -> HTTP $code_control (expected 200)"
    fi

    if [[ "$code_manage" != "403" && "$code_manage" != "401" ]]; then
        fail "Sending key masih bisa memanggil endpoint management (HTTP $code_manage, expected 401/403). Least privilege TIDAK terpenuhi."
    fi

    if [[ "$code_send" == "401" || "$code_send" == "403" ]]; then
        fail "Sending key ditolak untuk mengirim email (HTTP $code_send)."
    fi

    if [[ -n "$master_token" && "$code_control" != "200" ]]; then
        fail "Master key tidak dapat memanggil endpoint management (HTTP $code_control, expected 200)."
    fi

    echo "  result           : least privilege terpenuhi."
}

log_section() {
    echo
    echo "==> $1"
}

# ------------------------------------------------------------
# Arguments
# ------------------------------------------------------------

MODE="provision"
for argument in "$@"; do
    case "$argument" in
        --rotate) MODE="rotate" ;;
        --verify) MODE="verify" ;;
        --help | -h)
            usage
            exit 0
            ;;
        *)
            echo "ERROR: argumen tidak dikenal: $argument" >&2
            usage >&2
            exit 2
            ;;
    esac
done

# ------------------------------------------------------------
# Read secrets from .env (parsed, never sourced)
# ------------------------------------------------------------

MASTER_TOKEN="$(read_env_var RESEND_MASTER_API_KEY || true)"
DOMAIN_ID="$(read_env_var RESEND_DOMAIN_ID || true)"

if [[ "$MODE" != "verify" && -z "$MASTER_TOKEN" ]]; then
    fail "RESEND_MASTER_API_KEY tidak ditemukan di $ENV_FILE.
Isi $ENV_FILE dengan baris:
  RESEND_MASTER_API_KEY=re_...
(full access key dari dashboard Resend, hanya dipakai oleh script ini)."
fi

# ------------------------------------------------------------
# --verify: run the check against the already-stored key only
# ------------------------------------------------------------

if [[ "$MODE" == "verify" ]]; then
    [[ -f "$KEY_FILE" ]] || fail "$KEY_FILE belum ada. Jalankan script ini tanpa --verify untuk provisioning terlebih dahulu."

    NEW_TOKEN="$(cat "$KEY_FILE")"

    # When .env holds the very same key (deployment key used in both places,
    # no separate full-access master key), the control probe would only
    # re-probe the sending key, so skip it instead of reporting a false alarm.
    if [[ -n "$MASTER_TOKEN" && "$MASTER_TOKEN" == "$NEW_TOKEN" ]]; then
        echo "Catatan: $ENV_FILE berisi key yang SAMA dengan $KEY_FILE (belum ada full-access master key terpisah)."
        echo "         Control probe dilewati; provisioning/rotate butuh full-access key di $ENV_FILE."
        MASTER_TOKEN=""
    fi

    verify_least_privilege "$NEW_TOKEN" "$MASTER_TOKEN"
    exit 0
fi

# ------------------------------------------------------------
# Idempotency: never mint a second key per deploy
# ------------------------------------------------------------

if [[ -f "$KEY_FILE" && "$MODE" == "provision" ]]; then
    EXISTING_ID="$(cat "$ID_FILE" 2>/dev/null || true)"
    echo "Sending key sudah ada di $KEY_FILE (id: ${EXISTING_ID:-unknown}). Tidak ada key baru dibuat."
    echo "Gunakan --rotate untuk mengganti key, atau --verify untuk mengecek least privilege."
    exit 0
fi

OLD_ID=""
if [[ -f "$ID_FILE" ]]; then
    OLD_ID="$(cat "$ID_FILE")"
fi

# ------------------------------------------------------------
# Create the key (explicit permission, never rely on the default)
# ------------------------------------------------------------

log_section "Creating sending-only Resend API key"

PAYLOAD="$(python3 - "$KEY_NAME" "$DOMAIN_ID" <<'PY'
import json
import sys

name, domain_id = sys.argv[1], sys.argv[2]
body = {"name": name, "permission": "sending_access"}
if domain_id:
    body["domain_id"] = domain_id
print(json.dumps(body))
PY
)"

CREATE_STATUS="$(api POST /api-keys "$PAYLOAD" "$MASTER_TOKEN")"

if [[ "$CREATE_STATUS" != "200" && "$CREATE_STATUS" != "201" ]]; then
    FAIL_REASON="$(json_field "$WORK_DIR/response.json" "message")"
    [[ -n "$FAIL_REASON" ]] || FAIL_REASON="$(cat "$WORK_DIR/response.json")"
    fail "Gagal membuat API key (HTTP $CREATE_STATUS): $FAIL_REASON"
fi

NEW_TOKEN="$(json_field "$WORK_DIR/response.json" "token")"
NEW_ID="$(json_field "$WORK_DIR/response.json" "id")"

[[ -n "$NEW_TOKEN" ]] || fail "Resend tidak mengembalikan token (token hanya dikembalikan sekali saat pembuatan)."
[[ -n "$NEW_ID" ]] || fail "Resend tidak mengembalikan id key."

echo "  key created : id=$NEW_ID permission=sending_access name=$KEY_NAME"
[[ -n "$DOMAIN_ID" ]] && echo "  domain      : $DOMAIN_ID (key dibatasi per domain)"
echo "  token       : $(describe_token "$NEW_TOKEN") (hanya 4 karakter terakhir yang ditampilkan)"

# ------------------------------------------------------------
# Rotation: delete the previous key by its stored id
# ------------------------------------------------------------

if [[ "$MODE" == "rotate" && -n "$OLD_ID" ]]; then
    log_section "Deleting previous key"

    DELETE_STATUS="$(api DELETE "/api-keys/$OLD_ID" "" "$MASTER_TOKEN")"

    if [[ "$DELETE_STATUS" == "200" || "$DELETE_STATUS" == "204" ]]; then
        echo "  old key id=$OLD_ID deleted (HTTP $DELETE_STATUS)"
    else
        echo "  WARNING: penghapusan key lama id=$OLD_ID gagal (HTTP $DELETE_STATUS)." >&2
        echo "  Hapus manual dari dashboard Resend agar tidak ada key aktif yang tidak terpakai." >&2
    fi
fi

# ------------------------------------------------------------
# Persist (atomic, chmod 600)
# ------------------------------------------------------------

write_key_files "$NEW_TOKEN" "$NEW_ID"

echo
echo "Persisted:"
echo "  $KEY_FILE (chmod 600)"
echo "  $ID_FILE (chmod 600)"

# ------------------------------------------------------------
# Least-privilege verification
# ------------------------------------------------------------

verify_least_privilege "$NEW_TOKEN" "$MASTER_TOKEN"

echo
echo "Done. Push the sending key to GitHub Actions with:"
echo "  gh secret set RESEND_SENDING_API_KEY < $KEY_FILE"
