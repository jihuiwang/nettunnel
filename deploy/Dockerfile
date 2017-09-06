FROM microsoft/dotnet:2.0.0-runtime

WORKDIR /app
COPY . .

EXPOSE 5000/tcp
EXPOSE 5001/tcp

ENTRYPOINT ["dotnet", "server.dll"]