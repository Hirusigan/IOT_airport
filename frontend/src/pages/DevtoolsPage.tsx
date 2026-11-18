import { useState } from 'react';
import { useDevtools } from '../hooks/useDevtools';
import { useDashboard } from '../context/DashboardProvider';
import { devtoolsService } from '../services/devtoolsService';
export function DevtoolsPage() {
  const { scenarios, status, error, busy, message, execute } = useDevtools();
  const { setSelected, setSearched } = useDashboard();
  const [scenario, setScenario] = useState('mixed');
  const [confirm, setConfirm] = useState(false);
  const running = status?.state === 'Starting' || status?.state === 'Running';
  return (
    <>
      <section className="panel">
        <h2>Simulation</h2>
        <p>
          Choose a scenario. Events travel through the airport brokers into the
          dashboard.
        </p>
        <label htmlFor="scenario">Scenario</label>
        <select
          id="scenario"
          value={scenario}
          disabled={busy || running}
          onChange={(e) => setScenario(e.target.value)}
        >
          {scenarios.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
        <p>{scenarios.find((s) => s.id === scenario)?.description}</p>
        <div className="devtools-actions">
          <button
            className="primary"
            disabled={busy || running || !status || !scenarios.length}
            onClick={() => {
              void execute(
                () => devtoolsService.start(scenario),
                'Simulation started.',
              );
            }}
          >
            Start simulation
          </button>
          <button
            disabled={busy || !running}
            onClick={() => {
              void execute(
                devtoolsService.stop,
                'Simulation stopped. Already imported data is retained.',
              );
            }}
          >
            Stop simulation
          </button>
        </div>
        {status && (
          <div aria-live="polite">
            <h3>Status: {status.state}</h3>
            <p>
              {status.publishedEvents} / {status.totalEvents} events published
            </p>
            {status.runId && <small>Run: {status.runId}</small>}
            {status.error && <p role="alert">{status.error}</p>}
          </div>
        )}
      </section>
      <section className="panel">
        <h2>Database</h2>
        <p>
          Seed defaults adds missing AAR/CPH airports and devices without
          removing history.
        </p>
        <div className="devtools-actions">
          <button
            disabled={busy || !status}
            onClick={() => {
              void execute(
                devtoolsService.seed,
                'Default airports and devices are ready.',
              );
            }}
          >
            Seed defaults
          </button>
          <button disabled={busy || !status} onClick={() => setConfirm(true)}>
            Clear database…
          </button>
        </div>
        {confirm && (
          <div className="reset-confirmation" role="alert">
            <p>
              This stops the active simulation and deletes all airports,
              devices, flights, journeys, observations, alerts and diagnostics.
              It cannot be undone. Seed defaults afterward to restore the two
              default airports.
            </p>
            <div className="devtools-actions">
              <button
                disabled={busy}
                onClick={() => {
                  void execute(async () => {
                    await devtoolsService.clear();
                    setSelected(null);
                    setSearched('');
                    setConfirm(false);
                  }, 'Database cleared.');
                }}
              >
                Confirm clear database
              </button>
              <button disabled={busy} onClick={() => setConfirm(false)}>
                Cancel
              </button>
            </div>
          </div>
        )}
      </section>
      {busy && <p role="status">Working…</p>}
      {message && <p role="status">{message}</p>}
      {error && (
        <div className="error" role="alert">
          {error}
        </div>
      )}
    </>
  );
}
