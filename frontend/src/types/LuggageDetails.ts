import type { LuggageState } from './LuggageState';
import type { LuggageObservation } from './LuggageObservation';
import type { Trip } from './Trip';
import type { Flight } from './Flight';
export interface LuggageDetails {
  states: LuggageState[];
  events: LuggageObservation[];
  trips: Trip[];
  flights: Flight[];
}
