FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RegistroDoc.Web/RegistroDoc.Web.csproj", "src/RegistroDoc.Web/"]
COPY ["src/RegistroDoc.Contracts/RegistroDoc.Contracts.csproj", "src/RegistroDoc.Contracts/"]
RUN dotnet restore "src/RegistroDoc.Web/RegistroDoc.Web.csproj"

COPY . .
WORKDIR "/src/src/RegistroDoc.Web"

RUN dotnet publish "RegistroDoc.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RegistroDoc.Web.dll"]
