// frontend/public/service-worker.js
self.addEventListener('push', function(event) {
    if (!(self.Notification && self.Notification.permission === 'granted')) {
      return;
    }
  
    const data = event.data.json();
    const title = data.title || 'New Notification';
    const options = {
      body: data.body,
      icon: data.icon || '/logo192.png',
      data: {
        url: data.url || '/'
      }
    };
  
    event.waitUntil(
      self.registration.showNotification(title, options)
    );
  });
  
  self.addEventListener('notificationclick', function(event) {
    event.notification.close();
    event.waitUntil(
      clients.openWindow(event.notification.data.url)
    );
  });