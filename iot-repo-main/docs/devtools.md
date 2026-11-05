# Devtools and Docker deployment

Start with `docker compose up --build -d`, then operate the application through `/devtools`. Bootstrap generates configuration into a named volume before any dependent service starts. PostgreSQL reads its password from that volume; the backend reads generated JSON; brokers read generated password/ACL/bridge files. Configuration is reused on subsequent starts. No Docker socket is mounted into the application.

The backend Docker image compiles and bundles the existing C++ simulator. SimulationRunner is a singleton hosted service with one queued/running job. It reserves a job before returning HTTP 202, preventing duplicate starts. It prepares a uniquely named run file, imports through IRunImportService in a fresh scope, and publishes through airport brokers. Process arguments are passed separately and scenario IDs are selected from a fixed catalog. Requesting Stop cancels the job, kills the child process if needed, and waits for it to exit.

Run status is persisted in `/runs/status.json`. Restarting a backend during a run marks it Interrupted; publishing is not automatically resumed. Prepared files remain in the simulation volume. Published-event progress measures MQTT acknowledgments, not completed database writes.

DevtoolsService serializes start, stop, clear and seed with a separate operation gate. Clear stops the runner before acquiring the ingestion/rules processing gate. DevtoolsRepository records existing run IDs in RetiredRuns and truncates application tables in one transaction. Those tombstones remain across clears and restarts; ingestion ignores their queued messages and manifest import rejects them. The schema and EF migration history remain intact. Clear does not purge broker persistence or prepared files.

DatabaseSeeder implements IDatabaseSeeder and adds only missing default registrations. Seed and clear notifications are emitted after successful operations. Startup also seeds missing defaults. The frontend clears selected airport/luggage IDs after reset, and shared SignalR refreshes update dashboard counts.

The frontend route, page, hook, Axios service and shared types follow the existing frontend architecture. Devtools polls status while its page is mounted. A confirmation panel explains the irreversible database action; controls disable during operations and during active runs where appropriate.

For a Pi 5, use 64-bit Ubuntu Server and Docker Compose v2. All base image manifests include Linux ARM64; building on the Pi selects its native architecture. Native Pi startup/performance has not been tested here. First builds need internet and compile C++ and .NET; later builds reuse Docker layers.

The dashboard publishes no host port. The frontend joins the existing external `ingress` Docker network, allowing a cloudflared container on that network to reach `http://frontend:80`. Tunnel/Cloudflare Access configuration is managed separately; no tunnel token or cloud account setup is embedded in this repository. The application has no login, so restrict access before exposing the Devtools endpoints publicly.

When upgrading an existing installation, keep its generated .env on first startup to preserve credentials for existing volumes. Once runtime-config is initialized, editing .env does not rotate stored credentials. Keep runtime-config together with the database and broker volumes. Ordinary database reset is performed from Devtools and preserves configuration.
