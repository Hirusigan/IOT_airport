import type { Airport } from '../types/Airport';
import type { LuggageDetails } from '../types/LuggageDetails';
import type { Trip } from '../types/Trip';
import type { TripView } from '../types/TripView';
import type { MapPosition } from '../types/MapPosition';

const colors = ['#247f77', '#4269bc', '#9b5fb5', '#bc712a'];
const position = (
  airport: Airport,
  offsetX = 0,
  offsetY = -24,
): MapPosition => ({
  latitude: airport.latitude,
  longitude: airport.longitude,
  offsetX,
  offsetY,
});

// Schematic connections, not aircraft tracks. Opposite directions bend to opposite sides.
export function flightRoute(
  origin: Airport,
  destination: Airport,
): [number, number][] {
  let endLongitude = destination.longitude;
  if (endLongitude - origin.longitude > 180) endLongitude -= 360;
  if (endLongitude - origin.longitude < -180) endLongitude += 360;
  const dx = endLongitude - origin.longitude,
    dy = destination.latitude - origin.latitude;
  return Array.from({ length: 33 }, (_, i) => {
    const t = i / 32,
      bend = Math.sin(Math.PI * t) * 0.12;
    return [
      origin.latitude + dy * t + dx * bend,
      origin.longitude + dx * t - dy * bend,
    ];
  });
}

export function buildTripView(
  trip: Trip,
  bag: LuggageDetails,
  airports: Airport[],
): TripView {
  const byId = new Map(
    airports
      .filter(
        (a) => Number.isFinite(a.latitude) && Number.isFinite(a.longitude),
      )
      .map((a) => [a.id, a]),
  );
  const states = new Map(bag.states.map((s) => [s.journeyId, s]));
  const legs = trip.legs.map((leg, i) => {
    const origin = byId.get(leg.flight.origin),
      destination = byId.get(leg.flight.destination);
    const state = states.get(leg.journeyId);
    const loaded =
      leg.loadedAt !== null &&
      Date.parse(leg.loadedAt) <=
        Date.parse(leg.flight.actualDeparture ?? leg.flight.scheduledDeparture);
    const arrived =
      state?.stage === 'arrived' &&
      state.airportId === leg.flight.destination &&
      loaded &&
      leg.flight.actualDeparture !== null &&
      Date.parse(state.occurredAt) >= Date.parse(leg.flight.actualDeparture);
    const status = arrived
      ? 'Arrived'
      : loaded && leg.flight.actualDeparture
        ? 'Departed'
        : loaded
          ? 'Loaded'
          : 'Planned';
    return {
      leg,
      route: origin && destination ? flightRoute(origin, destination) : [],
      color: colors[i % colors.length],
      status,
    };
  });
  const ids = [
    ...new Set(
      trip.legs.flatMap((l) => [l.flight.origin, l.flight.destination]),
    ),
  ];
  const view: TripView = {
    id: trip.id,
    airports: ids.flatMap((id) => (byId.has(id) ? [byId.get(id)!] : [])),
    legs,
    position: null,
    positionKey: `${trip.id}:planned`,
    label: 'Awaiting the first luggage observation.',
    transferAirport: null,
    deviation: null,
    missingAirports: ids.filter((id) => !byId.has(id)),
  };
  // State is the latest accepted sequence per leg; historical observations stay in the history only.
  let activeIndex = -1;
  trip.legs.forEach((leg, index) => {
    if (states.has(leg.journeyId)) activeIndex = index;
  });
  if (activeIndex < 0) return view;
  const leg = trip.legs[activeIndex],
    state = states.get(leg.journeyId)!;
  const airport = byId.get(state.airportId);
  view.positionKey = `${trip.id}:${leg.journeyId}:${state.sequence}:${leg.flight.actualDeparture ?? ''}`;
  view.label = `${state.stage} at ${state.airportId} · leg ${leg.order}`;
  const expectedAirport =
    state.stage === 'arrived' ? leg.flight.destination : leg.flight.origin;
  const wrongFlight =
    state.stage === 'loaded' && state.flightId !== leg.flight.id;
  const unexpectedAirport = state.airportId !== expectedAirport;
  if (airport) view.position = position(airport);
  else
    view.missingAirports = [
      ...new Set([...view.missingAirports, state.airportId]),
    ];
  if (wrongFlight || unexpectedAirport) {
    view.label = wrongFlight
      ? `Loaded onto a different flight at ${state.airportId}`
      : `Unexpected airport: ${state.airportId}`;
    if (airport && !view.airports.some((a) => a.id === airport.id))
      view.airports.push(airport);
  }
  if (state.stage === 'loaded' && state.flightId) {
    const observedFlight = bag.flights.find((f) => f.id === state.flightId);
    if (observedFlight) {
      const origin = byId.get(observedFlight.origin),
        destination = byId.get(observedFlight.destination);
      const route =
        origin && destination ? flightRoute(origin, destination) : [];
      if (wrongFlight) {
        view.deviation = {
          route,
          label: `Observed loading: ${observedFlight.number}`,
        };
        for (const a of [origin, destination])
          if (a && !view.airports.some((item) => item.id === a.id))
            view.airports.push(a);
      }
      // A midpoint denotes a departed flight with arrival unconfirmed, never a live GPS position.
      const loadedAt = wrongFlight ? state.occurredAt : leg.loadedAt;
      if (
        observedFlight.actualDeparture &&
        loadedAt &&
        Date.parse(loadedAt) <= Date.parse(observedFlight.actualDeparture) &&
        state.airportId === observedFlight.origin &&
        route.length
      ) {
        const [latitude, longitude] = route[16];
        view.position = { latitude, longitude, offsetX: 0, offsetY: 0 };
        view.positionKey += `:${observedFlight.actualDeparture}`;
        view.label = `${observedFlight.number} departed · arrival unconfirmed${wrongFlight ? ' · wrong flight' : ''}`;
      }
    }
  }
  const next = trip.legs[activeIndex + 1];
  if (airport && legs[activeIndex].status === 'Arrived' && next) {
    view.transferAirport = airport;
    view.position = position(airport, -24);
    view.label = `Arrived at ${airport.id} · awaiting transfer to ${next.flight.number}`;
  } else if (
    airport &&
    activeIndex > 0 &&
    state.airportId === leg.flight.origin &&
    state.stage !== 'arrived' &&
    !wrongFlight &&
    !unexpectedAirport &&
    legs[activeIndex - 1].status === 'Arrived' &&
    view.position?.offsetY !== 0
  ) {
    view.transferAirport = airport;
    view.position = position(airport, 24);
    if (state.stage !== 'loaded')
      view.label = `Transferring at ${airport.id} to ${leg.flight.number}`;
  }
  return view;
}
