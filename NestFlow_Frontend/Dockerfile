FROM node:22-alpine AS build
WORKDIR /src
COPY package*.json ./
RUN npm ci
COPY . .
# API 位址於建置期注入，Vite 會將其寫入靜態檔案
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
# 部署子路徑。預設為根路徑，直接執行不需反向代理；
# 反向代理掛在 https://host/nestflow/ 時，改由 docker-compose.prod.yml 傳入 /nestflow/
ARG VITE_BASE_PATH=/
ENV VITE_BASE_PATH=$VITE_BASE_PATH
RUN npm run build

FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 80
