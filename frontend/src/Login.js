import { useState } from "react";
import axios from "axios";

const API_URL = 'http://localhost:80';

export default function Login({ onLogin, switchToRegister }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleLogin = async () => {
    try {
      const res = await axios.post(`/api/auth/login`, {
        username,
        passwordHash: password,
      });

      if (res.data.token) {
        localStorage.setItem("token", res.data.token);
        onLogin(res.data.token);
      } else {
        setError("Login failed. Please check your credentials.");
      }
    } catch (err) {
      console.error("Login error:", err);
      setError("Login failed. Please check your credentials.");
    }
  };

  return (
    <div className="auth-container">
      <h2>Login</h2>
      {error && <div className="error">{error}</div>}
      <input
        type="text"
        placeholder="Username"
        value={username}
        onChange={(e) => setUsername(e.target.value)}
      />
      <input
        type="password"
        placeholder="Password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
      />
      <div className="auth-buttons">
        <button onClick={handleLogin}>Log In</button>
        <button className="switch-auth-btn" onClick={switchToRegister}>Register</button>
      </div>
    </div>
  );
}