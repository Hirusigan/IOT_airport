export function StatisticsCards({
  items,
  overview = false,
}: {
  items: { label: string; value: number | string }[];
  overview?: boolean;
}) {
  return (
    <div className={overview ? 'metrics' : 'statistics'}>
      {items.map((item) => (
        <div key={item.label}>
          <strong>{item.value}</strong>
          <span>{item.label}</span>
        </div>
      ))}
    </div>
  );
}
