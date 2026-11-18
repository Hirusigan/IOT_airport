import type { Alert } from '../types/Alert';
import { formatDateTime as time } from '../helpers/formatDateTime';
export function AlertList({ alerts }: { alerts: Alert[] }) {
  return alerts.length ? (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Rule</th>
            <th>Airport</th>
            <th>Journey</th>
            <th>Status</th>
            <th>Created</th>
          </tr>
        </thead>
        <tbody>
          {alerts.map((a) => (
            <tr key={a.id}>
              <td>
                <span className={`badge ${a.severity}`}>{a.rule}</span>
              </td>
              <td>{a.airportId}</td>
              <td className="identifier" title={a.journeyId}>
                {a.journeyId.split(':').at(-1)}
              </td>
              <td>
                {a.active ? 'Active' : 'Resolved'}
                {a.resolvedAt && <small>{time(a.resolvedAt)}</small>}
              </td>
              <td>{time(a.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <p className="empty">No alerts to show.</p>
  );
}
