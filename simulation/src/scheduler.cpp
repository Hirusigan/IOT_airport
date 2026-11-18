#include "scheduler.hpp"
#include <chrono>
#include <iomanip>
#include <sstream>
#include <random>

using json = nlohmann::json;
static std::string uuid() {
    std::random_device source; std::uniform_int_distribution<int> digit(0, 15);
    std::string id = "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx";
    for (char& c : id) { if (c == 'x') c = "0123456789abcdef"[digit(source)]; else if (c == 'y') c = "89ab"[digit(source) % 4]; }
    return id;
}
static std::string utc(std::chrono::system_clock::time_point time) {
    auto seconds = std::chrono::system_clock::to_time_t(time);
    std::tm value{};
#ifdef _WIN32
    gmtime_s(&value, &seconds);
#else
    gmtime_r(&seconds, &value);
#endif
    std::ostringstream output; output << std::put_time(&value, "%Y-%m-%dT%H:%M:%SZ"); return output.str();
}
json prepare(const Scenario& s) {
    auto start = std::chrono::time_point_cast<std::chrono::seconds>(std::chrono::system_clock::now()) + std::chrono::seconds(15);
    const auto runId = uuid();
    auto scoped = [&](const std::string& id) { return runId + ":" + id; };
    json manifest = {{"simulationRunId", runId}, {"startAt", utc(start)}, {"airports", json::array()},
        {"devices", json::array()}, {"flights", json::array()}, {"journeys", json::array()}, {"assignments", json::array()}};
    json endpoints = json::array(), events = json::array();
    for (const auto& a : s.airports) {
        manifest["airports"].push_back({{"id", a.code}, {"name", a.name}, {"latitude", a.latitude}, {"longitude", a.longitude}, {"regionId", "EU"}});
        endpoints.push_back({{"airportId", a.code}, {"host", a.host}, {"port", a.port}});
    }
    for (const auto& d : s.devices) manifest["devices"].push_back({{"id", d.id}, {"airportId", d.airportId}, {"type", d.type}});
    for (const auto& f : s.flights) manifest["flights"].push_back({{"id", scoped(f.flightId)}, {"runId", runId}, {"number", f.number},
        {"origin", f.origin}, {"destination", f.destination}, {"scheduledDeparture", utc(start + std::chrono::seconds(f.departureTime))}});
    for (const auto& b : s.luggage) {
        manifest["journeys"].push_back({{"id", scoped(b.journeyId)}, {"runId", runId}, {"luggageId", b.luggageId},
            {"tripId", scoped(b.tripId)}, {"legOrder", b.legOrder}});
        manifest["assignments"].push_back({{"journeyId", scoped(b.journeyId)}, {"flightId", scoped(b.flightId)}});
    }
    for (const auto& e : s.events) {
        auto at = utc(start + std::chrono::seconds(e.time));
        json data;
        if (e.eventType == "departure") data = {{"flightId", scoped(e.flightId)}, {"departedAt", at}};
        else {
            data = {{"luggageId", e.luggageId}, {"journeyId", scoped(e.journeyId)}, {"sequence", e.sequence}, {"stage", e.checkpoint}};
            if (!e.flightId.empty()) data["flightId"] = scoped(e.flightId);
        }
        auto type = e.eventType == "departure" ? "gate" : "scanner";
        events.push_back({{"offset", e.time}, {"topic", "airport/" + e.airport + "/" + type + "/" + e.deviceId + "/" + e.eventType},
            {"envelope", {{"schemaVersion", 1}, {"eventId", uuid()}, {"simulationRunId", runId}, {"occurredAt", at},
                {"airportId", e.airport}, {"deviceId", e.deviceId}, {"eventType", e.eventType}, {"data", data}}}});
    }
    auto epoch = std::chrono::duration_cast<std::chrono::seconds>(start.time_since_epoch()).count();
    return {{"manifest", manifest}, {"startEpochSeconds", epoch}, {"endpoints", endpoints}, {"events", events}};
}
