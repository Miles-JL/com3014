// frontend/src/components/NotificationButton.js
import React, { useState, useEffect } from 'react';
import { Button, message } from 'antd';
import { BellOutlined, BellFilled } from '@ant-design/icons';
import notificationService from '../services/notificationService';
import './NotificationButton.css';

const NotificationButton = () => {
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [isSupported, setIsSupported] = useState(true);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const checkSubscription = async () => {
      if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        setIsSupported(false);
        message.warning('Push notifications are not supported in this browser');
        return;
      }

      try {
        setIsLoading(true);
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        setIsSubscribed(!!subscription);
      } catch (error) {
        console.error('Error checking subscription status:', error);
        message.error('Failed to check notification status');
        setIsSupported(false);
      } finally {
        setIsLoading(false);
      }
    };

    checkSubscription();
  }, []);

  const toggleNotifications = async () => {
    if (!isSupported) {
      message.warning('Push notifications are not supported in this browser');
      return;
    }

    try {
      setIsLoading(true);
      const permission = await notificationService.requestNotificationPermission();
      
      if (permission === 'denied') {
        message.warning('Please enable notifications in your browser settings');
        return;
      }

      if (isSubscribed) {
        // Unsubscribe logic
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        
        if (subscription) {
          await subscription.unsubscribe();
          // Notify backend about unsubscription if needed
          await fetch('http://localhost:5201/api/Notification/push/unsubscribe', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify({ endpoint: subscription.endpoint })
          });
        }
        
        setIsSubscribed(false);
        message.success('Notifications disabled');
      } else {
        const subscribed = await notificationService.subscribeUser();
        setIsSubscribed(subscribed);
        if (subscribed) {
          message.success('Notifications enabled!');
        }
      }
    } catch (error) {
      console.error('Error toggling notifications:', error);
      message.error('Failed to update notification settings');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isSupported) {
    return null;
  }

  return (
    <Button
      type="text"
      icon={isSubscribed ? <BellFilled style={{ color: '#1890ff' }} /> : <BellOutlined />}
      onClick={toggleNotifications}
      loading={isLoading}
      className="notification-button"
      title={isSubscribed ? 'Disable notifications' : 'Enable notifications'}
    >
      {isSubscribed ? 'Notifications On' : 'Notifications Off'}
    </Button>
  );
};

export default NotificationButton;