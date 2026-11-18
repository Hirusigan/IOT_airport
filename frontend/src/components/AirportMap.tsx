import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import type { Airport } from '../types/Airport';

export function AirportMap({ airports, select }: { airports: Airport[]; select: (id: string) => void }) {
  const host = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const markers = useRef<L.LayerGroup | null>(null);
  const fitted = useRef(false);
  useEffect(() => {
    map.current = L.map(host.current!).setView([56, 11.6], 7);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors', maxZoom: 18 }).addTo(map.current);
    markers.current = L.layerGroup().addTo(map.current);
    return () => { map.current?.remove(); map.current = null; fitted.current = false; };
  }, []);
  useEffect(() => {
    if (!map.current || !markers.current) return;
    markers.current.clearLayers();
    for (const airport of airports) {
      const label = document.createElement('button');
      label.className = 'map-label'; label.textContent = `${airport.id} · ${airport.name} · telemetry ${airport.telemetryHealth}`;
      label.onclick = () => select(airport.id);
      const marker = L.circleMarker([airport.latitude, airport.longitude], { radius: 12, color: airport.telemetryHealth === 'healthy' ? '#fff' : '#536377',
        weight: 4, dashArray: airport.telemetryHealth === 'healthy' ? undefined : '3 3', fillOpacity: 1,
        fillColor: { green: '#23a57a', orange: '#e79b23', red: '#df5353' }[airport.businessStatus] ?? '#7a8799' }).addTo(markers.current);
      marker.bindTooltip(label, { direction: 'top' }); marker.on('click', () => select(airport.id));
    }
    if (!fitted.current && airports.length) {
      map.current.fitBounds(L.latLngBounds(airports.map(a => [a.latitude, a.longitude])), { padding: [60, 60], maxZoom: 8 });
      fitted.current = true;
    }
  }, [airports, select]);
  return <div className="map" ref={host} aria-label="Airport status map" />;
}


