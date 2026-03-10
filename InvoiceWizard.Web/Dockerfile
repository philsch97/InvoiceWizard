FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["InvoiceWizard.Web/InvoiceWizard.Web.csproj", "InvoiceWizard.Web/"]
RUN dotnet restore "InvoiceWizard.Web/InvoiceWizard.Web.csproj"
COPY . .
RUN dotnet publish "InvoiceWizard.Web/InvoiceWizard.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InvoiceWizard.Web.dll"]
