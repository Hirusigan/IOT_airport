# Backend architecture

Controllers handle HTTP routing and response codes, and call service interfaces. Services own validation, event ordering/loading evidence, import decisions, alert lifecycle and dashboard status. Services depend on repository interfaces; EF queries stay inside repositories.

`BaseRepository<T>` shares Add/AddRange and protected lookup/query helpers. Concrete repositories define domain-specific operations and composite-key lookups. Dashboard queries use no tracking; mutations use tracked entities. Alert lookup includes pending additions so repeated evaluation cannot add duplicate alerts before saving.

Repositories share a scoped `Store` and `IUnitOfWork`. Services control transaction boundaries and saves across repositories. The singleton processing gate retains serialization across imports, ingestion, registration and timer evaluation. Notifications follow successful commits.

`DependencyInjection.AddLuggageBackend` registers scoped repositories/services/persistence, singleton clock/settings/gate/broker health, and the SignalR notification adapter. Hosted services create a scope and delegate to services. `Program.cs` configures startup and maps endpoints. `DatabaseInitializer` applies migrations; `DatabaseSeeder` owns idempotent creation of missing default airports/devices.

`DTO` contains transport contracts and response shapes. `Models` contains entities and alert enums. `Helpers` contains JSON parsing, metadata validation and alert string mapping. `Infrastructure` and `Hubs` contain transport adapters and shared runtime state.

AlertType and AlertSeverity map to existing database and API strings: at-risk, missed-flight, wrong-flight, unexpected-airport, orange and red. Existing migrations and frontend contracts are retained.

Service unit tests mock repositories, transactions and notifications. They exercise business decisions without connecting to a database or broker.
