# Use the official .NET runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0

# Set working directory inside the container
WORKDIR /app

# Copy published files into container
COPY src/bin/Release/net9.0/ ./

# Default command: run with dotnet
ENTRYPOINT ["dotnet", "Ason.ExternalExecutor.dll"]