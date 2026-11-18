cmake_minimum_required(VERSION 3.20)

project(simulation LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(FetchContent)

FetchContent_Declare(
        json
        URL https://github.com/nlohmann/json/releases/download/v3.12.0/json.tar.xz
)

FetchContent_MakeAvailable(json)
find_package(PkgConfig REQUIRED)
pkg_check_modules(MOSQUITTO REQUIRED IMPORTED_TARGET libmosquitto)
find_package(Threads REQUIRED)

add_executable(simulation
        src/main.cpp
        src/scenarioloader.cpp
        src/scheduler.cpp
        src/publisher.cpp

        include/scenarioloader.hpp
        include/models/Airport.hpp
        include/models/Flight.hpp
        include/models/SimulationEvent.hpp
        include/models/Luggage.hpp
        include/models/Scenario.hpp
)

target_include_directories(simulation PRIVATE include)

target_link_libraries(simulation PRIVATE nlohmann_json::nlohmann_json PkgConfig::MOSQUITTO Threads::Threads)
enable_testing()
add_test(NAME scenario_validation COMMAND simulation --validate ${CMAKE_CURRENT_SOURCE_DIR}/scenarios/scenario.json)
add_executable(scenario_tests tests/scenario_tests.cpp src/scenarioloader.cpp src/scheduler.cpp)
target_include_directories(scenario_tests PRIVATE include)
target_link_libraries(scenario_tests PRIVATE nlohmann_json::nlohmann_json)
add_test(NAME scenario_contract COMMAND scenario_tests ${CMAKE_CURRENT_SOURCE_DIR}/scenarios/scenario.json)
