#include "scenarioloader.hpp"
#include <fstream>
#include <map>
#include <set>
#include <stdexcept>
#include <cmath>
#include <algorithm>
#include <nlohmann/json.hpp>

Scenario ScenarioLoader::load(const std::string& path) {
    std::ifstream file(path);
    if (!file) throw std::runtime_error("Cannot open scenario: " + path);
    nlohmann::json data; file >> data;
    Scenario s;
    std::set<std::string> airports, flights, journeys, devices;
    auto require = [](bool condition, const std::string& error) { if (!condition) throw std::runtime_error("Invalid scenario: " + error); };
    for (const auto& a : data.at("airports")) {
        Airport airport{a.at("code"), a.at("name"), a.at("latitude"), a.at("longitude"), a.at("host"), a.at("port")};
        require(airport.code.size() == 3 && airport.code.find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZ") == std::string::npos &&
            !airport.name.empty() && std::isfinite(airport.latitude) && std::isfinite(airport.longitude) &&
            std::abs(airport.latitude) <= 90 && std::abs(airport.longitude) <= 180 && !airport.host.empty() &&
            airport.port > 0 && airport.port <= 65535 && airports.insert(airport.code).second, "airport metadata/duplicate");
        s.airports.push_back(airport);
    }
    for (const auto& d : data.at("devices")) {
        Device device{d.at("id"), d.at("airportId"), d.at("type")};
        require(airports.contains(device.airportId) && !device.id.empty() && device.id.find_first_of("/+#") == std::string::npos &&
            (device.type == "scanner" || device.type == "gate") && devices.insert(device.airportId + "/" + device.id).second, "device identity/type");
        s.devices.push_back(device);
    }
    for (const auto& f : data.at("flights")) {
        Flight flight{f.at("flightId"), f.at("number"), f.at("origin"), f.at("destination"), f.at("departureTime")};
        require(!flight.flightId.empty() && flights.insert(flight.flightId).second && airports.contains(flight.origin) &&
            airports.contains(flight.destination) && flight.origin != flight.destination && flight.departureTime >= 0, "flight references/duplicate/time");
        s.flights.push_back(flight);
    }
    for (const auto& l : data.at("luggage")) {
        Luggage bag{l.at("luggageId"), l.at("journeyId"), l.at("flightId"), l.value("tripId", l.at("journeyId").get<std::string>()), l.value("legOrder", 1)};
        require(!bag.luggageId.empty() && !bag.journeyId.empty() && journeys.insert(bag.journeyId).second &&
            !bag.tripId.empty() && bag.legOrder > 0 && flights.contains(bag.flightId), "journey assignment/duplicate");
        s.luggage.push_back(bag);
    }
    std::map<std::string, std::vector<const Luggage*>> trips;
    for (const auto& bag : s.luggage) trips[bag.tripId].push_back(&bag);
    for (auto& [id, legs] : trips) {
        std::sort(legs.begin(), legs.end(), [](const auto* a, const auto* b) { return a->legOrder < b->legOrder; });
        const Flight* previousFlight = nullptr;
        for (size_t i = 0; i < legs.size(); ++i) {
            require(legs[i]->legOrder == static_cast<int>(i + 1) && legs[i]->luggageId == legs[0]->luggageId, "trip luggage/order");
            const auto& flight = *std::find_if(s.flights.begin(), s.flights.end(), [&](const auto& f) { return f.flightId == legs[i]->flightId; });
            require(previousFlight == nullptr || (previousFlight->destination == flight.origin && previousFlight->departureTime < flight.departureTime), "trip connection/order");
            previousFlight = &flight;
        }
    }
    std::map<std::string, int> sequence;
    int previous = -1;
    for (const auto& e : data.at("events")) {
        SimulationEvent event{e.at("time"), e.value("luggageId", ""), e.value("flightId", ""), e.at("airport"),
            e.value("stage", ""), e.at("deviceId"), e.at("eventType"), e.value("journeyId", ""), e.value("sequence", 0)};
        require(event.time >= 0 && event.time >= previous && airports.contains(event.airport), "event time/order/airport");
        previous = event.time;
        const Device* device = nullptr;
        for (const auto& d : s.devices) if (d.airportId == event.airport && d.id == event.deviceId) device = &d;
        require(device != nullptr, "event device reference");
        if (event.eventType == "luggage") {
            bool matched = false;
            for (const auto& l : s.luggage) if (l.journeyId == event.journeyId && l.luggageId == event.luggageId) matched = true;
            require(matched && device->type == "scanner" && event.sequence > sequence[event.journeyId], "luggage identity/sequence/device");
            sequence[event.journeyId] = event.sequence;
            require(event.checkpoint == "checkin" || event.checkpoint == "security" || event.checkpoint == "sorted" ||
                event.checkpoint == "loaded" || event.checkpoint == "arrived", "event stage");
            require((event.flightId.empty() && event.checkpoint != "loaded") || flights.contains(event.flightId), "event flight reference");
        } else {
            require(event.eventType == "departure" && device->type == "gate" && flights.contains(event.flightId), "departure event");
            for (const auto& f : s.flights) if (f.flightId == event.flightId) require(f.origin == event.airport, "departure origin");
        }
        s.events.push_back(event);
    }
    require(!s.airports.empty() && !s.flights.empty() && !s.luggage.empty() && !s.events.empty(), "empty scenario");
    return s;
}
