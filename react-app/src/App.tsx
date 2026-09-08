import ErrorBoundary from '@/components/ErrorBoundary';
import { TooltipProvider } from '@/components/ui/tooltip';
import Routes from '@/routes/Routes';

/**
 * Root Application Component
 * @component MainApp
 */
const MainApp = () => {
  return (
    <ErrorBoundary name="App">
      <TooltipProvider>
        <Routes />
      </TooltipProvider>
    </ErrorBoundary>
  );
};

export default MainApp;
