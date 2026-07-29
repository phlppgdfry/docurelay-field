FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/DocuRelay.Contracts/DocuRelay.Contracts.csproj", "src/DocuRelay.Contracts/"]
COPY ["src/DocuRelay.Worker/DocuRelay.Worker.csproj", "src/DocuRelay.Worker/"]
RUN dotnet restore "src/DocuRelay.Worker/DocuRelay.Worker.csproj"
COPY . .
RUN dotnet publish "src/DocuRelay.Worker/DocuRelay.Worker.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DocuRelay.Worker.dll"]
