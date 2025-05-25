# Use the official Nginx image
FROM nginx:1.27

# Remove the default nginx.conf
RUN rm /etc/nginx/nginx.conf

# Copy custom nginx.conf to the container
COPY nginx.conf /etc/nginx/nginx.conf

# Expose the port Nginx will listen on
EXPOSE 80