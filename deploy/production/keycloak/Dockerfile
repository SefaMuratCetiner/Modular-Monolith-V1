# Production Keycloak: an "optimized" image. Build-time options (database vendor, health and metrics
# endpoints) are baked in with `kc.sh build`, so the container starts with `start --optimized` and does not
# re-augment on every start. Runtime options (hostname, TLS files, database URL, secrets) stay external.

FROM quay.io/keycloak/keycloak:26.3.3 AS builder
ENV KC_DB=postgres \
    KC_HEALTH_ENABLED=true \
    KC_METRICS_ENABLED=true
RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:26.3.3
COPY --from=builder /opt/keycloak/ /opt/keycloak/
COPY realm-export.json /opt/keycloak/data/import/realm-export.json
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized", "--import-realm"]
