# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Certificados básicos
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080

# --- Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY HojaDeRuta.csproj ./
RUN dotnet restore HojaDeRuta.csproj

COPY . ./
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# --- Final ---
FROM base AS final
WORKDIR /app

# =====================================================
# DEPENDENCIAS NECESARIAS PARA CHROMIUM (PUPPETEER)
# =====================================================
RUN apt-get update && apt-get install -y \
    wget \
    gnupg \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libcups2 \
    libdrm2 \
    libgbm1 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libwayland-client0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    xdg-utils \
    libxkbcommon0 \
    libglib2.0-0 \
    libglib2.0-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# ============================================
# Certificado Avast
# ============================================
COPY certs/avast-root.cer /usr/local/share/ca-certificates/avast.crt
RUN update-ca-certificates

# Copiar publicación
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "HojaDeRuta.dll"]
