FROM ://microsoft.com AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM ://microsoft.com AS build
WORKDIR /src
COPY ["VotingSystem.csproj", "."]
RUN dotnet restore "./VotingSystem.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "VotingSystem.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VotingSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VotingSystem.dll"]
