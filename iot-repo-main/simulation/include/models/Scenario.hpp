//
// Created by dkand on 02/09/2026.
//

#ifndef SIMULATION_SCENARIO_H
#define SIMULATION_SCENARIO_H
#include <vector>

#include "Airport.hpp"
#include "Flight.hpp"
#include "Luggage.hpp"
#include "SimulationEvent.hpp"
#include "Device.hpp"

struct Scenario {
    std::vector<Airport> airports;
    std::vector<Device> devices;
    std::vector<Flight> flights;
    std::vector<Luggage> luggage;
    std::vector<SimulationEvent> events;
};
#endif //SIMULATION_SCENARIO_H
