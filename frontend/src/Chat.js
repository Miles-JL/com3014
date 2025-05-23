import { useEffect, useRef, useState } from "react";

const API_URL = 'http://localhost:80';

export default function Chat({ room, onLeave }) {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const socketRef = useRef(null);
  const messagesContainerRef = useRef(null);
  const [username, setUsername] = useState("");
  const [profileImage, setProfileImage] = useState("");
  const localMessagesRef = useRef(new Set()); // Track local message IDs to avoid duplicates
  const connectionAttempted = useRef(false);

  useEffect(() => {
    // Get current username from token
    const fetchUserProfile = async () => {
      try {
        const token = localStorage.getItem("token");
        if (token) {
          const response = await fetch(`${API_URL}/api/user/profile`, {
            headers: {
              Authorization: `Bearer ${token}`
            }
          });
          
          if (response.ok) {
            const data = await response.json();
            setUsername(data.username);
            setProfileImage(data.profileImage);
          } else {
            // Fallback to token decoding if profile fetch fails
            try {
              const payload = JSON.parse(atob(token.split('.')[1]));
              setUsername(payload.unique_name || payload.name || "");
            } catch (error) {
              console.error("Failed to decode token:", error);
            }
          }
        }
      } catch (err) {
        console.error("Failed to fetch user profile:", err);
      }
    };
    
    fetchUserProfile();
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token || !room || !username || connectionAttempted.current) return;

    // Mark that we've attempted a connection to prevent duplicates
    connectionAttempted.current = true;
    
    // Reset for each new room
    localMessagesRef.current = new Set();
    setMessages([]);

    if (socketRef.current) {
      socketRef.current.close();
    }

    const wsUrl = `ws://${API_URL.replace("http://", "")}/ws/chat?token=${token}&roomId=${room.id}`;
    socketRef.current = new WebSocket(wsUrl);

    socketRef.current.onopen = () => {
      console.log(`Connected to room: ${room.name}`);
      
      // Add system message about joining
      setMessages([{
        type: 'system',
        text: `You joined the room`,
        timestamp: new Date()
      }]);
    };

    socketRef.current.onmessage = (event) => {
      try {
        const messageData = JSON.parse(event.data);

        if (messageData.type === 'history') {
          setMessages(messageData.messages || []);
        } else {
          if (messageData.sender === username &&
              messageData.type !== 'system' &&
              messageData.text) {
            const messageKey = `${messageData.sender}:${messageData.text}:${new Date(messageData.timestamp).getTime()}`;
            if (localMessagesRef.current.has(messageKey)) {
              return;
            }
          }
          setMessages(prev => [...prev, messageData]);
        }
      } catch (err) {
        setMessages(prev => [...prev, { text: event.data }]);
      }
    };

    socketRef.current.onerror = (error) => {
      console.error("WebSocket error:", error);
    };

    socketRef.current.onclose = () => {
      console.log("WebSocket disconnected");
    };

    return () => {
      if (socketRef.current) {
        socketRef.current.close();
      }
      connectionAttempted.current = false;
    };
  }, [room, username]);

  useEffect(() => {
    if (messagesContainerRef.current) {
      messagesContainerRef.current.scrollTop = messagesContainerRef.current.scrollHeight;
    }
  }, [messages]);

  const sendMessage = () => {
    if (input.trim() && socketRef.current?.readyState === WebSocket.OPEN) {
      const localMessage = {
        text: input,
        sender: username,
        timestamp: new Date()
      };
      setMessages(prev => [...prev, localMessage]);
      const messageKey = `${localMessage.sender}:${localMessage.text}:${localMessage.timestamp.getTime()}`;
      localMessagesRef.current.add(messageKey);
      socketRef.current.send(input);
      setInput("");
    }
  };

  const handleLeave = () => {
    if (socketRef.current) {
      socketRef.current.close();
    }
    connectionAttempted.current = false;
    onLeave();
  }

  if (!room) {
    return <div>No chat room selected</div>;
  }

  return (
    <div className="chat-outer">
      <div className="chat-header">
        <div>
          <h2>{room.name}</h2>
          {room.description && <p className="chat-desc">{room.description}</p>}
        </div>
        <button className="leave-btn" onClick={handleLeave}>Leave Room</button>
      </div>
      <div className="chat-container" ref={messagesContainerRef}>
        {messages.length === 0 ? (
          <div className="no-messages">No messages yet. Be the first to say something!</div>
        ) : (
          messages.map((msg, i) => (
            <div key={i} className={msg.type === 'system' ? 'system-message' : 'message'}>
              {msg.type === 'system' ? (
                <span>{msg.text}</span>
              ) : (
                <>
                  <div className="message-header">
                    {msg.profileImage ? (
                      <img 
                        src={`${API_URL}${msg.profileImage}`} 
                        alt="Profile" 
                        className="profile-thumbnail" 
                      />
                    ) : (
                      <div className="profile-initial">
                        {msg.sender?.charAt(0)?.toUpperCase() || '?'}
                      </div>
                    )}
                    <span className="sender">{msg.sender || 'Anonymous'}</span>
                    {msg.timestamp && (
                      <span className="timestamp">
                        {new Date(msg.timestamp).toLocaleTimeString()}
                      </span>
                    )}
                  </div>
                  <div className="message-text">{msg.text}</div>
                </>
              )}
            </div>
          ))
        )}
      </div>
      <div className="message-input">
        <input
          type="text"
          value={input}
          placeholder="Type a message..."
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && sendMessage()}
        />
        <button onClick={sendMessage}>Send</button>
      </div>
    </div>
  );
}