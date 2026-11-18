import { useState } from 'react';
import type { Airport } from '../types/Airport';
import type { LuggageDetails } from '../types/LuggageDetails';
import { buildTripView } from '../helpers/buildTripView';
import { formatDateTime } from '../helpers/formatDateTime';
import { TripMap } from './TripMap';

export function TripVisualization({
  bag,
  airports,
}: {
  bag: LuggageDetails;
  airports: Airport[];
}) {
  const [selectedId, setSelectedId] = useState('');
  const trip = bag.trips.find((t) => t.id === selectedId) ?? bag.trips[0];
  if (!trip) return <p className="empty">No trip itinerary is available.</p>;
  const view = buildTripView(trip, bag, airports);
  return (
    <div className="trip-visualization">
      <div className="panel-heading">
        <h3>Trip map</h3>
        {bag.trips.length > 1 && (
          <label className="trip-picker">
            Trip{' '}
            <select
              value={trip.id}
              onChange={(e) => setSelectedId(e.target.value)}
            >
              {bag.trips.map((t) => (
                <option key={t.id} value={t.id}>
                  {formatDateTime(t.legs[0].flight.scheduledDeparture)} ·{' '}
                  {t.legs[0].flight.origin} →{' '}
                  {t.legs.at(-1)!.flight.destination} · {t.id.slice(-12)}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>
      <TripMap view={view} />
      <p className="trip-status" role="status">
        {view.label}
      </p>
      <ol className="trip-legs">
        {view.legs.map(({ leg, color, status }) => (
          <li key={leg.journeyId}>
            <span className="leg-number" style={{ backgroundColor: color }}>
              {leg.order}
            </span>
            <div>
              <strong>
                {leg.flight.origin} → {leg.flight.destination}
              </strong>{' '}
              · {leg.flight.number}
              <small>
                {formatDateTime(leg.flight.scheduledDeparture)} · {status}
              </small>
            </div>
          </li>
        ))}
      </ol>
      {view.missingAirports.length > 0 && (
        <p role="alert">
          Map coordinates unavailable for {view.missingAirports.join(', ')}.
        </p>
      )}
      <p className="map-note">
        Numbered lines show planned flight connections; dashed lines are pending
        and red shows observed wrong-flight loading. The suitcase shows
        confirmed airport observations or a schematic departed-flight position.
        Transfer motion is illustrative, not GPS tracking. Map tiles require
        internet.
      </p>
    </div>
  );
}
