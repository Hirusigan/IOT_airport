import type { BusinessStatus } from './BusinessStatus';
import type { TelemetryHealth } from './TelemetryHealth';
export interface Airport {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  regionId: string;
  businessStatus: BusinessStatus;
  telemetryHealth: TelemetryHealth;
  alertCount: number;
}
