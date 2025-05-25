import { useEffect, useRef, useState } from "react";

const API_URL = "http://localhost:80";

export default function DMChat({ recipient, onLeave }) {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [username, setUsername] = useState("");
  const [profileImage, setProfileImage] = useState("");

  const socketRef = useRef(null);
  const messagesContainerRef = useRef(null);
  const localMessagesRef = useRef(new Set());

  const connectionAttempted = useRef(false);
  const reconnectTimeout = useRef(null);
  const heartbeatInterval = useRef(null);
  const hasLeft = useRef(false);
  const intentionalCloseRef = useRef(false); // Tracks if the socket was closed on purpose

  useEffect(() => {
    const fetchUserProfile = async () => {
      const token = localStorage.getItem("token");
      if (!token) return;

      try {
        const response = await fetch(`/api/user/profile`, {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.ok) {
          const data = await response.json();
          setUsername(data.username);
          setProfileImage(data.profileImage);
        } else {
          const payload = JSON.parse(atob(token.split(".")[1]));
          setUsername(payload.unique_name || payload.name || "");
        }
      } catch (err) {
        console.error("Profile fetch error:", err);
      }
    };

    fetchUserProfile();
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token || !recipient || !username || connectionAttempted.current) return;

    connectionAttempted.current = true;
    hasLeft.current = false;
    connectWebSocket(token);

    return () => cleanupWebSocket();
  }, [recipient, username]);

  useEffect(() => {
    const fetchChatHistory = async () => {
      const token = localStorage.getItem("token");
      if (!token || !recipient) return;

      try {
        const response = await fetch(`/api/message/history?recipientId=${recipient.id}&limit=50`, {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.ok) {
          const history = await response.json();
          const formatted = history.map(async (msg) => {
            if (msg.senderId === recipient.id) {
              return {
                ...msg,
                text: msg.content,
                profileImage: recipient.profileImage,
                sender: recipient.username,
              };
            }

            const senderResponse = await fetch(`/api/user/${msg.senderId}`, {
              headers: { Authorization: `Bearer ${token}` },
            });

            if (senderResponse.ok) {
              const senderData = await senderResponse.json();
              return {
                ...msg,
                text: msg.content,
                profileImage: senderData.profileImage,
                sender: senderData.username,
              };
            }

            return {
              ...msg,
              text: msg.content,
              profileImage: null,
              sender: "Unknown",
            };
          });

          const resolvedMessages = await Promise.all(formatted);

          setMessages((prev) => {
            const existingIds = new Set(prev.map((msg) => msg.id));
            const uniqueHistory = resolvedMessages.filter((msg) => !existingIds.has(msg.id));
            const systemMessages = prev.filter((msg) => msg.type === "system");
            return [...systemMessages, ...uniqueHistory, ...prev.filter((msg) => msg.type !== "system")];
          });
        }
      } catch (err) {
        console.error("Chat history fetch failed:", err);
      }
    };

    fetchChatHistory();
  }, [recipient]);

  useEffect(() => {
    if (messagesContainerRef.current) {
      messagesContainerRef.current.scrollTop = messagesContainerRef.current.scrollHeight;
    }
  }, [messages]);

  const cleanupWebSocket = () => {
    connectionAttempted.current = false;
    clearInterval(heartbeatInterval.current);
    clearTimeout(reconnectTimeout.current);
    heartbeatInterval.current = null;
    reconnectTimeout.current = null;

    if (socketRef.current) {
      socketRef.current.close();
      socketRef.current = null;
    }

    localMessagesRef.current = new Set();
    hasLeft.current = false;
    // Do not reset intentionalCloseRef here to avoid race conditions
  };

  const connectWebSocket = (token) => {
    if (hasLeft.current) return;

    const wsUrl = `ws://${window.location.host}/ws/dm?access_token=${token}&recipientId=${recipient.id}`;
    socketRef.current = new WebSocket(wsUrl);

    socketRef.current.onopen = () => {
      if (hasLeft.current) {
        socketRef.current.close();
        return;
      }

      console.log(`Connected for DM with ${recipient.username}`);
      clearInterval(heartbeatInterval.current);
      clearTimeout(reconnectTimeout.current);

      // Only show reconnection message if previously trying to reconnect
      setMessages((prev) => {
        const last = prev[prev.length - 1];
        if (last?.type === "system" && last.text === "Attempting to reconnect...") {
          const updated = [...prev];
          updated[updated.length - 1] = {
            type: "system",
            text: "Reconnected successfully.",
            timestamp: new Date(),
          };
          return updated;
        }
        return prev;
      });

      heartbeatInterval.current = setInterval(() => {
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
        const data = JSON.parse(event.data);
        if (data.text === "__ping__") return;
        setMessages((prev) => [...prev, data]);
      } catch (err) {
        console.error("Message parse error:", err);
      }
    };

    socketRef.current.onclose = () => {
      clearInterval(heartbeatInterval.current);
      heartbeatInterval.current = null;

      if (intentionalCloseRef.current) {
        console.log("WebSocket closed due to Leave DM.");
        intentionalCloseRef.current = false;
        return;
      }

      console.log("WebSocket disconnected. Retrying in 5 seconds...");
      setMessages((prev) => [
        ...prev,
        {
          type: "system",
          text: "Attempting to reconnect...",
          timestamp: new Date(),
        },
      ]);

      reconnectTimeout.current = setTimeout(() => {
        const retryToken = localStorage.getItem("token");
        if (retryToken && connectionAttempted.current && !hasLeft.current) {
          connectWebSocket(retryToken);
        }
      }, 5000);
    };

    socketRef.current.onerror = (err) => {
      console.error("WebSocket error:", err);
    };
  };

  const handleLeave = () => {
    hasLeft.current = true;
    intentionalCloseRef.current = true;

    if (socketRef.current) {
      socketRef.current.close();
    }

    cleanupWebSocket();
    onLeave();
  };

  const sendMessage = () => {
    if (!input.trim() || socketRef.current?.readyState !== WebSocket.OPEN) return;

    const token = localStorage.getItem("token");
    const userId = getUserIdFromToken(token);
    if (!userId) return;

    const msg = {
      senderId: userId,
      sender: username,
      profileImage,
      recipientId: recipient.id,
      text: input.trim(),
      timestamp: new Date().toISOString(),
    };

    socketRef.current.send(JSON.stringify(msg));
    setMessages((prev) => [...prev, msg]);
    setInput("");
  };

  const getUserIdFromToken = (token) => {
    try {
      const payload = JSON.parse(atob(token.split(".")[1]));
      return parseInt(payload.uid || payload.nameidentifier || payload.sub);
    } catch (err) {
      console.error("Failed to extract user ID:", err);
      return null;
    }
  };

  if (!recipient) {
    return <div>Select a user to DM</div>;
  }

  return (
    <div className="chat-outer">
      <div className="chat-header">
        <h2>DM with {recipient.username}</h2>
        <button className="leave-btn" onClick={handleLeave}>
          Leave DM
        </button>
      </div>
      <div className="chat-container" ref={messagesContainerRef}>
        {messages.length === 0 ? (
          <div className="no-messages">No messages yet. Say hi!</div>
        ) : (
          messages.map((msg, i) => (
            <div key={i} className={msg.type === "system" ? "system-message" : "message"}>
              {msg.type === "system" ? (
                <span>{msg.text}</span>
              ) : (
                <>
                  <div className="message-header">
                    {msg.profileImage ? (
                      <img src={msg.profileImage} alt="Profile" className="profile-thumbnail" />
                    ) : (
                      <div className="profile-initial">{msg.sender?.charAt(0).toUpperCase() || "?"}</div>
                    )}
                    <span className="sender">{msg.sender || "Unknown"}</span>
                    {msg.timestamp && (
                      <span className="timestamp">{new Date(msg.timestamp).toLocaleTimeString()}</span>
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
