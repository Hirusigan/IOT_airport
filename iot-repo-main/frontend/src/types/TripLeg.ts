import type { Flight } from './Flight';
export interface TripLeg { journeyId: string; order: number; flight: Flight; loadedAt: string | null }
