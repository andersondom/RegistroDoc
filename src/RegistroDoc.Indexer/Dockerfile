FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["src/RegistroDoc.Indexer/RegistroDoc.Indexer.csproj", "src/RegistroDoc.Indexer/"]
COPY ["src/RegistroDoc.Application/RegistroDoc.Application.csproj", "src/RegistroDoc.Application/"]
COPY ["src/RegistroDoc.Domain/RegistroDoc.Domain.csproj", "src/RegistroDoc.Domain/"]
COPY ["src/RegistroDoc.Infrastructure/RegistroDoc.Infrastructure.csproj", "src/RegistroDoc.Infrastructure/"]

RUN dotnet restore "src/RegistroDoc.Indexer/RegistroDoc.Indexer.csproj"

COPY . .

WORKDIR "/src/src/RegistroDoc.Indexer"

RUN dotnet publish \
    "RegistroDoc.Indexer.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Development
ENV Indexer__InputPath=/data/input

ENTRYPOINT ["dotnet", "RegistroDoc.Indexer.dll"]
