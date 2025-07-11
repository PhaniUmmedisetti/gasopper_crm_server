FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy solution and project files
COPY CRM.sln ./
COPY CRM.csproj ./
RUN dotnet restore CRM.sln

# Copy source code
COPY . ./
RUN dotnet publish CRM.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5211
EXPOSE 5211

ENTRYPOINT ["dotnet", "CRM.dll"]