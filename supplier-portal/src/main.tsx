import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import App from './App';
import './index.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter
      future={{
        v7_startTransition: true,
        v7_relativeSplatPath: true,
      }}
    >
      <App />
      {/* Bottom-right so toasts never cover the header or page action buttons */}
      <Toaster
        position="bottom-right"
        gutter={10}
        toastOptions={{
          duration: 4000,
          style: {
            fontFamily: 'Nunito, system-ui, sans-serif',
            fontWeight: 700,
            fontSize: '14px',
            color: '#3A3A5E',
            borderRadius: '18px',
            padding: '12px 16px',
            border: '1px solid #E8ECF8',
            boxShadow: '0 16px 40px -12px rgba(37,68,154,0.25)',
          },
          success: { iconTheme: { primary: '#078C50', secondary: '#fff' } },
          error: { iconTheme: { primary: '#E02526', secondary: '#fff' } },
        }}
      />
    </BrowserRouter>
  </React.StrictMode>
);
