# Simulation and MQTT contract

Airport Simulation controls a run and sends its flight schedule and source devices to `POST /api/airports/schedule` before publishing starts. The schedule includes a fresh UUID `simulationRunId`, flights with run-scoped IDs, UTC `scheduledDeparture`, and the devices used by the scenario. Airport records are registered separately through `POST /api/airports` or supplied by the default AAR/CPH seeder. At departure time Airport Simulation records actual departure directly through `POST /api/airports/flights/{flightId}/departure`; the matching gate event is also retained in the MQTT event history. There is no run manifest or run-registration request.

Airport Simulation calls the IoT Device Simulator for each timed event. The IoT Device Simulator publishes to its airport's local broker. Each airport broker bridges only `airport/{airportId}/#` outward to `regional`; the backend subscribes to `airport/#` on the regional broker.

Topics use `airport/{airportId}/{deviceType}/{deviceId}/{eventType}`. Messages use QoS 1, retain false, and schemaVersion 1. Envelopes contain eventId (UUID), simulationRunId, occurredAt (UTC), airportId, deviceId, eventType, and data.

Luggage data contains luggageId, journeyId, sequence (positive per journey), stage (`checkin`, `security`, `sorted`, `loaded`, or `arrived`), optional flightId, optional tripId, and legOrder. A first `checkin` must contain the planned flightId. The backend validates the flight/run and creates its trip, journey, and assignment with the accepted observation. Later luggage events use that assignment; `loaded` events identify the observed flight. For later trip legs, the first check-in is accepted only after the preceding leg has confirmed arrival and departure/loading evidence.

Departure data contains flightId and departedAt (UTC); the backend records actual departure on the scheduled flight. Flight and device identities must exist before their events. Unknown identities and invalid topic/payload combinations are rejected and recorded as diagnostics.

The backend stores registered airports/devices, run-scoped flights, trips, journeys, assignments, raw accepted events, current luggage state, and alerts. Event IDs and alert keys are unique. Older sequences remain in history without replacing current state. Notifications follow transaction commit and prompt clients to refetch authoritative snapshots.

Mosquitto access is anonymous with ACL topic rules. Airport ACLs allow their own `airport/{CODE}/#` namespace and regional ACLs cover `airport/#`. These rules limit topics but do not authenticate a broker's claimed client ID. Brokers remain on the private Docker network without host-published ports.
