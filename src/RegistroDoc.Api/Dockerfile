FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RegistroDoc.Api/RegistroDoc.Api.csproj", "src/RegistroDoc.Api/"]
COPY ["src/RegistroDoc.Application/RegistroDoc.Application.csproj", "src/RegistroDoc.Application/"]
COPY ["src/RegistroDoc.Contracts/RegistroDoc.Contracts.csproj", "src/RegistroDoc.Contracts/"]
COPY ["src/RegistroDoc.Domain/RegistroDoc.Domain.csproj", "src/RegistroDoc.Domain/"]
COPY ["src/RegistroDoc.Infrastructure/RegistroDoc.Infrastructure.csproj", "src/RegistroDoc.Infrastructure/"]

RUN dotnet restore "src/RegistroDoc.Api/RegistroDoc.Api.csproj"

COPY . .

WORKDIR "/src/src/RegistroDoc.Api"

RUN dotnet publish "RegistroDoc.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "RegistroDoc.Api.dll"]
