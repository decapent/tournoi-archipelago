# Build
FROM node:22-alpine AS build
WORKDIR /source

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Execution : nginx sert le bundle et relaie /api vers le conteneur de l'API.
FROM nginx:1.27-alpine
COPY --from=build /source/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
