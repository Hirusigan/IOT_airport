import { FiSend } from 'react-icons/fi';
import type { AirportDetails } from '../types/AirportDetails';
import { formatDateTime as time } from '../helpers/formatDateTime';
import { AlertList } from './AlertList';
import './styles/Dashboard.css';
import './styles/AirportDetailsPanel.css';

export function AirportDetailsPanel({
  id,
  details,
  error,
  close,
}: {
  id: string;
  details: AirportDetails | null;
  error: string;
  close: () => void;
}) {
  return (
    <section className="panel airport-details">
      <div className="panel-heading">
        <h2>{details?.airport.name ?? id}</h2>
        <button
          type="button"
          className="close-button"
          onClick={close}
          aria-label="Close airport details"
        >
          <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
            <path
              d="m6 6 12 12M18 6 6 18"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
            />
          </svg>
        </button>
      </div>

      {error && (
        <p className="airport-details-error" role="alert">
          {error} Retrying automatically.
        </p>
      )}

      {details ? (
        <>
          <div className="airport-details-section">
            <div className="airport-details-subheading">
              <h3>Flights</h3>
              <span className="airport-details-count">
                {details.flights.length}
              </span>
            </div>

            {details.flights.length ? (
              <div className="flight-table-wrap">
                <table className="flight-table">
                  <thead>
                    <tr>
                      <th>Flight</th>
                      <th>Route</th>
                      <th>Scheduled</th>
                      <th>Actual departure</th>
                      <th>Scheduled arrival</th>
                      <th>Actual arrival</th>
                    </tr>
                  </thead>
                  <tbody>
                    {details.flights.map((f) => (
                      <tr key={f.id}>
                        <td className="flight-number">{f.number}</td>
                        <td>
                          <span className="flight-route">
                            <span className="flight-airport">{f.origin}</span>
                            <span className="flight-arrow" aria-hidden="true">
                              →
                            </span>
                            <span className="flight-airport">
                              {f.destination}
                            </span>
                          </span>
                        </td>
                        <td className="flight-time">
                          {time(f.scheduledDeparture)}
                        </td>
                        <td>
                          {f.actualDeparture ? (
                            <span className="flight-time">
                              {time(f.actualDeparture)}
                            </span>
                          ) : (
                            <span className="flight-pill pending">Pending</span>
                          )}
                        </td>
                        <td>
                          {f.scheduledArrival ? (
                            <span className="flight-time">
                              {time(f.scheduledArrival)}
                            </span>
                          ) : (
                            <span className="flight-pill muted">
                              Not scheduled
                            </span>
                          )}
                        </td>
                        <td>
                          {f.actualArrival ? (
                            <span className="flight-time">
                              {time(f.actualArrival)}
                            </span>
                          ) : (
                            <span className="flight-pill pending">Pending</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="flight-empty">
                <FiSend aria-hidden="true" />
                <p>No flights recorded for this airport.</p>
              </div>
            )}
          </div>

          <div className="airport-details-section">
            <div className="airport-details-subheading">
              <h3>Alert history</h3>
              <span className="airport-details-count">
                {details.alerts.length}
              </span>
            </div>
            <AlertList alerts={details.alerts} />
          </div>
        </>
      ) : (
        !error && (
          <p className="airport-details-loading">Loading airport details…</p>
        )
      )}
    </section>
  );
}