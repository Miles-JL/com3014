import { useState, useEffect } from 'react';
import axios from 'axios';

const API_URL = 'http://localhost:80';

export default function ProfilePage() {
  const [profile, setProfile] = useState({
    username: '',
    profileImage: '',
    profileDescription: ''
  });
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [file, setFile] = useState(null);
  const [filePreview, setFilePreview] = useState('');
  const [newUsername, setNewUsername] = useState('');
  const [updateSuccess, setUpdateSuccess] = useState(false);
  
  useEffect(() => {
    fetchProfile();
  }, []);
  
  const fetchProfile = async () => {
    try {
      const token = localStorage.getItem('token');
      if (!token) {
        setError('Not authenticated');
        setIsLoading(false);
        return;
      }
      
      const response = await axios.get(`${API_URL}/api/user/profile`, {
        headers: {
          Authorization: `Bearer ${token}`
        }
      });
      
      setProfile(response.data);
      setNewUsername(response.data.username);
      setIsLoading(false);
    } catch (err) {
      console.error('Error fetching profile:', err);
      setError('Failed to load profile');
      setIsLoading(false);
    }
  };
  
  const handleUpdate = async (e) => {
    e.preventDefault();
    setUpdateSuccess(false);
    setError('');
    
    try {
      const token = localStorage.getItem('token');
      
      // Only update username if it has changed
      if (newUsername !== profile.username) {
        // First update username in auth service
        const authResponse = await axios.post(
          'http://localhost:5106/api/Auth/update-username',
          { newUsername },
          {
            headers: {
              Authorization: `Bearer ${token}`,
              'Content-Type': 'application/json'
            },
            withCredentials: true
          }
        );
        
        if (authResponse.data && authResponse.data.token) {
          // Update the stored token with the new one containing the updated username
          localStorage.setItem('token', authResponse.data.token);
        }
      }
      
      // Then update the rest of the profile in the user service
      const updateResponse = await axios.put(
        `${API_URL}/api/user/profile`,
        {
          username: newUsername !== profile.username ? newUsername : undefined,
          profileDescription: profile.profileDescription
        },
        {
          headers: {
            Authorization: `Bearer ${token}`
          }
        }
      );
      
      // If we get here, both updates were successful
      
      // Sync with Auth service to ensure data consistency
      try {
        await axios.post(
          `${API_URL}/api/user/sync-with-auth`,
          {},
          {
            headers: {
              Authorization: `Bearer ${token}`
            }
          }
        );
      } catch (syncError) {
        console.warn('Failed to sync with auth service:', syncError);
        // Non-critical error, continue with the update
      }
      
      setUpdateSuccess(true);
      setTimeout(() => setUpdateSuccess(false), 3000);
      
      // Update local profile data
      setProfile(prev => ({
        ...prev, 
        username: newUsername,
        profileDescription: updateResponse.data.profileDescription || prev.profileDescription
      }));
      
    } catch (err) {
      console.error('Error updating profile:', err);
      if (err.response) {
        // Handle HTTP errors
        if (err.response.status === 400) {
          setError('Invalid request. ' + (err.response.data?.message || 'Please check your input.'));
        } else if (err.response.status === 401) {
          setError('Session expired. Please log in again.');
        } else if (err.response.status === 409) {
          setError('Username is already taken. Please choose another one.');
        } else {
          setError(`Server error (${err.response.status}): ${err.response.data?.message || 'Please try again later.'}`);
        }
      } else if (err.request) {
        // The request was made but no response was received
        setError('Unable to connect to the server. Please check your connection.');
      } else {
        // Something happened in setting up the request
        setError('An unexpected error occurred. Please try again.');
      }
    }
  };
  
  const handleFileChange = (e) => {
    const selectedFile = e.target.files[0];
    setFile(selectedFile);
    
    // Create preview
    if (selectedFile) {
      const reader = new FileReader();
      reader.onloadend = () => {
        setFilePreview(reader.result);
      };
      reader.readAsDataURL(selectedFile);
    }
  };
  
  const handleUpload = async () => {
    if (!file) return;
    
    const formData = new FormData();
    formData.append('file', file);
    
    try {
      const token = localStorage.getItem('token');
      const response = await axios.post(
        `${API_URL}/api/user/profile-image`,
        formData,
        {
          headers: {
            'Content-Type': 'multipart/form-data',
            Authorization: `Bearer ${token}`
          }
        }
      );
      
      setProfile(prev => ({
        ...prev,
        profileImage: response.data.profileImage
      }));
      
      setFile(null);
      setFilePreview('');
      setUpdateSuccess(true);
      setTimeout(() => setUpdateSuccess(false), 3000);
    } catch (err) {
      console.error('Error uploading image:', err);
      setError('Failed to upload image');
    }
  };
  
  if (isLoading) return <div>Loading profile...</div>;
  
  return (
    <div className="profile-page">
      <h2>Your Profile</h2>
      
      {error && <div className="error">{error}</div>}
      {updateSuccess && <div className="success">Profile updated successfully!</div>}
      
      <div className="profile-image-section">
        <div className="current-image">
          <h3>Profile Image</h3>
          {profile.profileImage ? (
            <img 
              src={profile.profileImage} 
              alt="Profile" 
              style={{ width: 150, height: 150, objectFit: 'cover' }} 
            />
          ) : (
            <div className="no-image">No profile image</div>
          )}
        </div>
        
        <div className="upload-section">
          <h3>Upload New Image</h3>
          <input type="file" accept="image/*" onChange={handleFileChange} />
          
          {filePreview && (
            <div className="preview">
              <h4>Preview:</h4>
              <img 
                src={filePreview} 
                alt="Preview" 
                style={{ width: 100, height: 100, objectFit: 'cover' }} 
              />
            </div>
          )}
          
          <button 
            className="upload-button" 
            onClick={handleUpload} 
            disabled={!file}
          >
            Upload Image
          </button>
        </div>
      </div>
      
      <form onSubmit={handleUpdate} className="profile-form">
        <div className="form-group">
          <label>Username</label>
          <input 
            type="text" 
            value={newUsername}
            onChange={(e) => setNewUsername(e.target.value)}
          />
        </div>
        
        <div className="form-group">
          <label>Profile Description</label>
          <textarea
            value={profile.profileDescription || ''}
            onChange={(e) => setProfile({...profile, profileDescription: e.target.value})}
            rows={4}
            placeholder="Tell others about yourself..."
          />
        </div>
        
        <button type="submit">Save Changes</button>
      </form>
    </div>
  );
}