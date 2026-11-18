//
// Created by dkand on 02/09/2026.
//

#ifndef SIMULATION_AIRPORT_H
#define SIMULATION_AIRPORT_H
#include <string>

struct Airport {
    std::string code;
    std::string name;
    double latitude;
    double longitude;
    std::string host;
    int port;
};
#endif //SIMULATION_AIRPORT_H
