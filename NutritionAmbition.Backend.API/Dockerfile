# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy the project files
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Use a smaller runtime image for final deployment
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app

# Set environment variable to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Copy published app to the runtime image
COPY --from=build /app/out ./

# Expose port 8080 for Cloud Run
EXPOSE 8080

# Start the app with the correct DLL name
ENTRYPOINT ["dotnet", "NutritionAmbition.Backend.API.dll"]
