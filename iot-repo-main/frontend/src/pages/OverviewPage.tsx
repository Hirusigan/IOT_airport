import { useDashboard } from '../context/DashboardProvider';
import { useAirportDetails } from '../hooks/useAirportDetails';
import { AirportMap } from '../components/AirportMap';
import { AirportCards } from '../components/AirportCards';
import { AirportDetailsPanel } from '../components/AirportDetailsPanel';
import { StatisticsCards } from '../components/StatisticsCards';
export function OverviewPage() {
  const { airports, statistics, selected, setSelected, revision } = useDashboard();
  const { details, error } = useAirportDetails(selected, revision);
  return <><StatisticsCards overview items={[{ label: 'Registered airports', value: airports.length }, { label: 'Luggage journeys', value: statistics?.journeys ?? '—' }, { label: 'Active alerts', value: statistics?.activeAlerts ?? '—' }]}/>
    <section className="panel"><div className="panel-heading"><h2>Airport network</h2><span className="legend">● Green: no handling alerts · Orange: at risk · Red: handling alert</span></div><AirportMap airports={airports} select={setSelected}/><p className="map-note">Dashed marker borders indicate stale, unknown, or disconnected telemetry. Map tiles require internet access.</p></section>
    <AirportCards airports={airports} select={setSelected}/>
    {selected && <AirportDetailsPanel id={selected} details={details} error={error} close={() => setSelected(null)}/>}
  </>;
}
