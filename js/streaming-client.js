// WebRTC Streaming Client Example
// This demonstrates how to integrate with the SignalR hub for live streaming

class StreamingClient {
    constructor(hubUrl, accessToken) {
        this.hubUrl = hubUrl;
        this.accessToken = accessToken;
        this.connection = null;
        this.peerConnections = new Map(); // Map of userId -> RTCPeerConnection
        this.localStream = null;
        this.producers = new Map(); // Map of producerId -> kind
        this.consumers = new Map(); // Map of consumerId -> MediaStream
        
        this.configuration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
                { urls: 'stun:stun2.l.google.com:19302' }
            ],
            iceCandidatePoolSize: 10
        };
    }

    async connect() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`${this.hubUrl}/hubs/streaming`, {
                accessTokenFactory: () => this.accessToken,
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.elapsedMilliseconds < 60000) {
                        return Math.random() * 10000;
                    } else {
                        return null;
                    }
                }
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupEventHandlers();

        try {
            await this.connection.start();
            console.log('SignalR Connected');
            return true;
        } catch (err) {
            console.error('SignalR Connection Error:', err);
            return false;
        }
    }

    setupEventHandlers() {
        // Stream events
        this.connection.on('StreamStarted', (data) => {
            console.log('Stream started:', data);
            this.onStreamStarted(data);
        });

        this.connection.on('JoinedStream', (data) => {
            console.log('Joined stream:', data);
            this.onJoinedStream(data);
        });

        // Media producer/consumer events
        this.connection.on('ProducerCreated', async (data) => {
            console.log('Producer created:', data);
            await this.handleProducerCreated(data);
        });

        this.connection.on('NewProducer', async (data) => {
            console.log('New producer available:', data);
            await this.consumeMedia(data.producerId);
        });

        this.connection.on('ConsumerCreated', async (data) => {
            console.log('Consumer created:', data);
            await this.handleConsumerCreated(data);
        });

        this.connection.on('ProducerPaused', (data) => {
            console.log('Producer paused:', data);
            this.onProducerPaused(data);
        });

        this.connection.on('ProducerResumed', (data) => {
            console.log('Producer resumed:', data);
            this.onProducerResumed(data);
        });

        this.connection.on('ProducerClosed', (data) => {
            console.log('Producer closed:', data);
            this.onProducerClosed(data);
        });

        // Participant events
        this.connection.on('UserJoinedStream', (data) => {
            console.log('User joined:', data);
            this.onUserJoined(data);
        });

        this.connection.on('UserLeftStream', (data) => {
            console.log('User left:', data);
            this.onUserLeft(data);
        });

        // Chat events
        this.connection.on('ReceiveStreamMessage', (data) => {
            console.log('Message received:', data);
            this.onMessageReceived(data);
        });

        // Stream end
        this.connection.on('StreamEnded', (data) => {
            console.log('Stream ended:', data);
            this.onStreamEnded(data);
        });

        // Error handling
        this.connection.on('Error', (data) => {
            console.error('Hub error:', data);
            this.onError(data);
        });

        // WebRTC signaling (legacy support)
        this.connection.on('ReceiveOffer', async (data) => {
            await this.handleReceiveOffer(data);
        });

        this.connection.on('ReceiveAnswer', async (data) => {
            await this.handleReceiveAnswer(data);
        });

        this.connection.on('ReceiveICECandidate', async (data) => {
            await this.handleReceiveICECandidate(data);
        });

        this.connection.onreconnecting(() => {
            console.log('Reconnecting...');
        });

        this.connection.onreconnected(() => {
            console.log('Reconnected');
        });

        this.connection.onclose(() => {
            console.log('Connection closed');
        });
    }

    // Start streaming as host
    async startStream(username, userId, title, description, category, visibility, type) {
        try {
            await this.connection.invoke('StartStream', username, userId, title, description, category, visibility, type);
        } catch (err) {
            console.error('Error starting stream:', err);
            throw err;
        }
    }

    // Join a stream as viewer
    async joinStream(roomId, userId, username) {
        try {
            await this.connection.invoke('JoinStream', roomId, userId, username);
        } catch (err) {
            console.error('Error joining stream:', err);
            throw err;
        }
    }

    // Get user media and start producing
    async startProducing(roomId, constraints = { audio: true, video: true }) {
        try {
            // Get local media stream
            this.localStream = await navigator.mediaDevices.getUserMedia(constraints);
            
            // Produce audio if enabled
            if (constraints.audio && this.localStream.getAudioTracks().length > 0) {
                await this.produceTrack(roomId, 'audio', this.localStream.getAudioTracks()[0]);
            }

            // Produce video if enabled
            if (constraints.video && this.localStream.getVideoTracks().length > 0) {
                await this.produceTrack(roomId, 'video', this.localStream.getVideoTracks()[0]);
            }

            return this.localStream;
        } catch (err) {
            console.error('Error starting production:', err);
            throw err;
        }
    }

    async produceTrack(roomId, kind, track) {
        try {
            // Create peer connection for producing
            const pc = new RTCPeerConnection(this.configuration);
            
            // Add track to peer connection
            pc.addTrack(track, this.localStream);

            // Create offer
            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);

            // Wait for ICE gathering to complete
            await new Promise(resolve => {
                if (pc.iceGatheringState === 'complete') {
                    resolve();
                } else {
                    pc.addEventListener('icegatheringstatechange', () => {
                        if (pc.iceGatheringState === 'complete') {
                            resolve();
                        }
                    });
                }
            });

            // Send offer to server
            await this.connection.invoke('ProduceMedia', roomId, kind, {
                type: pc.localDescription.type,
                sdp: pc.localDescription.sdp
            });

            // Store peer connection
            this.peerConnections.set(`producer-${kind}`, pc);

        } catch (err) {
            console.error(`Error producing ${kind}:`, err);
            throw err;
        }
    }

    async handleProducerCreated(data) {
        const { producerId, kind, answer } = data;
        
        const pc = this.peerConnections.get(`producer-${kind}`);
        if (pc) {
            await pc.setRemoteDescription(new RTCSessionDescription(answer));
            this.producers.set(producerId, kind);
            console.log(`${kind} producer created with ID: ${producerId}`);
        }
    }

    // Consume media from another user
    async consumeMedia(producerId) {
        try {
            await this.connection.invoke('ConsumeMedia', this.currentRoomId, producerId);
        } catch (err) {
            console.error('Error consuming media:', err);
        }
    }

    async handleConsumerCreated(data) {
        const { consumerId, producerId, kind, offer } = data;

        try {
            // Create peer connection for consuming
            const pc = new RTCPeerConnection(this.configuration);

            // Handle incoming tracks
            pc.ontrack = (event) => {
                console.log(`Received ${kind} track`);
                const stream = event.streams[0];
                this.consumers.set(consumerId, stream);
                this.onRemoteStream(stream, producerId, kind);
            };

            // Set remote description (offer from server)
            await pc.setRemoteDescription(new RTCSessionDescription(offer));

            // Create answer
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);

            // Wait for ICE gathering
            await new Promise(resolve => {
                if (pc.iceGatheringState === 'complete') {
                    resolve();
                } else {
                    pc.addEventListener('icegatheringstatechange', () => {
                        if (pc.iceGatheringState === 'complete') {
                            resolve();
                        }
                    });
                }
            });

            // Send answer to server
            await this.connection.invoke('ConsumerAnswer', this.currentRoomId, consumerId, {
                type: pc.localDescription.type,
                sdp: pc.localDescription.sdp
            });

            this.peerConnections.set(`consumer-${consumerId}`, pc);

        } catch (err) {
            console.error('Error handling consumer creation:', err);
        }
    }

    // Pause/resume producers
    async pauseProducer(roomId, producerId) {
        await this.connection.invoke('PauseProducer', roomId, producerId);
        
        const kind = this.producers.get(producerId);
        if (kind && this.localStream) {
            const tracks = kind === 'audio' ? this.localStream.getAudioTracks() : this.localStream.getVideoTracks();
            tracks.forEach(track => track.enabled = false);
        }
    }

    async resumeProducer(roomId, producerId) {
        await this.connection.invoke('ResumeProducer', roomId, producerId);
        
        const kind = this.producers.get(producerId);
        if (kind && this.localStream) {
            const tracks = kind === 'audio' ? this.localStream.getAudioTracks() : this.localStream.getVideoTracks();
            tracks.forEach(track => track.enabled = true);
        }
    }

    async closeProducer(roomId, producerId) {
        await this.connection.invoke('CloseProducer', roomId, producerId);
        
        const kind = this.producers.get(producerId);
        const pc = this.peerConnections.get(`producer-${kind}`);
        if (pc) {
            pc.close();
            this.peerConnections.delete(`producer-${kind}`);
        }
        this.producers.delete(producerId);
    }

    // Send chat message
    async sendMessage(roomId, message) {
        await this.connection.invoke('SendStreamMessage', roomId, message);
    }

    // Leave stream
    async leaveStream(roomId) {
        // Close all peer connections
        this.peerConnections.forEach(pc => pc.close());
        this.peerConnections.clear();

        // Stop local tracks
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop());
            this.localStream = null;
        }

        // Clear producers and consumers
        this.producers.clear();
        this.consumers.clear();

        await this.connection.invoke('LeaveStream', roomId);
    }

    // End stream (host only)
    async endStream(roomId) {
        await this.connection.invoke('EndStream', roomId);
    }

    // Legacy WebRTC signaling handlers (for peer-to-peer)
    async handleReceiveOffer(data) {
        const { fromUserId, offer, roomId } = data;
        
        const pc = new RTCPeerConnection(this.configuration);
        this.peerConnections.set(fromUserId, pc);

        pc.ontrack = (event) => {
            this.onRemoteStream(event.streams[0], fromUserId);
        };

        pc.onicecandidate = (event) => {
            if (event.candidate) {
                this.connection.invoke('SendICECandidate', roomId, fromUserId, {
                    candidate: event.candidate.candidate,
                    sdpMid: event.candidate.sdpMid,
                    sdpMLineIndex: event.candidate.sdpMLineIndex
                });
            }
        };

        await pc.setRemoteDescription(new RTCSessionDescription(offer));
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);

        await this.connection.invoke('SendAnswer', roomId, fromUserId, {
            type: answer.type,
            sdp: answer.sdp
        });
    }

    async handleReceiveAnswer(data) {
        const { fromUserId, answer } = data;
        const pc = this.peerConnections.get(fromUserId);
        if (pc) {
            await pc.setRemoteDescription(new RTCSessionDescription(answer));
        }
    }

    async handleReceiveICECandidate(data) {
        const { fromUserId, candidate } = data;
        const pc = this.peerConnections.get(fromUserId);
        if (pc) {
            await pc.addIceCandidate(new RTCIceCandidate(candidate));
        }
    }

    // Event callbacks (override these in your implementation)
    onStreamStarted(data) {}
    onJoinedStream(data) {}
    onUserJoined(data) {}
    onUserLeft(data) {}
    onMessageReceived(data) {}
    onStreamEnded(data) {}
    onError(data) {}
    onProducerPaused(data) {}
    onProducerResumed(data) {}
    onProducerClosed(data) {}
    onRemoteStream(stream, producerId, kind) {
        console.log('Received remote stream:', { producerId, kind });
    }

    disconnect() {
        if (this.connection) {
            this.connection.stop();
        }
    }
}

// Usage example:
/*
const client = new StreamingClient('https://your-api.com', 'your-jwt-token');

// Connect
await client.connect();

// Start a stream as host
await client.startStream('username', 123, 'My Stream', 'Description', 'Gaming', 'public', 'video');

// Start producing video and audio
const localStream = await client.startProducing('ROOM_ID', { video: true, audio: true });
document.getElementById('localVideo').srcObject = localStream;

// Handle remote streams
client.onRemoteStream = (stream, producerId, kind) => {
    const videoElement = document.createElement('video');
    videoElement.srcObject = stream;
    videoElement.autoplay = true;
    videoElement.id = producerId;
    document.getElementById('remoteVideos').appendChild(videoElement);
};

// Join a stream as viewer
await client.joinStream('ROOM_ID', 456, 'viewer_username');

// Send a message
await client.sendMessage('ROOM_ID', 'Hello everyone!');

// Leave the stream
await client.leaveStream('ROOM_ID');
*/