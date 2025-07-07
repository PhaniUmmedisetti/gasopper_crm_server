# 1) Build & publish
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# copy solution + project file(s) and restore
COPY *.sln ./
COPY CRM.csproj ./
RUN dotnet restore

# copy everything else & publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# 2) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app

# copy publish output
COPY --from=build /app/publish .

# default to Production unless overridden
ENV ASPNETCORE_ENVIRONMENT=Production

# your app’s entrypoint
ENTRYPOINT ["dotnet", "CRM.dll"]