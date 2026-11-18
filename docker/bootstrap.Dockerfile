FROM eclipse-mosquitto:2.0.22
USER root
COPY docker/bootstrap.sh /bootstrap.sh
RUN sed -i 's/\r$//' /bootstrap.sh && chmod +x /bootstrap.sh
ENTRYPOINT ["/bin/sh", "/bootstrap.sh"]
