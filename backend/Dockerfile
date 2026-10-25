# 以 repo 根目錄為 build context：docker build -f backend/Dockerfile .

# ---- 前端 ----
FROM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
# 正式建置強制使用 api 資料來源（vite.config.ts 檢查）
ENV VITE_DATA_SOURCE=api
RUN npm run build

# ---- 後端 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/src/ApiGateway/ApiGateway.csproj backend/src/ApiGateway/
RUN dotnet restore backend/src/ApiGateway/ApiGateway.csproj
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/ApiGateway/ApiGateway.csproj -c Release -o /app --no-restore
COPY --from=frontend /src/frontend/dist /app/wwwroot/admin

# ---- 執行 ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
