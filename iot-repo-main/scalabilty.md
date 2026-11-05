## Onboarding New Airports and Brokers

The system should allow new airports and MQTT brokers to be added without requiring changes to the backend or frontend source code. Onboarding should therefore be configuration-driven.

Each airport is registered in an **Airport Registry**, containing information such as airport code, name, coordinates, assigned MQTT broker, and whether the airport is enabled.

Example:

```text
Airport
--------------------------------
Code: FRA
Name: Frankfurt Airport
Latitude: 50.0379
Longitude: 8.5622
Broker: broker-eu-2
Enabled: true
```

MQTT brokers are registered separately in a **Broker Registry**.

```text
Broker
--------------------------------
ID: broker-eu-2
Host: mqtt-eu-2.example.com
Port: 8883
Region: Europe
Enabled: true
```

When a new broker is registered, the MQTT ingestion service establishes a connection to it and subscribes using a common topic structure such as:

```text
airport/#
```

All airports and devices must follow the same MQTT topic convention:

```text
airport/{airportId}/{deviceType}/{deviceId}/{eventType}
```

For example:

```text
airport/FRA/scanner/scanner-001/luggage
airport/FRA/gate/gate-A12/departure
airport/FRA/conveyor/conveyor-04/status
```

Because the backend subscribes using MQTT wildcards, individual airports and devices do not need separate subscriptions.

The onboarding process for a new airport is therefore:

```text
1. Register MQTT broker
        │
        ▼
2. Backend connects to broker
   and subscribes to airport/#
        │
        ▼
3. Register the airport
   and associate it with the broker
        │
        ▼
4. Configure airport devices
   with the standard MQTT topic structure
        │
        ▼
5. Devices begin publishing events
        │
        ▼
6. Backend automatically receives
   and processes the events
        │
        ▼
7. Airport is exposed through
   the backend API
        │
        ▼
8. Frontend automatically displays
   the new airport
```

The frontend should not contain hardcoded airports. Instead, it retrieves the available airports from the backend:

```http
GET /api/airports
```

This means that once an airport has been registered, it automatically becomes available to the frontend map and other views.

Live updates are distributed through SignalR. The frontend therefore does not need to know which MQTT broker or device produced an event.

For example:

```text
FRA scanner
    │
    ▼
FRA MQTT broker
    │
    ▼
MQTT ingestion
    │
    ▼
Backend / Rules Engine
    │
    ▼
SignalR
    │
    ▼
Frontend
```

If an airport already uses an existing broker, only the airport itself needs to be registered. If additional capacity or geographical separation is required, a new broker can first be registered and the new airport assigned to it.

The main principle is that **airports, brokers, and devices are treated as configuration rather than application code**. Adding a new airport should therefore not require recompiling or modifying either the backend or frontend.