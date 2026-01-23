using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using wenu.Entities;

namespace wumo.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<Users> _userManager;

        public JwtService(IConfiguration configuration, UserManager<Users> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<string> GenerateToken(Users user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            
            var secretKey = jwtSettings["SecretKey"] 
                ?? throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json");
            var issuer = jwtSettings["Issuer"] 
                ?? throw new InvalidOperationException("JWT Issuer is not configured in appsettings.json");
            var audience = jwtSettings["Audience"] 
                ?? throw new InvalidOperationException("JWT Audience is not configured in appsettings.json");
            var expiryInMinutesStr = jwtSettings["ExpiryInMinutes"] 
                ?? throw new InvalidOperationException("JWT ExpiryInMinutes is not configured in appsettings.json");
            
            if (!int.TryParse(expiryInMinutesStr, out var expiryInMinutes))
            {
                throw new InvalidOperationException("JWT ExpiryInMinutes must be a valid integer");
            }

            if (string.IsNullOrEmpty(user.UserName!))
            {
                throw new InvalidOperationException("User username cannot be null or empty");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var roles = await _userManager.GetRolesAsync(user);

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(expiryInMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName!),
                new Claim("userId", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Nbf,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            claims.AddRange(
                roles.Select(role => new Claim(ClaimTypes.Role, role))
            );

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<ClaimsPrincipal?> ValidateToken(string token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] 
                ?? throw new InvalidOperationException("JWT SecretKey is not configured");
            var issuer = jwtSettings["Issuer"] 
                ?? throw new InvalidOperationException("JWT Issuer is not configured");
            var audience = jwtSettings["Audience"] 
                ?? throw new InvalidOperationException("JWT Audience is not configured");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero // No tolerance for expiration
                }, out SecurityToken validatedToken);

                return principal;
            }
            catch (SecurityTokenExpiredException)
            {
                throw new UnauthorizedAccessException(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "TokenExpired",
                        message = "The token has expired. Please login again."
                    })
                );
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                throw new UnauthorizedAccessException(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "InvalidToken",
                        message = "Invalid token signature."
                    })
                );
            }
            catch (SecurityTokenValidationException ex)
            {
                throw new UnauthorizedAccessException(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "InvalidToken",
                        message = $"Token validation failed: {ex.Message}"
                    })
                );
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "InvalidToken",
                        message = $"An error occurred while validating the token: {ex.Message}"
                    })
                );
            }
        }
    }
}