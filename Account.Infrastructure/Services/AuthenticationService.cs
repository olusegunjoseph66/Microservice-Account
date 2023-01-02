﻿using Account.Application.DTOs.JWT;
using Account.Application.Interfaces.Services;
using Account.Application.ViewModels.Responses;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Shared.Data.Models;
using Shared.Data.Repository;
using Shared.ExternalServices.Interfaces;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Account.Application.ViewModels.Requests;
using Account.Application.Exceptions;
using Account.Application.Constants;
using Account.Application.Enums;
using Shared.Utilities.Helpers;
using Account.Application.Configurations;
using Account.Application.DTOs.Features.Login;
using Newtonsoft.Json.Linq;
using Account.Application.DTOs.APIDataFormatters;
using Account.Application.DTOs.Events;
using Account.Application.ViewModels.Responses.ResponseDto;
using Account.Application.DTOs;

namespace Account.Infrastructure.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IAsyncRepository<User> _userRepository;
        private readonly IAsyncRepository<UserLogin> _userLoginRepository;
        public readonly IMessagingService _messageBus;
        private readonly ICachingService _cache;
        private readonly JWTSettings _jwtSetting;
        private readonly JwtIssuerOptions _jwtOptions;
        private readonly PasswordSettings _passwordSetting;

        public AuthenticationService(IOptions<JWTSettings> jwtSetting,
            IAsyncRepository<User> userRepository, ICachingService cache, IOptions<PasswordSettings> passwordSetting,
            IAsyncRepository<UserLogin> userLoginRepository,
            IMessagingService messageBus)
        {
            _jwtSetting = jwtSetting.Value;
            _passwordSetting = passwordSetting.Value;

            _messageBus = messageBus;
            _cache = cache;
            _userRepository = userRepository;
            _userLoginRepository = userLoginRepository;
            _jwtOptions = new JwtIssuerOptions();
        }

        public async Task<ApiResponse> ValidateUser(AuthenticationRequest authenticationRequest, CancellationToken cancellationToken)
        {
            var user = await _userRepository.Table.Where(u => u.UserName == authenticationRequest.UserName && !u.IsDeleted).Select(x => new User
            {
                Id = x.Id,
                DateCreated = x.DateCreated,
                EmailAddress = x.EmailAddress,
                UserName = x.UserName,
                FirstName = x.FirstName,
                LastName = x.LastName,
                LockedOutDate = x.LockedOutDate,
                Password = x.Password,
                PasswordExpiryDate = x.PasswordExpiryDate,
                PasswordResetRequired = x.PasswordResetRequired,
                PhoneNumber = x.PhoneNumber,
                UserStatus = new UserStatus
                {
                    Id = x.UserStatus.Id,
                    Name = x.UserStatus.Name
                },
                ResetToken = x.ResetToken,
                UserRoles = x.UserRoles.Select(r => new UserRole
                {
                    Role = new Shared.Data.Models.Role
                    {
                        Id = r.Role.Id,
                        Name = r.Role.Name
                    }
                }).ToList(),
                UserLogins = x.UserLogins.Count != 0 ? x.UserLogins.Select(x => new UserLogin
                {
                    LoginDate = x.LoginDate
                }).ToList() : new List<UserLogin>()
            }).FirstOrDefaultAsync(cancellationToken); 

            if (user == null)
                throw new UnauthorizedUserException(ErrorMessages.USERNAME_PASSWORD_NOT_EXIST, ErrorCodes.USERNAME_PASSWORD_NOT_EXIST);

            else if (user.PasswordExpiryDate < DateTime.UtcNow || user.UserStatusId == (int)UserStatusEnum.Expired)
                throw new UnauthorizedUserException(ErrorMessages.ACCOUNT_EXPIRED_RESET_PASSWORD, ErrorCodes.ACCOUNT_EXPIRED_RESET_PASSWORD);

            else if (user.UserStatusId == (int)UserStatusEnum.Inactive)
                throw new UnauthorizedUserException(ErrorMessages.ACCOUNT_DISABLED_RESET_PASSWORD, ErrorCodes.ACCOUNT_LOCKED_RESET_PASSWORD);

            else if (user.UserStatusId == (int)UserStatusEnum.Locked)
                throw new UnauthorizedUserException(ErrorMessages.ACCOUNT_LOCKED_RESET_PASSWORD, ErrorCodes.ACCOUNT_LOCKED_RESET_PASSWORD);

            var passwordIsValid = EncryptionHelper.Verify(authenticationRequest.Password, user.Password);
            if (!passwordIsValid)
            {
                var key = $"{CacheKeys.PASSWORD_FAILURE_ATTEMPT}{user.Id}";
                var cacheResponse = await _cache.GetAsync(key, cancellationToken);
                int passwordAttempts = 0;

                if (cacheResponse == null)
                {
                    passwordAttempts = 1;
                    LoginCacheDto cacheData = new(user.Id, passwordAttempts);
                    await _cache.SetAsync(key, cacheData, TimeSpan.FromMinutes(5),cancellationToken: cancellationToken);
                }
                else
                {
                    var jObjectResponse = (JObject)cacheResponse;
                    LoginCacheDto cacheData = (LoginCacheDto)jObjectResponse.ToObject(typeof(LoginCacheDto));
                    passwordAttempts = cacheData.PasswordAttempts + 1;
                    var newData = new LoginCacheDto(cacheData.UserId, passwordAttempts);
                    await _cache.UpdateAsync(key, newData, requiresTimeReset: false, cancellationToken: cancellationToken);
                }

                if (passwordAttempts > _passwordSetting.PasswordAttemptedTries)
                {
                    user.UserStatusId = (int)UserStatusEnum.Locked;

                    _userRepository.Update(user);
                    await _userRepository.CommitAsync(cancellationToken);
                }

                throw new UnauthorizedUserException(ErrorMessages.USERNAME_PASSWORD_NOT_EXIST, ErrorCodes.USERNAME_PASSWORD_NOT_EXIST);
            }
            else
            {
                if (user.UserRoles.FirstOrDefault().Role.Id != (int)RoleStatusEnum.Distributor)
                    throw new UnauthorizedUserException(ErrorMessages.UNAUTHORIZED_ACCESS, ErrorCodes.UNAUTHORIZED_ACCESS);
                else
                    await _userLoginRepository.AddAsync(new UserLogin()
                    {
                        UserId = user.Id,
                        DeviceId = authenticationRequest.DeviceId,
                        IpAddress = authenticationRequest.IpAddress,
                        ChannelCode = authenticationRequest.ChannelCode.ToString(),
                        LoginDate = DateTime.UtcNow
                    }, cancellationToken);
            }

            var identity = GenerateClaimsIdentity(user);
            var jwtToken = await GenerateJwt(identity, user.UserName, new JsonSerializerSettings { Formatting = Formatting.None });

            var authToken = JsonConvert.DeserializeObject<TokenResponse>(jwtToken);

            var role = user.UserRoles.FirstOrDefault().Role;
            AuthenticationResponse response = new(authToken.AuthToken, new UserDto
            {
                EmailAddress = user.EmailAddress,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                LastLoginDate = user.UserLogins.Count == 0 ? null : user.UserLogins.OrderByDescending(x => x.LoginDate).FirstOrDefault().LoginDate
            });
            await _userLoginRepository.CommitAsync(cancellationToken);

            //Azure ServiceBus
            UserLoginMessage adminUserLoginMessage = new()
            {
                LoginDate = _userLoginRepository.Table.Where(ul => ul.UserId == user.Id).FirstOrDefault().LoginDate,
                ChannelCode = authenticationRequest.ChannelCode.ToString(),
                DeviceId = authenticationRequest.DeviceId,
                IpAddress = authenticationRequest.IpAddress,
                UserId = user.Id,
                DateCreated = user.DateCreated
            };

            await _messageBus.PublishTopicMessage(adminUserLoginMessage, EventMessages.ACCOUNT_USER_LOGIN);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_LOGIN_OF_USER, SuccessCodes.DEFAULT_SUCCESS_CODE, response);
        }

        public async Task<ApiResponse> ValidateAdminUser(AuthenticationRequest authenticationRequest, CancellationToken cancellationToken)
        {
            var user = await _userRepository.Table.Where(u => u.UserName == authenticationRequest.UserName && !u.IsDeleted).Select(x => new User
            {
                Id = x.Id,
                DateCreated = x.DateCreated,
                EmailAddress = x.EmailAddress,
                UserName = x.UserName,
                FirstName = x.FirstName,
                LastName = x.LastName,
                LockedOutDate = x.LockedOutDate,
                Password = x.Password,
                PasswordExpiryDate = x.PasswordExpiryDate,
                PasswordResetRequired = x.PasswordResetRequired,
                PhoneNumber = x.PhoneNumber,
                UserStatus = new UserStatus
                {
                    Id = x.UserStatus.Id,
                    Name = x.UserStatus.Name
                },
                ResetToken = x.ResetToken,
                UserRoles = x.UserRoles.Select(r => new UserRole
                {
                    RoleId = r.RoleId,
                    UserId = r.UserId,
                    Role = new Role
                    {
                        Id = r.Role.Id,
                        Name = r.Role.Name
                    }
                }).ToList()
            }).FirstOrDefaultAsync(cancellationToken);

            if (user == null)
                throw new UnauthorizedUserException(ErrorMessages.USERNAME_PASSWORD_NOT_EXIST, ErrorCodes.USERNAME_PASSWORD_NOT_EXIST);

            else if (user.PasswordExpiryDate < DateTime.UtcNow || user.UserStatusId == (int)UserStatusEnum.Expired)
                throw new UnauthorizedUserException(ErrorMessages.ACCOUNT_EXPIRED_RESET_PASSWORD, ErrorCodes.ACCOUNT_EXPIRED_RESET_PASSWORD);

            else if (user.UserStatusId == (int)UserStatusEnum.Inactive)
                throw new UnauthorizedUserException(ErrorMessages.ACCOUNT_DISABLED_RESET_PASSWORD, ErrorCodes.ACCOUNT_DISABLED_RESET_PASSWORD);

            else if (user.UserStatusId == (int)UserStatusEnum.Locked)
                throw new UnauthorizedUserException(ErrorMessages.ACCOUNT_LOCKED_RESET_PASSWORD, ErrorCodes.ACCOUNT_LOCKED_RESET_PASSWORD);

            var passwordIsValid = EncryptionHelper.Verify(authenticationRequest.Password, user.Password);
            var key = $"{CacheKeys.PASSWORD_FAILURE_ATTEMPT}{user.Id}";
            if (!passwordIsValid)
            {
                var cacheResponse = await _cache.GetAsync(key, cancellationToken);
                int passwordAttempts = 0;

                if (cacheResponse == null)
                {
                    passwordAttempts = 1;
                    LoginCacheDto cacheData = new(user.Id, passwordAttempts);
                    await _cache.SetAsync(key, cacheData, TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                }
                else
                {
                    var jObjectResponse = (JObject)cacheResponse;
                    LoginCacheDto cacheData = (LoginCacheDto)jObjectResponse.ToObject(typeof(LoginCacheDto));
                    passwordAttempts = cacheData.PasswordAttempts + 1;
                    var newData = new LoginCacheDto(cacheData.UserId, passwordAttempts);
                    await _cache.UpdateAsync(key, newData, requiresTimeReset: false, cancellationToken: cancellationToken);
                }

                if (passwordAttempts > _passwordSetting.PasswordAttemptedTries)
                {
                    user.UserStatusId = (int)UserStatusEnum.Locked;

                    _userRepository.Update(user);
                    await _userRepository.CommitAsync(cancellationToken);
                }

                throw new UnauthorizedUserException(ErrorMessages.USERNAME_PASSWORD_NOT_EXIST, ErrorCodes.USERNAME_PASSWORD_NOT_EXIST);
            }

            var roleStatuses = Enum.GetValues(typeof(RoleStatusEnum)).Cast<RoleStatusEnum>()
                   .Select(e => new NameAndId<byte>((byte)e, e.ToDescription())).ToList();

            var userRoles = user.UserRoles.Select(x => x.RoleId).ToList();
            if(!userRoles.Any(x => x == (short)RoleStatusEnum.SuperAdministrator || x == (short)RoleStatusEnum.Administrator))
                throw new UnauthorizedUserException(ErrorMessages.UNAUTHORIZED_ACCESS, ErrorCodes.UNAUTHORIZED_ACCESS);

            await _userLoginRepository.AddAsync(new UserLogin()
            {
                UserId = user.Id,
                DeviceId = authenticationRequest.DeviceId,
                IpAddress = authenticationRequest.IpAddress,
                ChannelCode = authenticationRequest.ChannelCode.ToString(),
                LoginDate = DateTime.UtcNow
            }, cancellationToken);
            var identity = GenerateClaimsIdentity(user);
            var jwtToken = await GenerateJwt(identity, user.UserName, new JsonSerializerSettings { Formatting = Formatting.None });

            var authToken = JsonConvert.DeserializeObject<TokenResponse>(jwtToken);

            var role = user.UserRoles.FirstOrDefault().Role;
            AuthenticationResponse response = new(authToken.AuthToken, new UserDto
            {
                EmailAddress = user.EmailAddress,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName,
                LastLoginDate = user.UserLogins.Count == 0 ? null : user.UserLogins.OrderByDescending(x => x.LoginDate).FirstOrDefault().LoginDate
            });
            await _userLoginRepository.CommitAsync(cancellationToken);

            //Azure ServiceBus
            UserLoginMessage adminUserLoginMessage = new()
            {
                LoginDate = _userLoginRepository.Table.Where(ul => ul.UserId == user.Id).FirstOrDefault().LoginDate,
                ChannelCode = authenticationRequest.ChannelCode.ToString(),
                DeviceId = authenticationRequest.DeviceId,
                IpAddress = authenticationRequest.IpAddress,
                UserId = user.Id,
                DateCreated = user.DateCreated
            };

            await _messageBus.PublishTopicMessage(adminUserLoginMessage, EventMessages.ACCOUNT_USER_LOGIN);

            await _cache.RemoveAsync(key, cancellationToken);
            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_LOGIN_OF_USER, SuccessCodes.DEFAULT_SUCCESS_CODE, response);
        }

        #region Private Methods
        private static ClaimsIdentity GenerateClaimsIdentity(User user)
        {
            return new ClaimsIdentity(new GenericIdentity(user.UserName, "Token"), new[]
            {
                new Claim(JwtClaimIdentifiers.UserId, user.Id.ToString()),
                new Claim(JwtClaimIdentifiers.FirstName, user.FirstName),
                new Claim(JwtClaimIdentifiers.LastName, user.LastName),
                new Claim(JwtClaimIdentifiers.UserName, user.UserName),
                new Claim(JwtClaimIdentifiers.EmailAddress, user.EmailAddress),
                new Claim(JwtClaimIdentifiers.PhoneNumber, user.PhoneNumber),
                new Claim(JwtClaimIdentifiers.Role, user.UserRoles.FirstOrDefault().Role.Name)
            });
        }

        private async Task<string> GenerateJwt(ClaimsIdentity identity, string userName, JsonSerializerSettings serializerSettings)
        {
            var elapsedTime = DateTime.UtcNow.AddMinutes(_jwtSetting.DurationInMinutes) - DateTime.UtcNow;
            var response = new
            {
                userId = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.UserId).Value,
                roleName = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.Role).Value,
                userName = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.UserName).Value,
                firstName = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.FirstName).Value,
                lastName = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.LastName).Value,
                emailAddress = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.EmailAddress).Value,
                phoneNumber = identity.Claims.Single(c => c.Type == JwtClaimIdentifiers.PhoneNumber).Value,

                authToken = await GenerateEncodedToken(userName, identity),
                expiresIn = (int)elapsedTime.TotalSeconds,
                refreshToken = Guid.NewGuid().ToString()
            };

            return JsonConvert.SerializeObject(response, serializerSettings);
        }

        private static long ToUnixEpochDate(DateTime date)
          => (long)Math.Round((date -
                               new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero))
                              .TotalSeconds);

        private async Task<string> GenerateEncodedToken(string userName, ClaimsIdentity identity)
        {
            var key = Encoding.ASCII.GetBytes(_jwtSetting.SecretKey);
            var claims = new[]
            {
                 new Claim(JwtRegisteredClaimNames.Sub, userName),
                 new Claim(JwtRegisteredClaimNames.Jti, await _jwtOptions.JtiGenerator()),
                 new Claim(JwtRegisteredClaimNames.Iat, ToUnixEpochDate(_jwtOptions.IssuedAt).ToString(), ClaimValueTypes.Integer64),
                 identity.FindFirst(JwtClaimIdentifiers.Role),
                 identity.FindFirst(JwtClaimIdentifiers.UserId),
                 identity.FindFirst(JwtClaimIdentifiers.UserName),
                 identity.FindFirst(JwtClaimIdentifiers.FirstName),
                 identity.FindFirst(JwtClaimIdentifiers.LastName),
                 identity.FindFirst(JwtClaimIdentifiers.EmailAddress),
                 identity.FindFirst(JwtClaimIdentifiers.PhoneNumber)
            };

            var jwt = new JwtSecurityToken(
                issuer: _jwtSetting.Issuer,
                audience: _jwtSetting.Audience,
                claims: claims,
                notBefore: _jwtOptions.NotBefore,
                expires: DateTime.UtcNow.AddDays(_jwtSetting.DurationInMinutes),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature));

            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
            return encodedJwt;
        }
        #endregion
    }
}
