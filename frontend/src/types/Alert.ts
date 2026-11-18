import type { AlertType } from './AlertType';
import type { AlertSeverity } from './AlertSeverity';
export interface Alert {
  id: string;
  rule: AlertType;
  journeyId: string;
  flightId: string;
  airportId: string;
  severity: AlertSeverity;
  active: boolean;
  createdAt: string;
  resolvedAt: string | null;
}
