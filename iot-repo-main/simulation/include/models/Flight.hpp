//
// Created by dkand on 02/09/2026.
//

#ifndef SIMULATION_FLIGHT_HPP
#define SIMULATION_FLIGHT_HPP
#include <string>

struct Flight {
    std::string flightId;
    std::string number;
    std::string origin;
    std::string destination;
    int departureTime;
};
#endif //SIMULATION_FLIGHT_HPP
