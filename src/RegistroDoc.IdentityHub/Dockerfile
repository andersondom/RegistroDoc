FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RegistroDoc.IdentityHub/RegistroDoc.IdentityHub.csproj", "src/RegistroDoc.IdentityHub/"]
RUN dotnet restore "src/RegistroDoc.IdentityHub/RegistroDoc.IdentityHub.csproj"

COPY . .
WORKDIR "/src/src/RegistroDoc.IdentityHub"

RUN dotnet publish "RegistroDoc.IdentityHub.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RegistroDoc.IdentityHub.dll"]
