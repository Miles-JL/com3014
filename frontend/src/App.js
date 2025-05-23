import { useState, useEffect } from "react";
import axios from "axios";
import ProfilePage from "./ProfilePage";
import ChatRoomList from "./ChatRoomList";
import Chat from "./Chat";
import DmChat from "./dmChat"; // import DM chat
import "./App.css";

const API_URL = "http://localhost:5247";

function App() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [token, setToken] = useState(localStorage.getItem("token") || "");
  const [showProfile, setShowProfile] = useState(false);

  // Instead of separate view states, just hold current chat info:
  const [selectedRoom, setSelectedRoom] = useState(null); // for group chat
  // Remove threadId because backend doesn't track it, just store recipient user object:
  const [activeDmRecipient, setActiveDmRecipient] = useState(null);

  useEffect(() => {
    if (token) {
      localStorage.setItem("token", token);
    } else {
      localStorage.removeItem("token");
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
          <>
            <div className="nav-bar">
              <button onClick={() => setShowProfile(false)}>
                Back to Chat
              </button>
              <button onClick={handleLogout}>Logout</button>
            </div>
            <ProfilePage />
          </>
        ) : (
          <>
            <div className="nav-bar">
              <div>
                <button onClick={() => setShowProfile(true)}>My Profile</button>
                <button onClick={handleLogout}>Logout</button>
              </div>
            </div>

            {/* If DM chat active, show it */}
            {activeDmRecipient ? (
              <DmChat recipient={activeDmRecipient} onLeave={handleLeaveDm} />
            ) : null}

            {/* If group chat room active, show it */}
            {selectedRoom ? (
              <Chat room={selectedRoom} onLeave={handleLeaveRoom} />
            ) : null}

            {/* If no chat active, show room list (pass handlers for selecting room or starting DM) */}
            {!selectedRoom && !activeDmRecipient && (
              <ChatRoomList
                onSelectRoom={handleSelectRoom}
                onStartDm={handleStartDm}
              />
            )}
          </>
        )}
      </div>
    </>
  );
}

export default App;
