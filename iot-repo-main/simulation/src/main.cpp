#include "scenarioloader.hpp"
#include "scheduler.hpp"
#include "publisher.hpp"
#include <iostream>
#include <fstream>

int main(int argc, char** argv) {
    try {
        if (argc == 3 && std::string(argv[1]) == "--validate") {
            auto s = ScenarioLoader::load(argv[2]);
            std::cout << "Valid scenario: " << s.events.size() << " events\n";
        } else if (argc == 4 && std::string(argv[1]) == "--prepare") {
            auto run = prepare(ScenarioLoader::load(argv[2]));
            std::ofstream output(argv[3]);
            if (!output) throw std::runtime_error("Cannot write prepared run");
            output << run.dump(2);
            std::cout << "Prepared " << run.at("manifest").at("simulationRunId") << "\n";
        } else if (argc == 3 && std::string(argv[1]) == "--publish") {
            std::ifstream input(argv[2]); nlohmann::json run; input >> run;
            publishRun(run);
        } else throw std::runtime_error("Usage: simulation --validate scenario.json | --prepare scenario.json run.json | --publish run.json");
    } catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
