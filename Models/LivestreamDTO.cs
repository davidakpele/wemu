using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Models
{
    public class StartLiveStreamRequest
    {
        [Required]
        [MaxLength(200)]
        public required string Title { get; set; }
        
        [MaxLength(1000)]
        public string Description { get; set; }
        
        public bool IsAudioEnabled { get; set; } = true;
        
        public bool IsVideoEnabled { get; set; } = true;
    }

    public class JoinLiveStreamRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
    }
    
    public class SendMessageRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; }
    }
    
    public class InviteCoHostRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        public bool CanRemoveParticipants { get; set; } = false;
    }
    
    public class RemoveCoHostRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [Required]
        public Guid CoHostId { get; set; }
    }

    public class BlockParticipantRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [MaxLength(500)]
        public string Reason { get; set; }
    }
    
    public class RemoveParticipantRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
    }

    public class EndLiveStreamRequest
    {
        [Required]
        public Guid LiveStreamId { get; set; }
    }

     public class LiveStreamResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public UserBasicInfo Host { get; set; }
        public DateTime StartedAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsAudioEnabled { get; set; }
        public bool IsVideoEnabled { get; set; }
        public int ViewerCount { get; set; }
        public string StreamUrl { get; set; }
        public List<ParticipantInfo> Participants { get; set; }
        public List<CoHostInfo> CoHosts { get; set; }
    }
    
    public class UserBasicInfo
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string ProfilePictureUrl { get; set; }
    }

    public class ParticipantInfo
    {
        public Guid Id { get; set; }
        public UserBasicInfo User { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsCurrentlyWatching { get; set; }
    }
    
    public class CoHostInfo
    {
        public Guid Id { get; set; }
        public UserBasicInfo User { get; set; }
        public DateTime InvitedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public bool IsActive { get; set; }
        public bool CanSpeak { get; set; }
        public bool CanEnableVideo { get; set; }
        public bool CanRemoveParticipants { get; set; }
    }

    public class MessageResponse
    {
        public Guid Id { get; set; }
        public UserBasicInfo User { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsSystemMessage { get; set; }
        public string MessageType { get; set; }
    }
}