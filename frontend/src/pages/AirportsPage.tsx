import { useState, type ChangeEvent, type SubmitEvent } from 'react';
import { airportService } from '../services/airportService';
import { requestError } from '../services/apiClient';
import './styles/AirportsPage.css';

const INITIAL_FORM = {
  id: '',
  name: '',
  latitude: '',
  longitude: '',
};

type StatusState = {
  type: 'idle' | 'success' | 'error';
  text: string;
};

export function AirportsPage() {
  const [form, setForm] = useState(INITIAL_FORM);
  const [status, setStatus] = useState<StatusState>({ type: 'idle', text: '' });
  const [busy, setBusy] = useState(false);

  function handleChange(field: keyof typeof INITIAL_FORM) {
    return (event: ChangeEvent<HTMLInputElement>) => {
      const value = event.target.value;
      setForm((prev) => ({ ...prev, [field]: value }));
    };
  }

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setStatus({ type: 'idle', text: '' });

    try {
      await airportService.register({
        id: form.id.trim().toUpperCase(),
        name: form.name.trim(),
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
        regionId: 'EU',
      });
      setStatus({
        type: 'success',
        text: 'Airport registered and broker started.',
      });
      setForm(INITIAL_FORM);
    } catch (error) {
      setStatus({
        type: 'error',
        text: requestError(error),
      });
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel">
      <h2>Register airport</h2>
      <p>
        Registration stores airport metadata and starts its Mosquitto broker.
      </p>
      <form onSubmit={submit} className="airport-form">
        <label>
          Airport code
          <input
            id="airport-code"
            maxLength={3}
            pattern="[A-Za-z]{3}"
            placeholder="e.g. BLL"
            value={form.id}
            onChange={handleChange('id')}
          />
        </label>
        <label>
          Name
          <input
            id="airport-name"
            required
            maxLength={120}
            placeholder="e.g. Billund Airport"
            value={form.name}
            onChange={handleChange('name')}
          />
        </label>
        <label>
          Latitude
          <input
            id="airport-lat"
            required
            type="number"
            step="any"
            min="-90"
            max="90"
            placeholder="e.g. 55.740"
            value={form.latitude}
            onChange={handleChange('latitude')}
          />
        </label>
        <label>
          Longitude
          <input
            id="airport-lon"
            required
            type="number"
            step="any"
            min="-180"
            max="180"
            placeholder="e.g. 9.152"
            value={form.longitude}
            onChange={handleChange('longitude')}
          />
        </label>
        <button type="submit" className="primary" disabled={busy}>
          {busy ? 'Registering…' : 'Register airport'}
        </button>
      </form>
      {status.text && (
        <p role="status" data-status={status.type}>
          {status.text}
        </p>
      )}
    </section>
  );
}