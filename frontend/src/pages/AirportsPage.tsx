import { useState, type FormEvent } from 'react';
import { airportService } from '../services/airportService';
import { requestError } from '../services/apiClient';
export function AirportsPage() {
  const [id, setId] = useState(''); const [name, setName] = useState('');
  const [latitude, setLatitude] = useState(''); const [longitude, setLongitude] = useState('');
  const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      await airportService.register({ id: id.trim().toUpperCase(), name: name.trim(), latitude: Number(latitude), longitude: Number(longitude), regionId: 'EU' });
      setMessage('Airport registered and broker started.'); setId(''); setName(''); setLatitude(''); setLongitude('');
    } catch (error) { setMessage(requestError(error)); } finally { setBusy(false); }
  }
  return <section className="panel"><h2>Register airport</h2><p>Registration stores airport metadata and starts its Mosquitto broker.</p>
    <form onSubmit={submit} className="airport-form">
      <label>Airport code<input required maxLength={3} pattern="[A-Za-z]{3}" value={id} onChange={e => setId(e.target.value)}/></label>
      <label>Name<input required maxLength={120} value={name} onChange={e => setName(e.target.value)}/></label>
      <label>Latitude<input required type="number" step="any" min="-90" max="90" value={latitude} onChange={e => setLatitude(e.target.value)}/></label>
      <label>Longitude<input required type="number" step="any" min="-180" max="180" value={longitude} onChange={e => setLongitude(e.target.value)}/></label>
      <button className="primary" disabled={busy}>{busy ? 'Registering…' : 'Register airport'}</button>
    </form>{message && <p role="status">{message}</p>}
  </section>;
}
