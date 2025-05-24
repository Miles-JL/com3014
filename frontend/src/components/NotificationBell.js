import { useState, useEffect, useRef, useCallback } from 'react';
import { FaBell } from 'react-icons/fa';
import NotificationDropdown from './NotificationDropdown';
import './NotificationBell.css';

// Service URLs with environment variable fallbacks
const NOTIFICATION_SERVICE_URL = process.env.REACT_APP_NOTIFICATION_SERVICE_URL || 'http://localhost:5201';
const WS_BASE_URL = process.env.REACT_APP_WS_BASE_URL || 'ws://localhost:5201/ws/notification';

// WebSocket connection reference
const NotificationBell = ({ token, userId }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState([]);
  const [connectionStatus, setConnectionStatus] = useState('disconnected'); // 'connecting', 'connected', 'error', 'disconnected'
  const [lastError, setLastError] = useState(null);
  const wsRef = useRef(null);
  const dropdownRef = useRef(null);
  const reconnectTimeoutRef = useRef(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    function handleClickOutside(event) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Fetch unread notifications
  const fetchUnreadNotifications = useCallback(async () => {
    if (!token) return;
    
    try {
      const response = await fetch(`${NOTIFICATION_SERVICE_URL}/api/Notification/unread`, {
        method: 'GET',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Accept': 'application/json'
        },
        credentials: 'include',
        mode: 'cors'
      });
      
      if (!response.ok) {
        const errorText = await response.text();
        console.error('Failed to fetch notifications:', response.status, errorText);
        throw new Error(`Failed to fetch notifications: ${response.status} ${response.statusText}`);
      }
      
      const data = await response.json();
      setNotifications(data);
      setUnreadCount(data.filter(n => !n.isRead).length);
    } catch (error) {
      console.error('Error fetching notifications:', error);
      // Don't throw here to prevent unhandled promise rejection
    }
  }, [token, NOTIFICATION_SERVICE_URL]);

  // Mark all notifications as read
  const markAllAsRead = async () => {
    try {
      const response = await fetch(`${NOTIFICATION_SERVICE_URL}/api/Notification/mark-all-read`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
          'Accept': 'application/json'
        },
        credentials: 'include',
        mode: 'cors'
      });
      
      if (response.ok) {
        setUnreadCount(0);
        // Update local notifications to mark all as read
        setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      } else {
        const errorText = await response.text();
        throw new Error(`Failed to mark as read: ${response.status} ${errorText}`);
      }
    } catch (error) {
      console.error('Error marking notifications as read:', error);
    }
  };

  // Setup WebSocket connection
  useEffect(() => {
    if (!token) {
      console.log('WebSocket: Missing authentication token');
      setConnectionStatus('error');
      setLastError('Authentication token is missing');
      return;
    }
    
    // Skip if WS_BASE_URL is not defined (e.g., during SSR)
    if (typeof window === 'undefined') {
      return;
    }
    
    // Clear any existing reconnect timeout
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
    
    setConnectionStatus('connecting');
    setLastError(null);
    
    // Initialize socket as null
    let socket = null;
    let pingInterval = null;
    let reconnectAttempts = 0;
    const MAX_RECONNECT_ATTEMPTS = 10; // Increased max attempts
    const BASE_RECONNECT_DELAY = 1000; // Start with 1 second
    let isMounted = true;

    const calculateReconnectDelay = (attempt) => {
      // Exponential backoff with jitter
      const jitter = Math.random() * 1000; // 0-1 second jitter
      return Math.min(30000, Math.pow(2, attempt) * BASE_RECONNECT_DELAY) + jitter;
    };

    const cleanup = (preventReconnect = false) => {
      console.log('Cleaning up WebSocket resources', { preventReconnect });
      isMounted = false;
      
      // Clear any pending reconnection attempts
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
      
      // Clear ping interval
      if (pingInterval) {
        clearInterval(pingInterval);
        pingInterval = null;
      }
      
      // Close WebSocket connection
      if (socket) {
        try {
          // Don't try to close if already closing or closed
          if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
            socket.close(1000, preventReconnect ? 'User initiated disconnect' : 'Reconnecting...');
          }
        } catch (error) {
          console.error('Error closing socket:', error);
        } finally {
          socket = null;
          wsRef.current = null;
        }
      }
      
      // If we're not preventing reconnection and the component is still mounted, schedule a reconnect
      if (!preventReconnect && isMounted && reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
        const delay = calculateReconnectDelay(reconnectAttempts);
        console.log(`Scheduling reconnection attempt ${reconnectAttempts + 1}/${MAX_RECONNECT_ATTEMPTS} in ${delay}ms`);
        reconnectTimeoutRef.current = setTimeout(() => {
          if (isMounted) {
            reconnectAttempts++;
            connectWebSocket();
          }
        }, delay);
      }
    };

    const connectWebSocket = () => {
      if (!token || !isMounted) {
        console.log('Skipping WebSocket connection: no token or component unmounted');
        return;
      }

      // Close existing connection if any
      if (socket) {
        try {
          if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
            console.log('Closing existing WebSocket connection');
            socket.close(1000, 'Reconnecting...');
          }
        } catch (e) {
          console.warn('Error closing existing socket:', e);
        } finally {
          socket = null;
          wsRef.current = null;
        }
      }

      // Create WebSocket URL with token
      const wsUrl = `${WS_BASE_URL}?access_token=${encodeURIComponent(token)}`;
      console.log('Creating new WebSocket connection to:', wsUrl);
      
      try {
        socket = new WebSocket(wsUrl);
        wsRef.current = socket;
        
        socket.onopen = () => {
          if (!isMounted) {
            console.log('Component unmounted, closing WebSocket');
            socket.close(1000, 'Component unmounted');
            return;
          }
          
          console.log('WebSocket connected successfully');
          setConnectionStatus('connected');
          setLastError(null);
          reconnectAttempts = 0; // Reset reconnect attempts on successful connection
          
          // Clear any existing ping interval
          if (pingInterval) {
            clearInterval(pingInterval);
            pingInterval = null;
          }
          
          // Start new ping interval
          pingInterval = setInterval(() => {
            if (socket && socket.readyState === WebSocket.OPEN) {
              try {
                const pingMsg = JSON.stringify({ 
                  type: 'ping', 
                  timestamp: Date.now() 
                });
                socket.send(pingMsg);
                console.log('Ping sent');
              } catch (e) {
                console.warn('Error sending ping:', e);
                // Don't try to reconnect here, onclose will handle it
              }
            }
          }, 25000); // Send ping every 25 seconds
          
          // Fetch initial notifications
          fetchUnreadNotifications().catch(e => 
            console.error('Error fetching notifications after connection:', e)
          );
        };

        socket.onmessage = (event) => {
          if (!isMounted) return;
          
          try {
            const data = JSON.parse(event.data);
            console.log('WebSocket message received:', data);
            
            if (data.type === 'pong') {
              console.log('Pong received, connection healthy');
              return;
            }
            
            if (data.Type === 'notification' || data.type === 'notification') {
              const notification = data.Data || data.data;
              console.log('New notification received:', notification);
              setNotifications(prev => [notification, ...prev]);
              setUnreadCount(prev => prev + 1);
            }
          } catch (error) {
            console.error('Error parsing WebSocket message:', error, event.data);
          }
        };

        socket.onerror = (error) => {
          console.error('WebSocket error:', error);
          // The onclose handler will handle reconnection
        };

        socket.onclose = (event) => {
          console.log(`WebSocket closed with code ${event.code}: ${event.reason || 'No reason provided'}`);
          
          // Clean up resources
          if (event.code === 1000) {
            // Normal closure
            setConnectionStatus('disconnected');
            return;
          }
          
          // Set error state if not a normal closure
          if (event.code !== 1000) {
            setConnectionStatus('error');
            setLastError(`Connection lost: ${event.reason || 'Unknown error'}`);
          }
          
          // Clean up resources and attempt to reconnect if needed
          cleanup();
          
          // Reset connection status if we've given up reconnecting
          if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS) {
            console.error('Max reconnection attempts reached');
            setConnectionStatus('error');
            setLastError('Unable to connect to notification service. Please refresh the page to try again.');
          }
        };
        
      } catch (error) {
        console.error('Error creating WebSocket:', error);
        
        // Attempt to reconnect after a delay if we haven't exceeded max attempts
        if (isMounted && reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
          const delay = calculateReconnectDelay(reconnectAttempts);
          console.log(`Retrying WebSocket connection in ${Math.round(delay / 1000)}s...`);
          
          reconnectTimeoutRef.current = setTimeout(() => {
            if (isMounted) {
              reconnectAttempts++;
              connectWebSocket();
            }
          }, delay);
        }
      }
    };
    
    // Initial connection
    console.log('Setting up WebSocket connection...');
    connectWebSocket();
    
    // Cleanup function
    return () => {
      console.log('Cleaning up WebSocket resources...');
      cleanup();
    };
  }, [userId, token, fetchUnreadNotifications, WS_BASE_URL]);

  // Toggle dropdown, fetch fresh notifications, and mark as read when opening
  const toggleDropdown = () => {
    const newIsOpen = !isOpen;
    setIsOpen(newIsOpen);
    
    if (newIsOpen) {
      // Mark all notifications as read when opening the dropdown
      if (unreadCount > 0) {
        markAllAsRead().catch(error => 
          console.error('Error marking notifications as read:', error)
        );
      }
      
      // Fetch fresh notifications
      fetchUnreadNotifications().catch(error => 
        console.error('Error fetching notifications:', error)
      );
    }
  };

  return (
    <div className="notification-bell" ref={dropdownRef}>
      <div className="bell-container">
        <button 
          className={`bell-button ${unreadCount > 0 ? 'has-unread' : ''}`}
          onClick={toggleDropdown}
          aria-label={`${unreadCount} unread notifications`}
          aria-expanded={isOpen}
          title={lastError || `Notifications (${unreadCount} unread)`}
        >
          <FaBell />
          {unreadCount > 0 && <span className="badge">{unreadCount > 9 ? '9+' : unreadCount}</span>}
        </button>
        <div 
          className={`connection-status ${connectionStatus}`}
          title={`Status: ${connectionStatus}${lastError ? ` - ${lastError}` : ''}`}
          aria-live="polite"
          aria-label={`Connection status: ${connectionStatus}`}
        />
      </div>
      {isOpen && (
        <NotificationDropdown 
          notifications={notifications} 
          onClose={() => setIsOpen(false)}
        />
      )}
    </div>
  );
};

export default NotificationBell;
