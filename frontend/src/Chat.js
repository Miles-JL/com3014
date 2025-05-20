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
        setUsername(payload.unique_name || payload.name || "");
      }
    } catch (err) {
      console.error("Failed to decode token:", err);
    }
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token || !room) return;

    localMessagesRef.current = new Set();

    if (socketRef.current) {
      socketRef.current.close();
    }

    const wsUrl = `ws://${API_URL.replace("http://", "")}/ws/chat?token=${token}&roomId=${room.id}`;
    socketRef.current = new WebSocket(wsUrl);

    socketRef.current.onopen = () => {
      console.log(`Connected to room: ${room.name}`);
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

    socketRef.current.onclose = () => {
      console.log("WebSocket disconnected");
    };

    return () => {
      if (socketRef.current) {
        socketRef.current.close();
      }
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
        <button className="leave-btn" onClick={onLeave}>Leave Room</button>
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