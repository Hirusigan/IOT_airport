import type { Airport } from './Airport';
import type { MapPosition } from './MapPosition';
import type { TripLeg } from './TripLeg';
export interface TripView {
  id: string;
  airports: Airport[];
  legs: {
    leg: TripLeg;
    route: [number, number][];
    color: string;
    status: string;
  }[];
  position: MapPosition | null;
  positionKey: string;
  label: string;
  transferAirport: Airport | null;
  deviation: { route: [number, number][]; label: string } | null;
  missingAirports: string[];
}
