FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libtesseract5 \
        liblept5 \
        tesseract-ocr-eng \
        tesseract-ocr-amh \
    && rm -rf /var/lib/apt/lists/* \
    && (ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so 2>/dev/null || true) \
    && (ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract50.so 2>/dev/null || true) \
    && (ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so 2>/dev/null || true) \
    && mkdir -p /app/tessdata \
    && (cp -rn /usr/share/tesseract-ocr/*/tessdata/* /app/tessdata/ 2>/dev/null || true)

ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VotingSystem.csproj", "./"]
RUN dotnet restore "VotingSystem.csproj"
COPY . .
RUN dotnet publish "VotingSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .

# Use the runtime image's system Tesseract libraries instead of machine-specific bundled Linux binaries.
RUN rm -rf /app/x64

ENTRYPOINT ["dotnet", "VotingSystem.dll"]
