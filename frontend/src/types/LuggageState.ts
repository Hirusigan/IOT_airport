export interface LuggageState {
  journeyId: string;
  sequence: number;
  stage: string;
  airportId: string;
  flightId: string | null;
  occurredAt: string;
}
