#include "scenarioloader.hpp"
#include "scheduler.hpp"
#include <fstream>
#include <filesystem>
#include <iostream>
#include <functional>
#include <set>

int main(int argc, char** argv) {
    if (argc != 2) return 1;
    try {
        std::ifstream input(argv[1]); nlohmann::json original; input >> original;
        auto temporary = std::filesystem::temp_directory_path() / "luggage-scenario-test.json";
        int passed = 0;
        auto rejects = [&](const std::function<void(nlohmann::json&)>& change) {
            auto invalid = original; change(invalid);
            { std::ofstream out(temporary); out << invalid; }
            bool rejected = false;
            try { ScenarioLoader::load(temporary.string()); } catch (const std::exception&) { rejected = true; }
            if (!rejected) throw std::runtime_error("Invalid scenario accepted");
            ++passed;
        };
        rejects([](auto& j) { j["luggage"][0]["flightId"] = "UNKNOWN"; });
        rejects([](auto& j) { j["events"][0]["deviceId"] = "UNKNOWN"; });
        rejects([](auto& j) { j["events"][0]["luggageId"] = "UNKNOWN"; });
        rejects([](auto& j) { j["events"][0]["sequence"] = 0; });
        rejects([](auto& j) { j["events"][1]["time"] = -1; });
        rejects([](auto& j) { j["events"][0]["stage"] = "loaded"; });
        rejects([](auto& j) { j["airports"][0]["latitude"] = 100; });
        rejects([](auto& j) { j["flights"].push_back(j["flights"][0]); });
        std::filesystem::remove(temporary);
        auto scenario = ScenarioLoader::load(argv[1]);
        auto first = prepare(scenario), second = prepare(scenario);
        if (first["manifest"]["simulationRunId"] == second["manifest"]["simulationRunId"]) throw std::runtime_error("Run IDs reused");
        std::set<std::string> ids;
        for (const auto& event : first["events"]) {
            if (!ids.insert(event["envelope"]["eventId"]).second) throw std::runtime_error("Event IDs reused");
            if (event["envelope"]["occurredAt"].get<std::string>().back() != 'Z') throw std::runtime_error("Timestamp is not UTC");
        }
        auto replay = nlohmann::json::parse(first.dump());
        if (replay != first) throw std::runtime_error("Prepared replay contract changed");
        std::cout << passed << " invalid scenarios rejected; run identity, UTC and replay checks passed\n";
    } catch (const std::exception& ex) { std::cerr << ex.what() << '\n'; return 1; }
}
