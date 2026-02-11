# escape=`
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0-windowsservercore-ltsc2022
WORKDIR /app

# Copy published app
COPY --from=build /out .

# Copy static files (logo, etc.)
# Must exist in your repo: .\wwwroot\assets\mq-cignal.png
COPY wwwroot C:\app\wwwroot

# Copy CA certs
COPY certs C:\certs

# Install CA certs into Windows trust store (for LDAPS)
RUN for %f in (C:\certs\*.cer) do certutil -addstore -f "Root" %f
RUN for %f in (C:\certs\*.cer) do certutil -addstore -f "CA" %f

EXPOSE 8080

ENTRYPOINT ["dotnet", "Gateway.dll"]
