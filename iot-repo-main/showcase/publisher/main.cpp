#include <mosquitto.h>
#include <nlohmann/json.hpp>
#include <chrono>
#include <condition_variable>
#include <ctime>
#include <iomanip>
#include <iostream>
#include <mutex>
#include <random>
#include <sstream>
#include <stdexcept>
#include <thread>

struct Connection {
    std::mutex mutex;
    std::condition_variable changed;
    bool connected = false;
    int acknowledged = -1;
};

std::string uuid() {
    static std::mt19937 generator(std::random_device{}());
    std::uniform_int_distribution<int> byte(0, 255);
    std::ostringstream out;
    for (int i = 0; i < 16; ++i) {
        if (i == 4 || i == 6 || i == 8 || i == 10) out << '-';
        int value = byte(generator);
        if (i == 6) value = (value & 15) | 64;
        if (i == 8) value = (value & 63) | 128;
        out << std::hex << std::setw(2) << std::setfill('0') << value;
    }
    return out.str();
}

std::string utc_now() {
    const auto now = std::time(nullptr);
    std::ostringstream out;
    out << std::put_time(std::gmtime(&now), "%Y-%m-%dT%H:%M:%SZ");
    return out.str();
}

int main(int argc, char** argv) {
    if (argc != 4) { std::cerr << "Usage: publisher host port demo-id\n"; return 1; }
    mosquitto_lib_init();
    Connection state;
    const std::string client_id = "sim-" + std::string(argv[3]) + "-AAR";
    auto* client = mosquitto_new(client_id.c_str(), true, &state);
    if (!client) { mosquitto_lib_cleanup(); return 1; }
    mosquitto_connect_callback_set(client, [](mosquitto*, void* data, int code) {
        auto& s = *static_cast<Connection*>(data);
        std::lock_guard lock(s.mutex);
        s.connected = code == 0;
        s.changed.notify_all();
    });
    mosquitto_publish_callback_set(client, [](mosquitto*, void* data, int id) {
        auto& s = *static_cast<Connection*>(data);
        std::lock_guard lock(s.mutex);
        s.acknowledged = id;
        s.changed.notify_all();
    });
    bool loop_started = false;
    int result = 0;
    try {
        if (mosquitto_connect(client, argv[1], std::stoi(argv[2]), 30) != MOSQ_ERR_SUCCESS)
            throw std::runtime_error("Cannot connect to MQTT broker");
        if (mosquitto_loop_start(client) != MOSQ_ERR_SUCCESS)
            throw std::runtime_error("Cannot start MQTT network loop");
        loop_started = true;
        {
            std::unique_lock lock(state.mutex);
            if (!state.changed.wait_for(lock, std::chrono::seconds(10), [&] { return state.connected; }))
                throw std::runtime_error("MQTT connection timed out");
        }
        const char* stages[] = {"checkin", "security", "sorted"};
        const char* devices[] = {"scanner-001", "scanner-002", "scanner-003"};
        const auto start = std::chrono::steady_clock::now();
        for (int stage = 0; stage < 3; ++stage) {
            for (int bag = 1; bag <= 3; ++bag) {
                std::this_thread::sleep_until(start + std::chrono::seconds(stage * 12 + (bag - 1) * 3));
                nlohmann::json event = {
                    {"schemaVersion", 1}, {"eventId", uuid()}, {"simulationRunId", argv[3]},
                    {"occurredAt", utc_now()}, {"airportId", "AAR"}, {"deviceId", devices[stage]},
                    {"eventType", "luggage"}, {"data", {
                        {"luggageId", "BAG-" + std::string(argv[3]) + "-" + std::to_string(bag)},
                        {"journeyId", std::string(argv[3]) + "-JOURNEY-" + std::to_string(bag)},
                        {"sequence", stage + 1}, {"stage", stages[stage]}
                    }}
                };
                const auto topic = "airport/AAR/scanner/" + std::string(devices[stage]) + "/luggage";
                const auto payload = event.dump();
                int id;
                std::unique_lock lock(state.mutex);
                if (mosquitto_publish(client, &id, topic.c_str(), static_cast<int>(payload.size()),
                    payload.data(), 1, false) != MOSQ_ERR_SUCCESS)
                    throw std::runtime_error("MQTT publish failed");
                if (!state.changed.wait_for(lock, std::chrono::seconds(10), [&] { return state.acknowledged == id; }))
                    throw std::runtime_error("MQTT acknowledgment timed out");
                std::cout << payload << std::endl;
            }
        }
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; result = 1; }
    mosquitto_disconnect(client);
    if (loop_started) mosquitto_loop_stop(client, true);
    mosquitto_destroy(client);
    mosquitto_lib_cleanup();
    return result;
}
