import truststore

# Este entorno está detrás de un proxy corporativo que reintercepta TLS con un
# certificado propio; el trust store nativo de Windows lo reconoce pero el
# bundle de certifi que usa Python por defecto no. truststore hace que ssl
# use el almacén de certificados del sistema operativo en su lugar.
truststore.inject_into_ssl()
