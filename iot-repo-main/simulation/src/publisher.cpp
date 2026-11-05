#include "publisher.hpp"
#include <mosquitto.h>
#include <chrono>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <map>
#include <memory>
#include <iostream>
#include <cstdlib>
#include <stdexcept>

namespace {
struct Connection {
    mosquitto* client = nullptr;
    std::mutex mutex;
    std::condition_variable changed;
    bool connected = false;
    int acknowledged = 0;
    Connection(const std::string& id, const std::string& airport, const std::string& host, int port) {
        client = mosquitto_new(id.c_str(), false, this);
        if (!client) throw std::runtime_error("Cannot create MQTT client");
        mosquitto_connect_callback_set(client, [](mosquitto*, void* ctx, int rc) {
            auto& c = *static_cast<Connection*>(ctx); std::lock_guard lock(c.mutex); c.connected = rc == 0; c.changed.notify_all();
        });
        mosquitto_disconnect_callback_set(client, [](mosquitto*, void* ctx, int) {
            auto& c = *static_cast<Connection*>(ctx); std::lock_guard lock(c.mutex); c.connected = false; c.changed.notify_all();
        });
        mosquitto_publish_callback_set(client, [](mosquitto*, void* ctx, int mid) {
            auto& c = *static_cast<Connection*>(ctx); std::lock_guard lock(c.mutex); c.acknowledged = mid; c.changed.notify_all();
        });
        const auto* password = std::getenv("MQTT_PASSWORD");
        if (!password) { mosquitto_destroy(client); throw std::runtime_error("MQTT_PASSWORD is required"); }
        mosquitto_username_pw_set(client, ("sim-" + airport).c_str(), password);
        mosquitto_reconnect_delay_set(client, 1, 30, true);
        mosquitto_max_inflight_messages_set(client, 1);
        int rc = mosquitto_connect_async(client, host.c_str(), port, 15);
        if (rc == MOSQ_ERR_SUCCESS) rc = mosquitto_loop_start(client);
        if (rc != MOSQ_ERR_SUCCESS) { mosquitto_destroy(client); throw std::runtime_error(mosquitto_strerror(rc)); }
    }
    ~Connection() { mosquitto_disconnect(client); mosquitto_loop_stop(client, true); mosquitto_destroy(client); }
    void send(const std::string& topic, const std::string& payload) {
        std::unique_lock lock(mutex);
        if (!changed.wait_for(lock, std::chrono::seconds(120), [&] { return connected; })) throw std::runtime_error("MQTT connection timeout");
        int mid = 0;
        auto rc = mosquitto_publish(client, &mid, topic.c_str(), static_cast<int>(payload.size()), payload.c_str(), 1, false);
        if (rc != MOSQ_ERR_SUCCESS) throw std::runtime_error(mosquitto_strerror(rc));
        if (!changed.wait_for(lock, std::chrono::seconds(120), [&] { return acknowledged == mid; })) throw std::runtime_error("MQTT acknowledgment timeout; replay prepared run to retry");
    }
};
}
void publishRun(const nlohmann::json& run) {
    mosquitto_lib_init();
    {
        std::map<std::string, std::unique_ptr<Connection>> connections;
        auto runId = run.at("manifest").at("simulationRunId").get<std::string>();
        for (const auto& endpoint : run.at("endpoints")) {
            auto airport = endpoint.at("airportId").get<std::string>();
            connections[airport] = std::make_unique<Connection>("sim-" + runId + "-" + airport, airport, endpoint.at("host"), endpoint.at("port"));
        }
        auto start = std::chrono::system_clock::time_point(std::chrono::seconds(run.at("startEpochSeconds").get<long long>()));
        for (const auto& event : run.at("events")) {
            std::this_thread::sleep_until(start + std::chrono::seconds(event.at("offset").get<int>()));
            auto airport = event.at("envelope").at("airportId").get<std::string>();
            connections.at(airport)->send(event.at("topic"), event.at("envelope").dump());
            std::cout << "Published " << event.at("topic") << std::endl;
        }
    }
    mosquitto_lib_cleanup();
}
