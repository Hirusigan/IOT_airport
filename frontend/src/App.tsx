import { BrowserRouter } from 'react-router-dom';
import { DashboardProvider } from './context/DashboardProvider';
import { AppRoutes } from './routes/AppRoutes';
export function App() { return <BrowserRouter><DashboardProvider><AppRoutes/></DashboardProvider></BrowserRouter>; }
