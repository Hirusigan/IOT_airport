import type { AirportDetails } from '../types/AirportDetails';
import { formatDateTime as time } from '../helpers/formatDateTime';
import { AlertList } from './AlertList';
export function AirportDetailsPanel({ id, details, error, close }: { id: string; details: AirportDetails | null; error: string; close: () => void }) {
  return <section className="panel"><div className="panel-heading"><h2>{details?.airport.name ?? id}</h2><button type="button" className="close-button" onClick={close} aria-label="Close airport details"><svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/></svg></button></div>
    {error && <p role="alert">{error} Retrying automatically.</p>}
    {details ? <><h3>Flights</h3><div className="table-wrap"><table><thead><tr><th>Flight</th><th>Route</th><th>Scheduled</th><th>Actual departure</th></tr></thead><tbody>{details.flights.map(f => <tr key={f.id}><td>{f.number}</td><td>{f.origin} → {f.destination}</td><td>{time(f.scheduledDeparture)}</td><td>{f.actualDeparture ? time(f.actualDeparture) : 'Pending'}</td></tr>)}</tbody></table></div><h3>Alert history</h3><AlertList alerts={details.alerts}/></> : !error && <p>Loading airport details…</p>}
  </section>;
}
