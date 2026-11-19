import './styles/Luggage.css';

export function LuggageSearchForm({
  query,
  change,
  search,
}: {
  query: string;
  change: (value: string) => void;
  search: () => void;
}) {
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        search();
      }}
    >
      <label htmlFor="luggage">Luggage ID</label>
      <div className="search">
        <input
          id="luggage"
          value={query}
          onChange={(e) => change(e.target.value)}
          placeholder="e.g. BAG-001"
        />
        <button className="primary">Search</button>
      </div>
    </form>
  );
}
