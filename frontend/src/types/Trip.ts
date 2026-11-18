import type { TripLeg } from './TripLeg';
export interface Trip { id: string; runId: string; luggageId: string; legs: TripLeg[] }
