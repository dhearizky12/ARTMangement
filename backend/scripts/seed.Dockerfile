FROM postgres:17-alpine
RUN apk add --no-cache python3
WORKDIR /seed
COPY seed-data ./seed-data
COPY scripts/seed-wilayah.py ./scripts/seed-wilayah.py
CMD ["sh", "-ec", "python3 scripts/seed-wilayah.py > /tmp/regions.sql && psql -v ON_ERROR_STOP=1 -f /tmp/regions.sql"]
