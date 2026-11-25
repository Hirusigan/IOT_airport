# Airport luggage tracking

1. Start Docker and open a terminal in the project folder.
2. If `docker network inspect ingress` reports that the network is missing, run `docker network create ingress`. The Compose file uses this shared network for the Cloudflare tunnel on the hosted setup and expects it to exist for local runs too.
3. Run `docker compose up -d`. Compose loads the local test database settings from `.env.example`. If you are reusing an existing database, set `POSTGRES_PASSWORD` in `.env` to its current password.
4. Open [http://localhost:8081](http://localhost:8081). In **Devtools**, choose a scenario and click **Start simulation**.
