# ---------- Base Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libtesseract5 \
        liblept5 \
        tesseract-ocr-eng \
        tesseract-ocr-amh \
    && rm -rf /var/lib/apt/lists/* \
    && (ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so 2>/dev/null || true) \
    && (ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract50.so 2>/dev/null || true) \
    && (ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so 2>/dev/null || true) \
    && mkdir -p /app/tessdata \
    && (cp -rn /usr/share/tesseract-ocr/*/tessdata/* /app/tessdata/ 2>/dev/null || true)

# ---------- Build Stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["VotingSystem.csproj", "./"]
RUN dotnet restore "VotingSystem.csproj"
COPY . .

# Publish project
RUN dotnet publish "VotingSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------- Final Stage ----------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Replace machine-specific bundled Linux binaries with symlinks to the container's native Debian Tesseract libraries
RUN rm -rf /app/x64/*.so* /app/*.so* \
    && mkdir -p /app/x64 \
    && ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so \
    && ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so \
    && ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /app/x64/libdl.so

ENTRYPOINT ["dotnet", "VotingSystem.dll"]