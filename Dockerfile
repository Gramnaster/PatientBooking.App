# Multi-stage: the SDK image restores/builds/publishes, then only the published output ships in
# the much smaller ASP.NET runtime image - keeps the final image lean and free of the SDK/source.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj files copied first so `dotnet restore` layer-caches independently of source changes -
# editing a .cs file doesn't invalidate the (slow) NuGet restore layer on the next build.
COPY Directory.Build.props Directory.Packages.props PatientBooking.App.slnx ./
COPY PatientBooking.Api.Common/PatientBooking.Api.Common.csproj PatientBooking.Api.Common/
COPY PatientBooking.Api.Domain/PatientBooking.Api.Domain.csproj PatientBooking.Api.Domain/
COPY PatientBooking.Api.Application/PatientBooking.Api.Application.csproj PatientBooking.Api.Application/
COPY PatientBooking.Api/PatientBooking.Api.csproj PatientBooking.Api/
RUN dotnet restore PatientBooking.Api/PatientBooking.Api.csproj

# dotnet-ef only used by the migrator service (docker-compose.yml, "tools" profile) - installed
# here so the build stage can run migrations on demand without shipping the SDK/tool in the
# runtime image below, which never sees this layer.
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

COPY . .
RUN dotnet publish PatientBooking.Api/PatientBooking.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# $APP_UID is baked into the aspnet base image (non-root since .NET 8) -
# https://learn.microsoft.com/en-us/dotnet/core/docker/build-container#non-root-user
USER $APP_UID

COPY --from=build /app/publish .

# .NET 8+ container images default ASPNETCORE_HTTP_PORTS to 8080 - Kestrel serves plain HTTP here.
# Program.cs's UseHttpsRedirection()/UseHsts() expect TLS to be terminated in FRONT of this
# container (a reverse proxy) - see docker-compose.yml's comment on that before exposing this
# publicly.
EXPOSE 8080
ENTRYPOINT ["dotnet", "PatientBooking.Api.dll"]
