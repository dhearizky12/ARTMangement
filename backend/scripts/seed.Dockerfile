FROM postgres:17-alpine
RUN apk add --no-cache python3
WORKDIR /seed
COPY seed-data ./seed-data
COPY scripts/seed-wilayah.py ./scripts/seed-wilayah.py
COPY scripts/run-wilayah-seed.sh ./scripts/run-wilayah-seed.sh
CMD ["sh", "scripts/run-wilayah-seed.sh"]
