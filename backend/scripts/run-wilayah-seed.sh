#!/bin/sh
set -eu

seed_url="${DATABASE_URL_UNPOOLED:-${DATABASE_URL:-}}"
test -n "$seed_url" || { echo 'DATABASE_URL is required' >&2; exit 2; }

# The seed runs in one transaction, so a non-empty Villages table means the
# complete reference snapshot was already applied. This keeps normal reruns
# fast and avoids a bulk write on every deployment.
already_seeded="$(psql "$seed_url" -Atqc 'SELECT CASE WHEN to_regclass('\''public."Villages"'\'') IS NULL THEN false ELSE EXISTS (SELECT 1 FROM "Villages") END')"
if [ "$already_seeded" = "t" ]; then
  echo 'Wilayah seed already applied; skipping.'
  exit 0
fi

python3 scripts/seed-wilayah.py > /tmp/regions.sql
psql "$seed_url" -v ON_ERROR_STOP=1 -f /tmp/regions.sql
