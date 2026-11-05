# Trips and map visualization

A Trip groups ordered Journey flight legs for one bag in one simulation run. Each leg retains its single Assignment and existing loading/alert rules. Journey.TripId and LegOrder identify membership and consecutive order starting at one. Airports between consecutive flights must connect, with increasing scheduled departures. There is no passenger account or independent passenger itinerary in this version.

The AddTrips migration creates one Trip per existing Journey and sets its leg order to one before installing the new foreign key and unique order index. Old import manifests without TripId keep that behavior. New manifests can group legs by supplying a shared TripId and explicit LegOrder. Trip IDs, like journey/flight IDs, must be unique across runs; the simulator scopes them to its generated run UUID.

RunImportService validates itinerary membership and connectivity before writing. EventValidationService accepts observations for a later leg only after the previous leg has confirmed arrival at its destination, a recorded departure and timely loading evidence. This uses the existing serialized ingestion gate. Messages rejected before their prerequisite arrives are diagnostic failures; the system does not add a durable retry mechanism.

GET /api/luggage/{id} retains journeys, assignments, states and events, and adds flights and trips. Each trip contains id, runId, luggageId and ordered legs (journeyId, order, flight and loadedAt). Flights include assigned flights and any flights referenced in the bag's observation history, allowing the map to identify wrong-flight loading. Controllers depend on services, which obtain data through repositories.

The Luggage page displays a trip map above the existing history. Only backend states drive the suitcase; historical-sequence observations remain in the timeline. Connections are drawn from airport coordinates and labelled by leg/flight. Departed flights are represented at a schematic route midpoint until arrival is observed. Transfers move between illustrative arriving/outgoing positions around the same airport, without inventing gate coordinates. Initial loads and trip switches snap to current state, subsequent changes animate; reduced-motion settings suppress animation.

The Connecting flights Devtools scenario uses BAG-TRANSFER with AAR → CPH → AAR. It uses the two existing brokers and does not require new credentials or broker services. No new map package, API key or frontend routing structure is needed.
