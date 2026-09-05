FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Enzo.Diagrams.Core/Enzo.Diagrams.Core.csproj src/Enzo.Diagrams.Core/
COPY src/Enzo.Diagrams.Language/Enzo.Diagrams.Language.csproj src/Enzo.Diagrams.Language/
COPY src/Enzo.Diagrams.Rendering/Enzo.Diagrams.Rendering.csproj src/Enzo.Diagrams.Rendering/
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
