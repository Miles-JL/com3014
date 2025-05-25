import { useState, useEffect } from "react";
import axios from "axios";

export default function ChatRoomList({ onSelectRoom, onStartDm }) {
  const [rooms, setRooms] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [newRoom, setNewRoom] = useState({ name: "", description: "" });
  const [isAdmin, setIsAdmin] = useState(false);
  const [currentUserId, setCurrentUserId] = useState(null);
  const [profile, setProfile] = useState({
    username: "",
    profileImage: "",
  });
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [isSearching, setIsSearching] = useState(false);

  useEffect(() => {
    // Only fetch profile and user ID when we actually need it
    const fetchRequiredData = async () => {
      await fetchUserProfile();
      getCurrentUserId();
    };

    // Only fetch data when we need it (e.g., when starting a DM)
    if (onStartDm) {
      fetchRequiredData();
    }

    fetchChatRooms();
    checkAdminStatus();
  }, [onStartDm]);

  useEffect(() => {
    const delay = setTimeout(() => {
      if (searchQuery.length > 1) {
        searchUsers(searchQuery);
      } else {
        setSearchResults([]);
      }
    }, 300);
    return () => clearTimeout(delay);
  }, [searchQuery]);

  const getCurrentUserId = () => {
    try {
      const token = localStorage.getItem("token");
      if (!token) return;

      const payload = JSON.parse(atob(token.split(".")[1]));
      const id = payload.nameid || payload.sub;
      setCurrentUserId(parseInt(id));
    } catch (err) {
      console.error("Error extracting user ID from token:", err);
    }
  };

  const fetchUserProfile = async () => {
    try {
      const token = localStorage.getItem("token");
      if (!token) return;

      const response = await axios.get(`/api/user/profile`, {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      setProfile(response.data);
    } catch (err) {
      console.error("Error fetching profile:", err);
    }
  };

  const checkAdminStatus = () => {
    try {
      const token = localStorage.getItem("token");
      if (!token) return;

      const payload = JSON.parse(atob(token.split(".")[1]));
      const roles = payload.role || [];

      if (Array.isArray(roles)) {
        setIsAdmin(roles.includes("Admin"));
      } else if (typeof roles === "string") {
        setIsAdmin(roles === "Admin");
      }
    } catch (err) {
      console.error("Error checking admin status:", err);
    }
  };

  const fetchChatRooms = async () => {
    try {
      setIsLoading(true);
      const response = await axios.get(`/api/chatroom`);
      setRooms(response.data);
      setIsLoading(false);
    } catch (err) {
      console.error("Error fetching chat rooms:", err);
      setError("Failed to load chat rooms");
      setIsLoading(false);
    }
  };

  const handleCreateRoom = async (e) => {
    e.preventDefault();

    if (!newRoom.name.trim()) {
      setError("Room name is required");
      return;
    }

    try {
      const token = localStorage.getItem("token");
      if (!token) {
        setError("You must be logged in to create a room");
        return;
      }

      const response = await axios.post(`/api/chatroom`, newRoom, {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      setRooms([...rooms, response.data]);
      setNewRoom({ name: "", description: "" });
      setShowCreateForm(false);
      onSelectRoom(response.data);
    } catch (err) {
      console.error("Error creating chat room:", err);
      setError("Failed to create chat room");
    }
  };

  const handleDeleteRoom = async (roomId) => {
    try {
      const token = localStorage.getItem("token");
      if (!token) return;

      const confirmed = window.confirm(
        "Are you sure you want to delete this room?"
      );
      if (!confirmed) return;

      await axios.delete(`/api/chatroom/${roomId}`, {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      setRooms(rooms.filter((r) => r.id !== roomId));
    } catch (err) {
      console.error("Error deleting chat room:", err);
      alert("Failed to delete chat room");
    }
  };

  const handleDeleteAllRooms = async () => {
    try {
      const token = localStorage.getItem("token");
      if (!token) return;

      if (!window.confirm("Are you sure you want to delete ALL chat rooms?"))
        return;

      await axios.delete(`/api/chatroom/admin/deleteAll`, {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      setRooms([]);
      alert("All chat rooms have been deleted");
    } catch (err) {
      console.error("Error deleting all rooms:", err);
      alert("Failed to delete all rooms");
    }
  };

  const searchUsers = async (query) => {
    if (!query.trim()) {
      setSearchResults([]);
      return;
    }

    try {
      setIsSearching(true);
      const token = localStorage.getItem("token");
      const res = await axios.get(`/api/user/search?query=${query}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      setSearchResults(res.data);
    } catch (err) {
      console.error("User search failed:", err);
    } finally {
      setIsSearching(false);
    }
  };

  if (isLoading) return <div>Loading chat rooms...</div>;

  return (
    <>
      <div className="header-bar">
        <div className="header-profile">
          <div className="header-profile-image">
            {profile.profileImage ? (
              <img src={profile.profileImage} alt="Profile" />
            ) : (
              <div className="no-image-small">
                {profile.username?.charAt(0)?.toUpperCase() || "?"}
              </div>
            )}
          </div>
          <span className="header-username">{profile.username}</span>
        </div>
      </div>

      <div className="chat-room-list">
        <div className="chat-header">
          <h2>Direct Messages</h2>
        </div>
        
        <div className="user-search-container">
          <div className="search-input-container">
            <input
              type="text"
              className="search-input"
              placeholder="Search users..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
            <span className="search-icon">🔍</span>
            {isSearching && <div className="search-loading">Searching...</div>}
          </div>

          {searchResults.length > 0 && (
            <div className="search-results">
              {searchResults.map((user) => (
                <div key={user.id} className="search-result-item">
                  <div className="user-avatar">
                    {user.profileImage ? (
                      <img
                        src={user.profileImage}
                        alt={user.username}
                        className="user-avatar-img"
                      />
                    ) : (
                      <div className="avatar-placeholder">
                        {user.username.charAt(0).toUpperCase()}
                      </div>
                    )}
                  </div>
                  <div className="user-info">
                    <span className="username">{user.username}</span>
                  </div>
                  <button 
                    className="dm-button"
                    onClick={() => onStartDm(user)}
                  >
                    DM
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="chat-room-list" style={{ marginTop: '20px' }}>
        <div className="chat-header">
          <h2>Available Chat Rooms</h2>
          <div className="chat-actions">
            <button
              onClick={() => setShowCreateForm(!showCreateForm)}
              className="create-room-btn"
            >
              {showCreateForm ? "Cancel" : "Create New Room"}
            </button>

            {isAdmin && (
              <button onClick={handleDeleteAllRooms} className="delete-all-btn">
                Delete All Rooms
              </button>
            )}
          </div>
        </div>

        {error && <div className="error">{error}</div>}

        {showCreateForm && (
          <form onSubmit={handleCreateRoom} className="create-room-form">
            <div className="form-group">
              <label>Room Name:</label>
              <input
                type="text"
                value={newRoom.name}
                onChange={(e) =>
                  setNewRoom({ ...newRoom, name: e.target.value })
                }
                required
              />
            </div>

            <div className="form-group">
              <label>Description:</label>
              <textarea
                value={newRoom.description || ""}
                onChange={(e) =>
                  setNewRoom({ ...newRoom, description: e.target.value })
                }
                rows={3}
                placeholder="What's this room about?"
              />
            </div>

            <button type="submit">Create Room</button>
          </form>
        )}

        {rooms.length === 0 ? (
          <p className="no-rooms">No chat rooms available. Create one!</p>
        ) : (
          <ul className="room-list">
            {rooms.map((room) => (
              <li key={room.id} className="room-item">
                <div className="room-info">
                  <div className="room-name">{room.name}</div>
                  <div className="room-description">{room.description}</div>
                  <div className="room-meta">
                    Created by {room.creatorName} on{" "}
                    {new Date(room.createdAt).toLocaleString()}
                  </div>
                </div>
                <div className="room-actions">
                  <button
                    onClick={() => onSelectRoom(room)}
                    className="join-room-btn"
                  >
                    Join Room
                  </button>
                  {(isAdmin || room.creatorName === profile.username) && (
                    <button
                      onClick={() => handleDeleteRoom(room.id)}
                      className="delete-room-btn"
                    >
                      Delete
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}
