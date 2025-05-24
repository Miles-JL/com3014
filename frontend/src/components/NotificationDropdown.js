import { formatDistanceToNow } from 'date-fns';
import './NotificationDropdown.css';

const NotificationDropdown = ({ notifications, onClose }) => {
  const handleNotificationClick = (notification) => {
    // Handle notification click (e.g., navigate to relevant page)
    console.log('Notification clicked:', notification);
    // You can add navigation logic here based on notification type
  };

  return (
    <div className="notification-dropdown">
      <div className="notification-header">
        <h4>Notifications</h4>
        <button onClick={onClose} className="close-btn">&times;</button>
      </div>
      <div className="notification-list">
        {notifications.length > 0 ? (
          notifications.map((notification) => (
            <div 
              key={notification.id} 
              className={`notification-item ${!notification.isRead ? 'unread' : ''}`}
              onClick={() => handleNotificationClick(notification)}
            >
              <div className="notification-content">
                <div className="notification-message">{notification.message}</div>
                <div className="notification-time">
                  {formatDistanceToNow(new Date(notification.timestamp), { addSuffix: true })}
                </div>
              </div>
              {!notification.isRead && <div className="unread-indicator"></div>}
            </div>
          ))
        ) : (
          <div className="no-notifications">No notifications</div>
        )}
      </div>
    </div>
  );
};

export default NotificationDropdown;
