//
// Created by dkand on 02/09/2026.
//

#ifndef SIMULATION_LUGGAGE_HPP
#define SIMULATION_LUGGAGE_HPP
#include <string>

struct Luggage {
    std::string luggageId;
    std::string journeyId;
    std::string flightId;
    std::string tripId;
    int legOrder = 1;
};
#endif //SIMULATION_LUGGAGE_HPP
