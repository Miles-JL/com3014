import { useState, useEffect } from 'react';
import axios from 'axios';
import ProfilePage from './ProfilePage';
import ChatRoomList from './ChatRoomList';
import Chat from './Chat';
import './App.css';

const API_URL = 'http://localhost:5247';

function App() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [token, setToken] = useState(localStorage.getItem('token') || '');
  const [showProfile, setShowProfile] = useState(false);
  const [view, setView] = useState('roomList'); // roomList, chat
  const [selectedRoom, setSelectedRoom] = useState(null);

  useEffect(() => {
    if (token) {
      localStorage.setItem('token', token);
    } else {
      localStorage.removeItem('token');
    }
  }, [token]);

  const handleRegister = async () => {
    try {
      await axios.post(`${API_URL}/api/auth/register`, {
        username,
        passwordHash: password,
      });
      alert('Registered! Now login.');
    } catch (error) {
      console.error('Registration error:', error);
      alert('Registration failed. Username might already exist.');
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
        setUsername('');
        setPassword('');
      } else {
        alert('Login failed. Please check your credentials.');
      }
    } catch (error) {
      console.error('Login error:', error);
      alert('An error occurred during login.');
    }
  };

  const handleLogout = () => {
    setToken('');
    setView('roomList');
    setSelectedRoom(null);
    setShowProfile(false);
  };

  const handleSelectRoom = (room) => {
    setSelectedRoom(room);
    setView('chat');
  };

  const handleLeaveRoom = () => {
    setSelectedRoom(null);
    setView('roomList');
  };

  return (
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
            <button onClick={() => setShowProfile(false)}>Back to Chat</button>
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
          
          {view === 'roomList' && (
            <ChatRoomList onSelectRoom={handleSelectRoom} />
          )}
          
          {view === 'chat' && selectedRoom && (
            <Chat room={selectedRoom} onLeave={handleLeaveRoom} />
          )}
        </>
      )}
    </div>
  );
}

export default App;