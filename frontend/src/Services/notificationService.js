// frontend/src/services/notificationService.js
class NotificationService {
    constructor() {
      this.serviceWorkerRegistration = null;
      this.publicKey = 'BCq5GrOpY0KYXBqJT9ygAfw_2YrWcRmV5ePlQX8Gk1eqvka_C3HuZKcUqfaRYrjKUPlPWkxg0GZWmLfJCAOOJKU';
      this.userId = null;
    }
  
    setUserId(userId) {
      this.userId = userId;
      // Try to subscribe when user ID is set
      if (this.shouldAutoSubscribe()) {
        this.requestNotificationPermission();
      }
    }
  
    shouldAutoSubscribe() {
      // Only auto-subscribe if we have a user ID and haven't been explicitly denied
      if (!this.userId) return false;
      return Notification.permission === 'default' || Notification.permission === 'granted';
    }
  
    async requestNotificationPermission() {
      if (!('Notification' in window)) {
        console.warn('This browser does not support notifications');
        return false;
      }
  
      if (Notification.permission === 'granted') {
        return true;
      }
  
      if (Notification.permission === 'denied') {
        console.warn('Notification permission was previously denied');
        return false;
      }
  
      try {
        const permission = await Notification.requestPermission();
        if (permission === 'granted' && this.userId) {
          await this.subscribeUser();
        }
        return permission === 'granted';
      } catch (error) {
        console.error('Error requesting notification permission:', error);
        return false;
      }
    }
  
    async subscribeUser() {
      if (!this.userId) {
        console.error('Cannot subscribe: No user ID set');
        return false;
      }
  
      if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        console.warn('Push notifications are not supported by this browser');
        return false;
      }
  
      try {
        this.serviceWorkerRegistration = await navigator.serviceWorker.ready;
        
        // Check for existing subscription
        let subscription = await this.serviceWorkerRegistration.pushManager.getSubscription();
        
        // If no existing subscription, create a new one
        if (!subscription) {
          subscription = await this.serviceWorkerRegistration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: this.urlBase64ToUint8Array(this.publicKey)
          });
        }
  
        // Send the subscription to the server with user context
        const response = await fetch('http://localhost:5201/api/Notification/push/subscribe', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${localStorage.getItem('token')}`
          },
          body: JSON.stringify({
            ...subscription.toJSON(),
            userId: this.userId  // Include user ID in the subscription
          })
        });
  
        if (!response.ok) {
          throw new Error('Failed to subscribe to push notifications');
        }
  
        return true;
      } catch (error) {
        console.error('Error subscribing to push notifications:', error);
        return false;
      }
    }
  
    async unsubscribeUser() {
      if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        return false;
      }
  
      try {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        
        if (subscription) {
          await subscription.unsubscribe();
          
          // Notify the server about the unsubscription
          await fetch('http://localhost:5201/api/Notification/push/unsubscribe', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify(subscription.toJSON())
          });
        }
        
        return true;
      } catch (error) {
        console.error('Error unsubscribing from push notifications:', error);
        return false;
      }
    }
  
    async sendTestNotification() {
      try {
        const response = await fetch('http://localhost:5201/api/Notification/test', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${localStorage.getItem('token')}`
          }
        });
  
        if (!response.ok) {
          throw new Error('Failed to send test notification');
        }
  
        return true;
      } catch (error) {
        console.error('Error sending test notification:', error);
        return false;
      }
    }
  
    urlBase64ToUint8Array(base64String) {
      const padding = '='.repeat((4 - base64String.length % 4) % 4);
      const base64 = (base64String + padding)
        .replace(/\-/g, '+')
        .replace(/_/g, '/');
  
      const rawData = window.atob(base64);
      const outputArray = new Uint8Array(rawData.length);
  
      for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
      }
      return outputArray;
    }
  }
  
  // Export a singleton instance
  const notificationService = new NotificationService();
  export default notificationService;