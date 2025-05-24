import { useState, useEffect } from "react";
import axios from "axios";
import ProfilePage from "./ProfilePage";
import ChatRoomList from "./ChatRoomList";
import Chat from "./Chat";
import DmChat from "./dmChat";
import "./App.css";

const API_URL = 'http://localhost:5247'; // API Gateway port

function App() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [token, setToken] = useState(localStorage.getItem("token") || "");
  const [showProfile, setShowProfile] = useState(false);
  const [currentUser, setCurrentUser] = useState(null);

  // Instead of separate view states, just hold current chat info:
  const [selectedRoom, setSelectedRoom] = useState(null); // for group chat
  // Remove threadId because backend doesn't track it, just store recipient user object:
  const [activeDmRecipient, setActiveDmRecipient] = useState(null);

  // Set up WebSocket for notifications when user is authenticated
  useEffect(() => {
    if (!token || !currentUser?.id) return;

    // Request notification permission
    if ('Notification' in window) {
      Notification.requestPermission().then(permission => {
        if (permission === 'granted') {
          console.log('Notification permission granted');
        } else {
          console.warn('Notification permission denied');
        }
      });
    }
  
  useEffect(() => {
      // Initialize notification service
      notificationService.init().then(initialized => {
        if (initialized) {
          console.log('Notification service initialized');
        }
      });
  
      // Request permission on user interaction
      const handleFirstInteraction = () => {
        notificationService.requestNotificationPermission().then(granted => {
          if (granted) {
            notificationService.subscribeUser().then(subscribed => {
              if (subscribed) {
                console.log('User subscribed to push notifications');
              }
            });
          }
        });
        document.removeEventListener('click', handleFirstInteraction);
        document.removeEventListener('keydown', handleFirstInteraction);
      };
  
      document.addEventListener('click', handleFirstInteraction);
      document.addEventListener('keydown', handleFirstInteraction);
  
      return () => {
        document.removeEventListener('click', handleFirstInteraction);
        document.removeEventListener('keydown', handleFirstInteraction);
      };
    }, []);
    // Set up WebSocket connection
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const ws = new WebSocket(`${protocol}//${window.location.host}/ws/notification?access_token=${token}`);

    ws.onopen = () => {
      console.log('Connected to notification service');
    };

    ws.onmessage = (event) => {
      try {
        const notification = JSON.parse(event.data);
        console.log('Received notification:', notification);

        // Show browser notification
        if (Notification.permission === 'granted') {
          new Notification(notification.title || 'New Notification', {
            body: notification.message,
            icon: notification.icon || '/logo192.png'
          });
        }
      } catch (error) {
        console.error('Error processing notification:', error);
      }
    };

    ws.onerror = (error) => {
      console.error('WebSocket error:', error);
    };

    ws.onclose = () => {
      console.log('Disconnected from notification service');
    };

    // Clean up WebSocket on unmount
    return () => {
      if (ws.readyState === WebSocket.OPEN) {
        ws.close();
      }
    };
  }, [token, currentUser?.id]);

  // Fetch current user when token changes
  useEffect(() => {
    const fetchCurrentUser = async () => {
      try {
        const response = await axios.get(`${API_URL}/api/auth/me`, {
          headers: { Authorization: `Bearer ${token}` }
        });
        setCurrentUser(response.data);
      } catch (error) {
        console.error('Error fetching current user:', error);
      }
    };

    if (token) {
      localStorage.setItem("token", token);
      fetchCurrentUser();
    } else {
      localStorage.removeItem("token");
      setCurrentUser(null);
    }
  }, [token]);

  const handleRegister = async () => {
    try {
      await axios.post(`${API_URL}/api/auth/register`, {
        username,
        passwordHash: password,
      });
      alert("Registered! Now login.");
    } catch (error) {
      console.error("Registration error:", error);
      alert("Registration failed. Username might already exist.");
    }
  };

  const handleLogin = async () => {
    try {
      const res = await axios.post(`${API_URL}/api/auth/login`, {
        username,
        passwordHash: password,
      });

      if (res.data.token) {
        setToken(res.data.token);
        setUsername("");
        setPassword("");
      } else {
        alert("Login failed. Please check your credentials.");
      }
    } catch (error) {
      console.error("Login error:", error);
      alert("An error occurred during login.");
    }
  };

  const handleLogout = () => {
    setToken("");
    setCurrentUser(null);
    setSelectedRoom(null);
    setActiveDmRecipient(null);
    setShowProfile(false);
  };

  // Select a group chat room (called by ChatRoomList)
  const handleSelectRoom = (room) => {
    setSelectedRoom(room);
    setActiveDmRecipient(null);
  };

  // Start DM chat (called by ChatRoomList or a user search component)
  // Changed: only recipient user passed, no threadId needed
  const handleStartDm = (recipient) => {
    setActiveDmRecipient(recipient);
    setSelectedRoom(null);
  };

  // Leave group chat room
  const handleLeaveRoom = () => {
    setSelectedRoom(null);
  };

  // Leave DM chat
  const handleLeaveDm = () => {
    setActiveDmRecipient(null);
  };

  return (
    <>
      <div className="stars"></div>
      <div className="stars2"></div>
      <div className="stars3"></div>
      <div className="app-container">
        {!token ? (
          <div className="auth-container">
            <h2>Login / Register</h2>
            <input
              placeholder="Username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
            />
            <input
              placeholder="Password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <div className="auth-buttons">
              <button onClick={handleLogin}>Login</button>
              <button onClick={handleRegister}>Register</button>
            </div>
          </div>
        ) : showProfile ? (
          <div className="app-content">
            <div className="nav-bar">
              <button onClick={() => setShowProfile(false)}>
                Back to Chat
              </button>
              <button onClick={handleLogout}>Logout</button>
            </div>
            <ProfilePage />
          </div>
        ) : (
          <div className="app-content">
            <div className="nav-bar">
              <div className="nav-actions">
                <button onClick={() => setShowProfile(true)}>My Profile</button>
                <button onClick={handleLogout}>Logout</button>
              </div>
            </div>

            <div className="main-content">
              {/* If DM chat active, show it */}
              {activeDmRecipient ? (
                <DmChat recipient={activeDmRecipient} onLeave={handleLeaveDm} />
              ) : null}

              {/* If group chat room active, show it */}
              {selectedRoom ? (
                <Chat room={selectedRoom} onLeave={handleLeaveRoom} />
              ) : null}

              {/* If no chat active, show room list */}
              {!selectedRoom && !activeDmRecipient && (
                <ChatRoomList
                  onSelectRoom={handleSelectRoom}
                  onStartDm={handleStartDm}
                />
              )}
            </div>
          </div>
        )}
      </div>
    </>
  );
}

export default App;
