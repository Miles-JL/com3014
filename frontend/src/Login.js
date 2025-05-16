import { useState } from "react";
import axios from "axios";

export default function Login({ onLogin, switchToRegister }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleLogin = async () => {
    try {
      const res = await axios.post("http://localhost:5247/api/auth/login", {
        username,
        passwordHash: password, // Match the field name expected by your backend
      });

      localStorage.setItem("token", res.data.token);
      onLogin();
    } catch (err) {
      console.error("Login error:", err);
      setError("Login failed. Please check your credentials.");
    }
  };

  return (
    <div>
      <h2>Login</h2>
      {error && <p style={{ color: "red" }}>{error}</p>}
      <input
        type="text"
        placeholder="Username"
        value={username}
        onChange={(e) => setUsername(e.target.value)}
      />
      <br />
      <input
        type="password"
        placeholder="Password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
      />
      <br />
      <button onClick={handleLogin}>Log In</button>
      {switchToRegister && (
        <p>
          No account? <button onClick={switchToRegister}>Register</button>
        </p>
      )}
    </div>
  );
}