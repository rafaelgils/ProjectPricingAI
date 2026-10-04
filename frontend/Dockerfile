# Etapa de build (tech-stack.md §2: node na linha LTS ativa)
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Etapa de execução: só os arquivos estáticos servidos pelo Nginx, sem root (plano, F9)
FROM nginx:1.30-alpine AS runtime

# A imagem oficial roda o processo principal como root; aqui tudo roda como o usuário nginx.
RUN sed -i '/^user /d; s#^pid .*#pid /tmp/nginx.pid;#' /etc/nginx/nginx.conf \
    && mkdir -p /etc/nginx/gerado \
    && chown -R nginx:nginx /var/cache/nginx /usr/share/nginx/html /etc/nginx/gerado

COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --chmod=755 nginx/40-gerar-env.sh /docker-entrypoint.d/40-gerar-env.sh
COPY --from=build --chown=nginx:nginx /app/dist /usr/share/nginx/html

USER nginx
EXPOSE 8080
