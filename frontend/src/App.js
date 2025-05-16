import { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import ProfilePage from './ProfilePage';
import './App.css';

const API_URL = 'http://localhost:5247';
const WS_URL = 'ws://localhost:5247/ws';

function App() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [token, setToken] = useState(localStorage.getItem('token') || '');
  const [message, setMessage] = useState('');
  const [chat, setChat] = useState([]);
  const [showProfile, setShowProfile] = useState(false);
  const socketRef = useRef(null);
  const chatContainerRef = useRef(null);

  useEffect(() => {
    if (token) {
      localStorage.setItem('token', token);
    } else {
      localStorage.removeItem('token');
    }
  }, [token]);

  useEffect(() => {
    if (!token) return;
    
    socketRef.current = new WebSocket(`${WS_URL}?token=${token}`);
    
    socketRef.current.onmessage = (e) => {
      try {
        const data = JSON.parse(e.data);
        setChat(prev => [...prev, data]);
      } catch (error) {
        console.error('Error parsing message:', error);
        setChat(prev => [...prev, { text: e.data, sender: 'System' }]);
      }
    };
    
    socketRef.current.onclose = () => {
      console.log('WebSocket disconnected');
    };
    
    return () => socketRef.current?.close();
  }, [token]);

  // Auto-scroll chat to bottom when new messages arrive
  useEffect(() => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTop = chatContainerRef.current.scrollHeight;
    }
  }, [chat]);

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
    setChat([]);
  };

  const sendMessage = () => {
    if (socketRef.current && message.trim()) {
      socketRef.current.send(message);
      setMessage('');
    }
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
            <h2>Chat Room</h2>
            <div>
              <button onClick={() => setShowProfile(true)}>My Profile</button>
              <button onClick={handleLogout}>Logout</button>
            </div>
          </div>
          
          <div 
            className="chat-container"
            ref={chatContainerRef}
          >
            {chat.map((msg, i) => (
              <div key={i} className="message">
                <div className="message-header">
                  {msg.profileImage && (
                    <img 
                      src={`${API_URL}${msg.profileImage}`} 
                      alt={msg.sender}
                      className="profile-thumbnail" 
                    />
                  )}
                  <span className="sender">{msg.sender}</span>
                  {msg.timestamp && (
                    <span className="timestamp">
                      {new Date(msg.timestamp).toLocaleTimeString()}
                    </span>
                  )}
                </div>
                <div className="message-text">{msg.text}</div>
              </div>
            ))}
          </div>
          
          <div className="message-input">
            <input 
              value={message} 
              onChange={(e) => setMessage(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
              placeholder="Type a message..."
            />
            <button onClick={sendMessage}>Send</button>
          </div>
        </>
      )}
    </div>
  );
}

export default App;