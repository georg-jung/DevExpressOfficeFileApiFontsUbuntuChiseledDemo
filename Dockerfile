# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY --link . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime (matches your prod "distroless-ish" base)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-composite-extra AS final
WORKDIR /app
COPY --from=build /app/publish .

# (Optional) keep globalization on explicitly
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER $APP_UID
ENTRYPOINT ["dotnet", "DxOfficeLinuxMwe.dll"]
