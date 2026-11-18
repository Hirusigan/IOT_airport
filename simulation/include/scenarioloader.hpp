//
// Created by dkand on 04/09/2026.
//

#ifndef SIMULATION_SCENARIOLOADER_HPP
#define SIMULATION_SCENARIOLOADER_HPP

#include <string>

#include "models/Scenario.hpp"
class ScenarioLoader {
public:
    static Scenario load(const std::string& path);
};


#endif //SIMULATION_SCENARIOLOADER_HPP
