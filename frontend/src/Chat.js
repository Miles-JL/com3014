import { useEffect, useRef, useState } from "react";

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
          const response = await fetch(`/api/user/profile`, {
            headers: {
              Authorization: `Bearer ${token}`,
            },
          });

          if (response.ok) {
            const data = await response.json();
            setUsername(data.username);
            setProfileImage(data.profileImage);
          } else {
            // Fallback to token decoding if profile fetch fails
            try {
              const payload = JSON.parse(atob(token.split(".")[1]));
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

    const wsUrl = `ws://${window.location.host}/ws/chat?token=${token}&roomId=${room.id}`;
    socketRef.current = new WebSocket(wsUrl);

    let heartbeatInterval = null;

    socketRef.current.onopen = () => {
      console.log(`Connected to room: ${room.name}`);

      // Add system message about joining
      setMessages([
        {
          type: "system",
          text: `You joined the room`,
          timestamp: new Date(),
        },
      ]);
      heartbeatInterval = setInterval(() => {
        if (socketRef.current?.readyState === WebSocket.OPEN) {
          socketRef.current.send("__ping__");
        }
      }, 25000);
    };

    socketRef.current.onmessage = (event) => {
      try {
        console.log('Raw WebSocket message:', event.data);
        const messageData = JSON.parse(event.data);
        console.log('Parsed message data:', messageData);

        if (messageData.type === "history") {
          console.log('Received history:', messageData.messages);
          setMessages(messageData.messages || []);
        } else {
          if (
            messageData.sender === username &&
            messageData.type !== "system" &&
            messageData.text
          ) {
            const messageKey = `${messageData.sender}:${
              messageData.text
            }:${new Date(messageData.timestamp).getTime()}`;
            console.log('Generated message key:', messageKey);
            if (localMessagesRef.current.has(messageKey)) {
              console.log('Duplicate message, skipping');
              return;
            }
          }
          console.log('Adding new message:', messageData);
          setMessages((prev) => [...prev, messageData]);
        }
      } catch (err) {
        console.error('Error processing message:', err);
        console.log('Raw message that caused error:', event.data);
        setMessages((prev) => [...prev, { text: event.data, type: 'error' }]);
      }
    };

    socketRef.current.onerror = (error) => {
      console.error("WebSocket error:", error);
    };

    socketRef.current.onclose = () => {
      console.log("WebSocket disconnected");
      clearInterval(heartbeatInterval);
    };

    return () => {
      if (socketRef.current) {
        socketRef.current.close();
      }
      clearInterval(heartbeatInterval);
      connectionAttempted.current = false;
    };
  }, [room, username]);

  useEffect(() => {
    if (messagesContainerRef.current) {
      messagesContainerRef.current.scrollTop =
        messagesContainerRef.current.scrollHeight;
    }
  }, [messages]);

  const sendMessage = () => {
    if (input.trim() && socketRef.current?.readyState === WebSocket.OPEN) {
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
  };

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
        <button className="leave-btn" onClick={handleLeave}>
          Leave Room
        </button>
      </div>
      <div className="chat-container" ref={messagesContainerRef}>
        {messages.length === 0 ? (
          <div className="no-messages">
            No messages yet. Be the first to say something!
          </div>
        ) : (
          messages.map((msg, i) => (
            <div
              key={i}
              className={msg.type === "system" ? "system-message" : "message"}
            >
              {msg.type === "system" ? (
                <span>{msg.text}</span>
              ) : (
                <>
                  <div className="message-header">
                    {console.log('Profile image URL for message:', msg.profileImage, 'Message:', msg)}
                    <img
                      src={msg.profileImage}
                      alt="Profile"
                      className="profile-thumbnail"
                      onError={(e) => {
                        console.error('Error loading profile image:', e.target.src);
                        e.target.style.display = 'none';
                      }}
                    />
                    <span className="sender">{msg.sender || "Anonymous"}</span>
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
