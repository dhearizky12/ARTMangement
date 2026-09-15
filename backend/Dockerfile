# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BantuBantu.Domain/BantuBantu.Domain.csproj BantuBantu.Domain/
COPY BantuBantu.Application/BantuBantu.Application.csproj BantuBantu.Application/
COPY BantuBantu.Infrastructure/BantuBantu.Infrastructure.csproj BantuBantu.Infrastructure/
COPY BantuBantu.Api/BantuBantu.Api.csproj BantuBantu.Api/
RUN dotnet restore BantuBantu.Api/BantuBantu.Api.csproj
COPY . .
RUN dotnet publish BantuBantu.Api/BantuBantu.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

# One-shot migration job; SDK and EF tooling stay out of the API runtime image.
FROM build AS migrations
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", "--no-build", "--configuration", "Release", "--project", "BantuBantu.Infrastructure", "--startup-project", "BantuBantu.Api"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /app-data/documents && chown "$APP_UID" /app-data/documents
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BantuBantu.Api.dll"]
