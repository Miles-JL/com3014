import { useState, useEffect } from 'react';
import axios from 'axios';

export default function ProfilePage({ onBack }) {
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
      
      const response = await axios.get(`/api/user/profile`, {
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
      
      // First update the profile in the user service
      const updateResponse = await axios.put(
        `/api/user/profile`,
        {
          username: newUsername !== profile.username ? newUsername : undefined,
          profileDescription: profile.profileDescription
        },
        {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
            'Accept': 'application/json'
          },
          withCredentials: true
        }
      );
      
      // If username was changed, also update it in the auth service
      if (newUsername !== profile.username) {
        try {
          const authResponse = await axios.post(
            `/api/auth/update-username`,
            { newUsername },
            {
              headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json',
                'Accept': 'application/json'
              },
              withCredentials: true
            }
          );
          
          if (authResponse.data && authResponse.data.token) {
            // Update the stored token with the new one containing the updated username
            localStorage.setItem('token', authResponse.data.token);
          }
        } catch (authError) {
          console.error('Error updating username in auth service:', authError);
          // Non-critical error, continue with the update
        }
      }
      
      // Update local profile data with the response from the user service
      setProfile(prev => ({
        ...prev, 
        username: newUsername,
        profileDescription: updateResponse.data.profileDescription || prev.profileDescription
      }));
      
      setUpdateSuccess(true);
      // Show success message for 1 second then go back
      setTimeout(() => {
        if (onBack) onBack();
      }, 1000);
      
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
        `/api/user/profile-image`,
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
      // Show success message for 1 second then go back
      setTimeout(() => {
        if (onBack) onBack();
      }, 1000);
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