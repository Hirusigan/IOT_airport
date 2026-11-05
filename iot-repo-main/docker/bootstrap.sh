#!/bin/sh
set -eu
umask 077
if [ -f /runtime/ready ]; then exit 0; fi
mkdir -p /runtime
secret() { od -An -N24 -tx1 /dev/urandom | tr -d ' \n'; }
db="${POSTGRES_PASSWORD:-$(secret)}"
sim="${MQTT_PASSWORD:-$(secret)}"
backend="${BACKEND_MQTT_PASSWORD:-$(secret)}"
aar="${BRIDGE_AAR_PASSWORD:-$(secret)}"
cph="${BRIDGE_CPH_PASSWORD:-$(secret)}"
for value in "$db" "$sim" "$backend" "$aar" "$cph"; do
  case "$value" in *[!a-zA-Z0-9_-]*) echo 'Bootstrap passwords must contain only letters, digits, underscore or hyphen' >&2; exit 1;; esac
done
printf '%s' "$db" > /runtime/postgres-password
printf '%s' "$backend" > /runtime/backend-password
printf '%s' "$sim" > /runtime/simulation-password
printf 'backend:%s\nbridge-AAR:%s\nbridge-CPH:%s\n' "$backend" "$aar" "$cph" > /runtime/regional.passwd
printf 'user backend\ntopic read airport/#\ntopic read $SYS/#\nuser bridge-AAR\ntopic write airport/AAR/#\nuser bridge-CPH\ntopic write airport/CPH/#\n' > /runtime/regional.acl
common='listener 1883
allow_anonymous false
persistence true
persistence_location /mosquitto/data/
autosave_interval 10
max_queued_messages 1000
max_queued_bytes 1048576
max_inflight_messages 20
message_size_limit 65536
log_dest stdout'
printf '%s\npassword_file /runtime/regional.passwd\nacl_file /runtime/regional.acl\n' "$common" > /runtime/regional.conf
for airport in AAR CPH; do
  if [ "$airport" = AAR ]; then bridge="$aar"; else bridge="$cph"; fi
  printf 'sim-%s:%s\n' "$airport" "$sim" > "/runtime/$airport.passwd"
  printf 'user sim-%s\ntopic write airport/%s/scanner/scanner-001/luggage\ntopic write airport/%s/gate/gate-01/departure\ntopic read $SYS/#\nuser local-bridge-%s\ntopic read airport/%s/#\n' "$airport" "$airport" "$airport" "$airport" "$airport" > "/runtime/$airport.acl"
  printf '%s\npassword_file /runtime/%s.passwd\nacl_file /runtime/%s.acl\nconnection bridge-%s\naddress regional:1883\nremote_username bridge-%s\nremote_password %s\nlocal_clientid local-bridge-%s\nlocal_username local-bridge-%s\nremote_clientid bridge-%s\ntry_private false\nnotifications false\ncleansession false\nrestart_timeout 1 30\ntopic airport/%s/# out 1\n' "$common" "$airport" "$airport" "$airport" "$airport" "$bridge" "$airport" "$airport" "$airport" "$airport" > "/runtime/$airport.conf"
done
for file in /runtime/*.passwd; do mosquitto_passwd -U "$file"; done
printf '{"ConnectionStrings":{"Database":"Host=postgres;Database=luggage;Username=luggage;Password=%s"},"Mqtt":{"Host":"regional","Username":"backend","Password":"%s"},"Simulation":{"Password":"%s"}}\n' "$db" "$backend" "$sim" > /runtime/backend.json
chmod 644 /runtime/*
touch /runtime/ready
echo 'Runtime configuration is ready.'
