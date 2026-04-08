FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/BilliardsBooking.API/BilliardsBooking.API.csproj", "./"]
RUN dotnet restore "BilliardsBooking.API.csproj"
COPY src/BilliardsBooking.API/ ./
RUN dotnet publish "BilliardsBooking.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BilliardsBooking.API.dll"]
