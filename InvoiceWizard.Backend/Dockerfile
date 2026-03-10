FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["InvoiceWizard.Backend/InvoiceWizard.Backend.csproj", "InvoiceWizard.Backend/"]
RUN dotnet restore "InvoiceWizard.Backend/InvoiceWizard.Backend.csproj"
COPY . .
RUN dotnet publish "InvoiceWizard.Backend/InvoiceWizard.Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InvoiceWizard.Backend.dll"]
