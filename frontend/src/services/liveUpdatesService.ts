import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import type { ConnectionStatus } from '../types/ConnectionStatus';

export function startLiveUpdates(
  refresh: () => void,
  status: (value: ConnectionStatus) => void,
) {
  let disposed = false;
  const connection = new HubConnectionBuilder()
    .withUrl('/hub')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();
  for (const event of [
    'AirportStatusChanged',
    'AlertChanged',
    'LuggageUpdated',
  ])
    connection.on(event, refresh);
  connection.onreconnecting(() => {
    if (!disposed) status('Reconnecting');
  });
  connection.onreconnected(() => {
    if (!disposed) {
      status('Live');
      refresh();
    }
  });
  connection.onclose(() => {
    if (!disposed) status('Disconnected');
  });
  const connect = async () => {
    if (disposed || connection.state !== HubConnectionState.Disconnected)
      return;
    try {
      await connection.start();
      if (disposed) {
        await connection.stop();
        return;
      }
      status('Live');
      refresh();
    } catch {
      if (!disposed) status('Disconnected');
    }
  };
  refresh();
  void connect();
  const poll = window.setInterval(() => {
    refresh();
    void connect();
  }, 10000);
  return () => {
    disposed = true;
    clearInterval(poll);
    void connection.stop();
  };
}
