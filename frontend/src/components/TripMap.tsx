import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import airportIconUrl from '../assets/airport.svg';
import luggageIconUrl from '../assets/luggage.svg';
import type { TripView } from '../types/TripView';
import { useLuggageAnimation } from '../hooks/useLuggageAnimation';

const airportIcon = L.icon({
  iconUrl: airportIconUrl,
  iconSize: [40, 40],
  iconAnchor: [20, 20],
});
const luggageIcon = L.icon({
  iconUrl: luggageIconUrl,
  iconSize: [36, 36],
  iconAnchor: [18, 18],
});
const label = (text: string) => {
  const element = document.createElement('span');
  element.textContent = text;
  return element;
};

export function TripMap({ view }: { view: TripView }) {
  const host = useRef<HTMLDivElement>(null);
  const map = useRef<L.Map | null>(null);
  const layers = useRef<L.LayerGroup | null>(null);
  const suitcase = useRef<L.Marker | null>(null);
  const fittedTrip = useRef('');
  const position = useLuggageAnimation(
    view.id,
    view.positionKey,
    view.position,
  );
  // Polling refreshes must not recreate unchanged markers or close an open airport popup.
  const layersKey = JSON.stringify([
    view.id,
    view.airports.map(({ id, name, latitude, longitude }) => ({
      id,
      name,
      latitude,
      longitude,
    })),
    view.legs,
    view.transferAirport?.id,
    view.deviation,
  ]);
  useEffect(() => {
    map.current = L.map(host.current!).setView([56, 11.6], 7);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 18,
    }).addTo(map.current);
    layers.current = L.layerGroup().addTo(map.current);
    return () => {
      map.current?.remove();
      map.current = null;
      suitcase.current = null;
      fittedTrip.current = '';
    };
  }, []);
  useEffect(() => {
    const instance = map.current;
    if (!instance || !layers.current) return;
    let transferLine: L.Polyline | null = null;
    const transferRoute = () => {
      if (!view.transferAirport) return [];
      const center = instance.latLngToLayerPoint([
        view.transferAirport.latitude,
        view.transferAirport.longitude,
      ]);
      return [-24, 0, 24].map((x) =>
        instance.layerPointToLatLng(
          center.add(L.point(x, x === 0 ? -38 : -24)),
        ),
      );
    };
    const draw = () => {
      layers.current!.clearLayers();
      for (const { leg, route, color, status } of view.legs) {
        if (!route.length) continue;
        const text = `Leg ${leg.order}: ${leg.flight.number} · ${leg.flight.origin} → ${leg.flight.destination} · ${status}`;
        L.polyline(route, {
          color,
          weight: 4,
          opacity: 0.85,
          dashArray: status === 'Planned' ? '8 8' : undefined,
        })
          .addTo(layers.current!)
          .bindTooltip(label(text));
        L.marker(route[16], {
          icon: L.divIcon({
            className: 'route-number',
            html: String(leg.order),
            iconSize: [24, 24],
            iconAnchor: [12, 12],
          }),
        })
          .addTo(layers.current!)
          .bindTooltip(label(text));
      }
      if (view.deviation?.route.length)
        L.polyline(view.deviation.route, {
          color: '#df5353',
          weight: 6,
          dashArray: '4 6',
        })
          .addTo(layers.current!)
          .bindTooltip(label(view.deviation.label));
      for (const airport of view.airports) {
        const marker = L.marker([airport.latitude, airport.longitude], {
          icon: airportIcon,
          alt: `${airport.id} airport`,
          title: airport.name,
        }).addTo(layers.current!);
        marker.bindTooltip(label(airport.id), {
          permanent: true,
          direction: 'bottom',
          offset: [0, 12],
        });
        const flights = view.legs.filter(
          (l) =>
            l.leg.flight.origin === airport.id ||
            l.leg.flight.destination === airport.id,
        );
        marker.bindPopup(
          label(
            `${airport.id} · ${airport.name}. ${flights.map((l) => `${l.leg.flight.number}: ${l.leg.flight.origin} → ${l.leg.flight.destination}`).join('; ')}`,
          ),
        );
      }
      if (view.transferAirport) {
        transferLine = L.polyline(transferRoute(), {
          color: '#172c3d',
          weight: 2,
          dashArray: '3 4',
        })
          .addTo(layers.current!)
          .bindTooltip(
            label('Schematic transfer: arriving flight → next flight'),
          );
      }
    };
    const fit = () => {
      if (!view.airports.length) return;
      const points = [
        ...view.airports.map(
          (a) => [a.latitude, a.longitude] as [number, number],
        ),
        ...view.legs.flatMap((l) => l.route),
      ];
      instance.fitBounds(L.latLngBounds(points), {
        padding: [60, 60],
        maxZoom: 8,
        animate: false,
      });
      fittedTrip.current = view.id;
    };
    if (fittedTrip.current !== view.id) fit();
    draw();
    const updateTransfer = () => {
      transferLine?.setLatLngs(transferRoute());
    };
    instance.on('zoomend', updateTransfer);
    instance.on('resize', fit);
    return () => {
      instance.off('zoomend', updateTransfer);
      instance.off('resize', fit);
    };
  }, [layersKey]);
  useEffect(() => {
    const instance = map.current;
    if (!instance) return;
    if (!position) {
      suitcase.current?.remove();
      suitcase.current = null;
      return;
    }
    const place = () => {
      const point = instance
        .latLngToLayerPoint([position.latitude, position.longitude])
        .add(L.point(position.offsetX, position.offsetY));
      const coordinates = instance.layerPointToLatLng(point);
      if (!suitcase.current)
        suitcase.current = L.marker(coordinates, {
          icon: luggageIcon,
          zIndexOffset: 1000,
          alt: 'Luggage location',
        }).addTo(instance);
      suitcase.current
        .setLatLng(coordinates)
        .setTooltipContent(label(view.label));
      if (!suitcase.current.getTooltip())
        suitcase.current.bindTooltip(label(view.label));
    };
    place();
    instance.on('zoomend', place);
    return () => {
      instance.off('zoomend', place);
    };
  }, [position, view.label]);
  return (
    <div
      ref={host}
      className="map trip-map"
      aria-label="Trip airports, flight connections and schematic luggage movement"
    />
  );
}
