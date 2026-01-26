using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace wenu.Resopones
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}