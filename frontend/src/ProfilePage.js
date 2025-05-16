import { useState, useEffect } from 'react';
import axios from 'axios';

const API_URL = 'https://localhost:5001';

export default function ProfilePage() {
  const [profile, setProfile] = useState({
    username: '',
    profileImage: '',
    profileDescription: '',
    location: ''
  });
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [file, setFile] = useState(null);
  const [filePreview, setFilePreview] = useState('');
  
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
      setIsLoading(false);
    } catch (err) {
      setError('Failed to load profile');
      setIsLoading(false);
    }
  };
  
  const handleUpdate = async (e) => {
    e.preventDefault();
    
    try {
      const token = localStorage.getItem('token');
      await axios.put(
        `${API_URL}/api/user/profile`,
        {
          profileDescription: profile.profileDescription,
          location: profile.location
        },
        {
          headers: {
            Authorization: `Bearer ${token}`
          }
        }
      );
      
      alert('Profile updated successfully!');
    } catch (err) {
      setError('Failed to update profile');
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
      alert('Profile image uploaded successfully!');
    } catch (err) {
      setError('Failed to upload image');
    }
  };
  
  if (isLoading) return <div>Loading profile...</div>;
  
  return (
    <div className="profile-page">
      <h2>Your Profile</h2>
      
      {error && <div className="error">{error}</div>}
      
      <div className="profile-image-section">
        <div className="current-image">
          <h3>Profile Image</h3>
          {profile.profileImage ? (
            <img 
              src={`${API_URL}${profile.profileImage}`} 
              alt="Profile" 
              style={{ width: 150, height: 150, objectFit: 'cover' }} 
            />
          ) : (
            <div className="no-image">No profile image</div>
          )}
        </div>
        
        <div className="upload-section">
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
          
          <button onClick={handleUpload} disabled={!file}>
            Upload Image
          </button>
        </div>
      </div>
      
      <form onSubmit={handleUpdate} className="profile-form">
        <div className="form-group">
          <label>Username</label>
          <input 
            type="text" 
            value={profile.username} 
            disabled 
          />
        </div>
        
        <div className="form-group">
          <label>Profile Description</label>
          <textarea
            value={profile.profileDescription}
            onChange={(e) => setProfile({...profile, profileDescription: e.target.value})}
            rows={4}
          />
        </div>
        
        <div className="form-group">
          <label>Location</label>
          <input 
            type="text" 
            value={profile.location}
            onChange={(e) => setProfile({...profile, location: e.target.value})}
          />
        </div>
        
        <button type="submit">Save Changes</button>
      </form>
    </div>
  );
}