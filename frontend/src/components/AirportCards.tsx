import type { Airport } from '../types/Airport';
export function AirportCards({ airports, select }: { airports: Airport[]; select: (id: string) => void }) {
  return <div className="airport-cards">{airports.map(a => <button className="airport-card" key={a.id} onClick={() => select(a.id)}><span className={`dot ${a.businessStatus}`}/><div><strong>{a.id}</strong><span>{a.name}</span></div><small>Telemetry: {a.telemetryHealth}<br/>{a.alertCount} active alerts</small></button>)}</div>;
}
