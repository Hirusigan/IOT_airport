# Backend architecture

Controllers handle HTTP routing and response codes, and call service interfaces. Services own airport/schedule validation, event ordering/loading evidence, first-check-in record creation, alert lifecycle, and dashboard status. Services depend on repository interfaces; EF queries stay inside repositories.

`BaseRepository<T>` shares Add/AddRange and protected lookup/query helpers. Concrete repositories define domain-specific operations and composite-key lookups. Dashboard queries use no tracking; mutations use tracked entities. Alert lookup includes pending additions so repeated evaluation cannot add duplicate alerts before saving.

Repositories share the scoped EF `TrackingDbContext` and `IUnitOfWork`. The singleton processing gate serializes schedule registration, ingestion, airport registration, and timer evaluation. Notifications follow successful commits.

`AirportService` registers flight schedules and airport metadata. New airport metadata triggers `IAirportBrokerProvisioner`, which writes an airport-scoped Mosquitto config and ACL to a shared Docker volume and starts a fixed Mosquitto image through Docker.DotNet on the private broker network. `SimulationRunner` preserves the backend Devtools API while proxying start/stop and status to Airport Simulation over HTTP.

`EventProcessingService` validates and stores MQTT observations. A first check-in includes the planned flight ID; within the same transaction the backend creates the trip, journey, and assignment needed by downstream state and alert processing. Departure messages update actual flight departure. Later trip legs still require confirmed arrival and departure/loading evidence for the preceding leg.

`DependencyInjection.AddLuggageBackend` registers scoped repositories/services/persistence, singleton clock/settings/gates/broker health/provisioner, the SignalR adapter, and hosted MQTT/deadline/simulation services. `DatabaseInitializer` applies the existing schema migrations; `DatabaseSeeder` idempotently creates missing default AAR/CPH airports and devices. No database schema change was required for the new event flow.

`DTO` contains transport contracts and response shapes. `Models` contains entities and alert enums. `Helpers` contains JSON parsing, metadata validation and alert string mapping. `Infrastructure` and `Hubs` contain transport adapters and shared runtime state.
