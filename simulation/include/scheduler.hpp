#pragma once
#include "models/Scenario.hpp"
#include <nlohmann/json.hpp>
nlohmann::json prepare(const Scenario& scenario);
