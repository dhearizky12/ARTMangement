FROM alpine:3.22
RUN apk add --no-cache openssl
COPY init-keys.sh /usr/local/bin/init-keys
ENTRYPOINT ["sh", "/usr/local/bin/init-keys"]
