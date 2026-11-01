# certs/

The local compose stack serves HTTPS on 443 from the PEM pair in this directory,
the same way `deploy/http` serves it from the `mgo2-tls` Secret: two files named
`tls.crt` and `tls.key`.

```sh
cp fullchain.pem certs/tls.crt   # the leaf, or the full chain
cp privkey.pem   certs/tls.key   # unencrypted PEM; a PKCS#12 would need its password
docker compose up
```

Point `MGO2_TLS_DIRECTORY` at a different directory to keep the pair elsewhere;
it has to hold the same two names.

```sh
MGO2_TLS_DIRECTORY=/path/to/certificate docker compose up
```

The pair is optional. With no directory named and no files here, the server
starts exactly as before and serves plaintext 80 alone, which is what the game
console dials.

The two files are ignored by git: a private key does not belong in the
repository, and the deployment's copy lives in a Kubernetes Secret instead. The
certificate has to name the hostname a client dials, and the CA that issued it
has to be trusted on that client — a `mkcert` pair needs its root CA installed
before a browser accepts the connection.
