#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["PerturbationInjector/PerturbationInjector.csproj", "PerturbationInjector/"]
COPY ["AntifragilePolicies/AntifragilePolicies.csproj", "AntifragilePolicies/"]
RUN dotnet restore "PerturbationInjector/PerturbationInjector.csproj"
COPY . .
WORKDIR "/src/PerturbationInjector"
RUN dotnet build "PerturbationInjector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PerturbationInjector.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PerturbationInjector.dll"]
CMD [ "--interval", "5", "--maximum", "15", "--duration", "480", "--target","mynginx", "--rate", "100", "--iterations", "10"]
