//
// Created by dkand on 02/09/2026.
//

#ifndef SIMULATION_SIMULATIONEVENT_HPP
#define SIMULATION_SIMULATIONEVENT_HPP
#include <string>

struct SimulationEvent {
    int time;
    std::string luggageId;
    std::string flightId;
    std::string airport;
    std::string checkpoint;
    std::string deviceId;
    std::string eventType;
    std::string journeyId;
    int sequence;
};
#endif //SIMULATION_SIMULATIONEVENT_HPP
