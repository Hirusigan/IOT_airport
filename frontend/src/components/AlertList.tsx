import { FiCheckCircle } from 'react-icons/fi';
import type { Alert } from '../types/Alert';
import { formatDateTime as time } from '../helpers/formatDateTime';
import './styles/AlertList.css';

function formatRule(rule: string) {
  const spaced = rule.replace(/_/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export function AlertList({ alerts }: { alerts: Alert[] }) {
  return alerts.length ? (
    <div className="alert-table-wrap">
      <table className="alert-table">
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
            <tr key={a.id} className={a.active ? '' : 'is-resolved'}>
              <td>
                <span className={`alert-badge ${a.severity.toLowerCase()}`}>
                  {formatRule(a.rule)}
                </span>
              </td>
              <td>
                <span className="alert-airport">{a.airportId}</span>
              </td>
              <td className="alert-identifier" title={a.journeyId}>
                {a.journeyId.split(':').at(-1)}
              </td>
              <td>
                <div className="alert-status">
                  <span className={`alert-status-label ${a.active ? 'active' : 'resolved'}`}>
                    <span className="alert-status-dot" aria-hidden="true" />
                    {a.active ? 'Active' : 'Resolved'}
                  </span>
                  {a.resolvedAt && (
                    <small className="alert-resolved-time">{time(a.resolvedAt)}</small>
                  )}
                </div>
              </td>
              <td className="alert-time">{time(a.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <div className="alert-empty">
      <FiCheckCircle aria-hidden="true" />
      <p>No alerts to show.</p>
    </div>
  );
}