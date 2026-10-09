#!/usr/bin/env bash

# ============================================================
# BantuBantu backend deploy helpers
#
# Sourced by deploy-be.sh. Kept in a separate file so the lftp
# retry logic can be exercised in isolation with a stub lftp on
# PATH (see the user-facing verification section in the deploy
# report / scripts).
#
# Conventions:
#   * Everything must be safe under `set -euo pipefail`.
#   * run_lftp_retry reads FTP_PORT / FTP_USER / FTP_HOST /
#     FTP_PASSWORD at call time (the caller defines them before
#     lftp runs, and the workflow only passes MONSTERASP_FTP_PASSWORD
#     into .env, never into the lftp command line).
#   * The FTP password is never echoed here. lftp commands are fed
#     through stdin (here-string), so lftp's exit status surfaces as
#     $? directly — no pipe, no PIPESTATUS juggling — and the account
#     string never appears in the process list.
# ============================================================

# ------------------------------------------------------------
# Retry classification
#
# Retry only on the two expected transient classes:
#   1. lock errors — the previous app process is still unloading
#      DLLs after ANCM picked up app_offline.htm (this is the race
#      this library was written for); and
#   2. transient connection failures.
# Everything else — most importantly auth/authorization failures —
# is a hard failure and is NOT retried.
# ------------------------------------------------------------

RETRYABLE_ERROR_RE='being used by another process|Could not connect|Connection (refused|reset|closed|aborted|timed out)|Timed out|host is down|Name or service not known|Network is unreachable|No route to host|Temporary failure|Cannot connect'

AUTH_ERROR_RE='Login failed|Access denied|Authentication failed'

is_auth_error() {
    [[ "$1" =~ $AUTH_ERROR_RE ]]
}

is_retryable_error() {
    [[ "$1" =~ $RETRYABLE_ERROR_RE ]]
}

# ------------------------------------------------------------
# run_lftp_retry <description> <lftp-command-script>
#
# Runs a single lftp session (commands come from $2 via stdin)
# with up to MAX_LFTP_ATTEMPTS attempts. Backoff is 5s, 10s, then
# 20s (capped) between attempts. Both the "clean /wwwroot" and the
# "upload fresh publish" mirrors are idempotent, so re-running a
# failed attempt is safe.
#
# The captured lftp output is printed to the log while still being
# matched against the retryable / auth signatures. On success prints
# the output (if any) and a final "OK" line; on an unrecoverable
# failure prints why and returns non-zero.
# ------------------------------------------------------------

MAX_LFTP_ATTEMPTS=8
LFTP_BACKOFF_START=5
LFTP_BACKOFF_MAX=20

run_lftp_retry() {
    local description="$1"
    local commands="$2"
    local attempt=1
    local delay="$LFTP_BACKOFF_START"
    local rc=0
    local output
    local i

    while (( attempt <= MAX_LFTP_ATTEMPTS )); do

        echo
        echo "  [lftp] $description (attempt $attempt/$MAX_LFTP_ATTEMPTS)"

        rc=0
        # Here-string: no pipe, so this IS lftp's real exit status.
        output="$(lftp -p "$FTP_PORT" <<< "$commands" 2>&1)" || rc=$?

        if (( rc == 0 )); then

            if [[ -n "$output" ]]; then
                # lftp never prints the password; safe to echo its output.
                printf '%s\n' "$output"
            fi

            echo "  [lftp] $description OK"
            return 0

        fi

        # Show the failed attempt before classifying it.
        printf '%s\n' "$output"

        if is_auth_error "$output"; then
            echo "  [lftp] $description: auth/authorization failure (exit $rc) — NOT retrying." >&2
            return 1
        fi

        if ! is_retryable_error "$output"; then
            echo "  [lftp] $description: unrecognized failure (exit $rc) — NOT retrying." >&2
            return 1
        fi

        if (( attempt == MAX_LFTP_ATTEMPTS )); then
            echo "  [lftp] $description: giving up after $MAX_LFTP_ATTEMPTS attempts." >&2
            return 1
        fi

        echo "  [lftp] $description: retryable failure (exit $rc); retrying in ${delay}s..." >&2
        sleep "$delay"

        attempt=$((attempt + 1))
        i=$((delay * 2))
        if (( i > LFTP_BACKOFF_MAX )); then
            i="$LFTP_BACKOFF_MAX"
        fi
        delay="$i"

    done

    return 1
}

# ------------------------------------------------------------
# wait_for_site_offline [url] [timeout_seconds]
#
# After app_offline.htm is uploaded, poll the site until ANCM
# serves it (HTTP 503), then return 0. Poll interval is 2s; the
# default timeout is 90s.
#
# NOTE: a 503 only proves ANCM acknowledged the offline marker. It
# does NOT prove the old process has released its DLL locks, which
# is exactly why the clean/upload steps below still retry on
# "being used by another process" instead of trusting this wait.
# ------------------------------------------------------------

wait_for_site_offline() {
    local url="${1:-${STAGING_API_URL:-http://localhost}/health}"
    local timeout_seconds="${2:-90}"
    local interval_seconds=2
    local deadline
    local status

    if ! command -v curl >/dev/null 2>&1; then
        echo "ERROR: curl tidak tersedia; dibutuhkan untuk menunggu site offline." >&2
        return 1
    fi

    deadline=$(( $(date +%s) + timeout_seconds ))

    echo "  Polling $url until ANCM serves app_offline.htm (HTTP 503)..."

    while :; do

        status="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' --max-time 10 "$url" 2>/dev/null || true)"

        if [[ "$status" == "503" ]]; then
            echo "  Site offline: HTTP 503."
            return 0
        fi

        if (( $(date +%s) >= deadline )); then
            echo "ERROR: site tidak offline dalam ${timeout_seconds}s setelah upload app_offline.htm (status terakhir: HTTP ${status:-<no-response>})." >&2
            echo "       app_offline.htm tetap berada di tempat; cek status Aplikasi di panel MonsterASP.NET." >&2
            return 1
        fi

        echo "  ... masih online (HTTP ${status:-no-response}); cek lagi dalam ${interval_seconds}s"
        sleep "$interval_seconds"

    done
}

# ------------------------------------------------------------
# fail-safe exit handling (wired up by deploy-be.sh)
#
# If the deploy dies AFTER app_offline.htm was uploaded, the
# maintenance marker is deliberately left in place — the site shows
# a maintenance page instead of a half-deleted app. This function
# prints the exact state and the safe recovery path.
# ------------------------------------------------------------

print_fail_safe() {
    echo
    echo "============================================================"
    echo " DEPLOY FAILED — SITE LEFT OFFLINE (SAFE STATE)"
    echo "============================================================"
    echo
    echo "app_offline.htm telah diupload dan TIDAK dihapus, jadi situs"
    echo "menampilkan halaman maintenance (HTTP 503), bukan aplikasi yang"
    echo "setengah ter-deploy."
    echo
    echo "Keadaan sekarang:"
    echo "  $STAGING_API_URL  -> halaman maintenance"
    echo
    echo "Langkah selanjutnya:"
    echo "  * Menjalankan ulang deploy AMAN: workflow mengupload ulang"
    echo "    app_offline.htm, membersihkan /wwwroot dengan retry, lalu"
    echo "    mengirim publish yang lengkap."
    echo "  * Jika lock file (\"being used by another process\") terus"
    echo "    muncul setelah semua percobaan, berhentikan proses aplikasi"
    echo "    dari panel MonsterASP.NET (Aplikasi -> Stop, tunggu, Start),"
    echo "    lalu jalankan ulang deploy."
    echo
}