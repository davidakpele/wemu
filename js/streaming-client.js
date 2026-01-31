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
        this.currentRoomId = null; // Track current room
        
        this.configuration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
                { urls: 'stun:stun2.l.google.com:19302' },
                { urls: 'stun:stun3.l.google.com:19302' },
                { urls: 'stun:stun4.l.google.com:19302' }
            ],
            iceCandidatePoolSize: 10,
            bundlePolicy: 'max-bundle',
            rtcpMuxPolicy: 'require',
            iceTransportPolicy: 'all'
        };
        
        // Audio constraints for better quality
        this.audioConstraints = {
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true,
            sampleRate: 48000,
            channelCount: 1,
            sampleSize: 16
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
            this.onNewProducer(data);
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

        // New peer-to-peer signaling
        this.connection.on('ReceiveOfferFromConsumer', async (data) => {
            await this.handleOfferFromConsumer(data);
        });

        this.connection.on('ReceiveAnswerFromProducer', async (data) => {
            await this.handleAnswerFromProducer(data);
        });

        this.connection.on('ReceiveIceCandidateFromConsumer', async (data) => {
            await this.handleIceCandidateFromConsumer(data);
        });

        this.connection.on('ReceiveIceCandidateFromProducer', async (data) => {
            await this.handleIceCandidateFromProducer(data);
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
            // Enhanced constraints for better quality
            const enhancedConstraints = {
                audio: constraints.audio ? this.audioConstraints : false,
                video: constraints.video ? {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    frameRate: { ideal: 30, max: 30 }
                } : false
            };
            
            // Get local media stream
            this.localStream = await navigator.mediaDevices.getUserMedia(enhancedConstraints);
            
            console.log('Got media stream with tracks:', this.localStream.getTracks().map(t => `${t.kind}: ${t.label}`));
            
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
            // Store track for when consumers request it
            if (!this.producerTracks) {
                this.producerTracks = new Map();
            }
            this.producerTracks.set(kind, track);

            // Store the room ID
            this.currentRoomId = roomId;

            // Notify server (no SDP needed, just announcing availability)
            await this.connection.invoke('ProduceMedia', roomId, kind, {
                type: 'offer',
                sdp: '' // Not needed for peer-to-peer
            });

            console.log(`Stored ${kind} track for production`);

        } catch (err) {
            console.error(`Error producing ${kind}:`, err);
            throw err;
        }
    }

    async handleProducerCreated(data) {
        const { producerId, kind } = data;
        this.producers.set(producerId, kind);
        console.log(`${kind} producer created with ID: ${producerId}`);
    }

    // Consume media from another user
    async consumeMedia(roomIdOrProducerId, producerIdParam = null) {
        try {
            let roomId, producerId;
            
            if (producerIdParam) {
                roomId = roomIdOrProducerId;
                producerId = producerIdParam;
            } else {
                roomId = this.currentRoomId;
                producerId = roomIdOrProducerId;
            }
            
            if (!roomId) {
                console.error('No room ID available for consuming media');
                return;
            }
            
            console.log(`Requesting to consume media: roomId=${roomId}, producerId=${producerId}`);
            await this.connection.invoke('ConsumeMedia', roomId, producerId);
        } catch (err) {
            console.error('Error consuming media:', err);
        }
    }

    async handleConsumerCreated(data) {
        const { consumerId, producerId, producerUserId, kind } = data;

        try {
            console.log(`Creating consumer for ${kind} from producer ${producerUserId}`);
            
            // Check if we already have a peer connection to this producer
            let pc = this.consumerPcMapping?.get(producerUserId);
            let isNewConnection = false;
            
            if (!pc) {
                // Create NEW peer connection for this producer
                pc = new RTCPeerConnection(this.configuration);
                isNewConnection = true;
                console.log('Created NEW peer connection for producer', producerUserId);
                
                // Handle incoming tracks
                pc.ontrack = (event) => {
                    console.log(`✓ Received track from producer ${producerUserId}`, event.track.kind, event);
                    const stream = event.streams[0];
                    
                    // Store or update the stream
                    let existingVideoElement = document.getElementById(`remote-${producerUserId}`);
                    if (existingVideoElement && existingVideoElement.srcObject) {
                        // Add track to existing stream
                        console.log('Adding track to existing stream');
                        const existingStream = existingVideoElement.srcObject;
                        existingStream.addTrack(event.track);
                    } else {
                        // New stream
                        console.log('Creating new stream for producer');
                        this.onRemoteStream(stream, producerUserId, event.track.kind);
                    }
                };

                // Handle ICE candidates
                pc.onicecandidate = (event) => {
                    if (event.candidate) {
                        console.log('Sending ICE candidate to producer');
                        this.connection.invoke('SendIceCandidateToProducer', this.currentRoomId, producerUserId, {
                            candidate: event.candidate.candidate,
                            sdpMid: event.candidate.sdpMid,
                            sdpMLineIndex: event.candidate.sdpMLineIndex
                        }).catch(console.error);
                    }
                };

                // Monitor connection states
                pc.onconnectionstatechange = () => {
                    console.log(`Consumer connection state to ${producerUserId}:`, pc.connectionState);
                };

                pc.oniceconnectionstatechange = () => {
                    console.log(`Consumer ICE state to ${producerUserId}:`, pc.iceConnectionState);
                };
                
                // Store mapping
                if (!this.consumerPcMapping) {
                    this.consumerPcMapping = new Map();
                }
                this.consumerPcMapping.set(producerUserId, pc);
                
                // Initialize pending candidates queue
                if (!this.pendingIceCandidates) {
                    this.pendingIceCandidates = new Map();
                }
                this.pendingIceCandidates.set(producerUserId, {
                    candidates: [],
                    remoteDescriptionSet: false
                });
            } else {
                console.log('Reusing existing peer connection for producer', producerUserId);
            }

            // Add transceiver for the requested media type
            console.log(`Adding ${kind} transceiver`);
            pc.addTransceiver(kind, { direction: 'recvonly' });

            // Only create offer if this is a new connection or if we need to renegotiate
            if (isNewConnection || pc.signalingState === 'stable') {
                // Create offer to receive both audio and video
                const offer = await pc.createOffer({
                    offerToReceiveAudio: true,
                    offerToReceiveVideo: true
                });
                await pc.setLocalDescription(offer);
                console.log(`Created offer for ${kind} (receives both audio and video)`);

                // Wait for ICE gathering
                await new Promise((resolve) => {
                    if (pc.iceGatheringState === 'complete') {
                        console.log('ICE gathering complete');
                        resolve();
                    } else {
                        console.log('Waiting for ICE gathering...');
                        const timeout = setTimeout(() => {
                            console.log('ICE gathering timeout - proceeding');
                            resolve();
                        }, 3000);
                        
                        pc.addEventListener('icegatheringstatechange', () => {
                            console.log('ICE gathering state:', pc.iceGatheringState);
                            if (pc.iceGatheringState === 'complete') {
                                clearTimeout(timeout);
                                resolve();
                            }
                        });
                    }
                });

                // Send offer to producer
                console.log('Sending offer to producer...');
                await this.connection.invoke('SendOfferToProducer', this.currentRoomId, producerUserId, {
                    type: pc.localDescription.type,
                    sdp: pc.localDescription.sdp
                });
            }

            // Store consumer
            this.peerConnections.set(`consumer-${consumerId}`, pc);
            this.consumers.set(consumerId, { kind, producerUserId });
            
            console.log(`Consumer setup complete for ${kind} from producer ${producerUserId}`);

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

    // New peer-to-peer signaling handlers
    async handleOfferFromConsumer(data) {
        const { consumerUserId, consumerConnectionId, offer } = data;
        
        try {
            console.log(`Received offer from consumer ${consumerUserId}`, offer);
            
            // Create peer connection for this consumer
            const pc = new RTCPeerConnection(this.configuration);

            // Initialize pending ICE candidates queue for this consumer
            if (!this.producerPendingIce) {
                this.producerPendingIce = new Map();
            }
            this.producerPendingIce.set(consumerConnectionId, {
                candidates: [],
                remoteDescriptionSet: false
            });

            // CRITICAL: Add our local stream tracks to this connection
            if (this.localStream) {
                console.log('Adding tracks to peer connection:', this.localStream.getTracks().length);
                this.localStream.getTracks().forEach(track => {
                    console.log(`Adding ${track.kind} track:`, track.id);
                    pc.addTrack(track, this.localStream);
                });
            } else {
                console.error('No local stream available to send to consumer!');
            }

            // Handle ICE candidates
            pc.onicecandidate = (event) => {
                if (event.candidate) {
                    console.log('Sending ICE candidate to consumer');
                    this.connection.invoke('SendIceCandidateToConsumer', this.currentRoomId, consumerConnectionId, {
                        candidate: event.candidate.candidate,
                        sdpMid: event.candidate.sdpMid,
                        sdpMLineIndex: event.candidate.sdpMLineIndex
                    }).catch(console.error);
                }
            };

            // Monitor connection state
            pc.onconnectionstatechange = () => {
                console.log(`Connection state to consumer ${consumerConnectionId}:`, pc.connectionState);
            };

            pc.oniceconnectionstatechange = () => {
                console.log(`ICE connection state to consumer ${consumerConnectionId}:`, pc.iceConnectionState);
            };

            // Set remote description (offer from consumer)
            await pc.setRemoteDescription(new RTCSessionDescription(offer));
            console.log('Set remote description (offer)');
            
            // Mark that remote description is set
            const pending = this.producerPendingIce.get(consumerConnectionId);
            if (pending) {
                pending.remoteDescriptionSet = true;
                
                // Process any pending ICE candidates
                console.log(`Processing ${pending.candidates.length} pending ICE candidates from consumer`);
                for (const candidate of pending.candidates) {
                    try {
                        await pc.addIceCandidate(new RTCIceCandidate(candidate));
                        console.log('Added pending ICE candidate from consumer');
                    } catch (err) {
                        console.error('Error adding pending ICE candidate:', err);
                    }
                }
                pending.candidates = [];
            }

            // Create answer
            const answer = await pc.createAnswer();
            
            // Optimize audio quality in answer
            answer.sdp = this.setOpusPreferred(answer.sdp);
            answer.sdp = this.setAudioBitrate(answer.sdp, 128);
            
            await pc.setLocalDescription(answer);
            console.log('Created and set local description (answer)');

            // Wait for ICE gathering
            await new Promise((resolve) => {
                if (pc.iceGatheringState === 'complete') {
                    console.log('ICE gathering already complete');
                    resolve();
                } else {
                    console.log('Waiting for ICE gathering...');
                    const timeout = setTimeout(() => {
                        console.log('ICE gathering timeout - proceeding anyway');
                        resolve();
                    }, 3000);
                    
                    pc.addEventListener('icegatheringstatechange', () => {
                        console.log('ICE gathering state:', pc.iceGatheringState);
                        if (pc.iceGatheringState === 'complete') {
                            clearTimeout(timeout);
                            resolve();
                        }
                    });
                }
            });

            // Send answer to consumer
            await this.connection.invoke('SendAnswerToConsumer', this.currentRoomId, consumerConnectionId, {
                type: pc.localDescription.type,
                sdp: pc.localDescription.sdp
            });
            console.log('Sent answer to consumer');

            // Store peer connection
            this.peerConnections.set(`to-consumer-${consumerConnectionId}`, pc);

        } catch (err) {
            console.error('Error handling offer from consumer:', err);
        }
    }

    async handleAnswerFromProducer(data) {
        const { producerUserId, answer } = data;
        
        try {
            console.log(`Received answer from producer ${producerUserId}`);
            
            const pc = this.consumerPcMapping?.get(producerUserId);
            if (pc) {
                await pc.setRemoteDescription(new RTCSessionDescription(answer));
                console.log('Answer set successfully');
                
                // Process any pending ICE candidates
                if (this.pendingIceCandidates) {
                    const pending = this.pendingIceCandidates.get(producerUserId);
                    if (pending) {
                        pending.remoteDescriptionSet = true;
                        console.log(`Processing ${pending.candidates.length} pending ICE candidates`);
                        
                        for (const candidate of pending.candidates) {
                            try {
                                await pc.addIceCandidate(new RTCIceCandidate(candidate));
                                console.log('Added pending ICE candidate');
                            } catch (err) {
                                console.error('Error adding pending ICE candidate:', err);
                            }
                        }
                        
                        // Clear the queue
                        pending.candidates = [];
                    }
                }
            } else {
                console.error('No peer connection found for producer:', producerUserId);
            }
        } catch (err) {
            console.error('Error handling answer from producer:', err);
        }
    }

    async handleIceCandidateFromConsumer(data) {
        const { consumerConnectionId, candidate } = data;
        
        try {
            const pc = this.peerConnections.get(`to-consumer-${consumerConnectionId}`);
            if (pc && candidate.candidate) {
                // Check if remote description is set
                const pending = this.producerPendingIce?.get(consumerConnectionId);
                
                if (pending && !pending.remoteDescriptionSet) {
                    // Queue the candidate until remote description is set
                    console.log('Queueing ICE candidate from consumer (waiting for remote description)');
                    pending.candidates.push(candidate);
                } else {
                    // Remote description is set, add candidate immediately
                    console.log('Adding ICE candidate from consumer');
                    await pc.addIceCandidate(new RTCIceCandidate(candidate));
                }
            }
        } catch (err) {
            console.error('Error adding ICE candidate from consumer:', err);
        }
    }

    async handleIceCandidateFromProducer(data) {
        const { producerUserId, candidate } = data;
        
        try {
            const pc = this.consumerPcMapping?.get(producerUserId);
            if (pc && candidate.candidate) {
                // Check if remote description is set
                const pending = this.pendingIceCandidates?.get(producerUserId);
                
                if (pending && !pending.remoteDescriptionSet) {
                    // Queue the candidate until remote description is set
                    console.log('Queueing ICE candidate (waiting for remote description)');
                    pending.candidates.push(candidate);
                } else {
                    // Remote description is set, add candidate immediately
                    console.log('Adding ICE candidate from producer');
                    await pc.addIceCandidate(new RTCIceCandidate(candidate));
                }
            }
        } catch (err) {
            console.error('Error adding ICE candidate from producer:', err);
        }
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
    onNewProducer(data) {}
    onRemoteStream(stream, producerId, kind) {
        console.log('Received remote stream:', { producerId, kind });
    }

    disconnect() {
        if (this.connection) {
            this.connection.stop();
        }
    }
    
    // SDP manipulation helpers for better audio quality
    setOpusPreferred(sdp) {
        const lines = sdp.split('\r\n');
        const mLineIndex = lines.findIndex(line => line.startsWith('m=audio'));
        if (mLineIndex === -1) return sdp;
        
        const codecPattern = /a=rtpmap:(\d+) opus\/48000/;
        let opusPayloadType = null;
        
        for (let i = mLineIndex; i < lines.length; i++) {
            const match = lines[i].match(codecPattern);
            if (match) {
                opusPayloadType = match[1];
                break;
            }
            if (lines[i].startsWith('m=')) break;
        }
        
        if (opusPayloadType) {
            const mLine = lines[mLineIndex];
            const parts = mLine.split(' ');
            const codecs = parts.slice(3);
            const newCodecs = [opusPayloadType, ...codecs.filter(c => c !== opusPayloadType)];
            lines[mLineIndex] = parts.slice(0, 3).join(' ') + ' ' + newCodecs.join(' ');
        }
        
        return lines.join('\r\n');
    }
    
    setAudioBitrate(sdp, bitrate) {
        const lines = sdp.split('\r\n');
        let mLineIndex = -1;
        
        for (let i = 0; i < lines.length; i++) {
            if (lines[i].startsWith('m=audio')) {
                mLineIndex = i;
                break;
            }
        }
        
        if (mLineIndex === -1) return sdp;
        
        // Find opus payload type
        let opusPayloadType = null;
        for (let i = mLineIndex; i < lines.length; i++) {
            const match = lines[i].match(/a=rtpmap:(\d+) opus\/48000/);
            if (match) {
                opusPayloadType = match[1];
                break;
            }
            if (lines[i].startsWith('m=')) break;
        }
        
        if (opusPayloadType) {
            // Add or modify fmtp line for opus
            let fmtpLineIndex = -1;
            for (let i = mLineIndex; i < lines.length; i++) {
                if (lines[i].startsWith(`a=fmtp:${opusPayloadType}`)) {
                    fmtpLineIndex = i;
                    break;
                }
                if (lines[i].startsWith('m=')) break;
            }
            
            const bitrateKbps = bitrate * 1000;
            if (fmtpLineIndex !== -1) {
                // Modify existing fmtp line
                if (!lines[fmtpLineIndex].includes('maxaveragebitrate')) {
                    lines[fmtpLineIndex] += `;maxaveragebitrate=${bitrateKbps}`;
                }
                if (!lines[fmtpLineIndex].includes('stereo')) {
                    lines[fmtpLineIndex] += ';stereo=0';
                }
                if (!lines[fmtpLineIndex].includes('useinbandfec')) {
                    lines[fmtpLineIndex] += ';useinbandfec=1';
                }
            } else {
                // Add new fmtp line after rtpmap
                for (let i = mLineIndex; i < lines.length; i++) {
                    if (lines[i].startsWith(`a=rtpmap:${opusPayloadType}`)) {
                        lines.splice(i + 1, 0, `a=fmtp:${opusPayloadType} maxaveragebitrate=${bitrateKbps};stereo=0;useinbandfec=1`);
                        break;
                    }
                    if (lines[i].startsWith('m=')) break;
                }
            }
        }
        
        return lines.join('\r\n');
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