# VTG Pool 3D room server (no npm dependencies).
#   docker build -t vtgpool-server .
#   docker run -d --restart unless-stopped -p 127.0.0.1:8765:8765 --name vtgpool-server vtgpool-server
FROM node:20-alpine
WORKDIR /app
COPY server.js package.json ./
ENV HOST=0.0.0.0 PORT=8765
EXPOSE 8765
USER node
CMD ["node", "server.js"]
