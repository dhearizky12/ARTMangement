#!/bin/sh
set -eu
umask 077
key_dir=/keys
mkdir -p "$key_dir"
trap 'rm -f "$key_dir/private.tmp" "$key_dir/public.tmp" "$key_dir/check.tmp"' EXIT
if [ ! -e "$key_dir/private.pem" ] && [ ! -e "$key_dir/public.pem" ]; then
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$key_dir/private.tmp"
    openssl pkey -in "$key_dir/private.tmp" -pubout -out "$key_dir/public.tmp"
    mv "$key_dir/private.tmp" "$key_dir/private.pem"
    mv "$key_dir/public.tmp" "$key_dir/public.pem"
fi
# An incomplete or mismatching key pair must fail; never silently rotate keys.
test -s "$key_dir/private.pem"
test -s "$key_dir/public.pem"
openssl pkey -in "$key_dir/private.pem" -pubout -out "$key_dir/check.tmp"
cmp -s "$key_dir/check.tmp" "$key_dir/public.pem"
# ASP.NET official images use the app user (UID 1654).
chown "${KEY_OWNER_UID:?KEY_OWNER_UID required}" "$key_dir/private.pem" "$key_dir/public.pem"
chmod 755 "$key_dir"
chmod 600 "$key_dir/private.pem"
chmod 644 "$key_dir/public.pem"
echo 'RSA key pair ready.'
