import type { LuggageDetails } from '../types/LuggageDetails';
import { formatDateTime as time } from '../helpers/formatDateTime';
export function LuggageHistory({ id, bag }: { id: string; bag: LuggageDetails }) {
  return <><h3>{id} · current state per run</h3>{bag.states.map(s => <p key={s.journeyId}><span className="badge">{s.stage}</span> {s.airportId} · sequence {s.sequence} · <small>{s.journeyId}</small></p>)}<h3>Chronological observations</h3><ol className="timeline">{bag.events.map(e => <li key={e.eventId}><strong>{e.stage}</strong> · {e.airportId}<span>{time(e.occurredAt)} · sequence {e.sequence} · {e.outcome}</span></li>)}</ol></>;
}
