# 模擬環境閘道映像：只建置 .NET 後端，管理介面（js / css / icon）事先由
# simulation/scripts/build-frontend.ps1 打包到 backend/src/ApiGateway/wwwroot/admin，隨 dotnet publish 一起帶入
# 以 repo 根目錄為 build context：docker build -f simulation/Dockerfile .

# ---- 後端 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/src/ApiGateway/ApiGateway.csproj backend/src/ApiGateway/
RUN dotnet restore backend/src/ApiGateway/ApiGateway.csproj
COPY backend/src/ backend/src/
# 少了管理介面時直接中止，避免建出打不開 /admin 的映像
RUN test -f backend/src/ApiGateway/wwwroot/admin/index.html \
    || (echo "缺少 wwwroot/admin，請先執行 simulation/scripts/build-frontend.ps1" >&2 && exit 1)
RUN dotnet publish backend/src/ApiGateway/ApiGateway.csproj -c Release -o /app --no-restore

# ---- 執行 ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
