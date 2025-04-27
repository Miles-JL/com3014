import React, { useState, useEffect, useRef } from "react";
import axios from "axios";

const API_URL = "http://localhost:5247";
const WS_URL = "ws://localhost:5247/ws";

function App() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [token, setToken] = useState("");
  const [message, setMessage] = useState("");
  const [chat, setChat] = useState([]);
  const socketRef = useRef(null);

  const handleRegister = async () => {
    await axios.post(`${API_URL}/api/auth/register`, {
      username,
      passwordHash: password,
    });
    alert("Registered! Now login.");
  };

  const handleLogin = async () => {
    try {
      const res = await axios.post(`${API_URL}/api/auth/login`, {
        username,
        passwordHash: password,
      });

      console.log("Login response:", res); // Log the entire response
      if (res.data.token) {
        setToken(res.data.token); // Set token state if a token is returned
      } else {
        alert("Login failed. Please check your credentials.");
      }
    } catch (error) {
      console.error("Login error:", error);
      alert("An error occurred during login.");
    }
  };


  useEffect(() => {
    if (!token) return;
    socketRef.current = new WebSocket(`${WS_URL}?token=${token}`);
    socketRef.current.onmessage = (e) => setChat((prev) => [...prev, e.data]);
    return () => socketRef.current?.close();
  }, [token]);

  const sendMessage = () => {
    if (socketRef.current && message.trim()) {
      socketRef.current.send(message);
      setMessage("");
    }
  };

  return (
    <div style={{ maxWidth: 500, margin: "auto" }}>
      {!token ? (
        <>
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
          <button onClick={handleLogin}>Login</button>
          <button onClick={handleRegister}>Register</button>
        </>
      ) : (
        <>
          <h2>Chat Room</h2>
          <div
            style={{
              border: "1px solid #ccc",
              padding: 10,
              height: 300,
              overflowY: "scroll",
            }}
          >
            {chat.map((msg, i) => (
              <div key={i}>{msg}</div>
            ))}
          </div>
          <input value={message} onChange={(e) => setMessage(e.target.value)} />
          <button onClick={sendMessage}>Send</button>
        </>
      )}
    </div>
  );
}

export default App;
