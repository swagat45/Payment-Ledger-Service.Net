FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PaymentLedgerService.sln ./
COPY src/PaymentLedger.Api/PaymentLedger.Api.csproj src/PaymentLedger.Api/
COPY src/PaymentLedger.Application/PaymentLedger.Application.csproj src/PaymentLedger.Application/
COPY src/PaymentLedger.Domain/PaymentLedger.Domain.csproj src/PaymentLedger.Domain/
COPY src/PaymentLedger.Infrastructure/PaymentLedger.Infrastructure.csproj src/PaymentLedger.Infrastructure/
COPY src/PaymentLedger.Shared/PaymentLedger.Shared.csproj src/PaymentLedger.Shared/
COPY tests/PaymentLedger.UnitTests/PaymentLedger.UnitTests.csproj tests/PaymentLedger.UnitTests/
COPY tests/PaymentLedger.IntegrationTests/PaymentLedger.IntegrationTests.csproj tests/PaymentLedger.IntegrationTests/
COPY tests/PaymentLedger.ArchitectureTests/PaymentLedger.ArchitectureTests.csproj tests/PaymentLedger.ArchitectureTests/

RUN dotnet restore PaymentLedgerService.sln

COPY . .
RUN dotnet publish src/PaymentLedger.Api/PaymentLedger.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PaymentLedger.Api.dll"]
