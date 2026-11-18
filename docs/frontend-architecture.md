# Frontend architecture

The React application retains its existing appearance and backend contracts. `main.tsx` mounts `App`, which composes BrowserRouter, DashboardProvider and the route configuration.

Routes are defined in `src/routes/AppRoutes.tsx`: `/` is Overview, `/luggage` is Luggage, `/alerts` is Alerts, `/statistics` is Statistics and `/devtools` is Devtools. Unknown paths redirect to Overview. AppLayout owns the sidebar, page heading, connection indicator and shared loading/error display. nginx already serves index.html for direct route requests.

Top-level `pages` compose UI components and hooks. `components` contains the Leaflet map, airport cards/details, shared alert table, luggage form/history and shared statistics cards. Small headings and wrappers stay in pages. Styles remain in style.css, with navigation selectors adjusted for links.

`services/apiClient.ts` owns the shared Axios instance with `/api` as its base URL. Airport, luggage, alert and statistics services return typed response data. IDs are encoded in services. Luggage response payloads are parsed before rendering, and luggage 404 responses retain the existing message. HTTP and SignalR requests stay out of UI components.

`services/liveUpdatesService.ts` owns the single SignalR connection, subscriptions, reconnect callbacks and ten-second polling schedule. DashboardProvider owns shared airport/alert/statistics data, refresh error and connection state. It also preserves selected airport, luggage query/search and active-alert filter across navigation. These values reset on a full browser reload, as before.

`useAirportDetails` and `useLuggageSearch` own feature request state. They call services and refresh when the provider's revision changes. Superseded and unmounted requests are canceled; stale responses cannot replace current results. Feature errors appear in their own panels. Pages refetch details when revisited while preserving selection/search values.

`types` has one main type or interface per file, including explicit Statistics fields and alert enums using the backend's existing string values. `helpers/formatDateTime.ts` shares date presentation. No global state library or generic frontend repository abstraction is introduced.

After a luggage search, TripVisualization selects an explicitly grouped trip and composes TripMap with the existing history. Different runs remain separate. The luggage API includes ordered legs, assigned flights, loading evidence and observed flight details; luggageService retains those fields and parses observation payloads. buildTripView derives display state from the backend's latest accepted sequence per leg. Historical observations remain in the timeline and do not drive movement.

TripMap uses the existing Leaflet dependency and tile source, bundled SVG airport/suitcase icons, numbered curved flight connections and airport popups. Planned routes are dashed. Wrong-flight loading is shown in red. useLuggageAnimation smoothly moves between event-derived positions, cancels on unmount and honors reduced motion. A departed flight's midpoint means arrival is unconfirmed; transfer positions are schematic offsets around the airport. Neither routes nor luggage positions are live GPS tracks. Selecting a trip fits its bounds; ordinary data refreshes preserve pan/zoom. Overview retains its airport status map, with an accessible React X button for closing airport details.

Verification: local TypeScript/Vite build and Docker production image build pass. Browser checks cover all four routes, airport details, luggage history, alert filtering, browser history and a direct-route reload. SignalR connects successfully. Vite reports a bundle-size advisory for the main JavaScript chunk; this refactor does not add code splitting. No database or full-pipeline test harness was added.
