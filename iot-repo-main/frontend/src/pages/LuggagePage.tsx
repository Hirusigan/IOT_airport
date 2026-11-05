import { useDashboard } from '../context/DashboardProvider';
import { useLuggageSearch } from '../hooks/useLuggageSearch';
import { LuggageSearchForm } from '../components/LuggageSearchForm';
import { LuggageHistory } from '../components/LuggageHistory';
import { TripVisualization } from '../components/TripVisualization';
export function LuggagePage() {
  const { query, setQuery, searched, setSearched, revision, airports } = useDashboard();
  const { bag, error, retry } = useLuggageSearch(searched, revision);
  const search = () => { const id = query.trim(); if (!id) return; if (id === searched) retry(); else setSearched(id); };
  return <section className="panel"><h2>Find a bag</h2><LuggageSearchForm query={query} change={setQuery} search={search}/>
    {error && <p role="alert">{error}</p>}{searched && !bag && !error && <p>Loading luggage details…</p>}
    {bag && <><TripVisualization key={searched} bag={bag} airports={airports}/><LuggageHistory id={searched} bag={bag}/></>}
  </section>;
}
