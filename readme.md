# Airport luggage tracking

Airport Simulation (ASP.NET Core) sends flight schedules to the backend. IoT Device Simulator publishes scanner and gate events to each airport's Mosquitto broker. Airport brokers bridge `airport/<CODE>/#` to the regional broker, and the backend consumes `airport/#` from the regional broker. PostgreSQL and the backend own application data; React and SignalR display it.

## Start the server

Copy `.env.example` to `.env` and set `POSTGRES_PASSWORD`. Ensure the external `ingress` network used by your Cloudflare tunnel exists, then start the stack:

```sh
docker compose up -d --build
```

Compose runs PostgreSQL, the default `regional`, `aar`, and `cph` brokers, backend, frontend, Airport Simulation, and IoT Device Simulator. It does not publish broker or frontend ports to the host. Cloudflare/tunnel routing remains your existing deployment setup; route to `http://frontend:80` on `ingress`. Cloudflare Access is unchanged.

Keep `.env` and the `postgres-data`, `airport-broker-config`, and other named volumes when upgrading. For an existing database volume, set `POSTGRES_PASSWORD` to the password already configured for that database before starting the new stack. PostgreSQL credentials come from `.env`; broker access is anonymous and topic-limited by ACLs. The Docker socket is mounted into the backend so it can create Mosquitto containers for additional airports.

## Simulation and event flow

In **Devtools**, choose a scenario and start it. The backend preserves the existing start, stop, and status endpoints and forwards control to Airport Simulation. Airport Simulation registers flight schedules, including scheduled arrival times, and sends actual departure and arrival times directly to the backend over HTTP. A first luggage `checkin` event includes the planned flight ID; the backend creates the trip, journey, and flight assignment when it processes that event. Luggage arrival remains a scanner event over MQTT. No run manifest or run-registration request is used.

Airport Simulation sends each device event to the IoT Device Simulator over the private Compose network. The device simulator publishes anonymously to the local airport broker. The airport broker ACL permits that airport's `airport/<CODE>/#` topics, and its bridge sends those topics to the regional broker. The backend subscribes to `airport/#` there.

Airport records AAR and CPH and their default scanner/gate devices are seeded at startup. Use **Airports** to register more airport metadata; the backend writes broker config into the shared `airport-broker-config` volume and starts a Mosquitto container on the private `airport-brokers` network. Each airport broker initiates its bridge to `regional`. Broker ACLs restrict topics, but anonymous ACLs do not authenticate the broker's claimed client ID.

Simulation status includes starting/running/completed/stopped/failed and event progress. Stop cancels future events; already published events remain stored. Backend status is persisted in `simulation-runs`.

## Build and publish images

Pushes to `main` validate all four Docker images. To publish, manually run **Build and publish** from `main` and enable the Docker Hub publish option. The workflow publishes `latest` and the commit SHA tags for backend, frontend, Airport Simulation, and IoT Device Simulator, then triggers Watchtower for the corresponding application container. Configure repository secrets `DOCKER_ACCESS_TOKEN`, `WATCHTOWER_TOKEN`, and `WATCHTOWER_URL`; Docker Hub username is `andreasfrost1004`.

On the server, update the stack with:

```sh
docker compose pull
docker compose up -d
```

Watchtower runs from your separate Compose file. Only the backend, frontend, and two simulation applications carry its enable label; brokers do not.

## API

| Endpoint                           | Purpose                                           |
| ---------------------------------- | ------------------------------------------------- |
| GET /api/airports                  | Airport metadata and status                       |
| POST /api/airports                 | Register metadata and provision an airport broker |
| POST /api/airports/schedule        | Register a scenario's flight schedule and devices |
| POST /api/airports/flights/{flightId}/departure | Record actual flight departure |
| POST /api/airports/flights/{flightId}/arrival | Record actual flight arrival |
| GET /api/airports/{id}             | Flights and alert history                         |
| GET /api/luggage/{id}              | Luggage states and observations                   |
| GET /api/alerts                    | Alerts and filters                                |
| GET /api/statistics                | Persisted counts                                  |
| GET /api/devtools/scenarios        | Available scenarios                               |
| GET /api/devtools/simulation       | Current/last simulation status                    |
| POST /api/devtools/simulation      | Start a scenario                                  |
| POST /api/devtools/simulation/stop | Stop the active scenario                          |
| POST /api/devtools/database/clear  | Clear application data                            |
| POST /api/devtools/database/seed   | Add default airport/device records                |
| GET /health                        | Database/MQTT readiness                           |
| /hub                               | SignalR refresh notifications                     |

## Development notes

The backend uses .NET 10, PostgreSQL, MQTTnet, and Docker.DotNet. The Airport Simulation and IoT Device Simulator are separate .NET 10 ASP.NET Core applications. Scenario definitions are stored in `scenarios`; the old C++ publisher and Docker Bake setup are removed from the runtime/build workflow.

- [Backend architecture](docs/backend-architecture.md)
- [Frontend architecture](docs/frontend-architecture.md)
- [Devtools and Docker deployment](docs/devtools.md)
- [MQTT contract](docs/contract.md)
- [Verification notes](docs/verification.md)
