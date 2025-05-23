import { useEffect, useRef, useState } from "react";

const API_URL = "http://localhost:5247";

export default function DMChat({ recipient, onLeave }) {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const socketRef = useRef(null);
  const messagesContainerRef = useRef(null);
  const [username, setUsername] = useState("");
  const [profileImage, setProfileImage] = useState("");
  const localMessagesRef = useRef(new Set());
  const connectionAttempted = useRef(false);

  useEffect(() => {
    const fetchUserProfile = async () => {
      try {
        const token = localStorage.getItem("token");
        if (token) {
          const response = await fetch(`${API_URL}/api/user/profile`, {
            headers: {
              Authorization: `Bearer ${token}`,
            },
          });

          if (response.ok) {
            const data = await response.json();
            setUsername(data.username);
            setProfileImage(data.profileImage);
          } else {
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
    if (!token || !recipient || !username || connectionAttempted.current)
      return;

    connectionAttempted.current = true;
    localMessagesRef.current = new Set();
    setMessages([]);
    if (socketRef.current) socketRef.current.close();

    const wsUrl = `ws://${API_URL.replace(
      "http://",
      ""
    )}/ws/dm?access_token=${token}&recipientId=${recipient.id}`;
    socketRef.current = new WebSocket(wsUrl);

    let heartbeatInterval = null;

    socketRef.current.onopen = () => {
      console.log(`Connected for DM with ${recipient.username}`);

      setMessages([
        {
          type: "system",
          text: `You started a DM with ${recipient.username}`,
          timestamp: new Date(),
        },
      ]);

      // Start heartbeat
      heartbeatInterval = setInterval(() => {
        if (socketRef.current?.readyState === WebSocket.OPEN) {
          socketRef.current.send(
            JSON.stringify({
              senderId: getUserIdFromToken(token),
              recipientId: recipient.id,
              text: "__ping__",
              timestamp: new Date().toISOString(),
            })
          );
        }
      }, 25000);
    };

    socketRef.current.onmessage = (event) => {
      try {
        const messageData = JSON.parse(event.data);

        // Ignore pings
        if (messageData.text === "__ping__") return;

        if (messageData.type === "history") {
          setMessages(messageData.messages || []);
          return;
        }

        // Avoid duplicates
        if (
          messageData.sender === username &&
          messageData.type !== "system" &&
          messageData.text
        ) {
          const messageKey = `${messageData.sender}:${
            messageData.text
          }:${new Date(messageData.timestamp).getTime()}`;
          if (localMessagesRef.current.has(messageKey)) return;
        }

        setMessages((prev) => [...prev, messageData]);
      } catch (err) {
        setMessages((prev) => [...prev, { text: event.data }]);
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
  }, [recipient, username]);
  

  useEffect(() => {
    if (messagesContainerRef.current) {
      messagesContainerRef.current.scrollTop =
        messagesContainerRef.current.scrollHeight;
    }
  }, [messages]);

  function getUserIdFromToken(token) {
    try {
      const payload = JSON.parse(atob(token.split(".")[1]));
      return parseInt(payload.uid || payload.nameidentifier || payload.sub);
    } catch (err) {
      console.error("Failed to extract userId from token:", err);
      return null;
    }
  }

  const sendMessage = () => {
    if (input.trim() && socketRef.current?.readyState === WebSocket.OPEN) {
      const token = localStorage.getItem("token");
      const userId = getUserIdFromToken(token);
      if (!userId) return;

      const message = {
        senderId: userId,
        sender: username,
        recipientId: recipient.id,
        text: input.trim(),
        timestamp: new Date().toISOString(),
      };

      socketRef.current.send(JSON.stringify(message));

      setMessages((prev) => [...prev, { ...message, sender: username }]);
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

  if (!recipient) {
    return <div>Select a user to DM</div>;
  }

  return (
    <div className="chat-outer">
      <div className="chat-header">
        <div>
          <h2>DM with {recipient.username}</h2>
        </div>
        <button className="leave-btn" onClick={handleLeave}>
          Leave DM
        </button>
      </div>
      <div className="chat-container" ref={messagesContainerRef}>
        {messages.length === 0 ? (
          <div className="no-messages">No messages yet. Say hi!</div>
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
                    {msg.profileImage ? (
                      <img
                        src={`${API_URL}${msg.profileImage}`}
                        alt="Profile"
                        className="profile-thumbnail"
                      />
                    ) : (
                      <div className="profile-initial">
                        {msg.sender?.charAt(0)?.toUpperCase() || "?"}
                      </div>
                    )}
                    <span className="sender">{msg.sender || "Unknown"}</span>
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
