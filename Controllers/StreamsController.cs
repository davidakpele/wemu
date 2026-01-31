using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace wenu.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StreamsController : ControllerBase
    {
        private readonly Services.MediaServer _mediaServer;

        public StreamsController(Services.MediaServer mediaServer)
        {
            _mediaServer = mediaServer;
        }

        [HttpGet("active")]
        public IActionResult GetActiveStreams()
        {
            try
            {
                var activeStreams = Services.StreamingHub.GetActiveStreams();
                
                var streamsList = activeStreams.Select(stream => new
                {
                    roomId = stream.RoomId,
                    title = stream.Title,
                    description = stream.Description,
                    category = stream.Category,
                    visibility = stream.Visibility,
                    state = stream.State,
                    startTime = stream.StartTime,
                    currentViewers = stream.CurrentViewers,
                    host = new
                    {
                        id = stream.Host.Id,
                        username = stream.Host.Username
                    },
                    totalParticipants = stream.Participants.TotalMembers
                }).ToList();

                return Ok(streamsList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching active streams", error = ex.Message });
            }
        }

        [HttpGet("{roomId}")]
        public IActionResult GetStreamDetails(string roomId)
        {
            try
            {
                var stream = Services.StreamingHub.GetStreamRoom(roomId);
                
                if (stream == null)
                {
                    return NotFound(new { message = "Stream not found" });
                }

                var streamDetails = new
                {
                    roomId = stream.RoomId,
                    title = stream.Title,
                    description = stream.Description,
                    category = stream.Category,
                    visibility = stream.Visibility,
                    state = stream.State,
                    startTime = stream.StartTime,
                    endTime = stream.EndTime,
                    currentViewers = stream.CurrentViewers,
                    host = new
                    {
                        id = stream.Host.Id,
                        username = stream.Host.Username,
                        coHosts = stream.Host.CoHosts.Select(c => new
                        {
                            id = c.Id,
                            username = c.Username
                        })
                    },
                    participants = stream.Participants.UsersList.Select(p => new
                    {
                        id = p.Id,
                        username = p.Username,
                        role = p.Role
                    }),
                    totalParticipants = stream.Participants.TotalMembers,
                    mediaSettings = stream.MediaSettings
                };

                return Ok(streamDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching stream details", error = ex.Message });
            }
        }
    }
}