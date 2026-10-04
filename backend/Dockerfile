# Etapa de build e publicação (tech-stack.md §2)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ProjectPricing.Dominio/ProjectPricing.Dominio.csproj src/ProjectPricing.Dominio/
COPY src/ProjectPricing.Aplicacao/ProjectPricing.Aplicacao.csproj src/ProjectPricing.Aplicacao/
COPY src/ProjectPricing.Infraestrutura/ProjectPricing.Infraestrutura.csproj src/ProjectPricing.Infraestrutura/
COPY src/ProjectPricing.Api/ProjectPricing.Api.csproj src/ProjectPricing.Api/
RUN dotnet restore src/ProjectPricing.Api/ProjectPricing.Api.csproj

COPY prompts/ prompts/
COPY src/ src/
RUN dotnet publish src/ProjectPricing.Api/ProjectPricing.Api.csproj -c Release -o /app --no-restore

# Etapa de execução, sem SDK e com usuário não-root
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "ProjectPricing.Api.dll"]
