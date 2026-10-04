# Etapa de build (tech-stack.md §2: node na linha LTS ativa)
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Etapa de execução: só os arquivos estáticos servidos pelo Nginx
FROM nginx:1.30-alpine AS runtime
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --chmod=755 nginx/40-gerar-env.sh /docker-entrypoint.d/40-gerar-env.sh
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
