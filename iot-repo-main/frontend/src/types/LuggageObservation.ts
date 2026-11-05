import type { Observation } from './Observation';
export interface LuggageObservation extends Observation { stage: string; sequence: number; flightId: string | null }
