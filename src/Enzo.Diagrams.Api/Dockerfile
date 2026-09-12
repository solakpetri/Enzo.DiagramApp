FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Enzo.Diagrams.Domain/Enzo.Diagrams.Domain.csproj src/Enzo.Diagrams.Domain/
COPY src/Enzo.Diagrams.Application/Enzo.Diagrams.Application.csproj src/Enzo.Diagrams.Application/
COPY src/Enzo.Diagrams.Infrastructure/Enzo.Diagrams.Infrastructure.csproj src/Enzo.Diagrams.Infrastructure/
COPY src/Enzo.Diagrams.Api/Enzo.Diagrams.Api.csproj src/Enzo.Diagrams.Api/
RUN dotnet restore src/Enzo.Diagrams.Api/Enzo.Diagrams.Api.csproj

COPY . .
RUN dotnet build src/Enzo.Diagrams.Api/Enzo.Diagrams.Api.csproj -c Release --no-restore

FROM build AS publish
RUN dotnet publish src/Enzo.Diagrams.Api/Enzo.Diagrams.Api.csproj -c Release --no-build -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Enzo.Diagrams.Api.dll"]
