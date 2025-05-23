import { useEffect, useRef, useState } from "react";

const API_URL = "http://localhost:80";

export default function DMChat({ recipient, onLeave }) {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const socketRef = useRef(null);
  const messagesContainerRef = useRef(null);
  const [username, setUsername] = useState("");
  const [profileImage, setProfileImage] = useState("");
  const localMessagesRef = useRef(new Set());
  const connectionAttempted = useRef(false);
  let reconnectTimeout = null;

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
    if (socketRef.current) socketRef.current.close();

    const wsUrl = `ws://${API_URL.replace(
      "http://",
      ""
    )}/ws/dm?access_token=${token}&recipientId=${recipient.id}`;
    socketRef.current = new WebSocket(wsUrl);

    let heartbeatInterval = null;

    socketRef.current.onopen = () => {
      console.log(`Connected for DM with ${recipient.username}`);
      clearInterval(heartbeatInterval);
      clearTimeout(reconnectTimeout);

      setMessages((prev) => {
        // Only add the system message if there are no existing messages
        if (prev.length === 0) {
          return [
            {
              type: "system",
              text: `You started a DM with ${recipient.username}`,
              timestamp: new Date(),
            },
          ];
        }
        return prev;
      });

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
          const messageKey = `${messageData.sender}:$${
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
      console.log("WebSocket disconnected. Retrying in 5 seconds...");
      setMessages((prev) => [
        ...prev,
        {
          type: "system",
          text: "Attempting to reconnect...",
          timestamp: new Date(),
        },
      ]);
      clearInterval(heartbeatInterval);
      reconnectTimeout = setTimeout(connectWebSocket, 5000);
    };

    return () => {
      if (socketRef.current) {
        socketRef.current.close();
      }
      clearInterval(heartbeatInterval);
      connectionAttempted.current = false;
      clearTimeout(reconnectTimeout);
    };
  }, [recipient, username]);

  useEffect(() => {
    const fetchChatHistory = async () => {
      try {
        const token = localStorage.getItem("token");
        if (!token || !recipient) return;

        const response = await fetch(
          `${API_URL}/api/message/history?recipientId=${recipient.id}&limit=50`,
          {
            headers: {
              Authorization: `Bearer ${token}`,
            },
          }
        );

        if (response.ok) {
          const history = await response.json();
          const formattedHistory = history.map((msg) => ({
            ...msg,
            text: msg.content, // Map 'content' to 'text'
          }));

          setMessages((prev) => {
            const existingIds = new Set(prev.map((msg) => msg.id));
            const uniqueHistory = formattedHistory.filter(
              (msg) => !existingIds.has(msg.id)
            );
            const systemMessages = prev.filter((msg) => msg.type === "system");
            return [
              ...systemMessages,
              ...uniqueHistory,
              ...prev.filter((msg) => msg.type !== "system"),
            ];
          });
        } else {
          console.error("Failed to fetch chat history", response.statusText);
        }
      } catch (err) {
        console.error("Error fetching chat history:", err);
      }
    };

    fetchChatHistory();
  }, [recipient]);

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

  function connectWebSocket() {
    const token = localStorage.getItem("token");
    if (!token || !recipient) return;

    const wsUrl = `ws://${API_URL.replace(
      "http://",
      ""
    )}/ws/dm?access_token=${token}&recipientId=${recipient.id}`;
    socketRef.current = new WebSocket(wsUrl);

    let heartbeatInterval = null;

    socketRef.current.onopen = () => {
      console.log(`Connected for DM with ${recipient.username}`);
      clearInterval(heartbeatInterval);
      clearTimeout(reconnectTimeout);

      setMessages((prev) => {
        if (prev.length === 0) {
          return [
            {
              type: "system",
              text: `You started a DM with ${recipient.username}`,
              timestamp: new Date(),
            },
          ];
        }
        return prev;
      });

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
        if (messageData.text === "__ping__") return;
        setMessages((prev) => [...prev, messageData]);
      } catch (err) {
        console.error("Error parsing WebSocket message:", err);
      }
    };

    socketRef.current.onclose = () => {
      console.log("WebSocket disconnected. Retrying in 5 seconds...");
      setMessages((prev) => [
        ...prev,
        {
          type: "system",
          text: "Attempting to reconnect...",
          timestamp: new Date(),
        },
      ]);
      clearInterval(heartbeatInterval);
      reconnectTimeout = setTimeout(connectWebSocket, 5000);
    };

    socketRef.current.onerror = (error) => {
      console.error("WebSocket error:", error);
    };
  }

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
