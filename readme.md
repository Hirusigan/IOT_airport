# Airport luggage tracking

C++ virtual devices → airport Mosquitto bridges → regional Mosquitto → ASP.NET Core/EF Core → PostgreSQL → React/SignalR.

## Start the application

Install Docker Engine with Compose v2 on your Pi 5 running 64-bit Ubuntu Server. From the repository root:

```sh
docker compose up --build -d
```

No setup scripts, host SDKs or initial .env file are required. The bootstrap container generates credentials and broker configuration into the persistent `runtime-config` volume. Compose waits for it, starts the database/brokers, applies migrations, seeds missing AAR/CPH airports and devices, and starts the dashboard. Subsequent starts reuse credentials and data.

Open your configured tunnel hostname. Compose publishes no frontend host port; cloudflared reaches the frontend on the external `ingress` network.

If your existing cloudflared container runs on the same Docker network, point the tunnel to `http://frontend:80`; otherwise use your existing host routing. Cloudflare Access setup remains a separate deployment step. Devtools and the existing import/registration APIs have no application login, so protect the hostname before exposing it publicly. Database and MQTT ports stay internal.

**Upgrade from the script-based version:** keep your existing .env for the first startup. Bootstrap uses those credentials so the existing PostgreSQL volume remains accessible. Once generated, runtime configuration is authoritative. Do not delete `runtime-config` while retaining database/broker volumes, or fresh credentials may no longer match the stored database.

```sh
docker compose stop      # stop and preserve data/configuration
docker compose up -d     # resume
docker compose logs -f backend aar cph regional
docker compose down      # remove containers, preserve volumes
```

## Docker Hub deployment

The frontend joins the existing external Docker network `ingress`. Your cloudflared container must also be connected to it, with the application Service URL set to `http://frontend:80`. That network must exist before starting Compose, including when building locally.

To publish the bootstrap, backend and frontend images for both PCs (AMD64) and the Pi (ARM64), run on your development machine:

```sh
docker login --username andreasfrost1004
docker buildx bake --push
```

After the images are published, use the repository's Compose file on the Pi:

```sh
docker compose pull
docker compose up --no-build -d
```

Repeat those two commands to deploy updated images. Data stays in the existing volumes. Images use the `latest` tag; keep the Pi's Compose file updated when service configuration changes.

## Devtools

Open **Devtools** in the sidebar. Choose All scenarios, Normal journey, Missed flight, Wrong flight, or Unexpected airport and click **Start simulation**. Only one simulation runs at a time. Status shows starting/running/completed/stopped/failed/interrupted and the number of published events.

The backend prepares a fresh run, imports its manifest through the import service, and launches the existing C++ publisher. Messages still travel through the local airport brokers and regional bridge. The run starts 15 seconds after preparation and the full scenario lasts about 90 seconds. Completion means MQTT publishing finished; ingestion and dashboard refresh may follow shortly afterward.

**Stop simulation** cancels future publishing and retains already imported data. Imported flights and assignments remain, so deadline rules can still create alerts for a stopped run. A backend restart marks an unfinished run Interrupted rather than silently restarting it.

**Clear database** asks for confirmation, stops an active simulation, and deletes airports, devices, flights, journeys, observations, states, alerts and diagnostics. The schema and migrations remain. Internal cleared-run IDs survive so queued old messages are ignored, including after restarts. Existing prepared files/status remain in the simulation volume; cleared manifests cannot be reimported.

**Seed defaults** adds missing AAR/CPH airports and scanner/gate devices without removing history or changing existing metadata. It is safe to repeat. Clearing and seeding are separate actions. Startup also seeds missing defaults, so restarting after a clear restores those registrations.

| Bag in All scenarios | Expected behavior |
| --- | --- |
| BAG-001 | Correct loading and arrival; no active handling alert |
| BAG-002 | Orange warning, then red missed-flight alert |
| BAG-003 | Wrong-flight alert, then missed planned flight |
| BAG-004 | Unexpected-airport alert, then missed flight |

The backend owns alert decisions. Rules thresholds are configured in Compose. Business status is the highest active alert severity; telemetry status independently describes freshness and MQTT connectivity.

## Trip map

Search for a bag on the Luggage page to see its airports, numbered flight legs and event-driven suitcase movement. A trip selector keeps separate runs apart. Airport markers show airport and flight information. Routes and flight/transfer positions are schematic illustrations, not live tracking.

In Devtools, start **Connecting flights** and search for **BAG-TRANSFER**. The demo travels AAR → CPH → AAR, transferring at Copenhagen, and lasts about 105 seconds including preparation. Existing single-flight runs remain available as individual trips. Startup migrates existing journeys into single-leg trips without removing history.

## API

| Endpoint | Purpose |
| --- | --- |
| GET /api/airports | Airport metadata and status |
| POST /api/airports | Register airport metadata |
| GET /api/airports/{id} | Flights and alert history |
| GET /api/luggage/{id} | Luggage states and observations across runs |
| GET /api/alerts | Alerts; optional airportId, active and severity filters |
| GET /api/statistics | Persisted counts |
| POST /api/runs/import | Import a prepared manifest |
| GET /api/devtools/scenarios | Available scenarios |
| GET /api/devtools/simulation | Current/last simulation status |
| POST /api/devtools/simulation | Start with a scenarioId |
| POST /api/devtools/simulation/stop | Stop and await publisher exit |
| POST /api/devtools/database/clear | Clear application data |
| POST /api/devtools/database/seed | Add missing default registrations |
| GET /health | Database/MQTT readiness |
| /hub | SignalR refresh notifications |

## Development and verification

The backend Docker build runs the existing C++ CTest checks. In `frontend/`, use `npm ci` and `npm run build`. For local frontend development Vite proxies to backend port 5080; local backend development requires database/MQTT configuration and the compiled simulator path and scenarios directory for Devtools.

- [Backend architecture](docs/backend-architecture.md)
- [Frontend architecture](docs/frontend-architecture.md)
- [Devtools and Docker deployment](docs/devtools.md)
- [MQTT contract](docs/contract.md)
- [Verification](docs/verification.md)
- [Historical proposal](docs/original-proposal.md)

## Reliability and scope

Exactly one backend is supported. A shared processing gate serializes imports, ingestion, registration, rules, seeding and resets. Database keys enforce event/alert uniqueness. A separate Devtools gate serializes simulation and maintenance operations.

MQTT uses QoS 1, retain false, persistent sessions and bounded broker queues (1,000 messages/1 MiB per client). Publishing waits for acknowledgments and reconnects with bounded backoff. PUBACK does not guarantee database commit. There is no durable retry inbox/outbox or broker replication: the system remains best effort across the acknowledgment/commit gap. Long outages, queue overflow, crashes and unavailable storage can lose events.

Runtime data/configuration and prepared simulation files persist in Docker volumes. The initial version supports predefined scenarios and two airport brokers, not a scenario editor, automatic broker provisioning or horizontal scaling. Map tiles and the optional font need internet. Cloudflare Access is not configured by this application. A full volume reset with `docker compose down -v` permanently deletes project data and credentials; use Devtools for ordinary resets.
