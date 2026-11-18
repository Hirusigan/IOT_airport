export interface Flight {
  id: string;
  number: string;
  origin: string;
  destination: string;
  scheduledDeparture: string;
  actualDeparture: string | null;
  scheduledArrival: string | null;
  actualArrival: string | null;
}
