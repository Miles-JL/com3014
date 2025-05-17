import { useEffect, useRef, useState } from "react";

const API_URL = 'http://localhost:5247';

export default function Chat({ room, onLeave }) {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const socketRef = useRef(null);
  const messagesContainerRef = useRef(null);
  const [username, setUsername] = useState(""); // Track current user
  const localMessagesRef = useRef(new Set()); // Track local message IDs to avoid duplicates

  useEffect(() => {
    // Get current username from token
    try {
      const token = localStorage.getItem("token");
      if (token) {
        const payload = JSON.parse(atob(token.split('.')[1]));
        // The name claim is stored as "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" in .NET JWT tokens
        setUsername(payload.unique_name || payload.name || "");
      }
    } catch (err) {
      console.error("Failed to decode token:", err);
    }
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token || !room) return;
    
    // Clear tracked messages when joining a new room
    localMessagesRef.current = new Set();
    
    // Close any existing connection
    if (socketRef.current) {
      socketRef.current.close();
    }

    // Connect to WebSocket with the room ID parameter
    const wsUrl = `ws://${API_URL.replace("http://", "")}/ws/chat?token=${token}&roomId=${room.id}`;
    socketRef.current = new WebSocket(wsUrl);

    socketRef.current.onopen = () => {
      console.log(`Connected to room: ${room.name}`);
    };

    socketRef.current.onmessage = (event) => {
      try {
        const messageData = JSON.parse(event.data);
        
        // Detect if it's a message history update
        if (messageData.type === 'history') {
          setMessages(messageData.messages || []);
        } else {
          // For non-system messages from the current user, check if we already have a local copy
          if (messageData.sender === username && 
              messageData.type !== 'system' && 
              messageData.text) {
            
            // Create a unique key for this message
            const messageKey = `${messageData.sender}:${messageData.text}:${new Date(messageData.timestamp).getTime()}`;
            
            // If we've already processed this message locally, don't add it again
            if (localMessagesRef.current.has(messageKey)) {
              return;
            }
          }
          
          setMessages(prev => [...prev, messageData]);
        }
      } catch (err) {
        // If not JSON, treat as plain text
        setMessages(prev => [...prev, { text: event.data }]);
      }
    };

    socketRef.current.onclose = () => {
      console.log("WebSocket disconnected");
    };

    return () => {
      if (socketRef.current) {
        socketRef.current.close();
      }
    };
  }, [room, username]);

  // Auto-scroll to bottom when messages update
  useEffect(() => {
    if (messagesContainerRef.current) {
      messagesContainerRef.current.scrollTop = messagesContainerRef.current.scrollHeight;
    }
  }, [messages]);

  const sendMessage = () => {
    if (input.trim() && socketRef.current?.readyState === WebSocket.OPEN) {
      // Create a message object similar to what server would return
      const localMessage = {
        text: input,
        sender: username || "You", // Fallback to "You" if username isn't yet available
        timestamp: new Date()
      };
      
      // Add to local messages right away
      setMessages(prev => [...prev, localMessage]);
      
      // Track this message to prevent duplicates when server confirms
      const messageKey = `${localMessage.sender}:${localMessage.text}:${localMessage.timestamp.getTime()}`;
      localMessagesRef.current.add(messageKey);
      
      // Then send to server
      socketRef.current.send(input);
      setInput("");
    }
  };

  if (!room) {
    return <div>No chat room selected</div>;
  }

  return (
    <div style={{ maxWidth: "800px", margin: "0 auto" }}>
      <div style={{ 
        display: "flex", 
        justifyContent: "space-between",
        alignItems: "center",
        marginBottom: "10px"
      }}>
        <div>
          <h2 style={{ margin: "0" }}>{room.name}</h2>
          {room.description && <p style={{ margin: "5px 0" }}>{room.description}</p>}
        </div>
        <button 
          onClick={onLeave} 
          style={{ 
            backgroundColor: "#f44336",
            color: "white",
            border: "none",
            padding: "8px 16px",
            borderRadius: "4px",
            cursor: "pointer"
          }}
        >
          Leave Room
        </button>
      </div>

      <div
        ref={messagesContainerRef}
        style={{
          height: "400px", // Fixed height
          width: "100%", 
          overflowY: "auto",
          border: "1px solid #ccc",
          padding: "16px",
          boxSizing: "border-box",
          backgroundColor: "#f9f9f9",
          borderRadius: "4px",
          marginBottom: "10px"
        }}
      >
        {messages.length === 0 ? (
          <div style={{ textAlign: "center", color: "#888", marginTop: "180px" }}>
            No messages yet. Be the first to say something!
          </div>
        ) : (
          messages.map((msg, i) => (
            <div key={i} style={{ marginBottom: "10px" }}>
              {msg.type === 'system' ? (
                <div style={{ 
                  textAlign: 'center', 
                  color: '#888', 
                  fontStyle: 'italic', 
                  margin: '8px 0',
                  backgroundColor: '#f0f0f0',
                  padding: '5px 10px',
                  borderRadius: '15px',
                  display: 'inline-block'
                }}>
                  {msg.text}
                </div>
              ) : (
                <div>
                  <div>
                    <strong>{msg.sender || 'Anonymous'}</strong>
                    {msg.timestamp && (
                      <span style={{ color: '#888', fontSize: '0.8em', marginLeft: '8px' }}>
                        {new Date(msg.timestamp).toLocaleTimeString()}
                      </span>
                    )}
                  </div>
                  <div>{msg.text}</div>
                </div>
              )}
            </div>
          ))
        )}
      </div>

      <div style={{ 
        display: "flex", 
        gap: "10px" 
      }}>
        <input
          type="text"
          value={input}
          placeholder="Type a message..."
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && sendMessage()}
          style={{ 
            flex: 1, 
            padding: "10px",
            borderRadius: "4px",
            border: "1px solid #ccc"
          }}
        />
        <button 
          onClick={sendMessage}
          style={{
            padding: "10px 20px",
            backgroundColor: "#4caf50",
            color: "white",
            border: "none",
            borderRadius: "4px",
            cursor: "pointer"
          }}
        >
          Send
        </button>
      </div>
    </div>
  );
}