using Shared.Data.Extensions;


namespace Account.Infrastructure.QueryObjects
{
    public class UserQueryObject : QueryObject<Shared.Data.Models.User>
    {
        public UserQueryObject(UserFilterDto filter)
        {
            if (filter == null)
                And(u => !u.IsDeleted);

            And(u => !u.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.UserStatusCode))
                And(u => u.UserStatus.Code == filter.UserStatusCode);

            if (!string.IsNullOrWhiteSpace(filter.RoleName))
                And(u => u.UserRoles.Select(x => x.Role.Name).Contains(filter.RoleName));

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                And(u => u.FirstName.Contains(filter.SearchText)
                  || u.LastName.Contains(filter.SearchText));
            }
        }
    }
}
