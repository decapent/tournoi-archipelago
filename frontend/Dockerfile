# Build
FROM node:22-alpine AS build
WORKDIR /source

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Execution : nginx sert le bundle et relaie /api vers l'API.
FROM nginx:1.27-alpine
COPY --from=build /source/dist /usr/share/nginx/html

# L'image officielle applique envsubst aux gabarits de ce dossier au demarrage. Le filtre
# limite la substitution a API_URL, pour ne pas toucher aux variables nginx ($uri, $host...).
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
ENV NGINX_ENVSUBST_FILTER=^API_URL$
ENV API_URL=http://api:8080

EXPOSE 80
