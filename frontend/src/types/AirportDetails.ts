import type { Airport } from './Airport';
import type { Flight } from './Flight';
import type { Alert } from './Alert';
export interface AirportDetails { airport: Airport; flights: Flight[]; alerts: Alert[] }
