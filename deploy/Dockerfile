FROM microsoft/dotnet:1.1.2-runtime

WORKDIR /app
COPY . .

EXPOSE 5000/tcp
EXPOSE 5001/tcp

ENTRYPOINT ["dotnet", "server.dll"]