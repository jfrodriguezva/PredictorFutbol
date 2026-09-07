FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SportsPredictor.Domain/*.csproj SportsPredictor.Domain/
COPY SportsPredictor.Application/*.csproj SportsPredictor.Application/
COPY SportsPredictor.Infrastructure/*.csproj SportsPredictor.Infrastructure/
COPY SportsPredictor.Api/*.csproj SportsPredictor.Api/
RUN dotnet restore SportsPredictor.Api/SportsPredictor.Api.csproj

COPY SportsPredictor.Domain/ SportsPredictor.Domain/
COPY SportsPredictor.Application/ SportsPredictor.Application/
COPY SportsPredictor.Infrastructure/ SportsPredictor.Infrastructure/
COPY SportsPredictor.Api/ SportsPredictor.Api/
RUN dotnet publish SportsPredictor.Api/SportsPredictor.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "SportsPredictor.Api.dll"]
