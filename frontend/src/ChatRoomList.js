import { useState, useEffect } from 'react';
import axios from 'axios';

const API_URL = 'http://localhost:5247';

export default function ChatRoomList({ onSelectRoom }) {
  const [rooms, setRooms] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [newRoom, setNewRoom] = useState({ name: '', description: '' });

  useEffect(() => {
    fetchChatRooms();
  }, []);

  const fetchChatRooms = async () => {
    try {
      setIsLoading(true);
      const response = await axios.get(`${API_URL}/api/chatroom`);
      setRooms(response.data);
      setIsLoading(false);
    } catch (err) {
      console.error('Error fetching chat rooms:', err);
      setError('Failed to load chat rooms');
      setIsLoading(false);
    }
  };

  const handleCreateRoom = async (e) => {
    e.preventDefault();
    
    if (!newRoom.name.trim()) {
      setError('Room name is required');
      return;
    }
    
    try {
      const token = localStorage.getItem('token');
      if (!token) {
        setError('You must be logged in to create a room');
        return;
      }
      
      const response = await axios.post(
        `${API_URL}/api/chatroom`,
        newRoom,
        {
          headers: {
            Authorization: `Bearer ${token}`
          }
        }
      );
      
      // Add the new room to the list and reset form
      setRooms([...rooms, response.data]);
      setNewRoom({ name: '', description: '' });
      setShowCreateForm(false);
      
      // Select the newly created room
      onSelectRoom(response.data);
    } catch (err) {
      console.error('Error creating chat room:', err);
      setError('Failed to create chat room');
    }
  };

  if (isLoading) return <div>Loading chat rooms...</div>;

  return (
    <div className="chat-room-list" style={{ maxWidth: "800px", margin: "0 auto" }}>
      <div style={{ 
        display: "flex", 
        justifyContent: "space-between", 
        alignItems: "center", 
        marginBottom: "20px" 
      }}>
        <h2>Available Chat Rooms</h2>
        <button 
          onClick={() => setShowCreateForm(!showCreateForm)}
          style={{
            padding: "8px 16px",
            backgroundColor: "#4caf50",
            color: "white",
            border: "none",
            borderRadius: "4px",
            cursor: "pointer"
          }}
        >
          {showCreateForm ? 'Cancel' : 'Create New Room'}
        </button>
      </div>
      
      {error && <div style={{ color: "red", marginBottom: "15px" }}>{error}</div>}
      
      {showCreateForm && (
        <form onSubmit={handleCreateRoom} style={{ 
          backgroundColor: "#f5f5f5",
          padding: "20px",
          borderRadius: "8px",
          marginBottom: "20px"
        }}>
          <div style={{ marginBottom: "15px" }}>
            <label style={{ display: "block", marginBottom: "5px", fontWeight: "bold" }}>
              Room Name:
            </label>
            <input 
              type="text" 
              value={newRoom.name}
              onChange={(e) => setNewRoom({...newRoom, name: e.target.value})}
              required
              style={{ width: "100%", padding: "8px", borderRadius: "4px", border: "1px solid #ccc" }}
            />
          </div>
          
          <div style={{ marginBottom: "15px" }}>
            <label style={{ display: "block", marginBottom: "5px", fontWeight: "bold" }}>
              Description:
            </label>
            <textarea
              value={newRoom.description}
              onChange={(e) => setNewRoom({...newRoom, description: e.target.value})}
              rows={3}
              placeholder="What's this room about?"
              style={{ width: "100%", padding: "8px", borderRadius: "4px", border: "1px solid #ccc" }}
            />
          </div>
          
          <button 
            type="submit"
            style={{
              padding: "8px 16px",
              backgroundColor: "#4caf50",
              color: "white",
              border: "none",
              borderRadius: "4px",
              cursor: "pointer"
            }}
          >
            Create Room
          </button>
        </form>
      )}
      
      {rooms.length === 0 ? (
        <p style={{ textAlign: "center", color: "#666", fontStyle: "italic", marginTop: "30px" }}>
          No chat rooms available. Create one!
        </p>
      ) : (
        <ul style={{ listStyle: "none", padding: 0 }}>
          {rooms.map(room => (
            <li 
              key={room.id} 
              style={{
                padding: "15px",
                border: "1px solid #ccc",
                borderRadius: "8px",
                marginBottom: "15px",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center"
              }}
            >
              <div>
                <div style={{ fontSize: "1.2em", fontWeight: "bold", marginBottom: "5px" }}>
                  {room.name}
                </div>
                <div style={{ color: "#555", marginBottom: "8px" }}>
                  {room.description}
                </div>
                <div style={{ fontSize: "0.8em", color: "#888" }}>
                  Created by {room.creatorName} on {new Date(room.createdAt).toLocaleString()}
                </div>
              </div>
              <button 
                onClick={() => onSelectRoom(room)}
                style={{
                  padding: "8px 16px",
                  backgroundColor: "#2196f3",
                  color: "white",
                  border: "none",
                  borderRadius: "4px",
                  cursor: "pointer"
                }}
              >
                Join Room
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}