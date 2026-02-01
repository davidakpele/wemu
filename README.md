# StreamSphere - Live Streaming Platform Documentation

## **Project Overview**
StreamSphere is a comprehensive real-time video/audio streaming platform that enables users to broadcast live content, join existing streams, and interact through chat. The platform features a modern web interface with mobile-responsive design, real-time media streaming using WebRTC, and robust user management capabilities.

## **Core Features**

### **1. User Authentication & Management**
- **Registration System**: Users can create accounts with email, username, first name, last name, and password
- **Login System**: Secure authentication with session persistence
- **Session Management**: Automatic session restoration using local storage
- **User Profiles**: Display user information including name, username, and email

### **2. Stream Management**
- **Create Streams**: Users can start new live streams with custom titles, descriptions, categories, and stream types (audio-only or video)
- **Join Streams**: Browse and join active streams from other users
- **Stream Types**: Support for both audio-only and video streaming
- **Stream Categories**: Organized streams by categories (General, Music, Gaming, Education, Entertainment, Technology, Other)

### **3. Real-time Media Streaming**
- **WebRTC Implementation**: Peer-to-peer media streaming using WebRTC technology
- **Media Controls**:
  - Mute/unmute audio
  - Start/stop video
  - Switch between camera and audio-only modes
  - Media production and consumption management
- **Quality Settings**: Configurable video and audio quality options
- **ICE Servers**: Integration with STUN and TURN servers for NAT traversal

### **4. Co-host System**
- **Invitation System**: Hosts can invite viewers to become co-hosts
- **Role Management**: Co-hosts can produce media and have elevated privileges
- **Accept/Reject**: Users can accept or decline co-host invitations
- **Co-host Media**: Separate video containers for co-host streams

### **5. Chat System**
- **Real-time Messaging**: Live chat synchronized across all stream participants
- **Message Types**: Text messages and system notifications
- **User Identification**: Messages display sender username and role
- **Chat Moderation**: Hosts can manage chat participation

### **6. User Management & Moderation**
- **Viewer Management**: Real-time viewer list with user roles (host, co-host, viewer)
- **User Actions**: Hosts can perform actions on viewers:
  - Invite to co-host
  - Remove from stream
  - Block from stream (permanent ban)
- **Block System**: Blocked users cannot rejoin the stream
- **Auto-cleanup**: Automatic removal of duplicate user connections

### **7. Stream Persistence & Recovery**
- **Host Grace Period**: Streams remain active for 30 minutes after host disconnection
- **Host Rejoin**: Original hosts can reclaim their stream within grace period
- **Auto-termination**: Streams automatically end after 30 minutes of host absence
- **Connection Recovery**: Automatic reconnection with SignalR

### **8. Mobile Responsive Design**
- **Adaptive Layout**: Different views for desktop and mobile devices
- **Mobile Navigation**: Bottom navigation bar for mobile users
- **Responsive Views**:
  - Stream view (main content)
  - Profile view
  - Viewers list
  - Settings view
- **Touch-friendly Controls**: Optimized UI elements for mobile interaction

## **Technical Architecture**

### **Frontend Components**

#### **1. Authentication Interface**
- Login and registration forms
- Form validation and error handling
- Toast notifications for user feedback
- Responsive authentication layout

#### **2. Main Application Interface**
- **Header**: Navigation controls and stream title
- **Streams List**: Grid display of active streams with join buttons
- **Stream View**: Main streaming interface with:
  - Video player container
  - Local and remote video elements
  - Co-host video containers
  - Stream controls
  - Chat section
  - Viewers list (desktop)
  - Statistics bar

#### **3. Modal Windows**
- **Start Stream Modal**: Configuration for new streams
- **Menu Modal**: User options (logout)
- **User Actions Modal**: Moderation tools for hosts
- **Co-host Invite Modal**: Invitation acceptance/rejection
- **Confirmation Modal**: Action confirmation dialogs
- **Viewers Modal**: Mobile viewers list

#### **4. Mobile Views**
- **Stream View**: Adaptive video and chat layout
- **Profile View**: User information display
- **Viewers View**: Mobile-optimized viewers list
- **Settings View**: Stream quality and notification settings

### **Backend Integration**

#### **1. API Endpoints**
- Authentication endpoints (login/register)
- Stream management endpoints
- Real-time SignalR hub connections

#### **2. SignalR Hub Methods**
- **Stream Management**: Start, join, leave, end streams
- **Media Handling**: Produce, consume, pause, resume media
- **Co-host Management**: Invite, accept, reject, remove co-hosts
- **User Management**: Remove, block users
- **Messaging**: Send and receive chat messages
- **WebRTC Signaling**: Offer/answer exchange and ICE candidates

#### **3. Data Models**
- **StreamRoom**: Complete stream state including participants, media settings, and chat
- **StreamConnection**: User connection information and role
- **MediaProducer/MediaConsumer**: WebRTC media track management
- **ChatMessage**: Structured chat messages with sender information

### **Real-time Communication Flow**

#### **1. WebRTC Connection Process**
1. Producer creates media tracks and sends offer
2. Consumer receives offer and creates answer
3. ICE candidate exchange for NAT traversal
4. Media stream establishment
5. Track management (pause/resume/close)

#### **2. Stream Lifecycle**
1. Host starts stream → Room created
2. Users join → Added to participant list
3. Media exchange → WebRTC connections established
4. Host leaves → 30-minute grace period starts
5. Host returns → Stream continues
6. No host return → Stream auto-terminates

#### **3. Co-host Workflow**
1. Host invites viewer → Invitation sent
2. Viewer accepts → Role changed to co-host
3. Co-host can produce media → Separate video container
4. Co-host leaves → Role reverted to viewer

## **User Interface & Experience**

### **Visual Design Elements**
- **Dark Theme**: Modern dark interface with accent colors
- **Live Indicators**: Visual badges showing stream status
- **Avatar System**: User initials displayed in colored circles
- **Status Indicators**: Online/offline status dots
- **Interactive Elements**: Hover states and transition animations

### **Stream Controls**
- **Host Controls**: End stream, toggle media, invite co-hosts
- **Co-host Controls**: Leave co-host role, media toggles
- **Viewer Controls**: Leave stream, chat input
- **Media Controls**: Mute, video toggle, camera switch

### **Notification System**
- **Toast Notifications**: Temporary messages for user actions
- **System Messages**: Automated chat notifications
- **Error Handling**: Clear error messages for failed operations
- **Success Confirmations**: Positive feedback for completed actions

## **Security & Reliability Features**

### **Security Measures**
- **Authentication Required**: All stream interactions require login
- **Role-based Access**: Different permissions for hosts, co-hosts, and viewers
- **Block System**: Prevent abusive users from rejoining
- **Input Sanitization**: Protection against XSS attacks
- **Token-based Authentication**: Secure API access

### **Reliability Features**
- **Automatic Reconnection**: SignalR reconnection handling
- **Graceful Degradation**: Fallback options for failed features
- **Error Recovery**: Attempt to recover from WebRTC failures
- **Connection Monitoring**: Track connection states and health
- **Resource Cleanup**: Proper cleanup of media tracks and connections

### **Performance Optimizations**
- **Lazy Loading**: Resources loaded as needed
- **Efficient Polling**: Smart intervals for stream list updates
- **Media Optimization**: Configurable bitrates and resolutions
- **Connection Pooling**: Reuse of WebRTC peer connections
- **Memory Management**: Cleanup of unused media elements

## **Platform Compatibility**

### **Supported Browsers**
- Modern browsers with WebRTC support
- Mobile browser compatibility
- Responsive design for all screen sizes

### **Device Support**
- Desktop computers
- Tablets
- Mobile phones
- Touch and mouse input

### **Media Requirements**
- Webcam and microphone for broadcasting
- Audio output for viewing
- Stable internet connection

## **Use Cases**

### **For Broadcasters**
1. Start a live stream with custom settings
2. Invite viewers to become co-hosts
3. Manage participants and chat
4. Control media quality and visibility
5. End stream when finished

### **For Viewers**
1. Browse available streams
2. Join streams of interest
3. Participate in chat
4. Accept co-host invitations
5. Adjust viewing settings

### **For Co-hosts**
1. Accept co-host invitations
2. Share audio/video with stream
3. Collaborate with host
4. Return to viewer role when needed

## **Administrative Features**

### **Host Privileges**
- Full control over stream settings
- User moderation capabilities
- Co-host management
- Stream termination authority

### **Moderation Tools**
- Real-time viewer monitoring
- Quick action access via viewer list
- Block and remove functionality
- Chat oversight

## **Future Enhancement Areas**

### **Planned Features**
- Screen sharing capability
- Stream recording and playback
- Advanced analytics and statistics
- Monetization options
- Multi-language support
- Advanced moderation tools
- Stream scheduling
- Social features (following, notifications)

### **Technical Improvements**
- Improved ICE candidate handling
- Better bandwidth adaptation
- Enhanced error reporting
- Performance monitoring
- CDN integration for larger streams
- Advanced codec support

## **Project Structure Summary**

StreamSphere is a full-featured live streaming platform that combines modern web technologies with real-time communication protocols. The system provides a seamless experience for both broadcasters and viewers, with robust moderation tools, reliable streaming capabilities, and an intuitive user interface. The platform is designed to be scalable, maintainable, and extensible for future feature additions.