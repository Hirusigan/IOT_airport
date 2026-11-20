import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import type { Airport } from '../types/Airport';
import './styles/Maps.css';

export function AirportMap({
  airports,
  select,
}: {
  airports: Airport[];
  select: (id: string) => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const markers = useRef<L.LayerGroup | null>(null);
  const fitted = useRef(false);
  useEffect(() => {
    map.current = L.map(host.current!, { attributionControl: false }).setView(
      [56, 11.6],
      7,
    );
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '',
      maxZoom: 18,
    }).addTo(map.current);
    markers.current = L.layerGroup().addTo(map.current);
    return () => {
      map.current?.remove();
      map.current = null;
      fitted.current = false;
    };
  }, []);
  useEffect(() => {
    if (!map.current || !markers.current) return;

    markers.current.clearLayers();

    const validAirports = airports.filter(
      (airport) =>
        Number.isFinite(airport.latitude) &&
        Number.isFinite(airport.longitude),
    );

    for (const airport of validAirports) {
      const label = document.createElement('button');
      label.className = 'map-label';
      label.textContent = `${airport.id} · ${airport.name} · telemetry ${airport.telemetryHealth}`;
      label.onclick = () => select(airport.id);

      const marker = L.circleMarker(
        [airport.latitude, airport.longitude],
        {
          radius: 12,
          color: airport.telemetryHealth === 'healthy' ? '#fff' : '#536377',
          weight: 3,
          fillOpacity: 1,
          fillColor:
            { green: '#23a57a', orange: '#e79b23', red: '#df5353' }[
              airport.businessStatus
            ] ?? '#7a8799',
        },
      ).addTo(markers.current);

      marker.bindTooltip(label, { direction: 'top' });
      marker.on('click', () => select(airport.id));
    }

    if (!fitted.current && validAirports.length) {
      map.current.fitBounds(
        L.latLngBounds(
          validAirports.map((airport) => [
            airport.latitude,
            airport.longitude,
          ]),
        ),
        { padding: [60, 60], maxZoom: 8 },
      );

      fitted.current = true;
    }
  }, [airports, select]);
  return <div className="map" ref={host} aria-label="Airport status map" />;
}
