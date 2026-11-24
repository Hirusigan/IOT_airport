import { useState } from 'react';
import { useDevtools } from '../hooks/useDevtools';
import { useDashboard } from '../context/DashboardProvider';
import { devtoolsService } from '../services/devtoolsService';
import './styles/DevtoolsPage.css';
import {
  FiDatabase,
  FiPlay,
  FiSquare,
  FiTrash2,
  FiX,
} from 'react-icons/fi';

export function DevtoolsPage() {
  const { scenarios, status, error, busy, message, execute } = useDevtools();
  const { setSelected, setSearched } = useDashboard();
  const [scenario, setScenario] = useState('mixed');
  const [confirm, setConfirm] = useState(false);

  const running = status?.state === 'Starting' || status?.state === 'Running';
  const activeScenario = scenarios.some((s) => s.id === scenario)
    ? scenario
    : (scenarios[0]?.id ?? '');
  const selectedScenario = scenarios.find((s) => s.id === activeScenario);

  return (
    <div className="devtools">
      <section className="panel">
        <h2>Simulation</h2>
        <p className="panel-subtitle">
          Choose a scenario. Events travel through the airport brokers into the
          dashboard.
        </p>

        <div className="devtools-sim-row">
          <div className="devtools-field">
            <label htmlFor="scenario">Scenario</label>
            <select
              id="scenario"
              value={activeScenario}
              disabled={busy || running || !scenarios.length}
              onChange={(e) => setScenario(e.target.value)}
            >
              {!scenarios.length && <option value="">Select scenario</option>}
              {scenarios.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>

          <button
            className="devtools-btn accent"
            disabled={busy || running || !status || !activeScenario}
            onClick={() => {
              void execute(
                () => devtoolsService.start(activeScenario),
                'Simulation started.',
              );
            }}
          >
            <FiPlay aria-hidden="true" focusable="false" />
            Start simulation
          </button>

          <button
            className="devtools-btn warning"
            disabled={!running || busy}
            onClick={() => {
              void execute(
                devtoolsService.stop,
                'Simulation stopped. Already imported data is retained.',
              );
            }}
          >
            <FiSquare aria-hidden="true" focusable="false" />
            Stop simulation
          </button>
        </div>

        {selectedScenario?.description && (
          <p className="devtools-description">{selectedScenario.description}</p>
        )}

        {status && (
          <div className="devtools-status" aria-live="polite">
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
        <p className="panel-subtitle">
          Seed defaults adds missing AAR/CPH airports and devices without
          removing history.
        </p>

        <div className="devtools-row">
          <button
            className="devtools-btn primary"
            disabled={busy || !status}
            onClick={() => {
              void execute(
                devtoolsService.seed,
                'Default airports and devices are ready.',
              );
            }}
          >
            <FiDatabase aria-hidden="true" focusable="false" />
            Seed defaults
          </button>

          <button
            className="devtools-btn danger"
            disabled={busy || !status}
            onClick={() => setConfirm(true)}
          >
            <FiTrash2 aria-hidden="true" focusable="false" />
            Clear database
          </button>
        </div>

        {confirm && (
          <div className="reset-confirmation" role="alert">
            <p>
              This stops the active simulation and deletes all airports,
              devices, flights, journeys, observations, alerts and diagnostics.
              It also removes brokers and configuration files for airports added
              through the app. This cannot be undone. Seed defaults afterward to
              restore the two default airports.
            </p>
            <div className="devtools-actions">
              <button
                className="devtools-btn danger"
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
                <FiTrash2 aria-hidden="true" focusable="false" />
                Confirm clear database
              </button>
              <button
                className="devtools-btn neutral"
                disabled={busy}
                onClick={() => setConfirm(false)}
              >
                <FiX aria-hidden="true" focusable="false" />
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
    </div>
  );
}
