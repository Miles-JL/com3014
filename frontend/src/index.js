// frontend/src/index.js
import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import notificationService from './services/notificationService';

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);

// Register service worker and handle notifications
if ('serviceWorker' in navigator) {
  window.addEventListener('load', async () => {
    try {
      const registration = await navigator.serviceWorker.register('/service-worker.js');
      console.log('ServiceWorker registration successful');
      
      // Initialize notification service with the service worker registration
      notificationService.serviceWorkerRegistration = registration;
      
      // Check if user is already logged in
      const token = localStorage.getItem('token');
      if (token) {
        try {
          // Extract user ID from token (adjust based on your JWT structure)
          const payload = JSON.parse(atob(token.split('.')[1]));
          const userId = payload.sub || payload.userId;
          if (userId) {
            notificationService.setUserId(userId);
          }
        } catch (error) {
          console.error('Error parsing token:', error);
        }
      }
    } catch (error) {
      console.error('ServiceWorker registration failed:', error);
    }
  });
}

// Report web vitals
reportWebVitals();
