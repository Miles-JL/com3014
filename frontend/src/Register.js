import { useState } from "react";
import axios from "axios";

export default function Register({ onRegister, switchToLogin }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleRegister = async () => {
    try {
      await axios.post("https://localhost:5001/register", {
        username,
        password,
      });
      onRegister(); // optional auto-login after register
    } catch (err) {
      setError("Registration failed. Username might already exist.");
    }
  };

  return (
    <div>
      <h2>Register</h2>
      {error && <p style={{ color: "red" }}>{error}</p>}
      <input
        type="text"
        placeholder="Choose a username"
        value={username}
        onChange={(e) => setUsername(e.target.value)}
      />
      <br />
      <input
        type="password"
        placeholder="Choose a password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
      />
      <br />
      <button onClick={handleRegister}>Register</button>
      <p>
        Already have an account? <button onClick={switchToLogin}>Log In</button>
      </p>
    </div>
  );
}
