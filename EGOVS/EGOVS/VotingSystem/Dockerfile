# Stage 1: Build the project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory inside container
WORKDIR /src

# Copy all files from your repo into the container
COPY . .

# Publish the project
# Note: path to VotingSystem.csproj relative to the Dockerfile
RUN dotnet publish VotingSystem/VotingSystem/VotingSystem.csproj -c Release -o /app/publish

# Stage 2: Create runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Expose port 80
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "VotingSystem.dll"]