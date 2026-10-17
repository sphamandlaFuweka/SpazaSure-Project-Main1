# Build stage
FROM node:20-alpine AS build

WORKDIR /app

# Copy package files
COPY package.json package-lock.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the application: APP_TARGET=supplier (default) or admin
ARG BUILD_MODE=production
ARG APP_TARGET=supplier
RUN if [ "$APP_TARGET" = "admin" ]; then \
      npx tsc && npx vite build --config vite.admin.config.ts --mode ${BUILD_MODE} && mv dist-admin dist; \
    else \
      npm run build -- --mode ${BUILD_MODE}; \
    fi

# Production stage
FROM nginx:alpine AS production

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port 80
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
