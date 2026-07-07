# src/Backend dizini build context olarak verilmelidir:
#   docker build -f SanalPOS.API/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ../../Directory.Build.props ../../Directory.Packages.props /
COPY . .
RUN dotnet restore "SanalPOS.API/SanalPOS.API.csproj"
RUN dotnet publish "SanalPOS.API/SanalPOS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SanalPOS.API.dll"]
