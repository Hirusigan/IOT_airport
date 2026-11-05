# Small luggage-tracking showcase

This independent milestone demonstrates **C++ tracking points → Mosquitto MQTT → ASP.NET Core → PostgreSQL → React** at one airport. The full application in the parent directory is unchanged.

## Run

With Docker running and the external `ingress` network available, build locally from this directory:

```sh
docker compose up --build -d --wait
```

Open [the dashboard](http://localhost:8081) and click **Start demo**. Three newly identified bags pass through check-in, security, and sorting over 30 seconds. Select a bag to see its stored observations. The page refreshes every two seconds. Previous demos remain visible.

**Wipe database** asks for confirmation and clears only showcase luggage observations and history, keeping the schema and infrastructure. It is disabled while a demo is running; the API also rejects wipes during active demos and blocks new demos while wiping. Successful wipes reset the page selection and demo status to idle. Current luggage state is sorted by latest scan time, newest first.

Only one demo runs at a time. Completed means the publisher received all nine MQTT acknowledgments; ingestion and dashboard polling may finish shortly afterward. Publisher errors appear on the page. Restarting the backend cancels an active publisher and returns demo status to idle; stored observations remain and the run is not resumed.

```sh
docker compose logs -f backend mqtt
docker compose stop
docker compose up -d --wait
```

To remove **only this showcase's** containers and stored data, run from `showcase/`:

```sh
docker compose down -v
```

The Compose project is named `luggage-showcase`, with independent data volumes and an internal application network. Database and MQTT ports are internal. Port 8081 serves the UI and proxies API requests. The frontend also joins the external `ingress` network under the unique alias `luggage-showcase`, allowing the existing Cloudflare tunnel to reach it independently of the full system. Credentials and anonymous MQTT are for this classroom showcase; no host database or broker ports are published.

## Docker Hub and Raspberry Pi deployment

From `showcase/` on your development PC, publish all three showcase images for AMD64 and ARM64:

```sh
docker login --username andreasfrost1004
docker buildx bake --push
```

Compose starts or pulls containers; `buildx bake --push` builds and uploads the images. Showcase image names are separate from the original application's images. Broker configuration and the C++ publisher are bundled into the published images, so the Pi needs only this `compose.yaml`, Docker, and the external `ingress` network.

Copy `compose.yaml` into a dedicated folder on the Pi. If `ingress` does not already exist, create it once with `docker network create ingress`. Then run from that folder:

```sh
docker compose pull
docker compose up --no-build -d --wait
```

Repeat those commands after publishing updates. Volumes preserve history. No source code, `mosquitto.conf`, SDKs, or build tools are needed on the Pi. Both platforms use the same image tags; Docker chooses the matching architecture automatically. Services restart after a reboot unless explicitly stopped.

Connect your existing `cloudflared` container to `ingress` and point the showcase hostname's Service URL to `http://luggage-showcase:80`. The frontend service is named `showcase-frontend` to avoid sharing the original application's `frontend` alias. Apply the same Cloudflare Access protection as the full application, since demo and wipe controls have no application login.

## MQTT contract

The showcase uses the original [MQTT envelope and topic format](../docs/contract.md): `airport/{airportId}/{deviceType}/{deviceId}/{eventType}`, QoS 1, retain false. It subscribes to `airport/AAR/scanner/+/luggage`. The three tracking points publish on scanner IDs `scanner-001`, `scanner-002`, and `scanner-003`.

```json
{
  "schemaVersion": 1,
  "eventId": "fb199f98-347b-49d0-84fb-644e09d0180b",
  "simulationRunId": "f408585137824bbd8501be87edc75b97",
  "occurredAt": "2026-10-10T10:00:00Z",
  "airportId": "AAR",
  "deviceId": "scanner-001",
  "eventType": "luggage",
  "data": {
    "luggageId": "BAG-f408585137824bbd8501be87edc75b97-1",
    "journeyId": "f408585137824bbd8501be87edc75b97-JOURNEY-1",
    "sequence": 1,
    "stage": "checkin"
  }
}
```

The demonstration publishes the original stages `checkin`, `security`, and `sorted`; `sorted` appears as Sorting on the dashboard. Run and journey IDs stay stable throughout a demo. `flightId` is optional and omitted because this milestone has no flights. Envelope version, topic/payload identities, known scanner IDs, UTC times, positive sequences, and luggage/journey IDs are validated. Invalid messages are logged and rejected.

Only the MQTT contract is shared: the showcase maps luggage messages to its small observation model and does not require the full system's flight/journey manifest import. It does not handle departure, loaded, or arrived events. Dashboard API field names and existing stored history remain unchanged. Duplicate event IDs do not add history. Older sequences enter history without moving a bag backward. Current state is the highest sequence, with receipt time breaking ties and event ID providing a stable final ordering.

| Endpoint | Behavior |
| --- | --- |
| `GET /api/luggage` | Latest observation for each bag |
| `GET /api/luggage/{id}` | Current observation and ordered history; 404 for unknown bags |
| `POST /api/demo` | Start the bundled C++ publisher; 202 on start, 409 when running, 503 while MQTT is unavailable |
| `GET /api/demo` | Idle, running, completed, or failed; demo ID and error when applicable |
| `POST /api/database/wipe` | Clear showcase observations; 409 while a demo or another wipe is running |
| `GET /health` | Database/MQTT readiness; 503 until ready |

There are no flights, rules, alerts, maps, bridges, or import manifests. This is a single-backend demonstration with best-effort ingestion, not a durable delivery guarantee across database failures. The backend creates the initial showcase schema automatically; it does not run the parent application's migrations.

## Verification and development

```sh
dotnet test tests/Tests.csproj
cd frontend
npm ci
npm run build
```

Tests use a relational SQLite database to check persistence, deduplication, sequence ordering, validation, and API responses, plus controlled publishers for concurrency and failure reporting. The Docker smoke check exercises real PostgreSQL, MQTT, and the C++ executable:

```powershell
./scripts/smoke.ps1
```

The smoke check adds two demo runs and restarts the stack to verify persistence. It does not clear data. It expects the stack already running on port 8081.

To verify ARM64 image builds from `showcase/` on a PC with Docker's ARM emulation enabled:

```sh
docker buildx build --platform linux/arm64 -f backend/Dockerfile -t luggage-showcase-backend:arm64 --load .
docker buildx build --platform linux/arm64 -t luggage-showcase-frontend:arm64 --load frontend
```

These commands build separate test tags; ordinary Compose builds use the host architecture.

For development, configure `ConnectionStrings__Database`, `Mqtt__Host`, `Mqtt__Port`, and `Publisher__Path` before running the backend. The frontend development server needs a same-origin API proxy; the supported showcase launch is Compose.
