# Initial contract

Scenario offsets are seconds from one UTC run start. `simulation --prepare scenario.json run.json`
validates the complete scenario and writes an import manifest plus stable event envelopes.
Import that file using `POST /api/runs/import`, then `simulation --publish run.json`.
The backend simulation runner prepares the manifest, imports it through the import service, and starts the C++ publisher automatically. Each run has a new UUID;
flight and journey instance IDs are prefixed by it, luggage display IDs remain unchanged.
Replaying the same prepared file preserves event IDs and occurrence times.

Topics: `airport/{airportId}/{deviceType}/{deviceId}/{eventType}`.
QoS 1, retain false; schemaVersion 1. Envelopes contain eventId, simulationRunId,
occurredAt (UTC), airportId, deviceId, eventType and data.
Luggage data: luggageId, journeyId, sequence (positive per journey), stage
(`checkin`, `security`, `sorted`, `loaded`, `arrived`), optional flightId (required for loaded).
Departure data: flightId, departedAt (UTC).

One MQTT connection per airport, unique client ID incorporating the run and airport.
Flights and manifests must exist before observations. Unknown identities are rejected.
Loading at an origin is distinct from arrival at a destination; arrival before departure
is unexpected. The initial version supports exactly one assignment per journey.

Persistence includes registered airports/devices, run-scoped flights/journeys,
assignments with durable loading evidence, raw accepted events, current luggage state,
and alerts with unique rule/journey/flight keys and activation/resolution timestamps.
Old sequences enter history only. Notifications occur after transaction commit and prompt
clients to refetch authoritative snapshots.
