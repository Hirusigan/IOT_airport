import { useDashboard } from '../context/DashboardProvider';
import { useAirportDetails } from '../hooks/useAirportDetails';
import { AirportMap } from '../components/AirportMap';
import { AirportCards } from '../components/AirportCards';
import { AirportDetailsPanel } from '../components/AirportDetailsPanel';
import { StatisticsCards } from '../components/StatisticsCards';
import { InfoTooltip } from '../components/InfoTooltip';
import { TbBuildingAirport } from "react-icons/tb";
import { FaPersonWalkingLuggage } from "react-icons/fa6";
import { RiAlarmWarningFill } from "react-icons/ri";
import "./styles/OverviewPage.css"

export function OverviewPage() {
  const { airports, statistics, selected, setSelected, revision } = useDashboard();
  const { details, error } = useAirportDetails(selected, revision);
  return (
    <>
    <div className="overview-statistics">
      <StatisticsCards
        overview
        items={[
          {
            label: 'Registered airports',
            value: airports.length,
            icon: <TbBuildingAirport/>,
            tone: 'blue',
          },
          {
            label: 'Luggage journeys',
            value: statistics?.journeys ?? '—',
            icon: <FaPersonWalkingLuggage/>,
            tone: 'amber',
          },
          {
            label: 'Active alerts',
            value: statistics?.activeAlerts ?? '—',
            icon: <RiAlarmWarningFill/>,
            tone: 'red',
          },
        ]}
      />
    </div>
      <section className="panel">
        <div className="panel-heading">
          <h2>Airport network</h2>
          <InfoTooltip label="Airport network status legend">
            <span>
              <span className="info-tooltip-dot green" aria-hidden="true" />
              Green: No handling alerts
            </span>
            <span>
              <span className="info-tooltip-dot orange" aria-hidden="true" />
              Orange: At risk
            </span>
            <span>
              <span className="info-tooltip-dot red" aria-hidden="true" />
              Red: Handling alert
            </span>
          </InfoTooltip>
        </div>
        <AirportMap airports={airports} select={setSelected} />
        <p className="map-note">
          Grey borders indicate stale, unknown, or disconnected
          telemetry. Map tiles require internet access.
        </p>
      </section>
      <AirportCards airports={airports} select={setSelected} />
      {selected && (
        <AirportDetailsPanel
          id={selected}
          details={details}
          error={error}
          close={() => setSelected(null)}
        />
      )}
    </>
  );
}
