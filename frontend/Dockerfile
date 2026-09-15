# syntax=docker/dockerfile:1
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# VITE variables are public build-time config, never secrets.
ARG VITE_API_BASE_URL
ARG VITE_GOOGLE_CLIENT_ID
RUN test -n "$VITE_API_BASE_URL" && test -n "$VITE_GOOGLE_CLIENT_ID" && npm run build

FROM nginxinc/nginx-unprivileged:1.28-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1
