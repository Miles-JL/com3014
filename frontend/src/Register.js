import { useState } from "react";
import axios from "axios";

const API_URL = 'http://localhost:80';

export default function Register({ onRegister, switchToLogin }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleRegister = async () => {
    try {
      await axios.post(`/api/auth/register`, {
        username,
        passwordHash: password,
      });
      alert('Registered! Now login.');
      if (switchToLogin) switchToLogin();
    } catch (err) {
      setError("Registration failed. Username might already exist.");
    }
  };

  return (
    <div className="auth-container">
      <h2>Register</h2>
      {error && <div className="error">{error}</div>}
      <input
        type="text"
        placeholder="Choose a username"
        value={username}
        onChange={(e) => setUsername(e.target.value)}
      />
      <input
        type="password"
        placeholder="Choose a password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
      />
      <button onClick={handleRegister}>Register</button>
      <p style={{textAlign: "center", color: "#a3a3a3"}}>
        Already have an account?{" "}
        <button className="switch-auth-btn" onClick={switchToLogin}>Log In</button>
      </p>
    </div>
  );
}