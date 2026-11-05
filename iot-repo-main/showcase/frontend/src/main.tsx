import { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './style.css';

type Observation = { eventId: string; bagId: string; airportId: string; trackingPointId: string; sequence: number; occurredAt: string; receivedAt: string };
type Demo = { status: string; demoId?: string; error?: string };
type Details = { current: Observation; observations: Observation[] };
const labels: Record<string, string> = { checkin: 'Check-in', security: 'Security', sorting: 'Sorting' };

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(path, options);
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.error ?? `Request failed (${response.status})`);
  }
  return response.json();
}

function App() {
  const [bags, setBags] = useState<Observation[]>([]);
  const [demo, setDemo] = useState<Demo>({ status: 'idle' });
  const [selected, setSelected] = useState<string | null>(null);
  const [details, setDetails] = useState<Details | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const [wiping, setWiping] = useState(false);

  useEffect(() => {
    if (wiping) return;
    let active = true;
    let timer: ReturnType<typeof setTimeout>;
    setDetails(null);
    async function refresh() {
      try {
        const [nextBags, nextDemo, nextDetails] = await Promise.all([
          request<Observation[]>('/api/luggage'), request<Demo>('/api/demo'),
          selected ? request<Details>(`/api/luggage/${encodeURIComponent(selected)}`) : Promise.resolve(null),
        ]);
        if (active) { setBags(nextBags); setDemo(nextDemo); setDetails(nextDetails); setError(null); }
      } catch (e) { if (active) setError((e as Error).message); }
      finally { if (active) timer = setTimeout(refresh, 2000); }
    }
    void refresh();
    return () => { active = false; clearTimeout(timer); };
  }, [selected, wiping]);

  async function wipe() {
    if (!window.confirm('Wipe all showcase luggage and observation history? This cannot be undone.')) return;
    setWiping(true);
    try {
      await request('/api/database/wipe', { method: 'POST' });
      setSelected(null); setDetails(null); setBags([]); setDemo({ status: 'idle' }); setError(null);
    } catch (e) { setError((e as Error).message); }
    finally { setWiping(false); }
  }

  async function start() {
    setStarting(true);
    try { setDemo(await request<Demo>('/api/demo', { method: 'POST' })); setError(null); }
    catch (e) { setError((e as Error).message); }
    finally { setStarting(false); }
  }

  return <main>
    <header><div><h1>IOT: Demo</h1></div>
      <div className="actions">
        <button onClick={start} disabled={starting || wiping || demo.status === 'running'}>{starting ? 'Starting…' : demo.status === 'running' ? 'Demo running…' : 'Start demo'}</button>
        <button className="wipe" onClick={wipe} disabled={starting || wiping || demo.status === 'running'}>{wiping ? 'Wiping…' : 'Wipe database'}</button>
      </div>
    </header>
    <section className="route" aria-label="Tracking points"><span>01 · Check-in</span><span>02 · Security</span><span>03 · Sorting</span></section>
    <p role="status">Demo: <strong>{demo.status}</strong> · Three bags, approximately 30 seconds</p>
    {error && <p className="error" role="alert">{error}</p>}
    {demo.error && <p className="error" role="alert">{demo.error}</p>}
    <section><h2>Current luggage state <small>{bags.length} bags</small></h2>
      {!bags.length ? <p className="empty">Start a demo to see bags move through the airport.</p> :
        <div className="table"><table><thead><tr><th>Bag</th><th>Airport</th><th>Latest tracking point</th><th>Scan time</th></tr></thead>
          <tbody>{bags.map(bag => <tr key={bag.bagId} className={selected === bag.bagId ? 'selected' : ''}>
            <td><button className="bag" onClick={() => setSelected(bag.bagId)} aria-pressed={selected === bag.bagId}>{bag.bagId}</button></td>
            <td>{bag.airportId}</td><td>{labels[bag.trackingPointId]}</td><td>{new Date(bag.occurredAt).toLocaleTimeString()}</td>
          </tr>)}</tbody></table></div>}
    </section>
    <section><h2>Observation history</h2>
      {!selected ? <p>Select a bag to inspect its stored scans.</p> : !details ? <p>Loading history…</p> : <>
        <p className="identifier">{selected}</p>
        <ol>{details.observations.map(scan => <li key={scan.eventId}><strong>{labels[scan.trackingPointId]}</strong>
          <span>Sequence {scan.sequence} · Scanned {new Date(scan.occurredAt).toLocaleString()} · Stored {new Date(scan.receivedAt).toLocaleTimeString()}</span></li>)}</ol>
      </>}
    </section>
  </main>;
}

createRoot(document.getElementById('root')!).render(<App />);
